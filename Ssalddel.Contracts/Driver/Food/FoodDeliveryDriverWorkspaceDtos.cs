using Ssalddel.Contracts.Common.Metadata;

using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Contracts.Driver.Food;

/// <summary>지도와 현재 수행 업무용 조회입니다. 정산과 완료 내역은 포함하지 않습니다.</summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "지도와 현재 수행 업무의 정산 없는 조회 응답을 정의한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 15,
    StepKey = "contract.current-work",
    DependsOnStepKeys = ["contract.offer", "contract.active-delivery"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "월 정산·주문별 정산·완료 상세를 포함하지 않는 현재 업무 계약이다.")]
public sealed class FoodDeliveryDriverCurrentWorkDto
{
    public string DriverId { get; set; } = string.Empty;
    public IReadOnlyList<FoodDeliveryDriverOfferDto> Recommendations { get; set; } = [];
    public IReadOnlyList<FoodDeliveryDriverActiveDeliveryDto> ActiveDeliveries { get; set; } = [];
    public IReadOnlyList<FoodDeliveryBundleCandidateDto> BundleCandidates { get; set; } = [];
    public int MaxActiveDeliveries { get; set; }
    public bool DispatchAutomationEnabled { get; set; }
    public string DispatchAutomationNotice { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "기존 정산 포함 음식 기사 workspace 응답을 보존한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 16,
    StepKey = "contract.legacy-workspace",
    DependsOnStepKeys = ["contract.current-work"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "현재 업무 계약과 호환 정산 조회를 구별한다. 정산 기록이나 모의 지급 성공을 실제 이체로 승격하지 않는다.")]
public sealed class FoodDeliveryDriverWorkspaceDto
{
    public string DriverId { get; set; } = string.Empty;
    public IReadOnlyList<FoodDeliveryDriverOfferDto> Recommendations { get; set; } = [];
    public IReadOnlyList<FoodDeliveryDriverActiveDeliveryDto> ActiveDeliveries { get; set; } = [];
    public IReadOnlyList<FoodDeliveryBundleCandidateDto> BundleCandidates { get; set; } = [];
    public int MaxActiveDeliveries { get; set; }
    public 배달기사월정산응답 Settlement { get; set; } = new();
    public IReadOnlyList<Ssalddel.Contracts.Food.FoodDeliveryOrderSettlementDto> OrderSettlements { get; set; } = [];
    public bool DispatchAutomationEnabled { get; set; }
    public string DispatchAutomationNotice { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class FoodDeliveryDriverStopDto
{
    public string Label { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? TargetAtUtc { get; set; }
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "수락 전 음식 배달 제안의 조건과 가능한 행동을 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 13,
    StepKey = "contract.offer",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "거리 null과 확인된 0을 구별한다. 제안 금액은 음식 주문 금액이나 기사 실지급 완료를 뜻하지 않는다.")]
public sealed class FoodDeliveryDriverOfferDto
{
    public string OfferId { get; set; } = string.Empty;
    public string OrderSummary { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public FoodDeliveryDriverStopDto Pickup { get; set; } = new();
    public FoodDeliveryDriverStopDto Dropoff { get; set; } = new();
    public decimal DriverPayout { get; set; }
    public decimal WeatherSurcharge { get; set; }
    public bool WeatherSurchargeApplied { get; set; }
    public string PricingPolicyRevision { get; set; } = string.Empty;
    public DateTime? WeatherObservedAtUtc { get; set; }
    public decimal? DistanceKm { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public 운송실행프로필Dto ExecutionProfile { get; set; } = new();
    public IReadOnlyList<업무가능행동Dto> AvailableActions { get; set; } = [];
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "현재 기사에게 연결된 배달 시도와 Revision 및 허용 행동을 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 14,
    StepKey = "contract.active-delivery",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "주문·제안·시도 식별자는 서로 대체하지 않는다. 고객 상세는 권한 있는 현재 업무에 한정하며 전달 완료와 수령 확인·실지급은 별개다.")]
public sealed class FoodDeliveryDriverActiveDeliveryDto
{
    public IReadOnlyList<Ssalddel.Contracts.Food.DeliveryExperienceSummaryDto> ExperienceSummaries { get; set; } = [];
    public long TransportId { get; set; }
    /// <summary>현재 수행 원장의 주문번호입니다. 표시 제목이나 배차 제안 ID로 추론하지 않습니다.</summary>
    public string OrderNo { get; set; } = string.Empty;
    public string OfferId { get; set; } = string.Empty;
    public string OrderSummary { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public FoodDeliveryDriverStopDto Pickup { get; set; } = new();
    public FoodDeliveryDriverStopDto Dropoff { get; set; } = new();
    public decimal DriverPayout { get; set; }
    public decimal WeatherSurcharge { get; set; }
    public bool WeatherSurchargeApplied { get; set; }
    public string PricingPolicyRevision { get; set; } = string.Empty;
    public DateTime? WeatherObservedAtUtc { get; set; }
    public string TransportStatus { get; set; } = string.Empty;
    public string WorkStatus { get; set; } = string.Empty;
    public string DeliveryAttemptId { get; set; } = string.Empty;
    public long AttemptRevision { get; set; }
    public DateTime? RestaurantArrivedAtUtc { get; set; }
    public DateTime? DisplayedPreparationReadyAtUtc { get; set; }
    public int CurrentPreparationRound { get; set; } = 1;
    public DateTime? CurrentPickupReadyAtUtc { get; set; }
    public DateTime? RecookingRequestedAtUtc { get; set; }
    public DateTime? PreparationDelayEligibleAtUtc { get; set; }
    public bool IsPreparationDelayRedispatch { get; set; }
    public 운송실행프로필Dto ExecutionProfile { get; set; } = new();
    public FoodDeliveryDriverRecipientDto Recipient { get; set; } = new();
    public IReadOnlyList<업무가능행동Dto> AvailableActions { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class FoodDeliveryDriverRecipientDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string DeliveryInstructions { get; set; } = string.Empty;
    public bool OrdererIsRecipient { get; set; }
}

public sealed class FoodDeliveryBundleCandidateDto
{
    public string BundleId { get; set; } = string.Empty;
    public IReadOnlyList<string> OfferIds { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal TotalPayout { get; set; }
    public decimal EstimatedRouteKm { get; set; }
}

public sealed class FoodDeliveryBundleAcceptRequest
{
    public IReadOnlyList<string> OfferIds { get; set; } = [];
}

public sealed class FoodDeliveryOfferRejectRequest
{
    public string? ReasonCode { get; set; }
}

public sealed class FoodDeliveryDriverActionResultDto
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> CompletedOfferIds { get; set; } = [];
}

public sealed class FoodDeliveryDriverRouteRequestDto
{
    public decimal StartLatitude { get; set; }
    public decimal StartLongitude { get; set; }
    public IReadOnlyList<FoodDeliveryDriverRouteStopDto> Stops { get; set; } = [];
}

public sealed class FoodDeliveryDriverRouteStopDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public sealed class FoodDeliveryDriverRouteResponseDto
{
    public string Source { get; set; } = string.Empty;
    public bool IsEstimated { get; set; }
    public decimal DistanceKm { get; set; }
    public int DurationMinutes { get; set; }
    public IReadOnlyList<FoodDeliveryDriverRoutePointDto> Points { get; set; } = [];
}

public sealed class FoodDeliveryDriverRoutePointDto
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}
