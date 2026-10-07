using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverSessionRestoreOrderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedInitialRestore_CannotReplaceNewAccountOrClearItsStorage(bool emptyStoredSession)
    {
        var store = new DeferredStore();
        var session = new FDriverAuthSession(new ClientSessionGuard(), store);
        var restore = session.RestoreAsync();
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var current = Snapshot("driver-b");
        await session.ApplyAsync(current);
        store.Load.SetResult(emptyStoredSession ? null : Snapshot("driver-a"));
        Assert.Equal(ClientAuthSessionRestoreState.Authenticated, await restore);
        Assert.Equal("driver-b", session.UserId);
        Assert.Equal(current, store.Saved);
        Assert.Equal(0, store.ClearCalls);
    }

    [Fact]
    public async Task DelayedInitialRestore_CannotUndoLogout()
    {
        var store = new DeferredStore();
        var session = new FDriverAuthSession(new ClientSessionGuard(), store);
        var restore = session.RestoreAsync();
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.ClearAsync();
        store.Load.SetResult(Snapshot("previous-driver"));
        Assert.Equal(ClientAuthSessionRestoreState.Anonymous, await restore);
        Assert.Null(session.UserId);
        Assert.Null(session.AccessToken);
        Assert.Equal(1, store.ClearCalls);
    }

    [Fact]
    public async Task InitialRestore_ShowsWaitingStateAndBlocksLoginUntilItFinishes()
    {
        var result = new TaskCompletionSource<ClientAuthSessionRestoreState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FDriverTestSession { Restore = () => result.Task };
        await session.ClearAsync();
        var model = FDriverLifecycleTestSupport.Model(session, new());
        var initialize = model.InitializeAsync();
        Assert.True(model.IsRestoringSession);
        Assert.False(model.IsSignedOut);
        Assert.False(model.CanLogin);
        model.LoginId = "new-driver";
        model.Password = "test-only";
        await model.LoginCommand.ExecuteAsync(null);
        Assert.Equal(0, session.ApplyCount);
        result.SetResult(ClientAuthSessionRestoreState.Anonymous);
        await initialize;
        Assert.False(model.IsRestoringSession);
        Assert.True(model.IsSignedOut);
        Assert.True(model.CanLogin);
    }

    private static ClientAuthTokenSnapshot Snapshot(string owner) => new(
        owner + "-access", DateTime.UtcNow.AddHours(1), owner + "-refresh", DateTime.UtcNow.AddDays(1), owner, owner, ["Driver"]);

    private sealed class DeferredStore : IClientSecureTokenStore
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ClientAuthTokenSnapshot?> Load { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ClientAuthTokenSnapshot? Saved { get; private set; }
        public int ClearCalls { get; private set; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        { Started.TrySetResult(); return Load.Task; }
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { Saved = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { Saved = null; ++ClearCalls; return Task.CompletedTask; }
    }
}
