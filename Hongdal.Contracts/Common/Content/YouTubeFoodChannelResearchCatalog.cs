namespace Hongdal.Contracts.Common.Content;

public static class YouTube음식채널분류코드
{
    public const string 상품리뷰 = "ProductReview";
    public const string 요리재료 = "CookingIngredient";
    public const string 음식여행 = "FoodTravel";
    public const string 길거리음식 = "StreetFood";
    public const string 먹방 = "Mukbang";
    public const string 육류수산 = "MeatSeafood";
    public const string 세계음식 = "GlobalCuisine";
    public const string 식품산업 = "FoodIndustry";

    public static IReadOnlySet<string> 전체 { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        상품리뷰,
        요리재료,
        음식여행,
        길거리음식,
        먹방,
        육류수산,
        세계음식,
        식품산업
    };
}

public sealed record YouTube음식채널조사항목(
    string ChannelId,
    string Handle,
    string 채널명,
    string 국가코드,
    string 기본언어코드,
    IReadOnlyList<string> 분류코드목록,
    int 구매발견점수,
    int 수입발견점수,
    string 조사메모,
    string 공식채널Url,
    DateTime 조사확인일시Utc);

/// <summary>
/// 음식 상품 발견 가능성을 기준으로 1차 선별한 운영 시작용 카탈로그입니다.
/// 전체 YouTube 채널의 완전한 목록이 아니며, API 검색과 관리자 검수로 계속 확장합니다.
/// 구독자 수처럼 자주 변하는 수치는 의도적으로 저장하지 않습니다.
/// </summary>
public static class YouTube음식채널조사Catalog
{
    private static readonly DateTime 확인일시Utc =
        new(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<YouTube음식채널조사항목> 항목 { get; } =
    [
        C("UCfpaSruWW3S4dibonKXENjA", "@tzuyang6145", "tzuyang쯔양", YouTube채널수집국가코드.한국, "ko",
            [YouTube음식채널분류코드.먹방, YouTube음식채널분류코드.상품리뷰], 88, 45,
            "대용량 먹방과 식당·메뉴 탐방에서 국내 구매 수요를 발견하기 좋은 채널"),
        C("UCyn-K7rZLXjGl7VXGweIlcA", "@paik_jongwon", "백종원 PAIK JONG WON", "KR", "ko",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.식품산업], 92, 68,
            "조리법, 식재료, 외식업 정보를 함께 다뤄 상품화 후보를 찾기 좋은 채널"),
        C("UCl23-Cci_SMqyGXE1T_LYUg", "@sungsikyung", "성시경 SUNG SI KYUNG", "KR", "ko",
            [YouTube음식채널분류코드.음식여행, YouTube음식채널분류코드.요리재료], 82, 48,
            "국내외 식당과 조리 콘텐츠에서 메뉴·재료 수요를 관찰할 수 있는 채널"),
        C("UC0VR2v4TZeGcOrZHnmwbU_Q", "@yooxicman", "육식맨 YOOXICMAN", "KR", "ko",
            [YouTube음식채널분류코드.육류수산, YouTube음식채널분류코드.요리재료], 94, 88,
            "해외 육류·조리도구·소스 소개가 많아 구매와 수입 검토 연결성이 높은 채널"),
        C("UC1oXmhvYHVI2bApphh3IzuQ", "@meatcreator", "정육왕 MeatCreator", "KR", "ko",
            [YouTube음식채널분류코드.육류수산, YouTube음식채널분류코드.식품산업], 91, 82,
            "정육·육류 산지와 유통 정보를 다뤄 공급처 및 수입 후보 조사에 적합한 채널"),
        C("UCg-p3lQIqmhh7gHpyaOmOiQ", "@koreanenglishman", "영국남자 Korean Englishman", "KR", "ko",
            [YouTube음식채널분류코드.세계음식, YouTube음식채널분류코드.상품리뷰], 86, 70,
            "한국과 해외 식품의 교차 체험을 통해 역직구·수입 관심을 확인하기 좋은 채널"),
        C("UC8gFadPgK2r1ndqLI04Xvvw", "@maangchi", "Maangchi", "US", "en",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.세계음식], 90, 84,
            "영어권 한국 식재료 수요와 해외 유통 가능성을 살펴보기 좋은 채널"),
        C("UC5qRAYQmCLx8hFGIiTWSQvA", "@aaronandclaire", "Aaron and Claire", "US", "en",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.세계음식], 88, 82,
            "쉽게 구하는 한국 식재료와 대체재를 설명해 해외 구매 수요 탐색에 적합한 채널"),
        C("UCh8gHdtzO2tXd593_bjErWg", "@doobydobap", "Doobydobap", "US", "en",
            [YouTube음식채널분류코드.세계음식, YouTube음식채널분류코드.음식여행], 82, 72,
            "한식과 세계 음식 스토리에서 재료·식문화 기반 상품 후보를 찾을 수 있는 채널"),
        C("UCIvA9ZGeoR6CH2e0DZtvxzw", "@seonkyounglongest", "Seonkyoung Longest", "US", "en",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.세계음식], 87, 82,
            "아시아 조리법과 식재료 비중이 높아 해외 상품·소스 수요 탐색에 적합한 채널"),
        C("UC-SmuIHMG2HPDFUOBRu5LnA", "@futureneighbor", "Future Neighbor", "KR", "en",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.세계음식], 84, 78,
            "영어권 시청자를 위한 한국 장보기·조리 정보에서 수출입 후보를 찾기 좋은 채널"),
        C("UCdn6TcAo99RtH-FwIgKBXUA", "@thekoreanvegan", "The Korean Vegan", "US", "en",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.세계음식], 80, 75,
            "비건 한식 재료와 대체 식품의 틈새 구매 수요를 조사하기 좋은 채널"),
        C("UCe1ZsGtbRx2pgajGzwB9L0A", "@foodieboykr", "FoodieBoy 푸디보이", "KR", "ko",
            [YouTube음식채널분류코드.길거리음식, YouTube음식채널분류코드.식품산업], 83, 46,
            "조리 현장과 길거리 음식 영상에서 완제품·장비·공급자 후보를 찾기 좋은 채널"),
        C("UCiiV8stcewhoCNyiErr7GtA", "@yummyboys", "야미보이 Yummyboy", "KR", "ko",
            [YouTube음식채널분류코드.길거리음식, YouTube음식채널분류코드.식품산업], 82, 45,
            "대량 조리와 시장 음식 현장을 통해 식품·장비 구매 후보를 찾을 수 있는 채널"),
        C("UC1r112Pr9Ngcg2NtcE946HQ", "@matsangmu", "맛상무", "KR", "ko",
            [YouTube음식채널분류코드.상품리뷰, YouTube음식채널분류코드.먹방], 90, 66,
            "신제품과 간편식 비교·리뷰가 많아 구매 의향 수집에 직접 연결하기 좋은 채널"),
        C("UC-Bsa2ivAGWq7bsSPrPGFVA", "@short_mouth_sun", "입짧은햇님", "KR", "ko",
            [YouTube음식채널분류코드.먹방, YouTube음식채널분류코드.상품리뷰], 84, 43,
            "배달·간편식·외식 메뉴 반응을 통해 국내 공동구매 관심을 관찰하기 좋은 채널"),
        C("UCyEd6QBSgat5kkC6svyjudA", "@markwiens", "Mark Wiens", "US", "en",
            [YouTube음식채널분류코드.음식여행, YouTube음식채널분류코드.세계음식], 85, 91,
            "세계 각지 음식과 현지 식재료 노출이 많아 수입 조사 후보가 풍부한 채널"),
        C("UCcAd5Np7fO8SeejB1FVKcYw", "@besteverfoodreviewshow", "Best Ever Food Review Show", "US", "en",
            [YouTube음식채널분류코드.음식여행, YouTube음식채널분류코드.세계음식], 84, 92,
            "여러 국가의 희소 식품과 생산 문화를 다뤄 수입 가능성 조사에 적합한 채널"),
        C("UCXOKEdfOFxsHO_-Su3K8SHg", "@strictlydumpling", "Strictly Dumpling", "US", "en",
            [YouTube음식채널분류코드.음식여행, YouTube음식채널분류코드.상품리뷰], 86, 86,
            "여행 음식과 식품 구매 리뷰를 함께 다뤄 제품 단위 후보 발굴에 적합한 채널"),
        C("UCRFj4Yj1nKhgrT-_8AUsaDg", "@dancingbacons", "DancingBacons", "SG", "en",
            [YouTube음식채널분류코드.길거리음식, YouTube음식채널분류코드.상품리뷰], 84, 88,
            "아시아 편의점·자판기·시장 식품 노출이 많아 수입 완제품 후보가 풍부한 채널"),
        C("UCiAq_SU0ED1C6vWFMnw8Ekg", "@thefoodranger", "The Food Ranger", "AE", "en",
            [YouTube음식채널분류코드.음식여행, YouTube음식채널분류코드.길거리음식], 82, 89,
            "지역별 시장 음식과 식재료를 폭넓게 다뤄 해외 공급 후보 조사에 적합한 채널"),
        C("UCbPHHOiOY_tA9BSytK0jDYw", "@berylshereshewsky", "Beryl Shereshewsky", "US", "en",
            [YouTube음식채널분류코드.세계음식, YouTube음식채널분류코드.요리재료], 88, 94,
            "각국 시청자와 음식을 교류해 국가별 식품·재료 수요를 발견하기 좋은 채널"),
        C("UCzqbfYjQmf9nLQPMxVgPhiA", "@emmymade", "emmymade", "US", "en",
            [YouTube음식채널분류코드.상품리뷰, YouTube음식채널분류코드.세계음식], 90, 90,
            "세계 간식·특이 식품 리뷰가 많아 완제품 구매와 수입 후보 발굴에 적합한 채널"),
        C("UC54SLBnD5k5U3Q6N__UjbAw", "@chinesecookingdemystified", "Chinese Cooking Demystified", "CN", "en",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.세계음식], 86, 91,
            "중국 조리법과 전문 식재료를 자세히 설명해 식재료 수입 검토에 적합한 채널"),
        C("UCWj_rUMGYCP0DjffybE2nbg", "@middleeats", "Middle Eats", "GB", "en",
            [YouTube음식채널분류코드.요리재료, YouTube음식채널분류코드.세계음식], 84, 89,
            "중동 조리법과 향신료·가공품 정보를 통해 틈새 수입 후보를 찾기 좋은 채널"),
        C("UCfyehHM_eo4g5JUyWmms2LA", "@sortedfood", "Sorted Food", "GB", "en",
            [YouTube음식채널분류코드.상품리뷰, YouTube음식채널분류코드.요리재료], 90, 80,
            "식품·주방용품 비교와 조리 실험이 많아 구매 전환 후보를 찾기 좋은 채널"),
        C("UCwiTOchWeKjrJZw7S1H__1g", "@insiderfood", "Insider Food", "US", "en",
            [YouTube음식채널분류코드.식품산업, YouTube음식채널분류코드.음식여행], 82, 84,
            "식품 생산·가격·산업 배경을 다뤄 공급망과 상품성 검토에 도움이 되는 채널"),
        C("UCRzPUBhXUZHclB7B5bURFXw", "@eater", "Eater", "US", "en",
            [YouTube음식채널분류코드.식품산업, YouTube음식채널분류코드.음식여행], 80, 82,
            "외식 산업과 생산 현장을 다뤄 식품·공급자 후보 발굴에 도움이 되는 채널"),
        C("UC6Je3-ZV_x38NqQAxKiCCyQ", "@tabieats", "TabiEats", "JP", "en",
            [YouTube음식채널분류코드.상품리뷰, YouTube음식채널분류코드.음식여행], 88, 92,
            "일본 편의점·지역 식품 리뷰가 많아 국내 구매대행·수입 후보 발굴에 적합한 채널")
    ];

    public static YouTube음식채널조사항목? 찾기(string channelId)
        => 항목.FirstOrDefault(item => string.Equals(item.ChannelId, channelId, StringComparison.Ordinal));

    private static YouTube음식채널조사항목 C(
        string channelId,
        string handle,
        string 채널명,
        string 국가코드,
        string 기본언어코드,
        IReadOnlyList<string> 분류코드목록,
        int 구매발견점수,
        int 수입발견점수,
        string 조사메모)
        => new(
            channelId,
            handle,
            채널명,
            YouTube채널수집국가코드.정규화(국가코드),
            기본언어코드,
            분류코드목록,
            구매발견점수,
            수입발견점수,
            조사메모,
            $"https://www.youtube.com/{handle}",
            확인일시Utc);
}
