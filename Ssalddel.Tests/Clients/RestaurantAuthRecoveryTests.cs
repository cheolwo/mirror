using System.Net;
using System.Net.Http.Json;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantAuthRecoveryTests
{
    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task RefreshServerFailurePreservesSessionForRetry(int status)
    {
        var fixture = new SessionFixture();
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        using var http = CreateHttp(handler);
        var auth = new RestaurantAuthService(http, fixture.Session);

        var result = await auth.EnsureAccessTokenAsync();

        Assert.False(result.IsSuccess);
        Assert.False(result.RequiresLogin);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Equal("stored-refresh", fixture.Session.RefreshToken);
        Assert.Equal(ClientAuthSessionRestoreState.RefreshRequired, await fixture.Session.RestoreAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshTransportFailurePreservesSessionForRetry(bool timeout)
    {
        var fixture = new SessionFixture();
        using var handler = new RecordingHandler((_, _) => timeout
            ? Task.FromException<HttpResponseMessage>(new TaskCanceledException("synthetic timeout"))
            : Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic connection failure")));
        using var http = CreateHttp(handler);
        var auth = new RestaurantAuthService(http, fixture.Session);

        var result = await auth.EnsureAccessTokenAsync();

        Assert.False(result.IsSuccess);
        Assert.False(result.RequiresLogin);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Equal("stored-refresh", fixture.Session.RefreshToken);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"accessToken\":\"synthetic-incomplete\"}")]
    public async Task UnreadableRefreshResponseDoesNotEraseSavedCredentials(string content)
    {
        var fixture = new SessionFixture();
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        }));
        using var http = CreateHttp(handler);

        var result = await new RestaurantAuthService(http, fixture.Session).EnsureAccessTokenAsync();

        Assert.False(result.IsSuccess);
        Assert.False(result.RequiresLogin);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Equal("stored-refresh", fixture.Session.RefreshToken);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    public async Task DefinitiveRefreshRejectionRequiresLoginAndClearsSession(int status)
    {
        var fixture = new SessionFixture();
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        using var http = CreateHttp(handler);

        var result = await new RestaurantAuthService(http, fixture.Session).EnsureAccessTokenAsync();

        Assert.False(result.IsSuccess);
        Assert.True(result.RequiresLogin);
        Assert.Equal(1, fixture.Store.ClearCount);
        Assert.Null(fixture.Session.RefreshToken);
        Assert.False(fixture.Session.IsAuthenticated);
    }

    [Fact]
    public async Task CancellationDoesNotBecomeLoginFailureOrClearSession()
    {
        var fixture = new SessionFixture();
        using var cancellation = new CancellationTokenSource();
        using var handler = new RecordingHandler((_, token) =>
        {
            cancellation.Cancel();
            return Task.FromException<HttpResponseMessage>(new OperationCanceledException(token));
        });
        using var http = CreateHttp(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RestaurantAuthService(http, fixture.Session).EnsureAccessTokenAsync(cancellationToken: cancellation.Token));

        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Equal("stored-refresh", fixture.Session.RefreshToken);
    }

    [Theory]
    [InlineData("missing-refresh", false)]
    [InlineData("expired-refresh", false)]
    [InlineData("missing-refresh", true)]
    [InlineData("expired-refresh", true)]
    [InlineData("missing-user", false)]
    public async Task ForcedRefreshWithUnusableCachedSessionRequiresLoginWithoutRedirectLoop(string kind, bool clearFails)
    {
        var fixture = new SessionFixture(expired: false);
        await fixture.Session.ApplyAsync(new ClientAuthTokenSnapshot("cached-access", DateTime.UtcNow.AddHours(1),
            kind == "missing-refresh" ? "" : "cached-refresh",
            DateTime.UtcNow.AddDays(kind == "expired-refresh" ? -1 : 1),
            kind == "missing-user" ? "" : "synthetic-restaurant-id", "synthetic-restaurant", ["음식점"]));
        Assert.Equal(kind == "missing-user" ? ClientAuthSessionRestoreState.Anonymous
            : ClientAuthSessionRestoreState.Authenticated, await fixture.Session.RestoreAsync());
        Assert.Equal(kind != "missing-user", fixture.Session.IsAuthenticated);
        fixture.Store.ClearFails = clearFails;
        using var handler = new RecordingHandler((_, _) => throw new InvalidOperationException("No HTTP request is expected."));
        using var http = CreateHttp(handler);
        var auth = new RestaurantAuthService(http, fixture.Session);

        var result = await auth.EnsureAccessTokenAsync(forceRefresh: true);

        Assert.False(result.IsSuccess);
        Assert.True(result.RequiresLogin);
        Assert.False(fixture.Session.IsAuthenticated);
        Assert.Null(fixture.Session.AccessToken);
        Assert.Equal(1, fixture.Store.ClearCount);
        Assert.True((await auth.EnsureAccessTokenAsync()).RequiresLogin);
        Assert.Empty(handler.Paths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedRejectionCompletesCleanupWhenPageCancelsItsLifetime(bool clearFails)
    {
        var fixture = new SessionFixture();
        fixture.Store.ClearFails = clearFails;
        using var cancellation = new CancellationTokenSource();
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = CreateHttp(handler);
        var auth = new RestaurantAuthService(http, fixture.Session);
        auth.SessionEnding += _ => cancellation.Cancel();

        var result = await auth.EnsureAccessTokenAsync(cancellationToken: cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(result.RequiresLogin);
        Assert.False(fixture.Session.IsAuthenticated);
        Assert.Null(fixture.Session.AccessToken);
        Assert.Equal(1, fixture.Store.ClearCount);
    }

    [Fact]
    public async Task ConfirmedRejectionDoesNotClearNewOwnerAppliedBySessionEndListener()
    {
        var fixture = new SessionFixture();
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = CreateHttp(handler);
        var auth = new RestaurantAuthService(http, fixture.Session);
        auth.SessionEnding += _ => fixture.Session.ApplyAsync(new ClientAuthTokenSnapshot(
            "new-owner-access", DateTime.UtcNow.AddHours(1), "new-owner-refresh", DateTime.UtcNow.AddDays(1),
            "owner-b", "owner-b", ["음식점"])).GetAwaiter().GetResult();

        var result = await auth.EnsureAccessTokenAsync();

        Assert.True(result.RequiresLogin);
        Assert.True(fixture.Session.IsAuthenticated);
        Assert.Equal("owner-b", fixture.Session.UserId);
        Assert.Equal("new-owner-access", fixture.Session.AccessToken);
        Assert.Equal(0, fixture.Store.ClearCount);
    }

    [Fact]
    public async Task DefinitiveRefreshRejectionRemainsLoginFailureWhenSecureStoreClearFails()
    {
        var fixture = new SessionFixture();
        fixture.Store.ClearFails = true;
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = CreateHttp(handler);

        var result = await new RestaurantAuthService(http, fixture.Session).EnsureAccessTokenAsync();

        Assert.True(result.RequiresLogin);
        Assert.False(fixture.Session.IsAuthenticated);
        Assert.Null(fixture.Session.AccessToken);
        Assert.Equal(1, fixture.Store.ClearCount);
    }

    [Fact]
    public async Task InboxAfterTransientRefreshFailureRetriesWithSavedSession()
    {
        var fixture = new SessionFixture();
        var refreshCount = 0;
        using var handler = new RecordingHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/auth/refresh", StringComparison.Ordinal)
                ? ++refreshCount == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : TokenResponse()
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new 음식점주문수신함응답 { Page = 1, PageSize = 100 })
                }));
        using var http = CreateHttp(handler);
        var client = new Ssalddel음식주문Client(http, new RestaurantAuthService(http, fixture.Session), fixture.Session);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.주문목록조회Async(new 음식점주문수신함조회요청()));
        Assert.Null(error.StatusCode);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Single(handler.Paths);

        var inbox = await client.주문목록조회Async(new 음식점주문수신함조회요청());

        Assert.NotNull(inbox);
        Assert.Equal(2, refreshCount);
        Assert.Equal(3, handler.Paths.Count);
        Assert.True(fixture.Session.IsAuthenticated);
        Assert.Equal("renewed-access", fixture.Session.AccessToken);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Equal(1, fixture.Store.SaveCount);
    }

    [Fact]
    public async Task ForbiddenInboxRetainsItsRealApiExceptionStatusForUiLock()
    {
        var fixture = new SessionFixture(expired: false);
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new { detail = "synthetic permission denied" })
        }));
        using var http = CreateHttp(handler);
        var client = new Ssalddel음식주문Client(http, new RestaurantAuthService(http, fixture.Session), fixture.Session);

        var error = await Assert.ThrowsAsync<SsalddelApiException>(() =>
            client.주문목록조회Async(new 음식점주문수신함조회요청()));

        Assert.Equal(403, error.StatusCode);
        Assert.Single(handler.Paths);
        Assert.Equal(0, fixture.Store.ClearCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedInboxAfterSuccessfulRefreshBlocksLoginLoopEvenIfSecureStoreClearFails(bool clearFails)
    {
        var fixture = new SessionFixture(expired: false);
        fixture.Store.ClearFails = clearFails;
        using var handler = new RecordingHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/auth/refresh", StringComparison.Ordinal)
                ? TokenResponse()
                : new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = JsonContent.Create(new { detail = "synthetic access denied" })
                }));
        using var http = CreateHttp(handler);
        var auth = new RestaurantAuthService(http, fixture.Session);
        var client = new Ssalddel음식주문Client(http, auth, fixture.Session);

        var error = await Assert.ThrowsAsync<SsalddelApiException>(() =>
            client.주문목록조회Async(new 음식점주문수신함조회요청()));

        Assert.Equal(401, error.StatusCode);
        Assert.Equal(3, handler.Paths.Count);
        Assert.Equal(1, fixture.Store.SaveCount);
        Assert.Equal(1, fixture.Store.ClearCount);
        Assert.False(fixture.Session.IsAuthenticated);
        Assert.Null(fixture.Session.AccessToken);
        Assert.Null(fixture.Session.RefreshToken);

        var nextAuthentication = await auth.EnsureAccessTokenAsync();
        Assert.False(nextAuthentication.IsSuccess);
        Assert.True(nextAuthentication.RequiresLogin);
        Assert.Equal(3, handler.Paths.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledLateUnauthorizedResponseCannotInvalidateSharedSession(bool duringRetry)
    {
        var fixture = new SessionFixture(expired: false);
        using var cancellation = new CancellationTokenSource();
        var inboxCalls = 0;
        using var handler = new RecordingHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/refresh", StringComparison.Ordinal))
                return Task.FromResult(TokenResponse());
            inboxCalls++;
            if (!duringRetry || inboxCalls == 2) cancellation.Cancel();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { detail = "synthetic late unauthorized" })
            });
        });
        using var http = CreateHttp(handler);
        var client = new Ssalddel음식주문Client(http, new RestaurantAuthService(http, fixture.Session), fixture.Session);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.주문목록조회Async(new 음식점주문수신함조회요청(), cancellation.Token));

        Assert.Equal(duringRetry ? 2 : 1, inboxCalls);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.True(fixture.Session.IsAuthenticated);
        Assert.NotNull(fixture.Session.RefreshToken);
    }

    [Fact]
    public async Task RefreshToWrongRoleRequiresLoginInsteadOfTreatingItAsOffline()
    {
        var fixture = new SessionFixture();
        using var handler = new RecordingHandler((_, _) => Task.FromResult(TokenResponse(["주문자"])));
        using var http = CreateHttp(handler);

        var result = await new RestaurantAuthService(http, fixture.Session).EnsureAccessTokenAsync();

        Assert.True(result.RequiresLogin);
        Assert.Equal(1, fixture.Store.ClearCount);
        Assert.Equal(0, fixture.Store.SaveCount);
    }

    private static HttpClient CreateHttp(HttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("https://synthetic.invalid/") };

    private static HttpResponseMessage TokenResponse(string[]? roles = null)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new 토큰응답
            {
                AccessToken = "renewed-access",
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                RefreshToken = "renewed-refresh",
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                UserId = "synthetic-restaurant-id",
                UserName = "synthetic-restaurant",
                Roles = roles ?? ["음식점"]
            })
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return send(request, cancellationToken);
        }
    }

    private sealed class SessionFixture
    {
        public RecordingStore Store { get; }
        public ClientAuthSession Session { get; }
        public SessionFixture(bool expired = true)
        {
            Store = new RecordingStore(new ClientAuthTokenSnapshot("stored-access",
                DateTime.UtcNow.AddMinutes(expired ? -10 : 60), "stored-refresh", DateTime.UtcNow.AddDays(1),
                "synthetic-restaurant-id", "synthetic-restaurant", ["음식점"]));
            Session = new ClientAuthSession(Store, new ClientSessionGuard());
        }
    }

    private sealed class RecordingStore(ClientAuthTokenSnapshot snapshot) : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? _snapshot = snapshot;
        public int ClearCount { get; private set; }
        public int SaveCount { get; private set; }
        public bool ClearFails { get; set; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default)
        {
            _snapshot = value;
            SaveCount++;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCount++;
            if (ClearFails) return Task.FromException(new IOException("synthetic secure store failure"));
            _snapshot = null;
            return Task.CompletedTask;
        }
    }
}
