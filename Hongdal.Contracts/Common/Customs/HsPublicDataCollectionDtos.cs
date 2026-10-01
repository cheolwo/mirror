namespace Hongdal.Contracts.Common.Customs;

public static class Hs공공데이터출처Keys
{
    public const string 수입평균단가 = "customs-import-unit-price";

    public const string 세관장확인대상물품 = "customs-confirmation-requirements";

    public const string 관세환율 = "customs-weekly-exchange-rate";

    public static IReadOnlyList<string> 전체 { get; } =
    [
        수입평균단가,
        세관장확인대상물품,
        관세환율
    ];
}

public static class Hs공공데이터수집상태Codes
{
    public const string 성공 = "success";

    public const string 데이터없음 = "no_data";

    public const string 설정안됨 = "not_configured";

    public const string 적용안됨 = "not_applicable";

    public const string 지원안됨 = "unsupported";

    public const string 오류 = "error";
}

public sealed class Hs공공데이터수집요청
{
    public string HsCode { get; init; } = string.Empty;

    public string CountryCode { get; init; } = "CN";

    public string ReferenceMonth { get; init; } = string.Empty;

    public int LookbackMonths { get; init; } = 3;

    public string ReferenceDate { get; init; } = string.Empty;

    public decimal? ExpectedFxRateKrwPerUsd { get; init; }

    public IReadOnlyList<string> SourceKeys { get; init; } = [];
}

public sealed class Hs공공데이터묶음응답
{
    public string HsCode { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;

    public string ReferenceMonth { get; init; } = string.Empty;

    public string ReferenceDate { get; init; } = string.Empty;

    public DateTime CollectedAtUtc { get; init; }

    public int SuccessSourceCount { get; init; }

    public bool RequiresProfessionalReview { get; init; }

    public IReadOnlyList<Hs공공데이터출처응답> Sources { get; init; } = [];

    public string Notice { get; init; } =
        "공공데이터를 정리한 참고 정보이며 품목분류, 수입요건 충족 또는 통관 가능성을 확정하지 않습니다.";
}

public sealed class Hs공공데이터출처응답
{
    public string SourceKey { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string StatusCode { get; init; } = Hs공공데이터수집상태Codes.오류;

    public string Summary { get; init; } = string.Empty;

    public string DocumentationUrl { get; init; } = string.Empty;

    public DateTime CollectedAtUtc { get; init; }

    public IReadOnlyList<Hs공공데이터정보항목> Items { get; init; } = [];
}

public sealed class Hs공공데이터정보항목
{
    public string ItemKey { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public bool AttentionRequired { get; init; }

    public IReadOnlyDictionary<string, string?> Fields { get; init; } =
        new Dictionary<string, string?>();
}
