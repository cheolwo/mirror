namespace Hongdal.Contracts.Common.AgriculturalFisheries;

public static class 호주농수산식품가격출처Keys
{
    public const string AbsConsumerPriceIndex = "abs-cpi-food-price-index";
    public const string AbaresWeeklyAgriculturalPrices = "abares-weekly-australian-agricultural-prices";
    public const string AbaresWeeklyHorticulturePrices = "abares-weekly-australian-horticulture-prices";
    public const string AbaresFisheriesAquacultureStatistics = "abares-fisheries-aquaculture-statistics";
    public const string AbaresFarmDataPortal = "abares-farm-data-portal";

    public static IReadOnlyList<string> All { get; } =
    [
        AbsConsumerPriceIndex,
        AbaresWeeklyAgriculturalPrices,
        AbaresWeeklyHorticulturePrices,
        AbaresFisheriesAquacultureStatistics,
        AbaresFarmDataPortal
    ];
}

public static class 호주농수산식품가격조회상태Codes
{
    public const string 완료 = "Complete";
    public const string 자료없음 = "NoData";
    public const string 잘못된요청 = "InvalidRequest";
    public const string 지원하지않는출처 = "UnsupportedSource";
    public const string 자료조회불가 = "DataUnavailable";
}

public static class 호주식품가격지수Codes
{
    public const string FoodAndNonAlcoholicBeverages = "20001";
    public const string BreadAndCerealProducts = "30002";
    public const string MeatAndSeafoods = "30003";
    public const string BeefAndVeal = "40009";
    public const string Pork = "131178";
    public const string LambAndGoat = "40010";
    public const string Poultry = "40012";
    public const string FishAndOtherSeafood = "40015";
    public const string DairyAndRelatedProducts = "30001";
    public const string Milk = "40001";
    public const string Cheese = "40002";
    public const string FruitAndVegetables = "114120";
    public const string Fruit = "114121";
    public const string Vegetables = "114122";
    public const string Eggs = "40027";
    public const string OilsAndFats = "97550";

    public static IReadOnlyList<string> All { get; } =
    [
        FoodAndNonAlcoholicBeverages,
        BreadAndCerealProducts,
        MeatAndSeafoods,
        BeefAndVeal,
        Pork,
        LambAndGoat,
        Poultry,
        FishAndOtherSeafood,
        DairyAndRelatedProducts,
        Milk,
        Cheese,
        FruitAndVegetables,
        Fruit,
        Vegetables,
        Eggs,
        OilsAndFats
    ];
}

public static class 호주식품가격지수측정Codes
{
    public const string IndexNumber = "1";
    public const string PreviousPeriodPercentageChange = "2";
    public const string PreviousYearPercentageChange = "3";

    public static IReadOnlyList<string> All { get; } =
    [
        IndexNumber,
        PreviousPeriodPercentageChange,
        PreviousYearPercentageChange
    ];
}

public static class 호주식품가격지수지역Codes
{
    public const string Australia = "50";
    public const string Sydney = "1";
    public const string Melbourne = "2";
    public const string Brisbane = "3";
    public const string Adelaide = "4";
    public const string Perth = "5";
    public const string Hobart = "6";
    public const string Darwin = "7";
    public const string Canberra = "8";

    public static IReadOnlyList<string> All { get; } =
    [
        Australia,
        Sydney,
        Melbourne,
        Brisbane,
        Adelaide,
        Perth,
        Hobart,
        Darwin,
        Canberra
    ];
}

public sealed class 호주농수산식품가격조회요청
{
    public string SourceKey { get; init; } = 호주농수산식품가격출처Keys.AbsConsumerPriceIndex;

    public string IndexCode { get; init; } = 호주식품가격지수Codes.FoodAndNonAlcoholicBeverages;

    public string MeasureCode { get; init; } = 호주식품가격지수측정Codes.IndexNumber;

    public string RegionCode { get; init; } = 호주식품가격지수지역Codes.Australia;

    public string StartPeriod { get; init; } = string.Empty;

    public string EndPeriod { get; init; } = string.Empty;

    public int MaxItems { get; init; } = 60;
}

public sealed class 호주농수산식품가격조회응답
{
    public bool Success { get; init; }

    public string StatusCode { get; init; } = 호주농수산식품가격조회상태Codes.자료조회불가;

    public string? ErrorMessage { get; init; }

    public string SourceKey { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string DocumentationUrl { get; init; } = string.Empty;

    public 호주농수산식품가격조회요청 Query { get; init; } = new();

    public IReadOnlyList<호주농수산식품가격항목> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public bool IsTruncated { get; init; }

    public DateTime? SourcePreparedAtUtc { get; init; }

    public DateTime CollectedAtUtc { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Notices { get; init; } = [];

    public bool InformationOnly { get; init; } = true;

    public bool IsActualUnitPrice { get; init; }
}

public sealed class 호주농수산식품가격항목
{
    public string IndexCode { get; init; } = string.Empty;

    public string IndexLabel { get; init; } = string.Empty;

    public string OfficialIndexLabel { get; init; } = string.Empty;

    public string MeasureCode { get; init; } = string.Empty;

    public string MeasureLabel { get; init; } = string.Empty;

    public string RegionCode { get; init; } = string.Empty;

    public string RegionLabel { get; init; } = string.Empty;

    public string ReferencePeriod { get; init; } = string.Empty;

    public string RawValue { get; init; } = string.Empty;

    public decimal? NumericValue { get; init; }

    public string UnitCode { get; init; } = string.Empty;

    public string UnitLabel { get; init; } = string.Empty;

    public string BasePeriod { get; init; } = string.Empty;
}

public sealed class 호주농수산식품가격Catalog응답
{
    public string ReviewedOn { get; init; } = "2026-07-18";

    public IReadOnlyList<호주농수산식품가격원천응답> Sources { get; init; } = [];

    public IReadOnlyList<호주식품가격지수선택항목> Indexes { get; init; } = [];

    public IReadOnlyList<호주식품가격지수선택항목> Measures { get; init; } = [];

    public IReadOnlyList<호주식품가격지수선택항목> Regions { get; init; } = [];

    public bool InformationOnly { get; init; } = true;
}

public sealed class 호주농수산식품가격원천응답
{
    public string Key { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Coverage { get; init; } = string.Empty;

    public string UpdateCycle { get; init; } = string.Empty;

    public string AccessModeCode { get; init; } = string.Empty;

    public string IntegrationStatusCode { get; init; } = string.Empty;

    public bool AutomatedQueryAvailable { get; init; }

    public bool ContainsThirdPartyInputs { get; init; }

    public string DocumentationUrl { get; init; } = string.Empty;

    public string LicenseCode { get; init; } = string.Empty;

    public string Attribution { get; init; } = string.Empty;

    public string UsageNote { get; init; } = string.Empty;
}

public sealed class 호주식품가격지수선택항목
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string OfficialLabel { get; init; } = string.Empty;
}
