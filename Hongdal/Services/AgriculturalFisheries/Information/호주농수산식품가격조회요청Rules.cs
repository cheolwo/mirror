using System.Globalization;
using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static class 호주농수산식품가격조회요청Rules
{
    private const int MaximumMonthRange = 120;

    public static 호주농수산식품가격조회요청 Normalize(호주농수산식품가격조회요청 request)
    {
        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var defaultStart = currentMonth.AddMonths(-23);
        return new 호주농수산식품가격조회요청
        {
            SourceKey = string.IsNullOrWhiteSpace(request.SourceKey)
                ? 호주농수산식품가격출처Keys.AbsConsumerPriceIndex
                : request.SourceKey.Trim().ToLowerInvariant(),
            IndexCode = NormalizeCode(
                request.IndexCode,
                호주식품가격지수Codes.FoodAndNonAlcoholicBeverages),
            MeasureCode = NormalizeCode(
                request.MeasureCode,
                호주식품가격지수측정Codes.IndexNumber),
            RegionCode = NormalizeCode(
                request.RegionCode,
                호주식품가격지수지역Codes.Australia),
            StartPeriod = NormalizePeriod(request.StartPeriod, defaultStart),
            EndPeriod = NormalizePeriod(request.EndPeriod, currentMonth),
            MaxItems = Math.Clamp(request.MaxItems <= 0 ? 60 : request.MaxItems, 1, MaximumMonthRange)
        };
    }

    public static string? Validate(호주농수산식품가격조회요청 request)
    {
        if (!호주농수산식품가격Catalog.SupportsIndex(request.IndexCode))
        {
            return "지원하는 호주 식품 가격지수 코드를 확인해 주세요.";
        }

        if (!호주농수산식품가격Catalog.SupportsMeasure(request.MeasureCode))
        {
            return "지원하는 호주 식품 가격지수 측정 코드를 확인해 주세요.";
        }

        if (!호주농수산식품가격Catalog.SupportsRegion(request.RegionCode))
        {
            return "지원하는 호주 CPI 지역 코드를 확인해 주세요.";
        }

        if (!TryParsePeriod(request.StartPeriod, out var start)
            || !TryParsePeriod(request.EndPeriod, out var end))
        {
            return "조회 기간은 yyyy-MM 형식이어야 합니다.";
        }

        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        if (start > end || end > currentMonth)
        {
            return "호주 가격지수 조회 기간을 확인해 주세요.";
        }

        var monthCount = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        return monthCount > MaximumMonthRange
            ? $"한 번에 조회할 수 있는 기간은 최대 {MaximumMonthRange}개월입니다."
            : null;
    }

    private static string NormalizeCode(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizePeriod(string? value, DateTime fallback)
        => string.IsNullOrWhiteSpace(value)
            ? fallback.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : value.Trim();

    private static bool TryParsePeriod(string value, out DateTime period)
        => DateTime.TryParseExact(
            value,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out period);
}
