using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.도메인.창고;
using static Ssalddel.Application.Warehouse.분산물류UseCase;

namespace Ssalddel.Application.Warehouse;

/// <summary>현재 정차의 실제 피킹·출고·화물 인계를 반영하며 commit과 발행은 기존 실행기에 맡깁니다.</summary>
internal sealed class DistributedPickingCommandExecution(SsalddelContext db, TimeProvider clock, I분산화물연결Service cargo)
{
    private IQueryable<T> Query<T>() where T : class => db.Set<T>();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private readonly DistributedCompletionRecorder completionRecorder = new(db, clock);

    internal async Task<(string, long, string, string?, int)> PickAsync(DistributedActor actor, string id,
        DistributedPickBatchRequest r, DistributedCustodyWork work, CancellationToken ct)
    {
        var completed = new List<DistributedCompletionLineDraft>();
        foreach (var order in work.Orders) await cargo.ArrivePickupAsync(actor.UserId, order.CargoRequestId, ct);
        foreach (var line in r.Lines)
        {
            var reservation = work.Reservations.Single(x => x.ReservationId == line.ReservationId); var lot = work.Lots.Single(x => x.LotId == reservation.LotId);
            var changed = await db.입고상품.Where(x => x.Id == lot.InboundItemId && x.창고Id == work.Site.WarehouseId && x.소유자UserId == lot.OwnerUserId
                && x.판매자UserId == lot.SellerUserId && x.SKU == lot.Sku && x.보관위치 == work.Location.Barcode && x.예약수량 >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.예약수량, x => x.예약수량 - line.Quantity).SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "DistributedReservedInventoryChanged"); reservation.PickedQuantity += line.Quantity; reservation.Revision++;
            var outbound = await db.출고예정.SingleAsync(x => x.Id == reservation.OutboundPlanId, ct);
            outbound.상태 = reservation.PickedQuantity == reservation.Quantity ? 출고상태.출고완료 : "부분출고";
            outbound.출고처리일시 = reservation.PickedQuantity == reservation.Quantity ? Now : null;
            var history = await completionRecorder.InventoryHistory(lot.InboundItemId, actor.UserId, "기사직접피킹", -line.Quantity, PhotoMemo(r.PhotoEvidenceIds), ct);
            completed.Add(new(DistributedCompletionRecorder.CompletionLine(actor, "Pick", line.ReservationId, lot, line.Quantity, r.PhotoEvidenceIds, reservation, work.Wave), history));
        }
        foreach (var order in work.Orders)
        {
            var all = await Query<분산재고예약Record>().Where(x => x.OrderId == order.OrderId).ToArrayAsync(ct);
            if (all.All(x => x.PickedQuantity == x.Quantity)) { await cargo.PickupAsync(actor.UserId, order.CargoRequestId, ct); order.StatusCode = "Picked"; }
            else order.StatusCode = "PartiallyPicked"; order.Revision++;
        }
        work.Site.Revision++;
        var siteLots = await Query<분산재고LotRecord>().Where(x => x.SiteId == work.Site.SiteId).Select(x => x.LotId).ToArrayAsync(ct);
        var remaining = await Query<분산재고예약Record>().Where(x => x.WaveId == id && Enumerable.Contains(work.Stop!.OrderIds, x.OrderId) && Enumerable.Contains(siteLots, x.LotId)).ToArrayAsync(ct);
        if (remaining.All(x => x.PickedQuantity + x.ReleasedQuantity == x.Quantity)) CompleteStop(work.Wave, work.Stop!.StopId);
        work.Wave.Revision++; await completionRecorder.SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Wave.Revision, work.Wave.StatusCode, work.Reservations.Length == 1 ? work.Reservations[0].ReservationId : null, r.Lines.Sum(x => x.Quantity));
    }
}
