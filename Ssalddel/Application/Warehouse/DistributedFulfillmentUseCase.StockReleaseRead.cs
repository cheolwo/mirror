using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    private async Task PopulateStockReleaseWorkspace(DistributedActor actor, string roleKey, DistributedWorkspaceDto workspace,
        IReadOnlyList<분산거점Record> sites, CancellationToken ct)
    {
        if (roleKey is not ("warehouse" or "shipper" or "driver")) return;
        var visible = sites.SelectMany(site => StockReleases(site).Where(release => release.SpaceOwnerUserId == actor.UserId
            || release.GoodsOwnerUserId == actor.UserId || release.AssignedUserId == actor.UserId).Select(release => (Site: site, Release: release))).ToArray();
        var ids = visible.Select(x => x.Release.LotId).ToArray();
        var ownedSiteIds = sites.Where(s => s.OwnerUserId == actor.UserId).Select(s => s.SiteId).ToArray();
        var siteIds = sites.Select(s => s.SiteId).ToArray();
        var lots = await Query<분산재고LotRecord>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.LotId)
            || Enumerable.Contains(ownedSiteIds, x.SiteId) || x.OwnerUserId == actor.UserId).ToArrayAsync(ct);
        var stockIds = lots.Select(l => l.InboundItemId).ToArray();
        var stocks = await db.입고상품.AsNoTracking().Where(x => Enumerable.Contains(stockIds, x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var locations = await Query<분산적재위치Record>().AsNoTracking().Where(x => Enumerable.Contains(siteIds, x.SiteId)).ToDictionaryAsync(x => x.LocationId, ct);
        workspace.StockReleases = visible.Select(x =>
        {
            var site = x.Site; var release = x.Release;
            var lot = lots.SingleOrDefault(l => l.LotId == release.LotId && l.SiteId == site.SiteId);
            Require(lot is not null && stocks.ContainsKey(lot.InboundItemId) && locations.ContainsKey(release.LocationId), "DistributedInventorySourceUnavailable", 503);
            var party = actor.UserId == release.SpaceOwnerUserId || actor.UserId == release.GoodsOwnerUserId;
            var live = site.StatusCode == "RevocationPendingCustody" && release.StatusCode is not "Completed" and not "Cancelled";
            var actions = new List<string>();
            if (party && live) actions.AddRange(["consent-stock-release", "cancel-stock-release"]);
            var ready = live && release.SpaceOwnerConsented && release.GoodsOwnerConsented;
            if (ready && actor.UserId == release.AssignedUserId) actions.Add("handover-stock-release");
            var next = release.StatusCode is "Completed" or "Cancelled" ? "정리 기록 확인"
                : !release.SpaceOwnerConsented ? "공간 제공자 동의" : !release.GoodsOwnerConsented ? "물품 소유자 동의" : "지정 반출 담당자";
            var details = ready && actor.UserId == release.AssignedUserId ? Deserialize<DistributedSitePrivate>(site.PrivateDetailsJson) : null;
            return new DistributedStockReleaseDto(release.ReleaseId, site.SiteId, site.PublicLabel, site.Revision, release.LotId,
                stocks[lot!.InboundItemId].상품명, release.GoodsOwnerUserId, release.SpaceOwnerUserId, release.AssignedUserId,
                release.StatusCode, release.AgreedQuantity, release.ReleasedQuantity, release.GoodsOwnerConsented,
                release.SpaceOwnerConsented, release.HandoverTerms, next, actions,
                details is null ? null : locations[release.LocationId].Barcode, details?.PickupInstructions);
        }).ToArray();
        if (roleKey is "warehouse" or "shipper")
        {
            var drivers = await db.용달기사.AsNoTracking().OrderBy(x => x.기사Id).Take(100)
                .Select(x => new DistributedPartyCandidateDto(x.기사Id, x.기사명)).ToArrayAsync(ct);
            workspace.StockReleaseCandidates = lots.Where(lot => sites.Any(site => site.SiteId == lot.SiteId && site.StatusCode == "RevocationPendingCustody"
                && (actor.UserId == site.OwnerUserId || actor.UserId == lot.OwnerUserId))
                && stocks.TryGetValue(lot.InboundItemId, out var stock) && stock.가용수량 > 0 && stock.예약수량 == 0
                && !visible.Any(x => x.Release.LotId == lot.LotId && x.Release.StatusCode is not "Completed" and not "Cancelled"))
                .Select(lot =>
                {
                    var site = sites.Single(x => x.SiteId == lot.SiteId);
                    var parties = new[] { new DistributedPartyCandidateDto(site.OwnerUserId, "공간 제공자"),
                        new DistributedPartyCandidateDto(site.ManagerUserId, "공간 관리자"), new DistributedPartyCandidateDto(lot.OwnerUserId, "물품 소유자") };
                    return new DistributedStockReleaseCandidateDto(lot.LotId, site.SiteId, site.PublicLabel, site.Revision,
                        stocks[lot.InboundItemId].상품명, lot.OwnerUserId, stocks[lot.InboundItemId].가용수량,
                        parties.Concat(drivers).DistinctBy(x => x.UserId).ToArray());
                }).ToArray();
        }
        foreach (var site in workspace.Sites.Where(x => sites.Any(s => s.SiteId == x.SiteId && s.OwnerUserId == actor.UserId)
            && x.StatusCode is "RevocationPendingCustody" or "Revoked"))
            site.AllowedActions = [.. site.AllowedActions, "close-site"];
    }
}
