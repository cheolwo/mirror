using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.창고;
using 살뜰.도메인.운송;
using 살뜰.도메인.공통;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    public Task<DistributedCommandResponse> CreateWaveAsync(DistributedActor actor, DistributedWaveCreateRequest r, CancellationToken ct)
        => Execute(actor, "CreateWave", "", r, async () =>
        {
            await Operator(actor, ct);
            Require(r.ExpectedRevision == 0 && new[] { r.CutoffUtc, r.PickupStartUtc, r.PickupEndUtc, r.DeliveryStartUtc, r.DeliveryEndUtc }.All(x => x.Kind == DateTimeKind.Utc)
                && r.CutoffUtc > Now && r.CutoffUtc <= r.PickupStartUtc && r.PickupStartUtc < r.PickupEndUtc
                && r.DeliveryStartUtc < r.DeliveryEndUtc && r.PickupStartUtc <= r.DeliveryStartUtc
                && r.PickupServiceMinutes >= 0 && r.DeliveryServiceMinutes >= 0, "DistributedWaveWindowInvalid", 400);
            var wave = new 분산배송WaveRecord { WaveId = Id("wave"), RoundId = Id("round"), CreatorUserId = actor.UserId, Label = Text(r.Label),
                CutoffUtc = r.CutoffUtc, PickupStartUtc = r.PickupStartUtc, PickupEndUtc = r.PickupEndUtc, DeliveryStartUtc = r.DeliveryStartUtc,
                DeliveryEndUtc = r.DeliveryEndUtc, PickupServiceMinutes = r.PickupServiceMinutes, DeliveryServiceMinutes = r.DeliveryServiceMinutes };
            db.Add(wave); return (wave.WaveId, wave.Revision, wave.StatusCode, (string?)null, 0);
        }, ct);
    public Task<DistributedCommandResponse> OrderAsync(DistributedActor actor, string id, DistributedOrderRequest r, CancellationToken ct)
        => Execute(actor, "AddOrder", id, r, async () =>
        {
            var wave = await Wave(id, ct); Revision(wave.Revision, r);
            Require(wave.StatusCode == "Open" && Now < wave.CutoffUtc, "DistributedCutoffPassed");
            Require(r.Lines is { Count: > 0 and <= 100 } && r.Lines.All(x => x is not null && x.Quantity > 0) && r.Lines.Select(x => x.LotId).Distinct().Count() == r.Lines.Count, "DistributedOrderLinesInvalid", 400);
            var ship = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == r.CargoRequestId, ct);
            Require(ship is not null && ship.화주Id == actor.UserId && !string.IsNullOrWhiteSpace(ship.주문자UserId), "DistributedCargoOwnerRequired", 403);
            Require(Ssalddel.Application.Shipper.Request.화주운송배차진입정책.판정(ship!).가능, "DistributedCargoSettlementNotReady");
            Require(ship!.화물수량 == r.Lines.Sum(x => x.Quantity) && ship.하차_위도.HasValue && ship.하차_경도.HasValue
                && ship.하차_시간창_시작일시.HasValue && ship.하차_시간창_종료일시.HasValue && ship.화물중량Kg.HasValue && ship.화물부피Cbm.HasValue && ship.화물팔레트개수.HasValue,
                "DistributedCargoEvidenceMissing");
            var rootOrder = await RootOrder(actor, r, ship.주문자UserId!, ct);
            var fare = await db.운임구성.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == ship.의뢰Id, ct);
            Require(fare is not null && fare.기사지급예정운임.HasValue && fare.예상거리Km.HasValue && !string.IsNullOrWhiteSpace(fare.단가출처), "DistributedFareEvidenceMissing");
            var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == ship.의뢰Id && x.배차업무유형 == 상태값.배차업무유형.용달운송, ct);
            Require(queue is not null && string.IsNullOrWhiteSpace(queue.기사_운송자), "DistributedCargoAlreadyAssigned");
            var order = new 분산배송주문Record { OrderId = Id("order"), WaveId = id, OrderReference = Text(r.OrderReference, 100), CargoRequestId = ship.의뢰Id,
                CargoEvidenceDigest = CargoDigest(ship), SellerUserId = actor.UserId, CustomerUserId = ship.주문자UserId }; db.Add(order);
            var existingLinks = await db.운송의뢰상품연결.AsNoTracking().Where(x => x.운송의뢰Id == ship.의뢰Id).ToArrayAsync(ct);
            Require(r.Lines.All(x => x.ExistingOutboundPlanId.HasValue) || r.Lines.All(x => !x.ExistingOutboundPlanId.HasValue), "DistributedReservationModesCannotMix", 400);
            Require(existingLinks.Length == 0 || r.Lines.All(x => x.ExistingOutboundPlanId.HasValue), "DistributedCargoAllocationAlreadyReserved");
            foreach (var line in r.Lines.OrderBy(x => x.LotId, StringComparer.Ordinal))
            {
                var lot = await Query<분산재고LotRecord>().SingleOrDefaultAsync(x => x.LotId == line.LotId, ct);
                Require(lot is not null && lot.SellerUserId == actor.UserId, "DistributedStockSellerMismatch", 403);
                var site = await Site(lot!.SiteId, ct); Require(site.StatusCode == "Active", "DistributedConsentRequired");
                var agreement = await ActiveAgreement(lot, ct);
                var item = await db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId, ct);
                Require(item.판매자UserId == actor.UserId && item.소유자UserId == lot.OwnerUserId && item.SKU == lot.Sku && item.창고Id == site.WarehouseId, "DistributedInventoryLineageMismatch");
                출고예정 outbound;
                if (line.ExistingOutboundPlanId is { } existingId)
                {
                    outbound = await db.출고예정.SingleOrDefaultAsync(x => x.Id == existingId, ct) ?? throw new DistributedFulfillmentException("DistributedOutboundNotFound", 404);
                    Require(outbound.입고상품Id == item.Id && outbound.판매자UserId == actor.UserId && outbound.출고창고Id == site.WarehouseId
                        && outbound.수량 == line.Quantity && outbound.상태 is "예정" or "준비중" && item.예약수량 >= line.Quantity,
                        "DistributedExistingReservationMismatch");
                    Require(outbound.운송의뢰Id is null || outbound.운송의뢰Id == ship.의뢰Id, "DistributedCargoAllocationMismatch");
                    if (existingLinks.Length > 0) Require(existingLinks.Any(x => x.입고상품Id == item.Id && x.할당수량 == line.Quantity), "DistributedCargoAllocationMismatch");
                    outbound.운송의뢰Id = ship.의뢰Id;
                }
                else
                {
                    var changed = await db.입고상품.Where(x => x.Id == item.Id && x.소유자UserId == lot.OwnerUserId && x.판매자UserId == actor.UserId && x.SKU == lot.Sku
                        && x.창고Id == site.WarehouseId && x.보관위치 == item.보관위치 && x.가용수량 >= line.Quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, x => x.가용수량 - line.Quantity).SetProperty(x => x.예약수량, x => x.예약수량 + line.Quantity).SetProperty(x => x.UpdatedAt, Now), ct);
                    Require(changed == 1, "DistributedStockInsufficientOrChanged");
                    // 표시명은 이행 원장 ID가 아닙니다. 여러 주문에 같은 표시명을 쓰더라도 출고 원장은 독립입니다.
                    outbound = new 출고예정 { 주문참조번호 = order.OrderId, 입고상품Id = item.Id, 판매자UserId = actor.UserId,
                        주문자UserId = order.CustomerUserId, 출고창고Id = site.WarehouseId, 상품명 = item.상품명, SKU = item.SKU,
                        수량 = line.Quantity, 상태 = 출고상태.준비중, 입고요청Id = item.입고요청Id, 운송의뢰Id = ship.의뢰Id };
                    db.Add(outbound);
                }
                await db.SaveChangesAsync(ct);
                if (existingLinks.Length == 0) db.Add(new 운송의뢰상품연결 { 운송의뢰Id = ship.의뢰Id, 입고상품Id = item.Id, 할당수량 = line.Quantity });
                var reservation = new 분산재고예약Record { ReservationId = Id("reservation"), WaveId = id, OrderId = order.OrderId, LotId = lot.LotId, OutboundPlanId = outbound.Id, Quantity = line.Quantity };
                db.Add(reservation); 분산주문원장Metadata.Reservation(wave, reservation, agreement, Now); agreement.Revision++;
                site.Revision++;
            }
            if (existingLinks.Length > 0) Require(existingLinks.Sum(x => x.할당수량) == r.Lines.Sum(x => x.Quantity), "DistributedCargoAllocationMismatch");
            if (rootOrder is not null)
            {
                var outboundIds = await Query<분산재고예약Record>().Where(x => x.OrderId == order.OrderId).Select(x => x.OutboundPlanId).ToArrayAsync(ct);
                // 마지막 예약은 아직 tracker에 있을 수 있으므로 실제 확정될 모든 행을 함께 보존합니다.
                outboundIds = outboundIds.Concat(db.ChangeTracker.Entries<분산재고예약Record>().Where(x => x.Entity.OrderId == order.OrderId)
                    .Select(x => x.Entity.OutboundPlanId)).Distinct().ToArray();
                분산주문원장Metadata.Connection(wave, new()
                {
                    OrderId = order.OrderId, RootOrderLedgerId = rootOrder.RootOrderLedgerId, ExpectedRootRevision = rootOrder.Revision,
                    RootBusinessDigest = rootOrder.BusinessDigest, SellerUserId = order.SellerUserId, CustomerUserId = order.CustomerUserId,
                    CargoRequestId = order.CargoRequestId, OutboundPlanIds = outboundIds, RequestId = r.RequestId,
                    OrderRequestId = r.RequestId, RecordedAtUtc = Now
                });
            }
            wave.Revision++; return (id, wave.Revision, wave.StatusCode, (string?)null, r.Lines.Sum(x => x.Quantity));
        }, ct);
    public Task<DistributedCommandResponse> CloseAsync(DistributedActor actor, string id, DistributedCommandRequest r, CancellationToken ct)
        => Execute(actor, "Close", id, r, async () =>
        {
            var wave = await Wave(id, ct); OwnWave(actor, wave); Revision(wave.Revision, r);
            Require(wave.StatusCode == "Open" && await Query<분산배송주문Record>().AnyAsync(x => x.WaveId == id, ct), "DistributedWaveCannotClose");
            wave.StatusCode = "Closed"; wave.Revision++; return (id, wave.Revision, wave.StatusCode, (string?)null, 0);
        }, ct);
    public Task<DistributedCommandResponse> PlanAsync(DistributedActor actor, string id, DistributedRoundPlanRequest r, CancellationToken ct)
        => Execute(actor, "Plan", id, r, async () =>
        {
            var wave = await Wave(id, ct); OwnWave(actor, wave); Revision(wave.Revision, r);
            Require(wave.StatusCode is "Closed" or "Planned" or "NeedsReplan" or "AcceptancePending", "DistributedWaveCannotPlan");
            if (sharedAdmission is not null) await sharedAdmission.LockAsync(r.DriverId, ct);
            var driver = await db.용달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == r.DriverId && x.상태 == "활동중", ct);
            Require(driver is not null && driverLocations.TryGetLatest(r.DriverId, out _), "DistributedDriverLocationMissing");
            driverLocations.TryGetLatest(r.DriverId, out var location);
            Require(location is not null && location.RecordedAtUtc >= Now.AddMinutes(-2) && location.RecordedAtUtc <= Now.AddMinutes(1), "DistributedDriverLocationStale");
            var vehicle = await db.차량제원.AsNoTracking().SingleOrDefaultAsync(x => x.차량코드 == driver!.차량 || x.차량명 == driver.차량, ct);
            Require(vehicle?.권장최대CBM.HasValue == true && vehicle.팔레트적재개수.HasValue, "DistributedVehicleCapacityMissing");
            var orders = await Query<분산배송주문Record>().Where(x => x.WaveId == id && x.StatusCode != "Cancelled" && x.StatusCode != "Delivered").ToArrayAsync(ct);
            Require(orders.Length > 0, "DistributedNoRemainingOrders");
            var cargoIds = orders.Select(x => x.CargoRequestId).ToArray();
            var existingAssignments = await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains(cargoIds, x.의뢰Id)
                && x.상태 != "인수완료" && x.배차큐단계 != 상태값.배차큐단계.종료)
                .ToArrayAsync(ct);
            Require(existingAssignments.All(x => (string.IsNullOrWhiteSpace(x.기사_운송자) || x.기사_운송자 == r.DriverId)
                && (string.IsNullOrWhiteSpace(x.확정기사Id) || x.확정기사Id == r.DriverId)), "DistributedCargoAssignmentDriverLocked");
            if (sharedAdmission is not null)
            {
                // 계획 전체를 한 번에 검사해 개별 건은 맞지만 전체 묶음이 희망 한도를 초과하는 계획을 막습니다.
                // 동일 요청의 영수증은 Execute가 먼저 복구하고, 기존 확정 건은 공통 관문이 중복 계산하지 않습니다.
                var participation = await sharedAdmission.CheckAsync(r.DriverId, cargoIds, ct);
                if (participation.IsFailed)
                {
                    var error = participation.Errors[0];
                    throw new DistributedFulfillmentException(error.Metadata.GetValueOrDefault("ErrorCode")?.ToString()
                        ?? "DistributedDriverParticipationRequired");
                }
            }
            Require(!await db.운송원장.AnyAsync(x => x.기사_운송자 == r.DriverId && x.상태 != "인수완료" && x.배차큐단계 != 살뜰.도메인.공통.상태값.배차큐단계.종료 && !Enumerable.Contains(cargoIds, x.의뢰Id), ct), "DistributedDriverOtherCommitmentsRequireReplan");
            var orderIdsForPlan = orders.Select(x => x.OrderId).ToArray();
            var reservations = await Query<분산재고예약Record>().Where(x => Enumerable.Contains(orderIdsForPlan, x.OrderId)).ToArrayAsync(ct);
            Require(!reservations.Any(x => x.ReturnedQuantity > 0 || x.ReleasedQuantity > 0), "DistributedCustodyNeedsReconciliation");
            var hasPicked = reservations.Any(x => x.PickedQuantity > x.DeliveredQuantity);
            Require(!hasPicked || wave.DriverId == r.DriverId, "DistributedPickedCustodyDriverLocked");
            var lotIds = reservations.Select(x => x.LotId).ToArray(); var lots = await Query<분산재고LotRecord>().Where(x => Enumerable.Contains(lotIds, x.LotId)).ToDictionaryAsync(x => x.LotId, ct);
            var siteIds = lots.Values.Select(x => x.SiteId).Distinct().ToArray(); var sites = await Query<분산거점Record>().Where(x => Enumerable.Contains(siteIds, x.SiteId)).ToDictionaryAsync(x => x.SiteId, ct);
            Require(sites.Values.All(x => x.StatusCode == "Active"), "DistributedConsentRequired");
            var warehouseIds = sites.Values.Select(x => x.WarehouseId).ToArray(); var warehouses = await db.창고.AsNoTracking().Where(x => Enumerable.Contains(warehouseIds, x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(cargoIds, x.의뢰Id)).ToDictionaryAsync(x => x.의뢰Id, ct);
            Require(orders.All(x => requests.TryGetValue(x.CargoRequestId, out var source) && x.CargoEvidenceDigest == CargoDigest(source)), "DistributedCargoEvidenceChanged");
            var fares = await db.운임구성.AsNoTracking().Where(x => Enumerable.Contains(cargoIds, x.의뢰Id)).ToArrayAsync(ct);
            Require(fares.Length == orders.Length && fares.All(x => x.기사지급예정운임.HasValue && x.예상거리Km.HasValue), "DistributedFareEvidenceMissing");
            var loads = reservations.ToDictionary(x => x.ReservationId, x =>
            { var req = requests[orders.Single(o => o.OrderId == x.OrderId).CargoRequestId]; var factor = (decimal)x.Quantity / req.화물수량!.Value;
                return new 분산경로적재량(req.화물중량Kg!.Value * factor, req.화물부피Cbm!.Value * factor, req.화물팔레트개수!.Value * factor); });
            var pickupLoads = reservations.ToDictionary(x => x.ReservationId, x => Scale(loads[x.ReservationId], (decimal)(x.Quantity - x.PickedQuantity) / x.Quantity));
            var initialLoad = reservations.Aggregate(new 분산경로적재량(0, 0, 0), (sum, x) => sum + Scale(loads[x.ReservationId], (decimal)(x.PickedQuantity - x.DeliveredQuantity) / x.Quantity));
            var initialPicked = reservations.Where(x => x.PickedQuantity == x.Quantity).Select(x => x.ReservationId).ToArray();
            var pickups = new List<분산경로Stop>(); var separate = new List<분산경로Stop>();
            foreach (var group in reservations.Where(x => x.PickedQuantity < x.Quantity).GroupBy(x => lots[x.LotId].SiteId))
            {
                var warehouse = warehouses[sites[group.Key].WarehouseId]; Require(warehouse.IsActive && warehouse.위도.HasValue && warehouse.경도.HasValue, "DistributedPickupCoordinatesMissing");
                var groupOrders = group.Select(x => x.OrderId).Distinct().ToArray(); var groupRequests = groupOrders.Select(x => requests[orders.Single(o => o.OrderId == x).CargoRequestId]).ToArray();
                var start = groupRequests.Select(x => x.픽업_시간창_시작일시).Append(wave.PickupStartUtc).Max();
                var end = groupRequests.Select(x => x.픽업_시간창_종료일시).Append(wave.PickupEndUtc).Min(); Require(start < end, "DistributedPickupWindowMismatch");
                var stop = new 분산경로Stop("pickup-" + group.Key, "Pickup", group.Key, groupOrders, group.Select(x => x.ReservationId).ToArray(), warehouse.주소,
                    new(warehouse.위도!.Value, warehouse.경도!.Value), start, end, wave.PickupServiceMinutes, pickupLoads);
                pickups.Add(stop);
                foreach (var orderGroup in group.GroupBy(x => x.OrderId)) separate.Add(stop with { StopId = stop.StopId + "-" + orderGroup.Key,
                    OrderIds = [orderGroup.Key], ReservationIds = orderGroup.Select(x => x.ReservationId).ToArray() });
            }
            var deliveries = new List<분산경로Stop>();
            foreach (var group in orders.GroupBy(x => x.CustomerUserId))
            {
                var reqs = group.Select(x => requests[x.CargoRequestId]).ToArray(); var first = reqs[0];
                Require(reqs.All(x => x.하차_도로명주소 == first.하차_도로명주소 && x.하차_위도 == first.하차_위도 && x.하차_경도 == first.하차_경도), "DistributedCustomerDestinationMismatch");
                var start = reqs.Select(x => x.하차_시간창_시작일시!.Value).Append(wave.DeliveryStartUtc).Max();
                var end = reqs.Select(x => x.하차_시간창_종료일시!.Value).Append(wave.DeliveryEndUtc).Min(); Require(start < end, "DistributedDeliveryWindowMismatch");
                var orderIds = group.Select(x => x.OrderId).ToArray();
                deliveries.Add(new("delivery-" + group.First().OrderId, "Delivery", null, orderIds, reservations.Where(x => Enumerable.Contains(orderIds, x.OrderId)).Select(x => x.ReservationId).ToArray(),
                    first.하차_도로명주소, new(first.하차_위도!.Value, first.하차_경도!.Value), start, end, wave.DeliveryServiceMinutes, loads));
            }
            var plan = await planner.PlanAsync(new(location!.Latitude, location.Longitude), Now,
                new(vehicle!.최대적재중량Kg, vehicle.권장최대CBM!.Value, vehicle.팔레트적재개수!.Value), pickups.Concat(deliveries).ToArray(), separate.Concat(deliveries).ToArray(), ct, initialLoad, initialPicked);
            var completed = Stops(wave).Where(x => x.StatusCode == "Completed").ToArray();
            wave.RouteJson = Serialize(completed.Concat(plan.Stops).Select((x, index) => x with { Sequence = index + 1 }).ToArray());
            var previousEvidence = JsonNode.Parse(wave.RouteEvidenceJson) as JsonObject;
            var planEvidence = JsonNode.Parse(plan.EvidenceJson) as JsonObject ?? new JsonObject();
            if (previousEvidence?["cargoAcceptanceHistory"] is { } history) planEvidence["cargoAcceptanceHistory"] = history.DeepClone();
            if (previousEvidence is not null) 분산주문원장Metadata.Preserve(previousEvidence, planEvidence);
            wave.RouteEvidenceJson = planEvidence.ToJsonString(Json); wave.TotalDistanceKm = plan.DistanceKm;
            wave.TotalElapsedMinutes = plan.ElapsedMinutes; wave.OfferedDriverFareKrw = fares.Sum(x => x.기사지급예정운임!.Value);
            wave.DriverId = r.DriverId; wave.StatusCode = "Planned"; wave.Revision++;
            return (id, wave.Revision, wave.StatusCode, (string?)null, 0);
        }, ct);
    public async Task<DistributedCommandResponse> AcceptAsync(DistributedActor actor, string id, DistributedRoundAcceptRequest r, CancellationToken ct)
    {
        Gate(actor); Require(r.RequestId != Guid.Empty, "DistributedRequestIdRequired", 400); var wave = await Wave(id, ct);
        var acknowledgements = r.AcknowledgedWarningCodes?.ToArray() ?? throw new DistributedFulfillmentException("DistributedInputInvalid", 400);
        Require(acknowledgements.All(x => !string.IsNullOrWhiteSpace(x)), "DistributedInputInvalid", 400);
        Require(actor.IsDriver && wave.DriverId == actor.UserId, "DistributedAssignedDriverRequired", 403);
        var results = new List<DistributedCargoAcceptanceResult>();
        var replay = await Query<분산물류CommandReceipt>().AsNoTracking().AnyAsync(x => x.ActorUserId == actor.UserId && x.RequestId == r.RequestId, ct);
        if (!replay)
        {
            Revision(wave.Revision, r); Require(wave.StatusCode is "Planned" or "AcceptancePending", "DistributedRoundCannotAccept");
            Require(Now <= wave.DeliveryEndUtc && Stops(wave).Where(x => x.StatusCode == "Pending").All(x => Now <= x.WindowEndUtc), "DistributedPickingGrantExpired");
            Require(driverLocations.TryGetLatest(actor.UserId, out var currentLocation) && currentLocation.RecordedAtUtc >= Now.AddMinutes(-2), "DistributedDriverLocationStale");
            var orders = await Query<분산배송주문Record>().Where(x => x.WaveId == id && x.StatusCode != "Cancelled" && x.StatusCode != "Delivered").ToArrayAsync(ct);
            if (sharedAdmission?.Enabled == true)
            {
                // 계획 뒤 희망 한도가 바뀐 경우에도 첫 화물 수락 전에 전체 묶음을 재검사합니다.
                // 이 사전 검사는 용량 예약이 아닙니다. 각 화물 수락이 최신 사실을 다시 확인합니다.
                await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var preflight = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                    var participation = await sharedAdmission.CheckAsync(actor.UserId, orders.Select(x => x.CargoRequestId).ToArray(), ct);
                    if (participation.IsFailed)
                    {
                        var error = participation.Errors[0];
                        throw new DistributedFulfillmentException(error.Metadata.GetValueOrDefault("ErrorCode")?.ToString()
                            ?? "DistributedDriverParticipationRequired");
                    }
                    await preflight.CommitAsync(ct);
                });
            }
            // Each existing cargo acceptance has its own transaction. Never describe partial assignment as a fully accepted round.
            foreach (var order in orders)
            {
                db.ChangeTracker.Clear(); var latest = await Wave(id, ct);
                Revision(latest.Revision, r); Require(latest.DriverId == actor.UserId && latest.StatusCode is "Planned" or "AcceptancePending", "DistributedRoundCannotAccept");
                Require(Now <= latest.DeliveryEndUtc, "DistributedPickingGrantExpired");
                var source = await db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == order.CargoRequestId, ct);
                Require(order.CargoEvidenceDigest == CargoDigest(source), "DistributedCargoEvidenceChanged");
                // Confirmation is scoped to this cargo's last returned warning; a shared code cannot approve another cargo's unseen message.
                var shownCodes = LatestAcceptance(latest)?.Results.Where(x => x.CargoRequestId == order.CargoRequestId)
                    .SelectMany(x => x.Warnings).Where(x => x.CargoRequestId == order.CargoRequestId)
                    .Select(x => x.Code).ToHashSet(StringComparer.Ordinal) ?? [];
                var acknowledgedForCargo = acknowledgements.Where(shownCodes.Contains).Distinct(StringComparer.Ordinal).ToArray();
                var result = await cargo.AcceptAsync(actor.UserId, order.CargoRequestId, acknowledgedForCargo, ct);
                results.Add(result); if (!result.Accepted) break;
            }
            db.ChangeTracker.Clear();
        }
        return await Execute(actor, "Accept", id, r, async () =>
        {
            var current = await Wave(id, ct); Revision(current.Revision, r);
            var cargoIds = await Query<분산배송주문Record>().Where(x => x.WaveId == id && x.StatusCode != "Cancelled" && x.StatusCode != "Delivered").Select(x => x.CargoRequestId).ToArrayAsync(ct);
            var assigned = await db.운송원장.AsNoTracking().CountAsync(x => Enumerable.Contains(cargoIds, x.의뢰Id) && x.기사_운송자 == actor.UserId && x.확정기사Id == actor.UserId
                && x.상태 != "인수완료" && x.배차큐단계 != 상태값.배차큐단계.종료, ct);
            var evidence = JsonNode.Parse(current.RouteEvidenceJson) as JsonObject ?? new JsonObject();
            var history = evidence["cargoAcceptanceHistory"]?.Deserialize<List<DistributedAcceptanceAttempt>>(Json) ?? [];
            history.Add(new(r.RequestId, actor.UserId, acknowledgements, results.ToArray(), Now));
            evidence["cargoAcceptanceHistory"] = JsonSerializer.SerializeToNode(history, Json); current.RouteEvidenceJson = evidence.ToJsonString(Json);
            current.StatusCode = assigned == cargoIds.Length && assigned > 0 ? "Accepted" : "AcceptancePending"; current.Revision++;
            return (id, current.Revision, current.StatusCode, (string?)null, 0);
        }, ct);
    }
    private static DistributedAcceptanceAttempt? LatestAcceptance(분산배송WaveRecord wave)
        => (JsonNode.Parse(wave.RouteEvidenceJson) as JsonObject)?["cargoAcceptanceHistory"]?.Deserialize<List<DistributedAcceptanceAttempt>>(Json)
            ?.LastOrDefault(x => x.DriverId == wave.DriverId);
    private static 분산경로적재량 Scale(분산경로적재량 load, decimal ratio) => new(load.WeightKg * ratio, load.VolumeCbm * ratio, load.Pallets * ratio);
    internal static string CargoDigest(살뜰.도메인.화주.화주운송의뢰 s)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Serialize(new {
            s.의뢰Id,s.화주Id,s.주문자UserId,s.화물수량,s.화물중량Kg,s.화물부피Cbm,s.화물팔레트개수,s.차량종류,
            s.픽업_도로명주소,s.픽업_위도,s.픽업_경도,s.픽업_시간창_시작일시,s.픽업_시간창_종료일시,
            s.하차_도로명주소,s.하차_위도,s.하차_경도,s.하차_시간창_시작일시,s.하차_시간창_종료일시 }))));
}
internal sealed record DistributedAcceptanceAttempt(Guid RequestId, string DriverId, IReadOnlyList<string> AcknowledgedWarningCodes,
    IReadOnlyList<DistributedCargoAcceptanceResult> Results, DateTime RecordedAtUtc);
