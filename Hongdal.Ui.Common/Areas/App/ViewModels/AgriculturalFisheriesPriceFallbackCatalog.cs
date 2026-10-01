using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

internal static class 농수산가격기본Catalog
{
    public static IReadOnlyList<AgriculturalFisheriesItemResponse> 국내품목 { get; } =
    [
        DomesticItem("1006", "쌀", "식량작물"),
        DomesticItem("0701", "감자", "식량작물"),
        DomesticItem("081010", "딸기", "채소류"),
        DomesticItem("080810", "사과", "과일류"),
        DomesticItem("0201", "쇠고기", "축산물"),
        DomesticItem("030354", "고등어", "수산물"),
        DomesticItem("030711", "굴", "수산물")
    ];

    public static IReadOnlyList<string> 미국품목예시 { get; } =
        ["APPLES", "POTATOES", "RICE", "STRAWBERRIES", "CATFISH", "TROUT"];

    public static 호주농수산식품가격Catalog응답 호주Catalog { get; } = new()
    {
        Indexes =
        [
            AustraliaOption(호주식품가격지수Codes.FoodAndNonAlcoholicBeverages, "식품 및 비알코올 음료"),
            AustraliaOption(호주식품가격지수Codes.BreadAndCerealProducts, "빵·곡물 제품"),
            AustraliaOption(호주식품가격지수Codes.MeatAndSeafoods, "육류·수산물"),
            AustraliaOption(호주식품가격지수Codes.BeefAndVeal, "쇠고기·송아지고기"),
            AustraliaOption(호주식품가격지수Codes.FishAndOtherSeafood, "어류·기타 수산물"),
            AustraliaOption(호주식품가격지수Codes.DairyAndRelatedProducts, "유제품"),
            AustraliaOption(호주식품가격지수Codes.Fruit, "과일"),
            AustraliaOption(호주식품가격지수Codes.Vegetables, "채소")
        ],
        Measures =
        [
            AustraliaOption(호주식품가격지수측정Codes.IndexNumber, "가격지수"),
            AustraliaOption(호주식품가격지수측정Codes.PreviousPeriodPercentageChange, "전월 대비 변동률"),
            AustraliaOption(호주식품가격지수측정Codes.PreviousYearPercentageChange, "전년 동월 대비 변동률")
        ],
        Regions =
        [
            AustraliaOption(호주식품가격지수지역Codes.Australia, "호주 8개 주도시 가중평균"),
            AustraliaOption(호주식품가격지수지역Codes.Sydney, "시드니"),
            AustraliaOption(호주식품가격지수지역Codes.Melbourne, "멜버른"),
            AustraliaOption(호주식품가격지수지역Codes.Brisbane, "브리즈번"),
            AustraliaOption(호주식품가격지수지역Codes.Perth, "퍼스")
        ]
    };

    public static IReadOnlyList<AgriculturalFisheriesDataSourceResponse> 출처 { get; } =
    [
        new()
        {
            Key = "at-daily-wholesale-retail-food-price",
            Provider = "한국농수산식품유통공사(aT)",
            DisplayName = "국내 도·소매 가격정보",
            Coverage = "한국 농축수산물 중도매·소매 조사 가격",
            StatusCode = "Unknown",
            StatusLabel = "서버 확인 전",
            UsageNote = "품질·등급·산지·포장 차이를 함께 확인합니다."
        },
        new()
        {
            Key = 미국농수산가격출처Keys.UsdaNassQuickStats,
            Provider = "미국 농무부 농업통계청(USDA NASS)",
            DisplayName = "Quick Stats 가격·판매 통계",
            Coverage = "미국 농작물·축산물·양식 수산물 집계 통계",
            StatusCode = "Unknown",
            StatusLabel = "서버 확인 전",
            UsageNote = "미국 공식 품목명과 원문 단위를 유지합니다."
        },
        new()
        {
            Key = 호주농수산식품가격출처Keys.AbsConsumerPriceIndex,
            Provider = "Australian Bureau of Statistics (ABS)",
            DisplayName = "Consumer Price Index 식품 가격지수",
            Coverage = "호주 식품·육류·수산물·유제품·과일·채소 소비자 가격 변동",
            StatusCode = "Unknown",
            StatusLabel = "서버 확인 전",
            UsageNote = "실제 A$/kg 단가가 아닌 기준시점 대비 가격지수입니다."
        }
    ];

    private static AgriculturalFisheriesItemResponse DomesticItem(
        string hsPrefix,
        string productName,
        string categoryLabel)
        => new()
        {
            HsPrefix = hsPrefix,
            ProductName = productName,
            CategoryLabel = categoryLabel
        };

    private static 호주식품가격지수선택항목 AustraliaOption(string code, string label)
        => new()
        {
            Code = code,
            Label = label,
            OfficialLabel = label
        };
}
