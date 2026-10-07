using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class CargoNativeHomeWorkflowTests
{
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
    public async Task FailedReturn_DoesNotPresentCachedTransportAsCurrentWork()
    {
        var samples = new Samples();
        var state = new DriverNativeHomeWorkspaceState(samples, new Auth());
        await state.ActivateAsync();
        Assert.NotNull(state.CurrentTransport);
        samples.Refresh = _ => throw new HttpRequestException("offline");
        await state.ActivateAsync();
        Assert.False(state.IsReady);
        Assert.False(state.IsLoading);
        Assert.Null(state.CurrentTransport);
        Assert.Contains("다시 시도", state.ErrorMessage);
        state.Deactivate();
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedDriver_DiscardsPreviousOwnerResponseAndFailure(bool failOldRead)
    {
        var delayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var samples = new Samples { Refresh = _ => delayed.Task };
        var auth = new Auth();
        var state = new DriverNativeHomeWorkspaceState(samples, auth);
        var invalidations = 0;
        state.Invalidated += () => ++invalidations;
        var load = state.ActivateAsync();
        await auth.ApplyAsync(Snapshot("driver-b"));
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
        public 기사운송샘플항목? ServerTransport { get; set; } = Transport("상차 전", "상차 확인");
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
        public 기사근무샘플상태 근무상태 => new("기사", "운행 중", "현재 위치", "상차지", null, DateTime.UtcNow, 0, 0);
        public 기사현재위치샘플 기사현재위치 => new("현재 위치", 37.5m, 127m, DateTime.UtcNow);
        public 기사정산샘플요약 정산요약 => new(2026, 10, 0, 0, 0, false, []);
        public IReadOnlyList<DriverRequestItem> 추천의뢰목록 => [];
        public IReadOnlyList<기사예약샘플항목> 예약목록 => [];
        public IReadOnlyList<기사운송샘플항목> 운송목록 => cached is null ? [] : [cached];
        public IReadOnlyList<기사알림샘플항목> 알림목록 => [];
        public DriverRequestItem? 추천의뢰조회(string 의뢰Id) => null;
        public IReadOnlyList<추천의뢰표시항목> 거리포함추천의뢰목록조회() => [];
        public 기사운송샘플항목? 운송조회(long 운송Id) => cached?.Id == 운송Id ? cached : null;
        public 기사운송샘플항목? 현재운송조회() => cached;
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
