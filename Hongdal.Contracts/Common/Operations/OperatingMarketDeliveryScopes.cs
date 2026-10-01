namespace Hongdal.Contracts.Common.Operations;

public static class OperatingGeographicAreaTypeCodes
{
    public const string State = "State";
    public const string County = "County";
    public const string IncorporatedPlace = "IncorporatedPlace";
    public const string CensusDesignatedPlace = "CensusDesignatedPlace";
    public const string ZipCodeTabulationArea = "ZipCodeTabulationArea";
}

public static class OperatingDeliveryScopeTypeCodes
{
    public const string StateDiscovery = "StateDiscovery";
    public const string CountyRecruitment = "CountyRecruitment";
    public const string PlaceRecruitment = "PlaceRecruitment";
    public const string ZctaRecruitment = "ZctaRecruitment";
}

public static class OperatingDeliveryBoundaryPolicyCodes
{
    public const string TravelTimeAndProviderServiceAreaRequired =
        "TravelTimeAndProviderServiceAreaRequired";
}

public static class OperatingDeliveryScopeLogisticsRoleCodes
{
    public const string RegionalInboundConsolidation = "RegionalInboundConsolidation";
    public const string RuralRouteConsolidation = "RuralRouteConsolidation";
    public const string UrbanHubConsolidation = "UrbanHubConsolidation";
    public const string LastMileStopConsolidation = "LastMileStopConsolidation";
}

public static class OperatingDeliveryScopeLogisticsPolicyCodes
{
    public const string ConsolidateDemandThenValidateFulfillment =
        "ConsolidateDemandThenValidateFulfillment";
}

public sealed class OperatingMarketGeographicArea
{
    public string AreaTypeCode { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public sealed class OperatingMarketDeliveryScopeCandidate
{
    public string MarketCode { get; init; } = string.Empty;

    public string ScopeKey { get; init; } = string.Empty;

    public string ScopeTypeCode { get; init; } = string.Empty;

    public string LogisticsRoleCode { get; init; } = string.Empty;

    public string GeographicAreaTypeCode { get; init; } = string.Empty;

    public string GeographicAreaCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? ParentScopeKey { get; init; }

    public bool IsRecommendedRecruitmentScope { get; init; }

    public bool IsRecommendedDemandConsolidationScope { get; init; }

    public bool IsFineGrained { get; init; }

    public int MinimumParticipantsForPublicDisplay { get; init; }

    public bool CanPublishForParticipantCount { get; init; }

    public bool SupportsLastMileBatching { get; init; }

    public bool RequiresLogisticsFeasibilityValidation { get; init; } = true;

    public bool RequiresOperationalRouteValidation { get; init; } = true;
}

public sealed class OperatingMarketDeliveryScopePlan
{
    public bool Success { get; init; }

    public string MarketCode { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    public string? RecommendedScopeKey { get; init; }

    public string? RecommendedDemandConsolidationScopeKey { get; init; }

    public string? ProviderCode { get; init; }

    public string? ProviderDatasetVersion { get; init; }

    public string? ProviderGeographyVintage { get; init; }

    public string OperationalBoundaryPolicyCode { get; init; } =
        OperatingDeliveryBoundaryPolicyCodes.TravelTimeAndProviderServiceAreaRequired;

    public string LogisticsEfficiencyPolicyCode { get; init; } =
        OperatingDeliveryScopeLogisticsPolicyCodes.ConsolidateDemandThenValidateFulfillment;

    public IReadOnlyList<OperatingMarketDeliveryScopeCandidate> Items { get; init; } = [];
}
