using Ssalddel.Contracts.Common.Finance;

namespace 살뜰.도메인.음식;

/// <summary>입력 판본의 검토 계산만 수행한다. 배차 가격·원장·지급 상태를 변경하지 않는다.</summary>
public static class 음식배달지급검토Policy
{
    public static FoodDeliveryPayoutPricingReviewResponse 건별계산(
        FoodDeliveryPayoutPricingReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Policy);
        ArgumentNullException.ThrowIfNull(request.Deliveries);
        var policy = request.Policy;
        ValidateRevision(policy.Revision, policy.EffectiveFrom, policy.EffectiveThrough);
        Money(policy.PickupFeeKrw); Money(policy.DropoffFeeKrw); Money(policy.DistanceUnitFeeKrw);
        Money(policy.MinimumPayoutKrw);
        Require(policy.IncludedDistanceMeters is >= 0 and <= 1000000, "포함 거리(m)가 유효하지 않습니다.");
        Require(policy.DistanceUnitMeters is >= 1 and <= 100000, "거리 단위(m)가 유효하지 않습니다.");
        Require(policy.DistanceRoundingCode is "Floor" or "Ceiling", "거리 절사/올림을 명시해야 합니다.");
        Require(request.Deliveries.Count is >= 1 and <= 1000, "검토할 배달은 1~1000건이어야 합니다.");
        var deliveryKeys = new HashSet<string>(StringComparer.Ordinal);
        var pickupDays = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
        var lines = new List<FoodDeliveryPayoutReviewLine>();
        foreach (var delivery in request.Deliveries)
        {
            ArgumentNullException.ThrowIfNull(delivery);
            Key(delivery.DeliveryKey); Key(delivery.TimeBandCode);
            Require(deliveryKeys.Add(delivery.DeliveryKey), "배달 식별자가 중복됐습니다.");
            Require(delivery.ServiceDate >= policy.EffectiveFrom && delivery.ServiceDate <= policy.EffectiveThrough,
                "배달일이 요금 정책의 유효기간 밖입니다.");
            Require(delivery.DistanceKm is >= 0m and <= 1000m, "확인된 거리(km)가 필요합니다. 미확인은 0으로 대체하지 않습니다.");
            Require(delivery.DistanceBasisCode is "AppDisplayed" or "RouteEstimate" or "ConfirmedRoute",
                "앱 표시/경로 추정/확정 경로 거리의 구분이 필요합니다.");
            Money(delivery.TimeSurchargeKrw); Money(delivery.StorePromotionKrw); Money(delivery.RegionSurchargeKrw);
            var pickup = policy.PickupFeeKrw;
            if (delivery.SimultaneousPickupKey is not null)
            {
                Key(delivery.SimultaneousPickupKey);
                if (pickupDays.TryGetValue(delivery.SimultaneousPickupKey, out var date))
                {
                    Require(date == delivery.ServiceDate, "동시 픽업 묶음은 날짜를 넘길 수 없습니다.");
                    pickup = 0m;
                }
                else
                    pickupDays.Add(delivery.SimultaneousPickupKey, delivery.ServiceDate);
            }
            lines.Add(구성계산(policy, delivery, pickup));
        }
        return new(policy.Revision, lines, lines.Sum(line => line.GrossPayoutKrw), policy);
    }

    // 검토 API와 실제 배차가 같은 거리/구성 산식을 사용한다. 호출자는 정책과 픽업 귀속을 확정한다.
    // 기존 운영 정책의 decimal(18,2) 지급액도 보존한다.
    public static FoodDeliveryPayoutReviewLine 구성계산(
        FoodDeliveryPayoutPricingPolicy policy,
        FoodDeliveryPayoutReviewDelivery delivery,
        decimal pickupFeeKrw)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(delivery);
        Require(delivery.DistanceKm is >= 0m and <= 1000m, "확인된 거리(km)가 필요합니다. 미확인은 0으로 대체하지 않습니다.");
        Require(policy.DistanceUnitMeters is >= 1 and <= 100000 && policy.IncludedDistanceMeters is >= 0 and <= 1000000,
            "거리 정책의 단위/포함 거리가 유효하지 않습니다.");
        Require(policy.DistanceRoundingCode is "Floor" or "Ceiling", "거리 절사/올림을 명시해야 합니다.");
        Require(new[] { pickupFeeKrw, policy.DropoffFeeKrw, policy.DistanceUnitFeeKrw, policy.MinimumPayoutKrw,
            delivery.TimeSurchargeKrw, delivery.StorePromotionKrw, delivery.RegionSurchargeKrw }
            .All(value => value is >= 0m and <= 1000000000m), "지급 구성액이 유효하지 않습니다.");
        var distance = delivery.DistanceKm!.Value;
        var units = Math.Max(0m, distance * 1000m - policy.IncludedDistanceMeters) / policy.DistanceUnitMeters;
        units = policy.DistanceRoundingCode == "Floor" ? Math.Floor(units) : Math.Ceiling(units);
        var distanceFee = units * policy.DistanceUnitFeeKrw;
        var gross = pickupFeeKrw + policy.DropoffFeeKrw + distanceFee + delivery.TimeSurchargeKrw
                    + delivery.StorePromotionKrw + delivery.RegionSurchargeKrw;
        var minimumAdjustment = Math.Max(0m, policy.MinimumPayoutKrw - gross);
        return new(delivery.DeliveryKey, delivery.ServiceDate, distance, delivery.DistanceBasisCode,
            delivery.TimeBandCode, delivery.SimultaneousPickupKey, pickupFeeKrw, policy.DropoffFeeKrw,
            distanceFee, delivery.TimeSurchargeKrw, delivery.StorePromotionKrw, delivery.RegionSurchargeKrw,
            minimumAdjustment, gross + minimumAdjustment);
    }

    public static FoodDeliverySettlementReviewResponse 기간계산(FoodDeliverySettlementReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Policy);
        var policy = request.Policy;
        ValidateRevision(policy.Revision, policy.EffectiveFrom, policy.EffectiveThrough);
        Require(request.PeriodStart <= request.PeriodEnd && request.PeriodStart >= policy.EffectiveFrom
                && request.PeriodEnd <= policy.EffectiveThrough, "정산 기간은 정책 유효기간 안이어야 합니다.");
        Money(request.DeliveryGrossKrw); Money(request.MissionGrossKrw);
        Money(request.PeriodPromotionGrossKrw); Money(request.MilestoneBonusGrossKrw);
        Require(policy.RewardTaxTruncationUnitKrw is >= 1 and <= 1000, "보상 원천세 절사 단위가 유효하지 않습니다.");
        var baseGross = request.DeliveryGrossKrw + request.MissionGrossKrw;
        var rewards = request.PeriodPromotionGrossKrw + request.MilestoneBonusGrossKrw;
        var gross = baseGross + rewards;
        var pending = new List<string>();
        decimal? insuranceBase = policy.IncludePeriodRewardsInInsurance.HasValue
            ? baseGross + (policy.IncludePeriodRewardsInInsurance.Value ? rewards : 0m)
            : rewards == 0m ? baseGross : null;
        if (rewards > 0m && policy.IncludePeriodRewardsInInsurance.HasValue)
            Require(!string.IsNullOrWhiteSpace(policy.RewardInsuranceTreatmentEvidenceRef), "보상의 보험 기준 포함/제외 근거가 필요합니다.");
        var income = Deduct("IncomeTax", baseGross, policy.IncomeTax, pending, insurance: false);
        var local = Deduct("LocalIncomeTax", baseGross, policy.LocalIncomeTax, pending, insurance: false);
        var employment = Deduct("EmploymentInsurance", insuranceBase, policy.EmploymentInsurance, pending, insurance: true);
        var accident = Deduct("AccidentInsurance", insuranceBase, policy.AccidentInsurance, pending, insurance: true);
        decimal? rewardTax = rewards == 0m ? 0m : null;
        if (rewards > 0m && income.AmountKrw.HasValue && local.AmountKrw.HasValue)
        {
            var rate = (policy.IncomeTax.ApplicabilityCode == "Applicable" ? policy.IncomeTax.Rate!.Value : 0m)
                     + (policy.LocalIncomeTax.ApplicabilityCode == "Applicable" ? policy.LocalIncomeTax.Rate!.Value : 0m);
            rewardTax = Truncate(rewards * rate, policy.RewardTaxTruncationUnitKrw);
        }
        if (!rewardTax.HasValue) pending.Add("RewardTaxEligibilityUnknown");
        FoodDeliveryDeductionReviewLine[] deductions = [income, local, employment, accident];
        decimal? total = pending.Count == 0 ? deductions.Sum(line => line.AmountKrw!.Value) + rewardTax!.Value : null;
        decimal? net = total.HasValue ? gross - total.Value : null;
        if (net < 0m)
        {
            pending.Add("DeductionsExceedGross");
            net = null;
        }
        return new(policy.Revision, request.PeriodStart, request.PeriodEnd, baseGross,
            request.PeriodPromotionGrossKrw, request.MilestoneBonusGrossKrw, gross,
            deductions, rewardTax, total, net, pending.Count == 0 ? "Estimate" : "NeedsReview", pending, policy);
    }

    private static FoodDeliveryDeductionReviewLine Deduct(string code, decimal? basis,
        FoodDeliveryDeductionReviewRule rule, List<string> pending, bool insurance)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Require(rule.ApplicabilityCode is "Unknown" or "Applicable" or "NotApplicable", "공제 적용 상태가 유효하지 않습니다.");
        Money(rule.MinimumKrw);
        Require(rule.ExpenseRatio is >= 0m and <= 1m, "필요경비 비율은 0~1이어야 합니다.");
        Require(rule.Rate is null or >= 0m and <= 1m, "공제 비율은 0~1이어야 합니다.");
        Require(rule.TruncationUnitKrw is >= 1 and <= 1000, "공제 절사 단위가 유효하지 않습니다.");
        Require(insurance || (rule.ExpenseRatio == 0m && rule.MinimumKrw == 0m), "원천세에 보험 필요경비/최저보험료를 적용할 수 없습니다.");
        decimal? amount = null;
        if (rule.ApplicabilityCode == "Unknown")
            pending.Add(code + "EligibilityUnknown");
        else
        {
            Require(!string.IsNullOrWhiteSpace(rule.EvidenceRef), "공제 적용/미적용 판정 근거가 필요합니다.");
            if (rule.ApplicabilityCode == "NotApplicable") amount = 0m;
            else
            {
                Require(rule.Rate.HasValue, "적용 공제의 비율을 확인해야 합니다.");
                if (!basis.HasValue) pending.Add(code + "RewardBasisUnknown");
                else amount = basis.Value == 0m ? 0m : Math.Max(rule.MinimumKrw,
                    Truncate(basis.Value * (1m - rule.ExpenseRatio) * rule.Rate!.Value, rule.TruncationUnitKrw));
            }
        }
        return new(code, rule.ApplicabilityCode, basis, rule.ExpenseRatio, rule.Rate,
            rule.TruncationUnitKrw, amount, rule.EvidenceRef);
    }

    private static decimal Truncate(decimal value, int unit) => Math.Floor(value / unit) * unit;
    private static void Money(decimal value) => Require(value is >= 0m and <= 1000000000m
        && value == decimal.Truncate(value), "금액은 확인된 0 이상 정수 원이어야 합니다.");
    private static void Key(string value) => Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 100
        && value == value.Trim(), "식별자/시간대는 앞뒤 공백 없는 1~100자여야 합니다.");
    private static void ValidateRevision(string revision, DateOnly from, DateOnly through)
    {
        Key(revision);
        Require(from != default && through >= from, "정책의 유효 시작일·종료일이 필요합니다.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException(message);
    }
}
