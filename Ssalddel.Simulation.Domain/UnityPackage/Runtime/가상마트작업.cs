using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private bool MartEnabled => 기존마트주문흐름사용 || (ScenarioStableId == "scenario:synthetic-delivery.r3"
            && ScenarioDataRevision == "synthetic-delivery.r3");

        private void CreateMartOrder(int batch)
        {
            var mart = waitingFleet!.Mart!;
            mart.Orders = mart.Orders.Concat(Enumerable.Range(1, 3).Select(index => new 가상마트주문Snapshot {
                OrderId = "mart-order:synthetic:" + batch + ":" + index, AcceptedTick = CurrentTick })).ToArray();
        }

        [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
            "가상 마트 한 명 작업자의 예약·피킹·포장·인계 전이", Boundary = "합성 Tick 전용. 실제 운영 효과 없음")]
        private void AdvanceMart()
        {
            var mart = waitingFleet!.Mart!;
            if(NeighborhoodLifeEnabled && mart.WorkerStage=="LifeInboundReturn")
            {if(MoveMartWorker(mart,0,-14)){mart.WorkerStage="Idle";mart.WorkerOrderId="";}return;}
            if(NeighborhoodLifeEnabled && mart.WorkerStage.StartsWith("LifeInbound",StringComparison.Ordinal)) return;
            if (mart.WorkerStage == "Idle")
            {
                if(!LifeCanAccept(LocalActorId(가상동네생활기준.MartWorkerId))) return;
                var next = mart.Orders.FirstOrDefault(x => x.Stage == "Pending");
                if (next == null) return;
                if (Enumerable.Range(0, 2).Any(i => mart.Stock[i] - mart.Reserved[i] < 1))
                { next.WaitReason = NeighborhoodLifeEnabled ? "재고 부족 · 보충 입고 대기" : "재고 부족 · 새 표본에서 초기 재고 복구"; return; }
                for (int i = 0; i < 2; i++) mart.Reserved[i]++;
                next.WaitReason = ""; next.Stage = "Picking";
                mart.WorkerOrderId = next.OrderId; mart.Shelf = 0; mart.WorkerStage = "WalkShelf";
                RecordMart("RESERVE", "Reserved");
                return;
            }
            var order = mart.Orders.Single(x => x.OrderId == mart.WorkerOrderId);
            switch (mart.WorkerStage)
            {
                case "WalkShelf":
                    if (MoveMartWorker(mart, mart.Shelf == 0 ? -8 : -4, -14))
                    { mart.WorkerStage = "Pick"; mart.WorkTicks = 0; }
                    break;
                case "Pick":
                    if (++mart.WorkTicks < 2) break;
                    mart.Stock[mart.Shelf]--; mart.Reserved[mart.Shelf]--; order.PickedUnits++;
                    RecordMart("PICK", "Picked");
                    if (++mart.Shelf < 2) mart.WorkerStage = "WalkShelf";
                    else mart.WorkerStage = "WalkPack";
                    break;
                case "WalkPack":
                    if (MoveMartWorker(mart, 0, -14))
                    { mart.WorkerStage = "Pack"; order.Stage = "Packing"; mart.WorkTicks = 0; }
                    break;
                case "Pack":
                    if (++mart.WorkTicks < 2) break;
                    order.PackedTick = CurrentTick; order.Stage = "Packed";
                    mart.WorkerStage = "WalkHandoff"; RecordMart("PACK", "Packed");
                    break;
                case "WalkHandoff":
                    // 적재대 앞 통로를 먼저 따라간 뒤 인계대로 간다.
                    if (MoveMartWorker(mart, -10, mart.X > -10 ? -14 : -8)
                        && mart.Z == -8)
                    {
                        order.ReadyTick = CurrentTick; order.Stage = "Ready";
                        RecordMart("STAGE", "ReadyForCourier");
                        mart.WorkerStage = "Return";
                    }
                    break;
                case "Return":
                    if (MoveMartWorker(mart, mart.Z > -14 ? -10 : 0, -14) && mart.X == 0)
                    { mart.WorkerStage = "Idle"; mart.WorkerOrderId = ""; }
                    break;
                default: throw new SimulationContractException("SyntheticMartWorkerStageInvalid");
            }
        }

        private static bool MoveMartWorker(가상마트Snapshot mart, double x, double z)
        {
            var length = Segment((mart.X, mart.Z), (x, z));
            var ratio = length <= 1 ? 1 : 1 / length;
            mart.X += (x - mart.X) * ratio; mart.Z += (z - mart.Z) * ratio;
            return length <= 1;
        }

        private void RecordMart(string action, string result)
        {
            var previous = syntheticCourier;
            syntheticCourier = new 가상배달기사Snapshot {
                ActorStableId = LocalActorId(가상동네생활기준.MartWorkerId), OrderStableId = waitingFleet!.Mart!.WorkerOrderId };
            RecordSynthetic("MART-" + action, result);
            syntheticCourier = previous;
        }

        // 음식점과 마트의 원장을 섞지 않고 배차/이동에 필요한 값만 읽는다.
        private sealed class 가상운송대상
        {
            public Simulation음식배달Snapshot? Food;
            public 가상마트주문Snapshot? Mart;
            public string Id => Food?.FoodOrderStableId ?? Mart!.OrderId;
            public string Destination => Food?.DestinationFacilityStableId ?? Mart!.DestinationId;
            public int Accepted => Food?.AcceptedTick ?? Mart!.AcceptedTick;
            public int Preparation => Food?.PreparationDurationTicks ?? 0;
            public bool Ready => Food != null ? Food.ReadyForPickupTick.HasValue : Mart!.ReadyTick.HasValue;
            public bool Received => Food != null ? Food.ReceivedTick.HasValue : Mart!.ReceivedTick.HasValue;
        }
        private 가상운송대상[] DeliveryTargets() => foodDeliveries.Values
            .Where(x => x.StateCode != 음식배달상태코드.거절)
            .Select(x => new 가상운송대상 { Food = x })
            .Concat(waitingFleet?.Mart?.Orders.Select(x => new 가상운송대상 { Mart = x })
                ?? Enumerable.Empty<가상운송대상>()).ToArray();

        private void TransitionDelivery(가상운송대상 target, string stage, string actor)
        {
            if (target.Food != null)
            {
                var order = target.Food;
                TransitionFoodDelivery(order, stage, CurrentTick, actor);
                if (stage == 음식배달상태코드.기사배정) order.DispatchCandidateTick = CurrentTick;
                if (stage == 음식배달상태코드.픽업완료) order.PickedUpTick = CurrentTick;
                if (stage == 음식배달상태코드.전달완료) order.DeliveredTick = CurrentTick;
                if (stage == 음식배달상태코드.수령확인) order.ReceivedTick = CurrentTick;
                return;
            }
            var martOrder = target.Mart!;
            if (stage == 음식배달상태코드.기사배정) martOrder.Stage = "Assigned";
            if (stage == 음식배달상태코드.픽업완료)
            {
                if (!martOrder.ReadyTick.HasValue || martOrder.PickedUpTick.HasValue)
                    throw new SimulationContractException("SyntheticMartPickupInvalid");
                martOrder.PickedUpTick = CurrentTick; martOrder.Stage = "PickedUp";
            }
            if (stage == 음식배달상태코드.전달완료)
            { martOrder.DeliveredTick = CurrentTick; martOrder.Stage = "Delivered"; }
            if (stage == 음식배달상태코드.수령확인)
            { martOrder.ReceivedTick = CurrentTick; martOrder.Stage = "Received"; }
        }
        private static (double x, double z)[] ToPickup(int slot, bool mart)
            => !mart ? ToRestaurant(slot)
                : slot < 3 ? new[] { WaitingSlots[slot], (slot == 2 ? 10d : 0d, 0d), (-10d, 0d) }
                : new[] { WaitingSlots[slot], (WaitingSlots[slot].x, 20d), (20d, 20d), (20d, 0d), (-10d, 0d) };
    }
}
