using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 기사 인증·현재 운송 Client에 합성 HTTP/토큰 저장소만 연결합니다. 서버·운행·기기 검증은 아닙니다.</summary>
public sealed class CargoDriverAuthenticationPreparationTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private const string Owner = "driver-a";
    private const string RefreshPath = "/api/v1/auth/refresh";
    private const string CurrentPath = "/api/v1/driver/transports/current";

    [Theory]
    [InlineData("driver-b")]
    [InlineData(Owner)]
    public async Task QueuedPublicEnsureCannotAdoptNewLogin(string nextOwner)
    {
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal(RefreshPath, call.Uri.AbsolutePath);
            return delayed.WaitAsync(ct);
        });
        var initialRevision = fixture.Session.SessionRevision;
        var active = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task<string?> queued;
        var next = Snapshot(nextOwner, "new-login");
        long nextRevision;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            queued = fixture.Auth.EnsureAccessTokenAsync();
            Assert.False(queued.IsCompleted);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
            nextRevision = fixture.Session.SessionRevision;
            Assert.NotEqual(initialRevision, nextRevision);
        }
        finally { delayed.Complete(TokenResponse(Snapshot(Owner, "obsolete-a-refresh"))); }
        await AssertSupersededEnsureAsync(active);
        await AssertSupersededEnsureAsync(queued);
        await AssertSnapshotAsync(fixture, next);
        Assert.Equal(nextRevision, fixture.Session.SessionRevision);
        Assert.Equal(Owner, RefreshBody(Assert.Single(fixture.Handler.Calls)).UserId);
        Assert.Equal(0, fixture.Store.ClearCount);
    }

    [Fact]
    public async Task QueuedEnsureCannotRefreshAndClearExpiredReplacementLogin()
    {
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal(RefreshPath, call.Uri.AbsolutePath);
            var body = RefreshBody(call);
            if (body.UserId == Owner) return delayed.WaitAsync(ct);
            Assert.Equal("driver-b", body.UserId);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        var active = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task<string?> queued;
        var next = Snapshot("driver-b", "new-expired-login")
            with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        long nextRevision;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            queued = fixture.Auth.EnsureAccessTokenAsync();
            Assert.False(queued.IsCompleted);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
            nextRevision = fixture.Session.SessionRevision;
        }
        finally { delayed.Complete(TokenResponse(Snapshot(Owner, "obsolete-a-refresh"))); }
        await AssertSupersededEnsureAsync(active);
        await AssertSupersededEnsureAsync(queued);
        await AssertSnapshotAsync(fixture, next);
        Assert.Equal(nextRevision, fixture.Session.SessionRevision);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Equal(Owner, RefreshBody(Assert.Single(fixture.Handler.Calls)).UserId);
    }

    [Fact]
    public async Task CurrentTransportWaitingForAuthenticationCannotSendWithReplacementBearer()
    {
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            if (call.Uri.AbsolutePath == RefreshPath) return delayed.WaitAsync(ct);
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal(CurrentPath, call.Uri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var active = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task current;
        var next = Snapshot("driver-b", "new-login");
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            current = fixture.Transport.현재조회Async();
            Assert.False(current.IsCompleted);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(TokenResponse(Snapshot(Owner, "obsolete-a-refresh"))); }
        await AssertSupersededEnsureAsync(active);
        Exception? stopped = null;
        try { await current.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (InvalidOperationException ex) { stopped = ex; }
        await AssertSnapshotAsync(fixture, next);
        Assert.NotNull(stopped);
        Assert.Equal(RefreshPath, Assert.Single(fixture.Handler.Calls).Uri.AbsolutePath);
        Assert.DoesNotContain(fixture.Handler.Calls, call => call.Uri.AbsolutePath == CurrentPath);
    }

    [Fact]
    public async Task SameSessionRefreshAdvancesTokenVersionAndResumesCurrentTransportWithFreshToken()
    {
        using var delayed = new DelayedResponse();
        var refreshed = Snapshot(Owner, "refreshed-a");
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            if (call.Uri.AbsolutePath == RefreshPath) return delayed.WaitAsync(ct);
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal(CurrentPath, call.Uri.AbsolutePath);
            Assert.True(string.Equals(refreshed.AccessToken, call.Bearer, StringComparison.Ordinal), "같은 세션의 최신 토큰으로 조회해야 합니다.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var originalVersion = fixture.Session.Version;
        var originalRevision = fixture.Session.SessionRevision;
        var active = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task current;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            current = fixture.Transport.현재조회Async();
            Assert.False(current.IsCompleted);
        }
        finally { delayed.Complete(TokenResponse(refreshed)); }
        Assert.Null(await active.WaitAsync(Limit));
        await current.WaitAsync(Limit);
        await AssertSnapshotAsync(fixture, refreshed);
        Assert.Equal(originalVersion + 1, fixture.Session.Version);
        Assert.Equal(originalRevision, fixture.Session.SessionRevision);
        Assert.Equal(2, fixture.Handler.Calls.Count);
        Assert.Single(fixture.Handler.Calls, call => call.Uri.AbsolutePath == CurrentPath);
    }

    [Fact]
    public async Task ColdStoredSessionRestoresBeforeFirstCurrentTransportRequest()
    {
        var saved = Snapshot();
        using var fixture = await Fixture.CreateAsync((call, _) =>
        {
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal(CurrentPath, call.Uri.AbsolutePath);
            Assert.True(string.Equals(saved.AccessToken, call.Bearer, StringComparison.Ordinal), "저장소에서 복원한 기사의 토큰을 사용해야 합니다.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }, cold: true, initial: saved);
        Assert.Null(fixture.Session.UserId);
        Assert.Null(await fixture.Transport.현재조회Async().WaitAsync(Limit));
        await AssertSnapshotAsync(fixture, saved);
        Assert.True(fixture.Session.IsAuthenticated);
        Assert.Single(fixture.Handler.Calls);
    }

    [Fact]
    public async Task CancellingColdLoadForOneEnsureAllowsAnotherCallerToLoadNormally()
    {
        var saved = Snapshot();
        using var fixture = await Fixture.CreateAsync((_, _) => throw new InvalidOperationException("정상 저장 세션에는 HTTP가 필요하지 않습니다."), cold: true, initial: saved);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLoad = new TaskCompletionSource<ClientAuthTokenSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loads = 0;
        fixture.Store.Load = async ct =>
        {
            if (Interlocked.Increment(ref loads) != 1) return saved;
            started.TrySetResult();
            return await firstLoad.Task.WaitAsync(Limit, ct);
        };
        var first = fixture.Auth.EnsureAccessTokenAsync(cancellationToken: cancellation.Token);
        Task<string?> second;
        try
        {
            await started.Task.WaitAsync(Limit);
            second = fixture.Auth.EnsureAccessTokenAsync();
            Assert.False(second.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Limit));
            Assert.Null(await second.WaitAsync(Limit));
        }
        finally { firstLoad.TrySetResult(saved); }
        Assert.Equal(2, Volatile.Read(ref loads));
        fixture.Store.Load = null;
        await AssertSnapshotAsync(fixture, saved);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Empty(fixture.Handler.Calls);
    }

    private static async Task AssertSupersededEnsureAsync(Task<string?> pending)
    {
        var cancelled = false;
        string? error = null;
        try { error = await pending.WaitAsync(Limit); }
        catch (OperationCanceledException) { cancelled = true; }
        Assert.True(cancelled || !string.IsNullOrWhiteSpace(error), "이전 세션의 인증 준비는 새 로그인에서 성공하면 안 됩니다.");
    }

    private static async Task AssertSnapshotAsync(Fixture fixture, ClientAuthTokenSnapshot expected)
    {
        AssertMemory(fixture.Session, expected);
        Assert.NotNull(fixture.Store.Snapshot);
        Assert.True(string.Equals(expected.AccessToken, fixture.Store.Snapshot.AccessToken, StringComparison.Ordinal), "저장된 access token이 보존돼야 합니다.");
        Assert.True(string.Equals(expected.RefreshToken, fixture.Store.Snapshot.RefreshToken, StringComparison.Ordinal), "저장된 refresh token이 보존돼야 합니다.");
        Assert.Equal(expected.UserId, fixture.Store.Snapshot.UserId);
        var restored = new AuthSession(fixture.Store, new ClientSessionGuard());
        await restored.RestoreAsync().WaitAsync(Limit);
        AssertMemory(restored, expected);
    }

    private static void AssertMemory(IAuthSession actual, ClientAuthTokenSnapshot expected)
    {
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.True(string.Equals(expected.AccessToken, actual.AccessToken, StringComparison.Ordinal), "현재 access token이 보존돼야 합니다.");
        Assert.True(string.Equals(expected.RefreshToken, actual.RefreshToken, StringComparison.Ordinal), "현재 refresh token이 보존돼야 합니다.");
        Assert.Equal(expected.AccessTokenExpiresAtUtc, actual.AccessTokenExpiresAtUtc);
        Assert.Equal(expected.RefreshTokenExpiresAtUtc, actual.RefreshTokenExpiresAtUtc);
        Assert.Equal(expected.Roles, actual.Roles);
    }

    private static ClientAuthTokenSnapshot Snapshot(string owner = Owner, string access = "initial-a")
        => new(access, DateTime.UtcNow.AddHours(1), "refresh-" + access, DateTime.UtcNow.AddDays(1), owner, owner, ["Driver"]);
    private static HttpResponseMessage TokenResponse(ClientAuthTokenSnapshot snapshot) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new 토큰응답
        {
            UserId = snapshot.UserId, UserName = snapshot.UserName, AccessToken = snapshot.AccessToken,
            RefreshToken = snapshot.RefreshToken, AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtc, Roles = snapshot.Roles.ToArray()
        })
    };
    private static 토큰갱신요청 RefreshBody(Call call)
        => JsonSerializer.Deserialize<토큰갱신요청>(call.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private sealed class MemoryTokenStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Snapshot { get; set; }
        public Func<CancellationToken, Task<ClientAuthTokenSnapshot?>>? Load { get; set; }
        public int ClearCount { get; private set; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Load?.Invoke(ct) ?? Task.FromResult(Snapshot);
        }
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); Snapshot = snapshot; return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); ClearCount++; Snapshot = null; return Task.CompletedTask;
        }
    }

    private sealed class DelayedResponse : IDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<HttpResponseMessage> WaitAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            return await response.Task.WaitAsync(Limit, ct);
        }
        public void Complete(HttpResponseMessage value) => response.TrySetResult(value);
        public void Dispose() => response.TrySetCanceled();
    }

    private sealed record Call(HttpMethod Method, Uri Uri, string? Bearer, string? Body);
    private sealed class Handler(Func<Call, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<Call> calls = [];
        public IReadOnlyList<Call> Calls { get { lock (calls) return calls.ToArray(); } }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var call = new Call(request.Method, request.RequestUri!, request.Headers.Authorization?.Parameter,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
            lock (calls) calls.Add(call);
            return await respond(call, ct);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public MemoryTokenStore Store { get; }
        public AuthSession Session { get; }
        public Handler Handler { get; }
        private HttpClient Http { get; }
        public AuthApiService Auth { get; }
        public DriverTransportApiService Transport { get; }
        private Fixture(MemoryTokenStore store, AuthSession session, Handler handler, HttpClient http)
        {
            Store = store; Session = session; Handler = handler; Http = http;
            Auth = new AuthApiService(http, session);
            Transport = new DriverTransportApiService(new DriverApiClient(http, session, Auth, new DriverOperatingProfileService()));
        }
        public static async Task<Fixture> CreateAsync(Func<Call, CancellationToken, Task<HttpResponseMessage>> respond,
            bool cold = false, ClientAuthTokenSnapshot? initial = null)
        {
            initial ??= Snapshot();
            var store = new MemoryTokenStore();
            var session = new AuthSession(store, new ClientSessionGuard());
            if (cold) store.Snapshot = initial;
            else await session.ApplyAsync(initial).WaitAsync(Limit);
            var handler = new Handler(respond);
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://cargo.example.test/") };
            return new(store, session, handler, http);
        }
        public void Dispose() => Http.Dispose();
    }
}
