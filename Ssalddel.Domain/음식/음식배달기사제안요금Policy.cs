using Ssalddel.Contracts.Common.Finance;

namespace 살뜰.도메인.음식;

public sealed record 음식배달기사제안요금판정(
    decimal 기본거리지급액,
    decimal 기상할증액,
    decimal 한시수요할증액,
    decimal 기사지급예정액,
    bool 기상할증적용여부,
    bool 한시수요할증적용여부)
{
    public decimal 시간대할증액 { get; init; }
    public FoodDeliveryPayoutPricingPolicy? 기본계산정책 { get; init; }
    public FoodDeliveryPayoutReviewLine? 기본요금구성 { get; init; }
    public string 기본지급구분Code { get; init; } = "LegacyUnsplit";
}

public static class 음식배달기사제안요금Policy
{
    public const string 기본판본 = "food-driver-offer-pricing.r3";

    public static 음식배달기사제안요금판정 판정(
        음식운영정책 policy,
        decimal? 픽업지에서전달지까지거리Km,
        bool 유효한강수근거있음,
        bool 강수중,
        DateTimeOffset 판정시각Utc,
        bool 한시수요할증허용,
        string deliveryKey = "offer",
        decimal 시간대할증액 = 0m)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (시간대할증액 < 0m) throw new ArgumentException("FoodPricingTimeSurchargeInvalid", nameof(시간대할증액));

        if (policy.기사픽업지급액 is < 0m || policy.기사픽업지급액 > policy.기사기본지급액)
            throw new ArgumentException("픽업 지급액은 총 기본 지급액 안에서 배분해야 합니다.", nameof(policy));
        var date = DateOnly.FromDateTime(판정시각Utc.UtcDateTime);
        var calculationPolicy = new FoodDeliveryPayoutPricingPolicy(
            기본판본, date, date, policy.기사픽업지급액 ?? 0m,
            policy.기사기본지급액 - (policy.기사픽업지급액 ?? 0m), policy.거리단위Meters,
            policy.기사거리단위지급액, "Ceiling", policy.포함거리Meters, policy.기사최소지급액);
        // 배분 미정일 때 DropoffFeeKrw 칸은 기존 기본액 전체를 운반한다. 실제 전달비로 해석하지 않는다.
        var components = 음식배달지급검토Policy.구성계산(calculationPolicy,
            new FoodDeliveryPayoutReviewDelivery(deliveryKey, date, 픽업지에서전달지까지거리Km,
                "RouteEstimate", "ExistingOperatingPolicy", 0m, 0m, 0m), calculationPolicy.PickupFeeKrw);
        var basePayout = components.GrossPayoutKrw;
        var weatherApplied = policy.기사기상할증활성화여부
                             && 유효한강수근거있음
                             && 강수중;
        var weatherSurcharge = weatherApplied
            ? Math.Max(0m, policy.기사기상할증액)
            : 0m;
        var decisionTime = 판정시각Utc.UtcDateTime;
        var demandApplied = 한시수요할증허용
                            && policy.기사한시수요할증액 > 0m
                            && policy.기사한시수요할증시작일시Utc.HasValue
                            && policy.기사한시수요할증종료일시Utc.HasValue
                            && decisionTime >= DateTime.SpecifyKind(
                                policy.기사한시수요할증시작일시Utc.Value,
                                DateTimeKind.Utc)
                            && decisionTime < DateTime.SpecifyKind(
                                policy.기사한시수요할증종료일시Utc.Value,
                                DateTimeKind.Utc);
        var demandSurcharge = demandApplied
            ? Math.Max(0m, policy.기사한시수요할증액)
            : 0m;

        return new 음식배달기사제안요금판정(
            basePayout,
            weatherSurcharge,
            demandSurcharge,
            basePayout + weatherSurcharge + demandSurcharge + 시간대할증액,
            weatherApplied,
            demandApplied)
        {
            시간대할증액 = 시간대할증액,
            기본계산정책 = calculationPolicy,
            기본요금구성 = components,
            기본지급구분Code = policy.기사픽업지급액.HasValue ? "PickupDropoffSplit" : "LegacyUnsplit"
        };
    }
}
