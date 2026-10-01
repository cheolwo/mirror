using System;

namespace 홍달.도메인.운송
{
    public static class 운송이벤트유형
    {
        public const string 배차엔진판단감사 = "DispatchEngineDecisionAudit";
    }

    public class 운송이벤트
    {
        public long Id { get; set; }

        public string 의뢰Id { get; set; } = string.Empty;

        public string 이벤트타입 { get; set; } = string.Empty;

        public DateTime 이벤트시각 { get; set; } = DateTime.UtcNow;

        public string 메타데이터 { get; set; } = string.Empty;
    }
}
