using System;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Presentation
{
    /// <summary>
    /// 서버 또는 Simulation 상태 사본을 한 배치 객체의 표시 단계로 바꿉니다.
    /// 객체 생성·경로 탐색·서버 상태 변경은 호출자의 책임입니다.
    /// </summary>
    public sealed class 음식배달수명주기표현
    {
        public string OrderStableId { get; }
        public string RestaurantStableId { get; }
        public string SourceCode { get; }
        public string SourceRevision { get; }
        public long Revision { get; }
        public string StateCode { get; }
        public string Label { get; }
        public int StepIndex { get; }
        public bool IsTerminal { get; }

        public 음식배달수명주기표현(음식배달수명주기Snapshot source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(source.OrderStableId) || source.OrderRevision < 0)
                throw new ArgumentException("FoodDeliveryLifecycleIdentityInvalid", nameof(source));
            OrderStableId = source.OrderStableId;
            RestaurantStableId = source.RestaurantStableId ?? string.Empty;
            SourceCode = source.SourceCode ?? string.Empty;
            SourceRevision = source.SourceRevision ?? string.Empty;
            Revision = source.OrderRevision;
            StateCode = source.OrderStateCode;
            (StepIndex, Label, IsTerminal) = source.OrderStateCode switch
            {
                "주문대기" => (0, "주문 접수 대기", false),
                "조리중" => (1, "음식점 조리 중", false),
                "픽업대기" => (2, "픽업 준비 완료", false),
                "기사배정" => (3, "기사가 음식점으로 이동", false),
                "픽업완료" => (4, "기사가 전달지로 이동", false),
                "전달완료" => (5, "전달 완료 · 수령 확인 대기", false),
                "수령확인" => (6, "수령 확인 완료", true),
                "거절" => (6, "음식점 거절", true),
                "취소" => (6, "주문 취소", true),
                _ => throw new ArgumentException("FoodDeliveryLifecycleStateUnsupported", nameof(source))
            };
        }
    }
}
