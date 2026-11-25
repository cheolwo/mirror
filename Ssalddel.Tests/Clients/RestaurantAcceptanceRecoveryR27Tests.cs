using System.Text.Json;
using Microsoft.Extensions.Options;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Options;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 음식점 Desk 서비스의 상태 복구 회귀입니다. 서버는 제어된 대역이며 HTTP·실기기·인쇄 실행이 아닙니다.</summary>
public sealed class RestaurantAcceptanceRecoveryR27Tests
{
    private static readonly DateTime Now = new(2026, 10, 28, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 신규명시수락은현재서버허용을읽은뒤한번만POST하고_정상전표를유지한다()
    {
        var api = new Orders();
        using var desk = Desk(api, await Auth());

        var result = await desk.주문수락후전표준비Async("A", 20);

        Assert.True(result.성공);
        Assert.False(result.수락복구됨);
        Assert.NotNull(result.전표Draft);
        Assert.Equal("A", result.전표Draft.DocumentNo);
        Assert.Equal(new[] { "GET", "POST" }, api.Events);
        Assert.NotEqual(Guid.Empty, Assert.Single(api.Acceptances).클라이언트요청Id);
        Assert.Equal(20, result.상세주문!.조리예상분);
    }

    [Fact]
    public async Task 수락저장뒤응답유실은GET으로복구하고_두번째POST나전표생성을하지않는다()
    {
        var api = new Orders();
        api.Accept = (request, _) =>
        {
            api.Apply(request);
            throw new HttpRequestException("controlled lost acceptance response");
        };
        using var desk = Desk(api, await Auth());

        var recovered = await desk.주문수락후전표준비Async("A", 20);

        AssertRecovered(recovered);
        Assert.Equal(20, recovered.상세주문!.조리예상분);
        Assert.Equal(new[] { "GET", "POST", "GET" }, api.Events);
        Assert.Single(api.Acceptances);
    }

    [Fact]
    public async Task 응답유실후재확인도실패했어도_새클라이언트의목록상세GET으로기존수락을복원한다()
    {
        var api = new Orders();
        var store = new PendingStore();
        api.Accept = (request, _) => { api.Apply(request); throw new HttpRequestException("controlled lost response"); };
        api.Read = (count, _, _) => count == 1
            ? Task.FromResult<음식주문응답?>(Pending())
            : throw new HttpRequestException("controlled failed confirmation");
        using (var first = Desk(api, await Auth(), store))
            await AssertNotSuccessful(() => first.주문수락후전표준비Async("A", 20));

        api.Read = null;
        api.Accept = null;
        using var restarted = Desk(api, await Auth(), store);
        var list = await restarted.주문목록조회Async();
        var detail = await restarted.주문조회Async("A");

        Assert.Equal("A", Assert.Single(list).주문번호);
        Assert.NotNull(detail!.상세주문!.음식점수락시각Utc);
        Assert.Single(api.Acceptances);
        Assert.Equal(0, store.Saves);
        var confirmed = await restarted.주문수락후전표준비Async("A", 45);
        AssertRecovered(confirmed);
        Assert.Equal(20, confirmed.상세주문!.조리예상분);
        Assert.Single(api.Acceptances);
    }

    [Fact]
    public async Task 미저장실패뒤GET이대기여도자동POST하지않고_사용자가다시수락할때만명령을보낸다()
    {
        var api = new Orders
        {
            Accept = (request, _) =>
            {
                request.조리예상분 = 160;
                request.음식점명 = "controlled mutated transport input";
                throw new HttpRequestException("controlled request not committed");
            }
        };
        using var desk = Desk(api, await Auth());
        await AssertNotSuccessful(() => desk.주문수락후전표준비Async("A", 20));
        var originalId = Assert.Single(api.Acceptances).클라이언트요청Id;
        Assert.Equal(new[] { "GET", "POST", "GET" }, api.Events);
        await desk.주문조회Async("A");
        Assert.Single(api.Acceptances);
        api.Accept = null;

        var next = await desk.주문수락후전표준비Async("A", 30);

        Assert.True(next.성공);
        Assert.False(next.수락복구됨);
        Assert.NotNull(next.전표Draft);
        Assert.Equal(2, api.Acceptances.Count);
        Assert.Equal(originalId, api.Acceptances[1].클라이언트요청Id);
        Assert.Equal(20, api.Acceptances[1].조리예상분);
        Assert.Equal(api.Acceptances[0].음식점명, api.Acceptances[1].음식점명);
    }

    [Fact]
    public async Task 수락전조회가실패하면캐시상태로POST하지않는다()
    {
        var api = new Orders();
        using var desk = Desk(api, await Auth());
        await desk.주문조회Async("A");
        api.Read = (_, _, _) => throw new HttpRequestException("controlled offline");

        await AssertNotSuccessful(() => desk.주문수락후전표준비Async("A", 20));

        Assert.Empty(api.Acceptances);
        Assert.DoesNotContain("POST", api.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수락전없는주문이나다른주문응답은수락이나전표로바꾸지않는다(bool differentOrder)
    {
        var api = new Orders { Read = (_, _, _) => Task.FromResult<음식주문응답?>(differentOrder ? Pending("B") : null) };
        using var desk = Desk(api, await Auth());

        await AssertNotSuccessful(() => desk.주문수락후전표준비Async("A", 20));

        Assert.Empty(api.Acceptances);
    }

    [Fact]
    public async Task 서버가수락행동을허용하지않으면대기상태이름만으로POST하지않는다()
    {
        var api = new Orders();
        api.Current.AvailableActions = [];
        using var desk = Desk(api, await Auth());

        await AssertNotSuccessful(() => desk.주문수락후전표준비Async("A", 20));

        Assert.Empty(api.Acceptances);
    }

    [Theory]
    [InlineData(음식주문상태코드.취소)]
    [InlineData(음식주문상태코드.거절)]
    public async Task 취소나거절주문은예전수락시각이있어도수락복구성공이나인쇄자료로바꾸지않는다(string status)
    {
        var api = new Orders { Current = Accepted() };
        api.Current.상태 = status;
        using var desk = Desk(api, await Auth());

        await AssertNotSuccessful(() => desk.주문수락후전표준비Async("A", 20));

        Assert.Empty(api.Acceptances);
    }

    [Theory]
    [InlineData("missing-stamp")]
    [InlineData("different-order")]
    [InlineData("closed")]
    public async Task POST응답의수락시각과주문식별자와현재상태를확인하지못하면전표를만들지않는다(string invalid)
    {
        var response = Accepted(invalid == "different-order" ? "B" : "A");
        if (invalid == "missing-stamp") response.음식점수락시각Utc = null;
        if (invalid == "closed") response.상태 = 음식주문상태코드.취소;
        var api = new Orders { Accept = (_, _) => Task.FromResult<음식주문응답?>(response) };
        using var desk = Desk(api, await Auth());

        await AssertNotSuccessful(() => desk.주문수락후전표준비Async("A", 20));

        Assert.Single(api.Acceptances);
        Assert.InRange(api.Reads, 1, 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 인증실패는일반통신오류처럼수락성공으로복구하거나추가조회하지않는다(bool failPost)
    {
        var api = new Orders();
        if (failPost) api.Accept = (_, _) => throw new UnauthorizedAccessException("controlled authentication rejection");
        else api.Read = (_, _, _) => throw new UnauthorizedAccessException("controlled authentication rejection");
        using var desk = Desk(api, await Auth());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => desk.주문수락후전표준비Async("A", 20));

        Assert.Equal(1, api.Reads);
        Assert.Equal(failPost ? 1 : 0, api.Acceptances.Count);
    }

    [Fact]
    public async Task 수락전GET대기중계정이바뀌면늦은응답으로이전주문을수락하지않는다()
    {
        var api = new Orders();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<음식주문응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Read = (_, _, _) => { entered.SetResult(); return reply.Task; };
        var auth = await Auth();
        using var desk = Desk(api, auth);
        var accept = desk.주문수락후전표준비Async("A", 20);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await auth.Session.ApplyAsync(Token("other-owner"));
        reply.SetResult(Pending());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => accept);

        Assert.Empty(api.Acceptances);
    }

    [Fact]
    public async Task POST대기중계정이바뀌면늦은수락응답이나후속GET을다른계정에적용하지않는다()
    {
        var api = new Orders();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<음식주문응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Accept = (_, _) => { entered.SetResult(); return reply.Task; };
        var auth = await Auth();
        using var desk = Desk(api, auth);
        var accept = desk.주문수락후전표준비Async("A", 20);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await auth.Session.ApplyAsync(Token("other-owner"));
        reply.SetResult(Accepted());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => accept);

        Assert.Single(api.Acceptances);
        Assert.Equal(1, api.Reads);
    }

    [Fact]
    public async Task 서버저장뒤호출취소는성공이나전표로바꾸지않고_나중GET으로상태만읽는다()
    {
        var api = new Orders();
        using var cancellation = new CancellationTokenSource();
        api.Accept = (request, _) =>
        {
            api.Apply(request);
            cancellation.Cancel();
            return Task.FromResult<음식주문응답?>(Copy(api.Current));
        };
        using (var first = Desk(api, await Auth()))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.주문수락후전표준비Async("A", 20, cancellation.Token));
        using var restarted = Desk(api, await Auth());

        var current = await restarted.주문조회Async("A");

        Assert.NotNull(current!.상세주문!.음식점수락시각Utc);
        Assert.Single(api.Acceptances);
    }

    [Fact]
    public async Task 서버가다른적용조리시간을반환하면새입력으로서버확정값을덮어쓰지않는다()
    {
        var api = new Orders { Accept = (_, _) => Task.FromResult<음식주문응답?>(Accepted(minutes: 20)) };
        using var desk = Desk(api, await Auth());

        var result = await desk.주문수락후전표준비Async("A", 45);

        Assert.True(result.성공);
        Assert.Equal(20, result.상세주문!.조리예상분);
        Assert.Equal(20, result.주문!.선택조리예상분);
        Assert.Single(api.Acceptances);
    }

    [Fact]
    public async Task 느린수락응답보다최신GET이앞서면최신상태만복구하고자동전표를보류한다()
    {
        var api = new Orders();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<음식주문응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Accept = (_, _) => { entered.SetResult(); return reply.Task; };
        using var desk = Desk(api, await Auth());
        var accept = desk.주문수락후전표준비Async("A", 20);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        api.Current = Accepted(minutes: 35);
        api.Current.Revision = 9;
        api.Current.상태 = 음식주문상태코드.조리중;
        await desk.주문조회Async("A");
        reply.SetResult(Accepted(minutes: 20));

        var current = await accept;

        AssertRecovered(current);
        Assert.Equal(9, current.상세주문!.Revision);
        Assert.Equal(35, current.상세주문.조리예상분);
        Assert.Equal(음식주문상태코드.조리중, current.상세주문.상태);
        Assert.Single(api.Acceptances);
    }

    private static void AssertRecovered(음식점주문수락결과 result)
    {
        Assert.True(result.성공);
        Assert.True(result.수락복구됨);
        Assert.NotNull(result.상세주문!.음식점수락시각Utc);
        Assert.Null(result.전표Draft);
    }

    private static async Task AssertNotSuccessful(Func<Task<음식점주문수락결과>> operation)
    {
        음식점주문수락결과? result;
        try { result = await operation(); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or ArgumentException or KeyNotFoundException) { return; }
        Assert.False(result.성공);
        Assert.False(result.수락복구됨);
        Assert.Null(result.전표Draft);
    }

    private static 음식점주문DeskService Desk(Orders api, RestaurantAuthService auth, IRestaurantProgressPendingStore? store = null)
        => new(api, new Sound(), new 음식점전표DraftFactory(), new Preparation(), Options.Create(new RestaurantDeskOptions()), auth, store);

    private static 음식주문응답 Pending(string orderNo = "A") => new()
    {
        주문번호 = orderNo, 음식점Id = 1, 상태 = 음식주문상태코드.주문대기, Revision = 5,
        CreatedAt = Now.AddMinutes(-20), 수령인정보 = new(), 상품목록 = [new() { 상품명 = "합성 메뉴", 수량 = 2, 단가 = 5000m }],
        AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점주문수락, ExpectedRevision = 5 }]
    };
    private static 음식주문응답 Accepted(string orderNo = "A", int minutes = 20)
    {
        var result = Pending(orderNo);
        result.상태 = 음식주문상태코드.주문확인;
        result.음식점수락시각Utc = Now;
        result.조리예상분 = minutes;
        result.Revision = 6;
        result.AvailableActions = [];
        return result;
    }
    private static 음식주문응답 Copy(음식주문응답 value) => JsonSerializer.Deserialize<음식주문응답>(JsonSerializer.Serialize(value))!;

    private sealed class Orders : I음식주문ApiClient
    {
        public 음식주문응답 Current { get; set; } = Pending();
        public int Reads { get; private set; }
        public List<string> Events { get; } = [];
        public List<음식점주문수락요청> Acceptances { get; } = [];
        public Func<int, string, CancellationToken, Task<음식주문응답?>>? Read { get; set; }
        public Func<음식점주문수락요청, CancellationToken, Task<음식주문응답?>>? Accept { get; set; }
        public Task<음식주문응답?> 주문상세조회Async(string 주문번호, CancellationToken cancellationToken = default)
        {
            Reads++; Events.Add("GET");
            return Read?.Invoke(Reads, 주문번호, cancellationToken) ?? Task.FromResult<음식주문응답?>(Copy(Current));
        }
        public Task<음식점주문수신함응답> 주문목록조회Async(음식점주문수신함조회요청 request, CancellationToken cancellationToken = default)
        {
            Events.Add("LIST");
            return Task.FromResult(new 음식점주문수신함응답 { Items = [Copy(Current)], TotalCount = 1, Page = 1, PageSize = 100 });
        }
        public Task<음식주문응답?> 음식점수락Async(string 주문번호, 음식점주문수락요청 request, CancellationToken cancellationToken = default)
        {
            Events.Add("POST");
            Acceptances.Add(JsonSerializer.Deserialize<음식점주문수락요청>(JsonSerializer.Serialize(request))!);
            if (Accept is not null) return Accept(request, cancellationToken);
            Apply(request);
            return Task.FromResult<음식주문응답?>(Copy(Current));
        }
        public void Apply(음식점주문수락요청 request)
        {
            Current = Accepted(Current.주문번호, request.조리예상분 ?? 20);
            Current.상태이력 = [new() { 클라이언트요청Id = request.클라이언트요청Id }];
        }
        public Task<음식주문응답?> 음식점진행변경Async(string 주문번호, 음식점주문진행변경요청 request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Acceptance recovery must not execute a progress command.");
    }
    private sealed class Preparation : I음식점조리시간설정Service
    {
        public 음식점조리시간설정Snapshot 현재조회() => new(20, new Dictionary<string, int>());
        public Task 저장Async(int 음식점기본조리분, IReadOnlyDictionary<string, int> 상품별기본조리분, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Sound : I주문알림Service
    {
        public Task 신규주문알림재생Async(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Canonical acceptance recovery must not alert a fabricated new order.");
    }
    private static ClientAuthTokenSnapshot Token(string owner) => new("test-access", DateTime.UtcNow.AddHours(1), "test-refresh", DateTime.UtcNow.AddDays(1), owner, "test", ["음식점"]);
    private static async Task<RestaurantAuthService> Auth()
    {
        var session = new ClientAuthSession(new TokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(Token("owner"));
        return new(new HttpClient { BaseAddress = new("http://localhost/") }, session);
    }
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { value = null; return Task.CompletedTask; }
    }
    private sealed class PendingStore : IRestaurantProgressPendingStore
    {
        private string? value;
        public int Saves { get; private set; }
        public Task<RestaurantProgressPendingSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(value is null ? null : JsonSerializer.Deserialize<RestaurantProgressPendingSnapshot>(value));
        public Task SaveAsync(RestaurantProgressPendingSnapshot snapshot, CancellationToken cancellationToken = default)
        { Saves++; value = JsonSerializer.Serialize(snapshot); return Task.CompletedTask; }
        public async Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
        { if ((await LoadAsync(cancellationToken))?.OwnerId == ownerId) value = null; }
    }
}
