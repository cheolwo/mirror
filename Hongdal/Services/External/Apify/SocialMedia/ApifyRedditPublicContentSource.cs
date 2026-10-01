using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify.SocialMedia;

public sealed class ApifyRedditPublicContentSource : ApifySocialMediaPublicContentSource
{
    private static readonly IReadOnlySet<string> Hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "reddit.com",
        "redd.it"
    };

    public ApifyRedditPublicContentSource(
        IApifyActorGateway gateway,
        IOptions<ApifySocialMediaOptions> options,
        TimeProvider timeProvider)
        : base(gateway, options.Value, options.Value.Reddit, timeProvider)
    {
    }

    public override CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.RedditPublicPosts,
        CommunityInformationSourceTypes.SocialMedia,
        "Reddit",
        "Reddit 공개 커뮤니티 게시물",
        CommunityInformationCollectionModes.OnDemandExternalResearch,
        "서버 관리자가 명시적으로 조사할 때만",
        "YouTube 영상과 핵심·인접 주제가 맞는 공개 게시물만 짧은 발췌와 링크로 모으고 자동 게시하지 않습니다.",
        "https://apify.com/trudax/reddit-scraper-lite",
        true);

    protected override IReadOnlySet<string> AllowedHosts => Hosts;

    protected override bool SupportsKeywordSearch => true;

    protected override bool RequiresStartUrl => false;

    protected override JsonElement BuildActorInput(SocialMediaPublicContentQuery query)
        => query.StartUrls.Count > 0
            ? JsonSerializer.SerializeToElement(new
            {
                startUrls = query.StartUrls.Select(url => new { url }).ToArray(),
                maxItems = query.Take,
                maxPostCount = query.Take,
                skipComments = true,
                skipCommunity = true,
                includeMediaLinks = true,
                includeNSFW = false
            })
            : JsonSerializer.SerializeToElement(new
            {
                searches = query.SearchTerms,
                maxItems = query.Take,
                maxPostCount = query.Take,
                searchPosts = true,
                searchComments = false,
                searchCommunities = false,
                searchUsers = false,
                skipComments = true,
                includeMediaLinks = true,
                includeNSFW = false,
                sort = "new",
                time = "all"
            });

    protected override CommunityInformationCandidateDto? MapItem(
        JsonElement item,
        SocialMediaPublicContentQuery query,
        DateTime collectedAtUtc)
    {
        var dataType = SocialMediaJson.GetString(item, "dataType");
        if (!string.IsNullOrWhiteSpace(dataType)
            && !string.Equals(dataType, "post", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var url = SocialMediaJson.GetString(item, "url", "postUrl", "permalink");
        if (url?.StartsWith("/", StringComparison.Ordinal) == true)
        {
            url = $"https://www.reddit.com{url}";
        }

        var community = SocialMediaJson.GetString(item, "communityName", "subreddit", "category");
        var tags = new[]
        {
            community,
            SocialMediaJson.GetString(item, "flair", "linkFlairText")
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>();
        return CreateCandidate(
            query,
            collectedAtUtc,
            SocialMediaJson.GetString(item, "id", "parsedId"),
            SocialMediaJson.GetString(item, "username", "author"),
            SocialMediaJson.GetString(item, "title"),
            SocialMediaJson.GetString(item, "body", "selfText", "selftext", "title"),
            url,
            SocialMediaJson.GetStringArray(item, "imageUrls").FirstOrDefault()
            ?? SocialMediaJson.GetString(item, "thumbnail", "thumbnailUrl", "imageUrl"),
            SocialMediaJson.GetDateTime(item, "createdAt", "created_utc", "timestamp"),
            query.LanguageCode,
            tags);
    }
}
