using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.External.Apify;
using Hongdal.Services.External.Apify.YouTube;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class ApifyYouTubeTranscriptSourceTests
{
    [Fact]
    public async Task GetAsync_searchResult세그먼트를_전사응답으로_정규화한다()
    {
        var gateway = new CapturingGateway(
            """
            [
              {
                "videoUrl": "https://www.youtube.com/watch?v=abc123_DEF-1",
                "transcript": [],
                "searchResult": [
                  { "start": "0.320", "dur": "4.080", "text": "First line" },
                  { "start": 4.4, "dur": 2.1, "text": "Second line" }
                ]
              }
            ]
            """);
        var source = CreateSource(gateway);

        var result = await source.GetAsync(
            new YouTubeTranscriptRequest("abc123_DEF-1", "ko"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("abc123_DEF-1", result.VideoId);
        Assert.Equal("ko", result.LanguageCode);
        Assert.Equal("First line Second line", result.Transcript);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(0.320m, result.Segments[0].StartSeconds);
        Assert.Equal(4.080m, result.Segments[0].DurationSeconds);

        var request = Assert.IsType<ApifyActorSyncRequest>(gateway.Request);
        Assert.Equal("pintostudio~youtube-transcript-scraper", request.ActorId);
        Assert.Equal(
            "https://www.youtube.com/watch?v=abc123_DEF-1",
            request.Input.GetProperty("videoUrl").GetString());
        Assert.Equal("ko", request.Input.GetProperty("targetLanguage").GetString());
    }

    [Fact]
    public async Task GetAsync_transcript배열도_지원한다()
    {
        var gateway = new CapturingGateway(
            """
            [
              {
                "transcript": [
                  { "start": "1", "duration": "3", "text": "  A   normalized line  " }
                ]
              }
            ]
            """);
        var source = CreateSource(gateway);

        var result = await source.GetAsync(
            new YouTubeTranscriptRequest("abc123_DEF-1"),
            CancellationToken.None);

        var segment = Assert.Single(result!.Segments);
        Assert.Equal("en", result.LanguageCode);
        Assert.Equal("A normalized line", segment.Text);
        Assert.Equal(1m, segment.StartSeconds);
        Assert.Equal(3m, segment.DurationSeconds);
    }

    [Fact]
    public async Task GetAsync_자막이_없으면_null을_반환한다()
    {
        var gateway = new CapturingGateway("[{ \"videoUrl\": \"https://www.youtube.com/watch?v=abc123_DEF-1\" }]");
        var source = CreateSource(gateway);

        var result = await source.GetAsync(
            new YouTubeTranscriptRequest("abc123_DEF-1"),
            CancellationToken.None);

        Assert.Null(result);
    }

    private static ApifyYouTubeTranscriptSource CreateSource(CapturingGateway gateway)
        => new(
            gateway,
            Options.Create(new ApifyYouTubeTranscriptOptions
            {
                Enabled = true,
                ActorId = "pintostudio~youtube-transcript-scraper",
                DefaultTargetLanguage = "en"
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero)));

    private sealed class CapturingGateway : IApifyActorGateway
    {
        private readonly string _body;

        public CapturingGateway(string body) => _body = body;

        public ApifyActorSyncRequest? Request { get; private set; }

        public Task<ApifyActorSyncResult> RunSyncGetDatasetItemsAsync(
            ApifyActorSyncRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            using var document = JsonDocument.Parse(_body);
            var items = document.RootElement
                .EnumerateArray()
                .Select(item => item.Clone())
                .ToArray();
            return Task.FromResult(new ApifyActorSyncResult(request.ActorId, items));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
