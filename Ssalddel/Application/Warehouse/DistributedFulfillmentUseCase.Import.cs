using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;
public sealed partial class 분산물류UseCase
{
    private async Task<ImportWork> PrepareImport(DistributedActor actor, string id, DistributedExistingLotRequest r, CancellationToken ct)
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
            var work = await PrepareImport(actor, id, r, ct); var item = work.Item; var site = work.Site; var location = work.Location;
            await BindWork(actor, new() { Kind = "ImportLot", AggregateId = id, ImportLot = r }, r.PhotoEvidenceIds, ct);
            var plan = new 분산입고계획Record { PlanId = Id("inbound"), SourceReference = "existing-inventory:" + item.Id, CreatorUserId = actor.UserId, StatusCode = "Received" }; db.Add(plan);
            var changed = await db.입고상품.Where(x => x.Id == item.Id && x.창고Id == site.WarehouseId && x.보관위치 == item.보관위치 && x.입고수량 == item.입고수량
                && x.소유자UserId == item.소유자UserId && x.판매자UserId == item.판매자UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.보관위치, location.Barcode).SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "DistributedExistingInventoryChanged");
            var lot = new 분산재고LotRecord { LotId = Id("lot"), PlanId = plan.PlanId, LineKey = "existing", SiteId = id, LocationId = location.LocationId,
                SourceInboundId = item.입고요청Id, TargetInboundId = item.입고요청Id, InboundItemId = item.Id, OwnerUserId = item.소유자UserId,
                SellerUserId = item.판매자UserId, Sku = item.SKU, LotCode = r.LotCode.Trim(), ExpectedQuantity = item.입고수량, ReceivedQuantity = item.입고수량 };
            SnapshotAgreement(lot, work.Agreement, item.입고수량); db.Add(lot);
            await photoEvidence.AttachImportedLineageAsync(r.RequestId, actor.UserId, plan.PlanId, [lot.LotId], ct);
            await InventoryHistory(item.Id, actor.UserId, "적재위치확인", 0, PhotoMemo(r.PhotoEvidenceIds), ct); site.Revision++; work.Agreement.Revision++;
            return (id, site.Revision, site.StatusCode, (string?)null, 0);
        }, ct);
    private sealed record ImportWork(분산거점Record Site, 분산적재위치Record Location, 입고상품 Item, 분산보관합의Record Agreement);
}
