using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Extensions;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.음식;
using Ssalddel.Contracts.Common.Drivers;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Fact]
    public async Task OllamaApplication_유효한대안은실제추천저장과요금감사에반영되고재호출은중복저장하지않는다()
    {
        using var http = new HttpClient(new ApplicationResponseHandler("C2"));
        var evidence = await RunOllamaApplicationAsync(http);
        Assert.Equal("application-near", evidence.RecommendedDriverId);
        Assert.Equal("application-older", evidence.BaselineDriverId);
        Assert.Equal(1, evidence.ModelRequests);
        Assert.True(evidence.AISelectionApplied);
        Assert.Equal(1, evidence.RecommendationRound);
        Assert.Equal(1, evidence.AuditEvents);
        Assert.Equal(1, evidence.ActivityEvents);
        Assert.Equal(1, evidence.NotificationRequestsSuppressed);
        Assert.Equal(4000m, evidence.FrozenFee);
        Assert.False(evidence.SecondTransitionChanged);
    }

    [Fact]
    public async Task OllamaApplication_알수없는토큰은실제추천에서기존기사를유지한다()
    {
        using var http = new HttpClient(new ApplicationResponseHandler("invented"));
        var evidence = await RunOllamaApplicationAsync(http);
        Assert.Equal("application-older", evidence.RecommendedDriverId);
        Assert.False(evidence.AISelectionApplied);
        Assert.Contains("InvalidResponse", evidence.Reason);
        Assert.Equal(1, evidence.AuditEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OllamaApplication_DI는기본비활성과명시활성을각각연결한다(bool enabled)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["FoodDispatchOllama:Enabled"] = enabled.ToString() }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSsalddelDomainServices();
        using var provider = services.BuildServiceProvider();
        var ai = provider.GetRequiredService<IFoodDispatchCandidateAI>();
        Assert.IsType<OllamaFoodDispatchCandidateAI>(ai);
        Assert.Equal(enabled, ai.Enabled);
        Assert.Equal(10, provider.GetRequiredService<IOptions<FoodDispatchOllamaOptions>>().Value.TimeoutSeconds);
    }

    // Explicit tool entry only. Normal tests pass a mock HttpClient and never call a running model.
    public static async Task<FoodDispatchApplicationEvidence> RunOllamaApplicationAsync(HttpClient http, string? databasePath = null)
    {
        var counter = new ApplicationCountingHandler(http);
        using var countedHttp = new HttpClient(counter) { Timeout = Timeout.InfiniteTimeSpan };
        var ai = new OllamaFoodDispatchCandidateAI(countedHttp, Options.Create(new FoodDispatchOllamaOptions
        { Enabled = true, TimeoutSeconds = 90 }));
        if (databasePath is not null && File.Exists(databasePath)) throw new IOException("Evidence database already exists.");
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = databasePath ?? ":memory:" }.ToString());
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection).Options, new ApplicationEncryption());
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("application-near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("application-older", 기사앱식별자.FoodDeliveryDriverApp, 37.5002m, 127m, now, 100)]);
        var spaces = await CreateFoodSpaceStoreAsync(("application-near", 37.5001m, 127m),
            ("application-older", 37.5002m, 127m));
        var policy = new 음식배달배차업무정책(states, new FakeRejectedRequestStore(),
            new StraightLineRouteService(), spaces, new FakeFoodOrderStore(),
            Options.Create(new 배차큐정책Options { 음식배달동일권최소후보기사수 = 1,
                음식배달후보검색반경Km = 5, 음식배달후보최대조회수 = 20 }),
            new TestDispatchAvailability(), candidateAI: ai);
        var queue = Queue();
        queue.Id = 4801;
        queue.원본의뢰유형 = 운송의뢰배차원천유형.음식점주문;
        queue.상태 = 상태값.배차대기상태.대기;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var baseline = await policy.후보검토Async(queue);
        Assert.Equal("application-older", baseline.선정후보?.DriverId);
        Assert.Equal(2, FoodDispatchCandidateConstraints.Eligible(baseline).Count);
        var engine = new 음식배달배차엔진([policy], new 운송의뢰배차원천분류Service(),
            new 음식배달배차흐름Resolver(), NullLogger<음식배달배차엔진>.Instance);
        var selection = new 배차추천후보선정Service(db, new 운영체제배차EngineCatalog([engine]), ai);
        var notifications = new ApplicationNotificationPort();
        var transition = new 배차대기원장전환Service(db, Options.Create(new 배차큐정책Options()),
            selection, notifications, null!, new 음식배달배차흐름Resolver(),
            new ApplicationPricingPort(), new ApplicationFoodRecordPort(),
            candidateAI: ai);
        var first = await transition.추천대기처리Async(queue.의뢰Id);
        Assert.True(first.전환여부);
        var second = await transition.추천대기처리Async(queue.의뢰Id);
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        var audit = await db.운송이벤트.SingleAsync();
        using var metadata = JsonDocument.Parse(audit.메타데이터);
        var reason = metadata.RootElement.GetProperty("CandidateReason").GetString() ?? string.Empty;
        return new(saved.현재추천대상기사Id!, counter.Calls, reason.Contains("FoodDispatchAI.Applied", StringComparison.Ordinal),
            reason, saved.추천라운드, await db.운송이벤트.CountAsync(), await db.운영배차활동사건.CountAsync(),
            notifications.Calls, saved.기사지급예정액 ?? throw new InvalidOperationException("Fee was not frozen."), second.전환여부)
            { BaselineDriverId = baseline.선정후보!.DriverId };
    }

    public sealed record FoodDispatchApplicationEvidence(string RecommendedDriverId, int ModelRequests,
        bool AISelectionApplied, string Reason, int RecommendationRound, int AuditEvents, int ActivityEvents,
        int NotificationRequestsSuppressed, decimal FrozenFee, bool SecondTransitionChanged)
    {
        public string BaselineDriverId { get; init; } = string.Empty;
    }

    private sealed class ApplicationResponseHandler(string token) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true,
                    message = new { role = "assistant", content = JsonSerializer.Serialize(new { candidateToken = token }) } }),
                    Encoding.UTF8, "application/json")
            });
    }

    private sealed class ApplicationCountingHandler(HttpClient inner) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri) { Content = request.Content };
            return await inner.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, ct);
        }
    }

    private sealed class ApplicationNotificationPort : I배차추천알림Service
    {
        public int Calls { get; private set; }
        public Task 추천알림요청생성Async(long id, string request, string driver, int round, CancellationToken ct = default)
        { Calls++; return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ApplicationFoodRecordPort : I음식배달기사추천기록Service
    {
        public Task 추천기록Async(string driver, DateTime now, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ApplicationPricingPort : I음식배달기사제안요금Service
    {
        public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken ct = default)
            => Task.FromResult(new 음식배달기사제안요금산정결과(
                new 음식배달기사제안요금판정(2500m, 1000m, 500m, 4000m, true, true),
                new 픽업지기상관측결과(true, true, 픽업지기상자료상태Code.Available,
                    "1", "1.0", DateTime.UnixEpoch, 픽업지기상관측결과.공식자료출처, new string('a', 64)),
                "ollama-application-test.r48", DateTime.UtcNow));
    }

    private sealed class ApplicationEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
