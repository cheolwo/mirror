using System.Net;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.External.Free.SocialMedia;
using 홍달.Services.Options;
using Microsoft.Extensions.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class RedditRssPublicContentSourceTests
{
    [Fact]
    public async Task SearchAsync_공개피드를_읽어_검수대기후보로_변환한다()
    {
        var handler = new StubHandler(
            """
            <rss version="2.0">
              <channel>
                <item>
                  <title>Neighbors plan a food group order</title>
                  <description>People compare local food prices before deciding.</description>
                  <link>https://www.reddit.com/r/food/comments/post-1/group-order/</link>
                  <guid>t3_post-1</guid>
                  <pubDate>Fri, 17 Jul 2026 13:00:00 GMT</pubDate>
                  <category>group-order</category>
                </item>
                <item>
                  <title>Unrelated game discussion</title>
                  <description>A different topic.</description>
                  <link>https://www.reddit.com/r/food/comments/post-2/game/</link>
                  <guid>t3_post-2</guid>
                </item>
              </channel>
            </rss>
            """);
        var source = CreateSource(handler);

        var result = await source.SearchAsync(
            new Hongdal.Services.External.Apify.SocialMedia.SocialMediaPublicContentQuery(
                ["food prices"],
                ["https://www.reddit.com/r/food/new/.rss"],
                10,
                "US",
                "en"),
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(CommunityInformationSourceKeys.RedditRssPublicPosts, item.SourceKey);
        Assert.Equal(CommunityInformationReviewStates.PendingReview, item.ReviewState);
        Assert.Equal("https://www.reddit.com/r/food/comments/post-1/group-order/", item.OriginalUrl);
        Assert.Contains("group-order", item.TopicTags, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.EndsWith("/r/food/new/.rss", handler.RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_Reddit가_아닌_피드는_외부호출전에_거절한다()
    {
        var handler = new StubHandler("<rss />");
        var source = CreateSource(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => source.SearchAsync(
            new Hongdal.Services.External.Apify.SocialMedia.SocialMediaPublicContentQuery(
                [],
                ["https://example.com/feed.rss"],
                10,
                "US",
                "en"),
            CancellationToken.None));

        Assert.Null(handler.RequestUri);
    }

    private static RedditRssPublicContentSource CreateSource(StubHandler handler)
        => new(
            new HttpClient(handler),
            Options.Create(new FreeSocialMediaOptions
            {
                Enabled = true,
                RedditRss = new RedditRssPublicContentOptions { Enabled = true }
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero)));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;

        public StubHandler(string body) => _body = body;

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body)
            });
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
