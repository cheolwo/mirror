using System.Net;
using System.Net.Http.Json;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Work;

namespace Ssalddel.Tests.Clients;

public sealed partial class CargoWorkRecoveryR75Tests
{
    [Fact]
    public async Task LostPostResponse_QueriesSameRequestAndContinuesLocationWithoutAnotherPost()
    {
        using var f = new Fixture { LosePostResponse = true };
        var result = await f.Start();
        Assert.True(result.ServerConfirmed); Assert.True(result.LocationStarted);
        Assert.Equal(1, f.Posts); Assert.Equal(2, f.Reads); Assert.Equal(1, f.LocationStarts);
        Assert.NotEqual(Guid.Empty, f.Sent!.ClientRequestId);
        Assert.False(f.Sent.커뮤니티운행공개); Assert.False(f.Sent.커뮤니티구단위위치공개동의);
        Assert.Null(f.Coordinator.Pending); Assert.Empty(f.Saved);
        Assert.Equal("운행중", f.LastLocationStatus);
    }

    [Fact]
    public async Task UnconfirmedRequest_IsRestoredAcrossPageReentryAndOnlyReadAgain()
    {
        using var f = new Fixture { LosePostResponse = true, FailRecoveryRead = true };
        var first = await f.Start();
        Assert.False(first.ServerConfirmed); Assert.Equal(0, f.LocationStarts);
        var requestId = f.Sent!.ClientRequestId;
        Assert.Equal(requestId, f.Coordinator.Pending!.ClientRequestId);
        Assert.Contains("미확인", first.Message);
        f.Coordinator.Dispose();
        using var restored = new DriverWorkStartRecoveryCoordinator(f.Api, f.Auth, f.Store);
        await restored.RestoreAsync();
        Assert.Equal(requestId, restored.Pending!.ClientRequestId);
        Assert.False(restored.Pending.CommunityPublicationRequested);
        f.FailRecoveryRead = false;
        var result = await restored.StartOrRecoverAsync(_ => throw new InvalidOperationException("Must not create a new request."), f.StartLocation);
        Assert.True(result.LocationStarted); Assert.Equal(1, f.Posts); Assert.Equal(1, f.LocationStarts);
        Assert.Empty(f.Saved);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("owner")]
    [InlineData("execution")]
    [InlineData("shift")]
    [InlineData("status")]
    public async Task CanonicalMismatch_DoesNotStartLocationOrClearTheOriginalRequest(string mismatch)
    {
        using var f = new Fixture { Mismatch = mismatch };
        var result = await f.Start();
        Assert.False(result.ServerConfirmed); Assert.False(result.LocationStarted);
        Assert.Equal(1, f.Posts); Assert.Equal(0, f.LocationStarts);
        Assert.NotNull(f.Coordinator.Pending); Assert.Single(f.Saved);
    }

    [Fact]
    public async Task InitialStateUnavailable_DoesNotCreateOrSendAStartRequest()
    {
        using var f = new Fixture { FailInitialRead = true };
        var result = await f.Start();
        Assert.False(result.ServerConfirmed); Assert.Equal(0, f.Posts);
        Assert.Empty(f.Saved); Assert.Null(f.Coordinator.Pending);
    }

    [Fact]
    public async Task ConfirmedServerStart_LocalLocationFailureKeepsIdentityForContinuation()
    {
        using var f = new Fixture { FailLocation = true };
        var result = await f.Start();
        Assert.True(result.ServerConfirmed); Assert.False(result.LocationStarted);
        Assert.True(f.Coordinator.IsServerConfirmed);
        var id = f.Coordinator.Pending!.ClientRequestId;
        f.FailLocation = false;
        result = await f.Start();
        Assert.True(result.LocationStarted); Assert.Equal(id, f.Sent!.ClientRequestId);
        Assert.Equal(1, f.Posts); Assert.Equal(2, f.LocationStarts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogoutOrNewLogin_DiscardsLateCanonicalResponse(bool sameUser)
    {
        using var f = new Fixture();
        f.BeforeCanonical = async () => await f.Auth.Login(sameUser ? "driver-r75" : "other-driver");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Start());
        Assert.Equal(1, f.Posts); Assert.Equal(0, f.LocationStarts); Assert.Null(f.Coordinator.Pending);
        Assert.NotNull(await f.Store.LoadAsync("driver-r75", default));
        if (!sameUser) Assert.Null(await f.Store.LoadAsync("other-driver", default));
    }

    [Fact]
    public async Task CancellationAfterPost_PreservesPendingIdentityAndDoesNotPublishLateSuccess()
    {
        using var f = new Fixture(); using var cancellation = new CancellationTokenSource();
        f.AfterPost = () => cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Start(cancellation.Token));
        Assert.Equal(1, f.Posts); Assert.Equal(0, f.LocationStarts); Assert.Single(f.Saved);
        var id = f.Sent!.ClientRequestId;
        f.AfterPost = null;
        var result = await f.Start();
        Assert.True(result.LocationStarted); Assert.Equal(id, f.Sent.ClientRequestId); Assert.Equal(1, f.Posts);
    }

    [Fact]
    public async Task TokenRefreshWithinSameSession_DoesNotInvalidateRecovery()
    {
        using var f = new Fixture();
        f.BeforeCanonical = () => { f.Auth.Refresh(); return Task.CompletedTask; };
        Assert.True((await f.Start()).LocationStarted);
        Assert.Equal(1, f.Posts);
    }

    [Fact]
    public async Task StoreFailure_PreventsPostAndStoresNoExactLocation()
    {
        using var f = new Fixture();
        var store = new DriverWorkStartRecoveryStore(_ => Task.FromResult<string?>(null),
            (_, json) => { Assert.DoesNotContain("37.57", json); Assert.DoesNotContain("127.07", json); throw new IOException("test store unavailable"); }, _ => { });
        using var coordinator = new DriverWorkStartRecoveryCoordinator(f.Api, f.Auth, store);
        await Assert.ThrowsAsync<IOException>(() => coordinator.StartOrRecoverAsync(f.CreateRequest, f.StartLocation));
        Assert.Equal(0, f.Posts); Assert.Equal(0, f.LocationStarts);
    }

    [Fact]
    public async Task ExistingCanonicalCargoShift_ResumesLocationWithoutChangingOptionalDisclosure()
    {
        using var f = new Fixture { AlreadyRunning = true };
        var result = await f.Start();
        Assert.True(result.LocationStarted); Assert.Equal(0, f.Posts); Assert.Empty(f.Saved);
        Assert.Contains("공개 설정은 새로 변경하지 않았습니다", result.Message);
    }

    [Fact]
    public async Task SameRequestCanonicalStopped_ClearsRecoveryWithoutSendingAnotherStart()
    {
        using var f = new Fixture { Mismatch = "stopped" };
        var result = await f.Start();
        Assert.False(result.ServerConfirmed); Assert.False(f.Coordinator.IsServerConfirmed);
        Assert.Equal("ConfirmedStopped", f.Coordinator.ResolutionState);
        Assert.Null(f.Coordinator.Pending); Assert.Empty(f.Saved);
        Assert.Equal(1, f.Posts); Assert.Equal(0, f.LocationStarts);
        Assert.Contains("종료된 상태", result.Message);
    }

    [Fact]
    public async Task TypedApiRejectionWithoutReliableStatusMetadata_KeepsPendingAndNeverCreatesAnotherStart()
    {
        using var f = new Fixture { RejectPost = true };
        var first = await f.Start();
        Assert.False(first.ServerConfirmed); Assert.NotNull(f.Coordinator.Pending);
        var requestId = f.Coordinator.Pending!.ClientRequestId;
        var second = await f.Start();
        Assert.False(second.ServerConfirmed); Assert.Equal(requestId, f.Coordinator.Pending!.ClientRequestId);
        Assert.Equal("PendingCanonicalMismatch", f.Coordinator.ResolutionState);
        Assert.Contains("운영자", second.Message); Assert.Equal(1, f.Posts); Assert.Equal(0, f.LocationStarts);
    }

    [Fact]
    public async Task R76_서버의동일요청거절과정본대기확인뒤에만_새시작을허용한다()
    {
        using var f = new Fixture { RejectPost = true, MarkedReject = true };
        var first = await f.Start();
        Assert.False(first.ServerConfirmed); Assert.Equal("ConfirmedRejected", f.Coordinator.ResolutionState);
        Assert.Null(f.Coordinator.Pending); Assert.Empty(f.Saved); Assert.Equal(0, f.LocationStarts);
        var rejectedId = f.Sent!.ClientRequestId;
        f.RejectPost = false;
        f.Sent = null; // 거절된 요청은 서버 근무를 생성하지 않았으므로 다음 사전 조회도 대기다.
        Assert.True((await f.Start()).LocationStarted);
        Assert.NotEqual(rejectedId, f.Sent.ClientRequestId); Assert.Equal(2, f.Posts);
    }

    [Fact]
    public async Task R76_거절후정본조회실패는_재진입후확인하며_시작을재전송하지않는다()
    {
        using var f = new Fixture { RejectPost = true, MarkedReject = true, FailRecoveryRead = true };
        Assert.False((await f.Start()).ServerConfirmed); Assert.NotNull(f.Coordinator.Pending);
        f.Coordinator.Dispose();
        using var restored = new DriverWorkStartRecoveryCoordinator(f.Api, f.Auth, f.Store);
        f.FailRecoveryRead = false; await restored.RestoreAsync();
        Assert.Equal(f.Sent!.ClientRequestId, restored.Pending!.RejectedClientRequestId);
        await restored.StartOrRecoverAsync(_ => throw new Exception("No repost"), f.StartLocation);
        Assert.Null(restored.Pending); Assert.Empty(f.Saved); Assert.Equal("ConfirmedRejected", restored.ResolutionState);
        Assert.Equal(1, f.Posts); Assert.Equal(0, f.LocationStarts);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(408, false)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    public async Task R76_다른요청거절과일시실패는_미확정요청을해제하지않는다(int status, bool wrongId)
    {
        using var f = new Fixture { RejectPost = true, MarkedReject = true, WrongRejectedId = wrongId, RejectStatus = (HttpStatusCode)status };
        Assert.False((await f.Start()).ServerConfirmed); Assert.NotNull(f.Coordinator.Pending);
        await f.Start(); Assert.Equal(1, f.Posts); Assert.Equal(0, f.LocationStarts); Assert.Single(f.Saved);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        public readonly RecoveryAuth Auth = new();
        public readonly Dictionary<string, string> Saved = [];
        public readonly DriverWorkStartRecoveryStore Store;
        public readonly DriverWorkStartRecoveryCoordinator Coordinator;
        public readonly DriverWorkApiService Api;
        public 기사운행시작요청? Sent;
        public int Posts, Reads, LocationStarts;
        public bool LosePostResponse, FailRecoveryRead, FailInitialRead, FailLocation, AlreadyRunning, RejectPost, MarkedReject, WrongRejectedId;
        public HttpStatusCode RejectStatus = HttpStatusCode.BadRequest;
        public string? Mismatch, LastLocationStatus;
        public Func<Task>? BeforeCanonical;
        public Action? AfterPost;
        public Fixture(IDriverOperationalDispatchApiService? dispatchReception = null)
        {
            Store = new(key => Task.FromResult(Saved.GetValueOrDefault(key)),
                (key, value) => { Saved[key] = value; return Task.CompletedTask; }, key => Saved.Remove(key));
            _http = new(new Handler(Handle)) { BaseAddress = new Uri("https://test.invalid/") };
            Api = new(new DriverApiClient(_http, Auth, new AuthApiService(_http, Auth), new DriverOperatingProfileService()));
            Coordinator = new(Api, Auth, Store, dispatchReception);
        }
        public Task<DriverWorkStartRecoveryResult> Start(CancellationToken token = default)
            => Coordinator.StartOrRecoverAsync(CreateRequest, StartLocation, token);
        public Task<기사운행시작요청?> CreateRequest(CancellationToken _)
            => Task.FromResult<기사운행시작요청?>(new()
            { 시작위치 = "37.57,127.07", 커뮤니티운행공개 = false, 커뮤니티구단위위치공개동의 = false });
        public Task StartLocation(string status, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); LocationStarts++; LastLocationStatus = status;
            return FailLocation ? Task.FromException(new IOException("test location unavailable")) : Task.CompletedTask;
        }
        private async Task<HttpResponseMessage> Handle(HttpRequestMessage request, CancellationToken _)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/v1/driver/work/start")
            {
                Posts++; Sent = await request.Content!.ReadFromJsonAsync<기사운행시작요청>(); AfterPost?.Invoke();
                if (RejectPost)
                {
                    var rejected = new HttpResponseMessage(RejectStatus) { Content = JsonContent.Create(new { code = "DriverWork.InvalidInput" }) };
                    if (MarkedReject)
                    {
                        rejected.Headers.Add("X-Mirror-Work-Start-Outcome", "Rejected");
                        rejected.Headers.Add("X-Mirror-Work-Start-Request-Id", (WrongRejectedId ? Guid.NewGuid() : Sent!.ClientRequestId!.Value).ToString("D"));
                    }
                    return rejected;
                }
                if (LosePostResponse) throw new HttpRequestException("test response lost after server commit");
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(new 기사운행시작응답 { DriverId = "driver-r75", ShiftId = 75, Status = "운행중" }) };
            }
            Assert.Equal("/api/v1/driver/work/status", request.RequestUri!.AbsolutePath); Assert.Equal(HttpMethod.Get, request.Method);
            Reads++;
            if ((Sent is null && FailInitialRead) || (Sent is not null && FailRecoveryRead)) return new(HttpStatusCode.ServiceUnavailable);
            if (Sent is not null && BeforeCanonical is not null) await BeforeCanonical();
            var status = new 기사운행상태응답
            {
                DriverId = Mismatch == "owner" && Sent is not null ? "other-driver" : "driver-r75",
                Status = Mismatch == "status" && Sent is not null ? "예약" : Mismatch == "stopped" || RejectPost || Sent is null && !AlreadyRunning ? "대기" : "운행중",
                ShiftId = Mismatch == "shift" ? null : 75,
                WorkStartClientRequestId = RejectPost ? null : Mismatch == "request" ? Guid.NewGuid() : Sent?.ClientRequestId,
                운송실행유형 = Mismatch == "execution" ? 운송실행유형코드.음식배달 : 운송실행유형코드.화물운송
            };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(status) };
        }
        public void Dispose() { Coordinator.Dispose(); _http.Dispose(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }

    internal sealed class RecoveryAuth : IAuthSession
    {
        public string? AccessToken { get; private set; } = "test-only-access";
        public string? RefreshToken => "test-only-refresh";
        public DateTime AccessTokenExpiresAtUtc => DateTime.UtcNow.AddHours(1);
        public DateTime RefreshTokenExpiresAtUtc => DateTime.UtcNow.AddDays(1);
        public string? UserId { get; private set; } = "driver-r75";
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => UserId is not null;
        public long Version { get; private set; }
        public long SessionRevision { get; private set; }
        public event Action? Changed;
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Login(string? owner) { UserId = owner; AccessToken = owner is null ? null : "test-only-access"; Version++; SessionRevision++; Changed?.Invoke(); return Task.CompletedTask; }
        public void Refresh() { Version++; Changed?.Invoke(); }
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) => Login(snapshot.UserId);
        public Task ClearAsync(CancellationToken cancellationToken = default) => Login(null);
        public async Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
        { if (Version != expectedVersion) return false; await ClearAsync(cancellationToken); return true; }
        public async Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion, CancellationToken cancellationToken = default, bool startsNewSession = false)
        { if (Version != expectedVersion) return false; if (startsNewSession || UserId != snapshot.UserId) await Login(snapshot.UserId); else Refresh(); return true; }
    }
}
