using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    internal static List<DistributedStopDto> Stops(분산배송WaveRecord wave) => Deserialize<List<DistributedStopDto>>(wave.RouteJson);
    private static DistributedStopDto CurrentStop(분산배송WaveRecord wave, string id, string kind)
    {
        var stop = Stops(wave).FirstOrDefault(x => x.StatusCode == "Pending");
        Require(stop is not null && stop.StopId == id && stop.Kind == kind, "DistributedCurrentStopMismatch"); return stop!;
    }
    internal static void CompleteStop(분산배송WaveRecord wave, string id)
    { wave.RouteJson = Serialize(Stops(wave).Select(x => x.StopId == id ? x with { StatusCode = "Completed" } : x).ToArray()); }
    private async Task<분산재고예약Record> Reservation(string id, string waveId, CancellationToken ct)
        => await Query<분산재고예약Record>().SingleOrDefaultAsync(x => x.ReservationId == id && x.WaveId == waveId, ct)
            ?? throw new DistributedFulfillmentException("DistributedReservationNotFound", 404);
    private async Task Assigned(DistributedActor actor, 분산배송WaveRecord wave, IEnumerable<string> orderIds, CancellationToken ct)
    {
        Require(actor.IsDriver && wave.DriverId == actor.UserId && wave.StatusCode == "Accepted", "DistributedAssignedDriverRequired", 403);
        var ids = orderIds.ToArray(); var cargoIds = await Query<분산배송주문Record>().Where(x => x.WaveId == wave.WaveId && Enumerable.Contains(ids, x.OrderId)).Select(x => x.CargoRequestId).ToArrayAsync(ct);
        Require(cargoIds.Length == ids.Length && await db.운송원장.CountAsync(x => Enumerable.Contains(cargoIds, x.의뢰Id) && x.기사_운송자 == actor.UserId && x.확정기사Id == actor.UserId && x.상태 != "인수완료" && x.배차큐단계 != 살뜰.도메인.공통.상태값.배차큐단계.종료, ct) == ids.Length,
            "DistributedCargoAssignmentChanged", 403);
        Require(Now <= wave.DeliveryEndUtc, "DistributedPickingGrantExpired", 403);
        var boundOrders = await Query<분산배송주문Record>().Where(x => Enumerable.Contains(ids, x.OrderId)).ToArrayAsync(ct);
        var sources = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(cargoIds, x.의뢰Id)).ToDictionaryAsync(x => x.의뢰Id, ct);
        Require(boundOrders.All(x => sources.TryGetValue(x.CargoRequestId, out var s) && CargoDigest(s) == x.CargoEvidenceDigest), "DistributedCargoEvidenceChanged");
    }
    public Task<DistributedCommandResponse> PickAsync(DistributedActor actor, string id, DistributedPickRequest r, CancellationToken ct)
        => Execute(actor, "Pick", id, r, () => PickCore(actor, id, PickBatch(r), ct), ct);
    private static DistributedPickBatchRequest PickBatch(DistributedPickRequest r)
        => new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, StopId = r.StopId, LocationBarcode = r.LocationBarcode,
            PhotoEvidenceIds = r.PhotoEvidenceIds, Lines = [new() { ReservationId = r.ReservationId, OwnerUserId = r.OwnerUserId, Sku = r.Sku, Quantity = r.Quantity }] };
    public Task<DistributedCommandResponse> DeliverAsync(DistributedActor actor, string id, DistributedDeliveryRequest r, CancellationToken ct)
        => Execute(actor, "Deliver", id, r, async () => await DeliverCore(actor, id, await DeliveryBatch(id, r, ct), ct), ct);
    private async Task<DistributedDeliveryBatchRequest> DeliveryBatch(string id, DistributedDeliveryRequest r, CancellationToken ct)
    {
        var reservations = await Query<분산재고예약Record>().Where(x => x.WaveId == id && x.OrderId == r.OrderId).ToArrayAsync(ct);
        Require(reservations.Length > 0, "DistributedDeliveryOrderMismatch");
        return new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, StopId = r.StopId, PhotoEvidenceIds = r.PhotoEvidenceIds,
            Lines = reservations.Select(x => new DistributedDeliveryLineRequest { ReservationId = x.ReservationId, Quantity = x.Quantity - x.DeliveredQuantity - x.ReturnedQuantity }).ToArray() };
    }
    public Task<DistributedCommandResponse> CancelAsync(DistributedActor actor, string id, DistributedCancelRequest r, CancellationToken ct)
        => Execute(actor, "Cancel", id, r, async () =>
        {
            var wave = await Wave(id, ct); Revision(wave.Revision, r);
            var order = await Query<분산배송주문Record>().SingleOrDefaultAsync(x => x.WaveId == id && x.OrderId == r.OrderId, ct);
            Require(order is not null && (actor.IsAdmin || wave.CreatorUserId == actor.UserId || order.SellerUserId == actor.UserId || order.CustomerUserId == actor.UserId), "DistributedOrderPartyRequired", 403);
            Require(order!.StatusCode is not "Delivered" and not "Cancelled", "DistributedOrderCannotCancel"); Text(r.Reason, 400);
            var reservations = await Query<분산재고예약Record>().Where(x => x.OrderId == order.OrderId).OrderBy(x => x.LotId).ToArrayAsync(ct);
            var total = 0; var picked = false;
            foreach (var reservation in reservations)
            {
                var quantity = reservation.Quantity - reservation.PickedQuantity - reservation.ReleasedQuantity;
                var lot = await Query<분산재고LotRecord>().SingleAsync(x => x.LotId == reservation.LotId, ct);
                if (quantity > 0)
                {
                    var changed = await db.입고상품.Where(x => x.Id == lot.InboundItemId && x.소유자UserId == lot.OwnerUserId && x.판매자UserId == lot.SellerUserId && x.예약수량 >= quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.예약수량, x => x.예약수량 - quantity).SetProperty(x => x.가용수량, x => x.가용수량 + quantity).SetProperty(x => x.UpdatedAt, Now), ct);
                    Require(changed == 1, "DistributedReservedInventoryChanged"); reservation.ReleasedQuantity += quantity; total += quantity;
                }
                picked |= reservation.PickedQuantity > reservation.ReturnedQuantity; reservation.Revision++; await TouchSite(lot.SiteId, ct);
                var outbound = await db.출고예정.SingleAsync(x => x.Id == reservation.OutboundPlanId, ct);
                outbound.상태 = reservation.PickedQuantity > reservation.ReturnedQuantity ? "반환대기" : 출고상태.취소;
            }
            order.StatusCode = picked ? "ReturnRequired" : "Cancelled"; order.Revision++;
            await cargo.CancelAsync(actor.UserId, order.CargoRequestId, picked, r.Reason, ct);
            // Any cancellation invalidates route commitments; picked custody must be reconciled before planning again.
            await RefreshCancellationStatus(wave, ct); wave.Revision++;
            return (id, wave.Revision, wave.StatusCode, (string?)null, total);
        }, ct);
    public Task<DistributedCommandResponse> ReturnAsync(DistributedActor actor, string id, DistributedReturnRequest r, CancellationToken ct)
        => Execute(actor, "Return", id, r, () => ReturnCore(actor, id, ReturnBatch(r), ct), ct);
    private static DistributedReturnBatchRequest ReturnBatch(DistributedReturnRequest r)
        => new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, LocationBarcode = r.LocationBarcode,
            PhotoEvidenceIds = r.PhotoEvidenceIds, Lines = [new() { ReservationId = r.ReservationId, Quantity = r.Quantity }] };
    public Task<DistributedCommandResponse> AcknowledgeAsync(DistributedActor actor, string id, DistributedAcknowledgementRequest r, CancellationToken ct)
        => Execute(actor, "Acknowledge", id, r, async () =>
        {
            var wave = await Wave(id, ct); Revision(wave.Revision, r);
            var order = await Query<분산배송주문Record>().SingleOrDefaultAsync(x => x.OrderId == r.OrderId && x.WaveId == id, ct);
            Require(order is not null && order.CustomerUserId == actor.UserId, "DistributedCustomerRequired", 403);
            Require(order!.StatusCode == "Delivered", "DistributedDeliveryNotRecorded"); Text(r.EvidenceReference, 200);
            order.CustomerAcknowledged = true; order.Revision++; wave.Revision++;
            return (id, wave.Revision, wave.StatusCode, (string?)null, 0);
        }, ct);
    private Task<재고이력> InventoryHistory(long itemId, string actor, string kind, int quantity, string evidence, CancellationToken ct)
        => completionRecorder.InventoryHistory(itemId, actor, kind, quantity, evidence, ct);
    private Task RefreshCancellationStatus(분산배송WaveRecord wave, CancellationToken ct)
        => deliveryCommands.RefreshCancellationStatus(wave, ct);
}
