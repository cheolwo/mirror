using System.Text.Json;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Microsoft.Extensions.Caching.Memory;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed class 미국농수산가격조회Service : I미국농수산가격조회Service
{
    private static readonly TimeSpan CompleteCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan NoDataCacheDuration = TimeSpan.FromMinutes(15);

    private readonly IReadOnlyDictionary<string, I미국농수산가격공급자> _providers;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<미국농수산가격조회Service> _logger;

    public 미국농수산가격조회Service(
        IEnumerable<I미국농수산가격공급자> providers,
        IMemoryCache memoryCache,
        ILogger<미국농수산가격조회Service> logger)
    {
        _providers = providers.ToDictionary(
            provider => provider.SourceKey,
            StringComparer.OrdinalIgnoreCase);
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<미국농수산가격조회응답> 조회Async(
        미국농수산가격조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var normalized = 미국농수산가격조회요청Rules.Normalize(request);
        if (!_providers.TryGetValue(normalized.SourceKey, out var provider))
        {
            return Fail(
                normalized,
                미국농수산가격조회상태Codes.지원하지않는출처,
                "지원하지 않는 미국 농수산물 가격 출처입니다.");
        }

        var validationError = 미국농수산가격조회요청Rules.Validate(normalized);
        if (validationError is not null)
        {
            return Fail(
                normalized,
                미국농수산가격조회상태Codes.잘못된요청,
                validationError,
                provider);
        }

        var cacheKey = BuildCacheKey(normalized);
        if (_memoryCache.TryGetValue(cacheKey, out 미국농수산가격조회응답? cached)
            && cached is not null)
        {
            return cached;
        }

        미국농수산가격조회응답 response;
        try
        {
            response = await provider.조회Async(normalized, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(
                "미국 농수산물 가격 출처 {SourceKey} 조회에 실패했습니다. 오류 유형: {ExceptionType}",
                provider.SourceKey,
                ex.GetType().Name);
            return Fail(
                normalized,
                미국농수산가격조회상태Codes.자료조회불가,
                "미국 농수산물 가격 자료를 현재 불러오지 못했습니다.",
                provider);
        }

        if (response.StatusCode == 미국농수산가격조회상태Codes.완료)
        {
            _memoryCache.Set(cacheKey, response, CompleteCacheDuration);
        }
        else if (response.StatusCode == 미국농수산가격조회상태Codes.자료없음)
        {
            _memoryCache.Set(cacheKey, response, NoDataCacheDuration);
        }

        return response;
    }

    private static string BuildCacheKey(미국농수산가격조회요청 request)
        => string.Join(
            '|',
            "us-agri-fish-price",
            request.SourceKey,
            request.Commodity,
            request.StatisticCategory,
            request.Program,
            request.Sector,
            request.Group,
            request.AggregationLevel,
            request.StateAlpha,
            request.Domain,
            request.Frequency,
            request.YearFrom,
            request.YearTo,
            request.MaxItems);

    private static 미국농수산가격조회응답 Fail(
        미국농수산가격조회요청 request,
        string statusCode,
        string errorMessage,
        I미국농수산가격공급자? provider = null)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            SourceKey = provider?.SourceKey ?? request.SourceKey,
            Provider = provider?.ProviderName ?? string.Empty,
            DocumentationUrl = provider?.DocumentationUrl ?? string.Empty,
            Query = request,
            CollectedAtUtc = DateTime.UtcNow,
            Summary = errorMessage,
            Notices = ["정보 제공 전용이며 주문·계약·주선 업무를 실행하지 않습니다."]
        };
}
