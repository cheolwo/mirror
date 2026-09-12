namespace 살뜰.Services.Options;

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

    public bool GroupPurchaseDemandWorkflow { get; set; }

    public bool GroupPurchasePracticeWorkflow { get; set; }

    public bool GroupPurchaseImportWorkflow { get; set; }

    public bool SalesChannelFulfillmentWorkflow { get; set; }

    public bool CommunityTrustWorkflow { get; set; } = true;

    public bool HrParticipationWorkflow { get; set; }

    public bool FoodDeliveryV30 { get; set; }

    public bool FoodDeliveryWorkflow { get; set; }

    public bool OperationalWorldObservationWorkflow { get; set; }

    public bool AdministrativeDongDioramaObservation { get; set; }

    public bool LocalDioramaSponsorship { get; set; }

    public bool SsalddelMartV35 { get; set; }

    public bool SsalddelMartWorkflow { get; set; }
}
