using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    public Task<DistributedCommandResponse> ReceiveBatchAsync(DistributedActor actor, string id, DistributedInboundReceiptBatchRequest r, CancellationToken ct)
        => Execute(actor, "ReceiveBatch", id, r, () => ReceiveCore(actor, id, r, ct), ct);
    public Task<DistributedCommandResponse> PickBatchAsync(DistributedActor actor, string id, DistributedPickBatchRequest r, CancellationToken ct)
        => Execute(actor, "PickBatch", id, r, () => PickCore(actor, id, r, ct), ct);
    public Task<DistributedCommandResponse> DeliverBatchAsync(DistributedActor actor, string id, DistributedDeliveryBatchRequest r, CancellationToken ct)
        => Execute(actor, "DeliverBatch", id, r, () => DeliverCore(actor, id, r, ct), ct);
    public Task<DistributedCommandResponse> ReturnBatchAsync(DistributedActor actor, string id, DistributedReturnBatchRequest r, CancellationToken ct)
        => Execute(actor, "ReturnBatch", id, r, () => ReturnCore(actor, id, r, ct), ct);

    private async Task<DistributedReceiveWork> PrepareReceive(DistributedActor actor, string id, DistributedInboundReceiptBatchRequest r, CancellationToken ct)
    {
        var plan = await Query<분산입고계획Record>().SingleOrDefaultAsync(x => x.PlanId == id, ct);
        Require(plan is not null, "DistributedInboundNotFound", 404); Revision(plan!.Revision, r);
        ValidateLines(r.Lines, x => x.LineKey, x => x.Quantity);
        var ids = r.Lines.Select(x => x.LineKey).ToArray();
        var lots = await Query<분산재고LotRecord>().Where(x => x.PlanId == id && Enumerable.Contains(ids, x.LineKey)).ToArrayAsync(ct);
        Require(lots.Length == ids.Length, "DistributedLotNotFound", 404);
        Require(lots.Select(x => x.LocationId).Distinct().Count() == 1 && lots.Select(x => x.OwnerUserId).Distinct().Count() == 1, "DistributedBatchScopeMismatch");
        var site = await Site(lots[0].SiteId, ct); Require(Manages(actor, site), "DistributedSiteManagerRequired", 403);
        Require(site.StatusCode == "Active", "DistributedConsentRequired");
        var location = await Query<분산적재위치Record>().SingleAsync(x => x.LocationId == lots[0].LocationId, ct);
        Require(r.LocationBarcode == location.Barcode, "DistributedLocationMismatch");
        foreach (var line in r.Lines)
        {
            var lot = lots.Single(x => x.LineKey == line.LineKey);
            Require(!ClosedInbound(lot), "DistributedInboundClosed");
            Require(line.Sku == lot.Sku && lot.ReceivedQuantity + line.Quantity <= lot.ExpectedQuantity, "DistributedInboundReceiptMismatch");
            await CheckInboundApproval(lot, site, ct);
        }
        var agreement = await ActiveAgreement(lots[0], ct);
        foreach (var lot in lots.Skip(1)) Require((await ActiveAgreement(lot, ct)).AgreementId == agreement.AgreementId, "DistributedBatchAgreementMismatch");
        await AgreementCapacity(agreement, r.Lines.Sum(x => x.Quantity), ct);
        return new(plan, site, location, agreement, lots);
    }
    private async Task<(string, long, string, string?, int)> ReceiveCore(DistributedActor actor, string id, DistributedInboundReceiptBatchRequest r, CancellationToken ct)
    {
        var work = await PrepareReceive(actor, id, r, ct);
        await BindWork(actor, new() { Kind = "Receive", AggregateId = id, Receive = r }, r.PhotoEvidenceIds, ct);
        return await inventoryCommands.ReceiveAsync(actor, id, r, work, ct);
    }
    private async Task<DistributedCustodyWork> PreparePick(DistributedActor actor, string id, DistributedPickBatchRequest r, CancellationToken ct)
    {
        var wave = await Wave(id, ct); Revision(wave.Revision, r); var stop = CurrentStop(wave, r.StopId, "Pickup");
        ValidateLines(r.Lines, x => x.ReservationId, x => x.Quantity);
        var ids = r.Lines.Select(x => x.ReservationId).ToArray(); var reservations = await Query<분산재고예약Record>().Where(x => x.WaveId == id && Enumerable.Contains(ids, x.ReservationId)).ToArrayAsync(ct);
        Require(reservations.Length == ids.Length && reservations.All(x => Enumerable.Contains(stop.OrderIds, x.OrderId)), "DistributedPickOrderMismatch");
        await Assigned(actor, wave, reservations.Select(x => x.OrderId).Distinct(), ct);
        Require(Now >= stop.WindowStartUtc && Now <= stop.WindowEndUtc, "DistributedPickupWindowClosed");
        var lotIds = reservations.Select(x => x.LotId).ToArray(); var lots = await Query<분산재고LotRecord>().Where(x => Enumerable.Contains(lotIds, x.LotId)).ToArrayAsync(ct);
        Require(lots.Select(x => x.LocationId).Distinct().Count() == 1 && lots.Select(x => x.OwnerUserId).Distinct().Count() == 1, "DistributedBatchScopeMismatch");
        var location = await Query<분산적재위치Record>().SingleAsync(x => x.LocationId == lots[0].LocationId, ct);
        Require(location.Barcode == r.LocationBarcode && stop.SiteId == lots[0].SiteId, "DistributedPickReceiptMismatch");
        var site = await Site(lots[0].SiteId, ct); Require(site.StatusCode == "Active", "DistributedConsentRequired");
        var orderIds = reservations.Select(x => x.OrderId).Distinct().ToArray(); var orders = await Query<분산배송주문Record>().Where(x => Enumerable.Contains(orderIds, x.OrderId)).ToArrayAsync(ct);
        foreach (var line in r.Lines)
        {
            var reservation = reservations.Single(x => x.ReservationId == line.ReservationId); var lot = lots.Single(x => x.LotId == reservation.LotId);
            Require(line.OwnerUserId == lot.OwnerUserId && line.Sku == lot.Sku && reservation.PickedQuantity + reservation.ReleasedQuantity + line.Quantity <= reservation.Quantity, "DistributedPickReceiptMismatch");
            var order = orders.Single(x => x.OrderId == reservation.OrderId); Require(order.StatusCode != "Cancelled", "DistributedOrderCancelled");
            var link = await db.운송의뢰상품연결.AsNoTracking().SingleOrDefaultAsync(x => x.운송의뢰Id == order.CargoRequestId && x.입고상품Id == lot.InboundItemId, ct);
            Require(link?.할당수량 == reservation.Quantity, "DistributedCargoAllocationMismatch");
        }
        return new(wave, stop, site, location, lots, reservations, orders);
    }
    private async Task<(string, long, string, string?, int)> PickCore(DistributedActor actor, string id, DistributedPickBatchRequest r, CancellationToken ct)
    {
        var work = await PreparePick(actor, id, r, ct);
        await BindWork(actor, new() { Kind = "Pick", AggregateId = id, Pick = r }, r.PhotoEvidenceIds, ct);
        return await pickingCommands.PickAsync(actor, id, r, work, ct);
    }
    private async Task<DistributedCustodyWork> PrepareDelivery(DistributedActor actor, string id, DistributedDeliveryBatchRequest r, CancellationToken ct)
    {
        var wave = await Wave(id, ct); Revision(wave.Revision, r); var stop = CurrentStop(wave, r.StopId, "Delivery");
        ValidateLines(r.Lines, x => x.ReservationId, x => x.Quantity);
        var ids = r.Lines.Select(x => x.ReservationId).ToArray(); var reservations = await Query<분산재고예약Record>().Where(x => x.WaveId == id && Enumerable.Contains(ids, x.ReservationId)).ToArrayAsync(ct);
        Require(reservations.Length == ids.Length && reservations.All(x => Enumerable.Contains(stop.OrderIds, x.OrderId)), "DistributedDeliveryOrderMismatch");
        var orderIds = reservations.Select(x => x.OrderId).Distinct().ToArray(); await Assigned(actor, wave, orderIds, ct);
        Require(Now >= stop.WindowStartUtc && Now <= stop.WindowEndUtc, "DistributedDeliveryWindowClosed");
        var all = await Query<분산재고예약Record>().Where(x => Enumerable.Contains(stop.OrderIds, x.OrderId)).ToArrayAsync(ct);
        Require(all.All(x => x.PickedQuantity == x.Quantity && x.ReturnedQuantity == 0 && x.ReleasedQuantity == 0), "DistributedOrderNotFullyPicked");
        var orders = await Query<분산배송주문Record>().Where(x => Enumerable.Contains(orderIds, x.OrderId)).ToArrayAsync(ct);
        Require(orders.Select(x => x.CustomerUserId).Distinct().Count() == 1 && orders.All(x => x.StatusCode != "Cancelled"), "DistributedBatchScopeMismatch");
        foreach (var line in r.Lines) { var item = reservations.Single(x => x.ReservationId == line.ReservationId); Require(item.DeliveredQuantity + line.Quantity <= item.PickedQuantity, "DistributedDeliveryQuantityMismatch"); }
        var lotIds = reservations.Select(x => x.LotId).ToArray(); var lots = await Query<분산재고LotRecord>().Where(x => Enumerable.Contains(lotIds, x.LotId)).ToArrayAsync(ct);
        return new(wave, stop, null!, null!, lots, reservations, orders);
    }
    private async Task<(string, long, string, string?, int)> DeliverCore(DistributedActor actor, string id, DistributedDeliveryBatchRequest r, CancellationToken ct)
    {
        var work = await PrepareDelivery(actor, id, r, ct);
        await BindWork(actor, new() { Kind = "Deliver", AggregateId = id, Deliver = r }, r.PhotoEvidenceIds, ct);
        return await deliveryCommands.DeliverAsync(actor, id, r, work, ct);
    }
    private async Task<DistributedCustodyWork> PrepareReturn(DistributedActor actor, string id, DistributedReturnBatchRequest r, CancellationToken ct)
    {
        var wave = await Wave(id, ct); Revision(wave.Revision, r); ValidateLines(r.Lines, x => x.ReservationId, x => x.Quantity);
        var ids = r.Lines.Select(x => x.ReservationId).ToArray(); var reservations = await Query<분산재고예약Record>().Where(x => x.WaveId == id && Enumerable.Contains(ids, x.ReservationId)).ToArrayAsync(ct);
        Require(reservations.Length == ids.Length, "DistributedReservationNotFound", 404);
        var lotIds = reservations.Select(x => x.LotId).ToArray(); var lots = await Query<분산재고LotRecord>().Where(x => Enumerable.Contains(lotIds, x.LotId)).ToArrayAsync(ct);
        Require(lots.Select(x => x.LocationId).Distinct().Count() == 1 && lots.Select(x => x.OwnerUserId).Distinct().Count() == 1, "DistributedBatchScopeMismatch");
        var site = await Site(lots[0].SiteId, ct); Require(Manages(actor, site), "DistributedReturnReceiverRequired", 403);
        var location = await Query<분산적재위치Record>().SingleAsync(x => x.LocationId == lots[0].LocationId, ct); Require(location.Barcode == r.LocationBarcode, "DistributedLocationMismatch");
        var orderIds = reservations.Select(x => x.OrderId).Distinct().ToArray(); var orders = await Query<분산배송주문Record>().Where(x => Enumerable.Contains(orderIds, x.OrderId)).ToArrayAsync(ct);
        Require(orders.All(x => x.StatusCode == "ReturnRequired"), "DistributedReturnNotRequired");
        foreach (var line in r.Lines) { var item = reservations.Single(x => x.ReservationId == line.ReservationId); Require(item.ReturnedQuantity + item.DeliveredQuantity + line.Quantity <= item.PickedQuantity, "DistributedActualReturnMismatch"); }
        return new(wave, null, site, location, lots, reservations, orders);
    }
    private async Task<(string, long, string, string?, int)> ReturnCore(DistributedActor actor, string id, DistributedReturnBatchRequest r, CancellationToken ct)
    {
        var work = await PrepareReturn(actor, id, r, ct);
        await BindWork(actor, new() { Kind = "Return", AggregateId = id, Return = r }, r.PhotoEvidenceIds, ct);
        return await deliveryCommands.ReturnAsync(actor, id, r, work, ct);
    }
    private static void ValidateLines<T>(IReadOnlyList<T> lines, Func<T, string> key, Func<T, int> quantity)
        => Require(lines is { Count: > 0 and <= 100 } && lines.All(x => x is not null && !string.IsNullOrWhiteSpace(key(x)) && quantity(x) > 0)
            && lines.Select(key).Distinct(StringComparer.Ordinal).Count() == lines.Count, "DistributedBatchLinesInvalid", 400);
    internal static string PhotoMemo(IReadOnlyList<string> ids) => "photos:" + string.Join(",", ids);
}
