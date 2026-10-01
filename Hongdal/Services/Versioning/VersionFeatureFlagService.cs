using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace 홍달.Services.Versioning;

public sealed class VersionFeatureFlagService : IVersionFeatureFlagService
{
    private readonly IOptionsMonitor<VersionFeatureFlagsOptions> _options;

    public VersionFeatureFlagService(IOptionsMonitor<VersionFeatureFlagsOptions> options)
    {
        _options = options;
    }

    public bool IsEnabled(string featureKey)
    {
        var flags = _options.CurrentValue;
        return featureKey switch
        {
            VersionFeatureFlagKeys.CargoYongdalV1 => IsDomesticTransportEnabled(flags),
            VersionFeatureFlagKeys.DomesticTransportWorkflow => IsDomesticTransportEnabled(flags),
            VersionFeatureFlagKeys.WarehouseV15 => IsWarehouseFulfillmentEnabled(flags),
            VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow => IsWarehouseFulfillmentEnabled(flags),
            VersionFeatureFlagKeys.CustomsHsV20 => IsCustomsAndTradeDataEnabled(flags),
            VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow => IsCustomsAndTradeDataEnabled(flags),
            VersionFeatureFlagKeys.OrdererGroupOrderV25 => IsGroupPurchaseImportEnabled(flags),
            VersionFeatureFlagKeys.ApartmentGroupOrderV25 => IsGroupPurchaseImportEnabled(flags),
            VersionFeatureFlagKeys.GroupPurchaseImportWorkflow => IsGroupPurchaseImportEnabled(flags),
            VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow => IsSalesChannelFulfillmentEnabled(flags),
            VersionFeatureFlagKeys.CommunityTrustWorkflow => IsCommunityTrustEnabled(flags),
            VersionFeatureFlagKeys.HrParticipationWorkflow => IsHrParticipationEnabled(flags),
            VersionFeatureFlagKeys.FoodDeliveryV30 => IsFoodDeliveryEnabled(flags),
            VersionFeatureFlagKeys.FoodDeliveryWorkflow => IsFoodDeliveryEnabled(flags),
            VersionFeatureFlagKeys.HongdalMartV35 => IsHongdalMartEnabled(flags),
            VersionFeatureFlagKeys.HongdalMartWorkflow => IsHongdalMartEnabled(flags),
            _ => false
        };
    }

    public IReadOnlyDictionary<string, bool> GetAll()
    {
        var flags = _options.CurrentValue;
        return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [VersionFeatureFlagKeys.DomesticTransportWorkflow] = IsDomesticTransportEnabled(flags),
            [VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow] = IsWarehouseFulfillmentEnabled(flags),
            [VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow] = IsCustomsAndTradeDataEnabled(flags),
            [VersionFeatureFlagKeys.GroupPurchaseImportWorkflow] = IsGroupPurchaseImportEnabled(flags),
            [VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow] = IsSalesChannelFulfillmentEnabled(flags),
            [VersionFeatureFlagKeys.CommunityTrustWorkflow] = IsCommunityTrustEnabled(flags),
            [VersionFeatureFlagKeys.HrParticipationWorkflow] = IsHrParticipationEnabled(flags),
            [VersionFeatureFlagKeys.FoodDeliveryWorkflow] = IsFoodDeliveryEnabled(flags),
            [VersionFeatureFlagKeys.HongdalMartWorkflow] = IsHongdalMartEnabled(flags),
            [VersionFeatureFlagKeys.CargoYongdalV1] = IsDomesticTransportEnabled(flags),
            [VersionFeatureFlagKeys.WarehouseV15] = IsWarehouseFulfillmentEnabled(flags),
            [VersionFeatureFlagKeys.CustomsHsV20] = IsCustomsAndTradeDataEnabled(flags),
            [VersionFeatureFlagKeys.OrdererGroupOrderV25] = IsGroupPurchaseImportEnabled(flags),
            [VersionFeatureFlagKeys.FoodDeliveryV30] = IsFoodDeliveryEnabled(flags),
            [VersionFeatureFlagKeys.HongdalMartV35] = IsHongdalMartEnabled(flags)
        };
    }

    private static bool IsDomesticTransportEnabled(VersionFeatureFlagsOptions flags)
        => flags.CommunityTrustWorkflow
            && (flags.DomesticTransportWorkflow || flags.CargoYongdalV1);

    private static bool IsWarehouseFulfillmentEnabled(VersionFeatureFlagsOptions flags)
        => flags.WarehouseFulfillmentWorkflow || flags.WarehouseV15;

    private static bool IsCustomsAndTradeDataEnabled(VersionFeatureFlagsOptions flags)
        => flags.CustomsAndTradeDataWorkflow || flags.CustomsHsV20;

    private static bool IsGroupPurchaseImportEnabled(VersionFeatureFlagsOptions flags)
        => flags.GroupPurchaseImportWorkflow || flags.OrdererGroupOrderV25 || flags.ApartmentGroupOrderV25;

    private static bool IsSalesChannelFulfillmentEnabled(VersionFeatureFlagsOptions flags)
        => flags.SalesChannelFulfillmentWorkflow || IsWarehouseFulfillmentEnabled(flags) || IsGroupPurchaseImportEnabled(flags);

    private static bool IsCommunityTrustEnabled(VersionFeatureFlagsOptions flags)
        => flags.CommunityTrustWorkflow;

    private static bool IsHrParticipationEnabled(VersionFeatureFlagsOptions flags)
        => flags.HrParticipationWorkflow || IsGroupPurchaseImportEnabled(flags);

    private static bool IsFoodDeliveryEnabled(VersionFeatureFlagsOptions flags)
        => flags.FoodDeliveryWorkflow || flags.FoodDeliveryV30;

    private static bool IsHongdalMartEnabled(VersionFeatureFlagsOptions flags)
        => flags.HongdalMartWorkflow || flags.HongdalMartV35;
}

public static class VersionFeatureFlagKeys
{
    public const string DomesticTransportWorkflow = nameof(DomesticTransportWorkflow);

    public const string CargoYongdalV1 = nameof(CargoYongdalV1);

    public const string WarehouseFulfillmentWorkflow = nameof(WarehouseFulfillmentWorkflow);

    public const string WarehouseV15 = nameof(WarehouseV15);

    public const string CustomsAndTradeDataWorkflow = nameof(CustomsAndTradeDataWorkflow);

    public const string CustomsHsV20 = nameof(CustomsHsV20);

    public const string GroupPurchaseImportWorkflow = nameof(GroupPurchaseImportWorkflow);

    public const string OrdererGroupOrderV25 = nameof(OrdererGroupOrderV25);

    public const string ApartmentGroupOrderV25 = nameof(ApartmentGroupOrderV25);

    public const string SalesChannelFulfillmentWorkflow = nameof(SalesChannelFulfillmentWorkflow);

    public const string CommunityTrustWorkflow = nameof(CommunityTrustWorkflow);

    public const string HrParticipationWorkflow = nameof(HrParticipationWorkflow);

    public const string FoodDeliveryWorkflow = nameof(FoodDeliveryWorkflow);

    public const string FoodDeliveryV30 = nameof(FoodDeliveryV30);

    public const string HongdalMartWorkflow = nameof(HongdalMartWorkflow);

    public const string HongdalMartV35 = nameof(HongdalMartV35);
}
