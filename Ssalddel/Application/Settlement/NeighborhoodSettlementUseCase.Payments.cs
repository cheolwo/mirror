using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.정산;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCase
{
    private static NeighborhoodSettlementPaymentDto Payment(NeighborhoodSettlementPaymentRecord row)
    {
        var payment = Deserialize<NeighborhoodSettlementPaymentDto>(row.DetailsJson);
        payment.StatusCode = row.StatusCode; payment.Revision = row.Revision;
        payment.RecipientConfirmedAtUtc = row.RecipientConfirmedAtUtc; payment.IsActualBankVerified = false; return payment;
    }
    private static decimal ConfirmedAllocation(NeighborhoodSettlementPaymentDto payment, string entryId)
    {
        // 부분 수령 확인은 신고 때 저장한 배분 순서대로 계산하며 총 확인액을 넘기지 않습니다.
        var remaining = payment.RecipientConfirmedAmountKrw;
        foreach (var allocation in payment.Allocations)
        {
            var confirmed = Math.Min(allocation.AmountKrw, remaining);
            if (allocation.EntryId == entryId) return confirmed;
            remaining -= confirmed;
        }
        return 0;
    }
    private static decimal EffectiveCharge(NeighborhoodSettlementEntryDto entry, IEnumerable<NeighborhoodSettlementEntryDto> all)
        => (entry.AmountKrw ?? 0m) + all.Where(x => x.OriginalEntryId == entry.EntryId && x.AmountKrw < 0
            && x.StatusCode == NeighborhoodSettlementCodes.Confirmed).Sum(x => x.AmountKrw ?? 0m);

    private static decimal? ReportablePayment(NeighborhoodSettlementEntryDto entry,
        IReadOnlyList<NeighborhoodSettlementEntryDto> entries, IReadOnlyList<NeighborhoodSettlementPaymentDto> reports, string actor)
    {
        var refund = entry.OriginalEntryId is not null && entry.AmountKrw < 0;
        if ((refund ? entry.PayeeUserId : entry.PayerUserId) != actor || entry.IsInternal || entry.IncludedInDelivery
            || entry.AmountKrw is null or 0 || entry.StatusCode != NeighborhoodSettlementCodes.Confirmed
            || entry.PaymentReleaseReady != true
            || entries.Any(x => x.OriginalEntryId == entry.EntryId && x.AmountKrw < 0 && x.StatusCode != NeighborhoodSettlementCodes.Confirmed)) return null;
        var reserved = reports.SelectMany(x => x.Allocations).Where(x => x.EntryId == entry.EntryId).Sum(x => x.AmountKrw);
        if (!refund) return Math.Max(0, EffectiveCharge(entry, entries) - reserved);
        var original = entries.SingleOrDefault(x => x.EntryId == entry.OriginalEntryId);
        if (original is null) return null;
        var linked = entries.Where(x => x.OriginalEntryId == original.EntryId).ToArray();
        if (linked.Any(x => x.AmountKrw < 0 && x.StatusCode != NeighborhoodSettlementCodes.Confirmed)) return null;
        var grossIds = linked.Where(x => x.AmountKrw > 0).Select(x => x.EntryId).Append(original.EntryId).ToHashSet(StringComparer.Ordinal);
        var refundIds = linked.Where(x => x.AmountKrw < 0).Select(x => x.EntryId).ToHashSet(StringComparer.Ordinal);
        var received = reports.Sum(p => p.Allocations.Where(a => grossIds.Contains(a.EntryId)).Sum(a => ConfirmedAllocation(p, a.EntryId)));
        var net = original.AmountKrw!.Value + linked.Where(x => x.StatusCode == NeighborhoodSettlementCodes.Confirmed).Sum(x => x.AmountKrw ?? 0m);
        var refundReserved = reports.SelectMany(x => x.Allocations).Where(x => refundIds.Contains(x.EntryId)).Sum(x => x.AmountKrw);
        return Math.Max(0, Math.Min(decimal.Abs(entry.AmountKrw!.Value) - reserved, Math.Max(0, received - net) - refundReserved));
    }

    public Task<NeighborhoodSettlementReceiptDto> ReportPaymentAsync(string id, string actor, NeighborhoodSettlementPaymentReportRequest r, CancellationToken ct)
        => Execute(actor, "ReportPayment", id, r, async () =>
        {
            var agreement = await Owned(id, actor, ct); Revision(agreement, r);
            Require(r.AmountKrw > 0 && Money(r.AmountKrw) && r.Allocations is { Count: > 0 and <= 64 }
                && r.Allocations.All(x => x.AmountKrw > 0 && Money(x.AmountKrw))
                && r.Allocations.Sum(x => x.AmountKrw) == r.AmountKrw
                && r.Allocations.Select(x => x.EntryId).Distinct(StringComparer.Ordinal).Count() == r.Allocations.Count,
                "NeighborhoodSettlementPaymentAllocationInvalid", 400);
            var entries = (await db.Set<NeighborhoodSettlementEntryRecord>().Where(x => x.AgreementId == id).ToArrayAsync(ct)).Select(Entry).ToArray();
            var reports = (await db.Set<NeighborhoodSettlementPaymentRecord>().Where(x => x.AgreementId == id).ToArrayAsync(ct)).Select(Payment).ToArray();
            string? payee = null;
            foreach (var allocation in r.Allocations)
            {
                var entry = entries.SingleOrDefault(x => x.EntryId == allocation.EntryId)
                    ?? throw new NeighborhoodSettlementException("NeighborhoodSettlementEntryNotFound", 404);
                var refund = entry.OriginalEntryId is not null && entry.AmountKrw < 0;
                Require((refund ? entry.PayeeUserId : entry.PayerUserId) == actor, "NeighborhoodSettlementPayerRequired", 403);
                Require(!entry.IsInternal && !entry.IncludedInDelivery && entry.AmountKrw != 0
                    && entry.StatusCode == NeighborhoodSettlementCodes.Confirmed, "NeighborhoodSettlementEntryNotPayable");
                await RequirePaymentRelease(agreement, entry, entries, ct);
                var recipient = refund ? entry.PayerUserId : entry.PayeeUserId;
                payee ??= recipient;
                Require(payee == recipient, "NeighborhoodSettlementPaymentRecipientMixed", 400);
                Require(!entries.Any(x => x.OriginalEntryId == entry.EntryId && x.AmountKrw < 0 && x.StatusCode != NeighborhoodSettlementCodes.Confirmed),
                    "NeighborhoodSettlementAdjustmentConfirmationRequired");
                var reserved = reports.SelectMany(x => x.Allocations).Where(x => x.EntryId == entry.EntryId).Sum(x => x.AmountKrw);
                if (!refund)
                    Require(reserved + allocation.AmountKrw <= EffectiveCharge(entry, entries), "NeighborhoodSettlementPaymentExceedsUnpaidAmount");
                else
                {
                    var original = entries.Single(x => x.EntryId == entry.OriginalEntryId);
                    var linked = entries.Where(x => x.OriginalEntryId == original.EntryId).ToArray();
                    Require(!linked.Any(x => x.AmountKrw < 0 && x.StatusCode != NeighborhoodSettlementCodes.Confirmed),
                        "NeighborhoodSettlementAdjustmentConfirmationRequired");
                    var grossIds = linked.Where(x => x.AmountKrw > 0).Select(x => x.EntryId).Append(original.EntryId).ToHashSet(StringComparer.Ordinal);
                    var refundIds = linked.Where(x => x.AmountKrw < 0).Select(x => x.EntryId).ToHashSet(StringComparer.Ordinal);
                    var actuallyReceived = reports.Sum(p => p.Allocations.Where(a => grossIds.Contains(a.EntryId)).Sum(a => ConfirmedAllocation(p, a.EntryId)));
                    var netObligation = (original.AmountKrw ?? 0m) + linked.Where(x => x.StatusCode == NeighborhoodSettlementCodes.Confirmed).Sum(x => x.AmountKrw ?? 0m);
                    var allRefundReservations = reports.SelectMany(x => x.Allocations).Where(a => refundIds.Contains(a.EntryId)).Sum(a => a.AmountKrw);
                    var thisReportRefunds = r.Allocations.TakeWhile(x => x.EntryId != allocation.EntryId)
                        .Where(x => refundIds.Contains(x.EntryId)).Sum(x => x.AmountKrw);
                    var refundable = Math.Max(0m, actuallyReceived - netObligation);
                    Require(reserved + allocation.AmountKrw <= decimal.Abs(entry.AmountKrw!.Value)
                        && allRefundReservations + thisReportRefunds + allocation.AmountKrw <= refundable, "NeighborhoodSettlementRefundExceedsReceivedAmount");
                }
            }
            var payment = new NeighborhoodSettlementPaymentDto
            {
                PaymentId = NewId("nsp"), AgreementId = id, PayerUserId = actor, PayeeUserId = payee!, AmountKrw = r.AmountKrw,
                Allocations = r.Allocations.Select(x => new NeighborhoodSettlementPaymentAllocation { EntryId = x.EntryId, AmountKrw = x.AmountKrw }).ToArray(),
                EvidenceReference = Text(r.EvidenceReference, 1000), StatusCode = NeighborhoodSettlementCodes.Reported,
                Revision = 1, ReportedAtUtc = Now, IsActualBankVerified = false
            };
            db.Add(new NeighborhoodSettlementPaymentRecord { PaymentId = payment.PaymentId, AgreementId = id,
                DetailsJson = Serialize(payment), ReportedAtUtc = Now });
            agreement.Revision++; agreement.UpdatedAtUtc = Now; AppendHistory(agreement, actor, "ReportPayment", Now);
            return Result(agreement, r.RequestId, "ReportPayment", payment: payment.PaymentId);
        }, ct);

    public Task<NeighborhoodSettlementReceiptDto> ConfirmReceiptAsync(string id, string actor, NeighborhoodSettlementPaymentReceiptRequest r, CancellationToken ct)
        => Execute(actor, "ConfirmReceipt", id, r, async () =>
        {
            var agreement = await Owned(id, actor, ct); Revision(agreement, r);
            var row = await db.Set<NeighborhoodSettlementPaymentRecord>().SingleOrDefaultAsync(x => x.AgreementId == id && x.PaymentId == r.PaymentId, ct)
                ?? throw new NeighborhoodSettlementException("NeighborhoodSettlementPaymentNotFound", 404);
            var payment = Payment(row);
            Require(payment.PayeeUserId == actor, "NeighborhoodSettlementRecipientRequired", 403);
            Require(Money(r.ConfirmedAmountKrw) && r.ConfirmedAmountKrw > payment.RecipientConfirmedAmountKrw && r.ConfirmedAmountKrw <= payment.AmountKrw,
                "NeighborhoodSettlementReceiptAmountInvalid", 400);
            payment.RecipientConfirmedAmountKrw = r.ConfirmedAmountKrw;
            payment.RecipientReason = Text(r.Reason, 1000);
            payment.StatusCode = r.ConfirmedAmountKrw == payment.AmountKrw ? NeighborhoodSettlementCodes.RecipientConfirmed : NeighborhoodSettlementCodes.PartiallyConfirmed;
            row.StatusCode = payment.StatusCode; row.RecipientConfirmedAtUtc = Now; row.Revision++;
            payment.Revision = row.Revision; payment.RecipientConfirmedAtUtc = Now; payment.AllowedActions = [];
            payment.IsActualBankVerified = false; row.DetailsJson = Serialize(payment);
            agreement.Revision++; agreement.UpdatedAtUtc = Now; AppendHistory(agreement, actor, "ConfirmReceipt", Now);
            return Result(agreement, r.RequestId, "ConfirmReceipt", payment: payment.PaymentId);
        }, ct);
}
