using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 화주 인증/조회 소스에 합성 HTTP와 기존 저장소 fixture를 연결합니다. 실서버·기기 검증은 아닙니다.</summary>
public sealed class ShipperAuthenticationRequestContinuityTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private const string Owner = "shipper-a";
    private const string RefreshPath = "/api/v1/auth/refresh";
    private const string ClientRequest = "synthetic-old-a-client-request";
    private const string RecoveryPath = "/api/v1/shipper/requests/by-client-request/" + ClientRequest;
    private const string ConsentPath = "/api/v1/common/application-privacy-consents";

    [Fact]
    public Task QueuedEnsureCannotAdoptDifferentOwnersValidLogin()
        => AssertQueuedEnsureRejectedAsync("shipper-b");

    [Fact]
    public Task QueuedEnsureCannotAdoptSameOwnersNewLogin()
        => AssertQueuedEnsureRejectedAsync(Owner);

    [Fact]
    public async Task QueuedEnsureCannotRefreshOrClearExpiredReplacementLogin()
    {
        using var fixture = new ShipperRecoveryFixture();
        var initial = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(initial).WaitAsync(Limit);
        using var delayed = new DelayedResponse();
        using var handler = new Handler((call, ct) =>
        {
            Assert.Equal(RefreshPath, call.Uri.AbsolutePath);
            var body = RefreshBody(call);
            if (body.UserId == Owner) return delayed.WaitAsync(ct);
            Assert.Equal("shipper-b", body.UserId);
            return Task.FromResult(Json(HttpStatusCode.BadRequest, new { errorCode = "SyntheticRejectedRefresh" }));
        });
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var active = api.EnsureAccessTokenAsync(forceRefresh: true);
        Task<string?> queued;
        var next = ShipperRecoveryFixture.Snapshot("shipper-b", "new-expired-login")
            with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        long nextRevision;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            queued = api.EnsureAccessTokenAsync();
            Assert.False(queued.IsCompleted);
            await fixture.Auth.ApplyAsync(next).WaitAsync(Limit);
            nextRevision = fixture.Auth.SessionRevision;
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token(initial with { AccessToken = "obsolete-a-refresh" }))); }
        await AssertSupersededEnsureAsync(active);
        await AssertSupersededEnsureAsync(queued);
        await AssertSnapshotAsync(fixture, next);
        Assert.Equal(nextRevision, fixture.Auth.SessionRevision);
        Assert.Equal(Owner, RefreshBody(Assert.Single(handler.Calls)).UserId);
    }

    [Fact]
    public Task QueuedClientRequestRecoveryCannotSendOldPathWithReplacementBearer()
        => AssertQueuedBusinessBlockedAsync(writeConsent: false);

    [Fact]
    public Task QueuedConsentWriteCannotSendWithReplacementBearer()
        => AssertQueuedBusinessBlockedAsync(writeConsent: true);

    [Fact]
    public async Task QueuedEnsureReusesOrdinaryRefreshWithinSameSession()
    {
        using var fixture = new ShipperRecoveryFixture();
        var initial = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(initial).WaitAsync(Limit);
        var revision = fixture.Auth.SessionRevision;
        var refreshed = initial with { AccessToken = "refreshed-a", RefreshToken = "refresh-refreshed-a" };
        using var delayed = new DelayedResponse();
        using var handler = new Handler((call, ct) =>
        {
            Assert.Equal(RefreshPath, call.Uri.AbsolutePath);
            return delayed.WaitAsync(ct);
        });
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var active = api.EnsureAccessTokenAsync(forceRefresh: true);
        Task<string?> queued;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            queued = api.EnsureAccessTokenAsync();
            Assert.False(queued.IsCompleted);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token(refreshed))); }
        Assert.Null(await active.WaitAsync(Limit));
        Assert.Null(await queued.WaitAsync(Limit));
        await AssertSnapshotAsync(fixture, refreshed);
        Assert.Equal(revision, fixture.Auth.SessionRevision);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task ColdStoredSessionCanRestoreAndEnsureWithoutHttp()
    {
        using var fixture = new ShipperRecoveryFixture();
        var saved = ShipperRecoveryFixture.Snapshot();
        fixture.Store.Snapshot = saved;
        Assert.Null(fixture.Auth.UserId);
        using var handler = new Handler((_, _) => throw new InvalidOperationException("정상 저장 세션은 인증 HTTP가 필요하지 않습니다."));
        using var http = Client(handler);
        Assert.Null(await CreateApi(http, fixture.Auth).EnsureAccessTokenAsync().WaitAsync(Limit));
        await AssertSnapshotAsync(fixture, saved);
        Assert.True(fixture.Auth.IsLoggedIn);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ColdStoredSessionCanReadEmptyListBoundToRestoredOwner(int page)
    {
        using var fixture = new ShipperRecoveryFixture();
        var saved = ShipperRecoveryFixture.Snapshot();
        fixture.Store.Snapshot = saved;
        Assert.Null(fixture.Auth.UserId);
        using var handler = new Handler((call, _) =>
        {
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal("/api/v1/shipper/requests", call.Uri.AbsolutePath);
            var expectedQuery = page == 1 ? "?shipperId=" + Owner : "?shipperId=" + Owner + "&page=2";
            Assert.Equal(expectedQuery, call.Uri.Query);
            Assert.True(string.Equals(saved.AccessToken, call.Bearer, StringComparison.Ordinal), "복원한 화주의 토큰을 사용해야 합니다.");
            return Task.FromResult(Json(HttpStatusCode.OK, Array.Empty<object>()));
        });
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var observer = new TransportRequestLedgerObserver();
        // null 토큰 제공자로 SignalR 연결을 시작하지 않습니다.
        await using var realtime = new TransportRequestLedgerRealtimeClient(http.BaseAddress!, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(http, fixture.Auth, api, observer, realtime);
        var read = page == 1 ? service.GetRequestsAsync() : service.GetRequestsPageAsync(page);
        Assert.Empty(await read.WaitAsync(Limit));
        await AssertSnapshotAsync(fixture, saved);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task RealtimeWaitCannotSendOldRecoveryRequestUnderReplacementSession()
    {
        using var fixture = new ShipperRecoveryFixture();
        var initial = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(initial).WaitAsync(Limit);
        using var handler = new Handler((_, _) => throw new InvalidOperationException("계정 변경 뒤에는 업무 HTTP를 전송하면 안 됩니다."));
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var observer = new TransportRequestLedgerObserver();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var realtime = new TransportRequestLedgerRealtimeClient(http.BaseAddress!, async ct =>
        {
            started.TrySetResult();
            return await release.Task.WaitAsync(Limit, ct);
        }, observer);
        var service = new ServerBackedShipperOperationsService(http, fixture.Auth, api, observer, realtime);
        var request = service.GetRequestByClientRequestIdAsync(ClientRequest);
        var next = ShipperRecoveryFixture.Snapshot("shipper-b", "new-login");
        try
        {
            await started.Task.WaitAsync(Limit);
            Assert.False(request.IsCompleted);
            await fixture.Auth.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { release.TrySetResult(null); } // null로 끝내므로 SignalR 연결을 만들지 않습니다.
        Exception? stopped = null;
        try { await request.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (InvalidOperationException ex) { stopped = ex; }
        await AssertSnapshotAsync(fixture, next);
        Assert.NotNull(stopped);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task OrdinarySameOwnerRefreshDuringRealtimeWaitUsesFreshTokenForList()
    {
        using var fixture = new ShipperRecoveryFixture();
        var initial = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(initial).WaitAsync(Limit);
        var revision = fixture.Auth.SessionRevision;
        var refreshed = initial with { AccessToken = "same-a-refreshed", RefreshToken = "refresh-same-a-refreshed" };
        using var handler = new Handler((call, _) =>
        {
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal("/api/v1/shipper/requests", call.Uri.AbsolutePath);
            Assert.Equal("?shipperId=" + Owner, call.Uri.Query);
            Assert.True(string.Equals(refreshed.AccessToken, call.Bearer, StringComparison.Ordinal), "같은 세션에서 정상 갱신한 최신 토큰을 사용해야 합니다.");
            return Task.FromResult(Json(HttpStatusCode.OK, Array.Empty<object>()));
        });
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var observer = new TransportRequestLedgerObserver();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var realtime = new TransportRequestLedgerRealtimeClient(http.BaseAddress!, async ct =>
        {
            started.TrySetResult();
            return await release.Task.WaitAsync(Limit, ct);
        }, observer);
        var service = new ServerBackedShipperOperationsService(http, fixture.Auth, api, observer, realtime);
        var request = service.GetRequestsAsync();
        try
        {
            await started.Task.WaitAsync(Limit);
            Assert.False(request.IsCompleted);
            Assert.True(await fixture.Auth.TryRefreshAsync(refreshed, revision, initial.RefreshToken).WaitAsync(Limit));
            Assert.Equal(revision, fixture.Auth.SessionRevision);
        }
        finally { release.TrySetResult(null); } // 중첩 조회가 다시 확인해도 null이며 외부 연결은 없습니다.
        Assert.Empty(await request.WaitAsync(Limit));
        await AssertSnapshotAsync(fixture, refreshed);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task CancellingOneColdEnsureDoesNotCancelAnotherCallersRestore()
    {
        using var fixture = new ShipperRecoveryFixture();
        var saved = ShipperRecoveryFixture.Snapshot();
        fixture.Store.Snapshot = saved;
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLoad = new TaskCompletionSource<ClientAuthTokenSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loads = 0;
        fixture.Store.Load = async () =>
        {
            if (Interlocked.Increment(ref loads) != 1) return saved;
            started.TrySetResult();
            // 기존 Load hook에는 ct 인자가 없어 첫 호출자 A의 토큰을 닫아 저장소 취소를 재현합니다.
            return await firstLoad.Task.WaitAsync(Limit, cancellation.Token);
        };
        using var handler = new Handler((_, _) => throw new InvalidOperationException("정상 저장 세션 복원에는 HTTP가 필요하지 않습니다."));
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var first = api.EnsureAccessTokenAsync(cancellationToken: cancellation.Token);
        Task<string?> second;
        try
        {
            await started.Task.WaitAsync(Limit);
            second = api.EnsureAccessTokenAsync();
            Assert.False(second.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Limit));
            Assert.Null(await second.WaitAsync(Limit));
        }
        finally { firstLoad.TrySetResult(saved); }
        Assert.Equal(2, Volatile.Read(ref loads));
        fixture.Store.Load = null;
        await AssertSnapshotAsync(fixture, saved);
        Assert.Empty(handler.Calls);
    }

    private static async Task AssertQueuedEnsureRejectedAsync(string nextOwner)
    {
        using var fixture = new ShipperRecoveryFixture();
        var initial = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(initial).WaitAsync(Limit);
        var originalRevision = fixture.Auth.SessionRevision;
        using var delayed = new DelayedResponse();
        using var handler = new Handler((call, ct) =>
        {
            Assert.Equal(RefreshPath, call.Uri.AbsolutePath);
            return delayed.WaitAsync(ct);
        });
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var active = api.EnsureAccessTokenAsync(forceRefresh: true);
        Task<string?> queued;
        var next = ShipperRecoveryFixture.Snapshot(nextOwner, "new-login");
        long nextRevision;
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            // 직접 호출은 현재 A에서 시작해 이미 점유한 refreshGate에 도달합니다.
            queued = api.EnsureAccessTokenAsync();
            Assert.False(queued.IsCompleted);
            await fixture.Auth.ApplyAsync(next).WaitAsync(Limit);
            nextRevision = fixture.Auth.SessionRevision;
            Assert.NotEqual(originalRevision, nextRevision);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token(initial with { AccessToken = "obsolete-a-refresh" }))); }
        await AssertSupersededEnsureAsync(active);
        await AssertSupersededEnsureAsync(queued);
        await AssertSnapshotAsync(fixture, next);
        Assert.Equal(nextRevision, fixture.Auth.SessionRevision);
        Assert.Equal(Owner, RefreshBody(Assert.Single(handler.Calls)).UserId);
    }

    private static async Task AssertQueuedBusinessBlockedAsync(bool writeConsent)
    {
        using var fixture = new ShipperRecoveryFixture();
        var initial = ShipperRecoveryFixture.Snapshot();
        await fixture.Auth.ApplyAsync(initial).WaitAsync(Limit);
        using var delayed = new DelayedResponse();
        using var handler = new Handler((call, ct) =>
        {
            if (call.Uri.AbsolutePath == RefreshPath) return delayed.WaitAsync(ct);
            if (writeConsent)
            {
                Assert.Equal(HttpMethod.Post, call.Method);
                Assert.Equal(ConsentPath, call.Uri.AbsolutePath);
                return Task.FromResult(Json(HttpStatusCode.OK, new 신청개인정보동의증적Response()));
            }
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal(RecoveryPath, call.Uri.AbsolutePath);
            return Task.FromResult(Json(HttpStatusCode.NotFound, new { errorCode = "SyntheticNotFound" }));
        });
        using var http = Client(handler);
        var api = CreateApi(http, fixture.Auth);
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(http.BaseAddress!, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(http, fixture.Auth, api, observer, realtime);
        var active = api.EnsureAccessTokenAsync(forceRefresh: true);
        Task business;
        var next = ShipperRecoveryFixture.Snapshot("shipper-b", "new-login");
        try
        {
            await delayed.Started.Task.WaitAsync(Limit);
            business = writeConsent
                ? service.RecordPrivacyConsentAsync(new 신청개인정보동의기록Request
                {
                    증적Id = Guid.NewGuid(), 업무Code = "SyntheticContinuity", 출처Code = "SyntheticFixture",
                    동의문버전 = "test-only", 수집이용동의 = true, 연령요건확인 = true
                })
                : service.GetRequestByClientRequestIdAsync(ClientRequest);
            Assert.False(business.IsCompleted);
            await fixture.Auth.ApplyAsync(next).WaitAsync(Limit);
        }
        finally { delayed.Complete(Json(HttpStatusCode.OK, Token(initial with { AccessToken = "obsolete-a-refresh" }))); }
        await AssertSupersededEnsureAsync(active);
        Exception? stopped = null;
        try { await business.WaitAsync(Limit); }
        catch (OperationCanceledException ex) { stopped = ex; }
        catch (InvalidOperationException ex) { stopped = ex; }
        await AssertSnapshotAsync(fixture, next);
        Assert.NotNull(stopped);
        Assert.Equal(RefreshPath, Assert.Single(handler.Calls).Uri.AbsolutePath);
        Assert.DoesNotContain(handler.Calls, call => call.Uri.AbsolutePath == (writeConsent ? ConsentPath : RecoveryPath));
    }

    private static async Task AssertSupersededEnsureAsync(Task<string?> pending)
    {
        var cancelled = false;
        string? error = null;
        try { error = await pending.WaitAsync(Limit); }
        catch (OperationCanceledException) { cancelled = true; }
        Assert.True(cancelled || !string.IsNullOrWhiteSpace(error), "이전 세션의 요청은 새 세션에서 성공으로 이어지면 안 됩니다.");
    }

    private static async Task AssertSnapshotAsync(ShipperRecoveryFixture fixture, ClientAuthTokenSnapshot expected)
    {
        AssertMemory(fixture.Auth, expected);
        Assert.NotNull(fixture.Store.Snapshot);
        Assert.True(string.Equals(expected.AccessToken, fixture.Store.Snapshot.AccessToken, StringComparison.Ordinal), "저장된 access token이 보존돼야 합니다.");
        Assert.True(string.Equals(expected.RefreshToken, fixture.Store.Snapshot.RefreshToken, StringComparison.Ordinal), "저장된 refresh token이 보존돼야 합니다.");
        Assert.Equal(expected.UserId, fixture.Store.Snapshot.UserId);
        Assert.Equal(expected.AccessTokenExpiresAtUtc, fixture.Store.Snapshot.AccessTokenExpiresAtUtc);
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

    private static HttpClient Client(Handler handler) => new(handler) { BaseAddress = new Uri("https://test.invalid/") };
    private static AuthApiService CreateApi(HttpClient http, IAuthSession auth)
        => new(http, auth,
            new 꾸미기보유권동기화Service(http, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
            new SsalddelMobilePushInstallationClient(http, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));
    private static 토큰응답 Token(ClientAuthTokenSnapshot snapshot) => new()
    {
        UserId = snapshot.UserId, UserName = snapshot.UserName, AccessToken = snapshot.AccessToken,
        RefreshToken = snapshot.RefreshToken, AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtc,
        RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtc, Roles = snapshot.Roles.ToArray()
    };
    private static 토큰갱신요청 RefreshBody(Call call)
        => JsonSerializer.Deserialize<토큰갱신요청>(call.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    private static HttpResponseMessage Json<T>(HttpStatusCode status, T body) => new(status) { Content = JsonContent.Create(body) };

    private sealed class UnusedDecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken ct = default)
            => throw new InvalidOperationException("인증 갱신 검사는 꾸미기 저장소를 사용하지 않습니다.");
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken ct = default)
            => throw new InvalidOperationException("인증 갱신 검사는 꾸미기 저장소를 사용하지 않습니다.");
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
}
