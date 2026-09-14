using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private bool 기존마트주문흐름사용 => NeighborhoodLifeEnabled
            || (ScenarioStableId == "scenario:synthetic-delivery.r4"
                && ScenarioDataRevision == "synthetic-delivery.r4");
        private bool 주문흐름사용 => 기존마트주문흐름사용
            || 사가정가상음식점Policy.Matches(ScenarioStableId, ScenarioDataRevision);

        [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
            "주문자별 미완료 제한과 결정적 주문 생성", Boundary = "기존 주문 명령을 로컬 Tick에서 재사용한다")]
        private void 주문주기진행()
        {
            if(NeighborhoodLifeEnabled) { GenerateLifeOrders(); return; }
            var flow = waitingFleet!.OrderFlow!;
            if (MartEnabled && waitingFleet.Batch == 0) CreateMartOrder(++waitingFleet.Batch);
            foreach (var person in flow.Orderers)
            {
                if (CurrentTick < person.NextTick) continue;
                do { person.NextTick += 60; } while (person.NextTick <= CurrentTick);
                if (DurationTicks - CurrentTick < 180) continue;
                var actorId = "participant:synthetic:" + person.Residence;
                if (foodDeliveries.Values.Any(x => x.OrdererStableId == actorId && !x.ReceivedTick.HasValue
                    && x.StateCode != 음식배달상태코드.거절)) continue;
                CreateWaitingOrders(++person.Sequence, new[] { person.Residence });
            }
        }

        private void 음식점응답연결(string orderId, bool accepted)
        {
            if (!주문흐름사용) return;
            var entry = waitingFleet!.OrderFlow!.Entries.SingleOrDefault(x => x.OrderId == orderId);
            if (entry == null) return;
            entry.ResponseTick = CurrentTick;
            entry.ResponseCode = accepted ? "Accepted" : "Rejected";
            entry.State = accepted ? "Accepted" : "Rejected";
            entry.WaitReason = accepted ? "배차 대기 등록 예정" : "음식점 거절";
        }

        private void 주문접수연결(Simulation음식배달Snapshot order)
        {
            if (!주문흐름사용) return;
            var flow = waitingFleet!.OrderFlow!;
            if (!flow.Entries.Any(x => x.OrderId == order.FoodOrderStableId))
                flow.Entries = flow.Entries.Concat(new[] { new 가상주문연결Snapshot {
                    OrderId = order.FoodOrderStableId, SubmittedTick = CurrentTick } }).ToArray();
        }

        [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
            "수락 원장과 마트 준비 결과의 멱등 배차 인계", Boundary = "엔진은 후보만 반환하고 Session이 배정을 확정한다")]
        private void 배차대기연결진행()
        {
            var flow = waitingFleet!.OrderFlow!;
            foreach (var order in DeliveryTargets())
            {
                var entry = flow.Entries.SingleOrDefault(x => x.OrderId == order.Id);
                if (entry == null && order.Mart != null)
                {
                    entry = new 가상주문연결Snapshot { OrderId = order.Id, SourceKind = "Mart",
                        SubmittedTick = order.Accepted, State = "Preparation", WaitReason = "피킹·포장 대기" };
                    flow.Entries = flow.Entries.Concat(new[] { entry }).ToArray();
                }
                if (entry == null) continue;
                if (order.Mart != null && order.Ready)
                { entry.ResponseCode = "Prepared"; entry.ResponseTick = order.Mart.ReadyTick; }
                var ready = order.Mart != null ? order.Ready
                    : entry.ResponseCode == "Accepted" && entry.ResponseTick < CurrentTick
                        && !string.IsNullOrEmpty(order.Food!.RestaurantResponseDecisionStableId);
                if (ready && !entry.QueuedTick.HasValue)
                { entry.QueuedTick = CurrentTick; entry.State = "WaitingDispatch"; entry.WaitReason = "기사 후보 확인 대기"; }
                if (order.Received) { entry.State = "Completed"; entry.WaitReason = ""; }
                else if (order.Mart != null && !order.Ready && order.Mart.WaitReason.Length > 0)
                    entry.WaitReason = order.Mart.WaitReason;
            }
        }

        private bool 배차등록됨(string orderId) => !주문흐름사용
            || waitingFleet!.OrderFlow!.Entries.Any(x => x.OrderId == orderId && x.QueuedTick.HasValue
                && x.State != "Rejected" && x.State != "Completed" && x.DriverId.Length == 0);

        private void 배차결과연결(string orderId, string? driverId)
        {
            if (!주문흐름사용) return;
            var entry = waitingFleet!.OrderFlow!.Entries.Single(x => x.OrderId == orderId);
            entry.DriverId = driverId ?? "";
            entry.State = driverId == null ? "WaitingDispatch" : "Assigned";
            entry.WaitReason = driverId == null ? "가용 기사 없음 · 재탐색 대기" : "";
        }
    }
}
