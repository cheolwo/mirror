using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WorkflowRules
{
    public static class 업무흐름규칙Catalog
    {
        private const string 기존RuleRevision = "workflow-rules.v1";
        private const string 음식배달RuleRevision = "food-delivery.v5";
        private const string 창고입고RuleRevision = "warehouse-inbound.v1";

        private static readonly IReadOnlyDictionary<string, 업무흐름규칙Snapshot> 규칙목록 =
            new Dictionary<string, 업무흐름규칙Snapshot>(StringComparer.Ordinal)
            {
                [업무흐름코드.개별주문] = Create(
                    업무흐름코드.개별주문,
                    "community.individual-order-ledger",
                    "community-ledger.v1",
                    new[]
                    {
                        개별주문상태코드.초안,
                        개별주문상태코드.진행중,
                        개별주문상태코드.완료,
                    },
                    new[]
                    {
                        Transition(개별주문상태코드.초안, 개별주문상태코드.진행중),
                        Transition(개별주문상태코드.진행중, 개별주문상태코드.완료),
                    },
                    "OperationalPayment", "OperationalInventoryReceipt", "PersonalNotification"),
                [업무흐름코드.같이주문] = Create(
                    업무흐름코드.같이주문,
                    "orderer.group-purchase-aggregation",
                    "group-purchase-aggregation.v1",
                    new[]
                    {
                        같이주문상태코드.수요수집중,
                        같이주문상태코드.확정대기,
                        같이주문상태코드.확정,
                        같이주문상태코드.모집종료목표미달,
                    },
                    new[]
                    {
                        Transition(같이주문상태코드.수요수집중, 같이주문상태코드.확정대기),
                        Transition(같이주문상태코드.확정대기, 같이주문상태코드.확정),
                        Transition(같이주문상태코드.수요수집중, 같이주문상태코드.모집종료목표미달),
                        Transition(같이주문상태코드.확정대기, 같이주문상태코드.모집종료목표미달),
                    },
                    "AutomaticParticipantConsent", "OperationalOrderCreation", "OperationalPayment"),
                [업무흐름코드.음식배달] = CreateWithRevision(
                    업무흐름코드.음식배달,
                    "food.orders-and-deliveries",
                    "food-order.v1",
                    new[]
                    {
                        음식배달상태코드.주문대기,
                        음식배달상태코드.주문확인,
                        음식배달상태코드.조리중,
                        음식배달상태코드.픽업대기,
                        음식배달상태코드.기사배정,
                        음식배달상태코드.픽업완료,
                        음식배달상태코드.전달완료,
                        음식배달상태코드.수령확인,
                        음식배달상태코드.거절,
                        음식배달상태코드.취소,
                    },
                    new[]
                    {
                        Transition(음식배달상태코드.주문대기, 음식배달상태코드.주문확인),
                        Transition(음식배달상태코드.주문확인, 음식배달상태코드.기사배정),
                        Transition(음식배달상태코드.기사배정, 음식배달상태코드.주문확인),
                        // 과거 Simulation/저장 계약의 읽기·재생 호환입니다.
                        // 운영 주문 저장소는 주문확인과 유효한 기사 배차 검증을 먼저 수행합니다.
                        Transition(음식배달상태코드.주문대기, 음식배달상태코드.조리중),
                        Transition(음식배달상태코드.주문대기, 음식배달상태코드.픽업대기),
                        Transition(음식배달상태코드.주문대기, 음식배달상태코드.거절),
                        Transition(음식배달상태코드.주문대기, 음식배달상태코드.취소),
                        Transition(음식배달상태코드.조리중, 음식배달상태코드.픽업대기),
                        Transition(음식배달상태코드.조리중, 음식배달상태코드.기사배정),
                        Transition(음식배달상태코드.픽업대기, 음식배달상태코드.기사배정),
                        Transition(음식배달상태코드.픽업대기, 음식배달상태코드.픽업완료),
                        Transition(음식배달상태코드.기사배정, 음식배달상태코드.픽업완료),
                        Transition(음식배달상태코드.기사배정, 음식배달상태코드.조리중),
                        Transition(음식배달상태코드.기사배정, 음식배달상태코드.픽업대기),
                        Transition(음식배달상태코드.픽업완료, 음식배달상태코드.전달완료),
                        Transition(음식배달상태코드.픽업완료, 음식배달상태코드.조리중),
                        Transition(음식배달상태코드.전달완료, 음식배달상태코드.수령확인),
                    },
                    음식배달RuleRevision,
                    "OperationalOrderWrite", "RealDriverDispatch", "PersonalAddress", "RealTimeNotification"),
                [업무흐름코드.화물운송] = Create(
                    업무흐름코드.화물운송,
                    "driver.freight-transport",
                    "driver-transport.v1",
                    new[]
                    {
                        화물운송상태코드.배차대기,
                        화물운송상태코드.배차대기확정,
                        화물운송상태코드.매칭중,
                        화물운송상태코드.배차확정,
                        화물운송상태코드.이동중,
                        화물운송상태코드.운송중,
                        화물운송상태코드.상차지도착,
                        화물운송상태코드.상차완료,
                        화물운송상태코드.하차지도착,
                        화물운송상태코드.인수완료,
                    },
                    new[]
                    {
                        Transition(화물운송상태코드.배차대기, 화물운송상태코드.상차지도착),
                        Transition(화물운송상태코드.배차대기확정, 화물운송상태코드.상차지도착),
                        Transition(화물운송상태코드.매칭중, 화물운송상태코드.상차지도착),
                        Transition(화물운송상태코드.배차확정, 화물운송상태코드.상차지도착),
                        Transition(화물운송상태코드.이동중, 화물운송상태코드.상차지도착),
                        Transition(화물운송상태코드.상차지도착, 화물운송상태코드.상차완료),
                        Transition(화물운송상태코드.상차완료, 화물운송상태코드.하차지도착),
                        Transition(화물운송상태코드.운송중, 화물운송상태코드.하차지도착),
                        Transition(화물운송상태코드.하차지도착, 화물운송상태코드.인수완료),
                    },
                    "RealDriverAssignment", "GpsLocationWrite", "OperationalFreightSettlement", "CarrierNotification"),
                [업무흐름코드.창고입고] = CreateWithRevision(
                    업무흐름코드.창고입고,
                    "warehouse.inbound-receive-inspect-put-away",
                    "warehouse-inbound.v1",
                    new[]
                    {
                        창고입고상태코드.입고예정,
                        창고입고상태코드.검수대기,
                        창고입고상태코드.적재대기,
                        창고입고상태코드.적재완료,
                    },
                    new[]
                    {
                        Transition(창고입고상태코드.입고예정, 창고입고상태코드.검수대기),
                        Transition(창고입고상태코드.검수대기, 창고입고상태코드.적재대기),
                        Transition(창고입고상태코드.적재대기, 창고입고상태코드.적재완료),
                    },
                    창고입고RuleRevision,
                    "OperationalInventoryWrite", "OperationalAuditLog", "OperationalWarehouseEvent", "OperationalEmployeeAuthorization"),
            };

        public static 업무흐름규칙Snapshot[] 전체조회()
        {
            return 규칙목록.Values
                .OrderBy(x => x.업무흐름코드, StringComparer.Ordinal)
                .Select(Clone)
                .ToArray();
        }

        public static 업무흐름규칙Snapshot 조회(string 업무코드)
        {
            if (string.IsNullOrWhiteSpace(업무코드)
                || !규칙목록.TryGetValue(업무코드.Trim(), out var rule))
            {
                throw new ArgumentException("지원하지 않는 업무 흐름입니다.", nameof(업무코드));
            }

            return Clone(rule);
        }

        private static 업무흐름규칙Snapshot Create(
            string workflowCode,
            string sourceCapabilityKey,
            string sourceContractRevision,
            string[] states,
            업무상태전이[] transitions,
            params string[] excludedOperationalEffects)
            => CreateWithRevision(
                workflowCode,
                sourceCapabilityKey,
                sourceContractRevision,
                states,
                transitions,
                기존RuleRevision,
                excludedOperationalEffects);

        private static 업무흐름규칙Snapshot CreateWithRevision(
            string workflowCode,
            string sourceCapabilityKey,
            string sourceContractRevision,
            string[] states,
            업무상태전이[] transitions,
            string ruleRevision,
            params string[] excludedOperationalEffects)
        {
            return new 업무흐름규칙Snapshot
            {
                업무흐름코드 = workflowCode,
                SourceCapabilityKey = sourceCapabilityKey,
                SourceContractRevision = sourceContractRevision,
                RuleRevision = ruleRevision,
                상태코드목록 = states,
                허용전이목록 = transitions,
                Simulation제외운영효과코드목록 = excludedOperationalEffects,
                SourceStableIds = new[]
                {
                    "capability:" + sourceCapabilityKey,
                    "contract-revision:" + sourceContractRevision,
                    "rule-revision:" + ruleRevision,
                },
            };
        }

        private static 업무상태전이 Transition(string from, string to)
        {
            return new 업무상태전이
            {
                현재상태코드 = from,
                목표상태코드 = to,
            };
        }

        private static 업무흐름규칙Snapshot Clone(업무흐름규칙Snapshot source)
        {
            return new 업무흐름규칙Snapshot
            {
                업무흐름코드 = source.업무흐름코드,
                SourceCapabilityKey = source.SourceCapabilityKey,
                SourceContractRevision = source.SourceContractRevision,
                RuleRevision = source.RuleRevision,
                상태코드목록 = source.상태코드목록.ToArray(),
                허용전이목록 = source.허용전이목록
                    .Select(x => Transition(x.현재상태코드, x.목표상태코드))
                    .ToArray(),
                Simulation제외운영효과코드목록 = source.Simulation제외운영효과코드목록.ToArray(),
                SourceStableIds = source.SourceStableIds.ToArray(),
            };
        }
    }
}
