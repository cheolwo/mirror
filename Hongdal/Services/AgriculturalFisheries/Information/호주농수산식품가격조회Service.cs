using System.Text.Json;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Microsoft.Extensions.Caching.Memory;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed class 호주농수산식품가격조회Service : I호주농수산식품가격조회Service
{
    private static readonly TimeSpan CompleteCacheDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan NoDataCacheDuration = TimeSpan.FromMinutes(30);

    private readonly IReadOnlyDictionary<string, I호주농수산식품가격공급자> _providers;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<호주농수산식품가격조회Service> _logger;

    public 호주농수산식품가격조회Service(
        IEnumerable<I호주농수산식품가격공급자> providers,
        IMemoryCache memoryCache,
        ILogger<호주농수산식품가격조회Service> logger)
    {
        _providers = providers.ToDictionary(
            provider => provider.SourceKey,
            StringComparer.OrdinalIgnoreCase);
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public 호주농수산식품가격Catalog응답 GetCatalog()
        => 호주농수산식품가격Catalog.Build();

    public async Task<호주농수산식품가격조회응답> 조회Async(
        호주농수산식품가격조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var normalized = 호주농수산식품가격조회요청Rules.Normalize(request);
        if (!_providers.TryGetValue(normalized.SourceKey, out var provider))
        {
            return Fail(
                normalized,
                호주농수산식품가격조회상태Codes.지원하지않는출처,
                "자동 조회를 지원하지 않는 호주 농수산물 가격 출처입니다.");
        }

        var validationError = 호주농수산식품가격조회요청Rules.Validate(normalized);
        if (validationError is not null)
        {
            return Fail(
                normalized,
                호주농수산식품가격조회상태Codes.잘못된요청,
                validationError,
                provider);
        }

        var cacheKey = BuildCacheKey(normalized);
        if (_memoryCache.TryGetValue(cacheKey, out 호주농수산식품가격조회응답? cached)
            && cached is not null)
        {
            return cached;
        }

        호주농수산식품가격조회응답 response;
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
                "호주 농수산물 가격 출처 {SourceKey} 조회에 실패했습니다. 오류 유형: {ExceptionType}",
                provider.SourceKey,
                ex.GetType().Name);
            return Fail(
                normalized,
                호주농수산식품가격조회상태Codes.자료조회불가,
                "호주 농수산물 가격 자료를 현재 불러오지 못했습니다.",
                provider);
        }

        if (response.StatusCode == 호주농수산식품가격조회상태Codes.완료)
        {
            _memoryCache.Set(cacheKey, response, CompleteCacheDuration);
        }
        else if (response.StatusCode == 호주농수산식품가격조회상태Codes.자료없음)
        {
            _memoryCache.Set(cacheKey, response, NoDataCacheDuration);
        }

        return response;
    }

    private static string BuildCacheKey(호주농수산식품가격조회요청 request)
        => string.Join(
            '|',
            "au-agri-fish-food-price",
            request.SourceKey,
            request.IndexCode,
            request.MeasureCode,
            request.RegionCode,
            request.StartPeriod,
            request.EndPeriod,
            request.MaxItems);

    private static 호주농수산식품가격조회응답 Fail(
        호주농수산식품가격조회요청 request,
        string statusCode,
        string errorMessage,
        I호주농수산식품가격공급자? provider = null)
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
            Notices =
            [
                "정보 제공 전용이며 주문·계약·주선 업무를 실행하지 않습니다.",
                "ABARES 웹 보고서·다운로드 원천은 카탈로그에서 확인할 수 있지만 현재 자동 조회 대상은 ABS CPI뿐입니다."
            ]
        };
}
