using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FDriverApp.Services;
using FDriverApp.PageModels;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverFoodPushTests
{
    [Fact]
    public async Task Registration_IsFoodOnlyAndUsesCurrentOwnerCredentials_AndTokenRotationRegistersAgain()
    {
        var session = new FDriverTestSession(); var store = new DeviceStore(); var bodies = new List<SsalddelMobilePushInstallationUpsertRequest>();
        var headers = new List<string?>();
        var service = Service(session, store, async request =>
        {
            bodies.Add((await request.Content!.ReadFromJsonAsync<SsalddelMobilePushInstallationUpsertRequest>())!);
            headers.Add(request.Headers.Authorization?.Parameter); return Ok();
        });
        Assert.Equal(FDriverPushRegistrationState.Registered, (await service.EnsureRegisteredAsync()).State);
        await service.EnsureRegisteredAsync();
        Assert.Single(bodies);
        await service.UpdateTokenAsync("rotated-food-token");
        Assert.Equal(2, bodies.Count); Assert.All(headers, token => Assert.Equal("test-access", token));
        Assert.All(bodies, body => { Assert.Equal(기사앱식별자.FoodDeliveryDriverApp, body.AppKey); Assert.Equal("android", body.Platform); });
        Assert.Equal("rotated-food-token", bodies[1].PushToken);
        Assert.Equal(FDriverPushRegistrationService.InstallationForOwner("device-one", "test-driver"), bodies[0].InstallationId);
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("token")]
    [InlineData("anonymous")]
    public async Task RegistrationWithoutPrerequisites_DoesNotWriteServer(string reason)
    {
        var session = new FDriverTestSession(); var store = new DeviceStore(); var count = 0;
        if (reason == "configuration") store.IsConfigured = false;
        if (reason == "token") store.State = store.State with { PushToken = null };
        if (reason == "anonymous") await session.ClearAsync();
        var service = Service(session, store, _ => { count++; return Task.FromResult(Ok()); });
        var result = await service.EnsureRegisteredAsync();
        Assert.NotEqual(FDriverPushRegistrationState.Registered, result.State); Assert.Equal(0, count);
    }

    [Fact]
    public async Task LateRegistrationResponse_AfterAccountChangeDoesNotStoreOldOwnerOrOverrideNewRegistration()
    {
        var session = new FDriverTestSession(); var store = new DeviceStore();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var installations = new List<string>();
        var service = Service(session, store, async request =>
        {
            var body = (await request.Content!.ReadFromJsonAsync<SsalddelMobilePushInstallationUpsertRequest>())!;
            installations.Add(body.InstallationId);
            if (installations.Count == 1) { entered.SetResult(); await released.Task; }
            return Ok();
        });
        var first = service.EnsureRegisteredAsync(); await entered.Task;
        await session.ApplyAsync(Token("driver-b")); released.SetResult();
        Assert.Equal(FDriverPushRegistrationState.Failed, (await first).State);
        Assert.Null(store.State.RegisteredOwnerId);
        Assert.Equal(FDriverPushRegistrationState.Registered, (await service.EnsureRegisteredAsync()).State);
        Assert.Equal("driver-b", store.State.RegisteredOwnerId); Assert.NotEqual(installations[0], installations[1]);
    }

    [Fact]
    public async Task LogoutFailurePersistsOriginalOwnerAndRestartRetriesDeleteBeforeRegistration()
    {
        var session = new FDriverTestSession(); var store = RegisteredStore(); var methods = new List<HttpMethod>();
        var failed = Service(session, store, request => { methods.Add(request.Method); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); });
        Assert.Equal(FDriverPushRegistrationState.Failed, (await failed.DeactivateCurrentOwnerAsync()).State);
        Assert.True(store.State.RevocationPending); Assert.Equal("test-driver", store.State.RegisteredOwnerId);
        var restarted = Service(session, store, request => { methods.Add(request.Method); return Task.FromResult(Ok()); });
        Assert.Equal(FDriverPushRegistrationState.Registered, (await restarted.EnsureRegisteredAsync()).State);
        Assert.Equal(new[] { HttpMethod.Delete, HttpMethod.Delete, HttpMethod.Put }, methods);
        Assert.False(store.State.RevocationPending);
    }

    [Fact]
    public async Task OtherOwner_NeverUsesOwnCredentialsToDeletePreviousOwnerInstallation()
    {
        var session = new FDriverTestSession(); var store = RegisteredStore();
        store.State = store.State with { RevocationPending = true }; await session.ApplyAsync(Token("driver-b"));
        var methods = new List<HttpMethod>();
        var service = Service(session, store, request => { methods.Add(request.Method); return Task.FromResult(Ok()); });
        await service.DeactivateCurrentOwnerAsync(); Assert.Empty(methods); Assert.True(store.State.RevocationPending);
        await service.EnsureRegisteredAsync(); Assert.Equal(new[] { HttpMethod.Put }, methods);
        Assert.Equal("driver-b", store.State.RegisteredOwnerId);
    }

    [Fact]
    public async Task LogoutStoreFailure_DoesNotDeleteServerOrClaimSuccessfulRemoval()
    {
        var session = new FDriverTestSession(); var store = RegisteredStore(); store.SaveFailure = new IOException("store failed"); var count = 0;
        var service = Service(session, store, _ => { count++; return Task.FromResult(Ok()); });
        Assert.Equal(FDriverPushRegistrationState.Failed, (await service.DeactivateCurrentOwnerAsync()).State);
        Assert.Equal(0, count); Assert.Equal("test-driver", store.State.RegisteredOwnerId);
    }

    [Fact]
    public async Task Receiver_PublishesOnlyGenericHintWithoutAnyApiRequest()
    {
        var session = new FDriverTestSession(); var store = RegisteredStore(); var notifications = new Notifications();
        var receiver = new FDriverFoodPushReceiver(session, store, notifications); var data = Data();
        data["address"] = "must not display"; data["customer"] = "must not display";
        Assert.True(await receiver.ReceiveAsync(data)); Assert.Equal(("test-driver", "offer-1"), Assert.Single(notifications.Published));
        Assert.Null(notifications.Target); // 알림 클릭 전에는 라우트 이동·상태 실행을 시작하지 않습니다.
    }

    [Theory]
    [InlineData("type", "DriverDispatchRecommendation")]
    [InlineData("appKey", "CargoYongdalDriverApp")]
    [InlineData("userId", "driver-b")]
    [InlineData("offerId", "")]
    [InlineData("expiresAtUtc", "expired")]
    public async Task Receiver_RejectsCargoOtherOwnerAndInvalidHints(string key, string value)
    {
        var session = new FDriverTestSession(); var notifications = new Notifications(); var data = Data(); data[key] = value;
        Assert.False(await new FDriverFoodPushReceiver(session, RegisteredStore(), notifications).ReceiveAsync(data));
        Assert.Empty(notifications.Published);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public async Task Receiver_RejectsExpiredOrUnboundedExpiry(int minutes)
    {
        var data = Data(); data["expiresAtUtc"] = DateTime.UtcNow.AddMinutes(minutes).ToString("O"); var notifications = new Notifications();
        Assert.False(await new FDriverFoodPushReceiver(new FDriverTestSession(), RegisteredStore(), notifications).ReceiveAsync(data));
        Assert.Empty(notifications.Published);
    }

    [Fact]
    public async Task Receiver_RejectsSignedOutRevocationPendingOrChangedOwnerDuringRestore()
    {
        var session = new FDriverTestSession(); var store = RegisteredStore(); var notifications = new Notifications();
        var receiver = new FDriverFoodPushReceiver(session, store, notifications);
        store.State = store.State with { RevocationPending = true }; Assert.False(await receiver.ReceiveAsync(Data()));
        store.State = store.State with { RevocationPending = false }; await session.ClearAsync(); Assert.False(await receiver.ReceiveAsync(Data()));
        await session.ApplyAsync(Token("test-driver"));
        store.BeforeLoad = () => session.ApplyAsync(Token("driver-b"));
        Assert.False(await receiver.ReceiveAsync(Data())); Assert.Empty(notifications.Published);
    }

    [Fact]
    public void Receiver_CallbackCompletesItsLocalNotificationBeforeReturning()
    {
        var notifications = new Notifications();
        var receiver = new FDriverFoodPushReceiver(new FDriverTestSession(), RegisteredStore(), notifications);
        Assert.True(receiver.ReceiveWithinCallback(Data()));
        Assert.Equal(("test-driver", "offer-1"), Assert.Single(notifications.Published));
    }

    [Fact]
    public async Task Receiver_CallbackTimeoutCannotPublishAfterDelayedLocalRestore()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = RegisteredStore();
        store.BeforeLoad = async () => { entered.SetResult(); await released.Task; loaded.SetResult(); };
        var notifications = new Notifications();
        var receiver = new FDriverFoodPushReceiver(new FDriverTestSession(), store, notifications);
        var callback = Task.Run(() => receiver.ReceiveWithinCallback(Data(), TimeSpan.FromMilliseconds(500)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(await callback.WaitAsync(TimeSpan.FromSeconds(2)));
        released.SetResult(); await loaded.Task;
        Assert.Empty(notifications.Published);
    }

    [Fact]
    public async Task TokenCallbackCachesLocallyWithoutRegisteringOrRefreshingCredentials()
    {
        var session = new FDriverTestSession(); var store = RegisteredStore(); var calls = 0;
        var service = Service(session, store, _ => { calls++; return Task.FromResult(Ok()); });
        await service.CacheTokenAsync("rotated-sdk-token");
        Assert.Equal("rotated-sdk-token", store.State.PushToken);
        Assert.Equal("test-driver", store.State.RegisteredOwnerId);
        Assert.Equal(0, calls); Assert.Equal(0, session.ApplyCount); Assert.Equal(0, session.ClearCount);
        await service.EnsureRegisteredAsync();
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task NotificationPermissionOffDoesNotFailServerRegistrationAndRemainsVisibleAfterMonitorStarts()
    {
        var session = new FDriverTestSession(); var store = new DeviceStore { AreNotificationsEnabled = false };
        using var http = new HttpClient(new FDriverTestHttpHandler((_, _) => Task.FromResult(Ok()))) { BaseAddress = new("http://localhost/") };
        var auth = new FDriverAuthApiService(http, session);
        var registration = new FDriverPushRegistrationService(http, session, auth, store);
        var model = new MainPageModel(new(), session, auth, new FDriverTestWorkspaceApi(), new FDriverTestLocationService(),
            new(), foodPushRegistration: registration);
        try
        {
            await model.InitializeAsync();
            await model.ActivateWorkspacePageAsync();
            Assert.Equal("test-driver", store.State.RegisteredOwnerId);
            Assert.Contains("알림 권한이 꺼져", model.RecommendationNotificationText);
            Assert.True(model.IsAuthenticated);
            store.AreNotificationsEnabled = true;
            await model.RefreshCommand.ExecuteAsync(null);
            Assert.StartsWith("새 배달 알림", model.RecommendationNotificationText);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    private static FDriverPushRegistrationService Service(FDriverTestSession session, DeviceStore store,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var http = new HttpClient(new FDriverTestHttpHandler((request, _) => handler(request))) { BaseAddress = new("http://localhost/") };
        return new(http, session, new(http, session), store);
    }
    private static HttpResponseMessage Ok() => new(HttpStatusCode.NoContent);
    private static DeviceStore RegisteredStore() => new() { State = new("device-one", "food-token", "test-driver") };
    private static ClientAuthTokenSnapshot Token(string owner) => new("access-" + owner, DateTime.UtcNow.AddMinutes(30), "refresh-" + owner,
        DateTime.UtcNow.AddHours(1), owner, "기사", ["Driver"]);
    private static Dictionary<string, string> Data() => new()
    { ["type"] = FDriverFoodPushReceiver.MessageType, ["appKey"] = 기사앱식별자.FoodDeliveryDriverApp, ["userId"] = "test-driver",
        ["offerId"] = "offer-1", ["expiresAtUtc"] = DateTime.UtcNow.AddMinutes(2).ToString("O") };

    private sealed class DeviceStore : IFDriverPushDeviceStore
    {
        public bool IsConfigured { get; set; } = true;
        public bool AreNotificationsEnabled { get; set; } = true;
        public FDriverPushDeviceState State = new("device-one", "food-token");
        public Exception? SaveFailure;
        public Func<Task>? BeforeLoad;
        public async Task<FDriverPushDeviceState> LoadAsync(CancellationToken cancellationToken = default)
        { if (BeforeLoad is not null) await BeforeLoad(); return JsonSerializer.Deserialize<FDriverPushDeviceState>(JsonSerializer.Serialize(State))!; }
        public Task SaveAsync(FDriverPushDeviceState state, CancellationToken cancellationToken = default)
        { if (SaveFailure is not null) throw SaveFailure; State = state; return Task.CompletedTask; }
    }

    private sealed class Notifications : IFDriverFoodNotificationService
    {
        public readonly List<(string User, string Offer)> Published = [];
        public FDriverFoodNotificationTarget? Target;
        public bool Publish(string userId, string offerId, DateTime expiresAtUtc) { Published.Add((userId, offerId)); return true; }
        public FDriverFoodNotificationReceipt? ReadReceipt() => null;
        public void WriteReceipt(FDriverFoodNotificationReceipt receipt) { }
        public FDriverFoodNotificationTarget? ReadOpenTarget() => Target;
        public void SetOpenTarget(FDriverFoodNotificationTarget target) => Target = target;
        public void ClearOpenTarget() => Target = null;
        public void Cancel() { }
        public void ClearAccount() { }
    }
}
