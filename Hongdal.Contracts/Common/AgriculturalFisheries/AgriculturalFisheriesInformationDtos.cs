using Hongdal.Contracts.Common.PublicData;

namespace Hongdal.Contracts.Common.AgriculturalFisheries;

public sealed class AgriculturalFisheriesInformationOverviewResponse
{
    public string ModuleCode { get; init; } = "KR_AGRI_FISH_INFORMATION";

    public string StageCode { get; init; } = "InformationFoundation";

    public string StageLabel { get; init; } = "정보 제공·이해 축적";

    public bool IsReadOnly { get; init; } = true;

    public bool AllowsReadinessRecordWrites { get; init; }

    public bool AllowsTransactionExecution { get; init; }

    public bool IsBrokerageEnabled { get; init; }

    public IReadOnlyList<string> SupportedMarketCodes { get; init; } = ["KR"];

    public string Positioning { get; init; } = string.Empty;

    public string BrokerageBoundaryNote { get; init; } = string.Empty;

    public string ReadinessRecordBoundaryNote { get; init; } = string.Empty;

    public IReadOnlyList<AgriculturalFisheriesDataSourceResponse> DataSources { get; init; } = [];

    public IReadOnlyList<AgriculturalFisheriesCapabilityResponse> Capabilities { get; init; } = [];

    public IReadOnlyList<string> NextDataPriorities { get; init; } = [];

    public IReadOnlyList<string> BrokerageReadinessRequirements { get; init; } = [];
}

public sealed class AgriculturalFisheriesDataSourceResponse
{
    public string Key { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Coverage { get; init; } = string.Empty;

    public string UpdateCycle { get; init; } = string.Empty;

    public string StatusCode { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public bool IsConfigured { get; init; }

    public string DocumentationUrl { get; init; } = string.Empty;

    public string UsageNote { get; init; } = string.Empty;
}

public sealed class AgriculturalFisheriesCapabilityResponse
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool AvailableNow { get; init; }

    public string? Endpoint { get; init; }
}

public sealed class AgriculturalFisheriesItemSearchResponse
{
    public IReadOnlyList<AgriculturalFisheriesItemResponse> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}

public sealed class AgriculturalFisheriesItemResponse
{
    public string HsPrefix { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string CategoryCode { get; init; } = string.Empty;

    public string CategoryLabel { get; init; } = string.Empty;

    public string AtItemCode { get; init; } = string.Empty;

    public string AtItemName { get; init; } = string.Empty;

    public IReadOnlyList<string> AtVarietyCodes { get; init; } = [];

    public string MatchQualityCode { get; init; } = string.Empty;

    public string MatchQualityLabel { get; init; } = string.Empty;

    public string DomesticOriginStatusCode { get; init; } = string.Empty;

    public string DomesticOriginStatusLabel { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public bool InformationOnly { get; init; } = true;
}

public sealed class AgriculturalFisheriesDomesticPriceRequest
{
    public string HsCode { get; init; } = string.Empty;

    public string ReferenceDate { get; init; } = string.Empty;

    public int LookbackDays { get; init; } = 14;
}

public sealed class AgriculturalFisheriesDomesticPriceResponse
{
    public bool Success { get; init; }

    public string StatusCode { get; init; } = "Unavailable";

    public string? ErrorMessage { get; init; }

    public string HsCode { get; init; } = string.Empty;

    public AgriculturalFisheriesItemResponse? Item { get; init; }

    public AtDomesticFoodPriceLookupResult? Price { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Notices { get; init; } = [];

    public bool InformationOnly { get; init; } = true;
}
