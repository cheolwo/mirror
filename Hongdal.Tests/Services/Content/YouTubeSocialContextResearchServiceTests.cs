using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Content;
using Hongdal.Services.External.Apify.SocialMedia;
using 홍달.Services.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTubeSocialContextResearchServiceTests
{
    [Fact]
    public async Task ResearchAsync_여러SNS를합치고_한원천실패를_응답에남긴다()
    {
        var video = CreateVideo();
        var sources = new ISocialMediaPublicContentSource[]
        {
            new StubSource(
                CreateSource("reddit-public-posts", "Reddit"),
                _ => Task.FromResult<IReadOnlyList<CommunityInformationCandidateDto>>(
                    [CreateCandidate("reddit-public-posts", "reddit-1", "Reddit post")])),
            new StubSource(
                CreateSource("x-public-posts", "X"),
                _ => throw new InvalidOperationException("provider unavailable"))
        };
        var sut = new YouTubeSocialContextResearchService(
            sources,
            new StubVideoSource(video),
            new StubComposer(),
            Options.Create(new ApifySocialMediaOptions { Enabled = true }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero)),
            NullLogger<YouTubeSocialContextResearchService>.Instance);

        var result = await sut.ResearchAsync(
            new YouTubeSocialContextResearchRequest
            {
                VideoId = video.VideoId,
                SourceKeys = ["reddit-public-posts", "x-public-posts"],
                SearchTerms = ["group order"],
                AdjacentTopics = ["local food"],
                TakePerSource = 4
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("reddit-public-posts", item.SourceKey);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("x-public-posts", failure.SourceKey);
        Assert.Contains("provider unavailable", failure.Message, StringComparison.Ordinal);
        Assert.Equal("[draft] Video title", result.Draft.Title);
        Assert.Equal("공동구매", result.Draft.CollectiveAction.WorkflowTag);
        Assert.Equal(CommunityCollectiveIntentTypeCodes.GroupPurchase, result.Draft.CollectiveAction.PrimaryIntentTypeCode);
    }

    [Fact]
    public async Task ResearchAsync_원천을선택하지않으면_활성화된원천을사용한다()
    {
        var video = CreateVideo();
        var source = new StubSource(
            CreateSource("reddit-public-posts", "Reddit"),
            _ => Task.FromResult<IReadOnlyList<CommunityInformationCandidateDto>>([]));
        var sut = CreateService(video, [source]);

        var result = await sut.ResearchAsync(
            new YouTubeSocialContextResearchRequest { VideoId = video.VideoId },
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Empty(result.Failures);
        Assert.Contains(result.Sources, item => item.SourceKey == "reddit-public-posts");
    }

    private static YouTubeSocialContextResearchService CreateService(
        YouTubeSocialContextVideoDto video,
        IReadOnlyList<ISocialMediaPublicContentSource> sources)
        => new(
            sources,
            new StubVideoSource(video),
            new StubComposer(),
            Options.Create(new ApifySocialMediaOptions { Enabled = true }),
            TimeProvider.System,
            NullLogger<YouTubeSocialContextResearchService>.Instance);

    private static YouTubeSocialContextVideoDto CreateVideo()
        => new(
            "video-1",
            "Food channel",
            "Video title",
            "Video summary",
            "https://www.youtube.com/watch?v=video-1",
            null,
            new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc),
            "US",
            "en");

    private static SocialMediaResearchSourceDto CreateSource(string key, string provider)
        => new(key, provider, provider, $"https://{provider.ToLowerInvariant()}.example.com/docs", true, true, false);

    private static CommunityInformationCandidateDto CreateCandidate(string sourceKey, string id, string title)
        => new(
            $"{sourceKey}:{id}",
            sourceKey,
            CommunityInformationSourceTypes.SocialMedia,
            sourceKey,
            title,
            "Summary",
            $"https://{sourceKey}.example.com/{id}",
            null,
            new DateTime(2026, 7, 17, 13, 0, 0, DateTimeKind.Utc),
            null,
            new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
            "US",
            "en",
            null,
            null,
            CommunityInformationReviewStates.PendingReview,
            [],
            "Source notice",
            "Limitations");

    private sealed class StubSource : ISocialMediaPublicContentSource
    {
        private readonly Func<SocialMediaPublicContentQuery, Task<IReadOnlyList<CommunityInformationCandidateDto>>> _search;

        public StubSource(
            SocialMediaResearchSourceDto description,
            Func<SocialMediaPublicContentQuery, Task<IReadOnlyList<CommunityInformationCandidateDto>>> search)
        {
            Source = new CommunityInformationSourceDto(
                description.SourceKey,
                CommunityInformationSourceTypes.SocialMedia,
                description.Provider,
                description.DisplayName,
                CommunityInformationCollectionModes.OnDemandExternalResearch,
                "on demand",
                "review required",
                description.DocumentationUrl,
                true);
            Description = description;
            _search = search;
        }

        private SocialMediaResearchSourceDto Description { get; }

        public CommunityInformationSourceDto Source { get; }

        public bool IsEnabled => true;

        public SocialMediaResearchSourceDto Describe() => Description;

        public Task<IReadOnlyList<CommunityInformationCandidateDto>> SearchAsync(
            SocialMediaPublicContentQuery query,
            CancellationToken cancellationToken)
            => _search(query);
    }

    private sealed class StubVideoSource : IYouTubeSocialContextVideoSource
    {
        private readonly YouTubeSocialContextVideoDto _video;

        public StubVideoSource(YouTubeSocialContextVideoDto video) => _video = video;

        public Task<YouTubeSocialContextVideoDto?> GetAsync(
            string videoId,
            CancellationToken cancellationToken)
            => Task.FromResult<YouTubeSocialContextVideoDto?>(
                string.Equals(videoId, _video.VideoId, StringComparison.Ordinal) ? _video : null);
    }

    private sealed class StubComposer : IYouTubeSocialContextPostComposer
    {
        public YouTubeSocialContextPostDraftDto Compose(
            YouTubeSocialContextVideoDto video,
            IReadOnlyList<string> searchTerms,
            IReadOnlyList<string> adjacentTopics,
            IReadOnlyList<CommunityInformationCandidateDto> items)
            => new(
                "[draft] " + video.Title,
                string.Join('|', searchTerms),
                new(
                    "공동구매",
                    CommunityCollectiveIntentTypeCodes.GroupPurchase,
                    [CommunityCollectiveIntentTypeCodes.GroupPurchase],
                    "prompt",
                    "notice",
                    "/api/v1/community/posts/{postId}/opportunities"));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
