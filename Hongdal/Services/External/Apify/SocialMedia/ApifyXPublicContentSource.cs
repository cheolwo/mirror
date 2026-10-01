using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify.SocialMedia;

public sealed class ApifyXPublicContentSource : ApifySocialMediaPublicContentSource
{
    private static readonly IReadOnlySet<string> Hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "x.com",
        "twitter.com"
    };

    public ApifyXPublicContentSource(
        IApifyActorGateway gateway,
        IOptions<ApifySocialMediaOptions> options,
        TimeProvider timeProvider)
        : base(gateway, options.Value, options.Value.X, timeProvider)
    {
    }

    public override CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.XPublicPosts,
        CommunityInformationSourceTypes.SocialMedia,
        "X",
        "X 공개 게시물",
        CommunityInformationCollectionModes.OnDemandExternalResearch,
        "서버 관리자가 명시적으로 조사할 때만",
        "YouTube 영상의 핵심·인접 주제 검색 결과를 짧은 발췌와 링크로 모으고 자동 게시하지 않습니다.",
        "https://apify.com/apidojo/twitter-scraper-lite",
        true);

    protected override IReadOnlySet<string> AllowedHosts => Hosts;

    protected override bool SupportsKeywordSearch => true;

    protected override bool RequiresStartUrl => false;

    protected override JsonElement BuildActorInput(SocialMediaPublicContentQuery query)
        => query.StartUrls.Count > 0
            ? JsonSerializer.SerializeToElement(new
            {
                startUrls = query.StartUrls,
                maxItems = query.Take,
                sort = "Latest",
                includeSearchTerms = true
            })
            : JsonSerializer.SerializeToElement(new
            {
                searchTerms = query.SearchTerms,
                maxItems = query.Take,
                sort = "Latest",
                tweetLanguage = query.LanguageCode == "und" ? null : query.LanguageCode.Split('-')[0],
                includeSearchTerms = true
            });

    protected override CommunityInformationCandidateDto? MapItem(
        JsonElement item,
        SocialMediaPublicContentQuery query,
        DateTime collectedAtUtc)
    {
        var userName = SocialMediaJson.GetNestedString(item, "author", "userName", "username");
        var authorName = SocialMediaJson.GetNestedString(item, "author", "name") ?? userName;
        var authorLabel = userName is null ? authorName : $"@{userName}";
        var text = SocialMediaJson.GetString(item, "text", "fullText", "content");
        var articleTitle = SocialMediaJson.GetNestedString(item, "article", "title");
        var articleImage = SocialMediaJson.GetNestedString(item, "article", "coverImage");
        var language = SocialMediaJson.GetString(item, "lang", "language") ?? query.LanguageCode;
        return CreateCandidate(
            query,
            collectedAtUtc,
            SocialMediaJson.GetString(item, "id", "tweetId"),
            authorLabel,
            articleTitle ?? SocialMediaJson.BuildTitle(authorLabel, text),
            text ?? SocialMediaJson.GetNestedString(item, "article", "previewText"),
            SocialMediaJson.GetString(item, "url", "twitterUrl"),
            articleImage ?? SocialMediaJson.GetNestedString(item, "author", "profilePicture"),
            SocialMediaJson.GetDateTime(item, "createdAt", "timestamp"),
            language,
            []);
    }
}
