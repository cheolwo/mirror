using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Admin.Operations;

public static class 플랫폼운영경제성본인대리인가정Codes
{
    public const string 미정 = "Undetermined";
    public const string 본인후보 = "PrincipalCandidate";
    public const string 대리인후보 = "AgentCandidate";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        미정,
        본인후보,
        대리인후보
    };
}

public static class 플랫폼운영경제성금액분류Codes
{
    public const string 총거래액 = "GrossTransactionAmount";
    public const string 주문수익후보 = "OrderRevenueCandidate";
    public const string 반복수익후보 = "RecurringRevenueCandidate";
    public const string 통과자금 = "PassThroughFunds";
    public const string 변동비용 = "VariableCost";
    public const string 고정비용 = "FixedCost";
    public const string 현금유입 = "CashReceipt";
    public const string 현금유출 = "CashPayment";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        총거래액,
        주문수익후보,
        반복수익후보,
        통과자금,
        변동비용,
        고정비용,
        현금유입,
        현금유출
    };
}

public static class 플랫폼운영경제성평가상태Codes
{
    public const string 관리Simulation전용 = "ManagementSimulationOnly";
}

public static class 플랫폼운영경제성주의Codes
{
    public const string 본인대리인미정 = "PrincipalAgentUndetermined";
    public const string 주문당지표계산불가 = "PerOrderMetricsUnavailable";
    public const string 기여금비양수 = "NonPositiveContribution";
    public const string 실제회계매핑승인필요 = "ActualAccountingMappingApprovalRequired";
}

public sealed class 플랫폼운영경제성금액항목Dto
{
    public string StableId { get; set; } = string.Empty;
    public string 분류Code { get; set; } = string.Empty;
    public decimal 금액 { get; set; }
    public string 근거Revision { get; set; } = string.Empty;
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformOperatingEconomics,
    SsalddelCodeLayer.Contract,
    "한 국가·관할·통화의 관리 Simulation 시나리오와 금액 분류 입력을 정의합니다.",
    StepKey = "contract.platform-operating-economics",
    FlowOrder = 10,
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Effects = SsalddelCodeEffect.None,
    Boundary = "통화를 자동 환산하지 않고, 고객 총결제액과 플랫폼 수익 후보를 서로 다른 항목으로 받습니다.")]
public sealed class 플랫폼운영경제성평가요청Dto
{
    public string 시나리오StableId { get; set; } = string.Empty;
    public string 국가Code { get; set; } = string.Empty;
    public string 관할Code { get; set; } = string.Empty;
    public string 통화Code { get; set; } = string.Empty;
    public DateOnly 기간시작일 { get; set; }
    public DateOnly 기간종료일 { get; set; }
    public long 완료주문수 { get; set; }
    public decimal 기초가용현금 { get; set; }
    public string 본인대리인가정Code { get; set; } = 플랫폼운영경제성본인대리인가정Codes.미정;
    public string 입력Revision { get; set; } = string.Empty;
    public IReadOnlyList<플랫폼운영경제성금액항목Dto> 항목 { get; set; } = [];
}

public sealed class 플랫폼운영경제성평가응답Dto
{
    public string 시나리오StableId { get; set; } = string.Empty;
    public string 국가Code { get; set; } = string.Empty;
    public string 관할Code { get; set; } = string.Empty;
    public string 통화Code { get; set; } = string.Empty;
    public DateOnly 기간시작일 { get; set; }
    public DateOnly 기간종료일 { get; set; }
    public long 완료주문수 { get; set; }
    public string 본인대리인가정Code { get; set; } = string.Empty;
    public decimal 총거래액 { get; set; }
    public decimal 주문수익후보 { get; set; }
    public decimal 반복수익후보 { get; set; }
    public decimal 플랫폼보유수익후보 { get; set; }
    public decimal 통과자금 { get; set; }
    public decimal 변동비용 { get; set; }
    public decimal 고정비용 { get; set; }
    public decimal 총비용 { get; set; }
    public decimal 기여금 { get; set; }
    public decimal? 주문당기여금 { get; set; }
    public decimal? 기여이익률 { get; set; }
    public decimal 영업이익후보 { get; set; }
    public long? 손익분기완료주문수 { get; set; }
    public long? 손익분기추가필요주문수 { get; set; }
    public bool 손익분기도달 { get; set; }
    public decimal 현금유입 { get; set; }
    public decimal 현금유출 { get; set; }
    public decimal 기말가용현금 { get; set; }
    public string 결과Hash { get; set; } = string.Empty;
    public string 상태Code { get; set; } = 플랫폼운영경제성평가상태Codes.관리Simulation전용;
    public bool 회계매핑승인필요 { get; set; } = true;
    public bool 운영전표쓰기허용 { get; set; }
    public IReadOnlyList<string> 주의사항Codes { get; set; } = [];
}
