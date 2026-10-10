using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    /// <summary>지역 참고자료·배송 묶음을 읽지 않고 권한 있는 한 공간의 재고만 제한 조회합니다.</summary>
    public async Task<분산공간재고PageDto> 공간재고Async(DistributedActor actor, string siteId, string roleKey,
        int skip, int take, CancellationToken ct)
    {
        Gate(actor);
        ct.ThrowIfCancellationRequested();
        Require(roleKey is "warehouse" or "shipper", "DistributedRoleScopeInvalid", 400);
        Require(skip >= 0 && take is >= 1 and <= 100, "DistributedInventoryPageInvalid", 400);
        siteId = Text(siteId);

        var sites = Query<분산거점Record>().AsNoTracking().Where(site => site.SiteId == siteId);
        var lots = Query<분산재고LotRecord>().AsNoTracking().Where(lot => lot.SiteId == siteId);
        if (roleKey == "warehouse")
            sites = sites.Where(site => actor.IsAdmin || site.OwnerUserId == actor.UserId || site.ManagerUserId == actor.UserId);
        else
        {
            lots = lots.Where(lot => lot.OwnerUserId == actor.UserId || lot.SellerUserId == actor.UserId);
            sites = sites.Where(site => lots.Any(lot => lot.SiteId == site.SiteId));
        }

        var header = await sites.Select(site => new { site.SiteId, site.PublicLabel, site.StatusCode,
            site.Revision, site.WarehouseId }).SingleOrDefaultAsync(ct);
        // 없는 공간과 접근할 수 없는 공간을 같은 응답으로 처리합니다.
        Require(header is not null, "DistributedSiteNotFound", 404);
        var page = await lots.Where(lot => sites.Any(site => site.SiteId == lot.SiteId))
            .OrderBy(lot => lot.LotId).Skip(skip).Take(take + 1)
            .Select(lot => new { lot.LotId, lot.Revision, lot.PlanId, lot.LocationId, lot.InboundItemId, lot.Sku,
                lot.LotCode, lot.ExpectedQuantity, lot.OwnerUserId, lot.SellerUserId }).ToArrayAsync(ct);
        var hasMore = page.Length > take;
        var visible = page.Take(take).ToArray();
        var stockIds = visible.Select(lot => lot.InboundItemId).ToArray();
        var locationIds = visible.Select(lot => lot.LocationId).Distinct().ToArray();
        var stocks = await db.입고상품.AsNoTracking().Where(stock => Enumerable.Contains(stockIds, stock.Id))
            .Select(stock => new { stock.Id, stock.상품명, stock.입고수량, stock.가용수량, stock.예약수량, stock.불량수량,
                stock.소유자UserId, stock.판매자UserId, stock.SKU, stock.창고Id, stock.보관위치 })
            .ToDictionaryAsync(stock => stock.Id, ct);
        var locations = await Query<분산적재위치Record>().AsNoTracking()
            .Where(location => location.SiteId == siteId && Enumerable.Contains(locationIds, location.LocationId))
            .Select(location => new { location.LocationId, location.Label, location.Barcode }).ToDictionaryAsync(location => location.LocationId, ct);
        // 연결 결손을 정상 0 재고나 빈 페이지로 숨기지 않습니다.
        Require(visible.All(lot => stocks.TryGetValue(lot.InboundItemId, out var stock)
            && locations.TryGetValue(lot.LocationId, out var location) && stock.소유자UserId == lot.OwnerUserId
            && stock.판매자UserId == lot.SellerUserId && stock.SKU == lot.Sku && stock.창고Id == header!.WarehouseId
            && stock.보관위치 == location.Barcode),
            "DistributedInventorySourceUnavailable", 503);
        var items = visible.Select(lot =>
        {
            var stock = stocks[lot.InboundItemId];
            return new 분산공간재고ItemDto(lot.LotId, lot.Revision, lot.PlanId, lot.LocationId,
                roleKey == "warehouse" ? locations[lot.LocationId].Label : "선택 가능한 적재 위치",
                lot.Sku, lot.LotCode, stock.상품명, lot.ExpectedQuantity, stock.입고수량,
                Math.Max(0, lot.ExpectedQuantity - stock.입고수량), stock.가용수량, stock.예약수량, stock.불량수량);
        }).ToArray();
        return new(new(header!.SiteId, header.PublicLabel, header.StatusCode, header.Revision), roleKey, skip, take, hasMore, items);
    }
}
