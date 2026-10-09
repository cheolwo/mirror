using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.정산;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCase
{
    public async Task<NeighborhoodSettlementSummaryDto> GetAsync(string id, string actor, CancellationToken ct)
    {
        Gate(actor); var row = await Owned(id, actor, ct); var terms = Terms(row); var source = Source(row);
        var current = await sources.ResolveAsync(row.SourceKind, row.SourceId, ct);
        var changed = current.SourceVersion != source.SourceVersion || current.SourceDigest != source.SourceDigest;
        var visible = terms.Lines.Where(x => LineParty(x, actor) || row.CreatorUserId == actor && (string.IsNullOrWhiteSpace(x.PayerUserId) || string.IsNullOrWhiteSpace(x.PayeeUserId))).ToArray();
        var unresolvedCount = visible.Count(x => !Resolved(x, source));
        foreach (var line in visible) line.DeliveryCostShares = line.DeliveryCostShares.Where(x => x.PayerUserId == actor).ToArray();
        terms.Lines = visible;
        var consents = Deserialize<List<NeighborhoodSettlementConsentDto>>(row.ConsentsJson);
        var required = RequiredConsents(terms);
        var agreement = new NeighborhoodSettlementAgreementDto
        {
            AgreementId = id, SourceKind = row.SourceKind, SourceId = row.SourceId, TermsVersion = row.TermsVersion, Revision = row.Revision,
            StatusCode = changed ? "ReconfirmationRequired" : row.StatusCode, Terms = terms,
            Consents = consents.Where(x => required.Contains(x.UserId, StringComparer.Ordinal)).ToArray(), RequiredConsentUserIds = required,
            CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc,
            AllowedActions = Party(current, actor) ? changed ? ["change-terms"] : RequiredConsents(Terms(row)).Contains(actor, StringComparer.Ordinal)
                ? ["change-terms", "consent", "revoke", .. (row.StatusCode == NeighborhoodSettlementCodes.Active ? new[] { "accrue" } : [])]
                : ["change-terms"] : []
        };
        var versions = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking().Where(x => x.AgreementId == id).ToArrayAsync(ct);
        var versionTerms = versions.ToDictionary(x => x.TermsVersion, x => Deserialize<NeighborhoodSettlementAgreementRequest>(x.TermsJson));
        var entries = (await db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking().Where(x => x.AgreementId == id).OrderBy(x => x.RecordedAtUtc).ThenBy(x => x.EntryId).ToArrayAsync(ct))
            .Select(Entry).Where(x => EntryParty(x, actor) || x.IncludedInDelivery && x.Kind == NeighborhoodSettlementCodes.Delivery
                && versionTerms.TryGetValue(x.TermsVersion, out var historical)
                && historical.Lines.Any(line => line.LineKey == x.LineKey && line.DeliveryCostShares.Any(s => s.PayerUserId == actor))).ToArray();
        foreach (var entry in entries)
            entry.AllowedActions = entry.IncludedInDelivery || !entry.AmountKrw.HasValue ? []
                : entry.StatusCode == NeighborhoodSettlementCodes.Disputed ? ["confirm-entry", "adjustment"]
                : ["confirm-entry", "dispute", .. (entry.OriginalEntryId is null ? new[] { "adjustment" } : [])];
        var payments = (await db.Set<NeighborhoodSettlementPaymentRecord>().AsNoTracking().Where(x => x.AgreementId == id).OrderBy(x => x.ReportedAtUtc).ToArrayAsync(ct))
            .Select(Payment).Where(x => x.PayerUserId == actor || x.PayeeUserId == actor).ToArray();
        foreach (var payment in payments)
            payment.AllowedActions = payment.PayeeUserId == actor && payment.RecipientConfirmedAmountKrw < payment.AmountKrw ? ["confirm-receipt"] : [];
        foreach (var entry in entries.Where(x => x.OriginalEntryId is not null && payments.Any(p => p.Allocations.Any(a => a.EntryId == x.EntryId))))
            entry.AllowedActions = entry.AllowedActions.Where(x => x != "dispute").ToArray();
        // 통과자금과 내부 작업은 경제 수입에 다시 더하지 않습니다. 각각의 실제 진술은 항목·지급 목록에 남습니다.
        var economic = entries.Where(x => !x.IsInternal && !x.IsTransit && !x.IncludedInDelivery).ToArray();
        var economicIds = economic.Select(x => x.EntryId).ToHashSet(StringComparer.Ordinal);
        var signs = economic.ToDictionary(x => x.EntryId, x => x.AmountKrw < 0 ? -1m : 1m, StringComparer.Ordinal);
        var reported = payments.SelectMany(x => x.Allocations).Where(x => economicIds.Contains(x.EntryId)).Sum(x => x.AmountKrw * signs[x.EntryId]);
        var received = payments.Sum(x => x.Allocations.Where(a => economicIds.Contains(a.EntryId)).Sum(a => ConfirmedAllocation(x, a.EntryId) * signs[a.EntryId]));
        var confirmed = economic.Where(x => x.StatusCode == NeighborhoodSettlementCodes.Confirmed).Sum(x => x.AmountKrw ?? 0m);
        return new()
        {
            Agreement = agreement, Entries = entries, Payments = payments,
            Totals = new() { KnownAccruedAmountKrw = economic.Sum(x => x.AmountKrw ?? 0m), KnownConfirmedAmountKrw = confirmed,
                ReportedPaymentAmountKrw = reported, RecipientConfirmedAmountKrw = received, OutstandingAmountKrw = confirmed - received,
                UnresolvedLineCount = unresolvedCount, InternalEntryCount = entries.Count(x => x.IsInternal),
                TransitEntryCount = entries.Count(x => x.IsTransit), IsActualBankVerified = false }
        };
    }
    public async Task<IReadOnlyList<NeighborhoodSettlementSummaryDto>> MySummaryAsync(string actor, CancellationToken ct)
    {
        Gate(actor); var rows = await db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc).ToArrayAsync(ct);
        var versions = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking().ToArrayAsync(ct);
        var results = new List<NeighborhoodSettlementSummaryDto>();
        foreach (var row in rows.Where(x => x.CreatorUserId == actor || Party(Source(x), actor)
            || versions.Any(v => v.AgreementId == x.AgreementId && Party(Deserialize<NeighborhoodSettlementSourceSnapshot>(v.SourceSnapshotJson), actor))))
            results.Add(await GetAsync(row.AgreementId, actor, ct));
        return results;
    }
}
