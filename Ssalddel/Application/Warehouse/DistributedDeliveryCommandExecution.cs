using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.도메인.창고;
using static Ssalddel.Application.Warehouse.분산물류UseCase;

namespace Ssalddel.Application.Warehouse;

/// <summary>실제 전달·반환과 남은 보관 책임을 반영합니다. 수령 확인이나 SQL commit을 대신하지 않습니다.</summary>
internal sealed class DistributedDeliveryCommandExecution(SsalddelContext db, TimeProvider clock, I분산화물연결Service cargo)
{
    private IQueryable<T> Query<T>() where T : class => db.Set<T>();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private readonly DistributedCompletionRecorder completionRecorder = new(db, clock);
    private async Task<분산거점Record> Site(string id, CancellationToken ct)
        => await Query<분산거점Record>().SingleOrDefaultAsync(x => x.SiteId == id, ct) ?? throw new DistributedFulfillmentException("DistributedSiteNotFound", 404);

    internal async Task<(string, long, string, string?, int)> DeliverAsync(DistributedActor actor, string id,
        DistributedDeliveryBatchRequest r, DistributedCustodyWork work, CancellationToken ct)
    {
        var completed = new List<DistributedCompletionLineDraft>();
        foreach (var line in r.Lines)
        {
            var reservation = work.Reservations.Single(x => x.ReservationId == line.ReservationId);
            reservation.DeliveredQuantity += line.Quantity; reservation.Revision++;
            completed.Add(new(DistributedCompletionRecorder.CompletionLine(actor, "Deliver", line.ReservationId, work.Lots.Single(x => x.LotId == reservation.LotId),
                line.Quantity, r.PhotoEvidenceIds, reservation, work.Wave)));
        }
        foreach (var order in work.Orders)
        {
            var all = await Query<분산재고예약Record>().Where(x => x.OrderId == order.OrderId).ToArrayAsync(ct);
            if (all.All(x => x.DeliveredQuantity == x.Quantity)) { await cargo.DeliverAsync(actor.UserId, order.CargoRequestId, ct); order.StatusCode = "Delivered"; }
            else order.StatusCode = "PartiallyDelivered"; order.Revision++;
        }
        foreach (var siteId in work.Lots.Select(x => x.SiteId).Distinct()) await TouchSite(siteId, ct);
        var stopOrders = await Query<분산배송주문Record>().Where(x => Enumerable.Contains(work.Stop!.OrderIds, x.OrderId)).ToArrayAsync(ct);
        if (stopOrders.All(x => x.StatusCode == "Delivered")) CompleteStop(work.Wave, work.Stop!.StopId);
        if (Stops(work.Wave).All(x => x.StatusCode == "Completed")) work.Wave.StatusCode = "Completed";
        work.Wave.Revision++; await completionRecorder.SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Wave.Revision, work.Wave.StatusCode, null, r.Lines.Sum(x => x.Quantity));
    }

    internal async Task<(string, long, string, string?, int)> ReturnAsync(DistributedActor actor, string id,
        DistributedReturnBatchRequest r, DistributedCustodyWork work, CancellationToken ct)
    {
        var completed = new List<DistributedCompletionLineDraft>();
        foreach (var line in r.Lines)
        {
            var item = work.Reservations.Single(x => x.ReservationId == line.ReservationId); var lot = work.Lots.Single(x => x.LotId == item.LotId);
            var changed = await db.입고상품.Where(x => x.Id == lot.InboundItemId && x.소유자UserId == lot.OwnerUserId && x.판매자UserId == lot.SellerUserId && x.보관위치 == work.Location.Barcode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, x => x.가용수량 + line.Quantity).SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "DistributedInventoryLineageMismatch"); item.ReturnedQuantity += line.Quantity; item.Revision++;
            var history = await completionRecorder.InventoryHistory(lot.InboundItemId, actor.UserId, "실물반환", line.Quantity, PhotoMemo(r.PhotoEvidenceIds), ct);
            completed.Add(new(DistributedCompletionRecorder.CompletionLine(actor, "Return", line.ReservationId, lot, line.Quantity, r.PhotoEvidenceIds, item, work.Wave), history));
        }
        work.Site.Revision++;
        foreach (var order in work.Orders)
        {
            var all = await Query<분산재고예약Record>().Where(x => x.OrderId == order.OrderId).ToArrayAsync(ct);
            if (all.All(x => x.PickedQuantity == x.ReturnedQuantity + x.DeliveredQuantity))
            {
                order.StatusCode = "Cancelled"; order.Revision++;
                foreach (var item in all) (await db.출고예정.SingleAsync(x => x.Id == item.OutboundPlanId, ct)).상태 = 출고상태.취소;
                await cargo.CancelAsync(actor.UserId, order.CargoRequestId, false, PhotoMemo(r.PhotoEvidenceIds), ct);
            }
        }
        await RefreshCancellationStatus(work.Wave, ct); work.Wave.Revision++; await completionRecorder.SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Wave.Revision, work.Wave.StatusCode, work.Reservations.Length == 1 ? work.Reservations[0].ReservationId : null, r.Lines.Sum(x => x.Quantity));
    }

    internal async Task TouchSite(string id, CancellationToken ct)
    { var site = await Site(id, ct); site.Revision++; }
    internal async Task RefreshCancellationStatus(분산배송WaveRecord wave, CancellationToken ct)
    {
        var orders = await Query<분산배송주문Record>().Where(x => x.WaveId == wave.WaveId).ToArrayAsync(ct);
        wave.StatusCode = orders.Any(x => x.StatusCode == "ReturnRequired") ? "ReturnRequired"
            : orders.Any(x => x.StatusCode is not "Cancelled" and not "Delivered") ? wave.StatusCode == "Open" && Now < wave.CutoffUtc ? "Open" : "NeedsReplan"
            : orders.Any(x => x.StatusCode == "Delivered") ? "Completed" : "Cancelled";
    }
}
