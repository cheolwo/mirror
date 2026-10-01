using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Microsoft.EntityFrameworkCore;

namespace Hongdal.Services.Content;

public sealed class CommunityUsdaNassInformationCandidateSource
    : ICommunityInformationCandidateSource
{
    private const string DocumentationUrl = "https://quickstats.nass.usda.gov/api";
    private static readonly IReadOnlyDictionary<string, int> MonthNumbers =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["JAN"] = 1,
            ["JANUARY"] = 1,
            ["FEB"] = 2,
            ["FEBRUARY"] = 2,
            ["MAR"] = 3,
            ["MARCH"] = 3,
            ["APR"] = 4,
            ["APRIL"] = 4,
            ["MAY"] = 5,
            ["JUN"] = 6,
            ["JUNE"] = 6,
            ["JUL"] = 7,
            ["JULY"] = 7,
            ["AUG"] = 8,
            ["AUGUST"] = 8,
            ["SEP"] = 9,
            ["SEPT"] = 9,
            ["SEPTEMBER"] = 9,
            ["OCT"] = 10,
            ["OCTOBER"] = 10,
            ["NOV"] = 11,
            ["NOVEMBER"] = 11,
            ["DEC"] = 12,
            ["DECEMBER"] = 12
        };

    private readonly AgriculturalFisheriesDbContext _db;

    public CommunityUsdaNassInformationCandidateSource(AgriculturalFisheriesDbContext db)
    {
        _db = db;
    }

    public CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.UsdaNassPriceObservations,
        CommunityInformationSourceTypes.PublicData,
        "USDA National Agricultural Statistics Service (NASS)",
        "USDA NASS 미국 농산물 월별 생산자가격",
        CommunityInformationCollectionModes.ScheduledArchive,
        "서버 배치 월 1회, USDA Quick Stats 원천은 평일 갱신",
        "서버가 보관한 미국 전국 월별 PRICE RECEIVED 관측값만 글쓰기 근거 후보로 제공합니다.",
        DocumentationUrl,
        true);

    public async Task<IReadOnlyList<CommunityInformationCandidateDto>> ReadAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!MatchesUnitedStatesSource(query.CountryCode)
            || !MatchesOfficialObservation(query.ReviewState))
        {
            return [];
        }

        var observations = _db.PriceObservations
            .AsNoTracking()
            .Where(observation => observation.FrequencyDesc == "MONTHLY"
                                  && !observation.IsSuppressed
                                  && observation.NumericValue.HasValue);
        if (query.StartDate.HasValue)
        {
            observations = observations.Where(observation => observation.Year >= query.StartDate.Value.Year);
        }

        if (query.EndDate.HasValue)
        {
            observations = observations.Where(observation => observation.Year <= query.EndDate.Value.Year);
        }

        var searchText = query.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            observations = observations.Where(observation =>
                observation.CommodityDesc.Contains(searchText)
                || observation.ClassDesc.Contains(searchText)
                || observation.ShortDesc.Contains(searchText)
                || observation.GroupDesc.Contains(searchText)
                || observation.UnitDesc.Contains(searchText));
        }

        var fetchCount = (int)Math.Clamp(Math.Max((long)query.Take * 20, 200), 1, 2000);
        var rows = await observations
            .OrderByDescending(observation => observation.Year)
            .ThenBy(observation => observation.CommodityDesc)
            .ThenBy(observation => observation.ClassDesc)
            .ThenBy(observation => observation.ReferencePeriodDesc)
            .Take(fetchCount)
            .ToListAsync(cancellationToken);

        return rows
            .Select(ToCandidate)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .Where(candidate => OverlapsRange(candidate, query.StartDate, query.EndDate))
            .GroupBy(candidate => candidate.CandidateKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(candidate => candidate.ReferenceDate)
            .ThenBy(candidate => candidate.Title)
            .Take(Math.Clamp(query.Take, 1, 100))
            .ToArray();
    }

    private static CommunityInformationCandidateDto? ToCandidate(UsdaNassPriceObservation observation)
    {
        if (!TryResolveReferencePeriod(observation, out var startDate, out var endDate))
        {
            return null;
        }

        var sourceUrl = IsPublicHttpUrl(observation.SourceUrl)
            ? observation.SourceUrl.Trim()
            : DocumentationUrl;
        var currencyCode = ResolveCurrencyCode(observation.UnitDesc);
        var unit = NormalizeUnit(observation.UnitDesc, currencyCode);
        var title = string.IsNullOrWhiteSpace(observation.ShortDesc)
            ? observation.CommodityDesc.Trim()
            : observation.ShortDesc.Trim();
        var metricLabel = string.Equals(
            observation.StatisticCategoryDesc,
            "PRICE RECEIVED",
            StringComparison.OrdinalIgnoreCase)
            ? "생산자가격"
            : observation.StatisticCategoryDesc.Trim();

        return new CommunityInformationCandidateDto(
            $"usda-nass:{observation.RecordKey}",
            CommunityInformationSourceKeys.UsdaNassPriceObservations,
            CommunityInformationSourceTypes.PublicData,
            "USDA National Agricultural Statistics Service (NASS)",
            title,
            $"{startDate:yyyy년 M월} · 미국 전국 · {FormatValue(observation.NumericValue!.Value, currencyCode, unit)}",
            sourceUrl,
            null,
            null,
            startDate,
            DateTime.SpecifyKind(observation.LastSeenAtUtc, DateTimeKind.Utc),
            "US",
            "en",
            currencyCode,
            unit,
            CommunityInformationReviewStates.OfficialObservation,
            new[]
            {
                "농수산물",
                "미국",
                observation.GroupDesc,
                observation.CommodityDesc,
                observation.ClassDesc
            }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            "USDA NASS Quick Stats에서 서버 배치로 수집·보관한 공식 월별 관측값입니다. This product uses the NASS API but is not endorsed or certified by NASS.",
            "미국 생산자가 받은 전국 월별 가격 통계이며 소매가격, 개별 판매 제안 또는 운송 포함 견적이 아닙니다. 품목·등급·용도·생산방식·단위가 같은 계열만 비교하고 비공개·극소량 표시는 제외합니다.",
            observation.NumericValue,
            string.IsNullOrWhiteSpace(metricLabel) ? "관측값" : metricLabel,
            BuildMetricSeriesKey(observation),
            endDate,
            BuildMetricSeriesLabel(observation));
    }

    private static bool TryResolveReferencePeriod(
        UsdaNassPriceObservation observation,
        out DateOnly startDate,
        out DateOnly endDate)
    {
        startDate = default;
        endDate = default;
        if (observation.Year is < 1900 or > 2100)
        {
            return false;
        }

        var period = observation.ReferencePeriodDesc.Trim();
        if (!MonthNumbers.TryGetValue(period, out var month))
        {
            return false;
        }

        startDate = new DateOnly(observation.Year, month, 1);
        endDate = new DateOnly(
            observation.Year,
            month,
            DateTime.DaysInMonth(observation.Year, month));
        return true;
    }

    private static string BuildMetricSeriesKey(UsdaNassPriceObservation observation)
    {
        var dimensions = string.Join(
            '\u001f',
            observation.SourceDesc,
            observation.SectorDesc,
            observation.GroupDesc,
            observation.CommodityDesc,
            observation.ClassDesc,
            observation.UtilPracticeDesc,
            observation.ProductionPracticeDesc,
            observation.StatisticCategoryDesc,
            observation.UnitDesc,
            observation.ShortDesc,
            observation.DomainDesc,
            observation.DomainCategoryDesc,
            observation.AggregationLevelDesc,
            observation.CountryCode,
            observation.FrequencyDesc);
        return $"usda-nass|{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dimensions))).ToLowerInvariant()}";
    }

    private static string BuildMetricSeriesLabel(UsdaNassPriceObservation observation)
    {
        var subject = string.IsNullOrWhiteSpace(observation.ClassDesc)
            ? observation.CommodityDesc
            : $"{observation.CommodityDesc} · {observation.ClassDesc}";
        return string.Join(
            " · ",
            new[] { subject, observation.StatisticCategoryDesc, observation.UnitDesc }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? ResolveCurrencyCode(string? rawUnit)
    {
        var unit = rawUnit?.Trim() ?? string.Empty;
        return unit.Contains('$')
               || unit.Contains("DOLLAR", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : null;
    }

    private static string? NormalizeUnit(string? rawUnit, string? currencyCode)
    {
        var unit = rawUnit?.Trim() ?? string.Empty;
        if (unit.Length == 0)
        {
            return null;
        }

        if (!string.Equals(currencyCode, "USD", StringComparison.Ordinal))
        {
            return unit;
        }

        if (unit.StartsWith("DOLLARS", StringComparison.OrdinalIgnoreCase))
        {
            unit = unit["DOLLARS".Length..];
        }
        else if (unit.StartsWith('$'))
        {
            unit = unit[1..];
        }

        return unit.TrimStart(' ', '/').Trim() is { Length: > 0 } normalized
            ? normalized
            : null;
    }

    private static string FormatValue(decimal value, string? currencyCode, string? unit)
    {
        var amount = string.Equals(currencyCode, "USD", StringComparison.Ordinal)
            ? $"USD {value.ToString("N2", CultureInfo.InvariantCulture)}"
            : value.ToString("N2", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(unit) ? amount : $"{amount}/{unit}";
    }

    private static bool OverlapsRange(
        CommunityInformationCandidateDto candidate,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var candidateStart = candidate.ReferenceDate!.Value;
        var candidateEnd = candidate.ReferencePeriodEndDate ?? candidateStart;
        return (!startDate.HasValue || candidateEnd >= startDate.Value)
               && (!endDate.HasValue || candidateStart <= endDate.Value);
    }

    private static bool MatchesUnitedStatesSource(string? countryCode)
        => string.IsNullOrWhiteSpace(countryCode)
           || string.Equals(countryCode.Trim(), "US", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesOfficialObservation(string? reviewState)
        => string.IsNullOrWhiteSpace(reviewState)
           || string.Equals(
               reviewState.Trim(),
               CommunityInformationReviewStates.OfficialObservation,
               StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
