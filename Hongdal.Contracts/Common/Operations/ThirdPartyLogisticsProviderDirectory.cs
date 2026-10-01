namespace Hongdal.Contracts.Common.Operations;

public static class ThirdPartyLogisticsProviderCapabilityCodes
{
    public const string WarehousingAndDistribution = "WarehousingAndDistribution";
    public const string EcommerceFulfillment = "EcommerceFulfillment";
    public const string OmnichannelFulfillment = "OmnichannelFulfillment";
    public const string InventoryManagement = "InventoryManagement";
    public const string ReverseLogistics = "ReverseLogistics";
    public const string TransportationManagement = "TransportationManagement";
    public const string DedicatedTransportation = "DedicatedTransportation";
    public const string LastMileDelivery = "LastMileDelivery";
    public const string FreightForwarding = "FreightForwarding";
    public const string FreightBrokerage = "FreightBrokerage";
    public const string CustomsBrokerage = "CustomsBrokerage";
    public const string CustomsControlledWarehousing =
        "CustomsControlledWarehousing";
    public const string ForeignTradeZoneOperations = "ForeignTradeZoneOperations";
    public const string InBondTransportation = "InBondTransportation";
    public const string PortDrayage = "PortDrayage";
    public const string Transloading = "Transloading";
    public const string ColdChain = "ColdChain";
    public const string TemperatureControlledStorage = "TemperatureControlledStorage";
    public const string ImportExportSupport = "ImportExportSupport";
    public const string ValueAddedServices = "ValueAddedServices";
    public const string CampaignFulfillment = "CampaignFulfillment";
    public const string KittingAndAssembly = "KittingAndAssembly";
    public const string LotAndExpirationTracking = "LotAndExpirationTracking";
    public const string RetailDistribution = "RetailDistribution";
}

public static class ThirdPartyLogisticsProviderSegmentCodes
{
    public const string EnterpriseContractLogistics = "EnterpriseContractLogistics";
    public const string EcommerceDirectToConsumer = "EcommerceDirectToConsumer";
    public const string RetailOmnichannel = "RetailOmnichannel";
    public const string SharedMultiClientWarehousing = "SharedMultiClientWarehousing";
    public const string FoodColdChain = "FoodColdChain";
    public const string PortAndImportDistribution = "PortAndImportDistribution";
    public const string CampaignAndCrowdfunding = "CampaignAndCrowdfunding";
    public const string SmallBusinessFlexibleFulfillment =
        "SmallBusinessFlexibleFulfillment";
    public const string HeavyAndBulkyFulfillment = "HeavyAndBulkyFulfillment";
}

public static class ThirdPartyLogisticsProviderCoverageCodes
{
    public const string UnitedStates = "UnitedStates";
    public const string NorthAmerica = "NorthAmerica";
    public const string Global = "Global";
}

public static class ThirdPartyLogisticsProviderDirectoryStatusCodes
{
    public const string ResearchCandidate = "ResearchCandidate";
}

public static class ThirdPartyLogisticsProviderRelationshipStatusCodes
{
    public const string NoPlatformRelationship = "NoPlatformRelationship";
}

public static class ThirdPartyLogisticsProviderVerificationStatusCodes
{
    public const string OfficialCompanySourceReviewed = "OfficialCompanySourceReviewed";
    public const string RegulatoryStatusNotVerified = "RegulatoryStatusNotVerified";
}

public static class ThirdPartyLogisticsProviderEvidenceTypeCodes
{
    public const string OfficialProviderServicePage = "OfficialProviderServicePage";
    public const string OfficialProviderLocationPage = "OfficialProviderLocationPage";
}

public static class ThirdPartyLogisticsProviderSelectionPolicyCodes
{
    public const string NeutralCandidateDirectory = "NeutralCandidateDirectory";
}

public static class ThirdPartyLogisticsProviderDirectoryErrorCodes
{
    public const string MarketNotAvailableInDeployment =
        "MarketNotAvailableInDeployment";
}

public static class ThirdPartyLogisticsRegulatoryResourceCodes
{
    public const string FmcsaAuthorityAndInsurance = "FmcsaAuthorityAndInsurance";
    public const string FmcOceanTransportationIntermediary =
        "FmcOceanTransportationIntermediary";
    public const string CbpBondedWarehouse = "CbpBondedWarehouse";
    public const string CbpPermittedCustomsBrokers = "CbpPermittedCustomsBrokers";
    public const string CbpInBondTransportation = "CbpInBondTransportation";
    public const string CbpCustomsBond = "CbpCustomsBond";
    public const string CbpFirmsCode = "CbpFirmsCode";
    public const string ForeignTradeZonesBoard = "ForeignTradeZonesBoard";
    public const string EpaSmartWayPartner = "EpaSmartWayPartner";
}

public sealed class ThirdPartyLogisticsProviderEvidence
{
    public string EvidenceTypeCode { get; init; } = string.Empty;

    public string SourceTitle { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateOnly ReviewedOn { get; init; }

    public IReadOnlyList<string> SupportedCapabilityCodes { get; init; } = [];
}

public sealed class ThirdPartyLogisticsProviderDirectoryItem
{
    public string MarketCode { get; init; } = string.Empty;

    public string ProviderKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string OfficialWebsiteUrl { get; init; } = string.Empty;

    public string DirectoryStatusCode { get; init; } =
        ThirdPartyLogisticsProviderDirectoryStatusCodes.ResearchCandidate;

    public string PlatformRelationshipStatusCode { get; init; } =
        ThirdPartyLogisticsProviderRelationshipStatusCodes.NoPlatformRelationship;

    public string CompanySourceVerificationStatusCode { get; init; } =
        ThirdPartyLogisticsProviderVerificationStatusCodes
            .OfficialCompanySourceReviewed;

    public string RegulatoryVerificationStatusCode { get; init; } =
        ThirdPartyLogisticsProviderVerificationStatusCodes
            .RegulatoryStatusNotVerified;

    public bool IsPlatformPartner { get; init; }

    public bool CanBeSelectedForOperations { get; init; }

    public bool RequiresDirectQuote { get; init; } = true;

    public bool RequiresFacilityCapabilityConfirmation { get; init; } = true;

    public IReadOnlyList<string> CoverageCodes { get; init; } = [];

    public IReadOnlyList<string> CapabilityCodes { get; init; } = [];

    public IReadOnlyList<string> SegmentCodes { get; init; } = [];

    public IReadOnlyList<ThirdPartyLogisticsProviderEvidence> Evidence { get; init; } = [];
}

public sealed class ThirdPartyLogisticsRegulatoryVerificationResource
{
    public string ResourceCode { get; init; } = string.Empty;

    public string AuthorityName { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;

    public string OfficialUrl { get; init; } = string.Empty;
}

public sealed class ThirdPartyLogisticsProviderDirectoryQuery
{
    public string? SearchText { get; init; }

    public string? CapabilityCode { get; init; }

    public string? SegmentCode { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed class ThirdPartyLogisticsProviderDirectoryResponse
{
    public bool Success { get; init; }

    public string MarketCode { get; init; } = string.Empty;

    public string CatalogVersion { get; init; } = string.Empty;

    public DateOnly? SnapshotReviewedOn { get; init; }

    public string SelectionPolicyCode { get; init; } =
        ThirdPartyLogisticsProviderSelectionPolicyCodes.NeutralCandidateDirectory;

    public bool IsRecommendation { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<string> AvailableCapabilityCodes { get; init; } = [];

    public IReadOnlyList<string> AvailableSegmentCodes { get; init; } = [];

    public IReadOnlyList<ThirdPartyLogisticsRegulatoryVerificationResource>
        RegulatoryVerificationResources { get; init; } = [];

    public IReadOnlyList<ThirdPartyLogisticsProviderDirectoryItem> Items { get; init; } = [];
}
