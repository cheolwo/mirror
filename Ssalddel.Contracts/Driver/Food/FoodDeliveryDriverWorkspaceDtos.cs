using Ssalddel.Contracts.Common.Transport;

namespace Ssalddel.Contracts.Driver.Food;

public sealed class FoodDeliveryDriverWorkspaceDto
{
    public string DriverId { get; set; } = string.Empty;
    public IReadOnlyList<FoodDeliveryDriverOfferDto> Recommendations { get; set; } = [];
    public IReadOnlyList<FoodDeliveryDriverActiveDeliveryDto> ActiveDeliveries { get; set; } = [];
    public IReadOnlyList<FoodDeliveryBundleCandidateDto> BundleCandidates { get; set; } = [];
    public int MaxActiveDeliveries { get; set; }
    public 배달기사월정산응답 Settlement { get; set; } = new();
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
}

public sealed class FoodDeliveryDriverActiveDeliveryDto
{
    public long TransportId { get; set; }
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
    public DateTime? PreparationDelayEligibleAtUtc { get; set; }
    public bool IsPreparationDelayRedispatch { get; set; }
    public 운송실행프로필Dto ExecutionProfile { get; set; } = new();
    public FoodDeliveryDriverRecipientDto Recipient { get; set; } = new();
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
