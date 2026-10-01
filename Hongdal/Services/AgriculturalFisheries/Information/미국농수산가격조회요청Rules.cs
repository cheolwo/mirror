using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static class 미국농수산가격조회요청Rules
{
    private const int MaximumYearRange = 20;

    public static 미국농수산가격조회요청 Normalize(미국농수산가격조회요청 request)
    {
        var currentYear = DateTime.UtcNow.Year;
        var yearFrom = request.YearFrom > 0 ? request.YearFrom : currentYear - 3;
        var yearTo = request.YearTo ?? currentYear;

        return new 미국농수산가격조회요청
        {
            SourceKey = NormalizeSourceKey(request.SourceKey),
            Commodity = NormalizeRequiredFilter(request.Commodity),
            StatisticCategory = NormalizeRequiredFilter(request.StatisticCategory),
            Program = NormalizeRequiredFilter(request.Program),
            Sector = NormalizeOptionalFilter(request.Sector),
            Group = NormalizeOptionalFilter(request.Group),
            AggregationLevel = NormalizeRequiredFilter(request.AggregationLevel),
            StateAlpha = NormalizeOptionalFilter(request.StateAlpha),
            Domain = NormalizeRequiredFilter(request.Domain),
            Frequency = NormalizeOptionalFilter(request.Frequency),
            YearFrom = yearFrom,
            YearTo = yearTo,
            MaxItems = Math.Clamp(request.MaxItems <= 0 ? 100 : request.MaxItems, 1, 500)
        };
    }

    public static string? Validate(미국농수산가격조회요청 request)
    {
        if (string.IsNullOrWhiteSpace(request.Commodity))
        {
            return "미국 공식 품목명(commodity)을 입력해 주세요.";
        }

        if (request.Commodity.Length > 100)
        {
            return "품목명은 100자 이하여야 합니다.";
        }

        if (request.Program is not ("SURVEY" or "CENSUS"))
        {
            return "program은 SURVEY 또는 CENSUS여야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(request.StatisticCategory)
            || string.IsNullOrWhiteSpace(request.AggregationLevel)
            || string.IsNullOrWhiteSpace(request.Domain))
        {
            return "통계 구분, 집계 수준과 도메인을 확인해 주세요.";
        }

        var currentYear = DateTime.UtcNow.Year;
        var yearTo = request.YearTo ?? currentYear;
        if (request.YearFrom < 1800 || yearTo > currentYear || yearTo < request.YearFrom)
        {
            return "조회 연도 범위를 확인해 주세요.";
        }

        if (yearTo - request.YearFrom + 1 > MaximumYearRange)
        {
            return $"한 번에 조회할 수 있는 기간은 최대 {MaximumYearRange}년입니다.";
        }

        if (request.StateAlpha is { Length: > 0 and not 2 })
        {
            return "미국 주 코드는 영문 2자리여야 합니다.";
        }

        return null;
    }

    private static string NormalizeSourceKey(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? 미국농수산가격출처Keys.UsdaNassQuickStats
            : value.Trim().ToLowerInvariant();

    private static string NormalizeRequiredFilter(string? value)
        => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string? NormalizeOptionalFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
