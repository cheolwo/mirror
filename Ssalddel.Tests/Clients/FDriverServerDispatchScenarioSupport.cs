using System.Net;
using System.Text;
using System.Text.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Tests.Services.Dispatch.AI;
using Ssalddel.Contracts.Driver.Notification;

namespace Ssalddel.Tests.Clients;

// Only native/device boundaries are recorded. The API client, receiver and PageModel
// below are linked production code, and all normal HTTP requests reach the fixture server.
internal sealed class FDriverWorkflowMobileSession : IAsyncDisposable
{
    private FDriverWorkflowMobileSession(FoodDispatchWorkflowScenarioFixture fixture, FDriverTestSession session,
        FDriverWorkflowHttpHandler transport, HttpClient http, FoodDeliveryDriverApiService api,
        FDriverWorkflowNotifications notifications, FDriverFoodPushReceiver receiver, MainPageModel model)
        => (Fixture, Session, Transport, Http, Api, Notifications, Receiver, Model)
            = (fixture, session, transport, http, api, notifications, receiver, model);

    public FoodDispatchWorkflowScenarioFixture Fixture { get; }
    public FDriverTestSession Session { get; }
    public FDriverWorkflowHttpHandler Transport { get; }
    public HttpClient Http { get; }
    public FoodDeliveryDriverApiService Api { get; }
    public FDriverWorkflowNotifications Notifications { get; }
    public FDriverFoodPushReceiver Receiver { get; }
    public MainPageModel Model { get; }

    public static async Task<FDriverWorkflowMobileSession> CreateAsync(string scenarioId, HttpClient modelHttp,
        string? databasePath = null, FoodDispatchRegionalSeed? regionalSeed = null)
    {
        var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync(scenarioId, modelHttp, databasePath, regionalSeed);
        try
        {
            await fixture.RecommendAsync();
            await fixture.DispatchPendingNotificationsAsync();
            var session = new FDriverTestSession();
            await session.ApplyAsync(Token(fixture.DriverId, fixture.AccessToken));
            var transport = new FDriverWorkflowHttpHandler();
            var http = new HttpClient(transport) { BaseAddress = fixture.BaseAddress, Timeout = TimeSpan.FromSeconds(20) };
            var auth = new FDriverAuthApiService(http, session);
            var api = new FoodDeliveryDriverApiService(http, session, auth);
            var notifications = new FDriverWorkflowNotifications();
            // The recorded device boundary starts with an explicit food opt-in;
            // missing legacy flags must not silently make a new alert permissible.
            notifications.ApplySettings(fixture.DriverId, new 기사알림설정응답
            {
                DriverId = fixture.DriverId, 배차추천알림사용 = true, 음식배달추천알림사용 = true,
                소리사용 = true, 진동사용 = true
            }, DateTime.UtcNow);
            var receiver = new FDriverFoodPushReceiver(session, new FDriverWorkflowPushStore(fixture.DriverId), notifications);
            var model = new MainPageModel(new(), session, auth, api, new FDriverTestLocationService(), new(), notifications);
            // These switches pause map/device presentation, not the work lifetime or commands.
            model.SetWorkspacePageVisible(false);
            model.PauseFoodMap();
            return new(fixture, session, transport, http, api, notifications, receiver, model);
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public static ClientAuthTokenSnapshot Token(string driverId, string accessToken)
        => new(accessToken, DateTime.UtcNow.AddMinutes(30), "fixture-refresh-unused",
            DateTime.UtcNow.AddHours(1), driverId, "합성 기사", ["기사"]);

    public async ValueTask DisposeAsync()
    {
        await Model.StopMonitoringAsync();
        Http.Dispose();
        await Fixture.DisposeAsync();
    }
}

internal sealed class FDriverWorkflowPushStore(string owner) : IFDriverPushDeviceStore
{
    private FDriverPushDeviceState state = new("fixture-installation", RegisteredOwnerId: owner);
    public bool IsConfigured => true;
    public Task<FDriverPushDeviceState> LoadAsync(CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(state); }
    public Task SaveAsync(FDriverPushDeviceState value, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); state = value; return Task.CompletedTask; }
}

internal sealed class FDriverWorkflowNotifications : IFDriverFoodNotificationService
{
    private string? _settingsOwner;
    private 기사알림설정응답? _settings;
    public FDriverFoodNotificationReceipt? Receipt { get; private set; }
    public FDriverFoodNotificationTarget? Target { get; private set; }
    public int PublishRequests { get; private set; }
    public int CancelRequests { get; private set; }
    public bool PublicationAllowed { get; set; } = true;
    public FDriverFoodNotificationReceipt? ReadReceipt() => Receipt;
    public void WriteReceipt(FDriverFoodNotificationReceipt receipt)
        => Receipt = new(receipt.UserId, receipt.SeenOfferIds.ToArray());
    public FDriverFoodNotificationTarget? ReadOpenTarget() => Target;
    public void SetOpenTarget(FDriverFoodNotificationTarget target) => Target = target;
    public void ClearOpenTarget() => Target = null;
    public bool Publish(string userId, string offerId, DateTime expiresAtUtc)
    {
        if (userId != _settingsOwner || !FDriverNotificationPublicationGate.AllowsFoodAlert(_settings)) return false;
        PublishRequests++;
        // This is a request to the device port. No OS banner or sound is emitted.
        return PublicationAllowed;
    }
    public void Cancel() => CancelRequests++;
    public void ClearAccount() { Cancel(); Receipt = null; Target = null; _settings = null; _settingsOwner = null; }
    public void ApplySettings(string ownerId, 기사알림설정응답 settings, DateTime checkedAtUtc)
    {
        if (ownerId != settings.DriverId) return;
        _settingsOwner = ownerId; _settings = settings;
        if (!FDriverNotificationPublicationGate.AllowsFoodAlert(settings)) Cancel();
    }
}

internal sealed class FDriverWorkflowHttpHandler : DelegatingHandler
{
    public FDriverWorkflowHttpHandler() : base(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { }
    public List<FoodDispatchWorkflowHttpRequest> Requests { get; } = [];
    public bool FailNextCurrentRead { get; set; }
    public bool LoseNextAcceptResponse { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (FailNextCurrentRead && path.EndsWith("/workspace/current", StringComparison.Ordinal))
        {
            FailNextCurrentRead = false;
            Requests.Add(new(request.Method.Method, path, 503, "injected-before-server"));
            return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("fixture connection interruption") };
        }
        var response = await base.SendAsync(request, cancellationToken);
        if (LoseNextAcceptResponse && request.Method == HttpMethod.Post && path.EndsWith("/accept", StringComparison.Ordinal))
        {
            LoseNextAcceptResponse = false;
            var serverStatus = (int)response.StatusCode;
            response.Dispose();
            Requests.Add(new(request.Method.Method, path, serverStatus, "injected-response-loss-after-server"));
            return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("fixture lost response") };
        }
        Requests.Add(new(request.Method.Method, path, (int)response.StatusCode, "server"));
        return response;
    }
}

internal sealed class FDriverWorkflowModelHandler(string candidateToken = "C2", HttpStatusCode status = HttpStatusCode.OK)
    : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = "qwen3:1.7b", done = true,
                message = new { role = "assistant", content = JsonSerializer.Serialize(new { candidateToken }) }
            }), Encoding.UTF8, "application/json")
        });
    }
}
