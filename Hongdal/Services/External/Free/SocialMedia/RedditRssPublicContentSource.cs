using System.Globalization;
using System.Net.Http.Headers;
using System.Xml.Linq;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.External.Apify.SocialMedia;
using 홍달.Services.Options;
using Microsoft.Extensions.Options;

namespace Hongdal.Services.External.Free.SocialMedia;

/// <summary>
/// Apify를 사용하지 않고 운영자가 지정한 Reddit 공개 RSS/Atom 피드만 읽습니다.
/// 전역 Reddit 검색이나 HTML 페이지 수집을 수행하지 않습니다.
/// </summary>
public sealed class RedditRssPublicContentSource : ISocialMediaPublicContentSource
{
    private static readonly IReadOnlySet<string> AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "reddit.com",
        "redd.it"
    };

    private readonly HttpClient _httpClient;
    private readonly FreeSocialMediaOptions _moduleOptions;
    private readonly RedditRssPublicContentOptions _providerOptions;
    private readonly TimeProvider _timeProvider;

    public RedditRssPublicContentSource(
        HttpClient httpClient,
        IOptions<FreeSocialMediaOptions> options,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _moduleOptions = options.Value;
        _providerOptions = options.Value.RedditRss;
        _timeProvider = timeProvider;
    }

    public CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.RedditRssPublicPosts,
        CommunityInformationSourceTypes.SocialMedia,
        "Reddit RSS",
        "Reddit 공개 RSS/Atom 피드",
        CommunityInformationCollectionModes.OnDemandExternalResearch,
        "운영자가 지정한 공개 피드 요청 때만",
        "운영자가 지정한 공개 RSS/Atom 피드의 게시물 제목·짧은 설명·원문 링크만 검수 대기 후보로 반환합니다.",
        "https://www.reddit.com/dev/api/",
        true);

    public bool IsEnabled => _moduleOptions.Enabled && _providerOptions.Enabled;

    public SocialMediaResearchSourceDto Describe()
        => new(
            Source.SourceKey,
            Source.Provider,
            Source.DisplayName,
            Source.DocumentationUrl,
            IsEnabled,
            false,
            true);

    public async Task<IReadOnlyList<CommunityInformationCandidateDto>> SearchAsync(
        SocialMediaPublicContentQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureEnabled();

        var feedUrls = (_providerOptions.DefaultStartUrls ?? [])
            .Concat(query.StartUrls ?? [])
            .Select(NormalizeFeedUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_moduleOptions.MaxStartUrlsPerSource, 1, 20))
            .ToArray();
        if (feedUrls.Length == 0)
        {
            throw new ArgumentException("Reddit RSS 조사는 공개 subreddit 피드 URL이 필요합니다.");
        }

        var terms = (query.SearchTerms ?? [])
            .Select(value => SocialMediaJson.NormalizeText(value, 160))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        var take = Math.Clamp(
            query.Take,
            1,
            Math.Clamp(_moduleOptions.MaxItemsPerFeed, 1, 100));
        var candidates = new List<CommunityInformationCandidateDto>();

        foreach (var feedUrl in feedUrls)
        {
            var feedUri = new Uri(feedUrl, UriKind.Absolute);
            using var request = new HttpRequestMessage(HttpMethod.Get, feedUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            candidates.AddRange(ParseFeed(xml, feedUri, terms, query, take));
        }

        return candidates
            .GroupBy(item => item.CandidateKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.PublishedAtUtc ?? item.CollectedAtUtc)
            .Take(take)
            .ToArray();
    }

    private IReadOnlyList<CommunityInformationCandidateDto> ParseFeed(
        string xml,
        Uri feedUri,
        IReadOnlyList<string> searchTerms,
        SocialMediaPublicContentQuery query,
        int take)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var entries = document.Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .Take(Math.Clamp(_moduleOptions.MaxItemsPerFeed, 1, 100));
        var collectedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var candidates = new List<CommunityInformationCandidateDto>();

        foreach (var entry in entries)
        {
            var title = ElementText(entry, "title");
            var summary = ElementText(entry, "description", "summary", "content");
            if (!MatchesTerms(title, summary, searchTerms))
            {
                continue;
            }

            var originalUrl = ResolveLink(entry, feedUri);
            var normalizedUrl = SocialMediaJson.NormalizeHttpsUrl(originalUrl);
            if (normalizedUrl is null
                || !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var originalUri)
                || !IsAllowedHost(originalUri.Host))
            {
                continue;
            }

            var normalizedTitle = SocialMediaJson.NormalizeText(title, 200)
                                  ?? SocialMediaJson.BuildTitle(null, summary);
            var normalizedSummary = SocialMediaJson.NormalizeText(summary, 500)
                                    ?? normalizedTitle;
            if (normalizedTitle is null || normalizedSummary is null)
            {
                continue;
            }

            var id = SocialMediaJson.NormalizeText(
                         ElementText(entry, "id", "guid")
                         ?? normalizedUrl,
                         200)
                     ?? SocialMediaJson.StableId(normalizedUrl);
            var publishedAtUtc = ParseDateTime(
                ElementText(entry, "published", "updated", "pubDate"));
            var tags = entry.Elements()
                .Where(element => element.Name.LocalName is "category" or "tag")
                .Select(element => SocialMediaJson.NormalizeText(element.Value, 80))
                .Where(value => value is not null)
                .Cast<string>()
                .Take(8)
                .ToArray();

            candidates.Add(new CommunityInformationCandidateDto(
                $"{Source.SourceKey}:{id}",
                Source.SourceKey,
                CommunityInformationSourceTypes.SocialMedia,
                Source.Provider,
                normalizedTitle,
                normalizedSummary,
                normalizedUrl,
                null,
                publishedAtUtc,
                publishedAtUtc.HasValue ? DateOnly.FromDateTime(publishedAtUtc.Value) : null,
                collectedAtUtc,
                NormalizeCountryCode(query.CountryCode),
                NormalizeLanguageCode(query.LanguageCode),
                null,
                null,
                CommunityInformationReviewStates.PendingReview,
                tags,
                "Reddit 공개 RSS/Atom 피드에서 수집한 게시물의 짧은 발췌와 원문 링크입니다.",
                "피드 결과는 전체 Reddit 여론을 대표하지 않으며 작성자의 주장·신원·국가·상품 사실을 홍달이 확인했다는 뜻이 아닙니다."));

            if (candidates.Count >= take)
            {
                break;
            }
        }

        return candidates;
    }

    private static string NormalizeFeedUrl(string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !IsAllowedHost(uri.Host))
        {
            throw new ArgumentException($"Reddit 공개 HTTPS 피드 URL만 조사할 수 있습니다: {value}");
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith(".rss", StringComparison.OrdinalIgnoreCase))
        {
            path += "/.rss";
        }

        return new UriBuilder(uri) { Path = path }.Uri.AbsoluteUri;
    }

    private static string? ResolveLink(XElement entry, Uri feedUri)
    {
        var link = entry.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "link");
        var href = link?.Attribute("href")?.Value;
        var value = string.IsNullOrWhiteSpace(href) ? link?.Value : href;
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var absolute)
            ? absolute.AbsoluteUri
            : Uri.TryCreate(feedUri, value?.Trim(), out var relative)
                ? relative.AbsoluteUri
                : null;
    }

    private static string? ElementText(XElement element, params string[] names)
        => element.Elements()
            .FirstOrDefault(child => names.Contains(child.Name.LocalName, StringComparer.OrdinalIgnoreCase))?
            .Value;

    private static bool MatchesTerms(
        string? title,
        string? summary,
        IReadOnlyList<string> searchTerms)
        => searchTerms.Count == 0
           || searchTerms.Any(term =>
               (title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
               || (summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));

    private static DateTime? ParseDateTime(string? value)
        => DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var timestamp)
            ? timestamp.UtcDateTime
            : null;

    private static string NormalizeCountryCode(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized?.Length == 2 ? normalized : "ZZ";
    }

    private static string NormalizeLanguageCode(string? value)
        => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 20
            ? "und"
            : value.Trim();

    private static bool IsAllowedHost(string host)
        => AllowedHosts.Any(allowed =>
            string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{allowed}", StringComparison.OrdinalIgnoreCase));

    private void EnsureEnabled()
    {
        if (!_moduleOptions.Enabled)
        {
            throw new InvalidOperationException("무료 SNS 공개 피드 조사가 비활성화되어 있습니다.");
        }

        if (!_providerOptions.Enabled)
        {
            throw new InvalidOperationException("Reddit RSS 공개 피드 조사가 비활성화되어 있습니다.");
        }
    }
}
