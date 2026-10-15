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

    private async Task<ReceiveWork> PrepareReceive(DistributedActor actor, string id, DistributedInboundReceiptBatchRequest r, CancellationToken ct)
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
        var completed = new List<CompletionLineDraft>();
        foreach (var line in r.Lines)
        {
            var lot = work.Lots.Single(x => x.LineKey == line.LineKey);
            var changed = await db.입고상품.Where(x => x.Id == lot.InboundItemId && x.소유자UserId == lot.OwnerUserId && x.판매자UserId == lot.SellerUserId && x.SKU == lot.Sku && x.입고수량 == lot.ReceivedQuantity)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.입고수량, x => x.입고수량 + line.Quantity).SetProperty(x => x.가용수량, x => x.가용수량 + line.Quantity)
                    .SetProperty(x => x.상태, "보관중").SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "DistributedInventoryConflict"); lot.ReceivedQuantity += line.Quantity; lot.Revision++; SnapshotAgreement(lot, work.Agreement, line.Quantity);
            var target = await db.입고요청.SingleAsync(x => x.Id == lot.TargetInboundId, ct);
            target.상태 = lot.ReceivedQuantity == lot.ExpectedQuantity ? "입고완료" : "부분입고"; target.입고완료일시 = target.상태 == "입고완료" ? Now : null;
            var history = await InventoryHistory(lot.InboundItemId, actor.UserId, "분할입고", line.Quantity, PhotoMemo(r.PhotoEvidenceIds), ct);
            completed.Add(new(CompletionLine(actor, "Receive", line.LineKey, lot, line.Quantity, r.PhotoEvidenceIds), history));
        }
        work.Site.Revision++; work.Agreement.Revision++;
        await RefreshInboundCompletion(work.Plan, ct); work.Plan.Revision++;
        foreach (var sourceId in work.Lots.Select(x => x.SourceInboundId).Distinct())
        {
            var source = await db.입고요청.SingleAsync(x => x.Id == sourceId, ct);
            var sourceLots = await Query<분산재고LotRecord>().Where(x => x.SourceInboundId == sourceId).ToArrayAsync(ct);
            source.상태 = sourceLots.Sum(x => x.ReceivedQuantity) == source.예정수량 ? "입고완료" : "부분입고";
            source.입고완료일시 = source.상태 == "입고완료" ? Now : null;
        }
        await SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Plan.Revision, work.Plan.StatusCode, null, r.Lines.Sum(x => x.Quantity));
    }
    private async Task<CustodyWork> PreparePick(DistributedActor actor, string id, DistributedPickBatchRequest r, CancellationToken ct)
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
        var completed = new List<CompletionLineDraft>();
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
            var history = await InventoryHistory(lot.InboundItemId, actor.UserId, "기사직접피킹", -line.Quantity, PhotoMemo(r.PhotoEvidenceIds), ct);
            completed.Add(new(CompletionLine(actor, "Pick", line.ReservationId, lot, line.Quantity, r.PhotoEvidenceIds, reservation, work.Wave), history));
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
        work.Wave.Revision++; await SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Wave.Revision, work.Wave.StatusCode, work.Reservations.Length == 1 ? work.Reservations[0].ReservationId : null, r.Lines.Sum(x => x.Quantity));
    }
    private async Task<CustodyWork> PrepareDelivery(DistributedActor actor, string id, DistributedDeliveryBatchRequest r, CancellationToken ct)
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
        var completed = new List<CompletionLineDraft>();
        foreach (var line in r.Lines)
        {
            var reservation = work.Reservations.Single(x => x.ReservationId == line.ReservationId);
            reservation.DeliveredQuantity += line.Quantity; reservation.Revision++;
            completed.Add(new(CompletionLine(actor, "Deliver", line.ReservationId, work.Lots.Single(x => x.LotId == reservation.LotId),
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
        work.Wave.Revision++; await SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Wave.Revision, work.Wave.StatusCode, null, r.Lines.Sum(x => x.Quantity));
    }
    private async Task<CustodyWork> PrepareReturn(DistributedActor actor, string id, DistributedReturnBatchRequest r, CancellationToken ct)
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
        var completed = new List<CompletionLineDraft>();
        foreach (var line in r.Lines)
        {
            var item = work.Reservations.Single(x => x.ReservationId == line.ReservationId); var lot = work.Lots.Single(x => x.LotId == item.LotId);
            var changed = await db.입고상품.Where(x => x.Id == lot.InboundItemId && x.소유자UserId == lot.OwnerUserId && x.판매자UserId == lot.SellerUserId && x.보관위치 == work.Location.Barcode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, x => x.가용수량 + line.Quantity).SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "DistributedInventoryLineageMismatch"); item.ReturnedQuantity += line.Quantity; item.Revision++;
            var history = await InventoryHistory(lot.InboundItemId, actor.UserId, "실물반환", line.Quantity, PhotoMemo(r.PhotoEvidenceIds), ct);
            completed.Add(new(CompletionLine(actor, "Return", line.ReservationId, lot, line.Quantity, r.PhotoEvidenceIds, item, work.Wave), history));
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
        await RefreshCancellationStatus(work.Wave, ct); work.Wave.Revision++; await SaveCompletionLines(actor, r, completed, ct);
        return (id, work.Wave.Revision, work.Wave.StatusCode, work.Reservations.Length == 1 ? work.Reservations[0].ReservationId : null, r.Lines.Sum(x => x.Quantity));
    }
    private static void ValidateLines<T>(IReadOnlyList<T> lines, Func<T, string> key, Func<T, int> quantity)
        => Require(lines is { Count: > 0 and <= 100 } && lines.All(x => x is not null && !string.IsNullOrWhiteSpace(key(x)) && quantity(x) > 0)
            && lines.Select(key).Distinct(StringComparer.Ordinal).Count() == lines.Count, "DistributedBatchLinesInvalid", 400);
    private static string PhotoMemo(IReadOnlyList<string> ids) => "photos:" + string.Join(",", ids);
    private sealed record ReceiveWork(분산입고계획Record Plan, 분산거점Record Site, 분산적재위치Record Location, 분산보관합의Record Agreement, 분산재고LotRecord[] Lots);
    private sealed record CustodyWork(분산배송WaveRecord Wave, DistributedStopDto? Stop, 분산거점Record Site, 분산적재위치Record Location, 분산재고LotRecord[] Lots,
        분산재고예약Record[] Reservations, 분산배송주문Record[] Orders);
}
