using System.Text.Json.Serialization;

namespace Ssalddel.Contracts.Common.Drivers;

/// <summary>이 제안에서 기사에게 실제로 배정한 작업입니다. 선행 작업 필요 여부나 개인 희망과 다릅니다.</summary>
public static class DriverOfferActivityCodes
{
    public const string Pickup = "Pickup";
    public const string Pick = "Pick";
    public const string Pack = "Pack";
    public const string OutboundHandover = "OutboundHandover";
    public const string Deliver = "Deliver";
}

/// <summary>기사 제안의 지급 근거입니다. 고객 청구 견적이나 주행 경로 거리와 구분합니다.</summary>
public sealed record DriverOfferPricingDto
{
    [JsonRequired, JsonPropertyName("distanceBasisCode")] public string DistanceBasisCode { get; init; } = "Unknown";
    [JsonRequired, JsonPropertyName("rawDistanceMeters")] public decimal RawDistanceMeters { get; init; }
    [JsonRequired, JsonPropertyName("ratePerKmKrw")] public decimal RatePerKmKrw { get; init; }
    [JsonRequired, JsonPropertyName("includedDistanceMeters")] public decimal IncludedDistanceMeters { get; init; }
    [JsonRequired, JsonPropertyName("roundingCode")] public string RoundingCode { get; init; } = "ProportionalAwayFromZero";
    [JsonRequired, JsonPropertyName("distanceFeeKrw")] public decimal DistanceFeeKrw { get; init; }
    [JsonPropertyName("baseFeeKrw")] public decimal? BaseFeeKrw { get; init; }
    [JsonPropertyName("minimumPayoutKrw")] public decimal? MinimumPayoutKrw { get; init; }
    [JsonPropertyName("workFeeKrw")] public decimal? WorkFeeKrw { get; init; }
    [JsonPropertyName("surchargeKrw")] public decimal? SurchargeKrw { get; init; }
    [JsonRequired, JsonPropertyName("totalPayoutKrw")] public decimal TotalPayoutKrw { get; init; }
    [JsonRequired, JsonPropertyName("policyRevision")] public string PolicyRevision { get; init; } = string.Empty;
}
