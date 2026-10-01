using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify.SocialMedia;

public sealed class ApifyInstagramPublicContentSource : ApifySocialMediaPublicContentSource
{
    private static readonly IReadOnlySet<string> Hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "instagram.com"
    };

    public ApifyInstagramPublicContentSource(
        IApifyActorGateway gateway,
        IOptions<ApifySocialMediaOptions> options,
        TimeProvider timeProvider)
        : base(gateway, options.Value, options.Value.Instagram, timeProvider)
    {
    }

    public override CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.InstagramPublicPosts,
        CommunityInformationSourceTypes.SocialMedia,
        "Instagram",
        "Instagram 공개 게시물",
        CommunityInformationCollectionModes.OnDemandExternalResearch,
        "서버 관리자가 명시적으로 조사할 때만",
        "YouTube 영상과 관련된 공개 해시태그의 게시물을 짧은 발췌와 링크로 모으고 자동 게시하지 않습니다.",
        "https://apify.com/apify/instagram-scraper",
        true);

    protected override IReadOnlySet<string> AllowedHosts => Hosts;

    protected override bool SupportsKeywordSearch => true;

    protected override bool RequiresStartUrl => false;

    protected override JsonElement BuildActorInput(SocialMediaPublicContentQuery query)
        => query.StartUrls.Count > 0
            ? JsonSerializer.SerializeToElement(new
            {
                directUrls = query.StartUrls,
                resultsType = "posts",
                resultsLimit = query.Take,
                skipPinnedPosts = true
            })
            : JsonSerializer.SerializeToElement(new
            {
                search = BuildHashtagSearch(query.SearchTerms[0]),
                searchType = "hashtag",
                searchLimit = 1,
                resultsType = "posts",
                resultsLimit = query.Take,
                skipPinnedPosts = true
            });

    protected override CommunityInformationCandidateDto? MapItem(
        JsonElement item,
        SocialMediaPublicContentQuery query,
        DateTime collectedAtUtc)
    {
        var caption = SocialMediaJson.GetString(item, "caption", "text", "alt");
        var author = SocialMediaJson.GetString(item, "ownerUsername", "username", "ownerFullName");
        var images = SocialMediaJson.GetStringArray(item, "images", "carouselImages");
        var tags = SocialMediaJson.GetStringArray(item, "hashtags");
        return CreateCandidate(
            query,
            collectedAtUtc,
            SocialMediaJson.GetString(item, "id", "shortCode"),
            author is null ? null : $"@{author.TrimStart('@')}",
            SocialMediaJson.BuildTitle(author, caption),
            caption,
            SocialMediaJson.GetString(item, "url", "inputUrl"),
            SocialMediaJson.GetString(item, "displayUrl", "image") ?? images.FirstOrDefault(),
            SocialMediaJson.GetDateTime(item, "timestamp", "taken_at", "createdAt"),
            query.LanguageCode,
            tags);
    }

    private static string BuildHashtagSearch(string term)
    {
        var hashtag = new string(term
            .Where(character => char.IsLetterOrDigit(character) || character == '_')
            .ToArray());
        if (hashtag.Length == 0)
        {
            throw new ArgumentException("Instagram 해시태그로 변환할 수 있는 검색어가 필요합니다.");
        }

        return $"#{hashtag}";
    }
}
