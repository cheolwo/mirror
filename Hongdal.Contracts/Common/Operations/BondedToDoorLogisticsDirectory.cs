namespace Hongdal.Contracts.Common.Operations;

public static class BondedToDoorLogisticsStageCodes
{
    public const string CustomsControlledStorage = "CustomsControlledStorage";
    public const string CustomsWithdrawalAndRelease = "CustomsWithdrawalAndRelease";
    public const string InBondTransportation = "InBondTransportation";
    public const string ReleasedDomesticTransfer = "ReleasedDomesticTransfer";
    public const string FulfillmentWarehouseInbound = "FulfillmentWarehouseInbound";
    public const string BreakPackKittingAndRelabeling =
        "BreakPackKittingAndRelabeling";
    public const string ParticipantOrderPickPackAndParcelTender =
        "ParticipantOrderPickPackAndParcelTender";
    public const string ParticipantAddressFinalMileDelivery =
        "ParticipantAddressFinalMileDelivery";
    public const string ReturnsProcessing = "ReturnsProcessing";
}

public static class CustomsControlledStorageModelCodes
{
    public const string CustomsBondedWarehouse = "CustomsBondedWarehouse";
    public const string ForeignTradeZone = "ForeignTradeZone";
    public const string ExternalControlledFacilityHandoff =
        "ExternalControlledFacilityHandoff";
}

public static class CustomsControlledFacilityOperatorRelationshipCodes
{
    public const string ProviderOperated = "ProviderOperated";
    public const string PartnerOrAgentOperated = "PartnerOrAgentOperated";
}

public static class CustomsControlledFacilityVerificationStatusCodes
{
    public const string OfficialProviderClaimReviewed =
        "OfficialProviderClaimReviewed";
    public const string CurrentAuthorizationNotIndependentlyVerified =
        "CurrentAuthorizationNotIndependentlyVerified";
}

public static class BondedToDoorRoleRequirementCodes
{
    public const string ImporterOfRecordRequired = "ImporterOfRecordRequired";
    public const string LicensedCustomsBrokerOrSelfFilerRequired =
        "LicensedCustomsBrokerOrSelfFilerRequired";
    public const string CustomsBondRequiredWhenApplicable =
        "CustomsBondRequiredWhenApplicable";
    public const string BondedCarrierRequiredBeforeCustomsRelease =
        "BondedCarrierRequiredBeforeCustomsRelease";
    public const string DutiesTaxesAndAdmissibilityResolvedBeforeDomesticFulfillment =
        "DutiesTaxesAndAdmissibilityResolvedBeforeDomesticFulfillment";
    public const string ExactFacilityAuthorizationAndFirmsCodeVerificationRequired =
        "ExactFacilityAuthorizationAndFirmsCodeVerificationRequired";
    public const string ProductSpecificAgencyApprovalRequired =
        "ProductSpecificAgencyApprovalRequired";
    public const string ParticipantAddressConsentRequired =
        "ParticipantAddressConsentRequired";
    public const string ParcelCarrierAccountOrContractRequired =
        "ParcelCarrierAccountOrContractRequired";
}

public static class BondedToDoorDirectoryBoundaryCodes
{
    public const string EndToEndSingleContractNotVerified =
        "EndToEndSingleContractNotVerified";
    public const string ExactFacilityAuthorizationNotIndependentlyVerified =
        "ExactFacilityAuthorizationNotIndependentlyVerified";
    public const string CustomsBrokerPermitNotIndependentlyVerified =
        "CustomsBrokerPermitNotIndependentlyVerified";
    public const string BondedCarrierAuthorityNotIndependentlyVerified =
        "BondedCarrierAuthorityNotIndependentlyVerified";
    public const string ProviderControlledStorageNotRecorded =
        "ProviderControlledStorageNotRecorded";
    public const string ExactFacilityClaimNotRecorded =
        "ExactFacilityClaimNotRecorded";
    public const string UnitedStatesBondedWarehouseNotOfferedByProvider =
        "UnitedStatesBondedWarehouseNotOfferedByProvider";
    public const string ProductAndFinalMileEligibilityRequiresQuote =
        "ProductAndFinalMileEligibilityRequiresQuote";
}

public static class BondedToDoorEvidenceTypeCodes
{
    public const string OfficialProviderServicePage =
        "OfficialProviderServicePage";
    public const string OfficialProviderFacilityPage =
        "OfficialProviderFacilityPage";
    public const string OfficialProviderOperationalNotice =
        "OfficialProviderOperationalNotice";
}

public sealed class CustomsControlledFacilityClaim
{
    public string FacilityKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string StateCode { get; init; } = string.Empty;

    public string StorageModelCode { get; init; } = string.Empty;

    public string OperatorRelationshipCode { get; init; } =
        CustomsControlledFacilityOperatorRelationshipCodes.ProviderOperated;

    public string? FirmsCode { get; init; }

    public string ClaimSourceUrl { get; init; } = string.Empty;

    public DateOnly ReviewedOn { get; init; }

    public string ClaimVerificationStatusCode { get; init; } =
        CustomsControlledFacilityVerificationStatusCodes
            .OfficialProviderClaimReviewed;

    public string AuthorizationVerificationStatusCode { get; init; } =
        CustomsControlledFacilityVerificationStatusCodes
            .CurrentAuthorizationNotIndependentlyVerified;

    public bool RequiresCurrentAuthorizationConfirmation { get; init; } = true;

    public bool RequiresCurrentFirmsCodeConfirmation { get; init; } = true;
}

public sealed class BondedToDoorLogisticsEvidence
{
    public string EvidenceTypeCode { get; init; } = string.Empty;

    public string SourceTitle { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateOnly ReviewedOn { get; init; }

    public IReadOnlyList<string> SupportedStageCodes { get; init; } = [];

    public IReadOnlyList<string> SupportedStorageModelCodes { get; init; } = [];
}

public sealed class BondedToDoorLogisticsProfile
{
    public string ProviderKey { get; init; } = string.Empty;

    public string OfficialInquiryUrl { get; init; } = string.Empty;

    public bool AdvertisesIntegratedFlow { get; init; }

    public bool RequiresRoleComposition { get; init; } = true;

    public bool CanAutoAssign { get; init; }

    public bool CanExecuteWithoutContract { get; init; }

    public bool IsEndToEndContractConfirmed { get; init; }

    public bool IsCustomsBrokerPermitIndependentlyVerified { get; init; }

    public bool IsBondedCarrierAuthorityIndependentlyVerified { get; init; }

    public IReadOnlyList<string> StageCodes { get; init; } = [];

    public IReadOnlyList<string> StorageModelCodes { get; init; } = [];

    public IReadOnlyList<string> RequiredRoleCodes { get; init; } = [];

    public IReadOnlyList<string> DirectoryBoundaryCodes { get; init; } = [];

    public IReadOnlyList<CustomsControlledFacilityClaim> FacilityClaims { get; init; } = [];

    public IReadOnlyList<BondedToDoorLogisticsEvidence> Evidence { get; init; } = [];
}

public sealed class BondedToDoorLogisticsProviderCandidate
{
    public ThirdPartyLogisticsProviderDirectoryItem Provider { get; init; } = new();

    public BondedToDoorLogisticsProfile BondedToDoorProfile { get; init; } = new();
}

public sealed class BondedToDoorLogisticsDirectoryQuery
{
    public string? SearchText { get; init; }

    public string? StageCode { get; init; }

    public string? StorageModelCode { get; init; }

    public string? StateCode { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed class BondedToDoorLogisticsDirectoryResponse
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

    public IReadOnlyList<string> AvailableStageCodes { get; init; } = [];

    public IReadOnlyList<string> AvailableStorageModelCodes { get; init; } = [];

    public IReadOnlyList<string> AvailableStateCodes { get; init; } = [];

    public IReadOnlyList<string> UniversalRoleRequirementCodes { get; init; } = [];

    public IReadOnlyList<ThirdPartyLogisticsRegulatoryVerificationResource>
        RegulatoryVerificationResources { get; init; } = [];

    public IReadOnlyList<BondedToDoorLogisticsProviderCandidate> Items { get; init; } = [];
}
