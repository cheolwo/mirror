using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using 살뜰.Services.Options;

namespace 살뜰.Services.Weather;

public static class 픽업지기상자료상태Code
{
    public const string Available = "Available";
    public const string MissingCoordinates = "MissingCoordinates";
    public const string MissingServiceKey = "MissingServiceKey";
    public const string RemoteFailure = "RemoteFailure";
    public const string ObservationNotFound = "ObservationNotFound";
    public const string UnknownPrecipitationCode = "UnknownPrecipitationCode";
}

public sealed record 픽업지기상관측결과(
    bool 유효한강수근거있음,
    bool 강수중,
    string 자료상태Code,
    string? 강수형태Code,
    string? 한시간강수량표기,
    DateTime? 관측기준시각Utc,
    string 자료출처,
    string? 원본Hash)
{
    public const string 공식자료출처 = "https://www.data.go.kr/data/15084084/openapi.do";

    public static 픽업지기상관측결과 사용불가(string statusCode)
        => new(false, false, statusCode, null, null, null, 공식자료출처, null);
}

public interface I픽업지기상관측Client
{
    Task<픽업지기상관측결과> 조회Async(
        decimal? latitude,
        decimal? longitude,
        CancellationToken cancellationToken = default);
}

public sealed class 기상청초단기실황Client(
    HttpClient httpClient,
    IMemoryCache memoryCache,
    IOptions<PublicDataOptions> options,
    TimeProvider timeProvider,
    ILogger<기상청초단기실황Client> logger) : I픽업지기상관측Client
{
    private static readonly SemaphoreSlim PublicApiRequestGate = new(1, 1);
    private static readonly HashSet<string> KnownPrecipitationCodes =
        ["0", "1", "2", "3", "5", "6", "7"];
    private readonly PublicDataOptions _options = options.Value;

    public async Task<픽업지기상관측결과> 조회Async(
        decimal? latitude,
        decimal? longitude,
        CancellationToken cancellationToken = default)
    {
        if (!latitude.HasValue || !longitude.HasValue
            || latitude is < -90m or > 90m
            || longitude is < -180m or > 180m)
        {
            return 픽업지기상관측결과.사용불가(픽업지기상자료상태Code.MissingCoordinates);
        }

        var serviceKey = string.IsNullOrWhiteSpace(_options.DataGoKrServiceKey)
            ? _options.ServiceKey
            : _options.DataGoKrServiceKey;
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return 픽업지기상관측결과.사용불가(픽업지기상자료상태Code.MissingServiceKey);
        }

        var grid = 위경도를기상청격자로변환(latitude.Value, longitude.Value);
        var koreaNow = timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(9));
        var safetyDelay = Math.Clamp(_options.KmaUltraShortNowcast.BaseTimeSafetyDelayMinutes, 40, 120);
        var baseTime = koreaNow.AddMinutes(-safetyDelay);
        baseTime = new DateTimeOffset(
            baseTime.Year,
            baseTime.Month,
            baseTime.Day,
            baseTime.Hour,
            0,
            0,
            baseTime.Offset);
        var requestUri = string.Create(
            CultureInfo.InvariantCulture,
            $"{_options.KmaUltraShortNowcast.ObservationPath}?serviceKey={Uri.EscapeDataString(serviceKey)}&pageNo=1&numOfRows=20&dataType=JSON&base_date={baseTime:yyyyMMdd}&base_time={baseTime:HHmm}&nx={grid.X}&ny={grid.Y}");
        var cacheKey = string.Create(
            CultureInfo.InvariantCulture,
            $"kma-ultra-short-observation:{baseTime:yyyyMMddHHmm}:{grid.X}:{grid.Y}");

        if (memoryCache.TryGetValue<픽업지기상관측결과>(cacheKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        await PublicApiRequestGate.WaitAsync(cancellationToken);
        try
        {
            if (memoryCache.TryGetValue<픽업지기상관측결과>(cacheKey, out cached)
                && cached is not null)
            {
                return cached;
            }

            var result = await FetchAsync(requestUri, grid, baseTime, cancellationToken);
            var cacheDuration = result.자료상태Code is 픽업지기상자료상태Code.RemoteFailure
                or 픽업지기상자료상태Code.ObservationNotFound
                ? TimeSpan.FromSeconds(Math.Clamp(
                    _options.KmaUltraShortNowcast.FailureCacheSeconds,
                    30,
                    600))
                : TimeSpan.FromMinutes(Math.Clamp(
                    _options.KmaUltraShortNowcast.ObservationCacheMinutes,
                    10,
                    180));
            memoryCache.Set(cacheKey, result, cacheDuration);
            return result;
        }
        finally
        {
            PublicApiRequestGate.Release();
        }
    }

    private async Task<픽업지기상관측결과> FetchAsync(
        string requestUri,
        (int X, int Y) grid,
        DateTimeOffset baseTime,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            return Parse(payload, baseTime, hash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "픽업지 초단기 기상실황 조회에 실패했습니다. GridX={GridX} GridY={GridY} BaseTime={BaseTime}",
                grid.X,
                grid.Y,
                baseTime);
            return 픽업지기상관측결과.사용불가(픽업지기상자료상태Code.RemoteFailure);
        }
    }

    private static 픽업지기상관측결과 Parse(
        byte[] payload,
        DateTimeOffset requestedBaseTime,
        string payloadHash)
    {
        using var document = JsonDocument.Parse(payload);
        if (!TryGetProperty(document.RootElement, "response", out var response)
            || !TryGetProperty(response, "header", out var header)
            || !TryGetProperty(header, "resultCode", out var resultCodeElement)
            || Scalar(resultCodeElement) is not ("00" or "0000"))
        {
            throw new InvalidOperationException("KmaUltraShortNowcastRemoteFailure");
        }

        if (!TryGetProperty(response, "body", out var body)
            || !TryGetProperty(body, "items", out var items)
            || !TryGetProperty(items, "item", out var itemElement))
        {
            return 픽업지기상관측결과.사용불가(픽업지기상자료상태Code.ObservationNotFound);
        }

        string? precipitationType = null;
        string? hourlyRainfall = null;
        string? baseDate = null;
        string? baseTime = null;
        foreach (var item in EnumerateItems(itemElement))
        {
            var category = OptionalScalar(item, "category");
            if (category == "PTY")
            {
                precipitationType = OptionalScalar(item, "obsrValue");
                baseDate ??= OptionalScalar(item, "baseDate");
                baseTime ??= OptionalScalar(item, "baseTime");
            }
            else if (category == "RN1")
            {
                hourlyRainfall = OptionalScalar(item, "obsrValue");
            }
        }

        if (string.IsNullOrWhiteSpace(precipitationType))
        {
            return 픽업지기상관측결과.사용불가(픽업지기상자료상태Code.ObservationNotFound);
        }

        if (!KnownPrecipitationCodes.Contains(precipitationType))
        {
            return new 픽업지기상관측결과(
                false,
                false,
                픽업지기상자료상태Code.UnknownPrecipitationCode,
                precipitationType,
                hourlyRainfall,
                ResolveObservedAtUtc(baseDate, baseTime, requestedBaseTime),
                픽업지기상관측결과.공식자료출처,
                payloadHash);
        }

        return new 픽업지기상관측결과(
            true,
            precipitationType != "0",
            픽업지기상자료상태Code.Available,
            precipitationType,
            hourlyRainfall,
            ResolveObservedAtUtc(baseDate, baseTime, requestedBaseTime),
            픽업지기상관측결과.공식자료출처,
            payloadHash);
    }

    internal static (int X, int Y) 위경도를기상청격자로변환(decimal latitude, decimal longitude)
    {
        const double earthRadiusKm = 6371.00877;
        const double gridKm = 5.0;
        const double standardLatitude1 = 30.0;
        const double standardLatitude2 = 60.0;
        const double originLongitude = 126.0;
        const double originLatitude = 38.0;
        const double originX = 43.0;
        const double originY = 136.0;
        var degreesToRadians = Math.PI / 180.0;
        var re = earthRadiusKm / gridKm;
        var slat1 = standardLatitude1 * degreesToRadians;
        var slat2 = standardLatitude2 * degreesToRadians;
        var olon = originLongitude * degreesToRadians;
        var olat = originLatitude * degreesToRadians;
        var sn = Math.Tan(Math.PI * 0.25 + slat2 * 0.5)
                 / Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
        sn = Math.Log(Math.Cos(slat1) / Math.Cos(slat2)) / Math.Log(sn);
        var sf = Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
        sf = Math.Pow(sf, sn) * Math.Cos(slat1) / sn;
        var ro = Math.Tan(Math.PI * 0.25 + olat * 0.5);
        ro = re * sf / Math.Pow(ro, sn);
        var ra = Math.Tan(Math.PI * 0.25 + (double)latitude * degreesToRadians * 0.5);
        ra = re * sf / Math.Pow(ra, sn);
        var theta = (double)longitude * degreesToRadians - olon;
        if (theta > Math.PI)
        {
            theta -= 2.0 * Math.PI;
        }
        else if (theta < -Math.PI)
        {
            theta += 2.0 * Math.PI;
        }

        theta *= sn;
        return (
            (int)Math.Floor(ra * Math.Sin(theta) + originX + 0.5),
            (int)Math.Floor(ro - ra * Math.Cos(theta) + originY + 0.5));
    }

    private static DateTime? ResolveObservedAtUtc(
        string? baseDate,
        string? baseTime,
        DateTimeOffset fallback)
    {
        if (DateTime.TryParseExact(
                $"{baseDate}{baseTime}",
                "yyyyMMddHHmm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return new DateTimeOffset(local, TimeSpan.FromHours(9)).UtcDateTime;
        }

        return fallback.UtcDateTime;
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement items)
        => items.ValueKind switch
        {
            JsonValueKind.Array => items.EnumerateArray(),
            JsonValueKind.Object => [items],
            _ => []
        };

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(name, out value);
    }

    private static string? OptionalScalar(JsonElement element, string name)
        => TryGetProperty(element, name, out var value) ? Scalar(value).Trim() : null;

    private static string Scalar(JsonElement element)
        => element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();
}
