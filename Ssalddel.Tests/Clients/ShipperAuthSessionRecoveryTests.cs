using Ssalddel.Client.Infrastructure.Security;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperAuthSessionRecoveryTests
{
    [Fact]
    public async Task 갱신commit뒤취소는_신Token저장을마친뒤알리고_조회판본을유지한다()
    {
        var store = new ShipperRecoveryTokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        var original = ShipperRecoveryFixture.Snapshot();
        await session.ApplyAsync(original);
        var revision = session.SessionRevision;
        var fresh = ShipperRecoveryFixture.Snapshot(token: "committed-refresh");
        using var cancellation = new CancellationTokenSource();
        var notifications = 0;
        session.Changed += () => { notifications++; cancellation.Cancel(); };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.TryRefreshAsync(
            fresh, revision, original.RefreshToken, cancellation.Token));
        Assert.Equal(fresh.AccessToken, session.AccessToken);
        Assert.Equal(fresh.RefreshToken, session.RefreshToken);
        Assert.Same(fresh, store.Snapshot);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData("refresh")]
    [InlineData("login")]
    [InlineData("logout")]
    public async Task 저장gate대기중취소된갱신은_마지막저장과새로그인로그아웃을되돌리지않는다(string next)
    {
        var store = new ShipperRecoveryTokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        var original = ShipperRecoveryFixture.Snapshot();
        await session.ApplyAsync(original);
        var revision = session.SessionRevision;
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var savedTokens = new List<string>();
        store.Save = async snapshot =>
        {
            if (!saveStarted.Task.IsCompleted)
            {
                saveStarted.SetResult();
                await releaseSave.Task;
            }
            savedTokens.Add(snapshot.AccessToken);
        };
        // 같은 세션의 이전 저장이 저장 gate를 점유하되 로그인 조회 판본은 바꾸지 않습니다.
        var heldSave = session.ApplyAsync(original, startsNewSession: false);
        await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var fresh = ShipperRecoveryFixture.Snapshot(token: "queued-refresh");
        var newLogin = ShipperRecoveryFixture.Snapshot(user: "shipper-b", token: "new-login");
        using var cancellation = new CancellationTokenSource();
        session.Changed += () => { if (session.AccessToken == fresh.AccessToken) cancellation.Cancel(); };
        var refreshing = session.TryRefreshAsync(fresh, revision, original.RefreshToken, cancellation.Token);
        Task latest = Task.CompletedTask;
        try
        {
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(fresh.AccessToken, session.AccessToken);
            Assert.Same(original, store.Snapshot);
            Assert.False(refreshing.IsCompleted);
            if (next == "login")
            {
                latest = session.ApplyAsync(newLogin);
                Assert.Equal(newLogin.UserId, session.UserId);
                Assert.True(session.SessionRevision > revision);
                Assert.False(latest.IsCompleted);
            }
            else if (next == "logout")
            {
                latest = session.ClearAsync();
                Assert.False(session.IsLoggedIn);
                Assert.True(session.SessionRevision > revision);
                Assert.False(latest.IsCompleted);
            }
        }
        finally { releaseSave.TrySetResult(); }

        await heldSave.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refreshing.WaitAsync(TimeSpan.FromSeconds(5)));
        await latest.WaitAsync(TimeSpan.FromSeconds(5));
        if (next == "refresh")
        {
            Assert.Same(fresh, store.Snapshot);
            Assert.Equal(fresh.AccessToken, session.AccessToken);
            Assert.Equal(revision, session.SessionRevision);
            Assert.Equal(new[] { original.AccessToken, fresh.AccessToken }, savedTokens);
        }
        else if (next == "login")
        {
            Assert.Same(newLogin, store.Snapshot);
            Assert.Equal(newLogin.AccessToken, session.AccessToken);
            Assert.Equal(new[] { original.AccessToken, newLogin.AccessToken }, savedTokens);
        }
        else
        {
            Assert.Null(store.Snapshot);
            Assert.False(session.IsLoggedIn);
            Assert.Equal(new[] { original.AccessToken }, savedTokens);
        }
    }

    [Theory]
    [InlineData("access")]
    [InlineData("access-expired")]
    [InlineData("access-near-expiry")]
    [InlineData("refresh")]
    [InlineData("refresh-expired")]
    [InlineData("refresh-near-expiry")]
    [InlineData("roles")]
    public async Task 같은사용자의잘못된갱신Token도_메모리저장알림을바꾸지않는다(string invalid)
    {
        var saves = 0;
        var store = new ShipperRecoveryTokenStore { Save = _ => { saves++; return Task.CompletedTask; } };
        var session = new AuthSession(store, new ClientSessionGuard());
        var original = ShipperRecoveryFixture.Snapshot();
        await session.ApplyAsync(original);
        var revision = session.SessionRevision;
        var notifications = 0;
        session.Changed += () => notifications++;
        var candidate = ShipperRecoveryFixture.Snapshot(token: "bad-refresh-response");
        candidate = invalid switch
        {
            "access" => candidate with { AccessToken = "" },
            "access-expired" => candidate with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) },
            "access-near-expiry" => candidate with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(90) },
            "refresh" => candidate with { RefreshToken = "" },
            "refresh-expired" => candidate with { RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) },
            "refresh-near-expiry" => candidate with { RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(90) },
            _ => candidate with { Roles = null! }
        };

        Assert.False(await session.TryRefreshAsync(candidate, revision, original.RefreshToken));
        Assert.Equal(original.AccessToken, session.AccessToken);
        Assert.Equal(original.RefreshToken, session.RefreshToken);
        Assert.Same(original.Roles, session.Roles);
        Assert.Same(original, store.Snapshot);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal(1, saves);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task 잘못된갱신Token이어도_명시취소를정상거절로바꾸지않는다()
    {
        var store = new ShipperRecoveryTokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        var original = ShipperRecoveryFixture.Snapshot();
        await session.ApplyAsync(original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.TryRefreshAsync(
            original with { RefreshToken = "" }, session.SessionRevision, original.RefreshToken, cancellation.Token));
        Assert.Same(original, store.Snapshot);
        Assert.Equal(original.AccessToken, session.AccessToken);
    }

    [Fact]
    public async Task 로그아웃없는동일사용자재로그인도_새판본이며_이전조건부변경을거부한다()
    {
        var store = new ShipperRecoveryTokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        await session.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var revision = session.SessionRevision;
        var refreshToken = session.RefreshToken;
        await session.ApplyAsync(ShipperRecoveryFixture.Snapshot(token: "same-user-new-login"));
        var newRevision = session.SessionRevision;
        Assert.True(newRevision > revision);
        Assert.False(await session.TryRefreshAsync(ShipperRecoveryFixture.Snapshot(token: "old-refresh"), revision, refreshToken));
        Assert.False(await session.TryClearAsync(revision, refreshToken));
        Assert.Equal("same-user-new-login", session.AccessToken);
        Assert.Equal("same-user-new-login", store.Snapshot?.AccessToken);
        Assert.Equal(newRevision, session.SessionRevision);
    }

    [Fact]
    public async Task 같은판본에서도_이미회전된RefreshToken의조건부변경은거부한다()
    {
        var session = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var revision = session.SessionRevision;
        var oldRefresh = session.RefreshToken;
        Assert.True(await session.TryRefreshAsync(ShipperRecoveryFixture.Snapshot(token: "fresh"), revision, oldRefresh));
        var notifications = 0;
        session.Changed += () => notifications++;
        Assert.False(await session.TryRefreshAsync(ShipperRecoveryFixture.Snapshot(token: "obsolete"), revision, oldRefresh));
        Assert.False(await session.TryClearAsync(revision, oldRefresh));
        Assert.Equal("fresh", session.AccessToken);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task 토큰갱신으로_다른사용자에게변경하거나_새로그인을시작할수없다()
    {
        var session = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var revision = session.SessionRevision;
        Assert.False(await session.TryRefreshAsync(ShipperRecoveryFixture.Snapshot(user: "foreign", token: "foreign"), revision, session.RefreshToken));
        Assert.Equal("shipper-a", session.UserId);
        Assert.Equal(revision, session.SessionRevision);
    }

    [Fact]
    public async Task 새로그인과사용자변경은_새판본이고_동일사용자갱신은같은판본이다()
    {
        var session = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        var notifications = 0;
        session.Changed += () => notifications++;
        await session.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var first = session.SessionRevision;
        Assert.True(await session.TryRefreshAsync(ShipperRecoveryFixture.Snapshot(token: "refreshed"), first, session.RefreshToken));
        Assert.Equal(first, session.SessionRevision);
        await session.ApplyAsync(ShipperRecoveryFixture.Snapshot(user: "shipper-b", token: "b"));
        Assert.True(session.SessionRevision > first);
        var switched = session.SessionRevision;
        await session.ClearAsync();
        Assert.True(session.SessionRevision > switched);
        Assert.Equal(4, notifications);
    }

    [Fact]
    public async Task 저장소삭제실패도_메모리와구독화면은즉시로그아웃된다()
    {
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        fixture.Store.FailClear = true;
        var revision = fixture.Auth.SessionRevision;
        var notified = false;
        fixture.Auth.Changed += () => notified = !fixture.Auth.IsLoggedIn;
        await Assert.ThrowsAsync<IOException>(() => fixture.Auth.ClearAsync());
        Assert.True(notified);
        Assert.True(fixture.Auth.SessionRevision > revision);
        Assert.Null(fixture.Auth.UserId);
        Assert.Null(fixture.Auth.AccessToken);
        Assert.Null(fixture.Auth.RefreshToken);
        Assert.True(fixture.Model.State.RequiresLogin);
    }

    [Fact]
    public async Task 지연된저장소복원은_새로그인을덮지않는다()
    {
        var restored = new TaskCompletionSource<ClientAuthTokenSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ShipperRecoveryTokenStore { Load = () => restored.Task };
        var session = new AuthSession(store, new ClientSessionGuard());
        var restoring = session.RestoreAsync();
        await session.ApplyAsync(ShipperRecoveryFixture.Snapshot(user: "new-user", token: "new"));
        var revision = session.SessionRevision;
        restored.SetResult(ShipperRecoveryFixture.Snapshot(user: "old-user", token: "old"));
        await restoring;
        Assert.Equal("new-user", session.UserId);
        Assert.Equal("new", session.AccessToken);
        Assert.Equal(revision, session.SessionRevision);
    }

    [Fact]
    public async Task 이전저장이늦어져도_로그아웃저장결과를되돌리지않는다()
    {
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ShipperRecoveryTokenStore { Save = _ => { saveStarted.SetResult(); return releaseSave.Task; } };
        var session = new AuthSession(store, new ClientSessionGuard());
        var saving = session.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        await saveStarted.Task;
        var clearing = session.ClearAsync();
        Assert.False(session.IsLoggedIn);
        releaseSave.SetResult();
        await Task.WhenAll(saving, clearing);
        Assert.Null(store.Snapshot);
        Assert.False(session.IsLoggedIn);
    }

    [Fact]
    public async Task 유효한복원은_한번알리고_반복복원은현재세션을유지한다()
    {
        var store = new ShipperRecoveryTokenStore { Snapshot = ShipperRecoveryFixture.Snapshot() };
        var session = new AuthSession(store, new ClientSessionGuard());
        var notifications = 0;
        session.Changed += () => notifications++;
        await session.RestoreAsync();
        var revision = session.SessionRevision;
        await session.RestoreAsync();
        Assert.True(session.IsLoggedIn);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal(1, notifications);
    }
}
