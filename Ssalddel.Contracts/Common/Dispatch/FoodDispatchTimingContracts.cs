using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Common.Dispatch;

public static class FoodDispatchTimingActionCodes
{
    public const string Collect = "Collect";
    public const string DispatchNow = "DispatchNow";
    public const string ExistingFlow = "ExistingFlow";
}

/// <summary>음식 첫 배차 전 수집 판단의 시험 기본값입니다. 기능 활성화는 별도 서버 옵션이 소유합니다.</summary>
public sealed record FoodDispatchTimingSettings
{
    public int CollectionSeconds { get; init; } = 30;
    public int MaxWaitSeconds { get; init; } = 60;
    public int MinimumCompatibleOrders { get; init; } = 2;
    public int DeadlineBufferSeconds { get; init; } = 15;
    public int MaxSnapshotAgeSeconds { get; init; } = 15;
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Contract,
    "최초 음식 배차 시각과 확인된 수요·기사 여력을 짧은 주문 수집 판단에 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 36,
    StepKey = "contract.food-dispatch-timing",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "최초 큐 시각과 확인된 선택 마감을 구분한다. 조합 가능성은 적재·실도로·모든 약속 시간의 검증이나 배차 확정이 아니다.")]
public sealed record FoodDispatchTimingInput
{
    public DateTimeOffset NowUtc { get; init; }
    /// <summary>최초 큐 생성 시각입니다. 재평가·새 주문·UpdatedAt으로 바꾸지 않습니다.</summary>
    public DateTimeOffset QueuedAtUtc { get; init; }
    public DateTimeOffset? SnapshotAtUtc { get; init; }
    /// <summary>서버 조회가 결속한 정보 준비 상태입니다. 구형 순수 정책 입력의 null은 호환용이며 실제 수집 대기에는 허용하지 않습니다.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FoodDispatchInformationReadiness? InformationReadiness { get; init; }
    /// <summary>확인된 배차 마감이 있을 때만 제공합니다. 미확인은 null입니다.</summary>
    public DateTimeOffset? LatestDispatchAtUtc { get; init; }
    public bool Applicable { get; init; } = true;
    public bool HasActiveOffer { get; init; }
    public bool IsRetry { get; init; }
    public bool DemandKnown { get; init; }
    public bool DriverCapacityKnown { get; init; }
    public bool DelayRisk { get; init; }
    public bool PromisingPlanAvailable { get; init; }
    public int WaitingOrders { get; init; }
    public int CompatibleOrders { get; init; }
    public int AvailableDrivers { get; init; }
    /// <summary>관측에 근거한 추가 주문 예상입니다. 주문 도착이나 미래 물량의 보장이 아닙니다.</summary>
    public decimal? ExpectedNewOrders { get; init; }
}

/// <summary>판정만 반환합니다. DispatchNow도 기존 후보·요금·수락 관문을 통과하라는 의미입니다.</summary>
public sealed record FoodDispatchTimingDecision(
    string ActionCode,
    string ReasonCode,
    DateTimeOffset? RecheckAtUtc,
    DateTimeOffset HardDeadlineUtc,
    IReadOnlyList<string> AllowedActions)
{
    public string PolicyVersion { get; init; } = "food-dispatch-timing/v1";
}
