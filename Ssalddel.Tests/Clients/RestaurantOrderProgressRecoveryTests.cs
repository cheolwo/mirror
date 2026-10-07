using Microsoft.Extensions.Options;
using RestaurantDeskApp.Options;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

public sealed partial class RestaurantOrderProgressRecoveryTests
{
    [Fact]
    public async Task StoredThenLostResultIsReadBeforeChangedInputCanBeSubmitted()
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("A");
        api.Command = request =>
        {
            api.Apply(request);
            throw new HttpRequestException("controlled lost response");
        };
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.조리시간변경Async("A", 20));
        var original = AssertLostCommandAttempts(api);
        api.Command = null;

        var confirmed = await desk.조리시간변경Async("A", 30);
        Assert.Equal(20, confirmed!.상세주문!.조리예상분);
        Assert.Single(api.Requests);
        Assert.Equal(2, api.Reads);
        Assert.Equal(new[] { "GET", "POST", "GET" }, api.Events);
        Assert.Equal(6, confirmed.상세주문.Revision);

        var changed = await desk.조리시간변경Async("A", 30);
        Assert.Equal(30, changed!.상세주문!.조리예상분);
        Assert.Equal(2, api.Requests.Count);
        Assert.NotEqual(original.클라이언트요청Id, api.Requests[1].클라이언트요청Id);
        Assert.Equal(6, api.Requests[1].예상Revision);
        Assert.Equal(new[] { "GET", "POST", "GET", "POST" }, api.Events);
    }

    [Fact]
    public async Task UnconfirmedRetryKeepsOriginalPayloadUntilRevisionConflictIsConfirmed()
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("A");
        api.Command = _ => throw new HttpRequestException("controlled unsent response");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.조리시간변경Async("A", 20));
        var original = AssertLostCommandAttempts(api);
        api.Current = Detail(7, 45);
        api.Command = _ => throw new SsalddelApiException("controlled conflict", 409, "progress", "", null,
            requiresStateRefresh: true, availableRecoveryActions: [업무복구행동Ids.상태전체재조회]);

        await Assert.ThrowsAsync<SsalddelApiException>(() => desk.조리시간변경Async("A", 30));
        Assert.Equal(2, api.Requests.Count);
        var retried = api.Requests[1];
        AssertSameRequest(original, retried);
        Assert.Equal(5, retried.예상Revision);
        Assert.Equal(20, retried.조리예상분);
        Assert.Equal(original.작업, retried.작업);
        Assert.Equal(original.사유, retried.사유);
        Assert.Equal(45, api.Current.조리예상분);
        Assert.Equal(new[] { "GET", "POST", "GET", "POST", "GET" }, api.Events);

        api.Command = null;
        var changed = await desk.조리시간변경Async("A", 30);
        Assert.Equal(3, api.Requests.Count);
        Assert.NotEqual(original.클라이언트요청Id, api.Requests[2].클라이언트요청Id);
        Assert.Equal(7, api.Requests[2].예상Revision);
        Assert.Equal(30, api.Requests[2].조리예상분);
        Assert.Equal(30, changed!.상세주문!.조리예상분);
        Assert.Equal(new[] { "GET", "POST", "GET", "POST", "GET", "POST" }, api.Events);
    }

    [Fact]
    public async Task SuccessfulCanonicalRefreshReleasesConfirmedRequestForANewChange()
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("A");
        api.Command = request => { api.Apply(request); throw new HttpRequestException("controlled loss"); };
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.조리시간변경Async("A", 20));
        var first = AssertLostCommandAttempts(api).클라이언트요청Id;
        await desk.주문조회Async("A");
        api.Command = null;
        await desk.조리시간변경Async("A", 30);
        Assert.Equal(2, api.Requests.Count);
        Assert.NotEqual(first, api.Requests[1].클라이언트요청Id);
        Assert.Equal(30, api.Requests[1].조리예상분);
        Assert.Equal(6, api.Requests[1].예상Revision);
        Assert.Equal(new[] { "GET", "POST", "GET", "POST" }, api.Events);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("missing")]
    [InlineData("other-order")]
    public async Task UnusableCanonicalReadDoesNotPostChangedOrOriginalInput(string outcome)
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("A");
        api.Command = _ => throw new HttpRequestException("controlled uncertain command");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.조리시간변경Async("A", 20));
        AssertLostCommandAttempts(api);
        api.Read = _ => outcome switch
        {
            "failure" => throw new HttpRequestException("controlled read failure"),
            "missing" => Task.FromResult<음식주문응답?>(null),
            _ => Task.FromResult<음식주문응답?>(new() { 주문번호 = "B" })
        };
        await Assert.ThrowsAnyAsync<Exception>(() => desk.조리시간변경Async("A", 30));
        Assert.Single(api.Requests);
        Assert.Equal(new[] { "GET", "POST", "GET" }, api.Events);
    }

    [Fact]
    public async Task RejectionRetryKeepsTheOriginalReason()
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("A");
        api.Command = _ => throw new HttpRequestException("controlled uncertain command");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.주문거절Async("A", "원래 품절 사유"));
        var original = AssertLostCommandAttempts(api);
        api.Command = null;
        await desk.주문거절Async("A", "바뀐 사유");
        Assert.Equal(2, api.Requests.Count);
        Assert.Equal("원래 품절 사유", api.Requests[1].사유);
        AssertSameRequest(original, api.Requests[1]);
        Assert.Equal(new[] { "GET", "POST", "GET", "POST" }, api.Events);
    }

    [Fact]
    public async Task ConfirmedRevisionConflictAllowsANewRequestWithCurrentRevision()
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("A");
        api.Current = Detail(8, 45);
        api.Command = _ => throw new SsalddelApiException("controlled conflict", 409, "progress", "", null,
            requiresStateRefresh: true, availableRecoveryActions: [업무복구행동Ids.상태전체재조회]);
        await Assert.ThrowsAsync<SsalddelApiException>(() => desk.조리시간변경Async("A", 20));
        var first = Assert.Single(api.Requests).클라이언트요청Id;
        Assert.Equal(new[] { "GET", "POST", "GET" }, api.Events);
        api.Command = null;
        await desk.조리시간변경Async("A", 30);
        Assert.NotEqual(first, api.Requests[1].클라이언트요청Id);
        Assert.Equal(8, api.Requests[1].예상Revision);
        Assert.Equal(30, api.Requests[1].조리예상분);
        Assert.Equal(2, api.Requests.Count);
        Assert.Equal(new[] { "GET", "POST", "GET", "POST" }, api.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameOwnerExpiryPreservesPendingButExplicitLogoutClearsIt(bool logout)
    {
        var api = new Orders();
        var session = new ClientAuthSession(new TokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(Token("owner"));
        var auth = new RestaurantAuthService(new HttpClient(), session);
        using var desk = Desk(api, auth);
        await desk.주문조회Async("A");
        api.Command = _ => throw new HttpRequestException("controlled uncertain command");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.조리시간변경Async("A", 20));
        var original = AssertLostCommandAttempts(api);
        if (logout) await auth.LogoutAsync();
        else await auth.InvalidateRejectedSessionAsync(CancellationToken.None);
        await session.ApplyAsync(Token("owner"));
        api.Command = null;
        await desk.조리시간변경Async("A", 30);
        Assert.Equal(2, api.Requests.Count);
        Assert.Equal(logout ? 30 : 20, api.Requests[1].조리예상분);
        Assert.Equal(!logout, original.클라이언트요청Id == api.Requests[1].클라이언트요청Id);
        if (logout)
        {
            Assert.Null(api.Requests[1].예상Revision);
            Assert.Equal(new[] { "GET", "POST", "POST" }, api.Events);
        }
        else
        {
            AssertSameRequest(original, api.Requests[1]);
            Assert.Equal(new[] { "GET", "POST", "GET", "POST" }, api.Events);
        }
    }

    [Fact]
    public async Task AnotherOwnerNeverReceivesThePreviousPendingRequest()
    {
        var api = new Orders();
        var session = new ClientAuthSession(new TokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(Token("first-owner"));
        using var desk = Desk(api, new RestaurantAuthService(new HttpClient(), session));
        await desk.주문조회Async("A");
        api.Command = _ => throw new HttpRequestException("controlled uncertain command");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.조리시간변경Async("A", 20));
        var first = AssertLostCommandAttempts(api).클라이언트요청Id;
        await session.ApplyAsync(Token("second-owner"));
        api.Command = null;
        await desk.조리시간변경Async("A", 30);
        Assert.Equal(2, api.Requests.Count);
        Assert.NotEqual(first, api.Requests[1].클라이언트요청Id);
        Assert.Equal(30, api.Requests[1].조리예상분);
        Assert.Null(api.Requests[1].예상Revision);
        Assert.Equal(1, api.Reads);
        Assert.Equal(new[] { "GET", "POST", "POST" }, api.Events);
    }

    [Fact]
    public async Task LateCommandResultCannotApplyToAnotherOwnersCache()
    {
        var api = new Orders();
        var session = new ClientAuthSession(new TokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(Token("first-owner"));
        using var desk = Desk(api, new RestaurantAuthService(new HttpClient(), session));
        await desk.주문조회Async("A");
        var late = new TaskCompletionSource<음식주문응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Command = _ => late.Task;
        var pending = desk.조리시간변경Async("A", 20);
        await session.ApplyAsync(Token("second-owner"));
        api.Current = Detail(9, 45);
        await desk.주문조회Async("A");
        late.SetResult(Detail(6, 20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        api.Command = null;
        await desk.조리시간변경Async("A", 30);
        Assert.Equal(9, api.Requests[1].예상Revision);
        Assert.Equal(30, api.Requests[1].조리예상분);
        Assert.NotEqual(api.Requests[0].클라이언트요청Id, api.Requests[1].클라이언트요청Id);
    }

    [Fact]
    public async Task CancellationAfterCommitKeepsRequestUntilCanonicalReadConfirmsIt()
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("A");
        using var cancellation = new CancellationTokenSource();
        api.Command = request => { api.Apply(request); cancellation.Cancel(); return Task.FromResult<음식주문응답?>(api.Current); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => desk.조리시간변경Async("A", 20, cancellation.Token));
        api.Command = null;
        var confirmed = await desk.조리시간변경Async("A", 30);
        Assert.Equal(20, confirmed!.상세주문!.조리예상분);
        Assert.Single(api.Requests);
        Assert.Equal(new[] { "GET", "POST", "GET" }, api.Events);
    }

    private static 음식점주문진행변경요청 AssertLostCommandAttempts(Orders api)
    {
        // Uncertain progress commands are not automatically re-sent.
        Assert.Equal(new[] { "GET", "POST" }, api.Events);
        return Assert.Single(api.Requests);
    }

    private static void AssertSameRequest(음식점주문진행변경요청 original, 음식점주문진행변경요청 retried)
    {
        Assert.NotEqual(Guid.Empty, original.클라이언트요청Id);
        Assert.Equal(original.클라이언트요청Id, retried.클라이언트요청Id);
        Assert.Equal(original.예상Revision, retried.예상Revision);
        Assert.Equal(original.작업, retried.작업);
        Assert.Equal(original.조리예상분, retried.조리예상분);
        Assert.Equal(original.사유, retried.사유);
        Assert.Equal(original.사유Code, retried.사유Code);
    }

    private static 음식점주문DeskService Desk(Orders api, RestaurantAuthService? auth = null, IRestaurantProgressPendingStore? pendingStore = null)
        => new(api, new Sound(), new 음식점전표DraftFactory(), new Preparation(), Options.Create(new RestaurantDeskOptions()), auth, pendingStore);

    private static 음식주문응답 Detail(long revision = 5, int minutes = 15) => new()
    {
        주문번호 = "A", 음식점Id = 1, 상태 = "조리중", 배차상태 = "기사배정", Revision = revision,
        CreatedAt = new DateTime(2026, 10, 4, 1, 0, 0, DateTimeKind.Utc),
        조리예상분 = minutes, 수령인정보 = new(), 상품목록 = [],
        AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점조리시간변경, ExpectedRevision = revision }]
    };

    private sealed class Orders : I음식주문ApiClient
    {
        public 음식주문응답 Current = Detail();
        public int Reads;
        public List<음식점주문진행변경요청> Requests { get; } = [];
        public List<string> Events { get; } = [];
        public Func<CancellationToken, Task<음식주문응답?>>? Read;
        public Func<음식점주문진행변경요청, Task<음식주문응답?>>? Command;
        public Task<음식주문응답?> 주문상세조회Async(string 주문번호, CancellationToken cancellationToken = default)
        { Events.Add("GET"); Reads++; return Read?.Invoke(cancellationToken) ?? Task.FromResult<음식주문응답?>(Current); }
        public Task<음식주문응답?> 음식점진행변경Async(string 주문번호, 음식점주문진행변경요청 request, CancellationToken cancellationToken = default)
        {
            Events.Add("POST");
            Requests.Add(new() { 클라이언트요청Id = request.클라이언트요청Id, 예상Revision = request.예상Revision,
                작업 = request.작업, 조리예상분 = request.조리예상분, 사유 = request.사유, 사유Code = request.사유Code });
            if (Command is not null) return Command(request);
            Apply(request);
            return Task.FromResult<음식주문응답?>(Current);
        }
        public void Apply(음식점주문진행변경요청 request)
        {
            // Model the server's idempotent acknowledgement: a lost response can repeat the POST,
            // but the same request ID must not advance the order revision twice.
            if (Current.상태이력.Any(history => history.클라이언트요청Id == request.클라이언트요청Id)) return;
            var history = Current.상태이력;
            Current = Detail(Current.Revision + 1, request.조리예상분 ?? Current.조리예상분 ?? 15);
            Current.상태이력 = [.. history, new() { 클라이언트요청Id = request.클라이언트요청Id, 사유 = request.사유 }];
        }
        public Task<음식점주문수신함응답> 주문목록조회Async(음식점주문수신함조회요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식주문응답?> 음식점수락Async(string 주문번호, 음식점주문수락요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Preparation : I음식점조리시간설정Service
    {
        public 음식점조리시간설정Snapshot 현재조회() => new(20, new Dictionary<string, int>());
        public Task 저장Async(int 음식점기본조리분, IReadOnlyDictionary<string, int> 상품별기본조리분, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Sound : I주문알림Service
    { public Task 신규주문알림재생Async(CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private static ClientAuthTokenSnapshot Token(string owner) => new("test-access", DateTime.UtcNow.AddHours(1), "test-refresh", DateTime.UtcNow.AddDays(1), owner, "test", ["음식점"]);
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? token;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) { token = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { token = null; return Task.CompletedTask; }
    }
}
