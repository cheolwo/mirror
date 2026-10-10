using Ssalddel.Contracts.Common.Metadata;

using System;
using Ssalddel.Contracts.Common.Transport;

namespace 살뜰.도메인.기사
{
    [SsalddelCodeMetadata(
        SsalddelCodeFeatureKeys.FoodDriverWork,
        SsalddelCodeLayer.Domain,
        "운행 시작 요청의 멱등성과 현재 근무 슬롯을 담는 SQL 전문 상태를 정의한다.",
        Effects = SsalddelCodeEffect.None,
        FlowOrder = 30,
        StepKey = "domain.driver-shift",
        DependsOnStepKeys = ["contract.work-start-request", "contract.work-status"],
        ExecutionStage = SsalddelCodeExecutionStage.Definition,
        ReadsFrom = SsalddelCodeDataScope.None,
        WritesTo = SsalddelCodeDataScope.None,
        Boundary = "음식·화물 공용 근무 모델이며 ActiveDriverId·종료 시각·요청 지문은 서버가 검증한다. 타입 정의 자체가 DB를 저장하지 않는다.")]
    public class 기사근무
    {
        public long Id { get; set; }

        public Guid? WorkStartClientRequestId { get; set; }
        public string? WorkStartRequestKey { get; set; }
        public string? WorkStartInputFingerprint { get; set; }
        // 현재 근무 하나만 채워지는 nullable unique slot. 과거 근무는 보존합니다.
        public string? ActiveDriverId { get; set; }
        public DateTime? StoppedAtUtc { get; set; }
        public string? StartReturnPreference { get; set; }

        // Notion/article ID와 맞추기 위해 기사 식별자는 문자열로 둔다.
        public string 기사Id { get; set; } = string.Empty;

        public string 시작모드 { get; set; } = string.Empty; // immediate|reserved

        public DateTime? 시작시각 { get; set; }

        public string 시작위치 { get; set; } = string.Empty;

        public string 운송실행유형 { get; set; } = 운송실행유형코드.화물운송;

        public string? 복귀지 { get; set; }

        public string? 오늘의복귀지주소 { get; set; }

        public decimal? 오늘의복귀지위도 { get; set; }

        public decimal? 오늘의복귀지경도 { get; set; }

        public string 복귀지출처 { get; set; } = string.Empty;

        public DateTime? 복귀지입력일시 { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
