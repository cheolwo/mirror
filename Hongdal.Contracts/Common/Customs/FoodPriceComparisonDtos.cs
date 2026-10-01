using Hongdal.Contracts.Common.PublicData;

namespace Hongdal.Contracts.Common.Customs;

public sealed class FoodPriceComparisonRequest
{
    public string HsCode { get; init; } = string.Empty;

    public string CountryCode { get; init; } = "CN";

    public string ReferenceDate { get; init; } = string.Empty;

    public int DomesticLookbackDays { get; init; } = 14;

    public string ReferenceMonth { get; init; } = string.Empty;

    public int ImportLookbackMonths { get; init; } = 3;

    public decimal? FxRateKrwPerUsd { get; init; }

    public decimal? EstimatedImportAdditionalCostKrwPerKg { get; init; }
}

public sealed class FoodPriceComparisonResponse
{
    public bool Success { get; init; }

    public string StatusCode { get; init; } = "Unavailable";

    public string? ErrorMessage { get; init; }

    public string HsCode { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;

    public FoodPriceMatchResponse? Match { get; init; }

    public FoodImportPriceReference? ImportPrice { get; init; }

    public AtDomesticFoodPriceLookupResult? DomesticPrice { get; init; }

    public FoodPriceGapResponse? PrimaryComparison { get; init; }

    public IReadOnlyList<FoodPriceGapResponse> Comparisons { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Notices { get; init; } = [];
}

public sealed class FoodPriceMatchResponse
{
    public string MatchQualityCode { get; init; } = string.Empty;

    public string MatchQualityLabel { get; init; } = string.Empty;

    public string DomesticOriginStatusCode { get; init; } = string.Empty;

    public string DomesticOriginStatusLabel { get; init; } = string.Empty;

    public string AtCategoryCode { get; init; } = string.Empty;

    public string AtItemCode { get; init; } = string.Empty;

    public string AtItemName { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;
}

public sealed class FoodImportPriceReference
{
    public string StartMonth { get; init; } = string.Empty;

    public string EndMonth { get; init; } = string.Empty;

    public decimal TotalImportWeightKg { get; init; }

    public decimal? AverageCifUsdPerKg { get; init; }

    public decimal? FxRateKrwPerUsd { get; init; }

    public decimal? AverageCifKrwPerKg { get; init; }

    public decimal? EstimatedLandedCostKrwPerKg { get; init; }

    public string PriceBasisLabel { get; init; } = "관세청 수입 신고 CIF 통계단가";

    public string DataSource { get; init; } = "관세청 품목별 국가별 수출입실적";
}

public sealed class FoodPriceGapResponse
{
    public string BasisCode { get; init; } = string.Empty;

    public string BasisLabel { get; init; } = string.Empty;

    public decimal DomesticPriceKrwPerKg { get; init; }

    public decimal ImportReferencePriceKrwPerKg { get; init; }

    public decimal DifferenceKrwPerKg { get; init; }

    public decimal DifferenceRate { get; init; }

    public string SignalCode { get; init; } = string.Empty;

    public string SignalLabel { get; init; } = string.Empty;

    public string PlainLanguageSummary { get; init; } = string.Empty;
}
