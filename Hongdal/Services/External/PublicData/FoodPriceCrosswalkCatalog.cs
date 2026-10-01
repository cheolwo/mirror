using System.Text.RegularExpressions;

namespace 홍달.Services.External.PublicData;

public interface IFoodPriceCrosswalkCatalog
{
    IReadOnlyList<FoodPriceCrosswalk> GetAll();

    FoodPriceCrosswalk? Find(string? hsCode);
}

public sealed record FoodPriceCrosswalk(
    string HsPrefix,
    string ProductName,
    string AtCategoryCode,
    string AtItemCode,
    string AtItemName,
    IReadOnlyList<string> AtVarietyCodes,
    string MatchQualityCode,
    string MatchQualityLabel,
    string DomesticOriginStatusCode,
    string DomesticOriginStatusLabel,
    string Note,
    IReadOnlyList<string> ExcludedNameTokens);

public sealed class FoodPriceCrosswalkCatalog : IFoodPriceCrosswalkCatalog
{
    private static readonly string[] ImportedNameTokens =
    [
        "수입",
        "미국산",
        "호주산",
        "뉴질랜드",
        "중국",
        "칠레",
        "페루",
        "인도"
    ];

    private static readonly IReadOnlyList<FoodPriceCrosswalk> Entries =
    [
        Exact("1006", "쌀", "100", "111", "쌀", [], DomesticMarket(), "쌀 품목의 국내 유통가격을 사용합니다."),
        Exact("071333", "흰콩", "100", "141", "콩", ["01"], DomesticVariant(), "국산 흰콩 품종만 사용합니다."),
        Exact("071420", "고구마", "100", "151", "고구마", ["00"], DomesticMarket(), "밤고구마 대표가격을 사용합니다."),
        Exact("0701", "감자", "100", "152", "감자", [], DomesticMarket(), "감자의 국내 유통가격을 사용합니다."),
        Exact("070490", "배추", "200", "211", "배추", [], DomesticMarket(), "계절별 배추 품종을 함께 집계합니다."),
        Exact("070410", "브로콜리", "200", "261", "브로콜리", ["01"], DomesticMarket(), "브로콜리 대표가격을 사용합니다."),
        Exact("070970", "시금치", "200", "213", "시금치", ["00"], DomesticMarket(), "시금치 대표가격을 사용합니다."),
        Exact("070511", "상추", "200", "214", "상추", [], DomesticMarket(), "적상추와 청상추 가격을 함께 집계합니다."),
        Exact("070519", "상추", "200", "214", "상추", [], DomesticMarket(), "적상추와 청상추 가격을 함께 집계합니다."),
        Exact("070700", "오이", "200", "223", "오이", [], DomesticMarket(), "오이 품종 가격을 함께 집계합니다."),
        Exact("070993", "호박", "200", "224", "호박", [], DomesticMarket(), "애호박·쥬키니·단호박을 포함한 대표가격입니다."),
        Exact("070200", "토마토", "200", "225", "토마토", ["00"], DomesticMarket(), "토마토 대표가격을 사용합니다."),
        Exact("081010", "딸기", "200", "226", "딸기", ["00"], DomesticMarket(), "딸기 대표가격을 사용합니다."),
        Exact("070310", "양파", "200", "245", "양파", ["00", "02"], DomesticVariant(), "수입 품종을 제외한 양파 가격입니다."),
        Exact("070320", "마늘", "200", "258", "깐마늘(국산)", ["01", "03", "04", "05", "06"], DomesticVariant(), "국산 깐마늘 품종만 사용합니다."),
        Exact("091011", "생강", "200", "247", "생강", ["00"], DomesticVariant(), "국산 생강 품종만 사용합니다."),
        Exact("120740", "참깨", "300", "312", "참깨", ["01"], DomesticVariant(), "국산 참깨 품종만 사용합니다."),
        Exact("120241", "땅콩", "300", "314", "땅콩", ["01"], DomesticVariant(), "국산 땅콩 품종만 사용합니다."),
        Exact("120242", "땅콩", "300", "314", "땅콩", ["01"], DomesticVariant(), "국산 땅콩 품종만 사용합니다."),
        Exact("080810", "사과", "400", "411", "사과", [], DomesticMarket(), "국내 조사 사과 품종을 함께 집계합니다."),
        Exact("080830", "배", "400", "412", "배", [], DomesticMarket(), "국내 조사 배 품종을 함께 집계합니다."),
        Exact("080930", "복숭아", "400", "413", "복숭아", [], DomesticMarket(), "국내 조사 복숭아 품종을 함께 집계합니다."),
        Exact("080610", "포도", "400", "414", "포도", ["01", "02", "03", "06", "12"], DomesticVariant(), "수입 품종을 제외한 국내 조사 포도 가격입니다."),
        Exact("080521", "감귤", "400", "415", "감귤", [], DomesticMarket(), "감귤 대표가격을 사용합니다."),
        Exact("081070", "단감", "400", "416", "단감", ["00"], DomesticMarket(), "단감 대표가격을 사용합니다."),
        Exact("081050", "참다래", "400", "419", "참다래", ["01"], DomesticVariant(), "국산 참다래 품종만 사용합니다."),
        Representative("0201", "쇠고기", "500", "512", "쇠고기", ["11", "12", "13", "14", "15", "16"], DomesticVariant(), "한우 부위 가격을 국내 대표가격으로 사용합니다."),
        Representative("0202", "쇠고기", "500", "512", "쇠고기", ["11", "12", "13", "14", "15", "16"], DomesticVariant(), "한우 부위 가격을 국내 대표가격으로 사용합니다."),
        Representative("0203", "돼지고기", "500", "514", "돼지고기", ["00"], DomesticVariant(), "국산 냉장 삼겹살 가격을 국내 대표가격으로 사용합니다."),
        Representative("020711", "닭고기", "500", "515", "닭고기", ["02"], DomesticMarket(), "도계 가격을 닭고기 대표가격으로 사용합니다."),
        Representative("020712", "닭고기", "500", "515", "닭고기", ["02"], DomesticMarket(), "도계 가격을 닭고기 대표가격으로 사용합니다."),
        Representative("020713", "닭고기", "500", "515", "닭고기", ["02"], DomesticMarket(), "도계 가격을 닭고기 대표가격으로 사용합니다."),
        Representative("020714", "닭고기", "500", "515", "닭고기", ["02"], DomesticMarket(), "도계 가격을 닭고기 대표가격으로 사용합니다."),
        Representative("040721", "계란", "500", "516", "계란", ["00", "02", "03", "04", "05", "07"], DomesticVariant(), "수입란을 제외한 국내 조사 계란 가격입니다."),
        Exact("030354", "고등어", "600", "611", "고등어", ["05", "06"], DomesticVariant(), "국산 냉장·냉동 고등어 가격입니다."),
        Exact("030742", "오징어", "600", "619", "물오징어", ["03", "04"], DomesticVariant(), "연근해 오징어 가격입니다."),
        Exact("030743", "오징어", "600", "619", "물오징어", ["03", "04"], DomesticVariant(), "연근해 오징어 가격입니다."),
        Exact("030711", "굴", "600", "644", "굴", ["00"], DomesticMarket(), "굴 대표가격을 사용합니다."),
        Exact("030712", "굴", "600", "644", "굴", ["00"], DomesticMarket(), "굴 대표가격을 사용합니다."),
        Exact("030731", "홍합", "600", "658", "홍합", ["01", "02"], DomesticMarket(), "냉장 홍합 대표가격을 사용합니다."),
        Exact("030732", "홍합", "600", "658", "홍합", ["01", "02"], DomesticMarket(), "냉장 홍합 대표가격을 사용합니다."),
        Exact("030781", "전복", "600", "653", "전복", ["00"], DomesticMarket(), "전복 대표가격을 사용합니다."),
        Exact("030782", "전복", "600", "653", "전복", ["00"], DomesticMarket(), "전복 대표가격을 사용합니다."),
        Exact("030721", "가리비", "600", "659", "가리비", ["01"], DomesticMarket(), "홍가리비 대표가격을 사용합니다."),
        Exact("030722", "가리비", "600", "659", "가리비", ["01"], DomesticMarket(), "홍가리비 대표가격을 사용합니다."),
        Representative("030614", "꽃게", "600", "656", "꽃게", [], DomesticMarket(), "꽃게 냉장·냉동 가격을 함께 집계합니다."),
        Representative("030633", "꽃게", "600", "656", "꽃게", [], DomesticMarket(), "꽃게 냉장·냉동 가격을 함께 집계합니다.")
    ];

    public IReadOnlyList<FoodPriceCrosswalk> GetAll()
        => Entries;

    public FoodPriceCrosswalk? Find(string? hsCode)
    {
        var normalized = Regex.Replace(hsCode ?? string.Empty, "[^0-9]", string.Empty);
        if (normalized.Length < 4)
        {
            return null;
        }

        return Entries
            .Where(entry => normalized.StartsWith(entry.HsPrefix, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.HsPrefix.Length)
            .FirstOrDefault();
    }

    private static FoodPriceCrosswalk Exact(
        string hsPrefix,
        string productName,
        string categoryCode,
        string itemCode,
        string itemName,
        IReadOnlyList<string> varietyCodes,
        OriginStatus origin,
        string note)
        => Create(
            hsPrefix,
            productName,
            categoryCode,
            itemCode,
            itemName,
            varietyCodes,
            "ExactCommodity",
            "동일 품목",
            origin,
            note);

    private static FoodPriceCrosswalk Representative(
        string hsPrefix,
        string productName,
        string categoryCode,
        string itemCode,
        string itemName,
        IReadOnlyList<string> varietyCodes,
        OriginStatus origin,
        string note)
        => Create(
            hsPrefix,
            productName,
            categoryCode,
            itemCode,
            itemName,
            varietyCodes,
            "Representative",
            "대표 품목",
            origin,
            note);

    private static FoodPriceCrosswalk Create(
        string hsPrefix,
        string productName,
        string categoryCode,
        string itemCode,
        string itemName,
        IReadOnlyList<string> varietyCodes,
        string matchQualityCode,
        string matchQualityLabel,
        OriginStatus origin,
        string note)
        => new(
            hsPrefix,
            productName,
            categoryCode,
            itemCode,
            itemName,
            varietyCodes,
            matchQualityCode,
            matchQualityLabel,
            origin.Code,
            origin.Label,
            note,
            ImportedNameTokens);

    private static OriginStatus DomesticVariant()
        => new("DomesticVariant", "국산 품종 확인");

    private static OriginStatus DomesticMarket()
        => new("DomesticMarket", "국내 시장 조사값");

    private sealed record OriginStatus(string Code, string Label);
}
