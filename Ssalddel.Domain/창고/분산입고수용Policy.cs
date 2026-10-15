using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.창고;

/// <summary>수용 적합성만 판정합니다. 계약·입고 승인·재고 확정 권한은 호출하는 UseCase가 소유합니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.WarehouseInboundVertical, SsalddelCodeLayer.Domain,
    "공간 수용 조건과 명시된 상품 특성·공유 용량을 대조하고 불명확 조건을 보류한다.",
    Effects = SsalddelCodeEffect.None, StepKey = "domain.distributed-inbound-acceptance", FlowOrder = 25,
    DependsOnStepKeys = ["contract.distributed-inbound-collaboration"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview, ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "DB·외부 API·AI를 호출하거나 계약·승인·재고를 변경하지 않는다. 미분류와 단위·용량 미확인을 허용으로 바꾸지 않는다.")]
public static class 분산입고수용Policy
{
    public static IReadOnlyList<string> Validate(DistributedSiteAcceptancePolicy? policy)
    {
        if (policy is null) return ["AcceptancePolicyRequired"];
        var reasons = new List<string>();
        if (!KnownList(policy.AllowedCategoryCodes, DistributedInboundCodes.CategoryCodes, true)
            || !KnownList(policy.DeniedCategoryCodes, DistributedInboundCodes.CategoryCodes, false))
            reasons.Add("InvalidPolicyCategories");
        if (!KnownList(policy.AllowedPackagingCodes, DistributedInboundCodes.PackagingCodes, true))
            reasons.Add("InvalidPolicyPackaging");
        if (!KnownList(policy.AllowedTemperatureCodes, DistributedInboundCodes.TemperatureCodes, true))
            reasons.Add("InvalidPolicyTemperature");
        if (!ValidUnit(policy.CapacityUnit) || policy.MaximumCapacity <= 0)
            reasons.Add("InvalidPolicyCapacity");
        if (policy.MaximumWeightKg is <= 0 || policy.MaximumVolumeM3 is <= 0)
            reasons.Add("InvalidPolicyDimensions");
        if (policy.MaximumStorageDays is <= 0 or > 3650) reasons.Add("InvalidPolicyStorageDays");
        if (string.IsNullOrWhiteSpace(policy.AccessHours) || policy.AccessHours.Length > 500)
            reasons.Add("InvalidPolicyAccessHours");
        return reasons;
    }

    public static DistributedAcceptanceDecision EvaluateGoods(DistributedSiteAcceptancePolicy policy,
        DistributedGoodsFacts? goods, int quantity)
    {
        var reasons = Validate(policy).ToList();
        if (reasons.Count > 0) return Decision(reasons, 0, null, null, true);
        if (quantity <= 0) return Decision(["InvalidInboundQuantity"], 0, null, null);
        if (goods is null) return Decision([.. reasons, "GoodsFactsUnknown"], 0, null, null, true);
        // 겹치는 분류 모두 적용하고 명시적 거부를 가장 먼저 확인합니다.
        var categories = goods.CategoryCodes ?? [];
        if (categories.Any(x => policy.DeniedCategoryCodes.Contains(x, StringComparer.Ordinal)))
            reasons.Add("GoodsCategoryDenied");
        if (!KnownList(categories, DistributedInboundCodes.CategoryCodes, true)) reasons.Add("GoodsCategoryUnknown");
        else if (categories.Any(x => !policy.AllowedCategoryCodes.Contains(x, StringComparer.Ordinal)))
            reasons.Add("GoodsCategoryNotAllowed");
        if (goods.IsFood is null) reasons.Add("FoodClassificationUnknown");
        else if (goods.IsFood.Value && !policy.AllowFood) reasons.Add("FoodNotAllowed");
        if (goods.IsFood == true && policy.RequireProcessedFood)
        {
            if (goods.IsProcessed is null) reasons.Add("ProcessingClassificationUnknown");
            else if (!goods.IsProcessed.Value) reasons.Add("UnprocessedFoodNotAllowed");
        }
        if (categories.Contains(DistributedInboundCodes.PackagedFood, StringComparer.Ordinal))
        {
            if (goods.IsFood is null || goods.IsProcessed is null) reasons.Add("PackagedFoodFactsUnknown");
            else if (goods.IsFood != true || goods.IsProcessed != true) reasons.Add("GoodsClassificationConflict");
        }
        if (!DistributedInboundCodes.PackagingCodes.Contains(goods.PackagingCode, StringComparer.Ordinal))
            reasons.Add("PackagingUnknown");
        else if (!policy.AllowedPackagingCodes.Contains(goods.PackagingCode, StringComparer.Ordinal)
            || (policy.RequireSealedPackaging && goods.PackagingCode != DistributedInboundCodes.Sealed))
            reasons.Add("PackagingNotAllowed");
        if (!DistributedInboundCodes.TemperatureCodes.Contains(goods.TemperatureCode, StringComparer.Ordinal))
            reasons.Add("TemperatureUnknown");
        else if (!policy.AllowedTemperatureCodes.Contains(goods.TemperatureCode, StringComparer.Ordinal))
            reasons.Add("TemperatureNotAllowed");
        if (!ValidUnit(goods.CapacityUnit) || !string.Equals(goods.CapacityUnit, policy.CapacityUnit, StringComparison.Ordinal))
            reasons.Add("CapacityUnitMismatch");
        if (goods.CapacityPerItem <= 0) reasons.Add("GoodsCapacityUnknown");
        if (goods.WeightKgPerItem is <= 0 || goods.VolumeM3PerItem is <= 0) reasons.Add("InvalidGoodsDimensions");
        if (policy.MaximumWeightKg.HasValue && goods.WeightKgPerItem is null) reasons.Add("GoodsWeightUnknown");
        if (policy.MaximumVolumeM3.HasValue && goods.VolumeM3PerItem is null) reasons.Add("GoodsVolumeUnknown");
        try
        {
            return Decision(reasons, checked(goods.CapacityPerItem * quantity),
                goods.WeightKgPerItem.HasValue ? checked(goods.WeightKgPerItem.Value * quantity) : null,
                goods.VolumeM3PerItem.HasValue ? checked(goods.VolumeM3PerItem.Value * quantity) : null);
        }
        catch (OverflowException) { return Decision([.. reasons, "CapacityOverflow"], 0, null, null); }
    }

    public static DistributedAcceptanceDecision Evaluate(DistributedSiteAcceptancePolicy policy,
        DistributedGoodsFacts? goods, int quantity, DistributedCapacityUsage? existingUsage)
    {
        var product = EvaluateGoods(policy, goods, quantity);
        if (!product.IsAllowed) return product;
        var reasons = new List<string>();
        if (existingUsage is null || !existingUsage.IsKnown)
            return product with { IsAllowed = false, NeedsReview = true, ReasonCodes = ["ExistingCapacityUnknown"] };
        if (!string.Equals(existingUsage.CapacityUnit, policy.CapacityUnit, StringComparison.Ordinal))
            reasons.Add("ExistingCapacityUnitMismatch");
        if (existingUsage.Capacity < 0 || existingUsage.WeightKg is < 0 || existingUsage.VolumeM3 is < 0)
            reasons.Add("InvalidExistingCapacity");
        if (policy.MaximumWeightKg.HasValue && existingUsage.WeightKg is null) reasons.Add("ExistingWeightUnknown");
        if (policy.MaximumVolumeM3.HasValue && existingUsage.VolumeM3 is null) reasons.Add("ExistingVolumeUnknown");
        try
        {
            if (checked(existingUsage.Capacity + product.RequiredCapacity) > policy.MaximumCapacity)
                reasons.Add("CapacityExceeded");
            if (policy.MaximumWeightKg.HasValue && existingUsage.WeightKg.HasValue
                && checked(existingUsage.WeightKg.Value + product.RequiredWeightKg!.Value) > policy.MaximumWeightKg.Value)
                reasons.Add("WeightExceeded");
            if (policy.MaximumVolumeM3.HasValue && existingUsage.VolumeM3.HasValue
                && checked(existingUsage.VolumeM3.Value + product.RequiredVolumeM3!.Value) > policy.MaximumVolumeM3.Value)
                reasons.Add("VolumeExceeded");
        }
        catch (OverflowException) { reasons.Add("CapacityOverflow"); }
        return Decision(reasons, product.RequiredCapacity, product.RequiredWeightKg, product.RequiredVolumeM3);
    }

    public static IReadOnlyList<string> EvaluateSchedule(DistributedSiteAcceptancePolicy policy,
        DateTime arrivalStartUtc, DateTime arrivalEndUtc, DateTime storageEndsAtUtc, bool accessHoursConfirmed)
    {
        var reasons = new List<string>();
        if (arrivalStartUtc.Kind != DateTimeKind.Utc || arrivalEndUtc.Kind != DateTimeKind.Utc
            || storageEndsAtUtc.Kind != DateTimeKind.Utc || arrivalStartUtc >= arrivalEndUtc || arrivalEndUtc >= storageEndsAtUtc)
            reasons.Add("InvalidInboundSchedule");
        else if (policy.MaximumStorageDays.HasValue
            && storageEndsAtUtc - arrivalStartUtc > TimeSpan.FromDays(policy.MaximumStorageDays.Value))
            reasons.Add("StoragePeriodExceeded");
        if (!accessHoursConfirmed) reasons.Add("AccessHoursConfirmationRequired");
        return reasons;
    }

    private static DistributedAcceptanceDecision Decision(IReadOnlyList<string> reasons, decimal capacity,
        decimal? weight, decimal? volume, bool forceReview = false)
        => new(reasons.Count == 0, forceReview || reasons.Any(x => x.Contains("Unknown", StringComparison.Ordinal)
            || x.Contains("Mismatch", StringComparison.Ordinal)), reasons.Distinct(StringComparer.Ordinal).ToArray(), capacity, weight, volume);

    private static bool KnownList(IReadOnlyList<string>? items, IReadOnlyList<string> known, bool required)
        => items is not null && (!required || items.Count > 0) && items.Count <= known.Count
            && items.Distinct(StringComparer.Ordinal).Count() == items.Count
            && items.All(x => known.Contains(x, StringComparer.Ordinal));
    private static bool ValidUnit(string? unit) => !string.IsNullOrWhiteSpace(unit) && unit.Length <= 20 && unit.Trim() == unit;
}
