using System.Globalization;
using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.Customs;
using Microsoft.Extensions.Caching.Memory;

namespace 홍달.Services.External.PublicData;

public sealed class Hs공공데이터수집Service : IHs공공데이터수집Service
{
    private readonly IReadOnlyDictionary<string, IHs공공데이터수집기> _collectors;
    private readonly IMemoryCache _cache;
    private readonly ILogger<Hs공공데이터수집Service> _logger;

    public Hs공공데이터수집Service(
        IEnumerable<IHs공공데이터수집기> collectors,
        IMemoryCache cache,
        ILogger<Hs공공데이터수집Service> logger)
    {
        _collectors = collectors
            .GroupBy(collector => collector.SourceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _cache = cache;
        _logger = logger;
    }

    public async Task<Hs공공데이터묶음응답> 수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var requestedSourceKeys = ResolveSourceKeys(normalized.SourceKeys);
        var sourceTasks = requestedSourceKeys
            .Select(sourceKey => CollectSafelyAsync(sourceKey, normalized, cancellationToken))
            .ToArray();
        var sources = await Task.WhenAll(sourceTasks);
        var collectedAtUtc = DateTime.UtcNow;

        return new Hs공공데이터묶음응답
        {
            HsCode = normalized.HsCode,
            CountryCode = normalized.CountryCode,
            ReferenceMonth = normalized.ReferenceMonth,
            ReferenceDate = normalized.ReferenceDate,
            CollectedAtUtc = collectedAtUtc,
            SuccessSourceCount = sources.Count(source =>
                string.Equals(source.StatusCode, Hs공공데이터수집상태Codes.성공, StringComparison.Ordinal)),
            RequiresProfessionalReview = sources
                .SelectMany(source => source.Items)
                .Any(item => item.AttentionRequired),
            Sources = sources
        };
    }

    private async Task<Hs공공데이터출처응답> CollectSafelyAsync(
        string sourceKey,
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken)
    {
        if (!_collectors.TryGetValue(sourceKey, out var collector))
        {
            return new Hs공공데이터출처응답
            {
                SourceKey = sourceKey,
                Provider = "홍달",
                DisplayName = "지원하지 않는 공공데이터 출처",
                StatusCode = Hs공공데이터수집상태Codes.지원안됨,
                Summary = $"'{sourceKey}' 출처는 등록되어 있지 않습니다.",
                CollectedAtUtc = DateTime.UtcNow
            };
        }

        var cacheKey = BuildCacheKey(sourceKey, request);
        if (_cache.TryGetValue(cacheKey, out Hs공공데이터출처응답? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var response = await collector.수집Async(request, cancellationToken);
            if (CanCache(response.StatusCode))
            {
                _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "HS 공공데이터 출처 수집에 실패했습니다. SourceKey={SourceKey}, HsCode={HsCode}",
                sourceKey,
                request.HsCode);

            return new Hs공공데이터출처응답
            {
                SourceKey = sourceKey,
                Provider = "공공데이터포털",
                DisplayName = "공공데이터 조회",
                StatusCode = Hs공공데이터수집상태Codes.오류,
                Summary = "외부 공공데이터 조회 중 오류가 발생했습니다. 다른 출처의 결과는 계속 사용할 수 있습니다.",
                CollectedAtUtc = DateTime.UtcNow
            };
        }
    }

    private static bool CanCache(string statusCode)
        => string.Equals(statusCode, Hs공공데이터수집상태Codes.성공, StringComparison.Ordinal)
            || string.Equals(statusCode, Hs공공데이터수집상태Codes.데이터없음, StringComparison.Ordinal)
            || string.Equals(statusCode, Hs공공데이터수집상태Codes.적용안됨, StringComparison.Ordinal);

    private static string BuildCacheKey(string sourceKey, Hs공공데이터수집요청 request)
        => string.Join(
            '|',
            "hs-public-data",
            sourceKey,
            request.HsCode,
            request.CountryCode,
            request.ReferenceMonth,
            request.LookbackMonths.ToString(CultureInfo.InvariantCulture),
            request.ReferenceDate,
            request.ExpectedFxRateKrwPerUsd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private IReadOnlyList<string> ResolveSourceKeys(IReadOnlyList<string> requestedSourceKeys)
    {
        if (requestedSourceKeys.Count == 0)
        {
            return Hs공공데이터출처Keys.전체;
        }

        return requestedSourceKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Hs공공데이터수집요청 Normalize(Hs공공데이터수집요청 request)
    {
        var now = DateTime.UtcNow;

        return new Hs공공데이터수집요청
        {
            HsCode = DigitsOnly(request.HsCode),
            CountryCode = Regex.Replace(request.CountryCode ?? string.Empty, "[^0-9A-Za-z]", string.Empty)
                .ToUpperInvariant(),
            ReferenceMonth = NormalizeMonth(request.ReferenceMonth)
                ?? now.ToString("yyyyMM", CultureInfo.InvariantCulture),
            LookbackMonths = Math.Clamp(request.LookbackMonths <= 0 ? 3 : request.LookbackMonths, 1, 12),
            ReferenceDate = NormalizeDate(request.ReferenceDate)
                ?? now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ExpectedFxRateKrwPerUsd = request.ExpectedFxRateKrwPerUsd,
            SourceKeys = request.SourceKeys
        };
    }

    private static string DigitsOnly(string? value)
        => Regex.Replace(value ?? string.Empty, "[^0-9]", string.Empty);

    private static string? NormalizeMonth(string? value)
    {
        var digits = DigitsOnly(value);
        return DateTime.TryParseExact(
            digits,
            "yyyyMM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _)
            ? digits
            : null;
    }

    private static string? NormalizeDate(string? value)
    {
        var digits = DigitsOnly(value);
        return DateTime.TryParseExact(
            digits,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _)
            ? digits
            : null;
    }
}
