using System.Text.Json;
using Ssalddel.Contracts.Common.Fulfillment;

namespace Ssalddel.Services.Fulfillment;

/// <summary>기존 null 정책만 정액으로 해석합니다. 손상된 저장값을 구형 정액으로 숨기지 않습니다.</summary>
public static class DeliveryCostSharingStorage
{
    public static DeliveryCostSharingPolicy Read(string? json, decimal legacyFixedSupport)
    {
        if (json is null) return DeliveryCostSharingCalculator.Effective(null, legacyFixedSupport);
        try { return JsonSerializer.Deserialize<DeliveryCostSharingPolicy>(json) is { } policy
            ? DeliveryCostSharingCalculator.Clone(policy) : new(); }
        catch (JsonException) { return new(); }
    }

    public static string Write(DeliveryCostSharingPolicy policy) => JsonSerializer.Serialize(DeliveryCostSharingCalculator.Clone(policy));
    public static decimal LegacyAmount(DeliveryCostSharingPolicy policy)
        => policy.Mode == DeliveryCostSharingModes.FixedContribution ? policy.FixedAmountKrw ?? 0 : 0;
}
