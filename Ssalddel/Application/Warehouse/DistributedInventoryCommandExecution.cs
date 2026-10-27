using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.도메인.창고;
using static Ssalddel.Application.Warehouse.분산물류UseCase;

namespace Ssalddel.Application.Warehouse;

/// <summary>검증·사진 결속 이후의 입고 재고와 원천/대상 입고 사실을 같은 transaction에서 반영합니다.</summary>
internal sealed class DistributedInventoryCommandExecution(SsalddelContext db, TimeProvider clock, IDistributedPhotoEvidenceBinder photoEvidence)
{
    private IQueryable<T> Query<T>() where T : class => db.Set<T>();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private readonly DistributedCompletionRecorder completionRecorder = new(db, clock);
    private readonly DistributedStorageAgreementCommands agreementCommands = new(db, clock);
    private readonly DistributedInboundCompletion inboundCompletion = new(db);

    internal async Task<(string, long, string, string?, int)> ReceiveAsync(DistributedActor actor, string id,
        DistributedInboundReceiptBatchRequest r, DistributedReceiveWork work, CancellationToken ct)
    {
        var completed = new List<DistributedCompletionLineDraft>();
        foreach (var line in r.Lines)
        {
            var lot = work.Lots.Single(x => x.LineKey == line.LineKey);
            var changed = await db.입고상품.Where(x => x.Id == lot.InboundItemId && x.소유자UserId == lot.OwnerUserId && x.판매자UserId == lot.SellerUserId && x.SKU == lot.Sku && x.입고수량 == lot.ReceivedQuantity)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.입고수량, x => x.입고수량 + line.Quantity).SetProperty(x => x.가용수량, x => x.가용수량 + line.Quantity)
                    .SetProperty(x => x.상태, "보관중").SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "DistributedInventoryConflict"); lot.ReceivedQuantity += line.Quantity; lot.Revision++; agreementCommands.SnapshotAgreement(lot, work.Agreement, line.Quantity);
            var target = await db.입고요청.SingleAsync(x => x.Id == lot.TargetInboundId, ct);
            target.상태 = lot.ReceivedQuantity == lot.ExpectedQuantity ? "입고완료" : "부분입고"; target.입고완료일시 = target.상태 == "입고완료" ? Now : null;
            var history = await completionRecorder.InventoryHistory(lot.InboundItemId, actor.UserId, "분할입고", line.Quantity, PhotoMemo(r.PhotoEvidenceIds), ct);
            completed.Add(new(DistributedCompletionRecorder.CompletionLine(actor, "Receive", line.LineKey, lot, line.Quantity, r.PhotoEvidenceIds), history));
        }
        work.Site.Revision++; work.Agreement.Revision++;
        await inboundCompletion.RefreshInboundCompletion(work.Plan, ct); work.Plan.Revision++;
        foreach (var sourceId in work.Lots.Select(x => x.SourceInboundId).Distinct())
        {
            var source = await db.입고요청.SingleAsync(x => x.Id == sourceId, ct);
            var sourceLots = await Query<분산재고LotRecord>().Where(x => x.SourceInboundId == sourceId).ToArrayAsync(ct);
            source.상태 = sourceLots.Sum(x => x.ReceivedQuantity) == source.예정수량 ? "입고완료" : "부분입고";
            source.입고완료일시 = source.상태 == "입고완료" ? Now : null;
        }
        await completionRecorder.SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Plan.Revision, work.Plan.StatusCode, null, r.Lines.Sum(x => x.Quantity));
    }

    internal async Task<(string, long, string, string?, int)> ImportAsync(DistributedActor actor, string id,
        DistributedExistingLotRequest r, DistributedImportWork work, CancellationToken ct)
    {
        var item = work.Item; var site = work.Site; var location = work.Location;
        var plan = new 분산입고계획Record { PlanId = Id("inbound"), SourceReference = "existing-inventory:" + item.Id, CreatorUserId = actor.UserId, StatusCode = "Received" }; db.Add(plan);
        var changed = await db.입고상품.Where(x => x.Id == item.Id && x.창고Id == site.WarehouseId && x.보관위치 == item.보관위치 && x.입고수량 == item.입고수량
            && x.소유자UserId == item.소유자UserId && x.판매자UserId == item.판매자UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.보관위치, location.Barcode).SetProperty(x => x.UpdatedAt, Now), ct);
        Require(changed == 1, "DistributedExistingInventoryChanged");
        var lot = new 분산재고LotRecord { LotId = Id("lot"), PlanId = plan.PlanId, LineKey = "existing", SiteId = id, LocationId = location.LocationId,
            SourceInboundId = item.입고요청Id, TargetInboundId = item.입고요청Id, InboundItemId = item.Id, OwnerUserId = item.소유자UserId,
            SellerUserId = item.판매자UserId, Sku = item.SKU, LotCode = r.LotCode.Trim(), ExpectedQuantity = item.입고수량, ReceivedQuantity = item.입고수량 };
        agreementCommands.SnapshotAgreement(lot, work.Agreement, item.입고수량); db.Add(lot);
        await photoEvidence.AttachImportedLineageAsync(r.RequestId, actor.UserId, plan.PlanId, [lot.LotId], ct);
        await completionRecorder.InventoryHistory(item.Id, actor.UserId, "적재위치확인", 0, PhotoMemo(r.PhotoEvidenceIds), ct); site.Revision++; work.Agreement.Revision++;
        return (id, site.Revision, site.StatusCode, (string?)null, 0);
    }
}
