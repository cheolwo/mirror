using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    public Task<DistributedCommandResponse> StockReleaseProposalAsync(DistributedActor actor, string siteId,
        DistributedStockReleaseProposalRequest r, CancellationToken ct)
        => Execute(actor, "StockReleaseProposal", siteId, r, async () =>
        {
            var site = await Site(siteId, ct); Revision(site.Revision, r); RequireReleaseSite(site);
            var lot = await Query<분산재고LotRecord>().SingleOrDefaultAsync(x => x.LotId == r.LotId && x.SiteId == siteId, ct);
            Require(lot is not null, "DistributedLotNotFound", 404);
            Require(actor.UserId == site.OwnerUserId || actor.UserId == lot!.OwnerUserId, "DistributedStockReleasePartyRequired", 403);
            Require(r.Quantity > 0, "DistributedStockReleaseQuantityInvalid", 400);
            var assigned = Text(r.AssignedUserId, 191);
            Require(assigned == site.OwnerUserId || assigned == site.ManagerUserId || assigned == lot!.OwnerUserId
                || await db.용달기사.AnyAsync(x => x.기사Id == assigned, ct), "DistributedStockReleaseAssigneeUnknown");
            var releases = StockReleases(site);
            Require(releases.Count < 200, "DistributedStockReleaseHistoryLimitReached");
            Require(!releases.Any(x => x.LotId == lot!.LotId && x.StatusCode is not "Completed" and not "Cancelled"), "DistributedStockReleaseAlreadyPending");
            var stock = await ReleaseStock(site, lot!, ct);
            Require(stock.가용수량 >= r.Quantity && stock.예약수량 == 0, "DistributedStockReleaseReservedOrInsufficient");
            await RequireNoPickedCustody(lot!.LotId, ct);
            var release = new StockReleasePlan
            {
                ReleaseId = "release-" + r.RequestId.ToString("N"), LotId = lot.LotId,
                LocationId = lot.LocationId, GoodsOwnerUserId = lot.OwnerUserId, SpaceOwnerUserId = site.OwnerUserId,
                AssignedUserId = assigned, AgreedQuantity = r.Quantity, HandoverTerms = Text(r.HandoverTerms, 500),
                Reason = Text(r.Reason, 500), CreatedAtUtc = Now
            };
            SaveStockRelease(site, release, actor.UserId, "Proposed"); site.Revision++;
            return (siteId, site.Revision, release.StatusCode, (string?)null, 0);
        }, ct);

    public Task<DistributedCommandResponse> StockReleaseConsentAsync(DistributedActor actor, string siteId,
        DistributedStockReleaseConsentRequest r, CancellationToken ct)
        => Execute(actor, "StockReleaseConsent", siteId, r, async () =>
        {
            var site = await Site(siteId, ct); Revision(site.Revision, r); RequireReleaseSite(site);
            var release = StockRelease(site, r.ReleaseId);
            Require(actor.UserId == release.SpaceOwnerUserId || actor.UserId == release.GoodsOwnerUserId,
                "DistributedStockReleasePartyRequired", 403);
            Require(release.SpaceOwnerUserId == site.OwnerUserId && release.StatusCode is not "Completed" and not "Cancelled", "DistributedStockReleaseClosed");
            if (r.CancelRemaining)
            {
                Require(!r.Granted, "DistributedStockReleaseDecisionInvalid", 400);
                release.StatusCode = "Cancelled";
            }
            else
            {
                if (r.Granted)
                {
                    var lot = await Query<분산재고LotRecord>().SingleAsync(x => x.LotId == release.LotId && x.SiteId == siteId, ct);
                    Require(lot.OwnerUserId == release.GoodsOwnerUserId, "DistributedInventoryLineageMismatch");
                    var stock = await ReleaseStock(site, lot, ct);
                    Require(stock.예약수량 == 0 && stock.가용수량 >= release.AgreedQuantity - release.ReleasedQuantity, "DistributedStockReleaseReservedOrInsufficient");
                    await RequireNoPickedCustody(lot.LotId, ct);
                }
                if (actor.UserId == release.SpaceOwnerUserId) release.SpaceOwnerConsented = r.Granted;
                if (actor.UserId == release.GoodsOwnerUserId) release.GoodsOwnerConsented = r.Granted;
                release.StatusCode = release.SpaceOwnerConsented && release.GoodsOwnerConsented
                    ? release.ReleasedQuantity > 0 ? "PartiallyReleased" : "Confirmed" : "AwaitingConsent";
            }
            SaveStockRelease(site, release, actor.UserId, r.CancelRemaining ? "CancelledRemaining" : r.Granted ? "Granted" : "Withdrawn"); site.Revision++;
            return (siteId, site.Revision, release.StatusCode, (string?)null, 0);
        }, ct);

    private async Task<StockReleaseWork> PrepareStockRelease(DistributedActor actor, string siteId,
        DistributedStockReleaseHandoverRequest r, CancellationToken ct)
    {
        var site = await Site(siteId, ct); Revision(site.Revision, r); RequireReleaseSite(site);
        var release = StockRelease(site, r.ReleaseId);
        Require(release.AssignedUserId == actor.UserId, "DistributedStockReleaseAssigneeRequired", 403);
        Require(release.SpaceOwnerUserId == site.OwnerUserId && release.SpaceOwnerConsented && release.GoodsOwnerConsented
            && release.StatusCode is "Confirmed" or "PartiallyReleased", "DistributedStockReleaseConsentRequired");
        Require(r.Quantity > 0 && r.Quantity <= release.AgreedQuantity - release.ReleasedQuantity, "DistributedStockReleaseQuantityInvalid", 400);
        var lot = await Query<분산재고LotRecord>().SingleAsync(x => x.LotId == release.LotId && x.SiteId == siteId, ct);
        Require(lot.OwnerUserId == release.GoodsOwnerUserId && lot.LocationId == release.LocationId, "DistributedInventoryLineageMismatch");
        var location = await Query<분산적재위치Record>().SingleAsync(x => x.LocationId == release.LocationId && x.SiteId == siteId, ct);
        Require(r.LocationBarcode == location.Barcode, "DistributedLocationMismatch");
        var stock = await ReleaseStock(site, lot, ct);
        Require(stock.예약수량 == 0 && stock.가용수량 >= r.Quantity, "DistributedStockReleaseReservedOrInsufficient");
        await RequireNoPickedCustody(lot.LotId, ct);
        return new(site, release, lot, stock, location);
    }

    public Task<DistributedCommandResponse> StockReleaseHandoverAsync(DistributedActor actor, string siteId,
        DistributedStockReleaseHandoverRequest r, CancellationToken ct)
        => Execute(actor, "StockReleaseHandover", siteId, r, async () =>
        {
            var work = await PrepareStockRelease(actor, siteId, r, ct);
            await BindWork(actor, new() { Kind = "StockRelease", AggregateId = siteId, StockRelease = r }, r.PhotoEvidenceIds, ct);
            var stock = work.Stock;
            var changed = await db.입고상품.Where(x => x.Id == stock.Id && x.소유자UserId == work.Lot.OwnerUserId
                && x.판매자UserId == work.Lot.SellerUserId && x.SKU == work.Lot.Sku && x.창고Id == work.Site.WarehouseId
                && x.보관위치 == work.Location.Barcode && x.가용수량 == stock.가용수량 && x.예약수량 == 0 && x.UpdatedAt == stock.UpdatedAt)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, x => x.가용수량 - r.Quantity).SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "DistributedStockReleaseInventoryChanged");
            work.Release.ReleasedQuantity += r.Quantity;
            work.Release.StatusCode = work.Release.ReleasedQuantity == work.Release.AgreedQuantity ? "Completed" : "PartiallyReleased";
            SaveStockRelease(work.Site, work.Release, actor.UserId, "HandedOver", r.Quantity); work.Site.Revision++;
            var history = await InventoryHistory(work.Lot.InboundItemId, actor.UserId, "잔여재고반출", -r.Quantity, PhotoMemo(r.PhotoEvidenceIds), ct);
            await SaveCompletionLines(actor, r, [new(CompletionLine(actor, "StockRelease", work.Release.ReleaseId, work.Lot, r.Quantity, r.PhotoEvidenceIds), history)], ct);
            return (siteId, work.Site.Revision, work.Release.StatusCode, (string?)null, r.Quantity);
        }, ct);

    public Task<DistributedCommandResponse> CloseSiteAsync(DistributedActor actor, string siteId, DistributedSiteCloseRequest r, CancellationToken ct)
        => Execute(actor, "CloseSite", siteId, r, async () =>
        {
            var site = await Site(siteId, ct); Revision(site.Revision, r);
            Require(actor.UserId == site.OwnerUserId, "DistributedSpaceOwnerRequired", 403);
            Require(site.StatusCode is "RevocationPendingCustody" or "Revoked", "DistributedSiteRevocationRequired");
            Require(r.NoRemainingCustodyConfirmed, "DistributedSiteEmptyConfirmationRequired", 400); Text(r.EvidenceReference, 500);
            var lots = await Query<분산재고LotRecord>().Where(x => x.SiteId == siteId).ToArrayAsync(ct);
            Require(!lots.Any(x => x.ReceivedQuantity < x.ExpectedQuantity && !ClosedInbound(x)), "DistributedSiteInboundPending");
            foreach (var lot in lots) { await ReleaseStock(site, lot, ct); await RequireNoPickedCustody(lot.LotId, ct); }
            // 미연결·불량 재고도 정상 0으로 숨기지 않습니다. 과거 입고 누계는 보관 잔량이 아닙니다.
            Require(!await db.입고상품.AnyAsync(x => x.창고Id == site.WarehouseId
                && (x.가용수량 != 0 || x.예약수량 != 0 || x.불량수량 != 0), ct), "DistributedSiteStockRemaining");
            Require(StockReleases(site).All(x => x.StatusCode is "Completed" or "Cancelled"), "DistributedStockReleasePending");
            SaveStockReleaseClosure(site, actor.UserId, Text(r.EvidenceReference, 500));
            site.StatusCode = "Closed"; site.Revision++;
            return (siteId, site.Revision, site.StatusCode, (string?)null, 0);
        }, ct);

    private static void RequireReleaseSite(분산거점Record site)
        => Require(site.StatusCode == "RevocationPendingCustody" && (!site.OwnerConsented || !site.ManagerConsented), "DistributedSiteRevocationRequired");
    private async Task<입고상품> ReleaseStock(분산거점Record site, 분산재고LotRecord lot, CancellationToken ct)
    {
        var stock = await db.입고상품.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lot.InboundItemId, ct);
        var location = await Query<분산적재위치Record>().AsNoTracking().SingleOrDefaultAsync(x => x.LocationId == lot.LocationId && x.SiteId == site.SiteId, ct);
        Require(stock is not null && location is not null && stock.소유자UserId == lot.OwnerUserId && stock.판매자UserId == lot.SellerUserId
            && stock.SKU == lot.Sku && stock.창고Id == site.WarehouseId && stock.보관위치 == location.Barcode
            && stock.가용수량 >= 0 && stock.예약수량 >= 0 && stock.불량수량 >= 0, "DistributedInventorySourceUnavailable", 503);
        return stock!;
    }
    private async Task RequireNoPickedCustody(string lotId, CancellationToken ct)
    {
        Require(!await Query<분산재고예약Record>().AnyAsync(x => x.LotId == lotId
            && x.PickedQuantity > x.DeliveredQuantity + x.ReturnedQuantity, ct), "DistributedStockReleasePickedCustodyRemaining");
        Require(!await Query<분산재고예약Record>().AnyAsync(x => x.LotId == lotId
            && x.Quantity > x.ReleasedQuantity + x.DeliveredQuantity + x.ReturnedQuantity, ct), "DistributedStockReleaseReservationRemaining");
    }
    private sealed record StockReleaseWork(분산거점Record Site, StockReleasePlan Release, 분산재고LotRecord Lot,
        입고상품 Stock, 분산적재위치Record Location);
}
