using Ssalddel.Client.Infrastructure.Security;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperAuthSessionRecoveryTests
{
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
