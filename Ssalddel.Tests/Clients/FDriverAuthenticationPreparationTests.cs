using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Driver.Food;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 음식기사 세션·인증·업무 Client에 합성 HTTP/저장소를 연결합니다. 실서버·기기 검증은 아닙니다.</summary>
public sealed class FDriverAuthenticationPreparationTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private const string Owner = "food-driver-a";
    private const string NextOwner = "food-driver-b";
    private const string RefreshPath = "/api/v1/auth/refresh";
    private const string WorkspacePath = "/api/v1/driver/food-deliveries/workspace";

    [Fact]
    public Task QueuedPublicEnsureCannotAdoptReplacementOwnersValidSession()
        => AssertQueuedEnsureRejectedAsync(expiredReplacement: false);

    [Fact]
    public Task QueuedPublicEnsureCannotRefreshOrClearExpiredReplacementOwner()
        => AssertQueuedEnsureRejectedAsync(expiredReplacement: true);

    [Fact]
    public Task WorkspaceAuthenticationWaitCannotRefreshOrClearExpiredReplacementOwner()
        => AssertWorkspaceOwnerBoundaryAsync(expiredReplacement: true);

    [Fact]
    public Task WorkspaceAuthenticationWaitKeepsExistingValidReplacementOwnerGuard()
        => AssertWorkspaceOwnerBoundaryAsync(expiredReplacement: false);

    [Fact]
    public async Task OrdinarySameOwnerRefreshCanAdvanceRevisionAndResumeWorkspaceWithFreshToken()
    {
        using var delayed = new DelayedResponse();
        var refreshed = Snapshot(Owner, "refreshed-a");
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            if (call.Uri.AbsolutePath == RefreshPath) return delayed.WaitAsync(ct);
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal(WorkspacePath, call.Uri.AbsolutePath);
            Assert.True(string.Equals(refreshed.AccessToken, call.Bearer, StringComparison.Ordinal), "같은 기사의 정상 갱신 토큰으로 조회해야 합니다.");
            return Task.FromResult(WorkspaceResponse());
        });
        var revision = fixture.Session.SessionRevision;
        var active = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task<FoodDeliveryDriverWorkspaceDto> workspace;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            workspace = fixture.Workspace.GetWorkspaceAsync();
            Assert.False(workspace.IsCompleted);
        }
        finally { delayed.Complete(TokenResponse(refreshed)); }
        Assert.Null(await active.WaitAsync(Limit));
        Assert.NotNull(await workspace.WaitAsync(Limit));
        await AssertSnapshotAsync(fixture, refreshed);
        Assert.Equal(revision + 1, fixture.Session.SessionRevision);
        Assert.Equal(2, fixture.Handler.Calls.Count);
        Assert.Single(fixture.Handler.Calls, call => call.Uri.AbsolutePath == WorkspacePath);
        Assert.Equal(0, fixture.Store.ClearCount);
    }

    [Fact]
    public async Task ColdStoredSessionRestoresBeforeFirstWorkspaceRequest()
    {
        var saved = Snapshot();
        using var fixture = await Fixture.CreateAsync((call, _) =>
        {
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal(WorkspacePath, call.Uri.AbsolutePath);
            Assert.True(string.Equals(saved.AccessToken, call.Bearer, StringComparison.Ordinal), "복원한 음식기사의 토큰으로 조회해야 합니다.");
            return Task.FromResult(WorkspaceResponse());
        }, cold: true, initial: saved);
        Assert.Null(fixture.Session.UserId);
        Assert.NotNull(await fixture.Workspace.GetWorkspaceAsync().WaitAsync(Limit));
        await AssertSnapshotAsync(fixture, saved);
        Assert.Equal(ClientAuthSessionRestoreState.Authenticated, fixture.Session.CurrentState);
        Assert.Single(fixture.Handler.Calls);
    }

    [Fact]
    public async Task CancellingOneColdRestoreAllowsAnotherEnsureToLoadNormally()
    {
        var saved = Snapshot();
        using var fixture = await Fixture.CreateAsync((_, _) => throw new InvalidOperationException("정상 저장 세션 복원에는 HTTP가 필요하지 않습니다."), cold: true, initial: saved);
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

    private static async Task AssertQueuedEnsureRejectedAsync(bool expiredReplacement)
    {
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            Assert.Equal(RefreshPath, call.Uri.AbsolutePath);
            var body = RefreshBody(call);
            if (body.UserId == Owner) return delayed.WaitAsync(ct);
            Assert.Equal(NextOwner, body.UserId);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });
        var active = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task<string?> queued;
        var next = Snapshot(NextOwner, "new-b-login");
        if (expiredReplacement) next = next with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
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

    private static async Task AssertWorkspaceOwnerBoundaryAsync(bool expiredReplacement)
    {
        using var delayed = new DelayedResponse();
        using var fixture = await Fixture.CreateAsync((call, ct) =>
        {
            if (call.Uri.AbsolutePath == RefreshPath)
            {
                var body = RefreshBody(call);
                if (body.UserId == Owner) return delayed.WaitAsync(ct);
                Assert.Equal(NextOwner, body.UserId);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal(WorkspacePath, call.Uri.AbsolutePath);
            return Task.FromResult(WorkspaceResponse());
        });
        var active = fixture.Auth.EnsureAccessTokenAsync(forceRefresh: true);
        Task<FoodDeliveryDriverWorkspaceDto> workspace;
        var next = Snapshot(NextOwner, "new-b-login");
        if (expiredReplacement) next = next with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            workspace = fixture.Workspace.GetWorkspaceAsync();
            Assert.False(workspace.IsCompleted);
            await fixture.Session.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(TokenResponse(Snapshot(Owner, "obsolete-a-refresh"))); }
        await AssertSupersededEnsureAsync(active);
        Exception? stopped = null;
        try { await workspace.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (FDriverApiException ex) { stopped = ex; }
        await AssertSnapshotAsync(fixture, next);
        Assert.NotNull(stopped);
        Assert.Equal(0, fixture.Store.ClearCount);
        Assert.Equal(RefreshPath, Assert.Single(fixture.Handler.Calls).Uri.AbsolutePath);
        Assert.DoesNotContain(fixture.Handler.Calls, call => call.Uri.AbsolutePath == WorkspacePath);
    }

    private static async Task AssertSupersededEnsureAsync(Task<string?> pending)
    {
        var cancelled = false;
        string? error = null;
        try { error = await pending.WaitAsync(Limit); }
        catch (OperationCanceledException) { cancelled = true; }
        Assert.True(cancelled || !string.IsNullOrWhiteSpace(error), "이전 기사의 인증 준비가 새 기사 세션에서 성공하면 안 됩니다.");
    }

    private static async Task AssertSnapshotAsync(Fixture fixture, ClientAuthTokenSnapshot expected)
    {
        AssertMemory(fixture.Session, expected);
        Assert.NotNull(fixture.Store.Snapshot);
        Assert.True(string.Equals(expected.AccessToken, fixture.Store.Snapshot.AccessToken, StringComparison.Ordinal), "저장된 access token이 보존돼야 합니다.");
        Assert.True(string.Equals(expected.RefreshToken, fixture.Store.Snapshot.RefreshToken, StringComparison.Ordinal), "저장된 refresh token이 보존돼야 합니다.");
        Assert.Equal(expected.UserId, fixture.Store.Snapshot.UserId);
        var restored = new FDriverAuthSession(new ClientSessionGuard(), fixture.Store);
        await restored.RestoreAsync().WaitAsync(Limit);
        AssertMemory(restored, expected);
    }

    private static void AssertMemory(FDriverAuthSession actual, ClientAuthTokenSnapshot expected)
    {
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.True(string.Equals(expected.AccessToken, actual.AccessToken, StringComparison.Ordinal), "현재 access token이 보존돼야 합니다.");
        Assert.True(string.Equals(expected.RefreshToken, actual.RefreshToken, StringComparison.Ordinal), "현재 refresh token이 보존돼야 합니다.");
        Assert.Equal(expected.AccessTokenExpiresAtUtc, actual.AccessTokenExpiresAtUtc);
        Assert.Equal(expected.RefreshTokenExpiresAtUtc, actual.RefreshTokenExpiresAtUtc);
        Assert.Equal(expected.Roles, actual.Roles);
    }

    private static ClientAuthTokenSnapshot Snapshot(string owner = Owner, string access = "initial-a")
        => new(access, DateTime.UtcNow.AddMinutes(30), "refresh-" + access, DateTime.UtcNow.AddHours(1), owner, owner, ["Driver"]);
    private static HttpResponseMessage WorkspaceResponse() => new(HttpStatusCode.OK) { Content = JsonContent.Create(new FoodDeliveryDriverWorkspaceDto()) };
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
        public FDriverAuthSession Session { get; }
        public Handler Handler { get; }
        private HttpClient Http { get; }
        public FDriverAuthApiService Auth { get; }
        public FoodDeliveryDriverApiService Workspace { get; }
        private Fixture(MemoryTokenStore store, FDriverAuthSession session, Handler handler, HttpClient http)
        {
            Store = store; Session = session; Handler = handler; Http = http;
            Auth = new FDriverAuthApiService(http, session);
            Workspace = new FoodDeliveryDriverApiService(http, session, Auth);
        }
        public static async Task<Fixture> CreateAsync(Func<Call, CancellationToken, Task<HttpResponseMessage>> respond,
            bool cold = false, ClientAuthTokenSnapshot? initial = null)
        {
            initial ??= Snapshot();
            var store = new MemoryTokenStore();
            var session = new FDriverAuthSession(new ClientSessionGuard(), store);
            if (cold) store.Snapshot = initial;
            else await session.ApplyAsync(initial).WaitAsync(Limit);
            var handler = new Handler(respond);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            return new(store, session, handler, http);
        }
        public void Dispose() => Http.Dispose();
    }
}
