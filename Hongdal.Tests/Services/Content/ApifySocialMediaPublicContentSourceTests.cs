using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.External.Apify;
using Hongdal.Services.External.Apify.SocialMedia;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class ApifySocialMediaPublicContentSourceTests
{
    private static readonly DateTimeOffset CollectedAt =
        new(2026, 7, 18, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task Reddit_게시물만_짧은_검토후보로_정규화한다()
    {
        var gateway = new CapturingGateway(
            """
            [
              {
                "dataType": "post",
                "id": "reddit-1",
                "url": "https://www.reddit.com/r/food/comments/reddit-1/topic/",
                "username": "cook_user",
                "title": "A shared order idea",
                "body": "Neighbors compare a bulk order before deciding.",
                "imageUrls": ["https://images.example.test/reddit.jpg"],
                "communityName": "food",
                "createdAt": "2026-07-17T12:00:00Z"
              },
              {
                "dataType": "comment",
                "id": "comment-1",
                "url": "https://www.reddit.com/r/food/comments/reddit-1/topic/comment-1/",
                "body": "A comment must not become a candidate."
              }
            ]
            """);
        var source = new ApifyRedditPublicContentSource(
            gateway,
            CreateOptions(options => options.Reddit.Enabled = true),
            new FixedTimeProvider(CollectedAt));

        var result = await source.SearchAsync(
            Query(["shared order", "food import"]),
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(CommunityInformationSourceKeys.RedditPublicPosts, item.SourceKey);
        Assert.Equal(CommunityInformationReviewStates.PendingReview, item.ReviewState);
        Assert.Equal("https://www.reddit.com/r/food/comments/reddit-1/topic/", item.OriginalUrl);
        Assert.Equal(CollectedAt.UtcDateTime, item.CollectedAtUtc);

        var request = Assert.IsType<ApifyActorSyncRequest>(gateway.Request);
        Assert.Equal("trudax~reddit-scraper-lite", request.ActorId);
        Assert.True(request.Input.GetProperty("skipComments").GetBoolean());
        Assert.False(request.Input.GetProperty("includeNSFW").GetBoolean());
        Assert.False(request.Input.GetProperty("searchComments").GetBoolean());
        Assert.Equal("shared order", request.Input.GetProperty("searches")[0].GetString());
    }

    [Fact]
    public async Task Reddit_공개_URL_조사는_Lite_Actor에_시작_URL만_전달한다()
    {
        var gateway = new CapturingGateway("[]");
        var source = new ApifyRedditPublicContentSource(
            gateway,
            CreateOptions(options => options.Reddit.Enabled = true),
            new FixedTimeProvider(CollectedAt));

        await source.SearchAsync(
            Query(
                ["이 검색어는 URL 조사 입력에 섞이지 않아야 한다"],
                ["https://www.reddit.com/r/localfood/new/"]),
            CancellationToken.None);

        var request = Assert.IsType<ApifyActorSyncRequest>(gateway.Request);
        Assert.Equal("trudax~reddit-scraper-lite", request.ActorId);
        Assert.Equal(
            "https://www.reddit.com/r/localfood/new/",
            request.Input.GetProperty("startUrls")[0].GetProperty("url").GetString());
        Assert.False(request.Input.TryGetProperty("searches", out _));
        Assert.True(request.Input.GetProperty("skipComments").GetBoolean());
        Assert.False(request.Input.GetProperty("includeNSFW").GetBoolean());
    }

    [Fact]
    public async Task X_작성자와_원문을_검토후보로_정규화한다()
    {
        var gateway = new CapturingGateway(
            """
            [
              {
                "id": "x-1",
                "url": "https://x.com/community_user/status/1",
                "text": "People are discussing a neighborhood group order.",
                "createdAt": "2026-07-17T13:00:00Z",
                "lang": "en",
                "author": {
                  "userName": "community_user",
                  "name": "Community User",
                  "profilePicture": "https://images.example.test/x.jpg"
                }
              }
            ]
            """);
        var source = new ApifyXPublicContentSource(
            gateway,
            CreateOptions(options => options.X.Enabled = true),
            new FixedTimeProvider(CollectedAt));

        var item = Assert.Single(await source.SearchAsync(
            Query(["neighborhood group order"]),
            CancellationToken.None));

        Assert.Equal(CommunityInformationSourceKeys.XPublicPosts, item.SourceKey);
        Assert.Contains("@community_user", item.Provider, StringComparison.Ordinal);
        Assert.Equal("en", item.LanguageCode);
        Assert.Equal("https://x.com/community_user/status/1", item.OriginalUrl);

        var request = Assert.IsType<ApifyActorSyncRequest>(gateway.Request);
        Assert.Equal("Latest", request.Input.GetProperty("sort").GetString());
        Assert.Equal("neighborhood group order", request.Input.GetProperty("searchTerms")[0].GetString());
    }

    [Fact]
    public async Task Instagram_첫_주제를_공개_해시태그로_검색한다()
    {
        var gateway = new CapturingGateway(
            """
            [
              {
                "id": "instagram-1",
                "url": "https://www.instagram.com/p/ABC123xyz/",
                "caption": "A local food group order is taking shape.",
                "ownerUsername": "local_food",
                "displayUrl": "https://images.example.test/instagram.jpg",
                "timestamp": "2026-07-17T14:00:00Z",
                "hashtags": ["grouporder", "localfood"]
              }
            ]
            """);
        var source = new ApifyInstagramPublicContentSource(
            gateway,
            CreateOptions(options => options.Instagram.Enabled = true),
            new FixedTimeProvider(CollectedAt));

        var item = Assert.Single(await source.SearchAsync(
            Query(["Korean ramen", "shared import"]),
            CancellationToken.None));

        Assert.Equal(CommunityInformationSourceKeys.InstagramPublicPosts, item.SourceKey);
        Assert.Equal("https://www.instagram.com/p/ABC123xyz/", item.OriginalUrl);

        var input = Assert.IsType<ApifyActorSyncRequest>(gateway.Request).Input;
        Assert.Equal("#Koreanramen", input.GetProperty("search").GetString());
        Assert.Equal("hashtag", input.GetProperty("searchType").GetString());
        Assert.Equal(1, input.GetProperty("searchLimit").GetInt32());
    }

    [Fact]
    public async Task Facebook_공개페이지_URL이_없으면_외부호출하지_않는다()
    {
        var gateway = new CapturingGateway("[]");
        var source = new ApifyFacebookPublicContentSource(
            gateway,
            CreateOptions(options => options.Facebook.Enabled = true),
            new FixedTimeProvider(CollectedAt));

        var action = () => source.SearchAsync(
            Query(["local market"]),
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(action);
        Assert.Null(gateway.Request);
    }

    [Fact]
    public async Task Facebook_운영자가_지정한_공개페이지만_조사한다()
    {
        var gateway = new CapturingGateway(
            """
            [
              {
                "postId": "facebook-1",
                "url": "https://www.facebook.com/localmarket/posts/1",
                "text": "The market is preparing a joint produce order.",
                "time": "2026-07-17T15:00:00Z",
                "user": {
                  "name": "Local Market",
                  "profilePic": "https://images.example.test/facebook.jpg"
                }
              }
            ]
            """);
        var source = new ApifyFacebookPublicContentSource(
            gateway,
            CreateOptions(options => options.Facebook.Enabled = true),
            new FixedTimeProvider(CollectedAt));
        var query = Query(
            ["local market"],
            ["https://www.facebook.com/localmarket/"]);

        var item = Assert.Single(await source.SearchAsync(query, CancellationToken.None));

        Assert.Equal(CommunityInformationSourceKeys.FacebookPublicPosts, item.SourceKey);
        Assert.Equal("https://www.facebook.com/localmarket/posts/1", item.OriginalUrl);
        var input = Assert.IsType<ApifyActorSyncRequest>(gateway.Request).Input;
        Assert.Equal(
            "https://www.facebook.com/localmarket/",
            input.GetProperty("startUrls")[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Reddit_원천과_무관한_원문링크는_후보로_만들지않는다()
    {
        var gateway = new CapturingGateway(
            """
            [
              {
                "dataType": "post",
                "id": "reddit-unsafe",
                "url": "https://example.com/not-reddit",
                "title": "Unrelated result",
                "body": "This link must not be published as Reddit content."
              }
            ]
            """);
        var source = new ApifyRedditPublicContentSource(
            gateway,
            CreateOptions(options => options.Reddit.Enabled = true),
            new FixedTimeProvider(CollectedAt));

        var result = await source.SearchAsync(
            Query(["food"]),
            CancellationToken.None);

        Assert.Empty(result);
    }

    private static SocialMediaPublicContentQuery Query(
        IReadOnlyList<string> terms,
        IReadOnlyList<string>? startUrls = null)
        => new(terms, startUrls ?? [], 5, "US", "en");

    private static IOptions<ApifySocialMediaOptions> CreateOptions(
        Action<ApifySocialMediaOptions> configure)
    {
        var options = new ApifySocialMediaOptions
        {
            Enabled = true
        };
        configure(options);
        return Options.Create(options);
    }

    private sealed class CapturingGateway : IApifyActorGateway
    {
        private readonly IReadOnlyList<JsonElement> _items;

        public CapturingGateway(string json)
        {
            using var document = JsonDocument.Parse(json);
            _items = document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
        }

        public ApifyActorSyncRequest? Request { get; private set; }

        public Task<ApifyActorSyncResult> RunSyncGetDatasetItemsAsync(
            ApifyActorSyncRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new ApifyActorSyncResult(request.ActorId, _items));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
