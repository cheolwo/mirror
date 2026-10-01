using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class 업무흐름코드
    {
        public const string 개별주문 = "IndividualOrder";
        public const string 같이주문 = "GroupOrder";
        public const string 음식배달 = "FoodDelivery";
        public const string 화물운송 = "FreightTransport";
        public const string 창고입고 = "WarehouseInbound";
    }

    public static class 개별주문상태코드
    {
        public const string 초안 = "초안";
        public const string 진행중 = "진행중";
        public const string 완료 = "완료";
    }

    public static class 같이주문상태코드
    {
        public const string 수요수집중 = "CollectingDemand";
        public const string 확정대기 = "ReadyToConfirm";
        public const string 확정 = "Confirmed";
        public const string 모집종료목표미달 = "RecruitmentClosedTargetNotReached";
    }

    public static class 음식배달상태코드
    {
        public const string 주문대기 = "주문대기";
        public const string 주문확인 = "주문확인";
        public const string 조리중 = "조리중";
        public const string 픽업대기 = "픽업대기";
        public const string 기사배정 = "기사배정";
        public const string 픽업완료 = "픽업완료";
        public const string 전달완료 = "전달완료";
        public const string 수령확인 = "수령확인";
        public const string 거절 = "거절";
        public const string 취소 = "취소";
    }

    public static class 화물운송상태코드
    {
        public const string 배차대기 = "배차대기";
        public const string 배차대기확정 = "확정";
        public const string 매칭중 = "매칭중";
        public const string 배차확정 = "배차확정";
        public const string 이동중 = "이동중";
        public const string 운송중 = "운송중";
        public const string 상차지도착 = "상차지도착";
        public const string 상차완료 = "상차완료";
        public const string 하차지도착 = "하차지도착";
        public const string 인수완료 = "인수완료";
    }

    public static class 창고입고상태코드
    {
        public const string 입고예정 = "Expected";
        public const string 검수대기 = "PendingInspection";
        public const string 적재대기 = "PutAwayPending";
        public const string 적재완료 = "PutAwayCompleted";
    }

    public static class 창고입고행동코드
    {
        public const string 수령기록 = "RecordReceipt";
        public const string 검수완료 = "CompleteInspection";
        public const string 적재완료 = "CompletePutAway";
    }

    public static class 업무규칙차단사유코드
    {
        public const string 지원하지않는업무 = "UnsupportedWorkflow";
        public const string 알수없는현재상태 = "UnknownCurrentState";
        public const string 허용되지않은상태전이 = "TransitionNotAllowed";
        public const string 음수수량 = "NegativeQuantity";
        public const string 수량불일치 = "QuantityImbalance";
    }

    public sealed class 업무상태전이
    {
        public string 현재상태코드 { get; set; } = string.Empty;
        public string 목표상태코드 { get; set; } = string.Empty;
    }

    public sealed class 업무흐름규칙Snapshot
    {
        public string 업무흐름코드 { get; set; } = string.Empty;
        public string SourceCapabilityKey { get; set; } = string.Empty;
        public string SourceContractRevision { get; set; } = string.Empty;
        public string RuleRevision { get; set; } = string.Empty;
        public string[] 상태코드목록 { get; set; } = Array.Empty<string>();
        public 업무상태전이[] 허용전이목록 { get; set; } = Array.Empty<업무상태전이>();
        public string[] Simulation제외운영효과코드목록 { get; set; } = Array.Empty<string>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 업무상태전이판정
    {
        public bool 허용여부 { get; set; }
        public bool 멱등재시도여부 { get; set; }
        public string[] 차단사유코드목록 { get; set; } = Array.Empty<string>();
        public string RuleRevision { get; set; } = string.Empty;
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 업무수량보존요청
    {
        public decimal 입력수량 { get; set; }
        public decimal 결과수량 { get; set; }
        public decimal 손실수량 { get; set; }
        public decimal 허용오차 { get; set; }
        public string 단위코드 { get; set; } = string.Empty;
    }

    public sealed class 업무수량보존판정
    {
        public bool 보존여부 { get; set; }
        public decimal 차이수량 { get; set; }
        public string 단위코드 { get; set; } = string.Empty;
        public string[] 차단사유코드목록 { get; set; } = Array.Empty<string>();
    }

    public sealed class 같이주문상태판정요청
    {
        public int 참여자수 { get; set; }
        public decimal 총희망수량 { get; set; }
        public int? 목표참여자수 { get; set; }
        public decimal? 목표수량 { get; set; }
        public int 최소참여자수 { get; set; } = 2;
        public int 기본확정대기참여자수 { get; set; } = 5;
        public decimal 기본확정대기수량 { get; set; } = 30m;
    }

    public sealed class 같이주문상태판정
    {
        public string 제안상태코드 { get; set; } = 같이주문상태코드.수요수집중;
        public string 모집종료결과상태코드 { get; set; } = 같이주문상태코드.모집종료목표미달;
        public bool 최소참여자충족여부 { get; set; }
        public bool 목표참여자충족여부 { get; set; }
        public bool 목표수량충족여부 { get; set; }
        public bool 명시목표충족여부 { get; set; }
        public string RuleRevision { get; set; } = string.Empty;
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }
}
