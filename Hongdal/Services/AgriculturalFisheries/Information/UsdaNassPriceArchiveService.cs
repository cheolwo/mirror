using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed record UsdaNassPriceArchiveResult(
    long CollectionRunId,
    int FetchedCount,
    int InsertedCount,
    int ExistingCount,
    int MappingCount,
    DateTime? LatestSourceLoadTimeUtc);

public interface IUsdaNassPriceArchiveService
{
    Task<UsdaNassPriceArchiveResult> CollectRecentMonthlyPricesAsync(
        int yearFrom,
        CancellationToken cancellationToken = default);
}

public sealed class UsdaNassPriceArchiveService : IUsdaNassPriceArchiveService
{
    private const string SourceUrl = "https://quickstats.nass.usda.gov/api";

    private readonly HttpClient _httpClient;
    private readonly AgriculturalFisheriesDbContext _db;
    private readonly PublicDataOptions _options;
    private readonly ILogger<UsdaNassPriceArchiveService> _logger;

    public UsdaNassPriceArchiveService(
        HttpClient httpClient,
        AgriculturalFisheriesDbContext db,
        IOptions<PublicDataOptions> options,
        ILogger<UsdaNassPriceArchiveService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UsdaNassPriceArchiveResult> CollectRecentMonthlyPricesAsync(
        int yearFrom,
        CancellationToken cancellationToken = default)
    {
        if (yearFrom is < 1900 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(yearFrom));
        }

        var apiKey = _options.UsdaNassQuickStats.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("USDA NASS Quick Stats API 키가 설정되지 않았습니다.");
        }

        var run = new UsdaNassPriceCollectionRun
        {
            YearFrom = yearFrom,
            QuerySummary =
                $"SURVEY / CROPS / PRICE RECEIVED / NATIONAL / MONTHLY / year >= {yearFrom}",
            SourceUrl = SourceUrl,
            StartedAtUtc = DateTime.UtcNow
        };
        _db.CollectionRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var requestPath = QueryHelpers.AddQueryString(
                _options.UsdaNassQuickStats.DataPath.TrimStart('/'),
                new Dictionary<string, string?>
                {
                    ["key"] = apiKey,
                    ["source_desc"] = "SURVEY",
                    ["sector_desc"] = "CROPS",
                    ["statisticcat_desc"] = "PRICE RECEIVED",
                    ["agg_level_desc"] = "NATIONAL",
                    ["freq_desc"] = "MONTHLY",
                    ["year__GE"] = yearFrom.ToString(CultureInfo.InvariantCulture),
                    ["format"] = "JSON"
                });

            using var response = await _httpClient.GetAsync(
                requestPath,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (TryGetProperty(document.RootElement, "error", out var apiError))
            {
                throw new InvalidOperationException(
                    $"USDA NASS API가 수집 요청을 거부했습니다: {apiError.GetRawText()}");
            }

            if (!TryGetProperty(document.RootElement, "data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("USDA NASS 응답에 data 배열이 없습니다.");
            }

            var collectedAtUtc = DateTime.UtcNow;
            var incoming = data
                .EnumerateArray()
                .Select(item => MapObservation(item, collectedAtUtc))
                .GroupBy(item => item.RecordKey, StringComparer.Ordinal)
                .Select(group => group.Last())
                .ToArray();
            var recordKeys = incoming
                .Select(item => item.RecordKey)
                .ToHashSet(StringComparer.Ordinal);
            var existing = await _db.PriceObservations
                .Where(item => recordKeys.Contains(item.RecordKey))
                .ToDictionaryAsync(item => item.RecordKey, StringComparer.Ordinal, cancellationToken);

            foreach (var item in incoming)
            {
                if (existing.TryGetValue(item.RecordKey, out var stored))
                {
                    stored.LastSeenAtUtc = collectedAtUtc;
                    continue;
                }

                item.FirstCollectionRunId = run.Id;
                _db.PriceObservations.Add(item);
            }

            var seeds = UsdaNassHsMappingSeed.Create();
            var mappingKeys = seeds
                .Select(item => item.MappingKey)
                .ToHashSet(StringComparer.Ordinal);
            var existingMappingKeyList = await _db.HsCommodityMappings
                .Where(item => mappingKeys.Contains(item.MappingKey))
                .Select(item => item.MappingKey)
                .ToListAsync(cancellationToken);
            var existingMappingKeys = existingMappingKeyList.ToHashSet(StringComparer.Ordinal);
            _db.HsCommodityMappings.AddRange(
                seeds.Where(item => !existingMappingKeys.Contains(item.MappingKey)));

            var latestSourceLoad = incoming
                .Where(item => item.SourceLoadTimeUtc.HasValue)
                .Select(item => item.SourceLoadTimeUtc!.Value)
                .Cast<DateTime?>()
                .DefaultIfEmpty()
                .Max();
            run.StatusCode = UsdaNassArchiveStatusCodes.Completed;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.LatestSourceLoadTimeUtc = latestSourceLoad;
            run.FetchedCount = incoming.Length;
            run.InsertedCount = incoming.Length - existing.Count;
            run.ExistingCount = existing.Count;
            await _db.SaveChangesAsync(cancellationToken);

            var mappingCount = await _db.HsCommodityMappings.CountAsync(cancellationToken);
            _logger.LogInformation(
                "USDA NASS 가격 아카이브 수집 완료. RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Existing={Existing}",
                run.Id,
                run.FetchedCount,
                run.InsertedCount,
                run.ExistingCount);

            return new UsdaNassPriceArchiveResult(
                run.Id,
                run.FetchedCount,
                run.InsertedCount,
                run.ExistingCount,
                mappingCount,
                latestSourceLoad);
        }
        catch (Exception ex)
        {
            run.StatusCode = UsdaNassArchiveStatusCodes.Failed;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.ErrorMessage = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static UsdaNassPriceObservation MapObservation(
        JsonElement source,
        DateTime collectedAtUtc)
    {
        var valueRaw = ReadString(source, "Value");
        var numericValue = ParseDecimal(valueRaw);
        var loadTime = ParseUtcDateTime(ReadString(source, "load_time"));
        var fields = new[]
        {
            ReadString(source, "source_desc"),
            ReadString(source, "sector_desc"),
            ReadString(source, "group_desc"),
            ReadString(source, "commodity_desc"),
            ReadString(source, "class_desc"),
            ReadString(source, "util_practice_desc"),
            ReadString(source, "prodn_practice_desc"),
            ReadString(source, "statisticcat_desc"),
            ReadString(source, "unit_desc"),
            ReadString(source, "short_desc"),
            ReadString(source, "domain_desc"),
            ReadString(source, "domaincat_desc"),
            ReadString(source, "agg_level_desc"),
            ReadString(source, "country_code"),
            ReadString(source, "year"),
            ReadString(source, "freq_desc"),
            ReadString(source, "begin_code"),
            ReadString(source, "end_code"),
            ReadString(source, "reference_period_desc"),
            valueRaw,
            ReadString(source, "load_time")
        };

        return new UsdaNassPriceObservation
        {
            RecordKey = Sha256(string.Join('\u001f', fields)),
            SourceDesc = fields[0],
            SectorDesc = fields[1],
            GroupDesc = fields[2],
            CommodityDesc = fields[3],
            ClassDesc = fields[4],
            UtilPracticeDesc = fields[5],
            ProductionPracticeDesc = fields[6],
            StatisticCategoryDesc = fields[7],
            UnitDesc = fields[8],
            ShortDesc = fields[9],
            DomainDesc = fields[10],
            DomainCategoryDesc = fields[11],
            AggregationLevelDesc = fields[12],
            CountryCode = fields[13],
            CountryName = ReadString(source, "country_name"),
            Year = int.TryParse(fields[14], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
                ? year
                : 0,
            FrequencyDesc = fields[15],
            BeginCode = fields[16],
            EndCode = fields[17],
            ReferencePeriodDesc = fields[18],
            ValueRaw = valueRaw,
            NumericValue = numericValue,
            IsSuppressed = numericValue is null && !string.IsNullOrWhiteSpace(valueRaw),
            CvPercentRaw = ReadString(source, "CV (%)"),
            SourceLoadTimeUtc = loadTime,
            SourceUrl = SourceUrl,
            RawJson = source.GetRawText(),
            FirstCollectedAtUtc = collectedAtUtc,
            LastSeenAtUtc = collectedAtUtc
        };
    }

    private static string ReadString(JsonElement source, string propertyName)
    {
        if (!TryGetProperty(source, propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
    }

    private static bool TryGetProperty(
        JsonElement source,
        string propertyName,
        out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static decimal? ParseDecimal(string value)
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

    private static DateTime? ParseUtcDateTime(string value)
    {
        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return null;
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    internal static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
