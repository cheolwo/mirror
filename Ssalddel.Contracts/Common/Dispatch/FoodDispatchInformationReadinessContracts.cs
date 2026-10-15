using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Common.Dispatch;

public static class FoodDispatchInformationStateCodes
{
    public const string Current = "Current";
    public const string Missing = "Missing";
    public const string Stale = "Stale";
    public const string NotApplicable = "NotApplicable";
}

public static class FoodDispatchInformationKeys
{
    public const string Order = "Order";
    public const string Preparation = "Preparation";
    public const string Locations = "Locations";
    public const string OrderPool = "OrderPool";
    public const string Demand = "Demand";
    public const string DriverCapacity = "DriverCapacity";
    public const string CustomerPromise = "CustomerPromise";
    public const string CostConsent = "CostConsent";
    public const string DriverScheduleAndLoad = "DriverScheduleAndLoad";
    public const string DriverQuote = "DriverQuote";
    public const string PlatformFunds = "PlatformFunds";
}

/// <summary>주소·이름·원문 대신 고정 항목과 판본·관측시각만 표현하는 정보 근거입니다.</summary>
public sealed record FoodDispatchInformationEvidence(
    string Key,
    string StateCode,
    string ReasonCode,
    DateTimeOffset? ObservedAtUtc = null,
    long? SourceRevision = null);

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Contract,
    "짧은 수집 판단의 정보 준비와 전체 배차 조율에 필요한 정보 준비를 구분한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 37,
    StepKey = "contract.food-dispatch-information",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "관측의 충분성을 표현할 뿐 배차·적재·약속 준수·요금·재원이나 정산을 확정하지 않는다. 개인정보와 원시 오류 문구를 포함하지 않는다.")]
public sealed record FoodDispatchInformationReadiness(
    string SchedulingStateCode,
    string CoordinationStateCode,
    IReadOnlyList<FoodDispatchInformationEvidence> Evidence)
{
    public string PolicyVersion { get; init; } = "food-dispatch-information/v1";
    /// <summary>판정 이후 원장 재조회나 전환 직전에도 같은 유효시간으로 다시 확인합니다.</summary>
    public int MaxAgeSeconds { get; init; } = 15;
}
