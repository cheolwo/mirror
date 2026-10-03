using System.Net;
using System.Net.Http.Json;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;

namespace Ssalddel.Tests.Clients;

public sealed class CargoDriverAuthenticationRecoveryTests
{
    [Fact]
    public async Task ExpiredRefreshToken_EndsSessionWithoutSendingRequest()
    {
        var store = new TokenStore();
        var session = await SessionAsync(store, expiredAccess: true, expiredRefresh: true);
        var handler = new TestHandler((_, _) => throw new InvalidOperationException("No request expected."));
        using var http = Client(handler);

        var error = await new AuthApiService(http, session).EnsureAccessTokenAsync();

        Assert.Contains("다시 로그인", error);
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.AccessToken);
        Assert.Equal(1, store.ClearCount);
        Assert.Empty(handler.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task RejectedRefresh_EndsCurrentSession(HttpStatusCode status)
    {
        var store = new TokenStore();
        var session = await SessionAsync(store, expiredAccess: true);
        using var http = Client(new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(status))));

        var error = await new AuthApiService(http, session).EnsureAccessTokenAsync();

        Assert.Contains("다시 로그인", error);
        Assert.False(session.IsAuthenticated);
        Assert.Equal(1, store.ClearCount);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("unavailable")]
    [InlineData("forbidden")]
    public async Task TransientRefreshOrPermissionFailure_PreservesSessionForRetry(string failure)
    {
        var store = new TokenStore();
        var session = await SessionAsync(store, expiredAccess: true);
        var revision = session.SessionRevision;
        var fail = true;
        var handler = new TestHandler((_, _) => !fail
            ? Task.FromResult(TokenResponse())
            : failure switch
            {
                "connection" => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")),
                "timeout" => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")),
                "forbidden" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            });
        using var http = Client(handler);
        var auth = new AuthApiService(http, session);

        Assert.NotNull(await auth.EnsureAccessTokenAsync());
        Assert.True(session.IsAuthenticated);
        Assert.Equal("refresh-old", session.RefreshToken);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal(0, store.ClearCount);

        fail = false;
        Assert.Null(await auth.EnsureAccessTokenAsync());
        Assert.Equal("access-refreshed", session.AccessToken);
        Assert.Equal(revision, session.SessionRevision);
    }

    [Fact]
    public async Task SuccessfulRefresh_ChangesTokenVersionAndKeepsSessionRevision()
    {
        var session = await SessionAsync(new TokenStore(), expiredAccess: true);
        var tokenVersion = session.Version;
        var revision = session.SessionRevision;
        using var http = Client(new TestHandler((_, _) => Task.FromResult(TokenResponse())));

        Assert.Null(await new AuthApiService(http, session).EnsureAccessTokenAsync());

        Assert.Equal(tokenVersion + 1, session.Version);
        Assert.Equal(revision, session.SessionRevision);
        Assert.Equal("driver-a", session.UserId);
    }

    [Fact]
    public async Task SameUserLogin_StartsNewSessionRevision()
    {
        var session = await SessionAsync(new TokenStore());
        var revision = session.SessionRevision;
        using var http = Client(new TestHandler((_, _) => Task.FromResult(TokenResponse())));

        var result = await new AuthApiService(http, session).LoginAsync("driver-a", "test-password");

        Assert.True(result.IsSuccess);
        Assert.Equal(revision + 1, session.SessionRevision);
    }

    [Fact]
    public async Task FinalUnauthorized_AfterRefresh_EndsSessionAndDoesNotRetryAgain()
    {
        var store = new TokenStore();
        var session = await SessionAsync(store);
        var handler = new TestHandler((request, _) => Task.FromResult(
            IsRefresh(request) ? TokenResponse() : new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = Client(handler);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Api(http, session).GetAsync<object>("api/test", "운송"));

        Assert.False(session.IsAuthenticated);
        Assert.Equal(1, store.ClearCount);
        Assert.Equal(new[] { "/api/test", "/api/v1/auth/refresh", "/api/test" }, handler.Paths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldUnauthorized_AfterNewLogin_DoesNotClearNewSession(bool finalResponse)
    {
        var store = new TokenStore();
        var session = await SessionAsync(store);
        var entered = Signal();
        var delayed = ResponseSignal();
        var transportCalls = 0;
        var handler = new TestHandler((request, _) =>
        {
            if (IsRefresh(request))
                return Task.FromResult(TokenResponse());
            transportCalls++;
            if (finalResponse && transportCalls == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            entered.TrySetResult();
            return delayed.Task;
        });
        using var http = Client(handler);
        var pending = Api(http, session).GetAsync<object>("api/test", "운송");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await session.ApplyAsync(Snapshot("driver-b", "access-new"));
        delayed.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("driver-b", session.UserId);
        Assert.Equal("access-new", session.AccessToken);
        Assert.Equal(0, store.ClearCount);
        Assert.Equal(finalResponse ? 2 : 1, transportCalls);
    }

    [Fact]
    public async Task OldSuccessfulResponse_AfterSameUserRelogin_IsNotReturned()
    {
        var session = await SessionAsync(new TokenStore());
        var entered = Signal();
        var delayed = ResponseSignal();
        using var http = Client(new TestHandler((_, _) => { entered.SetResult(); return delayed.Task; }));
        var pending = Api(http, session).GetAsync<object>("api/test", "운송");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await session.ApplyAsync(Snapshot("driver-a", "access-new"));
        delayed.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { id = 1 }) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("access-new", session.AccessToken);
    }

    [Fact]
    public async Task OldSuccessfulRefresh_AfterNewLogin_DoesNotOverwriteNewToken()
    {
        var store = new TokenStore();
        var session = await SessionAsync(store, expiredAccess: true);
        var entered = Signal();
        var delayed = ResponseSignal();
        using var http = Client(new TestHandler((_, _) => { entered.SetResult(); return delayed.Task; }));
        var pending = new AuthApiService(http, session).EnsureAccessTokenAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await session.ApplyAsync(Snapshot("driver-b", "access-new"));
        delayed.SetResult(TokenResponse());

        Assert.NotNull(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("driver-b", session.UserId);
        Assert.Equal("access-new", session.AccessToken);
        Assert.Equal(0, store.ClearCount);
    }

    [Fact]
    public async Task CancelledRefresh_DelayedSuccessfulResponse_DoesNotApplyToken()
    {
        var session = await SessionAsync(new TokenStore(), expiredAccess: true);
        var version = session.Version;
        var entered = Signal();
        var delayed = ResponseSignal();
        using var http = Client(new TestHandler((_, _) => { entered.SetResult(); return delayed.Task; }));
        using var cancellation = new CancellationTokenSource();
        var pending = new AuthApiService(http, session).EnsureAccessTokenAsync(cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        delayed.SetResult(TokenResponse());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(version, session.Version);
        Assert.Equal("access-old", session.AccessToken);
    }

    [Fact]
    public async Task SecureRemovalFailure_StillNotifiesSignedOutAndReportsAuthenticationFailure()
    {
        var store = new TokenStore { ClearFailure = new InvalidOperationException("device storage") };
        var session = await SessionAsync(store, expiredAccess: true);
        var notifications = 0;
        session.Changed += () => notifications++;
        using var http = Client(new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        var error = await new AuthApiService(http, session).EnsureAccessTokenAsync();

        Assert.Contains("다시 로그인", error);
        Assert.False(session.IsAuthenticated);
        Assert.Empty(session.Roles);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task FailedSecureLoad_CanBeRestoredAgain()
    {
        var store = new TokenStore { Snapshot = Snapshot(), LoadFailure = new InvalidOperationException("device storage") };
        var session = new AuthSession(store, new ClientSessionGuard());

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RestoreAsync());
        Assert.False(session.IsAuthenticated);
        store.LoadFailure = null;
        await session.RestoreAsync();

        Assert.True(session.IsAuthenticated);
        Assert.Equal("driver-a", session.UserId);
    }

    [Fact]
    public async Task ApiUnavailable_DoesNotClearOrForceRefreshValidSession()
    {
        var store = new TokenStore();
        var session = await SessionAsync(store);
        var handler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var http = Client(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Api(http, session).GetAsync<object>("api/test", "운송"));

        Assert.True(session.IsAuthenticated);
        Assert.Equal(0, store.ClearCount);
        Assert.Equal(new[] { "/api/test" }, handler.Paths);
    }

    private static async Task<AuthSession> SessionAsync(TokenStore store, bool expiredAccess = false, bool expiredRefresh = false)
    {
        var session = new AuthSession(store, new ClientSessionGuard());
        await session.ApplyAsync(Snapshot() with
        {
            AccessTokenExpiresAtUtc = expiredAccess ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddHours(1),
            RefreshTokenExpiresAtUtc = expiredRefresh ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddDays(1)
        });
        return session;
    }

    private static ClientAuthTokenSnapshot Snapshot(string user = "driver-a", string access = "access-old")
        => new(access, DateTime.UtcNow.AddHours(1), "refresh-old", DateTime.UtcNow.AddDays(1), user, user, ["Driver"]);

    private static HttpResponseMessage TokenResponse()
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new 토큰응답
            {
                AccessToken = "access-refreshed", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                RefreshToken = "refresh-new", RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                UserId = "driver-a", UserName = "driver-a", Roles = ["Driver"]
            })
        };

    private static bool IsRefresh(HttpRequestMessage request)
        => request.RequestUri!.AbsolutePath == "/api/v1/auth/refresh";

    private static HttpClient Client(HttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("https://cargo.example.test/") };

    private static DriverApiClient Api(HttpClient http, AuthSession session)
        => new(http, session, new AuthApiService(http, session), new DriverOperatingProfileService());

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource<HttpResponseMessage> ResponseSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return send(request, cancellationToken);
        }
    }

    private sealed class TokenStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Snapshot { get; set; }
        public Exception? LoadFailure { get; set; }
        public Exception? ClearFailure { get; set; }
        public int ClearCount { get; private set; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => LoadFailure is null ? Task.FromResult(Snapshot) : Task.FromException<ClientAuthTokenSnapshot?>(LoadFailure);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCount++;
            if (ClearFailure is not null)
                return Task.FromException(ClearFailure);
            Snapshot = null;
            return Task.CompletedTask;
        }
    }
}
