using System.Text.Json.Serialization;
using Ssalddel.Contracts.Common.Transport;

namespace Ssalddel.Contracts.Common.Drivers;

public static class DriverWorkOfferStatus
{
    public const string Recommended = "Recommended";
    public const string Accepted = "Accepted";
    public const string MovingToPickup = "MovingToPickup";
    public const string PickupConfirmed = "PickupConfirmed";
    public const string MovingToDropoff = "MovingToDropoff";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
}

public sealed record DriverWorkProfile(
    string AppKey,
    string DriverDomain,
    string WorkType,
    string DisplayName,
    string Description,
    string Focus);

public sealed record DriverWorkStopDto(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("targetTime")] DateTimeOffset? TargetTime = null);

public sealed record DriverWorkRecipientDto(
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("contactPhone")] string ContactPhone,
    [property: JsonPropertyName("deliveryInstructions")] string DeliveryInstructions,
    [property: JsonPropertyName("ordererIsRecipient")] bool OrdererIsRecipient);

public sealed record DriverWorkOfferDto(
    [property: JsonPropertyName("offerId")] string OfferId,
    [property: JsonPropertyName("appKey")] string AppKey,
    [property: JsonPropertyName("driverDomain")] string DriverDomain,
    [property: JsonPropertyName("workType")] string WorkType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("pickup")] DriverWorkStopDto Pickup,
    [property: JsonPropertyName("dropoff")] DriverWorkStopDto Dropoff,
    [property: JsonPropertyName("driverPayout")] decimal DriverPayout,
    [property: JsonPropertyName("distanceKm")] double? DistanceKm,
    [property: JsonPropertyName("recommendationReason")] string RecommendationReason,
    [property: JsonPropertyName("status")] string Status = DriverWorkOfferStatus.Recommended,
    [property: JsonPropertyName("expiresAtUtc")] DateTimeOffset? ExpiresAtUtc = null,
    [property: JsonPropertyName("orderIds")] IReadOnlyList<string>? OrderIds = null,
    [property: JsonPropertyName("executionProfile")] 운송실행프로필Dto? ExecutionProfile = null,
    [property: JsonPropertyName("recipient")] DriverWorkRecipientDto? Recipient = null,
    [property: JsonPropertyName("weatherSurcharge")] decimal WeatherSurcharge = 0m,
    [property: JsonPropertyName("weatherSurchargeApplied")] bool WeatherSurchargeApplied = false,
    [property: JsonPropertyName("pricingPolicyRevision")] string? PricingPolicyRevision = null,
    [property: JsonPropertyName("weatherObservedAtUtc")] DateTimeOffset? WeatherObservedAtUtc = null);

public sealed class FoodDeliveryDriverActionResponse
{
    [JsonPropertyName("offerId")]
    public string OfferId { get; set; } = string.Empty;

    [JsonPropertyName("orderIds")]
    public IReadOnlyList<string> OrderIds { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("locationAuditCode")]
    public string LocationAuditCode { get; set; } = string.Empty;

    [JsonPropertyName("locationDistanceKm")]
    public decimal? LocationDistanceKm { get; set; }

    [JsonPropertyName("deliveryAttemptId")]
    public string DeliveryAttemptId { get; set; } = string.Empty;

    [JsonPropertyName("attemptRevision")]
    public long? AttemptRevision { get; set; }

    [JsonPropertyName("occurredAtUtc")]
    public DateTime? OccurredAtUtc { get; set; }
}
