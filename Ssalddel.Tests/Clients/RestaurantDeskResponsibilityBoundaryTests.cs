using Microsoft.Extensions.Options;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Options;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantDeskResponsibilityBoundaryTests
{
    [Fact]
    public async Task InboxReadsEveryHundredItemPageAndRemovesOnlyAfterTheFullSnapshot()
    {
        var api = new Orders();
        using var desk = Desk(api);
        await desk.주문조회Async("previous");
        var all = Enumerable.Range(1, 205).Select(number => Detail($"order-{number}")).ToArray();
        api.Inbox = request => Task.FromResult(Page(205, all.Skip((request.Page - 1) * 100).Take(100).ToArray()));

        var result = await desk.주문목록조회Async();

        Assert.Equal(205, result.Count);
        Assert.DoesNotContain(result, item => item.주문번호 == "previous");
        Assert.Equal(new[] { 1, 2, 3 }, api.Pages.Select(page => page.Page));
        Assert.All(api.Pages, page =>
        {
            Assert.Equal(100, page.PageSize);
            Assert.Equal(음식점주문수신함처리상태코드.미처리, page.처리상태);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyOrRepeatedPartialPageStopsWithoutRemovingTheLastSuccessfulOrders(bool emptyPage)
    {
        var api = new Orders();
        using var desk = Desk(api);
        api.Inbox = _ => Task.FromResult(Page(2, Detail("A"), Detail("B")));
        var previous = await desk.주문목록조회Async();
        api.Pages.Clear();
        api.Inbox = request => Task.FromResult(request.Page == 1
            ? Page(3, Detail("A"))
            : emptyPage ? Page(3) : Page(3, Detail("A")));

        var partial = await desk.주문목록조회Async();

        Assert.Equal(new[] { 1, 2 }, api.Pages.Select(page => page.Page));
        Assert.Equal(2, partial.Count);
        Assert.Same(previous.Single(item => item.주문번호 == "B"), partial.Single(item => item.주문번호 == "B"));
        Assert.Equal(2, partial.Select(item => item.주문번호).Distinct().Count());
    }

    [Fact]
    public async Task FailureOnLaterPageKeepsTheSuccessfulSnapshotAndDoesNotApplyPartialResults()
    {
        var api = new Orders();
        using var desk = Desk(api);
        api.Inbox = _ => Task.FromResult(Page(2, Detail("A"), Detail("B")));
        var previous = await desk.주문목록조회Async();
        api.Inbox = request => request.Page == 1
            ? Task.FromResult(Page(2, Detail("C")))
            : throw new HttpRequestException("controlled second-page failure");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.주문목록조회Async());
        api.Inbox = _ => Task.FromResult(Page(1));

        var recovered = await desk.주문목록조회Async();

        Assert.Equal(new[] { "A", "B" }, recovered.Select(item => item.주문번호).Order());
        Assert.All(recovered, item => Assert.Same(previous.Single(old => old.주문번호 == item.주문번호), item));
    }

    [Fact]
    public async Task ProjectionKeepsProductCopiesCookingRecommendationAndUnreadFirstSorting()
    {
        var api = new Orders();
        using var desk = Desk(api);
        var pending = Detail("pending");
        pending.상품목록 = [new() { 메뉴Id = 5, 상품명 = "설정 메뉴", 수량 = 2, 단가 = 9000 }];
        pending.조리예상분 = 18;
        var accepted = Detail("accepted");
        accepted.CreatedAt = pending.CreatedAt.AddMinutes(10);
        accepted.상태 = 음식주문상태코드.기사배정;
        api.Inbox = _ => Task.FromResult(Page(2, accepted, pending));

        var result = await desk.주문목록조회Async();
        var item = result[0];
        pending.상품목록[0].상품명 = "변경된 원본";

        Assert.Equal("pending", item.주문번호);
        Assert.True(item.미확인);
        Assert.Equal("설정 메뉴", Assert.Single(item.상품목록).상품명);
        Assert.Equal(2, item.상품목록[0].수량);
        Assert.Equal(9000, item.상품목록[0].단가);
        Assert.Equal(5, item.상품목록[0].메뉴Id);
        Assert.Equal(27, item.추천조리예상분);
        Assert.Equal(18, item.선택조리예상분);
        Assert.False(Assert.Single(item.상품별조리기준).음식점기본값사용);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationRequiresTheCanonicalRestaurantBeforeUpsertOrSound(bool missing)
    {
        var api = new Orders { Read = _ => Task.FromResult<음식주문응답?>(missing ? null : Detail("A")) };
        var sound = new Sound();
        using var desk = Desk(api, sound);

        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.주문알림수신Async(new() { 주문번호 = "A", 음식점Id = 2 }));

        Assert.Equal(0, sound.Calls);
        Assert.Empty(await desk.주문목록조회Async());
    }

    [Fact]
    public async Task CanonicalNotificationIsUpsertedBeforeSoundAndIgnoresPayloadCustomerAndMenu()
    {
        var api = new Orders();
        var sound = new Sound();
        using var desk = Desk(api, sound);
        sound.Play = () => desk.전표출력요청기록Async("A");

        var item = await desk.주문알림수신Async(new()
        {
            주문번호 = "A", 음식점Id = 1, 고객명 = "알림 후보", 메뉴요약 = "알림 메뉴", 본문 = "알림 안내"
        });

        Assert.Equal("정본 수령인", item.고객명);
        Assert.Equal("정본 메뉴 1", item.메뉴요약);
        Assert.Equal(음식점주문복구출처.실시간, item.복구출처);
        Assert.NotNull(item.전표출력요청시각);
        Assert.Null(item.전표출력시각);
        Assert.Equal(1, sound.Calls);
    }

    [Fact]
    public async Task SoundFailureDoesNotRollBackTheAlreadyConfirmedInboxOrder()
    {
        var api = new Orders();
        var sound = new Sound { Play = () => throw new InvalidOperationException("controlled sound failure") };
        using var desk = Desk(api, sound);
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.주문알림수신Async(new() { 주문번호 = "A", 음식점Id = 1 }));

        var result = await desk.주문목록조회Async();

        Assert.Equal("A", Assert.Single(result).주문번호);
        Assert.Equal(1, sound.Calls);
    }

    [Fact]
    public async Task LateNotificationCannotUpdateTheNewOwnersInboxOrPlaySound()
    {
        var session = new ClientAuthSession(new TokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(Token("first-owner"));
        var api = new Orders();
        var sound = new Sound();
        using var desk = Desk(api, sound, new RestaurantAuthService(new HttpClient(), session));
        var late = new TaskCompletionSource<음식주문응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Read = _ => late.Task;
        var notification = desk.주문알림수신Async(new() { 주문번호 = "A", 음식점Id = 1 });
        await session.ApplyAsync(Token("second-owner"));
        api.Read = orderNo => Task.FromResult<음식주문응답?>(Detail(orderNo));
        await desk.주문조회Async("B");
        late.SetResult(Detail("A"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notification);

        Assert.Equal("B", Assert.Single(await desk.주문목록조회Async()).주문번호);
        Assert.Equal(0, sound.Calls);
    }

    private static 음식점주문DeskService Desk(Orders api, Sound? sound = null, RestaurantAuthService? auth = null)
        => new(api, sound ?? new Sound(), new 음식점전표DraftFactory(), new Preparation(),
            Options.Create(new RestaurantDeskOptions()), auth);

    private static 음식주문응답 Detail(string orderNo) => new()
    {
        주문번호 = orderNo, 음식점Id = 1, 상태 = 음식주문상태코드.주문대기,
        CreatedAt = new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc),
        상품목록 = [new() { 상품명 = "정본 메뉴", 수량 = 1, 단가 = 9000 }],
        수령인정보 = new() { 수령인명 = "정본 수령인" }
    };

    // 빈 부분 응답은 전체 목록 확보가 아니므로 기존 수신함을 그대로 관찰한다.
    private static 음식점주문수신함응답 Page(int total, params 음식주문응답[] items)
        => new() { TotalCount = total, Items = items };

    private sealed class Orders : I음식주문ApiClient
    {
        public List<음식점주문수신함조회요청> Pages { get; } = [];
        public Func<음식점주문수신함조회요청, Task<음식점주문수신함응답>> Inbox = _ => Task.FromResult(Page(1));
        public Func<string, Task<음식주문응답?>> Read = orderNo => Task.FromResult<음식주문응답?>(Detail(orderNo));
        public Task<음식점주문수신함응답> 주문목록조회Async(음식점주문수신함조회요청 request, CancellationToken cancellationToken = default)
        { Pages.Add(request); return Inbox(request); }
        public Task<음식주문응답?> 주문상세조회Async(string 주문번호, CancellationToken cancellationToken = default) => Read(주문번호);
        public Task<음식주문응답?> 음식점수락Async(string 주문번호, 음식점주문수락요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<음식주문응답?> 음식점진행변경Async(string 주문번호, 음식점주문진행변경요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Preparation : I음식점조리시간설정Service
    {
        public 음식점조리시간설정Snapshot 현재조회() => new(20, new Dictionary<string, int> { ["설정 메뉴"] = 27 });
        public Task 저장Async(int 음식점기본조리분, IReadOnlyDictionary<string, int> 상품별기본조리분, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Sound : I주문알림Service
    {
        public int Calls;
        public Func<Task> Play = () => Task.CompletedTask;
        public Task 신규주문알림재생Async(CancellationToken cancellationToken = default) { Calls++; return Play(); }
    }

    private static ClientAuthTokenSnapshot Token(string owner) => new("synthetic-access", DateTime.UtcNow.AddHours(1),
        "synthetic-refresh", DateTime.UtcNow.AddDays(1), owner, "synthetic", ["음식점"]);

    private sealed class TokenStore : IClientSecureTokenStore
    {
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<ClientAuthTokenSnapshot?>(null);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
