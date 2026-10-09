using System.Globalization;

namespace Ssalddel.Contracts.Common.Fulfillment;

/// <summary>음식 배송 수행비의 주문 전체 분담 조건입니다. 실제 청구·지급 증거는 아닙니다.</summary>
public sealed class DeliveryCostSharingPolicy
{
    public string Mode { get; set; } = string.Empty;
    public decimal? FixedAmountKrw { get; set; }
    public decimal? RestaurantSharePercent { get; set; }
    public decimal? MaximumRestaurantChargeKrw { get; set; }
}

public static class DeliveryCostSharingModes
{
    public const string CustomerPays = "CustomerPays";
    public const string FixedContribution = "FixedContribution";
    public const string PercentageShare = "PercentageShare";
    public const string RestaurantPays = "RestaurantPays";
}

public sealed record DeliveryCostSharingResult(bool IsAllowed, decimal? RestaurantChargeKrw,
    decimal? CustomerChargeKrw, string? ReasonCode = null);

/// <summary>선정·상세·화면 예시가 사용하는 같은 계산입니다. DB와 외부 업체를 호출하지 않습니다.</summary>
public static class DeliveryCostSharingCalculator
{
    public static string? ValidationError(DeliveryCostSharingPolicy? policy)
    {
        if (policy is null) return "CostSharingPolicyRequired";
        if (policy.MaximumRestaurantChargeKrw is { } maximum && !Money(maximum)) return "InvalidCostSharingPolicy";
        return policy.Mode switch
        {
            DeliveryCostSharingModes.CustomerPays when policy.FixedAmountKrw is null
                && policy.RestaurantSharePercent is null && policy.MaximumRestaurantChargeKrw is null => null,
            DeliveryCostSharingModes.FixedContribution when policy.FixedAmountKrw is { } fixedAmount && Money(fixedAmount)
                && policy.RestaurantSharePercent is null && policy.MaximumRestaurantChargeKrw is null => null,
            DeliveryCostSharingModes.PercentageShare when policy.FixedAmountKrw is null
                && policy.RestaurantSharePercent is { } percent && percent is >= 0 and <= 100
                && decimal.Round(percent, 2) == percent => null,
            DeliveryCostSharingModes.RestaurantPays when policy.FixedAmountKrw is null && policy.RestaurantSharePercent is null => null,
            _ => "InvalidCostSharingPolicy"
        };
    }

    public static DeliveryCostSharingResult Calculate(decimal totalCostKrw, DeliveryCostSharingPolicy policy)
    {
        if (totalCostKrw < 0 || ValidationError(policy) is { }) return new(false, null, null, "InvalidCostSharingPolicy");
        decimal restaurant;
        switch (policy.Mode)
        {
            case DeliveryCostSharingModes.CustomerPays: restaurant = 0; break;
            case DeliveryCostSharingModes.FixedContribution: restaurant = Math.Min(policy.FixedAmountKrw!.Value, totalCostKrw); break;
            case DeliveryCostSharingModes.PercentageShare:
                restaurant = decimal.Floor(totalCostKrw * policy.RestaurantSharePercent!.Value / 100m);
                if (policy.MaximumRestaurantChargeKrw is { } cap) restaurant = Math.Min(restaurant, cap);
                break;
            case DeliveryCostSharingModes.RestaurantPays:
                if (policy.MaximumRestaurantChargeKrw is { } maximum && totalCostKrw > maximum)
                    return new(false, null, null, "RestaurantChargeLimitExceeded");
                restaurant = totalCostKrw; break;
            default: return new(false, null, null, "InvalidCostSharingPolicy");
        }
        return new(true, restaurant, totalCostKrw - restaurant);
    }

    public static DeliveryCostSharingPolicy Effective(DeliveryCostSharingPolicy? policy, decimal legacyFixedSupport)
        => policy is null ? new() { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = legacyFixedSupport } : Clone(policy);

    public static DeliveryCostSharingPolicy Clone(DeliveryCostSharingPolicy policy) => new()
    {
        Mode = policy.Mode,
        FixedAmountKrw = Canonical(policy.FixedAmountKrw), RestaurantSharePercent = Canonical(policy.RestaurantSharePercent),
        MaximumRestaurantChargeKrw = Canonical(policy.MaximumRestaurantChargeKrw)
    };

    public static bool Equivalent(DeliveryCostSharingPolicy left, DeliveryCostSharingPolicy right)
        => left.Mode == right.Mode && left.FixedAmountKrw == right.FixedAmountKrw
            && left.RestaurantSharePercent == right.RestaurantSharePercent && left.MaximumRestaurantChargeKrw == right.MaximumRestaurantChargeKrw;

    private static bool Money(decimal value) => value is >= 0 and <= 1000000 && decimal.Truncate(value) == value;
    private static decimal? Canonical(decimal? value) => value is { } number
        ? decimal.Parse(number.ToString("G29", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture) : null;
}
