using Microsoft.Extensions.Options;
using 살뜰.Services.Options;

namespace 살뜰.Services.Versioning;

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
            VersionFeatureFlagKeys.OrdererGroupOrderV25 => IsGroupPurchaseDemandEnabled(flags),
            VersionFeatureFlagKeys.ApartmentGroupOrderV25 => IsGroupPurchaseDemandEnabled(flags),
            VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow => IsGroupPurchaseDemandEnabled(flags),
            VersionFeatureFlagKeys.GroupPurchasePracticeWorkflow => IsGroupPurchasePracticeEnabled(flags),
            VersionFeatureFlagKeys.GroupPurchaseImportWorkflow => IsGroupPurchaseDemandEnabled(flags),
            VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow => IsSalesChannelFulfillmentEnabled(flags),
            VersionFeatureFlagKeys.CommunityTrustWorkflow => IsCommunityTrustEnabled(flags),
            VersionFeatureFlagKeys.HrParticipationWorkflow => IsHrParticipationEnabled(flags),
            VersionFeatureFlagKeys.FoodDeliveryV30 => IsFoodDeliveryEnabled(flags),
            VersionFeatureFlagKeys.FoodDeliveryWorkflow => IsFoodDeliveryEnabled(flags),
            VersionFeatureFlagKeys.OperationalWorldObservationWorkflow => flags.OperationalWorldObservationWorkflow,
            VersionFeatureFlagKeys.AdministrativeDongDioramaObservation => flags.AdministrativeDongDioramaObservation,
            VersionFeatureFlagKeys.LocalDioramaSponsorship => flags.AdministrativeDongDioramaObservation && flags.LocalDioramaSponsorship,
            VersionFeatureFlagKeys.SsalddelMartV35 => IsSsalddelMartEnabled(flags),
            VersionFeatureFlagKeys.SsalddelMartWorkflow => IsSsalddelMartEnabled(flags),
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
            [VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow] = IsGroupPurchaseDemandEnabled(flags),
            [VersionFeatureFlagKeys.GroupPurchasePracticeWorkflow] = IsGroupPurchasePracticeEnabled(flags),
            [VersionFeatureFlagKeys.GroupPurchaseImportWorkflow] = IsGroupPurchaseDemandEnabled(flags),
            [VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow] = IsSalesChannelFulfillmentEnabled(flags),
            [VersionFeatureFlagKeys.CommunityTrustWorkflow] = IsCommunityTrustEnabled(flags),
            [VersionFeatureFlagKeys.HrParticipationWorkflow] = IsHrParticipationEnabled(flags),
            [VersionFeatureFlagKeys.FoodDeliveryWorkflow] = IsFoodDeliveryEnabled(flags),
            [VersionFeatureFlagKeys.OperationalWorldObservationWorkflow] = flags.OperationalWorldObservationWorkflow,
            [VersionFeatureFlagKeys.AdministrativeDongDioramaObservation] = flags.AdministrativeDongDioramaObservation,
            [VersionFeatureFlagKeys.LocalDioramaSponsorship] = flags.AdministrativeDongDioramaObservation && flags.LocalDioramaSponsorship,
            [VersionFeatureFlagKeys.SsalddelMartWorkflow] = IsSsalddelMartEnabled(flags),
            [VersionFeatureFlagKeys.CargoYongdalV1] = IsDomesticTransportEnabled(flags),
            [VersionFeatureFlagKeys.WarehouseV15] = IsWarehouseFulfillmentEnabled(flags),
            [VersionFeatureFlagKeys.CustomsHsV20] = IsCustomsAndTradeDataEnabled(flags),
            [VersionFeatureFlagKeys.OrdererGroupOrderV25] = IsGroupPurchaseDemandEnabled(flags),
            [VersionFeatureFlagKeys.FoodDeliveryV30] = IsFoodDeliveryEnabled(flags),
            [VersionFeatureFlagKeys.SsalddelMartV35] = IsSsalddelMartEnabled(flags)
        };
    }

    private static bool IsDomesticTransportEnabled(VersionFeatureFlagsOptions flags)
        => flags.CommunityTrustWorkflow
            && (flags.DomesticTransportWorkflow || flags.CargoYongdalV1);

    private static bool IsWarehouseFulfillmentEnabled(VersionFeatureFlagsOptions flags)
        => flags.WarehouseFulfillmentWorkflow || flags.WarehouseV15;

    private static bool IsCustomsAndTradeDataEnabled(VersionFeatureFlagsOptions flags)
        => IsGroupPurchaseDemandEnabled(flags)
            && (flags.CustomsAndTradeDataWorkflow || flags.CustomsHsV20);

    private static bool IsGroupPurchaseDemandEnabled(VersionFeatureFlagsOptions flags)
        => flags.CommunityTrustWorkflow
            && (flags.GroupPurchaseDemandWorkflow
                || flags.GroupPurchaseImportWorkflow
                || flags.OrdererGroupOrderV25
                || flags.ApartmentGroupOrderV25);

    private static bool IsGroupPurchasePracticeEnabled(VersionFeatureFlagsOptions flags)
        => flags.CommunityTrustWorkflow && flags.GroupPurchasePracticeWorkflow;

    private static bool IsSalesChannelFulfillmentEnabled(VersionFeatureFlagsOptions flags)
        => flags.SalesChannelFulfillmentWorkflow || IsWarehouseFulfillmentEnabled(flags);

    private static bool IsCommunityTrustEnabled(VersionFeatureFlagsOptions flags)
        => flags.CommunityTrustWorkflow;

    private static bool IsHrParticipationEnabled(VersionFeatureFlagsOptions flags)
        => flags.HrParticipationWorkflow;

    private static bool IsFoodDeliveryEnabled(VersionFeatureFlagsOptions flags)
        => flags.FoodDeliveryWorkflow || flags.FoodDeliveryV30;

    private static bool IsSsalddelMartEnabled(VersionFeatureFlagsOptions flags)
        => flags.SsalddelMartWorkflow || flags.SsalddelMartV35;
}

public static class VersionFeatureFlagKeys
{
    public const string DomesticTransportWorkflow = nameof(DomesticTransportWorkflow);

    public const string CargoYongdalV1 = nameof(CargoYongdalV1);

    public const string WarehouseFulfillmentWorkflow = nameof(WarehouseFulfillmentWorkflow);

    public const string WarehouseV15 = nameof(WarehouseV15);

    public const string CustomsAndTradeDataWorkflow = nameof(CustomsAndTradeDataWorkflow);

    public const string CustomsHsV20 = nameof(CustomsHsV20);

    public const string GroupPurchaseDemandWorkflow = nameof(GroupPurchaseDemandWorkflow);

    public const string GroupPurchasePracticeWorkflow = nameof(GroupPurchasePracticeWorkflow);

    public const string GroupPurchaseImportWorkflow = nameof(GroupPurchaseImportWorkflow);

    public const string OrdererGroupOrderV25 = nameof(OrdererGroupOrderV25);

    public const string ApartmentGroupOrderV25 = nameof(ApartmentGroupOrderV25);

    public const string SalesChannelFulfillmentWorkflow = nameof(SalesChannelFulfillmentWorkflow);

    public const string CommunityTrustWorkflow = nameof(CommunityTrustWorkflow);

    public const string HrParticipationWorkflow = nameof(HrParticipationWorkflow);

    public const string FoodDeliveryWorkflow = nameof(FoodDeliveryWorkflow);

    public const string OperationalWorldObservationWorkflow = nameof(OperationalWorldObservationWorkflow);

    public const string AdministrativeDongDioramaObservation = nameof(AdministrativeDongDioramaObservation);

    public const string LocalDioramaSponsorship = nameof(LocalDioramaSponsorship);

    public const string FoodDeliveryV30 = nameof(FoodDeliveryV30);

    public const string SsalddelMartWorkflow = nameof(SsalddelMartWorkflow);

    public const string SsalddelMartV35 = nameof(SsalddelMartV35);
}
