using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.External.Apify.SocialMedia;

public sealed class ApifyFacebookPublicContentSource : ApifySocialMediaPublicContentSource
{
    private static readonly IReadOnlySet<string> Hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "facebook.com",
        "fb.com"
    };

    public ApifyFacebookPublicContentSource(
        IApifyActorGateway gateway,
        IOptions<ApifySocialMediaOptions> options,
        TimeProvider timeProvider)
        : base(gateway, options.Value, options.Value.Facebook, timeProvider)
    {
    }

    public override CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.FacebookPublicPosts,
        CommunityInformationSourceTypes.SocialMedia,
        "Facebook",
        "Facebook 공개 페이지 게시물",
        CommunityInformationCollectionModes.OnDemandExternalResearch,
        "서버 관리자가 명시적으로 조사할 때만",
        "운영자가 지정한 공개 페이지에서 YouTube 영상의 인접 주제를 검토할 자료만 모으고 자동 게시하지 않습니다.",
        "https://apify.com/apify/facebook-posts-scraper",
        true);

    protected override IReadOnlySet<string> AllowedHosts => Hosts;

    protected override bool SupportsKeywordSearch => false;

    protected override bool RequiresStartUrl => true;

    protected override JsonElement BuildActorInput(SocialMediaPublicContentQuery query)
        => JsonSerializer.SerializeToElement(new
        {
            startUrls = query.StartUrls.Select(url => new { url }).ToArray(),
            resultsLimit = query.Take,
            captionText = false
        });

    protected override CommunityInformationCandidateDto? MapItem(
        JsonElement item,
        SocialMediaPublicContentQuery query,
        DateTime collectedAtUtc)
    {
        var text = SocialMediaJson.GetString(item, "text", "caption");
        var author = SocialMediaJson.GetNestedString(item, "user", "name")
                     ?? SocialMediaJson.GetString(item, "pageName", "userName");
        var thumbnail = SocialMediaJson.GetFirstArrayObjectString(
            item,
            "media",
            "thumbnail",
            "url");
        return CreateCandidate(
            query,
            collectedAtUtc,
            SocialMediaJson.GetString(item, "postId", "id"),
            author,
            SocialMediaJson.BuildTitle(author, text),
            text,
            SocialMediaJson.GetString(item, "url", "topLevelUrl"),
            thumbnail ?? SocialMediaJson.GetNestedString(item, "user", "profilePic"),
            SocialMediaJson.GetDateTime(item, "time", "timestamp", "createdAt"),
            query.LanguageCode,
            author is null ? [] : [author]);
    }
}
