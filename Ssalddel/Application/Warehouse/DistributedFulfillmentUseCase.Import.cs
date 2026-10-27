using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;
public sealed partial class 분산물류UseCase
{
    private async Task<DistributedImportWork> PrepareImport(DistributedActor actor, string id, DistributedExistingLotRequest r, CancellationToken ct)
    {
        var site = await Site(id, ct); Require(Manages(actor, site), "DistributedSiteManagerRequired", 403); Revision(site.Revision, r);
        Require(site.StatusCode == "Active" && r.ActualLocationConfirmed, "DistributedActualLocationConfirmationRequired");
        // 정책이 있는 공간의 새 입고는 공급 협의와 양측 승인 경로를 거칩니다.
        Require(site.AcceptancePolicyJson is null, "DistributedInboundApprovalRequired");
        var location = await Query<분산적재위치Record>().SingleOrDefaultAsync(x => x.LocationId == r.LocationId && x.SiteId == id, ct);
        Require(location is not null && location.Barcode == r.LocationBarcode, "DistributedLocationMismatch");
        var item = await db.입고상품.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.InboundItemId && x.창고Id == site.WarehouseId, ct);
        Require(item is not null && item.입고수량 > 0 && !string.IsNullOrWhiteSpace(item.소유자UserId) && !string.IsNullOrWhiteSpace(item.판매자UserId), "DistributedExistingStockEvidenceMissing");
        Require(!await Query<분산재고LotRecord>().AnyAsync(x => x.InboundItemId == r.InboundItemId, ct), "DistributedStockAlreadyBound");
        Text(r.LotCode, 100);
        var prototype = new 분산재고LotRecord { SiteId = id, LocationId = location!.LocationId, OwnerUserId = item!.소유자UserId, Sku = item.SKU };
        var agreement = await ActiveAgreement(prototype, ct); await AgreementCapacity(agreement, item.입고수량, ct);
        return new(site, location, item, agreement);
    }
    public Task<DistributedCommandResponse> ImportLotAsync(DistributedActor actor, string id, DistributedExistingLotRequest r, CancellationToken ct)
        => Execute(actor, "ImportLot", id, r, async () =>
        {
            var work = await PrepareImport(actor, id, r, ct);
            await BindWork(actor, new() { Kind = "ImportLot", AggregateId = id, ImportLot = r }, r.PhotoEvidenceIds, ct);
            return await inventoryCommands.ImportAsync(actor, id, r, work, ct);
        }, ct);
}
