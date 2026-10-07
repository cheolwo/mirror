using System.Net;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverAuthenticationEpochTests
{
    [Theory]
    [InlineData("login", HttpStatusCode.OK)]
    [InlineData("refresh", HttpStatusCode.OK)]
    [InlineData("refresh", HttpStatusCode.Unauthorized)]
    public async Task DelayedAuthenticationResponse_CannotReplaceOrClearNewOwner(string operation, HttpStatusCode status)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FDriverTestSession(expired: operation == "refresh");
        var handler = new FDriverTestHttpHandler((_, _) => { entered.SetResult(); return released.Task; });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        var auth = new FDriverAuthApiService(http, session);
        Task action = operation == "login" ? auth.LoginAsync("a", "test-only") : auth.EnsureAccessTokenResultAsync();
        await entered.Task;
        await session.ApplyAsync(Token("driver-b"));
        released.SetResult(status == HttpStatusCode.OK ? FDriverTestHttpHandler.TokenResponse() : new(status));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
        Assert.Equal("driver-b", session.UserId); Assert.Equal(1, session.ApplyCount); Assert.Equal(0, session.ClearCount);
    }

    [Fact]
    public async Task DelayedProtectedUnauthorized_CannotRefreshAgainstNewOwner()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler((_, _) => { entered.TrySetResult(); return released.Task; });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));
        var action = api.GetWorkspaceAsync(); await entered.Task;
        await session.ApplyAsync(Token("driver-b")); released.SetResult(new(HttpStatusCode.Unauthorized));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
        Assert.Equal("driver-b", session.UserId); Assert.Equal(0, session.ClearCount);
        Assert.Equal(["/api/v1/driver/food-deliveries/workspace"], handler.Paths);
    }

    [Fact]
    public async Task NativeSessionConditionalApplyAndClear_RejectOldRevisionWithinMutationGate()
    {
        var store = new TokenStore(); var session = new FDriverAuthSession(new ClientSessionGuard(), store);
        await session.ApplyAsync(Token("driver-a")); var revision = session.SessionRevision;
        await session.ApplyAsync(Token("driver-b"));
        Assert.False(await session.TryApplyAsync(Token("driver-a"), revision));
        Assert.False(await session.TryClearAsync(revision));
        Assert.Equal("driver-b", session.UserId); Assert.Equal("driver-b", store.Saved!.UserId); Assert.Equal(0, store.Clears);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForegroundCleanupJoiningOldMonitor_CannotClearReplacementOwner(bool explicitLogout)
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi
        {
            ChangeIntent = (_, _) => throw new FDriverApiException("다시 로그인", HttpStatusCode.Unauthorized)
        };
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        var monitor = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FDriverLifecycleTestSupport.MonitorTask(model, monitor.Task);
        var cleanup = explicitLogout
            ? model.LogoutCommand.ExecuteAsync(null)
            : model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        Assert.False(cleanup.IsCompleted);
        Assert.True(FDriverLifecycleTestSupport.WorkspaceToken(model).IsCancellationRequested);

        await session.ApplyAsync(Token("driver-b"));
        monitor.SetResult();
        await cleanup.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("driver-b", session.UserId);
        Assert.Equal(0, session.ClearCount);
        Assert.True(model.IsAuthenticated);
        Assert.Empty(model.ActiveDeliveryItems);
        Assert.Empty(model.RecommendedTicketItems);
        await model.StopMonitoringAsync();
    }

    private static ClientAuthTokenSnapshot Token(string owner) => new("access-" + owner, DateTime.UtcNow.AddMinutes(30), "refresh-" + owner,
        DateTime.UtcNow.AddHours(1), owner, "기사", ["Driver"]);
    private sealed class TokenStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Saved;
        public int Clears;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) { Saved = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Saved = null; Clears++; return Task.CompletedTask; }
    }
}
