using System.Globalization;
using System.Text.RegularExpressions;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static partial class KamisPriceValueParser
{
    public static DateOnly? ParsePeriodSurveyDate(string year, string monthDay)
    {
        var parts = monthDay.Trim().Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(year.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear)
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedMonth)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedDay))
        {
            return null;
        }

        try
        {
            return new DateOnly(parsedYear, parsedMonth, parsedDay);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static DateOnly ParseSurveyDate(string value, DateOnly requestedDate)
    {
        var fullDate = FullDateRegex().Match(value);
        if (fullDate.Success
            && DateOnly.TryParseExact(
                fullDate.Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedFullDate))
        {
            return parsedFullDate;
        }

        var monthDay = MonthDayRegex().Match(value);
        if (!monthDay.Success
            || !int.TryParse(monthDay.Groups["month"].Value, out var month)
            || !int.TryParse(monthDay.Groups["day"].Value, out var day))
        {
            return requestedDate;
        }

        var candidate = new DateOnly(requestedDate.Year, month, day);
        return candidate > requestedDate.AddDays(1)
            ? candidate.AddYears(-1)
            : candidate;
    }

    public static decimal? ParsePrice(string value)
    {
        var normalized = value.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex FullDateRegex();

    [GeneratedRegex(@"(?<month>\d{1,2})/(?<day>\d{1,2})", RegexOptions.CultureInvariant)]
    private static partial Regex MonthDayRegex();
}
