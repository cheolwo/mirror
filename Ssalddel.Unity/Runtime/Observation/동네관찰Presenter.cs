using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Observation
{
    public enum 동네휴대폰App { 홈, 운영지도, 동네, 주문, NPC, 창고, 정책 }

    /// <summary>실제 E5 AreaSet 관찰 준비 상태만 표시하며 Simulation 상태를 소유하지 않는다.</summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E3,
        "운영 지도 Area 상태를 Unity 관찰 화면용 읽기 모델로 표현한다.",
        Boundary = "표현용 모델이며 실제 위치·운영 상태·Scene 배치를 소유하지 않는다.")]
    public sealed class 운영지도AreaViewModel
    {
        public string StableId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool Selectable { get; set; }
        public bool Selected { get; set; }
        public string Reason { get; set; } = "";
    }

    public sealed class 동네관찰Row
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string ActorId { get; set; } = "";
        public string FacilityId { get; set; } = "";
        public string RelatedId { get; set; } = "";
    }

    /// <summary>한 음식 주문의 참여 주체와 현재 단계를 같은 Simulation revision에서 읽는 관찰 전용 투영이다.</summary>
    public sealed class 동네음식생활CycleModel
    {
        public string OrderId { get; set; } = "";
        public string OrdererActorId { get; set; } = "";
        public string RestaurantActorId { get; set; } = "";
        public string DriverActorId { get; set; } = "";
        public string StageCode { get; set; } = "";
        public string StageDisplayName { get; set; } = "";
        public string WaitReason { get; set; } = "";
        public string CurrentActorId { get; set; } = "";
        public string CurrentFacilityId { get; set; } = "";
        public bool Completed { get; set; }
        public bool Rejected { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
    }

    public sealed class 동네관찰ScreenModel
    {
        public long Revision { get; set; }
        public string Time { get; set; } = "";
        public string Summary { get; set; } = "";
        public string BoundaryNotice { get; set; } = "가상 NPC 생활 관찰 · 실제 앱 주문 및 실제 기사 배차 없음";
        public 동네음식생활CycleModel[] FoodLifeCycles { get; set; } = Array.Empty<동네음식생활CycleModel>();
        public 동네관찰Row[] FoodCycles { get; set; } = Array.Empty<동네관찰Row>();
        public 동네관찰Row[] Orders { get; set; } = Array.Empty<동네관찰Row>();
        public 동네관찰Row[] Actors { get; set; } = Array.Empty<동네관찰Row>();
        public 동네관찰Row[] Warehouses { get; set; } = Array.Empty<동네관찰Row>();
    }

    /// <summary>편집 초안은 원장과 분리하며 명시적 적용 성공 뒤에만 재동기화한다.</summary>
    public sealed class 동네정책Draft
    {
        public bool Dirty { get; private set; }
        public bool Automation { get; private set; }
        public int Priority { get; private set; }
        public int Cooking { get; private set; }
        public int Work { get; private set; }
        public int Rest { get; private set; }
        public int Threshold { get; private set; }
        public void 동기화(경영SimulationSessionSnapshot state, bool discard = false)
        {
            if (Dirty && !discard) return;
            var life = state.WaitingFleet?.NeighborhoodLife;
            if (life == null) return;
            Automation = life.AutomationEnabled; Priority = life.Priority;
            Work = life.WorkingTicks; Rest = life.RestTicks; Threshold = life.RestockThreshold;
            Cooking = state.NpcWorkPolicies.FirstOrDefault(x => x.PolicyStableId == "policy:restaurant:cooking")?.WorkDurationTicks ?? 2;
            Dirty = false;
        }
        public void 변경(bool? automation = null, int? priority = null, int? cooking = null,
            int? work = null, int? rest = null, int? threshold = null)
        {
            Automation = automation ?? Automation; Priority = priority ?? Priority; Cooking = cooking ?? Cooking;
            Work = work ?? Work; Rest = rest ?? Rest; Threshold = threshold ?? Threshold; Dirty = true;
        }
        public SimulationNpcPolicyChangeRequest 요청() => new SimulationNpcPolicyChangeRequest {
            PolicyStableId = "policy:restaurant:cooking", AutomationEnabled = Automation, Priority = Priority,
            CookingDurationTicks = Cooking, ObserverWorkingTicks = Work, ObserverRestTicks = Rest, ObserverRestockThreshold = Threshold };
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "동네 주문·NPC·재고의 관찰 조회 모델", Boundary = "사본을 읽으며 업무 상태를 변경하지 않는다")]
    public static class 동네관찰Presenter
    {
        public static string 이름(string id)
        {
            if (id == 가상동네생활기준.MartWorkerId) return "살뜰마트 작업자";
            if (id == 가상동네생활기준.DepotWorkerId) return "보충창고 작업자";
            if (id == 가상동네생활기준.TruckDriverId) return "화물 기사";
            if (id == 가상동네생활기준.RestaurantActorId) return "음식점 주인";
            if (id.StartsWith("actor:synthetic-courier:", StringComparison.Ordinal)) return "배달 기사 " + id.Split(':').Last();
            if (id == "participant:synthetic:a") return "주민 A";
            if (id == "participant:synthetic:b") return "주민 B";
            return "미배정";
        }
        public static string 시설이름(string id) => id switch {
            "mart" => "살뜰마트", "depot" => "보충창고", "restaurant" => "음식점",
            "home-a" => "주택 A", "home-b" => "주택 B", _ => "관련 시설" };
        public static string 단계(string code) => code switch {
            "Working" => "근무", "Finishing" => "업무 마무리", "ReturningToRest" => "휴식점 귀환", "Resting" => "휴식",
            "ReturningToWork" => "근무점 복귀", "AtHome" => "집에서 생활", "Receiving" => "물품 수령",
            "GoingHome" => "도보 귀가", "Eating" => "식사", "HomeRest" => "집에서 휴식", "Sleeping" => "수면",
            "Idle" => "업무 대기", "Pending" => "재고 확인 대기", "Inbox" => "음식점 확인 대기",
            "Accepted" => "수락 · 배차 등록 예정", "Preparation" => "상품 준비", "WaitingDispatch" => "배차 대기",
            "Rejected" => "거절", "Assigned" => "기사 배정", "Completed" => "수령 완료",
            "Picking" or "Pick" => "상품 피킹", "Packing" or "Pack" => "포장", "Packed" => "포장 완료",
            "Ready" => "기사 인계 대기", "PickedUp" => "배달 중", "Delivered" => "전달 완료", "Received" => "수령 완료",
            "Pickup" => "물품 픽업", "Deliver" => "주문자에게 전달", "Receive" => "수령 확인 대기",
            "WalkShelf" => "적재대로 이동", "WalkPack" => "포장대로 이동", "WalkHandoff" => "인계대로 이동",
            "Return" => "대기점 복귀", "WaitingRoad" => "통행 대기", "DriveRestaurant" => "픽업 장소로 운전",
            "WalkRestaurant" => "픽업점 접근", "WaitFood" => "조리 완료 대기", "WalkRestaurantOut" => "차량으로 복귀",
            "DriveHome" => "주문지로 운전", "WalkHome" => "주택 출입구 접근", "WalkHomeOut" => "차량으로 복귀",
            "UnloadInbound" => "입고 물품 하차", "InspectInbound" => "입고 검수", "PutAwayInbound" => "적재대 적치",
            "Reserved" => "출고 예약", "Picked" => "피킹 완료", "Loaded" => "운송 중", "Unloaded" => "입고 검수 대기",
            "Inspected" => "검수 완료", "WalkLoad" => "상차점 접근", "Loading" => "상차", "WalkLoadOut" => "차량 복귀",
            "DriveMart" => "마트 보충 운송", "WalkUnload" => "하차점 접근", "Unloading" => "하차", "WalkUnloadOut" => "차량 복귀",
            "LifeInboundWalk" => "입고점 접근", "LifeInboundInspect" => "보충 물품 검수", "LifeInboundStore" => "보충 물품 적치",
            "LifeInboundReturn" => "작업점 복귀", "" => "대기", _ => code.Any(c => c >= '가' && c <= '힣') ? code : "상태 확인 필요" };

        private static string 관찰시설(string stableId)
            => stableId.EndsWith(":a", StringComparison.Ordinal) ? "home-a"
                : stableId.EndsWith(":b", StringComparison.Ordinal) ? "home-b"
                : "restaurant";

        public static 동네관찰ScreenModel 생성(경영SimulationSessionSnapshot state)
        {
            var life = state.WaitingFleet?.NeighborhoodLife;
            var model = new 동네관찰ScreenModel { Revision = state.Revision,
                Time = $"{state.CurrentTick / 60:00}:{state.CurrentTick % 60:00} / {state.DurationTicks / 60:00}:{state.DurationTicks % 60:00}" };
            if (life == null) return model;
            model.Time = (life.DayEnabled ? (state.CurrentTick < 300 ? "아침" : state.CurrentTick < 1050 ? "낮" : state.CurrentTick < 1500 ? "저녁" : "밤")
                : "기존 근무 표본") + " · " + model.Time;
            var fleet = state.WaitingFleet!;
            var entries = fleet.OrderFlow?.Entries ?? Array.Empty<가상주문연결Snapshot>();
            model.FoodLifeCycles = state.FoodDeliveries.Select(food =>
            {
                var entry = entries.FirstOrDefault(x => x.OrderId == food.FoodOrderStableId);
                var driver = entry?.DriverId ?? "";
                var completed = food.ReceivedTick.HasValue || food.StateCode == "수령확인";
                var rejected = food.StateCode == "거절";
                var courierActive = driver.Length > 0 && (food.DispatchCandidateTick.HasValue || food.PickedUpTick.HasValue || food.DeliveredTick.HasValue);
                var currentActor = completed ? food.OrdererStableId
                    : courierActive ? driver
                    : 가상동네생활기준.RestaurantActorId;
                var currentFacility = food.PickedUpTick.HasValue || food.DeliveredTick.HasValue || completed
                    ? 관찰시설(food.DestinationFacilityStableId)
                    : "restaurant";
                var waitReason = rejected ? food.RejectionReasonCode
                    : !string.IsNullOrWhiteSpace(entry?.WaitReason) ? entry!.WaitReason
                    : driver.Length == 0 && (food.ReadyForPickupTick.HasValue || food.DispatchCandidateTick.HasValue) ? "담당 기사 대기"
                    : "";
                var participants = $"주문자 {이름(food.OrdererStableId)} · 음식점 {이름(가상동네생활기준.RestaurantActorId)}"
                    + (driver.Length == 0 ? " · 배달 기사 미배정" : " · " + 이름(driver));
                return new 동네음식생활CycleModel {
                    OrderId = food.FoodOrderStableId,
                    OrdererActorId = food.OrdererStableId,
                    RestaurantActorId = 가상동네생활기준.RestaurantActorId,
                    DriverActorId = driver,
                    StageCode = food.StateCode,
                    StageDisplayName = 단계(food.StateCode),
                    WaitReason = waitReason,
                    CurrentActorId = currentActor,
                    CurrentFacilityId = currentFacility,
                    Completed = completed,
                    Rejected = rejected,
                    Title = $"음식 생활 · {이름(food.OrdererStableId)} · {단계(food.StateCode)}",
                    Body = participants + (waitReason.Length == 0 ? "" : "\n" + waitReason)
                };
            }).Reverse().ToArray();
            model.FoodCycles = (fleet.OrderFlow?.Orderers ?? Array.Empty<가상주문자Snapshot>()).Select(person =>
            {
                var id = "participant:synthetic:" + person.Residence;
                var destination = "facility:synthetic:" + person.Residence;
                var food = state.FoodDeliveries.Where(x => x.OrdererStableId == id).ToArray();
                var active = food.LastOrDefault(x => !x.ReceivedTick.HasValue && x.StateCode != "거절");
                var martPending = fleet.Mart?.Orders.Any(x => x.DestinationId == destination && !x.ReceivedTick.HasValue) == true;
                var next = state.IsCompleted ? "표본 종료 · 자동 재시작 없음"
                    : !life.AutomationEnabled ? "신규 주문 중단 · 기존 업무는 마무리"
                    : life.DayEnabled && state.CurrentTick >= 1050 ? "오늘 신규 주문 마감 · 미완료 수령은 유지"
                    : state.DurationTicks - state.CurrentTick < 180 ? "마감 · 신규 주문 없음"
                    : active != null ? "현재 음식 수령 후 다음 주문 차례"
                    : martPending ? "마트 주문 수령 대기 · 음식/마트 교대"
                    : $"다음 주문 검토까지 {Math.Max(0, person.NextTick - state.CurrentTick)}초 · 음식/마트 교대";
                return new 동네관찰Row { Id = id, ActorId = id, RelatedId = active?.FoodOrderStableId ?? "",
                    Title = 이름(id) + " · 음식 수령 " + food.Count(x => x.ReceivedTick.HasValue) + "회",
                    Body = (active == null ? "진행 중 음식 주문 없음" : "현재: " + 단계(active.StateCode)) + "\n" + next };
            }).ToArray();
            var orders = new List<동네관찰Row>();
            int index = 0;
            foreach (var food in state.FoodDeliveries)
            {
                var entry = entries.FirstOrDefault(x => x.OrderId == food.FoodOrderStableId);
                var actor = entry?.DriverId ?? "";
                var wait = food.ReadyForPickupTick.HasValue && !food.PickedUpTick.HasValue
                    ? $"\n픽업 대기 {Math.Max(0, state.CurrentTick - food.ReadyForPickupTick.Value)}초" : "";
                orders.Add(new 동네관찰Row { Id = food.FoodOrderStableId, Title = $"음식 주문 {++index} · {이름(food.OrdererStableId)}",
                    Body = $"{단계(food.StateCode)} · {이름(actor)}\n{entry?.WaitReason ?? ""}{wait}", ActorId = actor, FacilityId = "restaurant" });
            }
            index = 0;
            foreach (var order in fleet.Mart?.Orders ?? Array.Empty<가상마트주문Snapshot>())
            {
                var actor = entries.FirstOrDefault(x => x.OrderId == order.OrderId)?.DriverId ?? "";
                var wait = order.ReadyTick.HasValue && !order.PickedUpTick.HasValue
                    ? $"\n픽업 대기 {Math.Max(0, state.CurrentTick - order.ReadyTick.Value)}초" : "";
                orders.Add(new 동네관찰Row { Id = order.OrderId, Title = $"마트 주문 {++index} · 주택 {(order.DestinationId.EndsWith(":a", StringComparison.Ordinal) ? "A" : "B")}",
                    Body = $"{단계(order.Stage)} · {이름(actor)}\n{order.WaitReason}{wait}", ActorId = actor, FacilityId = "mart" });
            }
            model.Orders = orders.AsEnumerable().Reverse().ToArray();
            model.Actors = life.Actors.Select(actor => {
                var driver = fleet.Drivers.FirstOrDefault(x => x.Courier.ActorStableId == actor.ActorId);
                var stage = driver != null ? 단계(driver.Courier.Stage) : actor.ActorId == 가상동네생활기준.TruckDriverId ? 단계(life.Depot.Truck.Stage)
                    : actor.ActorId == 가상동네생활기준.MartWorkerId ? 단계(fleet.Mart?.WorkerStage ?? "")
                    : actor.ActorId == 가상동네생활기준.DepotWorkerId ? 단계(life.Depot.WorkerStage) : actor.Reason;
                var wait = life.WaitingSince.TryGetValue(actor.ActorId, out var since) ? $"\n통행 대기 {Math.Max(0, state.CurrentTick - since)}초" : "";
                var orderId = driver?.Courier.OrderStableId ?? actor.TaskId;
                var related = orders.FirstOrDefault(x => x.Id == orderId);
                var away = life.DayEnabled && actor.Stage != "Working" && actor.Stage != "Finishing";
                if (away) { stage = actor.Reason; related = null; }
                var daily = life.DayEnabled && actor.HomeFacilityId.Length > 0
                    ? "\n주거: " + (actor.HomeFacilityId.EndsWith(":a",StringComparison.Ordinal) ? "주택 A" : "주택 B")
                        + " · 다음: " + actor.NextAction + "\n오늘 기록: " + string.Join(" → ",actor.DayHistory.Select(생활기록표시)) : "";
                return new 동네관찰Row { Id = actor.ActorId, Title = 이름(actor.ActorId), ActorId = actor.ActorId,
                    Body = $"{단계(actor.Stage)} · {stage}\n{(away ? actor.Reason : driver?.WaitReason ?? actor.Reason)}{wait}" + (related == null ? "" : "\n" + related.Title) + daily,
                    RelatedId = related?.Id ?? "", FacilityId = away && actor.HomeFacilityId.Length > 0 ? (actor.HomeFacilityId.EndsWith(":a",StringComparison.Ordinal) ? "home-a" : "home-b") : actor.ActorId == 가상동네생활기준.DepotWorkerId || actor.ActorId == 가상동네생활기준.TruckDriverId ? "depot"
                        : actor.ActorId == 가상동네생활기준.MartWorkerId ? "mart" : actor.ActorId == 가상동네생활기준.RestaurantActorId ? "restaurant"
                        : related?.FacilityId ?? "" };
            }).ToArray();
            var depot = life.Depot;
            var warehouses = new List<동네관찰Row> {
                new 동네관찰Row { Id = "depot", Title = "보충창고", ActorId = 가상동네생활기준.DepotWorkerId, FacilityId = "depot",
                    Body = $"{단계(depot.WorkerStage)}\n재고 {수량(depot.Stock)}\n예약 {수량(depot.Reserved)} · 입고 대기 {수량(depot.Incoming)}" },
                new 동네관찰Row { Id = "mart", Title = "살뜰마트", ActorId = 가상동네생활기준.MartWorkerId, FacilityId = "mart",
                    Body = $"{단계(fleet.Mart?.WorkerStage ?? "")}\n재고 {수량(fleet.Mart?.Stock)}\n예약 {수량(fleet.Mart?.Reserved)}" } };
            index = 0;
            foreach (var shipment in depot.Shipments)
                warehouses.Add(new 동네관찰Row { Id = shipment.Id, Title = $"보충 운송 {++index} · 보충창고 → 마트", ActorId = 가상동네생활기준.TruckDriverId,
                    FacilityId = "depot", Body = $"{단계(shipment.Stage)}\n요청 {수량(shipment.Quantity)} · 운송/입고 중 {수량(shipment.Cargo)}" });
            model.Warehouses = warehouses.ToArray();
            model.Summary = $"NPC {life.Actors.Length}명 · 휴식/수면 {life.Actors.Count(x => x.Stage == "Resting" || x.Stage == "HomeRest" || x.Stage == "Sleeping")}명\n"
                + $"음식 수령 {state.FoodDeliveries.Count(x => x.ReceivedTick.HasValue)}건 · 마트 수령 {fleet.Mart?.Orders.Count(x => x.ReceivedTick.HasValue) ?? 0}건\n"
                + $"보충 입고 {depot.Shipments.Count(x => x.ReceivedTick.HasValue)}건 · 점유 구간 {life.ResourceOwners.Count}곳";
            return model;
        }
        private static string 생활기록표시(string entry)
        {
            var split = entry.IndexOf(" · ", StringComparison.Ordinal);
            return split < 0 ? entry : entry.Substring(0,split + 3) + 단계(entry.Substring(split + 3));
        }
        private static string 수량(int[]? values) => values == null || values.Length < 2 ? "확인 필요" : $"상품 1: {values[0]} / 상품 2: {values[1]}";
    }
}
