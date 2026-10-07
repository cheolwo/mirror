using System.Net;
using System.Net.Http.Json;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantAuthActorRecoveryTests
{
    [Theory]
    [InlineData(200, "owner-b")]
    [InlineData(401, "owner-b")]
    [InlineData(403, "owner-b")]
    [InlineData(500, "owner-b")]
    [InlineData(200, "owner-a")]
    [InlineData(401, "owner-a")]
    public async Task LateRefreshCannotOverwriteOrClearAnewLogin(int status, string nextOwner)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/refresh")) { started.SetResult(); return delayed.Task; }
            return Task.FromResult(Token(nextOwner, "new-login"));
        })) { BaseAddress = new("http://controlled.invalid/") };
        var store = new MemoryStore();
        var session = new ClientAuthSession(store, new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("owner-a", "old"));
        var auth = new RestaurantAuthService(http, session);
        var sessionEnds = 0;
        auth.SessionEnding += _ => sessionEnds++;

        var refreshing = auth.EnsureAccessTokenAsync(forceRefresh: true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await auth.LoginAsync(nextOwner, "synthetic-password")).IsSuccess);
        delayed.SetResult(status == 200 ? Token("owner-a", "late-refresh") : new((HttpStatusCode)status));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refreshing);
        Assert.Equal(nextOwner, session.UserId);
        Assert.Equal("new-login", session.AccessToken);
        Assert.Equal("new-login", store.Value!.AccessToken);
        Assert.Equal(0, sessionEnds);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(401)]
    public async Task LateApiResponseCannotReadOldOwnersDataOrInvalidateNewOwner(int status)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCalls = 0;
        string? requestToken = null;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Task.FromResult(Token("owner-b", "new-login"));
            if (request.RequestUri.AbsolutePath.EndsWith("/refresh")) { refreshCalls++; return Task.FromResult(Token("owner-b", "unexpected")); }
            requestToken = request.Headers.Authorization?.Parameter;
            started.SetResult();
            return delayed.Task;
        })) { BaseAddress = new("http://controlled.invalid/") };
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("owner-a", "old"));
        var auth = new RestaurantAuthService(http, session);
        var client = new Ssalddel음식주문Client(http, auth, session);

        var reading = client.목록Async();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await auth.LoginAsync("owner-b", "synthetic-password");
        delayed.SetResult(status == 200 ? MenuResponse() : new(HttpStatusCode.Unauthorized));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);

        Assert.Equal("old", requestToken);
        Assert.Equal(0, refreshCalls);
        Assert.Equal("owner-b", session.UserId);
        Assert.Equal("new-login", session.AccessToken);
    }

    [Fact]
    public async Task FirstUnauthorizedForcesSameActorsRefreshAndRetriesSameMenuPayloadOnce()
    {
        var paths = new List<string>();
        var sentTokens = new List<string?>();
        var sentMenus = new List<음식점메뉴등록요청>();
        using var http = new HttpClient(new Handler(async (request, cancellationToken) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
                return Token("owner-a", "refreshed");
            Assert.Equal(HttpMethod.Post, request.Method);
            sentTokens.Add(request.Headers.Authorization?.Parameter);
            sentMenus.Add((await request.Content!.ReadFromJsonAsync<음식점메뉴등록요청>(cancellationToken))!);
            return sentMenus.Count == 1
                ? new(HttpStatusCode.Unauthorized)
                : new(HttpStatusCode.OK) { Content = JsonContent.Create(new 음식점메뉴관리응답 { Id = 41, 메뉴명 = "계정 경계 시험 메뉴" }) };
        })) { BaseAddress = new("http://controlled.invalid/") };
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        // The access token is still locally valid. Only the actual first 401 should force refresh.
        await session.ApplyAsync(Snapshot("owner-a", "old"));
        var client = new Ssalddel음식주문Client(http, new RestaurantAuthService(http, session), session);
        var menu = new 음식점메뉴등록요청 { 클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "계정 경계 시험 메뉴", 판매가 = 7500m };

        Assert.Equal(41, (await client.등록Async(menu)).Id);
        Assert.Equal(new[] { "/api/v1/restaurant/menus", "/api/v1/auth/refresh", "/api/v1/restaurant/menus" }, paths);
        Assert.Equal(new[] { "old", "refreshed" }, sentTokens);
        Assert.Equal(2, sentMenus.Count);
        Assert.All(sentMenus, sent =>
        {
            Assert.Equal(menu.클라이언트요청Id, sent.클라이언트요청Id);
            Assert.Equal(menu.메뉴명, sent.메뉴명);
            Assert.Equal(menu.판매가, sent.판매가);
        });
        Assert.Equal("owner-a", session.UserId);
        Assert.Equal("refreshed", session.AccessToken);
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public async Task SameActorTokenRefreshedByAnotherRequestIsReusedWithoutSecondRefresh()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCalls = 0;
        var sentTokens = new List<string?>();
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/refresh"))
            { refreshCalls++; return Task.FromResult(Token("owner-a", "refreshed")); }
            sentTokens.Add(request.Headers.Authorization?.Parameter);
            if (sentTokens.Count == 1) { started.SetResult(); return delayed.Task; }
            return Task.FromResult(MenuResponse());
        })) { BaseAddress = new("http://controlled.invalid/") };
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("owner-a", "old"));
        var auth = new RestaurantAuthService(http, session);
        var client = new Ssalddel음식주문Client(http, auth, session);
        var reading = client.목록Async();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await auth.EnsureAccessTokenAsync(forceRefresh: true);
        delayed.SetResult(new(HttpStatusCode.Unauthorized));

        Assert.Empty(await reading);
        Assert.Equal(new[] { "old", "refreshed" }, sentTokens);
        Assert.Equal(1, refreshCalls);
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public async Task InitialSecureStoreRestoreCannotFinishOverAnewLogin()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<ClientAuthTokenSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new MemoryStore { Load = () => { started.SetResult(); return delayed.Task; } };
        var session = new ClientAuthSession(store, new ClientSessionGuard());
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Token("owner-b", "new-login"))))
        { BaseAddress = new("http://controlled.invalid/") };
        var auth = new RestaurantAuthService(http, session);
        var restoring = auth.EnsureAccessTokenAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var login = auth.LoginAsync("owner-b", "synthetic-password");
        Assert.False(login.IsCompleted);
        delayed.SetResult(Snapshot("owner-a", "old-store"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => restoring);
        Assert.True((await login).IsSuccess);
        Assert.Equal("owner-b", session.UserId);
        Assert.Equal("new-login", store.Value!.AccessToken);
    }

    [Fact]
    public async Task RefreshResponseWithWrongOwnerDoesNotReplaceOriginalSession()
    {
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("owner-a", "old"));
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Token("owner-b", "wrong-owner"))))
        { BaseAddress = new("http://controlled.invalid/") };
        var result = await new RestaurantAuthService(http, session).EnsureAccessTokenAsync(forceRefresh: true);
        Assert.False(result.IsSuccess);
        Assert.False(result.RequiresLogin);
        Assert.Equal("owner-a", session.UserId);
        Assert.Equal("old", session.AccessToken);
    }

    [Fact]
    public async Task RequestQueuedBehindRefreshCannotAcquireNewOwnersCredential()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var menuCalls = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Task.FromResult(Token("owner-b", "new-login"));
            if (request.RequestUri.AbsolutePath.EndsWith("/refresh")) { started.SetResult(); return delayed.Task; }
            menuCalls++;
            return Task.FromResult(MenuResponse());
        })) { BaseAddress = new("http://controlled.invalid/") };
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        await session.ApplyAsync(Snapshot("owner-a", "old"));
        var auth = new RestaurantAuthService(http, session);
        var client = new Ssalddel음식주문Client(http, auth, session);
        var refreshing = auth.EnsureAccessTokenAsync(forceRefresh: true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var reading = client.목록Async();
        await auth.LoginAsync("owner-b", "synthetic-password");
        delayed.SetResult(Token("owner-a", "late-refresh"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refreshing);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        Assert.Equal(0, menuCalls);
        Assert.Equal("owner-b", session.UserId);
    }

    private static ClientAuthTokenSnapshot Snapshot(string owner, string token)
        => new(token, DateTime.UtcNow.AddHours(1), token + "-refresh", DateTime.UtcNow.AddDays(1), owner, "시험 가게", ["음식점"]);
    private static HttpResponseMessage Token(string owner, string token)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(new 토큰응답 { AccessToken = token,
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1), RefreshToken = token + "-refresh",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1), UserId = owner, UserName = "시험 가게", Roles = ["음식점"] }) };
    private static HttpResponseMessage MenuResponse()
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<음식점메뉴관리응답>()) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
    private sealed class MemoryStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Value { get; private set; }
        public Func<Task<ClientAuthTokenSnapshot?>>? Load { get; set; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Load?.Invoke() ?? Task.FromResult(Value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { Value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { Value = null; return Task.CompletedTask; }
    }
}
