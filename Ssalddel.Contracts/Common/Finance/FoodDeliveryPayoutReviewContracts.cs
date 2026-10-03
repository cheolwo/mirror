using System.Text.Json.Serialization;

namespace Ssalddel.Contracts.Common.Finance;

// 검토 입력에는 고객 주소, 기사 신원, 계좌와 촬영 자료를 포함하지 않는다.
public sealed record FoodDeliveryPayoutPricingPolicy(
    [property: JsonRequired] string Revision,
    [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly EffectiveThrough,
    [property: JsonRequired] decimal PickupFeeKrw,
    [property: JsonRequired] decimal DropoffFeeKrw,
    [property: JsonRequired] int DistanceUnitMeters,
    [property: JsonRequired] decimal DistanceUnitFeeKrw,
    [property: JsonRequired] string DistanceRoundingCode,
    int IncludedDistanceMeters = 0,
    decimal MinimumPayoutKrw = 0m);

public sealed record FoodDeliveryPayoutReviewDelivery(
    [property: JsonRequired] string DeliveryKey,
    [property: JsonRequired] DateOnly ServiceDate,
    [property: JsonRequired] decimal? DistanceKm,
    [property: JsonRequired] string DistanceBasisCode,
    [property: JsonRequired] string TimeBandCode,
    [property: JsonRequired] decimal TimeSurchargeKrw,
    [property: JsonRequired] decimal StorePromotionKrw,
    [property: JsonRequired] decimal RegionSurchargeKrw,
    string? SimultaneousPickupKey = null);

public sealed record FoodDeliveryPayoutPricingReviewRequest(
    [property: JsonRequired] FoodDeliveryPayoutPricingPolicy Policy,
    [property: JsonRequired] IReadOnlyList<FoodDeliveryPayoutReviewDelivery> Deliveries);

public sealed record FoodDeliveryPayoutReviewLine(
    string DeliveryKey,
    DateOnly ServiceDate,
    decimal DistanceKm,
    string DistanceBasisCode,
    string TimeBandCode,
    string? SimultaneousPickupKey,
    decimal PickupFeeKrw,
    decimal DropoffFeeKrw,
    decimal DistanceFeeKrw,
    decimal TimeSurchargeKrw,
    decimal StorePromotionKrw,
    decimal RegionSurchargeKrw,
    decimal MinimumAdjustmentKrw,
    decimal GrossPayoutKrw);

public sealed record FoodDeliveryPayoutPricingReviewResponse(
    string PolicyRevision,
    IReadOnlyList<FoodDeliveryPayoutReviewLine> Lines,
    decimal TotalGrossPayoutKrw,
    FoodDeliveryPayoutPricingPolicy PolicySnapshot,
    string StatusCode = "ReviewOnly");

// Unknown / Applicable / NotApplicable. 미확인 적용 판정을 '공제 없음'으로 바꾸지 않는다.
public sealed record FoodDeliveryDeductionReviewRule
{
    public string ApplicabilityCode { get; init; } = "Unknown";
    public decimal? Rate { get; init; }
    public decimal ExpenseRatio { get; init; }
    public decimal MinimumKrw { get; init; }
    public int TruncationUnitKrw { get; init; } = 10;
    public string EvidenceRef { get; init; } = string.Empty;
}

public sealed record FoodDeliverySettlementReviewPolicy
{
    public string Revision { get; init; } = string.Empty;
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly EffectiveThrough { get; init; }
    public bool? IncludePeriodRewardsInInsurance { get; init; }
    public string RewardInsuranceTreatmentEvidenceRef { get; init; } = string.Empty;
    public int RewardTaxTruncationUnitKrw { get; init; } = 1;
    public FoodDeliveryDeductionReviewRule IncomeTax { get; init; } = new();
    public FoodDeliveryDeductionReviewRule LocalIncomeTax { get; init; } = new();
    public FoodDeliveryDeductionReviewRule EmploymentInsurance { get; init; } = new();
    public FoodDeliveryDeductionReviewRule AccidentInsurance { get; init; } = new();
}

public sealed record FoodDeliverySettlementReviewRequest(
    [property: JsonRequired] DateOnly PeriodStart,
    [property: JsonRequired] DateOnly PeriodEnd,
    [property: JsonRequired] decimal DeliveryGrossKrw,
    [property: JsonRequired] decimal MissionGrossKrw,
    [property: JsonRequired] decimal PeriodPromotionGrossKrw,
    [property: JsonRequired] decimal MilestoneBonusGrossKrw,
    [property: JsonRequired] FoodDeliverySettlementReviewPolicy Policy);

public sealed record FoodDeliveryDeductionReviewLine(
    string Code,
    string ApplicabilityCode,
    decimal? BaseKrw,
    decimal ExpenseRatio,
    decimal? Rate,
    int TruncationUnitKrw,
    decimal? AmountKrw,
    string EvidenceRef);

public sealed record FoodDeliverySettlementReviewResponse(
    string PolicyRevision,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal BaseGrossKrw,
    decimal PeriodPromotionGrossKrw,
    decimal MilestoneBonusGrossKrw,
    decimal TotalGrossKrw,
    IReadOnlyList<FoodDeliveryDeductionReviewLine> Deductions,
    decimal? RewardTaxKrw,
    decimal? TotalDeductionsKrw,
    decimal? NetPayoutKrw,
    string StatusCode,
    IReadOnlyList<string> PendingCodes,
    FoodDeliverySettlementReviewPolicy PolicySnapshot);
