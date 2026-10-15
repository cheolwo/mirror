using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.정산;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCase
{
    private sealed record InventoryCostResult(IReadOnlyList<NeighborhoodInventoryCostAttributionDto> Costs, string? Reason = null);

    private static void ValidateInventoryCostSelections(NeighborhoodSettlementAgreementRequest terms)
    {
        if (terms.ActivityRewardTerms?.Economics is not { } economics) return;
        Require(economics.CostAllocations is { Count: <= 256 }, "InventoryCostSelectionChanged", 400);
        foreach (var selection in economics.CostAllocations)
        {
            Require(selection is not null && selection.ExpectedEntryRevision > 0, "InventoryCostSelectionChanged", 400);
            Text(selection!.SourceEntryId, 120);
        }
        Require(economics.CostAllocations.Select(x => x.SourceEntryId).Distinct(StringComparer.Ordinal).Count() == economics.CostAllocations.Count,
            "InventoryCostSelectionChanged", 400);
    }

    // 원천 작업자의 채무·지급 항목을 복제하지 않고 출고 주문에 귀속하는 회계 비용만 계산합니다.
    private async Task<InventoryCostResult> InventoryCosts(NeighborhoodSettlementAgreementRecord row,
        NeighborhoodSettlementSourceSnapshot current, CancellationToken ct)
    {
        var reward = Terms(row).ActivityRewardTerms!;
        if (reward.Economics is not { } economics) return new([]);
        var selected = economics.CostAllocations;
        if (selected is { Count: 0 }) return Hold("InventoryCostEvidenceRequired");
        if (selected is null || selected.Count > 256 || selected.Any(x => x is null || string.IsNullOrWhiteSpace(x.SourceEntryId) || x.ExpectedEntryRevision <= 0)
            || selected.Select(x => x.SourceEntryId).Distinct(StringComparer.Ordinal).Count() != selected.Count)
            return Hold("InventoryCostSelectionChanged");
        var reservations = current.InventoryReservations;
        if (reservations.Count == 0 || reservations.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.LotId)
                || string.IsNullOrWhiteSpace(x.ReservationId) || x.FirstPickedAtUtc is not { Kind: DateTimeKind.Utc } at || at > Now)
            || reservations.Select(x => x.ReservationId).Distinct(StringComparer.Ordinal).Count() != reservations.Count
            || reservations.Sum(x => x.Quantity) != current.RewardOutcome?.ConfirmedQuantity)
            return Hold("InventoryCostEvidenceRequired");
        var results = new List<NeighborhoodInventoryCostAttributionDto>();
        foreach (var group in reservations.GroupBy(x => x.LotId).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var lot = await sources.ResolveAsync(C.DistributedLot, group.Key, ct);
            if (lot.SourceKind != C.DistributedLot || lot.SourceId != group.Key || !Party(lot, reward.PayerUserId)
                || !(Role(lot, reward.PayerUserId, "Owner") || Role(lot, reward.PayerUserId, "Seller")))
                return Hold("InventoryCostEvidenceRequired");
            var inbound = lot.CompletedWork.Where(x => x.Kind == C.Inbound).ToArray();
            // 예약은 입고회차를 참조하지 않습니다. 여러 회차의 물품 계보/FIFO를 임의 확정하지 않습니다.
            if (inbound.Length != 1) return Hold("InventoryCostAmbiguous");
            var received = inbound[0];
            if (received.Quantity <= 0 || received.Quantity != lot.ActualCompletedUnits || received.OriginRevision <= 0
                || received.OccurredAtUtc is not { Kind: DateTimeKind.Utc } receivedAt || receivedAt > Now
                || received.EvidenceReferences.Count == 0)
                return Hold("InventoryCostEvidenceRequired");
            var quantity = group.Sum(x => x.Quantity); var firstPick = group.Min(x => x.FirstPickedAtUtc)!.Value;
            if (quantity > received.Quantity || firstPick < receivedAt) return Hold("InventoryCostQuantityExceeded");
            var sourceAgreement = await db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.SourceKind == C.DistributedLot && x.SourceId == group.Key, ct);
            if (sourceAgreement is null) return Hold("InventoryCostEvidenceRequired");
            var sourceTerms = Terms(sourceAgreement); var frozenSource = Source(sourceAgreement);
            if (sourceAgreement.StatusCode != C.Active || sourceTerms.ExpiresAtUtc is { } expiry && expiry <= Now
                || frozenSource.SourceVersion != lot.SourceVersion || frozenSource.SourceDigest != lot.SourceDigest)
                return Hold("InventoryCostNotConfirmed");
            var expectedLines = sourceTerms.Lines.Where(x => x.PayerUserId == reward.PayerUserId && !x.IncludedInDelivery
                && x.Kind is C.Inbound or C.Storage).ToArray();
            // 확인되지 않은 비용을 0으로 간주하지 않습니다. 무료도 명시된 약정과 확정 근거가 필요합니다.
            if (!expectedLines.Any(x => x.Kind == C.Inbound) || !expectedLines.Any(x => x.Kind == C.Storage))
                return Hold("InventoryCostIncomplete");
            var sourceRows = await db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking()
                .Where(x => x.AgreementId == sourceAgreement.AgreementId).ToArrayAsync(ct);
            var entries = sourceRows.Select(Entry).ToArray();
            var originals = entries.Where(x => x.OriginalEntryId is null && x.PayerUserId == reward.PayerUserId
                && !x.IncludedInDelivery && !x.IsTransit && x.Kind is C.Inbound or C.Storage).ToArray();
            if (expectedLines.Any(line => !originals.Any(e => e.LineKey == line.LineKey && e.Kind == line.Kind)))
                return Hold("InventoryCostIncomplete");
            if (originals.Length == 0) return Hold("InventoryCostIncomplete");
            var versions = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking()
                .Where(x => x.AgreementId == sourceAgreement.AgreementId).ToArrayAsync(ct);
            foreach (var entry in originals.OrderBy(x => x.EntryId, StringComparer.Ordinal))
            {
                var selection = selected.SingleOrDefault(x => x.SourceEntryId == entry.EntryId);
                if (selection is null) return Hold("InventoryCostIncomplete");
                if (selection.ExpectedEntryRevision != entry.Revision) return Hold("InventoryCostSelectionChanged");
                var adjustments = entries.Where(x => x.OriginalEntryId == entry.EntryId).OrderBy(x => x.EntryId, StringComparer.Ordinal).ToArray();
                if (entry.StatusCode != C.Confirmed || entry.AmountKrw is null
                    || adjustments.Any(x => x.StatusCode != C.Confirmed || x.AmountKrw is null))
                    return Hold("InventoryCostNotConfirmed");
                var version = versions.SingleOrDefault(x => x.TermsVersion == entry.TermsVersion);
                var line = version is null ? null : Deserialize<NeighborhoodSettlementAgreementRequest>(version.TermsJson)
                    .Lines.SingleOrDefault(x => x.LineKey == entry.LineKey && x.Kind == entry.Kind);
                if (line is null || line.PayerUserId != reward.PayerUserId || line.PayeeUserId != entry.PayeeUserId)
                    return Hold("InventoryCostEvidenceRequired");
                decimal? denominator;
                if (entry.Kind == C.Inbound || entry.Kind == C.Storage && entry.Billing == C.PerUnit)
                {
                    if (entry.OriginKind != received.OriginKind || entry.OriginId != received.OriginId
                        || !entry.WorkEvidenceReferences.Intersect(received.EvidenceReferences).Any())
                        return Hold("InventoryCostEvidenceRequired");
                    denominator = entry.Billing == C.PerUnit ? entry.Quantity : line.Quantity;
                }
                else if (entry.Billing == C.StoragePeriod)
                {
                    denominator = entry.Quantity;
                    if (entry.PeriodStartUtc is not { Kind: DateTimeKind.Utc } start || entry.PeriodEndUtc is not { Kind: DateTimeKind.Utc } end
                        || start >= end || end > firstPick || start < receivedAt || entry.OriginKind != C.DistributedLot || entry.OriginId != group.Key
                        || !lot.StorageQuantityIntervals.Any(x => x.StartUtc.Kind == DateTimeKind.Utc
                            && (x.EndUtc is null || x.EndUtc.Value.Kind == DateTimeKind.Utc)
                            && x.StartUtc <= start && (x.EndUtc is null || x.EndUtc >= end) && x.Quantity >= denominator))
                        return Hold("InventoryStoragePeriodNotAttributable");
                }
                else if (entry.Billing == C.Free)
                {
                    denominator = line.Quantity ?? entry.Quantity;
                    if (entry.OriginKind != C.DistributedLot || entry.OriginId != group.Key || lot.StorageEvidenceReferences.Count == 0)
                        return Hold("InventoryCostEvidenceRequired");
                }
                else return Hold("InventoryStoragePeriodNotAttributable");
                // 부분 청구/부분 물품의 비용을 어느 주문이 가져갔는지는 현재 원장으로 판별할 수 없습니다.
                if (denominator is null or <= 0 || denominator != received.Quantity) return Hold("InventoryCostAmbiguous");
                var total = entry.AmountKrw.Value + adjustments.Sum(x => x.AmountKrw!.Value);
                if (total < 0 || decimal.Floor(total) != total) return Hold("InventoryCostBudgetUnderstated");
                var priorRows = await db.Set<NeighborhoodInventoryCostAttributionRecord>().AsNoTracking()
                    .Where(x => x.SourceEntryId == entry.EntryId).OrderBy(x => x.OrderId).ToArrayAsync(ct);
                var prior = priorRows.Select(x => Deserialize<NeighborhoodInventoryCostAttributionDto>(x.DetailsJson)).ToArray();
                var usedQuantity = prior.Sum(x => x.AllocatedQuantity); var usedAmount = prior.Sum(x => x.AmountKrw);
                if (priorRows.Any(x => x.OrderId == row.SourceId) || usedQuantity + quantity > denominator)
                    return Hold("InventoryCostQuantityExceeded");
                if (usedAmount > total) return Hold("InventoryCostBudgetUnderstated");
                // 누적 내림의 차액. 원천 전량을 귀속한 마지막 주문에는 남은 원을 모두 보존합니다.
                var amount = decimal.Floor(total * (usedQuantity + quantity) / denominator.Value) - usedAmount;
                if (amount < 0 || usedAmount + amount > total) return Hold("InventoryCostBudgetUnderstated");
                results.Add(new() { SourceEntryId = entry.EntryId, SourceAgreementId = sourceAgreement.AgreementId,
                    LotId = group.Key, OrderId = row.SourceId, Kind = entry.Kind, SourceEntryRevision = entry.Revision,
                    SourceQuantity = denominator.Value, AllocatedQuantity = quantity, AmountKrw = amount,
                    PeriodStartUtc = entry.PeriodStartUtc, PeriodEndUtc = entry.PeriodEndUtc,
                    EvidenceDigest = Digest(new { sourceAgreement.Revision, lot.SourceVersion, lot.SourceDigest, lot.StorageQuantityIntervals, received,
                        entry, Adjustments = adjustments, OriginalTerms = line,
                        Prior = priorRows.Select(x => new { x.AttributionId, x.CalculationDigest, x.DetailsJson }).ToArray() }) });
            }
        }
        if (selected.Count != results.Count) return Hold("InventoryCostSelectionChanged");
        return new(results);

        static InventoryCostResult Hold(string reason) => new([], reason);
    }

    private async Task AccrueInventoryCosts(NeighborhoodSettlementAgreementRecord row,
        NeighborhoodActivityRewardPreviewDto preview, string actor, CancellationToken ct)
    {
        foreach (var id in preview.CostAttributions.Select(x => x.SourceAgreementId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            // 공유 원천의 revision을 갱신하여 서로 다른 출고 주문의 동시 귀속도 같은 CAS 대상으로 묶습니다.
            var source = await db.Set<NeighborhoodSettlementAgreementRecord>().SingleAsync(x => x.AgreementId == id, ct);
            source.Revision++; source.UpdatedAtUtc = Now; AppendHistory(source, actor, "AttributeInventoryCost", Now);
        }
        foreach (var cost in preview.CostAttributions)
            db.Add(new NeighborhoodInventoryCostAttributionRecord { AttributionId = NewId("nca"), SourceEntryId = cost.SourceEntryId,
                SourceAgreementId = cost.SourceAgreementId, OrderId = row.SourceId, AgreementId = row.AgreementId,
                CalculationDigest = preview.CalculationDigest, DetailsJson = Serialize(cost), RecordedAtUtc = Now });
    }
}
