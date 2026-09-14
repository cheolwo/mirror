using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Domain
{
    [SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodWorkflowLineage, SsalddelCodeLayer.Domain,
        "가상 기사 후보 선정에서 운영과 공유하는 픽업 평가 규칙 사용",
        StepKey = "domain.food-dispatch-shared-rule", FlowOrder = 20,
        ExecutionStage = SsalddelCodeExecutionStage.Tick,
        ReadsFrom = SsalddelCodeDataScope.SimulationState, WritesTo = SsalddelCodeDataScope.SimulationState,
        Effects = SsalddelCodeEffect.StateMutation,
        SourceCodeRefs = new[] { "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs" },
        ReuseKind = "SharedRuleCall",
        SharedRuleRefs = new[] { "Ssalddel.WorkflowRules/UnityPackage/Runtime/음식배달픽업평가Policy.cs" },
        Adaptation = "음식배달후보선정Policy를 통해 공유 픽업 평가·정렬을 호출한다. 운영은 경로서비스/UTC/기사Store, 가상은 표본 경로/세션 시간/기사 사본을 사용한다. 후보 정책 전체나 배차 확정은 동일 코드가 아니다.",
        Boundary = "평가 결과를 세션에서 재검사·배정; 운영 배차·알림·위치 조회 없음")]
    public sealed partial class 경영SimulationSessionAggregate
    {
        private 가상배달대기Snapshot? waitingFleet;
        private bool WaitingFleetEnabled => 주문흐름사용 || MartEnabled || (ScenarioStableId == "scenario:synthetic-delivery.r2"
            && ScenarioDataRevision == "synthetic-delivery.r2");
        private static readonly (double x, double z)[] WaitingSlots = 가상동네배치기준.대기점();

        private 가상배달대기Snapshot CreateWaitingFleet()
            => new 가상배달대기Snapshot {
                Mart = MartEnabled ? new 가상마트Snapshot() : null,
                OrderFlow = 주문흐름사용 ? new 가상주문흐름Snapshot() : null,
                NeighborhoodLife = NeighborhoodLifeEnabled ? CreateNeighborhoodLife() : null,
                Drivers = CourierIds.Select((id,i) => new 가상배달대기기사Snapshot {
                    Slot = i, Courier = new 가상배달기사Snapshot { ActorStableId = id,
                        X = WaitingSlots[i].x, Z = WaitingSlots[i].z, VehicleX = WaitingSlots[i].x, VehicleZ = WaitingSlots[i].z }
                }).ToArray() };
        private void AdvanceWaitingFleet()
        {
            var fleet = waitingFleet ??= CreateWaitingFleet();
            if (NeighborhoodLifeEnabled) AdvanceNeighborhoodDepot();
            if (주문흐름사용) 주문주기진행();
            else if (fleet.Drivers.All(x => x.Courier.Stage == "Idle" && x.PendingOrderId.Length == 0)
                && DeliveryTargets().All(x => x.Received)
                && DurationTicks - CurrentTick >= (MartEnabled ? 240 : 120))
            {
                CreateWaitingOrders(++fleet.Batch);
                if (MartEnabled) CreateMartOrder(fleet.Batch);
            }
            if (MartEnabled) AdvanceMart();
            if (주문흐름사용) 배차대기연결진행();

            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(CurrentTick);
            foreach (var order in DeliveryTargets().Where(o => !o.Received && (주문흐름사용 || o.Accepted > 0)
                         && (o.Mart == null || o.Ready) && 배차등록됨(o.Id)).OrderBy(o => o.Accepted).ThenBy(o => o.Id, StringComparer.Ordinal))
            {
                if (fleet.Drivers.Any(x => x.Courier.OrderStableId == order.Id || x.PendingOrderId == order.Id)) continue;
                var candidates = fleet.Drivers.Where(x => (x.Courier.Stage == "Idle" || x.Courier.Stage == "Return") && x.PendingOrderId.Length == 0 && LifeCanAccept(x.Courier.ActorStableId))
                    .Select(x => {
                        var slot = x.Courier.Stage == "Return" ? x.ReturnSlot : x.Slot;
                        var distance = RouteLength(NeighborhoodLifeEnabled ? LifePickupRoute(slot,order.Mart!=null) : ToPickup(slot, order.Mart != null));
                        var remaining = x.Courier.Stage == "Return" ? Math.Max(0, RouteLength(ReturnRoute(x)) - x.Courier.Distance) : 0;
                        return new 음식배달기사후보(x.Courier.ActorStableId, slot < 3 ? 0 : 1,
                            (decimal)((distance + remaining) / 1000), (decimal)(((distance + remaining) / 5 + (order.Mart != null ? 8 : 4)) / 60),
                            (CurrentTick - x.AssignedTick) / 60m, 5);
                    }).ToArray();
                var evaluation = 음식배달후보선정Policy.판정(candidates, now,
                    now.AddSeconds(order.Ready ? 0 : order.Preparation), 5, 3, 0, 0);
                if (evaluation.선택기사Id == null) { fleet.Decision = "배차 대기 · 가용 기사 없음"; 배차결과연결(order.Id, null); continue; }
                var selected = fleet.Drivers.Single(x => x.Courier.ActorStableId == evaluation.선택기사Id);
                // 후보 계산과 확정 책임을 분리한다. 같은 Session 직렬 Tick 안에서도 확정 조건을 다시 확인한다.
                if (주문흐름사용 && (!배차등록됨(order.Id) || order.Received
                    || (selected.Courier.Stage != "Idle" && selected.Courier.Stage != "Return")
                    || selected.PendingOrderId.Length > 0)) continue;
                배차결과연결(order.Id, selected.Courier.ActorStableId);
                selected.AssignedTick = CurrentTick;
                fleet.Decision = evaluation.선택기사Id + " · " + string.Join(" / ", evaluation.평가목록.Select(x =>
                    x.후보.기사Id + ": " + (x.적격 ? x.평가!.설명 : x.제외사유)));
                syntheticCourier = selected.Courier;
                var previousOrder = syntheticCourier.OrderStableId;
                syntheticCourier.OrderStableId = order.Id;
                RecordSynthetic("ASSIGN", "Assigned");
                if (syntheticCourier.Stage == "Return") { selected.PendingOrderId = order.Id; syntheticCourier.OrderStableId = previousOrder; }
                else { syntheticCourier.Stage = "WaitingRoad"; selected.WaitReason = "도로 통행 대기"; }
            }

            if (!NeighborhoodLifeEnabled && fleet.TrafficOwner.Length == 0)
            {
                var requests = fleet.Drivers.Where(x => x.Courier.Stage == "WaitingRoad")
                    .Select(x => new 이동자원진입요청(x.Courier.ActorStableId, x.AssignedTick, "road:synthetic-network", "entrance:restaurant"));
                var occupancy = 이동자원점유Policy.판정(new Dictionary<string, string>(), requests);
                var admitted = occupancy.판정목록.FirstOrDefault(x => x.진입가능);
                if (admitted != null)
                {
                    fleet.TrafficOwner = admitted.기사Id;
                    var driver = fleet.Drivers.Single(x => x.Courier.ActorStableId == admitted.기사Id);
                    driver.Courier.Stage = "DriveRestaurant"; driver.Courier.Distance = 0; driver.WaitReason = "";
                }
            }
            foreach (var item in NeighborhoodLifeEnabled ? fleet.Drivers.OrderBy(x=>x.AssignedTick).ThenBy(x=>x.Courier.ActorStableId,StringComparer.Ordinal).ToArray() : fleet.Drivers)
            {
                var driver = item.Courier;
                if(NeighborhoodLifeEnabled && driver.Stage=="WaitingRoad") { driver.Stage="DriveRestaurant";driver.Distance=0; }
                if (driver.Stage == "Idle" || driver.Stage == "WaitingRoad") continue;
                if (!NeighborhoodLifeEnabled && fleet.TrafficOwner != driver.ActorStableId) throw new SimulationContractException("WaitingFleetTrafficOwnerMismatch");
                syntheticCourier = driver;
                var order = DeliveryTargets().Single(x => x.Id == driver.OrderStableId);
                var home = ResidenceX(order.Destination);
                var pickupX = order.Mart != null ? -10d : 10d;
                var pickupZ = order.Mart != null ? -8d : 4d;
                var parkingZ=NeighborhoodLifeEnabled?LifePickupZ(order.Mart!=null):0d;
                var homeParkingZ=NeighborhoodLifeEnabled?22.5d:20d;
                switch (driver.Stage)
                {
                    case "DriveRestaurant":
                        if(NeighborhoodLifeEnabled) item.WaitReason=MoveLifeVehicle(LifePickupRoute(item.Slot,order.Mart!=null),5,"WalkRestaurant")?"":"도로/정차점 대기";
                        else MoveSynthetic(ToPickup(item.Slot, order.Mart != null), 5, true, "WalkRestaurant"); break;
                    case "WalkRestaurant":
                        if(NeighborhoodLifeEnabled && !LifeReserve(driver.ActorStableId,"entrance:"+(order.Mart!=null?"mart":"restaurant"))) {item.WaitReason="출입구 대기";break;}
                        item.WaitReason="";MoveSynthetic(new[] { (pickupX,parkingZ), (pickupX,pickupZ) }, 1, false, "WaitFood"); break;
                    case "WaitFood":
                        item.WaitReason = order.Ready ? "" : "조리 완료 대기";
                        if (order.Ready) {
                            TransitionDelivery(order, 음식배달상태코드.기사배정, driver.ActorStableId);
                            SetSyntheticStage("Pickup"); }
                        break;
                    case "Pickup":
                        TransitionDelivery(order, 음식배달상태코드.픽업완료, driver.ActorStableId);
                        driver.Carrying = true; RecordSynthetic("PICKUP", "PickedUp"); SetSyntheticStage("WalkRestaurantOut"); break;
                    case "WalkRestaurantOut": MoveSynthetic(new[] { (pickupX,pickupZ), (pickupX,parkingZ) }, 1, false, "DriveHome");
                        if(NeighborhoodLifeEnabled && driver.Stage=="DriveHome") LifeRelease(driver.ActorStableId,"entrance:");break;
                    case "DriveHome":
                        var homeRoute=new[] { (pickupX,0d), (20d,0d), (20d,20d), (home,20d) };
                        if(NeighborhoodLifeEnabled) item.WaitReason=MoveLifeVehicle(LifeHomeRoute(pickupX,home,order.Mart!=null),5,"WalkHome")?"":"도로/정차점 대기";
                        else MoveSynthetic(homeRoute,5,true,"WalkHome");break;
                    case "WalkHome":
                        if(NeighborhoodLifeEnabled && !LifeReserve(driver.ActorStableId,"entrance:"+order.Destination)) {item.WaitReason="출입구 대기";break;}
                        item.WaitReason="";MoveSynthetic(new[] { (home,homeParkingZ), (home,24d) }, 1, false, "Deliver"); break;
                    case "Deliver":
                        TransitionDelivery(order, 음식배달상태코드.전달완료, driver.ActorStableId);
                        driver.Carrying = false; RecordSynthetic("DELIVER", "Delivered"); SetSyntheticStage("Receive"); break;
                    case "Receive":
                        if(!LifeRecipientReady(order.Destination)) {item.WaitReason="주문자 출입구 접근 대기";break;}
                        TransitionDelivery(order, 음식배달상태코드.수령확인, order.Food?.OrdererStableId ?? ResidentId(ResidenceKey(order.Destination)));
                        RecordSynthetic("RECEIVE", "Received"); SetSyntheticStage("WalkHomeOut"); break;
                    case "WalkHomeOut":
                        MoveSynthetic(new[] { (home,24d), (home,homeParkingZ) }, 1, false, "Return");
                        if (driver.Stage == "Return") {
                            if(NeighborhoodLifeEnabled) LifeRelease(driver.ActorStableId,"entrance:");
                            item.ReturnSlot = Enumerable.Range(0, WaitingSlots.Length)
                                .Where(i => !fleet.Drivers.Any(other => other != item && (other.Slot == i || other.ReturnSlot == i)))
                                .OrderBy(i => RouteLength(FromHome(home, i))).ThenBy(i => i).First(); }
                        break;
                    case "Return":
                        if(NeighborhoodLifeEnabled) item.WaitReason=MoveLifeVehicle(ReturnRoute(item),5,"Idle")?"":"복귀 통로 대기";
                        else MoveSynthetic(ReturnRoute(item), 5, true, "Idle");
                        if (driver.Stage == "Idle") {
                            item.Slot = item.ReturnSlot; item.ReturnSlot = -1; fleet.TrafficOwner = "";
                            if (item.PendingOrderId.Length > 0) { driver.OrderStableId = item.PendingOrderId; item.PendingOrderId = ""; driver.Stage = "WaitingRoad"; item.WaitReason = "도로 통행 대기"; }
                        }
                        break;
                    default: throw new SimulationContractException("WaitingFleetStageInvalid");
                }
            }
            syntheticCourier = null; // r1 단일 기사 사본을 r2의 권위로 노출하지 않는다.
            if (주문흐름사용) 배차대기연결진행();
        }

        private void CreateWaitingOrders(int batch, string[]? residences = null)
        {
            foreach (var residence in residences ?? new[] { "a", "b" })
            {
                var restaurant = 사가정가상음식점Policy.Matches(ScenarioStableId, ScenarioDataRevision)
                    ? 사가정가상음식점Catalog.주문순서(batch, residence)
                    : null;
                var orderKey = restaurant == null
                    ? batch + ":" + residence
                    : "sagajeong:r1:" + batch + ":" + residence;
                var request = new Simulation음식배달PreviewRequest {
                    FoodOrderStableId = "food-order:synthetic:" + orderKey,
                    MenuItemStableId = restaurant?.MenuItemStableId ?? "menu-item:sim.potato-stew-1",
                    RestaurantFacilityStableId = restaurant?.FacilityStableId ?? LocalFacilityId("facility:sim.restaurant-1"),
                    DestinationFacilityStableId = ResidenceId(residence), OrdererStableId = ResidentId(residence),
                    ActorStableId = ResidentId(residence), DeliveryScopeStableId = "delivery-scope:synthetic",
                    Quantity = 1, UnitCode = "serving", RestaurantPreparationOnly = true, AwaitRestaurantResponse = true,
                    NpcAutoAccept = true, PreparationDurationTicks = restaurant?.PreparationDurationTicks ?? 2, DeliveryDurationTicks = 2,
                    SourceStableIds = restaurant == null
                        ? new[] { "source:" + ScenarioDataRevision }
                        : new[] { "source:" + ScenarioDataRevision, restaurant.ProfileStableId, restaurant.DisplayRouteStableId } };
                ConfirmDecisionCore(new SimulationDecisionConfirmRequest { CommandId = "command:auto:" + request.FoodOrderStableId,
                    ExpectedRevision = Revision, Preview = CreateFoodDeliveryDecisionRequest(request, CreateFoodDeliveryPreview(request)) }, false, _ => { });
            }
        }
        private (double x, double z)[] ReturnRoute(가상배달대기기사Snapshot item)
        {
            var home=ResidenceX(DeliveryTargets().Single(x=>x.Id==item.Courier.OrderStableId).Destination);
            var route=FromHome(home,item.ReturnSlot);
            return NeighborhoodLifeEnabled?new[]{(home,22.5d)}.Concat(route).ToArray():route;
        }
        private static (double x, double z)[] ToRestaurant(int slot)
            => slot < 3 ? new[] { WaitingSlots[slot], (slot == 2 ? 10d : 0d,0d), (10d,0d) }
                : new[] { WaitingSlots[slot], (slot == 3 ? 10d : 30d,20d), (20d,20d), (20d,0d), (10d,0d) };
        private static (double x, double z)[] FromHome(double home, int slot)
            => slot >= 3 && home == WaitingSlots[slot].x ? new[] { (home,20d), WaitingSlots[slot] }
                : slot >= 3 ? new[] { (home,20d), (20d,20d), (slot == 3 ? 10d : 30d,20d), WaitingSlots[slot] }
                : new[] { (home,20d), (20d,20d), (20d,0d), (10d,0d), (slot == 2 ? 10d : 0d,0d), WaitingSlots[slot] };
        private static double RouteLength((double x, double z)[] points)
        { var length = 0d; for (int i=1; i<points.Length; i++) length += Segment(points[i-1], points[i]); return length; }
    }
}
