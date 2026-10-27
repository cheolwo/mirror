using DriverApp.Models.Driver;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class DriverForegroundRecommendationSignalTests
{
    [Fact]
    public async Task FreshDisplayedRequestUsesExistingPublisherOncePerRefreshWithoutOwnHistory()
    {
        var auth = new Auth(); var alerts = new Alerts();
        using var signal = new DriverForegroundRecommendationSignal(auth, alerts);
        var selected = Request("selected"); signal.Resume();
        var ticket = signal.BeginRefresh();
        Assert.True(await signal.TryPublishAsync(ticket, selected));
        Assert.False(await signal.TryPublishAsync(ticket, Request("other")));
        var call = Assert.Single(alerts.Calls);
        Assert.Same(selected, call.Request); Assert.Equal("driver-a", call.Owner); Assert.Equal(1, call.Revision);

        // 재조회와 Push의 실제 중복 억제는 같은 기존 publisher의 영속 이력을 사용합니다.
        Assert.True(await signal.TryPublishAsync(signal.BeginRefresh(), selected));
        Assert.Equal(2, alerts.Calls.Count); Assert.Equal(0, alerts.Cancelled);
    }

    [Fact]
    public async Task PausedOrMissingSelectionNeverStartsAnAlertRead()
    {
        var alerts = new Alerts(); using var signal = new DriverForegroundRecommendationSignal(new Auth(), alerts);
        Assert.Null(signal.BeginRefresh());
        Assert.False(await signal.TryPublishAsync(null, Request("A")));
        signal.Resume(); var ticket = signal.BeginRefresh();
        Assert.False(await signal.TryPublishAsync(ticket, null));
        signal.Pause();
        Assert.False(await signal.TryPublishAsync(ticket, Request("A")));
        Assert.Empty(alerts.Calls); Assert.Equal(0, alerts.Cancelled);
    }

    [Fact]
    public async Task ClearingTheMapInvalidatesItsSelectionWithoutLeavingForeground()
    {
        var alerts = new Alerts(); using var signal = new DriverForegroundRecommendationSignal(new Auth(), alerts);
        signal.Resume(); var ticket = signal.BeginRefresh(); signal.Invalidate();
        Assert.False(await signal.TryPublishAsync(ticket, Request("old")));
        Assert.True(await signal.TryPublishAsync(signal.BeginRefresh(), Request("fresh")));
        Assert.Single(alerts.Calls); Assert.Equal(0, alerts.Cancelled);
    }

    [Fact]
    public async Task BackgroundCancelsLateIgnoredReadWithoutCancellingAnotherPathsOsAlert()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var posted = false; var alerts = new Alerts();
        alerts.Publish = async (_, token) =>
        {
            await release.Task; posted = !token.IsCancellationRequested; finished.SetResult(); return posted;
        };
        using var signal = new DriverForegroundRecommendationSignal(new Auth(), alerts);
        signal.Resume(); var pending = signal.TryPublishAsync(signal.BeginRefresh(), Request("A"));
        signal.Pause();
        Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(1)));
        release.SetResult(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(posted); Assert.Equal(0, alerts.Cancelled);
    }

    [Fact]
    public async Task NewRefreshFencesOldReadButAllowsTheFreshlySelectedRequest()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var alerts = new Alerts
        {
            Publish = async (call, token) =>
            {
                if (call.Request.의뢰Id != "old") return true;
                await release.Task; finished.SetResult(); throw new HttpRequestException("late failure");
            }
        };
        using var signal = new DriverForegroundRecommendationSignal(new Auth(), alerts);
        signal.Resume(); var old = signal.BeginRefresh();
        var pending = signal.TryPublishAsync(old, Request("old"));
        var current = signal.BeginRefresh();
        Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.True(await signal.TryPublishAsync(current, Request("fresh")));
        Assert.False(await signal.TryPublishAsync(old, Request("old")));
        release.SetResult(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(2, alerts.Calls.Count); Assert.Equal(0, alerts.Cancelled);
    }

    [Theory]
    [InlineData("driver-b")]
    [InlineData("driver-a")]
    public async Task AccountSwitchOrSameOwnerNewSessionFencesTheOldAttempt(string nextOwner)
    {
        var auth = new Auth(); var alerts = new Alerts();
        using var signal = new DriverForegroundRecommendationSignal(auth, alerts);
        signal.Resume(); var old = signal.BeginRefresh();
        await auth.ApplyAsync(Snapshot(nextOwner));
        Assert.False(await signal.TryPublishAsync(old, Request("old")));
        Assert.True(await signal.TryPublishAsync(signal.BeginRefresh(), Request("fresh")));
        var call = Assert.Single(alerts.Calls);
        Assert.Equal(nextOwner, call.Owner); Assert.Equal(2, call.Revision); Assert.Equal(0, alerts.Cancelled);
    }

    [Fact]
    public async Task SessionChangeDuringAlertReadCancelsBeforeItsFinalPost()
    {
        var auth = new Auth(); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var posted = false; var alerts = new Alerts
        {
            Publish = async (_, token) =>
            { await release.Task; posted = !token.IsCancellationRequested; finished.SetResult(); return posted; }
        };
        using var signal = new DriverForegroundRecommendationSignal(auth, alerts);
        signal.Resume(); var pending = signal.TryPublishAsync(signal.BeginRefresh(), Request("old"));
        await auth.ApplyAsync(Snapshot("driver-b"));
        Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(1)));
        release.SetResult(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(posted); Assert.Equal(0, alerts.Cancelled);
    }

    [Fact]
    public async Task FirstCanonicalRefreshCanBindTheSessionRestoredDuringItsRead()
    {
        var auth = new Auth(); await auth.ClearAsync(); var alerts = new Alerts();
        using var signal = new DriverForegroundRecommendationSignal(auth, alerts);
        signal.Resume(); var ticket = signal.BeginRefresh();
        await auth.ApplyAsync(Snapshot("restored"));
        Assert.True(await signal.TryPublishAsync(ticket, Request("A")));
        var call = Assert.Single(alerts.Calls); Assert.Equal("restored", call.Owner);
        Assert.Equal(auth.SessionRevision, call.Revision);
    }

    [Fact]
    public async Task AlertFailureIsContainedAndResumeDoesNotRetainTheDepartedAttempt()
    {
        var auth = new Auth(); var alerts = new Alerts { Publish = (_, _) => throw new HttpRequestException("offline") };
        using var signal = new DriverForegroundRecommendationSignal(auth, alerts);
        signal.Resume(); var old = signal.BeginRefresh();
        Assert.False(await signal.TryPublishAsync(old, Request("old")));
        signal.Pause(); Assert.Equal(0, auth.Subscribers);
        signal.Resume(); Assert.Equal(1, auth.Subscribers);
        Assert.False(await signal.TryPublishAsync(old, Request("old")));
        alerts.Publish = (_, _) => Task.FromResult(true);
        Assert.True(await signal.TryPublishAsync(signal.BeginRefresh(), Request("fresh")));
        Assert.Equal(0, alerts.Cancelled);
    }

    private static DriverRequestItem Request(string id) => new() { 의뢰Id = id, 추천라운드 = 1 };
    private static ClientAuthTokenSnapshot Snapshot(string owner) => new("fixture-access", DateTime.UtcNow.AddHours(1),
        "fixture-refresh", DateTime.UtcNow.AddDays(1), owner, owner, ["Driver"]);
    private sealed record Call(string Owner, long Revision, DriverRequestItem Request);

    private sealed class Alerts : IDriverCargoRecommendationAlertService
    {
        public List<Call> Calls { get; } = []; public int Cancelled;
        public Func<Call, CancellationToken, Task<bool>> Publish { get; set; } = (_, _) => Task.FromResult(true);
        public Task<bool> TryPublishAsync(string ownerId, long sessionRevision, DriverRequestItem request,
            CancellationToken cancellationToken = default)
        { var call = new Call(ownerId, sessionRevision, request); Calls.Add(call); return Publish(call, cancellationToken); }
        public void Cancel() => Cancelled++;
    }

    private sealed class Auth : IAuthSession
    {
        private Action? _changed;
        public int Subscribers { get; private set; }
        public string? AccessToken => IsAuthenticated ? "fixture-access" : null;
        public string? RefreshToken => IsAuthenticated ? "fixture-refresh" : null;
        public DateTime AccessTokenExpiresAtUtc => DateTime.UtcNow.AddHours(1);
        public DateTime RefreshTokenExpiresAtUtc => DateTime.UtcNow.AddDays(1);
        public string? UserId { get; private set; } = "driver-a";
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => ["Driver"];
        public bool IsAuthenticated => UserId is not null;
        public long Version { get; private set; } = 1;
        public long SessionRevision { get; private set; } = 1;
        public event Action? Changed
        { add { _changed += value; ++Subscribers; } remove { _changed -= value; --Subscribers; } }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { UserId = snapshot.UserId; ++Version; ++SessionRevision; _changed?.Invoke(); return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { UserId = null; ++Version; ++SessionRevision; _changed?.Invoke(); return Task.CompletedTask; }
        public async Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion,
            CancellationToken cancellationToken = default, bool startsNewSession = false)
        { if (Version != expectedVersion) return false; await ApplyAsync(snapshot, cancellationToken); return true; }
        public async Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
        { if (Version != expectedVersion) return false; await ClearAsync(cancellationToken); return true; }
    }
}
