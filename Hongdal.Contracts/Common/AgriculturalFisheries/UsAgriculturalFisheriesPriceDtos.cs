namespace Hongdal.Contracts.Common.AgriculturalFisheries;

public static class 미국농수산가격출처Keys
{
    public const string UsdaNassQuickStats = "usda-nass-quickstats";
}

public static class 미국농수산가격조회상태Codes
{
    public const string 완료 = "Complete";

    public const string 자료없음 = "NoData";

    public const string 설정안됨 = "NotConfigured";

    public const string 잘못된요청 = "InvalidRequest";

    public const string 지원하지않는출처 = "UnsupportedSource";

    public const string 자료조회불가 = "DataUnavailable";
}

public sealed class 미국농수산가격조회요청
{
    public string SourceKey { get; init; } = 미국농수산가격출처Keys.UsdaNassQuickStats;

    public string Commodity { get; init; } = string.Empty;

    public string StatisticCategory { get; init; } = "PRICE RECEIVED";

    public string Program { get; init; } = "SURVEY";

    public string? Sector { get; init; }

    public string? Group { get; init; }

    public string AggregationLevel { get; init; } = "NATIONAL";

    public string? StateAlpha { get; init; }

    public string Domain { get; init; } = "TOTAL";

    public string? Frequency { get; init; }

    public int YearFrom { get; init; }

    public int? YearTo { get; init; }

    public int MaxItems { get; init; } = 100;
}

public sealed class 미국농수산가격조회응답
{
    public bool Success { get; init; }

    public string StatusCode { get; init; } = 미국농수산가격조회상태Codes.자료조회불가;

    public string? ErrorMessage { get; init; }

    public string SourceKey { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string DocumentationUrl { get; init; } = string.Empty;

    public 미국농수산가격조회요청 Query { get; init; } = new();

    public IReadOnlyList<미국농수산가격항목> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public bool IsTruncated { get; init; }

    public DateTime CollectedAtUtc { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Notices { get; init; } = [];

    public bool InformationOnly { get; init; } = true;
}

public sealed class 미국농수산가격항목
{
    public string Commodity { get; init; } = string.Empty;

    public string Class { get; init; } = string.Empty;

    public string ShortDescription { get; init; } = string.Empty;

    public string Sector { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string StatisticCategory { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public string RawValue { get; init; } = string.Empty;

    public decimal? NumericValue { get; init; }

    public bool IsSuppressed { get; init; }

    public string Program { get; init; } = string.Empty;

    public string AggregationLevel { get; init; } = string.Empty;

    public string StateAlpha { get; init; } = string.Empty;

    public string StateName { get; init; } = string.Empty;

    public string Year { get; init; } = string.Empty;

    public string Frequency { get; init; } = string.Empty;

    public string ReferencePeriod { get; init; } = string.Empty;

    public string LoadTime { get; init; } = string.Empty;
}
