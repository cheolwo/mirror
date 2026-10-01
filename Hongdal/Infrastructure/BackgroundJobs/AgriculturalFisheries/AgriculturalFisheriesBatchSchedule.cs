using 홍달.Services.Options;

namespace Hongdal.Infrastructure.BackgroundJobs.AgriculturalFisheries;

internal static class AgriculturalFisheriesBatchSchedule
{
    internal static DateOnly GetLocalDate(TimeProvider timeProvider, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var localNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            ResolveTimeZone(timeZoneId));
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    internal static DateOnly GetKamisDailyTargetDate(
        DateOnly localDate,
        AgriculturalFisheriesBatchOptions options)
        => localDate.AddDays(-Math.Clamp(options.KamisDailyDaysBehind, 1, 31));

    internal static (DateOnly StartDate, DateOnly EndDate) GetKamisMonthlyRange(
        DateOnly localDate,
        AgriculturalFisheriesBatchOptions options)
    {
        var completedMonthEnd = new DateOnly(localDate.Year, localDate.Month, 1).AddDays(-1);
        var lookbackMonths = Math.Clamp(options.KamisMonthlyLookbackMonths, 1, 60);
        var startDate = new DateOnly(completedMonthEnd.Year, completedMonthEnd.Month, 1)
            .AddMonths(-(lookbackMonths - 1));
        return (startDate, completedMonthEnd);
    }

    internal static int GetUsdaYearFrom(
        DateOnly localDate,
        AgriculturalFisheriesBatchOptions options)
        => localDate.Year - Math.Clamp(options.UsdaLookbackYears, 0, 10);

    internal static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException) when (
            string.Equals(timeZoneId.Trim(), "Asia/Seoul", StringComparison.Ordinal))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        }
    }
}
