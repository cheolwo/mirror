using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperAuthIntentRaceTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 서로다른서비스의_역순로그인응답은_최신의도만저장한다(bool sameUser)
    {
        using var fixture = new Fixture();
        var first = fixture.Api.LoginAsync("first", "test");
        var a = await fixture.Handler.NextAsync();
        var second = fixture.NewApi().LoginAsync("second", "test");
        var b = await fixture.Handler.NextAsync();
        b.Complete(Token(sameUser ? "shipper-a" : "shipper-b", "new"));
        Assert.True((await second.WaitAsync(Limit)).IsSuccess);
        var revision = fixture.Auth.SessionRevision;
        a.Complete(Token("shipper-a", "old"));
        Assert.False((await first.WaitAsync(Limit)).IsSuccess);
        Assert.True(a.Token.IsCancellationRequested);
        Assert.Equal("new", fixture.Auth.AccessToken);
        Assert.Equal("new", fixture.Tokens.Snapshot?.AccessToken);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Single(fixture.Decorations.LoadedUsers);
    }

    [Fact]
    public async Task 로그아웃뒤_늦은로그인200은_다시로그인시키지않는다()
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        var login = fixture.Api.LoginAsync("next", "test");
        var pending = await fixture.Handler.NextAsync();
        await fixture.NewApi().LogoutAsync().WaitAsync(Limit);
        pending.Complete(Token("next", "late"));
        Assert.False((await login.WaitAsync(Limit)).IsSuccess);
        Assert.False(fixture.Auth.IsLoggedIn);
        Assert.Null(fixture.Tokens.Snapshot);
        Assert.Empty(fixture.Decorations.LoadedUsers);
    }

    [Theory]
    [InlineData("unauthorized")]
    [InlineData("server")]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("json")]
    [InlineData("null")]
    public async Task 로그인전송실패는_원래세션과저장판본을보존하고_재전송하지않는다(string failure)
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        var revision = fixture.Auth.SessionRevision;
        var login = fixture.Api.LoginAsync("next", "test");
        var pending = await fixture.Handler.NextAsync();
        switch (failure)
        {
            case "network": pending.Response.SetException(new HttpRequestException("private test failure")); break;
            case "timeout": pending.Response.SetException(new TaskCanceledException()); break;
            case "json": pending.Response.SetResult(new(HttpStatusCode.OK) { Content = new StringContent("{invalid") }); break;
            case "null": pending.Response.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create<토큰응답?>(null) }); break;
            default: pending.Response.SetResult(new(failure == "server" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Unauthorized)); break;
        }
        var result = await login.WaitAsync(Limit);
        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("private test failure", result.ErrorMessage);
        Assert.Equal("original", fixture.Auth.UserId);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal("original", fixture.Tokens.Snapshot?.UserId);
        Assert.Empty(fixture.Decorations.LoadedUsers);
        Assert.Equal(1, fixture.Handler.AuthCalls);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("access")]
    [InlineData("access-expired")]
    [InlineData("refresh")]
    [InlineData("refresh-expired")]
    [InlineData("roles")]
    public async Task 잘못된로그인Token은_원래세션을보존하고_부가작업을시작하지않는다(string invalid)
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        var revision = fixture.Auth.SessionRevision;
        var login = fixture.Api.LoginAsync("next", "test");
        var pending = await fixture.Handler.NextAsync();
        var token = Token("next", "new");
        switch (invalid)
        {
            case "owner": token.UserId = " "; break;
            case "access": token.AccessToken = ""; break;
            case "access-expired": token.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1); break;
            case "refresh": token.RefreshToken = ""; break;
            case "refresh-expired": token.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1); break;
            case "roles": token.Roles = null!; break;
        }
        pending.Complete(token);
        Assert.False((await login.WaitAsync(Limit)).IsSuccess);
        Assert.Equal("original", fixture.Auth.UserId);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal("original", fixture.Tokens.Snapshot?.UserId);
        Assert.Empty(fixture.Decorations.LoadedUsers);
    }

    [Fact]
    public async Task 사용자가로그인전송을취소하면_원래Token을보존한다()
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        var revision = fixture.Auth.SessionRevision;
        using var cancellation = new CancellationTokenSource();
        var login = fixture.Api.LoginAsync("next", "test", cancellation.Token);
        var pending = await fixture.Handler.NextAsync();
        cancellation.Cancel();
        pending.Complete(Token("next", "late"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login.WaitAsync(Limit));
        Assert.Equal("original", fixture.Auth.UserId);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Equal("original", fixture.Tokens.Snapshot?.UserId);
        Assert.Empty(fixture.Decorations.LoadedUsers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task commit전timeout은원세션을보존하고_commit뒤선택적timeout은인증성공을유지한다(bool afterCommit)
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        if (afterCommit)
            fixture.Handler.Entitlements = _ => Task.FromException<HttpResponseMessage>(new TaskCanceledException());
        var login = fixture.Api.LoginAsync("new", "test");
        var pending = await fixture.Handler.NextAsync();
        if (afterCommit) pending.Complete(Token("new", "new-token"));
        else pending.Response.SetException(new TaskCanceledException());
        Assert.Equal(afterCommit, (await login.WaitAsync(Limit)).IsSuccess);
        Assert.Equal(afterCommit ? "new" : "original", fixture.Auth.UserId);
        Assert.Equal(afterCommit ? "new" : "original", fixture.Tokens.Snapshot?.UserId);
        Assert.Equal(1, fixture.Handler.AuthCalls);
        Assert.Equal(afterCommit ? 1 : 0, fixture.Decorations.LoadedUsers.Count);
    }

    [Fact]
    public async Task commit뒤선택적동기화중_사용자취소는_내부timeout으로흡수하지않는다()
    {
        using var fixture = new Fixture();
        var entered = Signal();
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Entitlements = _ => { entered.SetResult(); return response.Task; };
        using var cancellation = new CancellationTokenSource();
        var login = fixture.Api.LoginAsync("new", "test", cancellation.Token);
        (await fixture.Handler.NextAsync()).Complete(Token("new", "new-token"));
        await entered.Task.WaitAsync(Limit);
        cancellation.Cancel();
        response.SetException(new TaskCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login.WaitAsync(Limit));
        Assert.Equal("new", fixture.Auth.UserId); // 이미 확정한 인증을 원래 계정으로 되돌리지 않습니다.
        Assert.Equal("new", fixture.Tokens.Snapshot?.UserId);
        Assert.Equal(1, fixture.Handler.AuthCalls);
    }

    [Fact]
    public async Task 주인증보안저장실패는_선택적동기화실패로숨기지않는다()
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        fixture.Tokens.Save = _ => throw new IOException("synthetic secure storage unavailable");
        var login = fixture.Api.LoginAsync("new", "test");
        (await fixture.Handler.NextAsync()).Complete(Token("new", "new-token"));
        await Assert.ThrowsAsync<IOException>(() => login.WaitAsync(Limit));
        Assert.Equal("new", fixture.Auth.UserId);
        Assert.Equal("original", fixture.Tokens.Snapshot?.UserId);
        Assert.Empty(fixture.Decorations.LoadedUsers);
        Assert.Equal(1, fixture.Handler.AuthCalls);
    }

    [Fact]
    public async Task 부가작업전_새의도가생기면_이전판정뒤의Apply도거부한다()
    {
        var store = new ShipperRecoveryTokenStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        var entered = Signal(); var release = Signal(); var staleAfter = 0;
        using var first = session.BeginAuthenticationChange();
        var applying = session.TryApplyAuthenticationAsync(Snapshot("old"), first,
            async _ => { entered.SetResult(); await release.Task; }, _ => { staleAfter++; return Task.CompletedTask; });
        await entered.Task.WaitAsync(Limit);
        using var next = session.BeginAuthenticationChange();
        var latest = session.TryApplyAuthenticationAsync(Snapshot("new"), next, _ => Task.CompletedTask, _ => Task.CompletedTask);
        Assert.False(latest.IsCompleted);
        release.SetResult();
        Assert.Equal(AuthSessionCommitResult.Superseded, await applying.WaitAsync(Limit));
        Assert.Equal(AuthSessionCommitResult.Applied, await latest.WaitAsync(Limit));
        Assert.Equal("new", session.UserId);
        Assert.Equal("new", store.Snapshot?.UserId);
        Assert.Equal(0, staleAfter);
    }

    [Fact]
    public async Task 취소를무시한꾸미기복원이끝나기전에는_새계정으로바뀌지않는다()
    {
        using var fixture = new Fixture();
        var loaded = Signal(); var release = Signal();
        var product = fixture.State.Products.First(item => !item.IsFree);
        fixture.Decorations.Load = async user =>
        {
            if (user != "old") return new() { 사용자UserId = user };
            loaded.SetResult();
            await release.Task; // 의도적으로 취소를 무시하는 기존 저장소/후속 작업
            return new() { 사용자UserId = user, 보유권목록 = [new() { 팩Key = product.PackKey }] };
        };
        var oldLogin = fixture.Api.LoginAsync("old", "test");
        (await fixture.Handler.NextAsync()).Complete(Token("old", "old-token"));
        await loaded.Task.WaitAsync(Limit);
        var newLogin = fixture.NewApi().LoginAsync("new", "test");
        (await fixture.Handler.NextAsync()).Complete(Token("new", "new-token"));
        Assert.Equal("old", fixture.Auth.UserId);
        Assert.False(newLogin.IsCompleted);
        release.SetResult();
        Assert.False((await oldLogin.WaitAsync(Limit)).IsSuccess);
        Assert.True((await newLogin.WaitAsync(Limit)).IsSuccess);
        Assert.Equal("new", fixture.Auth.UserId);
        Assert.False(fixture.State.IsProductOwned(product));
        Assert.Equal(new[] { "old", "new" }, fixture.Decorations.LoadedUsers);
    }

    [Fact]
    public async Task 늦은로그아웃의Push응답은_새로그인을지우거나_새계정의설치를해제하지않는다()
    {
        using var fixture = new Fixture(useOwnedPush: true);
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        fixture.PushStore.State = fixture.PushStore.State with { RegisteredOwnerId = "original" };
        var entered = Signal(); var release = Signal();
        fixture.Handler.Delete = async _ => { entered.SetResult(); await release.Task; return new(HttpStatusCode.NoContent); };
        var logout = fixture.Api.LogoutAsync();
        await entered.Task.WaitAsync(Limit);
        var login = fixture.NewApi().LoginAsync("new", "test");
        (await fixture.Handler.NextAsync()).Complete(Token("new", "new-token"));
        Assert.Equal("original", fixture.Auth.UserId);
        Assert.False(login.IsCompleted);
        release.SetResult();
        await logout.WaitAsync(Limit);
        Assert.True((await login.WaitAsync(Limit)).IsSuccess);
        Assert.Equal("new", fixture.Auth.UserId);
        Assert.Equal("new", fixture.Tokens.Snapshot?.UserId);
        Assert.All(fixture.Handler.DeleteOwners, token => Assert.Equal("token-original", token));
    }

    [Fact]
    public async Task Push내부Gate뒤에서대기중인로그아웃도_새계정을해제하지않는다()
    {
        using var fixture = new Fixture(useOwnedPush: true);
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        fixture.PushStore.State = fixture.PushStore.State with { RegisteredOwnerId = "original" };
        var entered = Signal(); var release = Signal();
        fixture.PushProvider.Get = async () => { entered.TrySetResult(); await release.Task; return null; };
        var occupying = fixture.Push.EnsureRegisteredAsync();
        await entered.Task.WaitAsync(Limit);
        var product = fixture.State.Products.First(item => !item.IsFree);
        fixture.State.SynchronizeServerOwnedPacks([product.PackKey]);
        var cleared = Signal(); fixture.State.Changed += () => cleared.TrySetResult();
        var logout = fixture.Api.LogoutAsync();
        await cleared.Task.WaitAsync(Limit);
        var login = fixture.NewApi().LoginAsync("new", "test");
        (await fixture.Handler.NextAsync()).Complete(Token("new", "new-token"));
        Assert.False(login.IsCompleted);
        release.SetResult();
        await occupying.WaitAsync(Limit);
        await logout.WaitAsync(Limit);
        Assert.True((await login.WaitAsync(Limit)).IsSuccess);
        Assert.Equal("new", fixture.Auth.UserId);
        Assert.NotEmpty(fixture.Handler.DeleteOwners);
        Assert.All(fixture.Handler.DeleteOwners, token => Assert.Equal("token-original", token));
    }

    [Fact]
    public async Task 미리취소된로그아웃도_현재로컬세션은종료한다()
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Api.LogoutAsync(cancellation.Token).WaitAsync(Limit));
        Assert.False(fixture.Auth.IsLoggedIn);
        Assert.Null(fixture.Tokens.Snapshot);
    }

    [Fact]
    public async Task 로그인보안저장이늦어도_로그아웃메모리는즉시종료되고_늦은저장을삭제한다()
    {
        using var fixture = new Fixture();
        var saving = Signal(); var release = Signal();
        fixture.Tokens.Save = _ => { saving.SetResult(); return release.Task; };
        var login = fixture.Api.LoginAsync("old", "test");
        (await fixture.Handler.NextAsync()).Complete(Token("old", "old-token"));
        await saving.Task.WaitAsync(Limit);
        var logout = fixture.NewApi().LogoutAsync();
        Assert.False(fixture.Auth.IsLoggedIn);
        release.SetResult();
        Assert.False((await login.WaitAsync(Limit)).IsSuccess);
        await logout.WaitAsync(Limit);
        Assert.Null(fixture.Tokens.Snapshot);
        Assert.Empty(fixture.Decorations.LoadedUsers);
    }

    [Fact]
    public async Task 로그아웃보안삭제가늦어도_새로그인메모리와마지막저장을지우지않는다()
    {
        var store = new DelayedClearStore();
        var session = new AuthSession(store, new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("original"));
        using var ending = session.BeginAuthenticationChange();
        var logout = session.TryEndAuthenticationAsync(ending, _ => Task.CompletedTask);
        await store.Started.Task.WaitAsync(Limit);
        Assert.False(session.IsLoggedIn);
        using var next = session.BeginAuthenticationChange();
        var login = session.TryApplyAuthenticationAsync(Snapshot("new"), next, _ => Task.CompletedTask, _ => Task.CompletedTask);
        Assert.Equal("new", session.UserId);
        Assert.False(login.IsCompleted);
        store.Release.SetResult();
        Assert.True(await logout.WaitAsync(Limit));
        Assert.Equal(AuthSessionCommitResult.Applied, await login.WaitAsync(Limit));
        Assert.Equal("new", session.UserId);
        Assert.Equal("new", store.Snapshot?.UserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 복원과최신로그인경합은_완료순서와무관하게_최신로그인을보존한다(bool loginFirst)
    {
        using var fixture = new Fixture();
        var load = new TaskCompletionSource<ClientAuthTokenSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Tokens.Load = () => load.Task;
        var restore = fixture.Auth.RestoreAsync();
        var login = fixture.Api.LoginAsync("new", "test");
        var pending = await fixture.Handler.NextAsync();
        if (loginFirst)
        {
            pending.Complete(Token("new", "new-token"));
            Assert.True((await login.WaitAsync(Limit)).IsSuccess);
            load.SetResult(Snapshot("old"));
        }
        else
        {
            load.SetResult(Snapshot("old"));
            await restore.WaitAsync(Limit);
            pending.Complete(Token("new", "new-token"));
            Assert.True((await login.WaitAsync(Limit)).IsSuccess);
        }
        await restore.WaitAsync(Limit);
        Assert.Equal("new", fixture.Auth.UserId);
        Assert.Equal("new", fixture.Tokens.Snapshot?.UserId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.OK)]
    public async Task 현재세션갱신완료는_이미시작한최신로그인의도를무효화하지않는다(HttpStatusCode status)
    {
        using var fixture = new Fixture();
        await fixture.Auth.ApplyAsync(Snapshot("original"));
        var refresh = fixture.Api.EnsureAccessTokenAsync(forceRefresh: true);
        var old = await fixture.Handler.NextAsync();
        var login = fixture.NewApi().LoginAsync("new", "test");
        var next = await fixture.Handler.NextAsync();
        if (status == HttpStatusCode.OK) old.Complete(Token("original", "refreshed"));
        else old.Response.SetResult(new(status));
        await refresh.WaitAsync(Limit);
        next.Complete(Token("new", "new-token"));
        Assert.True((await login.WaitAsync(Limit)).IsSuccess);
        Assert.Equal("new", fixture.Auth.UserId);
        Assert.Equal("new", fixture.Tokens.Snapshot?.UserId);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ClientAuthTokenSnapshot Snapshot(string user) => ShipperRecoveryFixture.Snapshot(user, $"token-{user}");
    private static 토큰응답 Token(string user, string token) => new()
    {
        UserId = user, UserName = user, AccessToken = token, RefreshToken = $"refresh-{token}",
        AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30), RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1), Roles = ["화주"]
    };

    private sealed class Fixture : IDisposable
    {
        public ShipperRecoveryTokenStore Tokens { get; } = new();
        public AuthSession Auth { get; }
        public RoutedHandler Handler { get; } = new();
        public HttpClient Client { get; }
        public DecorationStore Decorations { get; } = new();
        public PlatformCommunityDecorationStateService State { get; } = new();
        public PushStore PushStore { get; } = new();
        public PushProvider PushProvider { get; } = new();
        public SsalddelMobilePushInstallationClient Push { get; }
        public AuthApiService Api { get; }
        public Fixture(bool useOwnedPush = false)
        {
            Auth = new(Tokens, new ClientSessionGuard());
            Client = new(Handler) { BaseAddress = new Uri("https://test.invalid/") };
            Push = useOwnedPush
                ? new(Client, PushProvider, () => Auth.AccessToken, PushStore, () => Auth.UserId, () => Auth.SessionRevision)
                : new(Client, PushProvider, () => Auth.AccessToken);
            Api = NewApi();
        }
        public AuthApiService NewApi() => new(Client, Auth, new(Client, Auth, Decorations, State), Push);
        public void Dispose() => Client.Dispose();
    }

    private sealed class DecorationStore : I꾸미기보유권LocalStore
    {
        public List<string> LoadedUsers { get; } = [];
        public Func<string, Task<노드스티커보유권동기화Response?>>? Load { get; set; }
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken cancellationToken = default)
        {
            LoadedUsers.Add(userId);
            return Load?.Invoke(userId) ?? Task.FromResult<노드스티커보유권동기화Response?>(new() { 사용자UserId = userId });
        }
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PushProvider : ISsalddelMobilePushTokenProvider
    {
        public Func<Task<SsalddelMobilePushTokenSnapshot?>>? Get { get; set; }
        public Task<SsalddelMobilePushTokenSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Get?.Invoke() ?? Task.FromResult<SsalddelMobilePushTokenSnapshot?>(null);
    }
    private sealed class PushStore : IMemberDeliveryPushStateStore
    {
        public MemberDeliveryPushState State { get; set; } = new("synthetic-installation");
        public Task<MemberDeliveryPushState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);
        public Task UpdateAsync(Func<MemberDeliveryPushState, MemberDeliveryPushState> update, CancellationToken cancellationToken = default)
        { State = update(State); return Task.CompletedTask; }
    }

    private sealed class DelayedClearStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Snapshot { get; private set; }
        public TaskCompletionSource Started { get; } = Signal();
        public TaskCompletionSource Release { get; } = Signal();
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { Snapshot = snapshot; return Task.CompletedTask; }
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        { Started.SetResult(); await Release.Task; Snapshot = null; }
    }

    private sealed record Pending(CancellationToken Token, TaskCompletionSource<HttpResponseMessage> Response)
    {
        public void Complete(토큰응답 token) => Response.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(token) });
    }
    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Channel<Pending> _pending = Channel.CreateUnbounded<Pending>();
        public int AuthCalls { get; private set; }
        public List<string?> DeleteOwners { get; } = [];
        public Func<CancellationToken, Task<HttpResponseMessage>>? Delete { get; set; }
        public Func<CancellationToken, Task<HttpResponseMessage>>? Entitlements { get; set; }
        public Task<Pending> NextAsync() => _pending.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.StartsWith("/api/v1/auth/", StringComparison.Ordinal))
            {
                AuthCalls++;
                var pending = new Pending(cancellationToken, new(TaskCreationOptions.RunContinuationsAsynchronously));
                _pending.Writer.TryWrite(pending);
                return pending.Response.Task; // 취소 비협조 응답도 완료 뒤 적용하지 않아야 합니다.
            }
            if (request.Method == HttpMethod.Delete)
            {
                DeleteOwners.Add(request.Headers.Authorization?.Parameter);
                return Delete?.Invoke(cancellationToken) ?? Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/v1/community/node-sticker-store/entitlements/me", request.RequestUri?.AbsolutePath);
            return Entitlements?.Invoke(cancellationToken) ?? Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
