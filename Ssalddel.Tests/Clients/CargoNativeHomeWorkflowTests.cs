using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;

namespace Ssalddel.Tests.Clients;

public sealed class CargoNativeHomeWorkflowTests
{
    [Fact]
    public async Task SecondaryReadFailures_StayOnTheirOwnPages_WithoutObscuringOrBlockingMapWork()
    {
        var samples = new Samples
        {
            ReadState = new()
            {
                Work = DriverWorkspaceSliceReadState.Current(), CurrentTransport = DriverWorkspaceSliceReadState.Current(),
                TransportDetail = DriverWorkspaceSliceReadState.Current(), Recommendations = DriverWorkspaceSliceReadState.Current(),
                Realtime = DriverWorkspaceSliceReadState.Current(),
                Settlement = DriverWorkspaceSliceReadState.Failed("월 정산"), Reservations = DriverWorkspaceSliceReadState.Failed("예약")
            }
        };
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), new Dispatch());
        await state.ActivateAsync();
        Assert.True(state.IsReady); Assert.NotNull(state.CurrentTransport);
        Assert.True(state.CanChangeReception); Assert.True(state.CanRespondToRecommendation);
        Assert.Empty(state.MapWarnings);
        Assert.Equal(2, samples.ReadState.Warnings.Count);
        Assert.False(samples.ReadState.Settlement.CanDisplayValues);
        Assert.False(samples.ReadState.Reservations.CanDisplayValues);
        state.Deactivate();
    }

    [Fact]
    public async Task MapWorkFailures_RemainVisibleAlongsideSecondaryReadFailures()
    {
        var samples = new Samples();
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), new Dispatch());
        await state.ActivateAsync();
        samples.ReadState = new()
        {
            Work = DriverWorkspaceSliceReadState.Failed("운행 상태"), CurrentTransport = DriverWorkspaceSliceReadState.Failed("현재 운송"),
            TransportDetail = DriverWorkspaceSliceReadState.Failed("운송 상세"), Recommendations = DriverWorkspaceSliceReadState.Failed("추천 운송"),
            Realtime = DriverWorkspaceSliceReadState.Failed("실시간 연결"),
            Settlement = DriverWorkspaceSliceReadState.Failed("월 정산"), Reservations = DriverWorkspaceSliceReadState.Failed("예약")
        };
        await state.RefreshAsync();
        Assert.Equal(5, state.MapWarnings.Count);
        Assert.Contains(samples.ReadState.CurrentTransport.Message, state.MapWarnings);
        Assert.Contains(samples.ReadState.TransportDetail.Message, state.MapWarnings);
        Assert.Contains(samples.ReadState.Work.Message, state.MapWarnings);
        Assert.Contains(samples.ReadState.Recommendations.Message, state.MapWarnings);
        Assert.Contains(samples.ReadState.Realtime.Message, state.MapWarnings);
        Assert.True(state.HasStaleTransport); Assert.False(state.IsReady);
        Assert.False(state.CanChangeReception); Assert.False(state.CanRespondToRecommendation);
        state.Deactivate();
    }

    [Fact]
    public async Task ReturningHome_AlwaysRefreshesAndShowsServerNextStage()
    {
        var samples = new Samples();
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth());
        samples.ServerTransport = Transport("상차 전", "상차 확인");
        await state.ActivateAsync();
        Assert.Equal("상차 전", state.CurrentTransport?.현재단계);
        state.Deactivate();
        samples.ServerTransport = Transport("하차 이동", "하차 확인");
        await state.ActivateAsync();
        Assert.True(state.IsReady);
        Assert.Equal("하차 이동", state.CurrentTransport?.현재단계);
        Assert.Equal("하차 확인", state.CurrentTransport?.다음행동);
        Assert.Equal(new[] { true, true }, samples.RefreshModes);
        state.Deactivate();
    }

    [Fact]
    public async Task FailedRefresh_PreservesLastConfirmedTransportAsStaleAndBlocksActions()
    {
        var samples = new Samples();
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth());
        await state.ActivateAsync();
        Assert.NotNull(state.CurrentTransport);
        samples.Refresh = _ => throw new HttpRequestException("offline");
        await state.ActivateAsync();
        Assert.False(state.IsReady);
        Assert.False(state.IsLoading);
        Assert.NotNull(state.CurrentTransport);
        Assert.True(state.HasStaleTransport);
        Assert.False(state.CanRespondToRecommendation);
        Assert.Contains("마지막 확인 업무", state.ErrorMessage);
        state.Deactivate();
    }

    [Fact]
    public async Task AcceptedNextCall_DoesNotReplaceCurrentTask_AndCompletionSwitchesOnSameForegroundPoll()
    {
        var clock = new Clock(); var samples = new Samples();
        var first = Transport("하차 이동", "하차 확인") with { Id = 1, 의뢰Id = "A", 하차위도 = 37.6m, 하차경도 = 127.1m };
        var next = Transport("배차확정", "상차 확인") with { Id = 2, 의뢰Id = "B", 픽업위도 = 37.7m, 픽업경도 = 127.2m };
        samples.ServerTransport = first; samples.ServerTransports = [first];
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), new Dispatch(), clock);
        await state.ActivateAsync();
        samples.ServerTransports = [first, next]; // B 수락 뒤에도 서버 다음 행동은 A입니다.
        await state.RefreshAsync();
        Assert.Same(first, state.CurrentTransport); Assert.Equal(2, samples.운송목록.Count);
        var routes = new DriverHomeRoutePlanningService();
        Assert.Equal("A", Assert.Single(routes.BuildCurrentRouteOverlays(samples.기사현재위치, state.CurrentTransport)).RouteId);
        samples.ServerTransport = next; samples.ServerTransports = [next];
        Assert.False(await state.PollAsync()); Assert.Same(first, state.CurrentTransport);
        clock.Now += DriverNativeHomeWorkspaceState.RefreshInterval;
        Assert.True(await state.PollAsync()); Assert.Same(next, state.CurrentTransport);
        Assert.Equal("B", Assert.Single(routes.BuildCurrentRouteOverlays(samples.기사현재위치, state.CurrentTransport)).RouteId);
        Assert.True(state.IsReady); state.Deactivate();
    }

    [Fact]
    public async Task NormalReturnWithFailedCurrentSlice_IsNotAnEmptySuccessOrNextCallTransition()
    {
        var samples = new Samples(); var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), new Dispatch());
        await state.ActivateAsync(); var first = state.CurrentTransport;
        samples.ServerTransport = null;
        samples.ReadState = samples.ReadState with { CurrentTransport = DriverWorkspaceSliceReadState.Failed("현재 운송") };
        await state.RefreshAsync();
        Assert.Same(first, state.CurrentTransport); Assert.True(state.HasStaleTransport); Assert.False(state.IsReady);
        Assert.False(state.CanRespondToRecommendation); Assert.False(state.CanChangeReception);
        samples.ReadState = samples.ReadState with { CurrentTransport = DriverWorkspaceSliceReadState.Current() };
        await state.RefreshAsync(); Assert.True(state.IsReady); Assert.Null(state.CurrentTransport);
        state.Deactivate();
    }

    [Fact]
    public async Task ForegroundPolling_CoalescesReads_AndPausePreventsCallsUntilFreshResume()
    {
        var clock = new Clock(); var samples = new Samples(); var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), new Dispatch(), clock);
        await state.ActivateAsync(); Assert.Single(samples.RefreshModes);
        Assert.False(await state.PollAsync());
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        samples.Refresh = _ => gate.Task;
        clock.Now += DriverNativeHomeWorkspaceState.RefreshInterval;
        var poll = state.PollAsync(); var concurrent = state.RefreshAsync();
        Assert.Equal(2, samples.RefreshModes.Count);
        gate.SetResult(); await Task.WhenAll(poll, concurrent);
        state.Pause(); clock.Now += DriverNativeHomeWorkspaceState.RefreshInterval;
        Assert.False(await state.PollAsync()); Assert.Equal(2, samples.RefreshModes.Count);
        samples.Refresh = null; await state.ActivateAsync();
        Assert.Equal(3, samples.RefreshModes.Count); Assert.True(state.IsReady);
        state.Deactivate();
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task UnknownAcceptanceHttpOutcome_RequeriesWithoutWritingAndKeepsAUntilCanonicalBIsFound(int status)
    {
        var samples = new Samples(); var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), new Dispatch());
        await state.ActivateAsync(); var first = state.CurrentTransport;
        state.KeepUnconfirmedAcceptance("B", 2);
        state.ObserveAcceptanceFailure("B", 2, new DriverApiException((System.Net.HttpStatusCode)status, "수락", null, null));
        await state.RefreshAsync();
        Assert.Same(first, state.CurrentTransport); Assert.Equal("B", state.UnconfirmedAcceptanceRequestId);
        Assert.False(state.CanRespondToRecommendation);
        samples.ServerTransports = [first!, Transport("배차확정", "상차 확인") with { Id = 2, 의뢰Id = "B" }];
        await state.RefreshAsync();
        Assert.Null(state.UnconfirmedAcceptanceRequestId); Assert.Same(first, state.CurrentTransport);
        state.Deactivate();
    }

    [Fact]
    public async Task RecommendationPageRoundTrip_PreservesUnknownAcceptanceAndCurrentWorkUntilCanonicalConfirmation()
    {
        var samples = new Samples();
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), new Dispatch());
        await state.ActivateAsync();
        var first = state.CurrentTransport;
        state.KeepUnconfirmedAcceptance("B", 2);
        var previousGeneration = state.Generation;
        state.Pause(); // 추천 지도 또는 수행 지도로 전환하면서 같은 기사 작업 상태는 보존합니다.
        Assert.False(state.CanRespondToRecommendation);
        Assert.Equal("B", state.UnconfirmedAcceptanceRequestId);
        await state.ActivateAsync();
        Assert.True(state.Generation > previousGeneration);
        Assert.Equal("B", state.UnconfirmedAcceptanceRequestId);
        Assert.False(state.CanRespondToRecommendation);
        Assert.Same(first, state.CurrentTransport);
        samples.ServerTransports = [first!, Transport("배차확정", "상차 확인") with { Id = 2, 의뢰Id = "B" }];
        await state.RefreshAsync();
        Assert.Null(state.UnconfirmedAcceptanceRequestId);
        Assert.Same(first, state.CurrentTransport);
        Assert.True(state.CanRespondToRecommendation);
        state.Deactivate();
    }

    [Fact]
    public async Task VisibleReceipt_InterpretsServerUtcWithoutZoneSuffixAsUtc()
    {
        var dispatch = new Dispatch();
        var samples = new Samples { Offers = [new()
        {
            의뢰Id = "no-zone", 추천라운드 = 1,
            추천시작시각 = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-1), DateTimeKind.Unspecified),
            추천만료시각 = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(1), DateTimeKind.Unspecified)
        }] };
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), dispatch);
        await state.ActivateAsync();
        await state.AcknowledgePresentedAsync("no-zone", 1);
        Assert.Single(dispatch.Receipts);
        state.Deactivate();
    }

    [Fact]
    public async Task RecommendationPagePausedAcrossAccountChange_DoesNotRetainPreviousDriversUnknownAcceptance()
    {
        var auth = new Auth();
        var state = new DriverNativeHomeWorkspaceState(new Samples(), auth, new Dispatch());
        await state.ActivateAsync();
        state.KeepUnconfirmedAcceptance("old-driver-offer", 2);
        state.Pause();
        await auth.ApplyAsync(Snapshot("driver-b"));
        await state.ActivateAsync();
        Assert.Null(state.UnconfirmedAcceptanceRequestId);
        Assert.False(state.CanRespondToRecommendation); // 조회 응답의 다른 기사 소유권도 거부합니다.
        state.Deactivate();
    }

    [Fact]
    public async Task ManualReceptionOn_AloneClearsNoResponsePause_AndVisibleReceiptRetriesNextReadOnly()
    {
        var clock = new Clock(); var samples = new Samples
        {
            Offers = [new DriverRequestItem { 의뢰Id = "B", 추천라운드 = 2, 추천시작시각 = clock.Now.UtcDateTime,
                추천만료시각 = clock.Now.AddMinutes(5).UtcDateTime }]
        };
        var dispatch = new Dispatch { Paused = true, FailReceipt = true };
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), dispatch, clock);
        await state.ActivateAsync(); await state.RefreshAsync();
        Assert.False(state.ReceivesNewDispatches); Assert.Empty(dispatch.Intents);
        await state.AcknowledgePresentedAsync("B", 2); Assert.Empty(dispatch.Receipts);
        await state.ToggleReceptionAsync();
        Assert.True(Assert.Single(dispatch.Intents).미응답자동중지해제); Assert.True(state.ReceivesNewDispatches);
        Assert.Empty(dispatch.Receipts); // 조회·수신 변경 자체는 표시 ACK가 아닙니다.
        await state.AcknowledgePresentedAsync("B", 2); await state.AcknowledgePresentedAsync("B", 2);
        Assert.Single(dispatch.Receipts);
        dispatch.FailReceipt = false; await state.RefreshAsync(); await state.AcknowledgePresentedAsync("B", 2);
        Assert.Equal(2, dispatch.Receipts.Count);
        await state.RefreshAsync(); await state.AcknowledgePresentedAsync("B", 2); Assert.Equal(2, dispatch.Receipts.Count);
        state.Pause(); await state.AcknowledgePresentedAsync("B", 2); Assert.Equal(2, dispatch.Receipts.Count);
        state.Deactivate();
    }

    [Theory]
    [InlineData(true, false, 운영배차실효상태Code.조건부적합, true)]
    [InlineData(true, true, 운영배차실효상태Code.조건부적합, false)]
    [InlineData(true, false, 운영배차실효상태Code.서버일시정지, false)]
    [InlineData(false, false, 운영배차실효상태Code.배차가능, false)]
    [InlineData(true, true, 운영배차실효상태Code.배차가능, true)]
    public async Task ExplicitReceptionAndRecordedRestrictions_MatchInitialAndPausedServerMeaning(
        bool intentRecorded, bool eligibilityRecorded, string effectiveState, bool expected)
    {
        var dispatch = new Dispatch { IntentRecorded = intentRecorded, EligibilityRecorded = eligibilityRecorded, EffectiveState = effectiveState };
        var state = new DriverNativeHomeWorkspaceState(new Samples(), new Auth(), dispatch);
        await state.ActivateAsync();
        Assert.Equal(expected, state.CanRespondToRecommendation); state.Deactivate();
    }

    [Fact]
    public async Task ForeignAvailability_KeepsOwnedCurrentWorkButBlocksReceptionWritesAndDisplayReceipts()
    {
        var samples = new Samples(); var dispatch = new Dispatch { OwnerId = "driver-b" };
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth(), dispatch);
        await state.ActivateAsync();
        Assert.True(state.IsReady); Assert.NotNull(state.CurrentTransport);
        Assert.False(state.ReceptionKnown); Assert.False(state.CanChangeReception); Assert.False(state.CanRespondToRecommendation);
        await state.ToggleReceptionAsync(); await state.AcknowledgePresentedAsync("B", 2);
        Assert.Empty(dispatch.Intents); Assert.Empty(dispatch.Receipts);
        dispatch.OwnerId = "driver-a"; await state.RefreshAsync();
        Assert.True(state.ReceptionKnown); Assert.True(state.CanChangeReception); state.Deactivate();
    }

    [Fact]
    public async Task DepartedHome_DiscardsDelayedTransportResponse()
    {
        var delayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var samples = new Samples { Refresh = _ => delayed.Task };
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth());
        var load = state.ActivateAsync();
        Assert.True(state.IsLoading);
        state.Deactivate();
        delayed.SetResult();
        await load;
        Assert.False(state.IsReady);
        Assert.False(state.IsLoading);
        Assert.Null(state.CurrentTransport);
        Assert.Empty(state.ErrorMessage);
    }

    [Fact]
    public async Task PreviousEntryResponse_CannotReplaceNewEntryStage()
    {
        var delayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var samples = new Samples { Refresh = _ => delayed.Task };
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth());
        var previous = state.ActivateAsync();
        state.Deactivate();
        samples.Refresh = null;
        samples.ServerTransport = Transport("하차 이동", "하차 확인");
        await state.ActivateAsync();
        var current = state.CurrentTransport;
        samples.ServerTransport = Transport("상차 전", "상차 확인");
        delayed.SetResult();
        await previous;
        Assert.True(state.IsReady);
        Assert.Same(current, state.CurrentTransport);
        Assert.Equal("하차 이동", state.CurrentTransport?.현재단계);
        state.Deactivate();
    }

    [Theory]
    [InlineData(false, "driver-b")]
    [InlineData(true, "driver-b")]
    [InlineData(false, "driver-a")]
    [InlineData(true, "driver-a")]
    public async Task ChangedSession_DiscardsPreviousOwnerResponseAndFailure(bool failOldRead, string nextOwner)
    {
        var delayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var samples = new Samples { Refresh = _ => delayed.Task };
        var auth = new Auth();
        var state = new DriverNativeHomeWorkspaceState(samples, auth);
        var invalidations = 0;
        state.Invalidated += () => ++invalidations;
        var load = state.ActivateAsync();
        await auth.ApplyAsync(Snapshot(nextOwner));
        Assert.Equal(1, invalidations);
        Assert.Null(state.CurrentTransport);
        Assert.Contains("로그인 정보가 변경", state.ErrorMessage);
        if (failOldRead) delayed.SetException(new HttpRequestException("previous owner failed"));
        else delayed.SetResult();
        await load;
        Assert.False(state.IsReady);
        Assert.False(state.IsLoading);
        Assert.Null(state.CurrentTransport);
        Assert.Contains("로그인 정보가 변경", state.ErrorMessage);
        state.Deactivate();
    }

    private static 기사운송샘플항목 Transport(string stage, string action) => new(
        1, "transport", "일반 화물", "상차지", "하차지", null, null, null, null,
        stage, DateTime.UtcNow, 3, 10000, false, false, "선결제", action);
    private static ClientAuthTokenSnapshot Snapshot(string owner) => new(
        "access", DateTime.UtcNow.AddHours(1), "refresh", DateTime.UtcNow.AddDays(1), owner, owner, ["Driver"]);

    private sealed class Samples : IDriverSampleDataService
    {
        public DriverWorkspaceReadState ReadState { get; set; } = new()
        {
            Work = DriverWorkspaceSliceReadState.Current(), CurrentTransport = DriverWorkspaceSliceReadState.Current(),
            TransportDetail = DriverWorkspaceSliceReadState.Current(), Recommendations = DriverWorkspaceSliceReadState.Current()
        };
        public 기사운송샘플항목? ServerTransport { get; set; } = Transport("상차 전", "상차 확인");
        public IReadOnlyList<기사운송샘플항목>? ServerTransports { get; set; }
        public IReadOnlyList<DriverRequestItem> Offers { get; set; } = [];
        private 기사운송샘플항목? cached;
        public Func<CancellationToken, Task>? Refresh { get; set; }
        public List<bool> RefreshModes { get; } = [];
        public async Task RefreshAsync(CancellationToken cancellationToken = default, bool force = false)
        {
            RefreshModes.Add(force);
            if (Refresh is not null) await Refresh(cancellationToken);
            // Ignore cancellation deliberately to verify the screen's own lifetime boundary.
            if (force || cached is null) cached = ServerTransport;
        }
        public 기사근무샘플상태 근무상태 => new("기사", "운행중", "현재 위치", "상차지", null, DateTime.UtcNow, 0, 0);
        public 기사현재위치샘플 기사현재위치 => new("현재 위치", 37.5m, 127m, DateTime.UtcNow);
        public 기사정산샘플요약 정산요약 => new(2026, 10, 0, 0, 0, false, []);
        public IReadOnlyList<DriverRequestItem> 추천의뢰목록 => Offers;
        public IReadOnlyList<기사예약샘플항목> 예약목록 => [];
        public IReadOnlyList<기사운송샘플항목> 운송목록 => ServerTransports ?? (cached is null ? [] : [cached]);
        public IReadOnlyList<기사알림샘플항목> 알림목록 => [];
        public DriverRequestItem? 추천의뢰조회(string 의뢰Id) => null;
        public IReadOnlyList<추천의뢰표시항목> 거리포함추천의뢰목록조회() => [];
        public 기사운송샘플항목? 운송조회(long 운송Id) => cached?.Id == 운송Id ? cached : null;
        public 기사운송샘플항목? 현재운송조회() => cached;
    }

    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Dispatch : IDriverOperationalDispatchApiService
    {
        public bool Paused, FailReceipt;
        public bool IntentRecorded = true, EligibilityRecorded;
        public string EffectiveState = 운영배차실효상태Code.배차가능;
        public string OwnerId = "driver-a";
        private string intent = 운영배차수신의사Code.On;
        public List<운영배차수신의사변경요청> Intents { get; } = [];
        public List<(string, int)> Receipts { get; } = [];
        public Task<운영배차수신상태Dto> GetAvailabilityAsync(CancellationToken token = default)
            => Task.FromResult(new 운영배차수신상태Dto { 주체Id = OwnerId, 수신의사Code = intent, 미응답자동중지여부 = Paused,
                수신의사변경시각Utc = IntentRecorded ? DateTimeOffset.UtcNow : null,
                실효상태변경시각Utc = EligibilityRecorded ? DateTimeOffset.UtcNow : null,
                실효상태Code = EffectiveState });
        public Task<운영배차수신상태Dto> ChangeIntentAsync(운영배차수신의사변경요청 request, CancellationToken token = default)
        {
            Intents.Add(request); intent = request.수신의사Code;
            if (request.미응답자동중지해제 && intent == 운영배차수신의사Code.On) Paused = false;
            return GetAvailabilityAsync(token);
        }
        public Task AcknowledgeOfferPresentationAsync(string requestId, int round, CancellationToken token = default)
        {
            Receipts.Add((requestId, round));
            return FailReceipt ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask;
        }
    }

    private sealed class Auth : IAuthSession
    {
        public string? AccessToken { get; private set; } = "access";
        public string? RefreshToken => "refresh";
        public DateTime AccessTokenExpiresAtUtc => DateTime.UtcNow.AddHours(1);
        public DateTime RefreshTokenExpiresAtUtc => DateTime.UtcNow.AddDays(1);
        public string? UserId { get; private set; } = "driver-a";
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => ["Driver"];
        public bool IsAuthenticated => UserId is not null;
        public long Version { get; private set; }
        public long SessionRevision { get; private set; }
        public event Action? Changed;
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            UserId = snapshot.UserId; AccessToken = snapshot.AccessToken; ++Version; ++SessionRevision;
            Changed?.Invoke(); return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            UserId = null; AccessToken = null; ++Version; ++SessionRevision;
            Changed?.Invoke(); return Task.CompletedTask;
        }
        public async Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion,
            CancellationToken cancellationToken = default, bool startsNewSession = false)
        {
            if (Version != expectedVersion) return false;
            await ApplyAsync(snapshot, cancellationToken); return true;
        }
        public async Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
        {
            if (Version != expectedVersion) return false;
            await ClearAsync(cancellationToken); return true;
        }
    }
}
