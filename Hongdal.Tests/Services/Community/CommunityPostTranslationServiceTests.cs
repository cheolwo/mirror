using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Localization;
using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Infrastructure.Security;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityPostTranslationServiceTests
{
    [Fact]
    public async Task AzureProvider_SendsKeyRegionAndTwoTextSegments()
    {
        var handler = new RecordingHttpHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.cognitive.microsofttranslator.com/")
        };
        var provider = new AzureCommunityTextTranslationProvider(
            client,
            Options.Create(new CommunityPostTranslationOptions
            {
                Enabled = true,
                ApiKey = "translator-key",
                Region = "koreacentral"
            }));

        var result = await provider.TranslateAsync(
            "제목",
            "본문",
            DisplayLanguageCodes.Korean,
            DisplayLanguageCodes.English,
            default);

        Assert.Equal("Translated title", result.Title);
        Assert.Contains("from=ko", handler.RequestUri);
        Assert.Contains("to=en", handler.RequestUri);
        Assert.Equal("translator-key", handler.ApiKey);
        Assert.Equal("koreacentral", handler.Region);
        using var requestJson = System.Text.Json.JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("제목", requestJson.RootElement[0].GetProperty("Text").GetString());
        Assert.Equal("본문", requestJson.RootElement[1].GetProperty("Text").GetString());
    }

    [Theory]
    [InlineData(null, "같이 장을 봐요", "양파와 감자를 공동구매합니다.", DisplayLanguageCodes.Korean)]
    [InlineData(null, "Local food market", "Let us buy onions together.", DisplayLanguageCodes.English)]
    [InlineData("en", "한국어 제목", "한국어 본문", DisplayLanguageCodes.English)]
    public void LanguageResolver_UsesExplicitLanguageThenDetectsSupportedScript(
        string? requested,
        string title,
        string body,
        string expected)
    {
        Assert.Equal(expected, CommunityPostLanguageResolver.Resolve(requested, title, body));
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesTranslationByPostLanguageAndContent()
    {
        await using var context = CreateContext();
        var post = CreatePost();
        context.PlatformCommunityPosts.Add(post);
        await context.SaveChangesAsync();
        var provider = new RecordingTranslationProvider();
        var service = CreateService(context, provider);

        var first = await service.GetOrCreateAsync(post.Id, DisplayLanguageCodes.English, default);
        var second = await service.GetOrCreateAsync(post.Id, DisplayLanguageCodes.English, default);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, provider.CallCount);
        Assert.False(first.Value.IsCached);
        Assert.True(second.Value.IsCached);
        Assert.Equal("Translated title", second.Value.TranslatedTitle);
        Assert.Single(await context.PlatformCommunityPostTranslations.ToListAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_DoesNotSendReportPostToExternalProviderByDefault()
    {
        await using var context = CreateContext();
        var post = CreatePost();
        post.Category = "신고/분쟁";
        post.IsReportBoardPost = true;
        context.PlatformCommunityPosts.Add(post);
        await context.SaveChangesAsync();
        var provider = new RecordingTranslationProvider();
        var service = CreateService(context, provider);

        var result = await service.GetOrCreateAsync(post.Id, DisplayLanguageCodes.English, default);

        Assert.True(result.IsFailed);
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(403, result.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task GetOrCreateAsync_ExcludesEvidenceChartBlockFromExternalTranslation()
    {
        await using var context = CreateContext();
        var post = CreatePost();
        post.Body = string.Join(
            Environment.NewLine,
            "공동구매 수치를 함께 확인합니다.",
            string.Empty,
            CommunityEvidenceChartTextCodec.Encode(new CommunityEvidenceChartBlock
            {
                ChartTypeCode = CommunityEvidenceChartTypeCodes.Bar,
                Title = "역할별 순편익",
                Claim = "각 역할의 편익과 부담을 비교합니다.",
                SeriesLabel = "순편익",
                Unit = "KRW",
                SourceLabel = "작성 중인 검토",
                ReferenceDate = "2026-07-19",
                Interpretation = "입력된 역할의 순편익이 모두 양수입니다.",
                Limitation = "작성자 추정값이며 실제 조건을 다시 확인해야 합니다.",
                Points = [new("구매자", 10_000m), new("공급자", 12_000m)]
            }));
        context.PlatformCommunityPosts.Add(post);
        await context.SaveChangesAsync();
        var provider = new RecordingTranslationProvider();
        var service = CreateService(context, provider);

        var result = await service.GetOrCreateAsync(post.Id, DisplayLanguageCodes.English, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("공동구매 수치를 함께 확인합니다.", provider.LastBody);
        Assert.DoesNotContain(CommunityEvidenceChartTextCodec.StartMarker, provider.LastBody, StringComparison.Ordinal);
    }

    private static CommunityPostTranslationService CreateService(
        HongdalContext context,
        ICommunityTextTranslationProvider provider)
        => new(
            context,
            provider,
            Options.Create(new CommunityPostTranslationOptions
            {
                Enabled = true,
                ApiKey = "test-key"
            }),
            NullLogger<CommunityPostTranslationService>.Instance);

    private static PlatformCommunityPost CreatePost()
        => new()
        {
            AppKey = "platform",
            Category = "자유",
            WorkflowTag = "공동구매",
            RoleTag = "플랫폼 구성원",
            Title = "같이 장을 봐요",
            Body = "양파와 감자를 공동구매합니다.",
            OriginalLanguageCode = DisplayLanguageCodes.Korean,
            Nickname = "테스터",
            PasswordHash = "hash"
        };

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"community-post-translation-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class RecordingTranslationProvider : ICommunityTextTranslationProvider
    {
        public bool IsAvailable => true;
        public int CallCount { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        public Task<CommunityTextTranslationResult> TranslateAsync(
            string title,
            string body,
            string sourceLanguageCode,
            string targetLanguageCode,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastBody = body;
            return Task.FromResult(new CommunityTextTranslationResult(
                "Translated title",
                "Translated body",
                "TestProvider",
                "test-v1"));
        }
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public string RequestUri { get; private set; } = string.Empty;
        public string ApiKey { get; private set; } = string.Empty;
        public string Region { get; private set; } = string.Empty;
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString() ?? string.Empty;
            ApiKey = request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single();
            Region = request.Headers.GetValues("Ocp-Apim-Subscription-Region").Single();
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[{\"translations\":[{\"text\":\"Translated title\",\"to\":\"en\"}]}," +
                    "{\"translations\":[{\"text\":\"Translated body\",\"to\":\"en\"}]}]",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
