using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 공간공동운영전문업무Bridge
{
    public async Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId,
        IReadOnlyList<공간공동운영전문참조Dto> currentRefs, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new UnauthorizedAccessException();
        if (!options.Value.Enabled || !execution.IsSimulation
            || options.Value.Profile != Ssalddel.Contracts.Common.Warehouse.DistributedFulfillmentCodes.Profile) return [];
        var requestedSites = currentRefs.Where(r => r.KindCode == "DistributedSite").Select(r => r.ReferenceId).Distinct().ToArray();
        if (requestedSites.Length > 1) return [];
        var ownedLotSites = await db.Set<분산재고LotRecord>().AsNoTracking().Where(l => l.OwnerUserId == actorUserId || l.SellerUserId == actorUserId)
            .Select(l => l.SiteId).Distinct().ToArrayAsync(cancellationToken);
        var sites = await db.Set<분산거점Record>().AsNoTracking().Where(s => s.OwnerUserId == actorUserId || s.ManagerUserId == actorUserId
            || Enumerable.Contains(ownedLotSites, s.SiteId)).OrderBy(s => s.SiteId).Take(100).ToArrayAsync(cancellationToken);
        if (requestedSites.Length == 1) sites = sites.Where(s => s.SiteId == requestedSites[0]).ToArray();
        var result = sites.Select(s => Candidate("DistributedSite", s.SiteId, s.PublicLabel + " · 공간", s.Revision)).ToList();
        if (requestedSites.Length == 0 || sites.Length != 1) return result;
        var site = sites[0];
        var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(l => l.SiteId == site.SiteId).ToArrayAsync(cancellationToken);
        var lotIds = lots.Select(l => l.LotId).ToArray();
        var agreements = await db.Set<분산보관합의Record>().AsNoTracking().Where(a => a.SiteId == site.SiteId
            && (a.SpaceOwnerUserId == actorUserId || a.GoodsOwnerUserId == actorUserId)).OrderBy(a => a.AgreementId).Take(100).ToArrayAsync(cancellationToken);
        result.AddRange(agreements.Select(a => Candidate("StorageAgreement", a.AgreementId, site.PublicLabel + " · 보관 합의 · " + a.StatusCode, a.Revision, site.SiteId)));
        var planIds = lots.Select(l => l.PlanId).Distinct().ToArray();
        var plans = await db.Set<분산입고계획Record>().AsNoTracking().Where(p => Enumerable.Contains(planIds, p.PlanId)).OrderBy(p => p.PlanId).Take(100).ToArrayAsync(cancellationToken);
        foreach (var plan in plans)
        {
            var r = new 공간공동운영전문참조Dto { KindCode = "InboundPlan", ReferenceId = plan.PlanId, ParentId = site.SiteId };
            if ((await Read(actorUserId, r, cancellationToken)).Verified)
                result.Add(Candidate(r.KindCode, r.ReferenceId, site.PublicLabel + " · 입고 계획 · " + plan.SourceReference, plan.Revision, site.SiteId));
        }
        var reservations = await db.Set<분산재고예약Record>().AsNoTracking().Where(r => Enumerable.Contains(lotIds, r.LotId)).ToArrayAsync(cancellationToken);
        var orderIds = reservations.Select(r => r.OrderId).Distinct().ToArray();
        var orders = await db.Set<분산배송주문Record>().AsNoTracking().Where(o => Enumerable.Contains(orderIds, o.OrderId)).ToArrayAsync(cancellationToken);
        var waveIds = orders.Select(o => o.WaveId).Distinct().ToArray();
        var waves = await db.Set<분산배송WaveRecord>().AsNoTracking().Where(w => w.CreatorUserId == actorUserId
            || Enumerable.Contains(waveIds, w.WaveId)).OrderBy(w => w.WaveId).Take(100).ToArrayAsync(cancellationToken);
        foreach (var wave in waves)
        {
            var r = new 공간공동운영전문참조Dto { KindCode = "DistributedWave", ReferenceId = wave.WaveId, ParentId = site.SiteId };
            if ((await Read(actorUserId, r, cancellationToken)).Verified)
                result.Add(Candidate(r.KindCode, r.ReferenceId, wave.Label + " · 출고 묶음 · " + wave.StatusCode, wave.Revision, site.SiteId));
        }
        var scopedOrders = new List<분산배송주문Record>();
        foreach (var order in orders)
        {
            if (!await OrderParty(actorUserId, order, cancellationToken)) continue;
            var r = new 공간공동운영전문참조Dto { KindCode = "DistributedOrder", ReferenceId = order.OrderId, ParentId = order.WaveId };
            if (!await Related(actorUserId, r, currentRefs, cancellationToken)) continue;
            scopedOrders.Add(order);
            result.Add(Candidate("DistributedOrder", order.OrderId, order.OrderReference + " · 주문 · " + order.StatusCode, order.Revision, order.WaveId));
        }
        var allowedOrderIds = scopedOrders.Where(o => o.SellerUserId == actorUserId || o.CustomerUserId == actorUserId).Select(o => o.OrderId).ToHashSet(StringComparer.Ordinal);
        foreach (var connection in waves.SelectMany(w => 분산주문원장Metadata.Connections(w).Values).Where(c => allowedOrderIds.Contains(c.OrderId)))
        {
            var r = new 공간공동운영전문참조Dto { KindCode = "RootOrderLedger", ReferenceId = connection.RootOrderLedgerId };
            var state = await Read(actorUserId, r, cancellationToken);
            if (state.Verified) result.Add(Candidate(r.KindCode, r.ReferenceId, "연결된 주문 원장", state.VerifiedRevision));
        }
        if (settlements is not null)
        {
            var knownSourceIds = scopedOrders.Select(o => o.OrderId).Concat(planIds).Concat(lotIds).ToHashSet(StringComparer.Ordinal);
            foreach (var summary in await settlements.MySummaryAsync(actorUserId, cancellationToken))
                if (knownSourceIds.Contains(summary.Agreement.SourceId)) result.Add(Candidate("Settlement", summary.Agreement.AgreementId,
                    "같은 회차의 본인 정산 · " + summary.Agreement.StatusCode, summary.Agreement.Revision, summary.Agreement.SourceId));
        }
        return result.DistinctBy(r => (r.Reference.KindCode, r.Reference.ReferenceId)).Take(200).ToArray();
    }

    private static 공간공동운영연결후보Dto Candidate(string kind, string id, string label, long? revision, string? parent = null) => new()
    {
        Label = label,
        Reference = new() { KindCode = kind, ReferenceId = id, ParentId = parent, ExpectedRevision = revision }
    };
}
