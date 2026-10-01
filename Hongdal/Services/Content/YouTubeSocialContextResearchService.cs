using Hongdal.Contracts.Common.Content;
using Hongdal.Services.External.Apify.SocialMedia;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public interface IYouTubeSocialContextResearchService
{
    IReadOnlyList<SocialMediaResearchSourceDto> GetSources();

    Task<YouTubeSocialContextResearchResponse> ResearchAsync(
        YouTubeSocialContextResearchRequest request,
        CancellationToken cancellationToken);
}

public sealed class YouTubeSocialContextResearchService : IYouTubeSocialContextResearchService
{
    private readonly IReadOnlyDictionary<string, ISocialMediaPublicContentSource> _sources;
    private readonly IYouTubeSocialContextVideoSource _videoSource;
    private readonly IYouTubeSocialContextPostComposer _composer;
    private readonly ApifySocialMediaOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<YouTubeSocialContextResearchService> _logger;

    public YouTubeSocialContextResearchService(
        IEnumerable<ISocialMediaPublicContentSource> sources,
        IYouTubeSocialContextVideoSource videoSource,
        IYouTubeSocialContextPostComposer composer,
        IOptions<ApifySocialMediaOptions> options,
        TimeProvider timeProvider,
        ILogger<YouTubeSocialContextResearchService> logger)
    {
        var sourceList = sources.ToArray();
        var duplicate = sourceList
            .GroupBy(source => source.Source.SourceKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"SNS 조사 원천 키가 중복되었습니다: {duplicate.Key}");
        }

        _sources = sourceList.ToDictionary(
            source => source.Source.SourceKey,
            StringComparer.OrdinalIgnoreCase);
        _videoSource = videoSource;
        _composer = composer;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IReadOnlyList<SocialMediaResearchSourceDto> GetSources()
        => _sources.Values
            .Select(source => source.Describe())
            .OrderBy(source => source.Provider, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public async Task<YouTubeSocialContextResearchResponse> ResearchAsync(
        YouTubeSocialContextResearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var videoId = NormalizeRequired(request.VideoId, nameof(request.VideoId), 100);
        var video = await _videoSource.GetAsync(videoId, cancellationToken)
                    ?? throw new KeyNotFoundException($"YouTube 감시 저장소에 영상이 없습니다: {videoId}");
        var primaryTerms = NormalizeList(request.SearchTerms, 160);
        if (primaryTerms.Count == 0)
        {
            primaryTerms = [video.Title];
        }

        var adjacentTopics = NormalizeList(request.AdjacentTopics, 120);
        var actorTerms = primaryTerms
            .Concat(adjacentTopics)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_options.MaxSearchTerms, 1, 20))
            .ToArray();
        var selectedSources = ResolveSources(request.SourceKeys);
        if (selectedSources.Count == 0)
        {
            throw new InvalidOperationException("활성화된 SNS 조사 원천이 없습니다.");
        }

        var targets = ResolveTargets(request.SourceTargets);
        var take = Math.Clamp(request.TakePerSource, 1, 50);
        var countryCode = NormalizeCountryCode(request.CountryCode, video.CountryCode);
        var languageCode = NormalizeLanguageCode(request.LanguageCode, video.LanguageCode);
        var items = new List<CommunityInformationCandidateDto>();
        var failures = new List<YouTubeSocialContextSourceFailureDto>();

        foreach (var source in selectedSources)
        {
            if (!source.IsEnabled)
            {
                failures.Add(new YouTubeSocialContextSourceFailureDto(
                    source.Source.SourceKey,
                    $"{source.Source.Provider} 조사 모듈이 비활성화되어 있습니다."));
                continue;
            }

            try
            {
                items.AddRange(await source.SearchAsync(
                    new SocialMediaPublicContentQuery(
                        actorTerms,
                        targets.GetValueOrDefault(source.Source.SourceKey) ?? [],
                        take,
                        countryCode,
                        languageCode),
                    cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                failures.Add(new YouTubeSocialContextSourceFailureDto(
                    source.Source.SourceKey,
                    exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                failures.Add(new YouTubeSocialContextSourceFailureDto(
                    source.Source.SourceKey,
                    exception.Message));
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "YouTube SNS 맥락 조사에 실패했습니다. VideoId={VideoId}, SourceKey={SourceKey}",
                    videoId,
                    source.Source.SourceKey);
                failures.Add(new YouTubeSocialContextSourceFailureDto(
                    source.Source.SourceKey,
                    "외부 SNS 공개 자료를 조회하지 못했습니다."));
            }
        }

        var normalizedItems = items
            .GroupBy(item => item.CandidateKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.PublishedAtUtc ?? item.CollectedAtUtc)
            .ThenBy(item => item.SourceKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var draft = _composer.Compose(
            video,
            primaryTerms,
            adjacentTopics,
            normalizedItems);

        return new YouTubeSocialContextResearchResponse(
            _timeProvider.GetUtcNow().UtcDateTime,
            video,
            primaryTerms,
            adjacentTopics,
            selectedSources.Select(source => source.Describe()).ToArray(),
            normalizedItems,
            failures,
            draft);
    }

    private IReadOnlyList<ISocialMediaPublicContentSource> ResolveSources(
        IReadOnlyList<string>? requestedKeys)
    {
        var keys = (requestedKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (keys.Length == 0)
        {
            return _sources.Values
                .Where(source => source.IsEnabled)
                .OrderBy(source => source.Source.SourceKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var selected = new List<ISocialMediaPublicContentSource>();
        foreach (var key in keys)
        {
            if (!_sources.TryGetValue(key, out var source))
            {
                throw new ArgumentException($"지원하지 않는 SNS 조사 원천입니다: {key}", nameof(requestedKeys));
            }

            selected.Add(source);
        }

        return selected;
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveTargets(
        IReadOnlyList<SocialMediaResearchTargetDto>? requestedTargets)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in (requestedTargets ?? [])
                     .Where(target => target is not null)
                     .GroupBy(target => target.SourceKey?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            if (!_sources.ContainsKey(group.Key))
            {
                throw new ArgumentException($"지원하지 않는 SNS URL 대상 원천입니다: {group.Key}", nameof(requestedTargets));
            }

            result[group.Key] = group
                .SelectMany(target => target.StartUrls ?? [])
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return result;
    }

    private IReadOnlyList<string> NormalizeList(IEnumerable<string>? values, int maxLength)
        => (values ?? [])
            .Select(value => NormalizeOptional(value, maxLength))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_options.MaxSearchTerms, 1, 20))
            .ToArray();

    private static string NormalizeCountryCode(string? requested, string fallback)
    {
        var normalized = NormalizeOptional(requested, 2)?.ToUpperInvariant()
                         ?? NormalizeOptional(fallback, 2)?.ToUpperInvariant();
        return normalized?.Length == 2 ? normalized : "ZZ";
    }

    private static string NormalizeLanguageCode(string? requested, string fallback)
        => NormalizeOptional(requested, 20)
           ?? NormalizeOptional(fallback, 20)
           ?? "und";

    private static string NormalizeRequired(string? value, string parameterName, int maxLength)
        => NormalizeOptional(value, maxLength)
           ?? throw new ArgumentException($"{parameterName} 값이 필요합니다.", parameterName);

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"입력값은 {maxLength}자 이하여야 합니다.");
        }

        return normalized;
    }
}
