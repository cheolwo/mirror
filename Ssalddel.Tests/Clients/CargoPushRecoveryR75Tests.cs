using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Drivers;

namespace Ssalddel.Tests.Clients;

public sealed class CargoPushRecoveryR75Tests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("requestId", "DriverDispatchRecommendation")]
    [InlineData("topRequestId", "DriverDispatchRecommendation")]
    [InlineData("requestIds", "DriverDispatchRecommendation")]
    [InlineData("requestId", "DispatchRecommendation")]
    public void OutboxAndBatchLegacyIdsResolveToTheSameRequest(string key, string type)
    {
        var data = new Dictionary<string, string> { ["type"] = type, [key] = key == "requestIds" ? " request-1,request-2 " : " request-1 " };
        var hint = Assert.IsType<DriverRecommendationPushHint>(DriverRecommendationPushHintParser.Parse(data, Now));
        Assert.Equal("request-1", hint.RequestId); Assert.Null(hint.OwnerId); Assert.Null(hint.ExpiresAtUtc);
    }

    [Theory]
    [InlineData("type", "FoodDeliveryRecommendation")]
    [InlineData("appKey", "FoodDeliveryDriverApp")]
    [InlineData("topRequestId", "different-request")]
    [InlineData("requestIds", "different-request")]
    [InlineData("requestId", "bad\nidentifier")]
    [InlineData("recommendationRound", "0")]
    [InlineData("recommendationRound", "NaN")]
    [InlineData("recommendationCount", "-1")]
    [InlineData("recommendationCount", "1001")]
    [InlineData("expiresAtUtc", "2026-10-15T07:59:59Z")]
    [InlineData("expiresAtUtc", "2026-10-16T08:00:00Z")]
    [InlineData("expiresAtUtc", "not-a-time")]
    public void ContradictoryForeignOrMalformedHintsAreRejected(string key, string value)
    {
        var data = Data(); data[key] = value;
        Assert.Null(DriverRecommendationPushHintParser.Parse(data, Now));
    }

    [Fact]
    public void DifferentOwnerAliasesAreRejected()
    {
        var data = Data(); data["driverId"] = "driver-a"; data["userId"] = "driver-b";
        Assert.Null(DriverRecommendationPushHintParser.Parse(data, Now));
    }

    [Fact]
    public async Task LegacyOwnerlessClickRequiresFreshScopedReadAndKeepsPointerUntilNavigationAcknowledged()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        var data = Data(); data["amount"] = "1"; data["pickupAddress"] = "payload address must not be used";
        Assert.Equal(DriverRecommendationPushRecoveryStatus.Captured, (await f.Recovery.CaptureAsync(data, true)).Status);
        var result = await f.Recovery.RecoverAsync();
        Assert.Equal(DriverRecommendationPushRecoveryStatus.Ready, result.Status);
        Assert.Equal("/driver/recommendations/request-1", result.Route);
        Assert.True(f.Samples.LastForced); Assert.Equal(1, f.Samples.Reads);
        Assert.Equal(4300m, Assert.Single(f.Notifications.Published).Request.예상수익);
        Assert.NotNull(f.Store.Value);
        await f.Recovery.AcknowledgeOpenedAsync(result);
        Assert.Null(f.Store.Value);
    }

    [Fact]
    public async Task FailedCanonicalReadKeepsExactPointerAndDoesNotPublishCachedOffer()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()]; f.Samples.Fail = true;
        await f.Recovery.CaptureAsync(Data(), true);
        var before = f.Store.Value;
        var failed = await f.Recovery.RecoverAsync();
        Assert.Equal(DriverRecommendationPushRecoveryStatus.RetryableFailure, failed.Status);
        Assert.Equal(before, f.Store.Value); Assert.Empty(f.Notifications.Published);
        // A new coordinator represents an app process restart; the durable pointer survives.
        f.Samples.Fail = false;
        var restarted = f.NewRecovery();
        var recovered = await restarted.RecoverAsync();
        Assert.Equal(DriverRecommendationPushRecoveryStatus.Ready, recovered.Status);
        Assert.Single(f.Notifications.Published);
    }

    [Theory]
    [InlineData(DriverWorkspaceReadStatus.Unknown)]
    [InlineData(DriverWorkspaceReadStatus.Failed)]
    [InlineData(DriverWorkspaceReadStatus.Sample)]
    public async Task PartialWorkspaceSuccessCannotTurnUnknownRecommendationsIntoMissingOrActionable(string state)
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        f.Samples.ReadState = f.Samples.ReadState with { Recommendations = new(state, "fixture recommendation unavailable") };
        await f.Recovery.CaptureAsync(Data(), true); var saved = f.Store.Value;
        var result = await f.Recovery.RecoverAsync();
        Assert.Equal(DriverRecommendationPushRecoveryStatus.RetryableFailure, result.Status);
        Assert.Equal(saved, f.Store.Value); Assert.Empty(f.Notifications.Published);
    }

    [Fact]
    public async Task MissingRecommendationWithUnknownCurrentTransportRetainsPointerForAcceptedWorkRecheck()
    {
        var f = await Fixture.CreateAsync();
        f.Samples.ReadState = f.Samples.ReadState with { CurrentTransport = DriverWorkspaceSliceReadState.Unknown("현재 운송") };
        await f.Recovery.CaptureAsync(Data(), true);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.RetryableFailure, (await f.Recovery.RecoverAsync()).Status);
        Assert.NotNull(f.Store.Value); Assert.Empty(f.Notifications.Published);
    }

    [Fact]
    public async Task AnotherOwnerHintIsIgnoredWithoutReadsOrWrites()
    {
        var f = await Fixture.CreateAsync(); var data = Data(); data["driverId"] = "driver-b";
        Assert.Equal(DriverRecommendationPushRecoveryStatus.Ignored, (await f.Recovery.CaptureAsync(data, true)).Status);
        Assert.Equal(0, f.Samples.Reads); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task AnonymousLegacyHintCannotBindItselfToAnUnknownFutureAccount()
    {
        var f = await Fixture.CreateAsync(); await f.Session.ClearAsync();
        var result = await f.Recovery.CaptureAsync(Data(), true);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.LoginRequired, result.Status);
        Assert.Contains("/login?returnUrl=", result.Route); Assert.Null(f.Store.Value); Assert.Equal(0, f.Samples.Reads);
    }

    [Fact]
    public async Task AccountSwitchDiscardsPendingTargetBeforeAnyCanonicalRead()
    {
        var f = await Fixture.CreateAsync(); await f.Recovery.CaptureAsync(Data(), true);
        await f.Session.ApplyAsync(Snapshot("driver-b"));
        Assert.Equal(DriverRecommendationPushRecoveryStatus.SessionChanged, (await f.Recovery.RecoverAsync()).Status);
        Assert.Null(f.Store.Value); Assert.Empty(f.Notifications.Published); Assert.Equal(0, f.Samples.Reads);
    }

    [Fact]
    public async Task AccountSwitchClearsThePreviouslyPublishedBannerEvenWithoutPendingPointer()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        await f.Recovery.CaptureAsync(Data(), false); await f.Recovery.RecoverAsync();
        await f.Session.ApplyAsync(Snapshot("driver-b"));
        await f.Recovery.RecoverAsync();
        Assert.Contains("request-1", f.Notifications.Dismissed);
        Assert.Equal(1, f.Samples.Reads);
    }

    [Fact]
    public async Task LateCanonicalResponseAfterSameOwnerReloginCannotPublishOrNavigate()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Samples.Wait = async _ => { entered.TrySetResult(); await release.Task; };
        await f.Recovery.CaptureAsync(Data(), true);
        var task = f.Recovery.RecoverAsync(); await entered.Task;
        await f.Session.ApplyAsync(Snapshot("driver-a")); release.SetResult();
        var result = await task;
        Assert.Equal(DriverRecommendationPushRecoveryStatus.SessionChanged, result.Status);
        Assert.Null(result.Route); Assert.Empty(f.Notifications.Published); Assert.Null(f.Store.Value);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expiry")]
    [InlineData("round")]
    [InlineData("untimed")]
    public async Task UnavailableExpiredOrSupersededOfferCannotProduceAnActionableBanner(string reason)
    {
        var f = await Fixture.CreateAsync(); var offer = Offer(); var data = Data();
        if (reason == "expiry") offer.추천만료시각 = Now.AddSeconds(-1).UtcDateTime;
        if (reason == "untimed") offer.추천만료시각 = null;
        if (reason == "round") data["recommendationRound"] = "2";
        f.Samples.Items = reason == "missing" ? [] : [offer];
        await f.Recovery.CaptureAsync(data, true);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.ExpiredOrUnavailable, (await f.Recovery.RecoverAsync()).Status);
        Assert.Empty(f.Notifications.Published); Assert.Null(f.Store.Value);
    }

    [Fact]
    public async Task LocalPointerExpiryIsNotExtendedByRestartAndNeedsNoNetwork()
    {
        var f = await Fixture.CreateAsync(); await f.Recovery.CaptureAsync(Data(), true);
        f.Time.UtcNow = Now.AddHours(2);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.ExpiredOrUnavailable, (await f.NewRecovery().RecoverAsync()).Status);
        Assert.Equal(0, f.Samples.Reads); Assert.Null(f.Store.Value);
    }

    [Fact]
    public async Task DuplicateHintsRequeryButPublishSameRoundOnlyOnceAndNeverAccept()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        await f.Recovery.CaptureAsync(Data(), false); await f.Recovery.RecoverAsync();
        await f.Recovery.CaptureAsync(Data(), false); await f.Recovery.RecoverAsync();
        Assert.Equal(2, f.Samples.Reads); Assert.Single(f.Notifications.Published); Assert.Null(f.Store.Value);
    }

    [Fact]
    public async Task AcceptedRequestOpensCurrentTransportWithoutPublishingAnotherAcceptanceOffer()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        f.Samples.Transports = [new(42, "request-1", "synthetic cargo", "synthetic pickup", "synthetic dropoff",
            37.5m, 127m, 37.6m, 127.1m, "확정", Now.UtcDateTime, 2.3m, 4300m, false, false, "후불", "상차지 도착")];
        await f.Recovery.CaptureAsync(Data(), true);
        var result = await f.Recovery.RecoverAsync();
        Assert.Equal(DriverRecommendationPushRecoveryStatus.AlreadyAccepted, result.Status);
        Assert.Contains("/driver/transports/current?acceptedRequestId=request-1", result.Route);
        Assert.Empty(f.Notifications.Published); Assert.Single(f.Notifications.Handled);
        await f.Recovery.AcknowledgeOpenedAsync(result); Assert.Null(f.Store.Value);
    }

    [Fact]
    public async Task NewBackgroundHintCannotReplaceTheExactClickedTarget()
    {
        var f = await Fixture.CreateAsync(); await f.Recovery.CaptureAsync(Data(), true);
        var clicked = f.Store.Value;
        var another = Data(); another["requestId"] = "request-2";
        await f.Recovery.CaptureAsync(another, false);
        Assert.Equal(clicked, f.Store.Value);
    }

    [Fact]
    public async Task AcknowledgingAnOldNavigationCannotEraseANewerClickedTarget()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        await f.Recovery.CaptureAsync(Data(), true); var result = await f.Recovery.RecoverAsync();
        var another = Data(); another["requestId"] = "request-2";
        await f.Recovery.CaptureAsync(another, true);
        await f.Recovery.AcknowledgeOpenedAsync(result);
        Assert.Equal("request-2", f.Store.Value!.Hint.RequestId);
    }

    [Fact]
    public async Task ReadCancellationCannotPublishAfterTheCallbackBudgetHasEnded()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        using var cancellation = new CancellationTokenSource();
        f.Samples.Wait = _ => { cancellation.Cancel(); return Task.CompletedTask; };
        await f.Recovery.CaptureAsync(Data(), false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Recovery.RecoverAsync(cancellation.Token));
        Assert.Empty(f.Notifications.Published); Assert.NotNull(f.Store.Value);
    }

    [Fact]
    public async Task MissingCoordinatesDoNotCreateAFabricatedZeroMarker()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()]; f.Map.Available = false;
        await f.Recovery.CaptureAsync(Data(), true);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.Ready, (await f.Recovery.RecoverAsync()).Status);
        Assert.Empty(f.Notifications.Published); Assert.NotNull(f.Store.Value);
    }

    [Fact]
    public async Task UnreadableSecurePointerIsNotOverwrittenByANewHint()
    {
        var f = await Fixture.CreateAsync(); f.Store.ReadFailure = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Recovery.CaptureAsync(Data(), true));
        Assert.Equal(0, f.Store.Writes); Assert.Equal(0, f.Samples.Reads);
    }

    [Fact]
    public async Task BackgroundOsAlertReceivesOnlyTheFreshCanonicalRecommendation()
    {
        var f = await Fixture.CreateAsync(); var offer = Offer(); f.Samples.Items = [offer];
        f.Alerts = new(); f.Recovery = f.NewRecovery();
        var data = Data(); data["title"] = "untrusted payload"; data["amount"] = "1";
        await f.Recovery.CaptureAsync(data, false);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.Ready, (await f.Recovery.RecoverAsync()).Status);
        Assert.Same(offer, Assert.Single(f.Alerts.Requests)); Assert.Single(f.Notifications.Published);
    }

    [Fact]
    public async Task UnconfirmedCanonicalReadDoesNotCallOsPublisher()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Fail = true;
        f.Alerts = new(); f.Recovery = f.NewRecovery();
        await f.Recovery.CaptureAsync(Data(), false);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.RetryableFailure, (await f.Recovery.RecoverAsync()).Status);
        Assert.Empty(f.Alerts.Requests);
    }

    [Fact]
    public async Task NotificationOpenDoesNotPostAnotherOsAlert()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        f.Alerts = new(); f.Recovery = f.NewRecovery();
        await f.Recovery.CaptureAsync(Data(), true);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.Ready, (await f.Recovery.RecoverAsync()).Status);
        Assert.Empty(f.Alerts.Requests);
    }

    [Fact]
    public async Task SettingsReadFailureKeepsThePointerForForegroundRetry()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        f.Alerts = new() { Wait = () => throw new HttpRequestException("fixture unavailable") }; f.Recovery = f.NewRecovery();
        await f.Recovery.CaptureAsync(Data(), false);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.RetryableFailure, (await f.Recovery.RecoverAsync()).Status);
        Assert.NotNull(f.Store.Value); Assert.Empty(f.Notifications.Published);
    }

    [Fact]
    public async Task SameOwnerReloginDuringAlertReadCannotPublishBannerOrKeepOldPointer()
    {
        var f = await Fixture.CreateAsync(); f.Samples.Items = [Offer()];
        f.Alerts = new() { Wait = () => f.Session.ApplyAsync(Snapshot("driver-a")) }; f.Recovery = f.NewRecovery();
        await f.Recovery.CaptureAsync(Data(), false);
        Assert.Equal(DriverRecommendationPushRecoveryStatus.SessionChanged, (await f.Recovery.RecoverAsync()).Status);
        Assert.Null(f.Store.Value); Assert.Empty(f.Notifications.Published);
    }

    private static Dictionary<string, string> Data() => new() { ["type"] = "DriverDispatchRecommendation", ["requestId"] = "request-1" };
    private static DriverRequestItem Offer() => new() { 의뢰Id = "request-1", 화물종류 = "synthetic cargo",
        추천시작시각 = Now.AddSeconds(-10).UtcDateTime, 추천만료시각 = Now.AddMinutes(2).UtcDateTime,
        추천라운드 = 1, 예상수익 = 4300m };
    private static ClientAuthTokenSnapshot Snapshot(string owner) => new("fixture-access", Now.AddDays(100).UtcDateTime,
        "fixture-refresh", Now.AddDays(100).UtcDateTime, owner, owner, ["기사"]);

    private sealed class Fixture
    {
        public AuthSession Session { get; } = new(new TokenStore(), new ClientSessionGuard());
        public Samples Samples { get; } = new(); public Map Map { get; } = new();
        public Notifications Notifications { get; } = new(); public Store Store { get; } = new();
        public Clock Time { get; } = new();
        public DriverRecommendationPushRecovery Recovery { get; set; } = null!;
        public CargoAlerts? Alerts;
        public DriverRecommendationPushRecovery NewRecovery() => new(Session, Samples, Map, Notifications, Store, Time, Alerts);
        public static async Task<Fixture> CreateAsync()
        { var f = new Fixture(); await f.Session.ApplyAsync(Snapshot("driver-a")); f.Recovery = f.NewRecovery(); return f; }
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset UtcNow { get; set; } = Now; public override DateTimeOffset GetUtcNow() => UtcNow; }
    private sealed class CargoAlerts : IDriverCargoRecommendationAlertService
    {
        public List<DriverRequestItem> Requests { get; } = []; public Func<Task>? Wait;
        public async Task<bool> TryPublishAsync(string owner, long revision, DriverRequestItem request,
            CancellationToken cancellationToken = default)
        { Requests.Add(request); if (Wait is not null) await Wait(); return true; }
        public void Cancel() { }
    }
    private sealed class Store : IDriverRecommendationPushTargetStore
    {
        public DriverRecommendationPushTarget? Value; public int Writes; public bool ReadFailure;
        public Task<DriverRecommendationPushTarget?> LoadAsync(CancellationToken cancellationToken = default)
            => ReadFailure ? throw new InvalidOperationException("fixture secure read failure") : Task.FromResult(Value);
        public Task SaveAsync(DriverRecommendationPushTarget target, CancellationToken cancellationToken = default)
        { Value = target; Writes++; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Value = null; return Task.CompletedTask; }
    }
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? _value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) { _value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { _value = null; return Task.CompletedTask; }
    }
    private sealed class Samples : IDriverSampleDataService
    {
        public DriverWorkspaceReadState ReadState { get; set; } = new()
        { Recommendations = DriverWorkspaceSliceReadState.Current(), CurrentTransport = DriverWorkspaceSliceReadState.Current() };
        public IReadOnlyList<DriverRequestItem> Items = []; public IReadOnlyList<기사운송샘플항목> Transports = [];
        public bool Fail, LastForced; public int Reads; public Func<CancellationToken, Task>? Wait;
        public async Task RefreshAsync(CancellationToken cancellationToken = default, bool force = false)
        { Reads++; LastForced = force; if (Wait is not null) await Wait(cancellationToken); if (Fail) throw new HttpRequestException("fixture offline"); }
        public 기사근무샘플상태 근무상태 => null!; public 기사현재위치샘플 기사현재위치 => null!; public 기사정산샘플요약 정산요약 => null!;
        public IReadOnlyList<DriverRequestItem> 추천의뢰목록 => Items; public IReadOnlyList<기사예약샘플항목> 예약목록 => [];
        public IReadOnlyList<기사운송샘플항목> 운송목록 => Transports; public IReadOnlyList<기사알림샘플항목> 알림목록 => [];
        public DriverRequestItem? 추천의뢰조회(string 의뢰Id) => Items.FirstOrDefault(x => x.의뢰Id == 의뢰Id);
        public IReadOnlyList<추천의뢰표시항목> 거리포함추천의뢰목록조회() => [];
        public 기사운송샘플항목? 운송조회(long 운송Id) => Transports.FirstOrDefault(x => x.Id == 운송Id);
        public 기사운송샘플항목? 현재운송조회() => Transports.FirstOrDefault();
    }
    private sealed class Map : IDriverHomeMapService
    {
        public bool Available = true;
        public IReadOnlyList<DriverMapMarkerItem> BuildMarkers(IEnumerable<DriverRequestItem> requests)
            => Available ? requests.Select(x => new DriverMapMarkerItem(x.의뢰Id, 37.5, 127, 37.6, 127.1, "synthetic cargo", "fixture", "synthetic pickup")).ToArray() : [];
    }
    private sealed class Notifications : IDriverRecommendationNotificationService
    {
        public List<DriverIncomingRecommendation> Published { get; } = []; public List<string> Handled { get; } = [];
        public List<string> Dismissed { get; } = [];
        public event Action<DriverIncomingRecommendation?>? Changed;
        public DriverIncomingRecommendation? GetCurrent() => Published.LastOrDefault();
        public void Publish(DriverIncomingRecommendation recommendation) { Published.Add(recommendation); Changed?.Invoke(recommendation); }
        public void MarkDismissed(string requestId) => Dismissed.Add(requestId);
        public void MarkHandled(string requestId) => Handled.Add(requestId);
    }
}
