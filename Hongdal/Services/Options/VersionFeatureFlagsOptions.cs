namespace 홍달.Services.Options;

public sealed class VersionFeatureFlagsOptions
{
    public const string SectionName = "VersionFeatureFlags";

    public bool CargoYongdalV1 { get; set; }

    public bool DomesticTransportWorkflow { get; set; }

    public bool WarehouseV15 { get; set; }

    public bool WarehouseFulfillmentWorkflow { get; set; }

    public bool CustomsHsV20 { get; set; }

    public bool CustomsAndTradeDataWorkflow { get; set; }

    public bool OrdererGroupOrderV25 { get; set; }

    public bool ApartmentGroupOrderV25 { get; set; }

    public bool GroupPurchaseImportWorkflow { get; set; }

    public bool SalesChannelFulfillmentWorkflow { get; set; }

    public bool CommunityTrustWorkflow { get; set; } = true;

    public bool HrParticipationWorkflow { get; set; }

    public bool FoodDeliveryV30 { get; set; }

    public bool FoodDeliveryWorkflow { get; set; }

    public bool HongdalMartV35 { get; set; }

    public bool HongdalMartWorkflow { get; set; }
}
