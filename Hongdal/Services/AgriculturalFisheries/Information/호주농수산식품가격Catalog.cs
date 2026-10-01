using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static class 호주농수산식품가격Catalog
{
    private static readonly IReadOnlyList<호주식품가격지수선택항목> Indexes =
    [
        Option(호주식품가격지수Codes.FoodAndNonAlcoholicBeverages, "식품 및 비알코올 음료", "Food and non-alcoholic beverages"),
        Option(호주식품가격지수Codes.BreadAndCerealProducts, "빵·곡물 제품", "Bread and cereal products"),
        Option(호주식품가격지수Codes.MeatAndSeafoods, "육류·수산물", "Meat and seafoods"),
        Option(호주식품가격지수Codes.BeefAndVeal, "쇠고기·송아지고기", "Beef and veal"),
        Option(호주식품가격지수Codes.Pork, "돼지고기", "Pork"),
        Option(호주식품가격지수Codes.LambAndGoat, "양고기·염소고기", "Lamb and goat"),
        Option(호주식품가격지수Codes.Poultry, "가금류", "Poultry"),
        Option(호주식품가격지수Codes.FishAndOtherSeafood, "어류·기타 수산물", "Fish and other seafood"),
        Option(호주식품가격지수Codes.DairyAndRelatedProducts, "유제품", "Dairy and related products"),
        Option(호주식품가격지수Codes.Milk, "우유", "Milk"),
        Option(호주식품가격지수Codes.Cheese, "치즈", "Cheese"),
        Option(호주식품가격지수Codes.FruitAndVegetables, "과일·채소", "Fruit and vegetables"),
        Option(호주식품가격지수Codes.Fruit, "과일", "Fruit"),
        Option(호주식품가격지수Codes.Vegetables, "채소", "Vegetables"),
        Option(호주식품가격지수Codes.Eggs, "달걀", "Eggs"),
        Option(호주식품가격지수Codes.OilsAndFats, "유지류", "Oils and fats")
    ];

    private static readonly IReadOnlyList<호주식품가격지수선택항목> Measures =
    [
        Option(호주식품가격지수측정Codes.IndexNumber, "가격지수", "Index numbers"),
        Option(호주식품가격지수측정Codes.PreviousPeriodPercentageChange, "전월 대비 변동률", "Percentage change from previous period"),
        Option(호주식품가격지수측정Codes.PreviousYearPercentageChange, "전년 동월 대비 변동률", "Percentage change from previous year")
    ];

    private static readonly IReadOnlyList<호주식품가격지수선택항목> Regions =
    [
        Option(호주식품가격지수지역Codes.Australia, "호주 8개 주도시 가중평균", "Australia"),
        Option(호주식품가격지수지역Codes.Sydney, "시드니", "Sydney"),
        Option(호주식품가격지수지역Codes.Melbourne, "멜버른", "Melbourne"),
        Option(호주식품가격지수지역Codes.Brisbane, "브리즈번", "Brisbane"),
        Option(호주식품가격지수지역Codes.Adelaide, "애들레이드", "Adelaide"),
        Option(호주식품가격지수지역Codes.Perth, "퍼스", "Perth"),
        Option(호주식품가격지수지역Codes.Hobart, "호바트", "Hobart"),
        Option(호주식품가격지수지역Codes.Darwin, "다윈", "Darwin"),
        Option(호주식품가격지수지역Codes.Canberra, "캔버라", "Canberra")
    ];

    private static readonly IReadOnlyList<호주농수산식품가격원천응답> Sources =
    [
        new()
        {
            Key = 호주농수산식품가격출처Keys.AbsConsumerPriceIndex,
            Provider = "Australian Bureau of Statistics (ABS)",
            DisplayName = "Consumer Price Index 식품 가격지수",
            Coverage = "호주 8개 주도시 가중평균과 각 주도시의 식품·육류·수산물·유제품·과일·채소 소비자 가격 변동",
            UpdateCycle = "월별",
            AccessModeCode = "SdmxApi",
            IntegrationStatusCode = "IntegratedApi",
            AutomatedQueryAvailable = true,
            DocumentationUrl = "https://www.abs.gov.au/statistics/application-programming-interfaces-apis/data-api-user-guide",
            LicenseCode = "CC BY 4.0",
            Attribution = "Based on Australian Bureau of Statistics data",
            UsageNote = "실제 A$/kg 가격이 아니라 기준시점 대비 가격 변동을 나타내는 지수입니다. 도시 간 절대 가격 수준 비교에는 사용할 수 없습니다."
        },
        new()
        {
            Key = 호주농수산식품가격출처Keys.AbaresWeeklyAgriculturalPrices,
            Provider = "Australian Bureau of Agricultural and Resource Economics and Sciences (ABARES)",
            DisplayName = "Australian agricultural prices",
            Coverage = "곡물·유지작물·가축·건초의 주간 국내 가격 요약",
            UpdateCycle = "주별",
            AccessModeCode = "WebReport",
            IntegrationStatusCode = "ReferenceOnly",
            ContainsThirdPartyInputs = true,
            DocumentationUrl = "https://www.agriculture.gov.au/abares/data/weekly-commodity-price-update/australian-agricultural-prices",
            LicenseCode = "Mixed; third-party source terms apply",
            Attribution = "ABARES source page and its data attribution must be retained",
            UsageNote = "일부 원자료가 민간·산업 출처이므로 안정적인 공식 API와 재이용 조건을 확인하기 전 자동 수집하지 않습니다."
        },
        new()
        {
            Key = 호주농수산식품가격출처Keys.AbaresWeeklyHorticulturePrices,
            Provider = "ABARES",
            DisplayName = "Australian horticulture price indicators",
            Coverage = "멜버른 도매시장의 주요 과일·채소 주간 가격 움직임 지표",
            UpdateCycle = "주별",
            AccessModeCode = "WebReport",
            IntegrationStatusCode = "ReferenceOnly",
            ContainsThirdPartyInputs = true,
            DocumentationUrl = "https://www.agriculture.gov.au/abares/data/weekly-commodity-price-update/australian-horticulture-prices",
            LicenseCode = "Mixed; third-party source terms apply",
            Attribution = "ABARES source page and its data attribution must be retained",
            UsageNote = "공개 화면의 지표는 품목 단가가 아니며, 원자료 제공자의 이용조건과 기계 판독 계약을 확인하기 전 스크래핑하지 않습니다."
        },
        new()
        {
            Key = 호주농수산식품가격출처Keys.AbaresFisheriesAquacultureStatistics,
            Provider = "ABARES",
            DisplayName = "Australian fisheries and aquaculture statistics",
            Coverage = "어획·양식 생산량, 생산가치, 무역, 소비와 고용의 연간 통계표",
            UpdateCycle = "연별",
            AccessModeCode = "XlsxDownload",
            IntegrationStatusCode = "DownloadAvailable",
            DocumentationUrl = "https://www.agriculture.gov.au/abares/research-topics/fisheries/fisheries-and-aquaculture-statistics",
            LicenseCode = "CC BY 4.0 except third-party material",
            Attribution = "Based on Australian Bureau of Agricultural and Resource Economics and Sciences data",
            UsageNote = "생산가치를 생산량으로 나눈 값은 소비자가격이 아니며, 원 통계표의 단위·어종·관할·회계연도를 함께 보존해야 합니다."
        },
        new()
        {
            Key = 호주농수산식품가격출처Keys.AbaresFarmDataPortal,
            Provider = "ABARES",
            DisplayName = "Farm Data Portal bulk data",
            Coverage = "광역농업·낙농 경영 조사와 국가·주·지역별 농장 성과 추정치",
            UpdateCycle = "연별 갱신",
            AccessModeCode = "CsvDownload",
            IntegrationStatusCode = "DownloadAvailable",
            DocumentationUrl = "https://www.agriculture.gov.au/abares/data/farm-data-portal",
            LicenseCode = "CC BY 4.0 except third-party material",
            Attribution = "Based on Australian Bureau of Agricultural and Resource Economics and Sciences data",
            UsageNote = "농장 조사 추정치이며 개별 농가의 실시간 판매가격이나 공급 제안으로 해석하지 않습니다."
        }
    ];

    public static 호주농수산식품가격Catalog응답 Build()
        => new()
        {
            Sources = Sources,
            Indexes = Indexes,
            Measures = Measures,
            Regions = Regions
        };

    public static bool SupportsIndex(string code)
        => Indexes.Any(item => string.Equals(item.Code, code, StringComparison.Ordinal));

    public static bool SupportsMeasure(string code)
        => Measures.Any(item => string.Equals(item.Code, code, StringComparison.Ordinal));

    public static bool SupportsRegion(string code)
        => Regions.Any(item => string.Equals(item.Code, code, StringComparison.Ordinal));

    public static string IndexLabel(string code)
        => Find(Indexes, code)?.Label ?? code;

    public static string OfficialIndexLabel(string code)
        => Find(Indexes, code)?.OfficialLabel ?? code;

    public static string MeasureLabel(string code)
        => Find(Measures, code)?.Label ?? code;

    public static string RegionLabel(string code)
        => Find(Regions, code)?.Label ?? code;

    private static 호주식품가격지수선택항목? Find(
        IReadOnlyList<호주식품가격지수선택항목> candidates,
        string code)
        => candidates.FirstOrDefault(item => string.Equals(
            item.Code,
            code,
            StringComparison.Ordinal));

    private static 호주식품가격지수선택항목 Option(
        string code,
        string label,
        string officialLabel)
        => new()
        {
            Code = code,
            Label = label,
            OfficialLabel = officialLabel
        };
}
