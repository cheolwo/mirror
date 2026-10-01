namespace Hongdal.Contracts.Common.PublicData;

public sealed class AtDomesticFoodPriceRequest
{
    public string CategoryCode { get; init; } = string.Empty;

    public string ItemCode { get; init; } = string.Empty;

    public string StartDate { get; init; } = string.Empty;

    public string EndDate { get; init; } = string.Empty;

    public IReadOnlyList<string> VarietyCodes { get; init; } = [];

    public IReadOnlyList<string> ExcludedNameTokens { get; init; } = [];
}

public sealed class AtDomesticFoodPriceLookupResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public string CategoryCode { get; init; } = string.Empty;

    public string ItemCode { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string StartDate { get; init; } = string.Empty;

    public string EndDate { get; init; } = string.Empty;

    public AtDomesticFoodPriceAggregate? Wholesale { get; init; }

    public AtDomesticFoodPriceAggregate? Retail { get; init; }

    public string DataSource { get; init; } = "한국농수산식품유통공사(aT) 일별 도·소매 가격정보";
}

public sealed class AtDomesticFoodPriceAggregate
{
    public string PriceTypeCode { get; init; } = string.Empty;

    public string PriceTypeLabel { get; init; } = string.Empty;

    public string LatestSurveyDate { get; init; } = string.Empty;

    public decimal AverageKrwPerKg { get; init; }

    public decimal MinimumKrwPerKg { get; init; }

    public decimal MaximumKrwPerKg { get; init; }

    public int SampleCount { get; init; }
}
