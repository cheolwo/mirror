using System.Globalization;
using System.Net;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Tests.ClientInfrastructure;

public sealed class MemberDeliveryPushTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 정상payload는내부제안route만생성하고외부url을무시한다()
    {
        var data = Data(); data["route"] = "https://outside.example/";
        var hint = MemberDeliveryPushCoordinator.Parse(data, Now);
        Assert.NotNull(hint);
        Assert.Equal("/community/exchange/deliveries/offers/offer-1", hint.Route);
    }

    [Fact]
    public void 서버DateTimeUtc의Z접미사도동일만료시각으로읽는다()
    {
        var data = Data(); data["expiresAtUtc"] = Now.AddMinutes(5).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        Assert.Equal(Now.AddMinutes(5), MemberDeliveryPushCoordinator.Parse(data, Now)?.ExpiresAtUtc);
    }

    [Theory]
    [InlineData("appKey", "ssalddel.food-driver")]
    [InlineData("type", "FoodDeliveryRecommendation")]
    [InlineData("notificationId", "")]
    [InlineData("offerId", "../x")]
    [InlineData("offerId", "%2Fadmin")]
    [InlineData("userId", " owner-a")]
    [InlineData("requestId", "request\nprivate")]
    [InlineData("expiresAtUtc", "tomorrow")]
    public void 다른앱과잘못된식별자값은차단한다(string key, string value)
    {
        var data = Data(); data[key] = value;
        Assert.Null(MemberDeliveryPushCoordinator.Parse(data, Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(301)]
    public void 만료나5분넘는알림은게시하지않는다(int seconds)
        => Assert.Null(MemberDeliveryPushCoordinator.Parse(Data(Now.AddSeconds(seconds)), Now));

    [Fact]
    public async Task 서버동의와설치소유자가맞을때만같은알림을한번게시한다()
    {
        var f = new Fixture();
        Assert.True(await f.Coordinator.ReceiveAsync(Data()));
        Assert.True(await f.Coordinator.ReceiveAsync(Data()));
        Assert.Single(f.Plans);
        Assert.Single(f.Store.State.Published);
    }

    [Fact]
    public async Task 등록계정불일치와철회대기는게시하지않는다()
    {
        var f = new Fixture();
        f.Store.State = f.Store.State with { RegisteredOwnerId = "owner-b" };
        Assert.False(await f.Coordinator.ReceiveAsync(Data()));
        f.Store.State = f.Store.State with { RegisteredOwnerId = "owner-a", RevocationPending = true };
        Assert.False(await f.Coordinator.ReceiveAsync(Data()));
        Assert.Empty(f.Plans);
    }

    [Fact]
    public async Task 인증이없거나동의가꺼져있거나동의조회가낡으면게시하지않는다()
    {
        var f = new Fixture(); f.Session = new(null, false, 2);
        Assert.False(await f.Coordinator.ReceiveAsync(Data()));
        f.Session = new("owner-a", true, 3);
        f.Store.State = f.Store.State with { Preferences = new(Enabled: false) };
        Assert.False(await f.Coordinator.ReceiveAsync(Data()));
        f.Store.State = f.Store.State with { Preferences = new(Enabled: true), PreferencesCheckedAtUtc = Now.AddDays(-2) };
        Assert.False(await f.Coordinator.ReceiveAsync(Data()));
    }

    [Fact]
    public async Task 야간무음은전경배경공통policy를따르고OS권한거부는성공으로기록하지않는다()
    {
        var f = new Fixture(); f.Clock.Now = Now.AddHours(13); // 한국 01:00
        f.Store.State = f.Store.State with { Preferences = new(true, true, true, true), PreferencesCheckedAtUtc = f.Clock.Now };
        f.AllowPublish = false;
        Assert.False(await f.Coordinator.ReceiveAsync(Data(f.Clock.Now.AddMinutes(5))));
        Assert.Empty(f.Store.State.Published);
        f.AllowPublish = true;
        Assert.True(await f.Coordinator.ReceiveAsync(Data(f.Clock.Now.AddMinutes(5))));
        Assert.False(f.Plans.Last().Sound); Assert.False(f.Plans.Last().Vibrate);
    }

    [Fact]
    public async Task 조회중계정이바뀌면원래알림은게시하지않는다()
    {
        var f = new Fixture(); f.Restore = () => f.Session = new("owner-b", true, 2);
        Assert.False(await f.Coordinator.ReceiveAsync(Data()));
        Assert.Empty(f.Plans);
    }

    [Fact]
    public async Task 취소된백그라운드작업은늦게읽기를마쳐도게시할수없다()
    {
        var f = new Fixture(); using var cancel = new CancellationTokenSource();
        f.Store.BeforeUpdate = () => cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Coordinator.ReceiveAsync(Data(), cancel.Token));
        Assert.Empty(f.Plans);
    }

    [Fact]
    public async Task 익명클릭은로그인후같은계정만한번소비한다()
    {
        var f = new Fixture(); f.Session = new(null, false, 2);
        Assert.True(await f.Coordinator.QueueOpenTargetAsync(Data()));
        Assert.True(await f.Coordinator.IsSignInRequiredAsync());
        Assert.Null(await f.Coordinator.TakeOpenTargetAsync());
        f.Session = new("owner-a", true, 3);
        Assert.False(await f.Coordinator.IsSignInRequiredAsync());
        Assert.NotNull(await f.Coordinator.TakeOpenTargetAsync());
        Assert.Null(await f.Coordinator.TakeOpenTargetAsync());
    }

    [Fact]
    public async Task 다른계정로그인과만료는보관된클릭을폐기한다()
    {
        var f = new Fixture(); Assert.True(await f.Coordinator.QueueOpenTargetAsync(Data()));
        f.Session = new("owner-b", true, 2);
        Assert.Null(await f.Coordinator.TakeOpenTargetAsync()); Assert.Null(f.Store.State.OpenTarget);
        f.Session = new("owner-a", true, 3);
        Assert.True(await f.Coordinator.QueueOpenTargetAsync(Data())); f.Clock.Now = Now.AddMinutes(6);
        Assert.Null(await f.Coordinator.TakeOpenTargetAsync()); Assert.Null(f.Store.State.OpenTarget);
    }

    [Fact]
    public async Task 늦은이전계정정리는새계정설정을지우지않는다()
    {
        var f = new Fixture(); f.Session = new("owner-b", true, 2);
        await f.Coordinator.ApplyPreferencesAsync("owner-b", new(true));
        await f.Coordinator.ClearAccountAsync("owner-a");
        Assert.Equal("owner-b", f.Store.State.PreferencesOwnerId);
        Assert.True(f.Store.State.Preferences.Enabled);
    }

    [Fact]
    public async Task 로그아웃해제실패는철회대기를남기고같은계정에서삭제후재등록한다()
    {
        var store = new MemoryStore(); var handler = new Handler { Status = HttpStatusCode.ServiceUnavailable };
        var owner = "owner-a"; long revision = 1;
        var client = Registration(store, handler, () => owner, () => revision);
        var failed = await client.DeactivateCurrentOwnerAsync();
        Assert.Equal(SsalddelMobilePushRegistrationState.Failed, failed.State);
        Assert.True(store.State.RevocationPending);
        Assert.False(store.State.Preferences.Enabled);
        handler.Status = HttpStatusCode.NoContent;
        var retry = await client.EnsureRegisteredAsync();
        Assert.Equal(SsalddelMobilePushRegistrationState.Registered, retry.State);
        Assert.Equal(new[] { HttpMethod.Delete, HttpMethod.Delete, HttpMethod.Put }, handler.Methods);
        Assert.False(store.State.RevocationPending); Assert.Equal(owner, store.State.RegisteredOwnerId);
    }

    [Fact]
    public async Task OS알림철회는설치를해제하되같은계정의소리진동선택은보존한다()
    {
        var store = new MemoryStore(); var handler = new Handler();
        var preferences = new NotificationAlertPreferences(true, false, false, true);
        store.State = store.State with { PushToken = null, Preferences = preferences };
        var result = await Registration(store, handler, () => "owner-a", () => 1).EnsureRegisteredAsync();
        Assert.Equal(SsalddelMobilePushRegistrationState.TokenNotAvailable, result.State);
        Assert.Equal(new[] { HttpMethod.Delete }, handler.Methods);
        Assert.Null(store.State.RegisteredOwnerId);
        Assert.Equal("owner-a", store.State.PreferencesOwnerId);
        Assert.Equal(preferences, store.State.Preferences);
    }

    [Fact]
    public async Task 등록응답중계정변경은성공상태로저장하지않는다()
    {
        var store = new MemoryStore(); var owner = "owner-a"; long revision = 1;
        var handler = new Handler { Action = () => { owner = "owner-b"; revision++; } };
        var result = await Registration(store, handler, () => owner, () => revision).EnsureRegisteredAsync();
        Assert.Equal(SsalddelMobilePushRegistrationState.Failed, result.State);
        Assert.Equal("owner-a", store.State.RegisteredOwnerId);
    }

    [Fact]
    public async Task 등록중토큰회전은이전토큰을현재성공으로저장하지않는다()
    {
        var store = new MemoryStore(); var handler = new Handler { Action = () => store.State = store.State with { PushToken = "token-b" } };
        var result = await Registration(store, handler, () => "owner-a", () => 1).EnsureRegisteredAsync();
        Assert.Equal(SsalddelMobilePushRegistrationState.Failed, result.State);
        Assert.Null(store.State.RegisteredTokenHash);
    }

    [Fact]
    public async Task 새계정등록은같은설치를소유계정으로다시결속한다()
    {
        var store = new MemoryStore();
        var result = await Registration(store, new Handler(), () => "owner-b", () => 2).EnsureRegisteredAsync();
        Assert.Equal(SsalddelMobilePushRegistrationState.Registered, result.State);
        Assert.Equal("owner-b", store.State.RegisteredOwnerId);
    }

    private static SsalddelMobilePushInstallationClient Registration(MemoryStore store, Handler handler,
        Func<string?> owner, Func<long> revision) => new(new HttpClient(handler) { BaseAddress = new("https://localhost/") },
            new Provider(store), () => "access", store, owner, revision);

    private static Dictionary<string, string> Data(DateTimeOffset? expiry = null) => new()
    {
        ["type"] = MemberDeliveryPushCoordinator.MessageType, ["appKey"] = MemberDeliveryPushCoordinator.AppKey,
        ["notificationId"] = "notification-1", ["offerId"] = "offer-1", ["requestId"] = "request-1",
        ["userId"] = "owner-a", ["expiresAtUtc"] = (expiry ?? Now.AddMinutes(5)).ToString("O", CultureInfo.InvariantCulture)
    };

    private sealed class Fixture
    {
        public MemoryStore Store { get; } = new();
        public Clock Clock { get; } = new();
        public MemberDeliveryPushSession Session { get; set; } = new("owner-a", true, 1);
        public Action? Restore { get; set; }
        public bool AllowPublish { get; set; } = true;
        public List<NotificationAlertPlan> Plans { get; } = [];
        public MemberDeliveryPushCoordinator Coordinator { get; }
        public Fixture() => Coordinator = new(Store, () => Session, _ => { Restore?.Invoke(); return Task.CompletedTask; },
            (_, plan) => { Plans.Add(plan); return AllowPublish; }, Clock);
    }

    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = MemberDeliveryPushTests.Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class MemoryStore : IMemberDeliveryPushStateStore
    {
        public MemberDeliveryPushState State { get; set; } = new(Guid.NewGuid().ToString("N"))
        {
            PushToken = "token-a", RegisteredOwnerId = "owner-a", PreferencesOwnerId = "owner-a",
            Preferences = new(true), PreferencesCheckedAtUtc = Now
        };
        public Action? BeforeUpdate { get; set; }
        public Task<MemberDeliveryPushState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);
        public Task UpdateAsync(Func<MemberDeliveryPushState, MemberDeliveryPushState> update, CancellationToken cancellationToken = default)
        { BeforeUpdate?.Invoke(); cancellationToken.ThrowIfCancellationRequested(); State = update(State); return Task.CompletedTask; }
    }
    private sealed class Provider(MemoryStore store) : ISsalddelMobilePushTokenProvider
    {
        public Task<SsalddelMobilePushTokenSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<SsalddelMobilePushTokenSnapshot?>(new(store.State.DeviceInstallationId, MemberDeliveryPushCoordinator.AppKey,
                "android", store.State.PushToken!, null, null));
    }
    private sealed class Handler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;
        public Action? Action { get; init; }
        public List<HttpMethod> Methods { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Methods.Add(request.Method); Action?.Invoke(); return Task.FromResult(new HttpResponseMessage(Status)); }
    }
}
