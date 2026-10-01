namespace Hongdal.Contracts.Common.Operations;

public static class CollectivePurchaseLogisticsStageCodes
{
    public const string InternationalInboundCoordination =
        "InternationalInboundCoordination";
    public const string CustomsBrokerageCoordination =
        "CustomsBrokerageCoordination";
    public const string PortDrayageAndTransload = "PortDrayageAndTransload";
    public const string BulkInboundReceiving = "BulkInboundReceiving";
    public const string SharedInventoryStorage = "SharedInventoryStorage";
    public const string BreakPackKittingAndRelabeling =
        "BreakPackKittingAndRelabeling";
    public const string ParticipantParcelFulfillment =
        "ParticipantParcelFulfillment";
    public const string RegionalHubOrRetailDistribution =
        "RegionalHubOrRetailDistribution";
    public const string ReturnsProcessing = "ReturnsProcessing";
}

public static class CollectivePurchaseProductHandlingCodes
{
    public const string GeneralMerchandise = "GeneralMerchandise";
    public const string SmallLightParcel = "SmallLightParcel";
    public const string HeavyOrBulkyGoods = "HeavyOrBulkyGoods";
    public const string ShelfStablePackagedFoodByReview =
        "ShelfStablePackagedFoodByReview";
    public const string RefrigeratedFoodByFacilityReview =
        "RefrigeratedFoodByFacilityReview";
    public const string FrozenFoodByFacilityReview =
        "FrozenFoodByFacilityReview";
    public const string LotOrExpirationTrackedGoods =
        "LotOrExpirationTrackedGoods";
}

public static class CollectivePurchaseEngagementSignalCodes
{
    public const string CampaignFulfillmentAdvertised =
        "CampaignFulfillmentAdvertised";
    public const string StartupOrEmergingBrandAdvertised =
        "StartupOrEmergingBrandAdvertised";
    public const string NoOngoingOrderMinimumAdvertised =
        "NoOngoingOrderMinimumAdvertised";
    public const string CampaignBackerOrderGuidelinePublished =
        "CampaignBackerOrderGuidelinePublished";
    public const string NoLongTermContractAdvertised =
        "NoLongTermContractAdvertised";
    public const string PublishedMonthlyMinimum = "PublishedMonthlyMinimum";
    public const string MinimumVolumeAppliesAmountNotPublished =
        "MinimumVolumeAppliesAmountNotPublished";
    public const string MonthlyMinimumFormulaApplies =
        "MonthlyMinimumFormulaApplies";
    public const string CustomQuoteRequired = "CustomQuoteRequired";
    public const string EnterpriseConsultation = "EnterpriseConsultation";
}

public static class CollectivePurchaseCommercialConditionCodes
{
    public const string OrderMinimum = "OrderMinimum";
    public const string LongTermContract = "LongTermContract";
    public const string SetupFee = "SetupFee";
    public const string MonthlyPickPackMinimum = "MonthlyPickPackMinimum";
    public const string ApproximateOrdersAtMonthlyMinimum =
        "ApproximateOrdersAtMonthlyMinimum";
    public const string MonthlyMinimumFormula = "MonthlyMinimumFormula";
    public const string MinimumMonthlySpendAndOrderVolume =
        "MinimumMonthlySpendAndOrderVolume";
    public const string CampaignBackerOrderGuideline =
        "CampaignBackerOrderGuideline";
    public const string MonthlyAccountFee = "MonthlyAccountFee";
}

public static class CollectivePurchaseCommercialConditionValueCodes
{
    public const string NoneAdvertised = "NoneAdvertised";
    public const string AppliesAmountNotPublished = "AppliesAmountNotPublished";
    public const string ProjectedOrdersBasedFormula = "ProjectedOrdersBasedFormula";
    public const string MonthToMonthWithThirtyDayMinimum =
        "MonthToMonthWithThirtyDayMinimum";
    public const string MonthToMonthOrLongTermChoice =
        "MonthToMonthOrLongTermChoice";
}

public static class CollectivePurchaseCommercialConditionScopeCodes
{
    public const string OngoingFulfillment = "OngoingFulfillment";
    public const string CampaignFulfillment = "CampaignFulfillment";
    public const string Account = "Account";
}

public static class CollectivePurchaseLogisticsRestrictionCodes
{
    public const string PerishableFoodNotAccepted = "PerishableFoodNotAccepted";
    public const string FrozenFoodNotAccepted = "FrozenFoodNotAccepted";
    public const string ClimateControlledGoodsNotAccepted =
        "ClimateControlledGoodsNotAccepted";
    public const string HeavyOrOversizedGoodsGenerallyNotAccepted =
        "HeavyOrOversizedGoodsGenerallyNotAccepted";
    public const string WholesalePalletDistributionNotOffered =
        "WholesalePalletDistributionNotOffered";
}

public static class CollectivePurchaseLogisticsResponsibilityCodes
{
    public const string ImporterOfRecord = "ImporterOfRecord";
    public const string ProductRegulatoryCompliance = "ProductRegulatoryCompliance";
    public const string CustomsDutiesAndTaxes = "CustomsDutiesAndTaxes";
    public const string InventoryTitleAndOwnership = "InventoryTitleAndOwnership";
    public const string ParticipantOrderAndAddressConsent =
        "ParticipantOrderAndAddressConsent";
    public const string FoodFacilityAndColdChainValidation =
        "FoodFacilityAndColdChainValidation";
}

public static class CollectivePurchaseLogisticsQuoteInputCodes
{
    public const string ProductCategoryAndRegulatoryClass =
        "ProductCategoryAndRegulatoryClass";
    public const string CountryOfOriginAndSupplier = "CountryOfOriginAndSupplier";
    public const string ImportPortOrInboundOrigin = "ImportPortOrInboundOrigin";
    public const string UnitsCasesPalletsAndSkuCount = "UnitsCasesPalletsAndSkuCount";
    public const string WeightAndDimensions = "WeightAndDimensions";
    public const string TemperatureLotAndExpiration =
        "TemperatureLotAndExpiration";
    public const string ParticipantDestinationDistribution =
        "ParticipantDestinationDistribution";
    public const string OneTimeOrRecurringSchedule = "OneTimeOrRecurringSchedule";
    public const string KittingPackagingAndLabeling = "KittingPackagingAndLabeling";
    public const string ReturnsAndUndeliverablePolicy =
        "ReturnsAndUndeliverablePolicy";
    public const string ImporterAndCustomsResponsibility =
        "ImporterAndCustomsResponsibility";
}

public sealed class CollectivePurchasePublishedCommercialCondition
{
    public string ConditionCode { get; init; } = string.Empty;

    public string? ScopeCode { get; init; }

    public string? ValueCode { get; init; }

    public decimal? Amount { get; init; }

    public string? CurrencyCode { get; init; }

    public int? ApproximateOrderCount { get; init; }

    public string SourceUrl { get; init; } = string.Empty;

    public DateOnly ReviewedOn { get; init; }

    public bool RequiresReconfirmationBeforeContract { get; init; } = true;
}

public sealed class CollectivePurchaseLogisticsEvidence
{
    public string SourceTitle { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateOnly ReviewedOn { get; init; }

    public IReadOnlyList<string> SupportedStageCodes { get; init; } = [];

    public IReadOnlyList<string> SupportedProductHandlingCodes { get; init; } = [];

    public IReadOnlyList<string> SupportedEngagementSignalCodes { get; init; } = [];

    public IReadOnlyList<string> SupportedRestrictionCodes { get; init; } = [];
}

public sealed class CollectivePurchaseLogisticsProfile
{
    public string ProviderKey { get; init; } = string.Empty;

    public string OfficialInquiryUrl { get; init; } = string.Empty;

    public bool RequiresRoleComposition { get; init; } = true;

    public bool CanAutoAssign { get; init; }

    public bool CanExecuteWithoutContract { get; init; }

    public IReadOnlyList<string> StageCodes { get; init; } = [];

    public IReadOnlyList<string> ProductHandlingCodes { get; init; } = [];

    public IReadOnlyList<string> EngagementSignalCodes { get; init; } = [];

    public IReadOnlyList<string> ExplicitRestrictionCodes { get; init; } = [];

    public IReadOnlyList<string> ExternalResponsibilityCodes { get; init; } = [];

    public IReadOnlyList<CollectivePurchasePublishedCommercialCondition>
        PublishedCommercialConditions { get; init; } = [];

    public IReadOnlyList<CollectivePurchaseLogisticsEvidence> Evidence { get; init; } = [];
}

public sealed class CollectivePurchaseLogisticsProviderCandidate
{
    public ThirdPartyLogisticsProviderDirectoryItem Provider { get; init; } = new();

    public CollectivePurchaseLogisticsProfile CollectivePurchaseProfile { get; init; } = new();
}

public sealed class CollectivePurchaseLogisticsDirectoryQuery
{
    public string? SearchText { get; init; }

    public string? StageCode { get; init; }

    public string? ProductHandlingCode { get; init; }

    public string? EngagementSignalCode { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed class CollectivePurchaseLogisticsDirectoryResponse
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

    public IReadOnlyList<string> AvailableProductHandlingCodes { get; init; } = [];

    public IReadOnlyList<string> AvailableEngagementSignalCodes { get; init; } = [];

    public IReadOnlyList<string> RequiredQuoteInputCodes { get; init; } = [];

    public IReadOnlyList<ThirdPartyLogisticsRegulatoryVerificationResource>
        RegulatoryVerificationResources { get; init; } = [];

    public IReadOnlyList<CollectivePurchaseLogisticsProviderCandidate> Items { get; init; } = [];
}
