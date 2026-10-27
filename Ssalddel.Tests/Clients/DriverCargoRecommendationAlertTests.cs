using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Driver.Notification;
using DriverWorkKinds = Ssalddel.Contracts.Driver.Settings.DriverWorkKinds;

namespace Ssalddel.Tests.Clients;

public sealed class DriverCargoRecommendationAlertTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LatestOwnerSettingsAndReceptionAreReadBeforeGenericCargoAlert()
    {
        using var f = await Fixture.CreateAsync();
        Assert.True(await f.PublishAsync());
        var alert = Assert.Single(f.Publisher.Posted);
        Assert.Equal(1, f.Settings.Reads); Assert.Equal(1, f.Dispatch.Reads);
        Assert.Equal("driver-a", alert.OwnerId); Assert.Equal(f.Session.SessionRevision, alert.SessionRevision);
        Assert.Equal("request-1", alert.RequestId); Assert.Equal(1, alert.RecommendationRound);
        Assert.Equal(DriverWorkKinds.CargoTransport, alert.WorkKind);
        Assert.Equal(new NotificationAlertPlan(true, true, true), alert.Plan);
        var visual = DriverRecommendationAlertPresentation.Resolve(alert.WorkKind)!;
        Assert.DoesNotContain("private", visual.Title + visual.Body);
        Assert.Equal("ic_driver_cargo", visual.AndroidIconName);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task MasterAndCargoOptInAreBothRequiredAndDisableCancels(bool master, bool cargo)
    {
        using var f = await Fixture.CreateAsync();
        f.Settings.Value!.배차추천알림사용 = master; f.Settings.Value.화물운송추천알림사용 = cargo;
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted);
        Assert.Equal(0, f.Dispatch.Reads); Assert.Equal(1, f.Publisher.Cancelled);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("missing")]
    public async Task UnconfirmedOrForeignSettingsCannotPublish(string reason)
    {
        using var f = await Fixture.CreateAsync();
        if (reason == "missing") f.Settings.Value = null;
        else f.Settings.Value!.DriverId = "driver-b";
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted);
        Assert.Equal(0, f.Dispatch.Reads); Assert.Equal(1, f.Publisher.Cancelled);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("paused")]
    [InlineData("auto-paused")]
    [InlineData("unknown")]
    [InlineData("owner")]
    [InlineData("unconfirmed-intent")]
    public async Task ReceptionOffPausedOrUnconfirmedDoesNotAlertOrChangeIntent(string reason)
    {
        using var f = await Fixture.CreateAsync();
        if (reason == "off") f.Dispatch.Value.수신의사Code = 운영배차수신의사Code.Off;
        if (reason == "paused") f.Dispatch.Value.실효상태Code = 운영배차실효상태Code.서버일시정지;
        if (reason == "unknown") f.Dispatch.Value.실효상태Code = 운영배차실효상태Code.연결확인불가;
        if (reason == "auto-paused") f.Dispatch.Value.미응답자동중지여부 = true;
        if (reason == "owner") f.Dispatch.Value.주체Id = "driver-b";
        if (reason == "unconfirmed-intent") f.Dispatch.Value.수신의사변경시각Utc = null;
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted); Assert.Equal(1, f.Publisher.Cancelled);
        Assert.Equal(0, f.Dispatch.Commands);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("sample")]
    [InlineData("work-unknown")]
    [InlineData("off-duty")]
    [InlineData("missing")]
    [InlineData("round")]
    [InlineData("food")]
    public async Task OnlyCurrentServerCargoRecommendationCanReadAlertPreferences(string reason)
    {
        using var f = await Fixture.CreateAsync();
        if (reason == "expired") f.Offer.추천만료시각 = Now.UtcDateTime;
        if (reason == "future") f.Offer.추천시작시각 = Now.AddMinutes(1).UtcDateTime;
        if (reason == "sample") f.Samples.ReadState = f.Samples.ReadState with { Recommendations = DriverWorkspaceSliceReadState.Sample() };
        if (reason == "work-unknown") f.Samples.ReadState = f.Samples.ReadState with { Work = DriverWorkspaceSliceReadState.Unknown("work") };
        if (reason == "off-duty") f.Samples.Work = f.Samples.Work with { 운행상태 = "운행종료" };
        if (reason == "missing") f.Samples.Items = [];
        if (reason == "round") f.Samples.Items = [new() { 의뢰Id = "request-1", 추천라운드 = 2 }];
        if (reason == "food") f.Offer.WorkKind = DriverWorkKinds.FoodDelivery;
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted); Assert.Equal(0, f.Settings.Reads);
    }

    [Fact]
    public async Task QuietHoursSuppressSoundAndVibrationWithoutSuppressingTheAlert()
    {
        using var f = await Fixture.CreateAsync();
        f.Clock.UtcNow = new(2026, 10, 15, 14, 0, 0, TimeSpan.Zero);
        f.Offer.추천만료시각 = f.Clock.UtcNow.AddMinutes(1).UtcDateTime;
        f.Settings.Value!.야간알림제한 = true;
        Assert.True(await f.PublishAsync());
        Assert.Equal(new NotificationAlertPlan(true, false, false), Assert.Single(f.Publisher.Posted).Plan);
    }

    [Theory]
    [InlineData("driver-b")]
    [InlineData("driver-a")]
    public async Task OwnerSwitchOrSameOwnerReloginDuringReadCannotPost(string nextOwner)
    {
        using var f = await Fixture.CreateAsync();
        f.Settings.Wait = () => f.Session.ApplyAsync(Snapshot(nextOwner));
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted);
        Assert.True(f.Publisher.Cancelled > 0); Assert.Equal(0, f.Dispatch.Reads);
    }

    [Fact]
    public async Task DisablingWhileAnOldReadIsInFlightCannotRestoreAnAlert()
    {
        using var f = await Fixture.CreateAsync();
        f.Settings.Wait = () => { f.Service.Cancel(); return Task.CompletedTask; };
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted);
    }

    [Fact]
    public async Task ExpiryDuringSettingsReadCannotPublishAfterTheRead()
    {
        using var f = await Fixture.CreateAsync();
        f.Settings.Wait = () => { f.Clock.UtcNow = Now.AddMinutes(3); return Task.CompletedTask; };
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted);
    }

    [Fact]
    public async Task IgnoredTransportCancellationCannotPostAfterCallbackBudget()
    {
        using var f = await Fixture.CreateAsync(); using var cancellation = new CancellationTokenSource();
        f.Settings.Wait = () => { cancellation.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.PublishAsync(cancellation.Token));
        Assert.Empty(f.Publisher.Posted); Assert.Equal(0, f.Dispatch.Reads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelledForegroundReadFailureCannotCancelANewerPublishedAlert(bool settingsRead)
    {
        using var f = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task LateFailure()
        {
            entered.SetResult();
            await release.Task;
            throw new InvalidOperationException("late transport failure");
        }
        if (settingsRead) f.Settings.Wait = LateFailure;
        else f.Dispatch.Wait = LateFailure;
        var old = f.PublishAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Settings.Wait = null; f.Dispatch.Wait = null;
        Assert.True(await f.PublishAsync());
        var priorCancels = f.Publisher.Cancelled;
        cancellation.Cancel(); release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => old);
        Assert.Single(f.Publisher.Posted);
        Assert.Equal(priorCancels, f.Publisher.Cancelled);
    }

    [Fact]
    public async Task CancellationImmediatelyBeforeOsPostIsRechecked()
    {
        using var f = await Fixture.CreateAsync(); using var cancellation = new CancellationTokenSource();
        f.Publisher.BeforePost = cancellation.Cancel;
        Assert.False(await f.PublishAsync(cancellation.Token)); Assert.Empty(f.Publisher.Posted);
    }

    [Fact]
    public async Task AccountChangeImmediatelyBeforeOsPostIsRechecked()
    {
        using var f = await Fixture.CreateAsync();
        f.Publisher.BeforePost = () => f.Session.ApplyAsync(Snapshot("driver-b")).GetAwaiter().GetResult();
        Assert.False(await f.PublishAsync()); Assert.Empty(f.Publisher.Posted);
    }

    [Fact]
    public void DuplicateRoundGuardExpiresAndRetainsOnlyBoundedLiveRecords()
    {
        var alerts = DriverCargoRecommendationPublicationGate.Record([], "request-1", 1, Now.AddMinutes(1), Now);
        Assert.True(DriverCargoRecommendationPublicationGate.IsDuplicate(alerts, "request-1", 1, Now));
        Assert.False(DriverCargoRecommendationPublicationGate.IsDuplicate(alerts, "request-1", 2, Now));
        Assert.False(DriverCargoRecommendationPublicationGate.IsDuplicate(alerts, "request-1", 1, Now.AddMinutes(1)));
        for (var i = 0; i < 110; i++) alerts = DriverCargoRecommendationPublicationGate.Record(alerts,
            "fixture-" + i, 1, Now.AddMinutes(1), Now);
        Assert.Equal(100, alerts.Count);
    }

    private static ClientAuthTokenSnapshot Snapshot(string owner) => new("fixture-access", Now.AddDays(100).UtcDateTime,
        "fixture-refresh", Now.AddDays(100).UtcDateTime, owner, owner, ["기사"]);

    private sealed class Fixture : IDisposable
    {
        public AuthSession Session { get; } = new(new TokenStore(), new ClientSessionGuard());
        public Samples Samples { get; } = new(); public Settings Settings { get; } = new();
        public Dispatch Dispatch { get; } = new(); public Publisher Publisher { get; } = new(); public Clock Clock { get; } = new();
        public DriverCargoRecommendationAlertService Service { get; private set; } = null!;
        public DriverRequestItem Offer { get; } = new() { 의뢰Id = "request-1", 추천라운드 = 1,
            추천시작시각 = Now.AddSeconds(-10).UtcDateTime, 추천만료시각 = Now.AddMinutes(2).UtcDateTime,
            화물종류 = "private payload content", 픽업지 = "private pickup", 하차지 = "private dropoff" };
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); await f.Session.ApplyAsync(Snapshot("driver-a")); f.Samples.Items = [f.Offer];
            f.Service = new(f.Session, f.Samples, f.Settings, f.Dispatch, f.Publisher, f.Clock); return f;
        }
        public Task<bool> PublishAsync(CancellationToken ct = default)
            => Service.TryPublishAsync("driver-a", Session.SessionRevision, Offer, ct);
        public void Dispose() => Service.Dispose();
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset UtcNow { get; set; } = Now; public override DateTimeOffset GetUtcNow() => UtcNow; }
    private sealed class Publisher : IDriverCargoRecommendationAlertPublisher
    {
        public List<DriverCargoRecommendationAlert> Posted { get; } = []; public int Cancelled; public Action? BeforePost;
        public bool Publish(DriverCargoRecommendationAlert alert, Func<bool> canPublish, CancellationToken ct)
        { BeforePost?.Invoke(); if (ct.IsCancellationRequested || !canPublish()) return false; Posted.Add(alert); return true; }
        public void Cancel() => Cancelled++;
    }
    private sealed class Settings : IDriverNotificationApiService
    {
        public 기사알림설정응답? Value = new() { DriverId = "driver-a", 배차추천알림사용 = true,
            화물운송추천알림사용 = true, 소리사용 = true, 진동사용 = true };
        public int Reads; public Func<Task>? Wait;
        public async Task<기사알림설정응답?> 설정조회Async(CancellationToken cancellationToken = default)
        { Reads++; if (Wait is not null) await Wait(); return Value; }
        public Task<기사알림설정응답?> 설정수정Async(기사알림설정수정요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<기사푸시토큰응답?> 푸시토큰조회Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<기사푸시토큰응답?> 푸시토큰등록Async(기사푸시토큰등록요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task 푸시토큰삭제Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<기사알림함목록응답?> 알림함조회Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<기사알림함항목응답?> 읽음처리Async(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Dispatch : IDriverOperationalDispatchApiService
    {
        public 운영배차수신상태Dto Value = new() { 주체Id = "driver-a", 수신의사Code = 운영배차수신의사Code.On,
            실효상태Code = 운영배차실효상태Code.배차가능, 수신의사변경시각Utc = Now.AddMinutes(-1) };
        public int Reads, Commands; public Func<Task>? Wait;
        public async Task<운영배차수신상태Dto> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        { Reads++; if (Wait is not null) await Wait(); return Value; }
        public Task<운영배차수신상태Dto> ChangeIntentAsync(운영배차수신의사변경요청 request, CancellationToken cancellationToken = default)
        { Commands++; throw new NotSupportedException(); }
        public Task AcknowledgeOfferPresentationAsync(string requestId, int round, CancellationToken cancellationToken = default)
        { Commands++; throw new NotSupportedException(); }
    }
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? _value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_value);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default) { _value = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { _value = null; return Task.CompletedTask; }
    }
    private sealed class Samples : IDriverSampleDataService
    {
        public DriverWorkspaceReadState ReadState { get; set; } = new()
        { Work = DriverWorkspaceSliceReadState.Current(), Recommendations = DriverWorkspaceSliceReadState.Current() };
        public IReadOnlyList<DriverRequestItem> Items = [];
        public 기사근무샘플상태 Work = new("fixture", "운행중", "fixture", "fixture", null, Now.UtcDateTime, 1, 0);
        public Task RefreshAsync(CancellationToken cancellationToken = default, bool force = false) => throw new NotSupportedException();
        public 기사근무샘플상태 근무상태 => Work; public 기사현재위치샘플 기사현재위치 => null!; public 기사정산샘플요약 정산요약 => null!;
        public IReadOnlyList<DriverRequestItem> 추천의뢰목록 => Items; public IReadOnlyList<기사예약샘플항목> 예약목록 => [];
        public IReadOnlyList<기사운송샘플항목> 운송목록 => []; public IReadOnlyList<기사알림샘플항목> 알림목록 => [];
        public DriverRequestItem? 추천의뢰조회(string id) => Items.FirstOrDefault(x => x.의뢰Id == id);
        public IReadOnlyList<추천의뢰표시항목> 거리포함추천의뢰목록조회() => [];
        public 기사운송샘플항목? 운송조회(long id) => null; public 기사운송샘플항목? 현재운송조회() => null;
    }
}
