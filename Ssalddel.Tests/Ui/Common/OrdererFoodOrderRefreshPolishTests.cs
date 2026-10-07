using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using OrdererApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererFoodOrderRefreshPolishTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task 같은주문_느린조회와일시실패는_마지막상세와입력을보존하고_변경행동을잠근다()
    {
        var service = new FoodService { Detail = ReceiptReady() };
        using var page = await CreatePageAsync(service);
        var previous = page.상세.상세;
        page.상세.수령확인메모 = "문 앞에서 받았습니다.";
        var entered = Signal();
        var release = Signal();
        service.OnRead = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            throw new HttpRequestException("연결 실패", null, HttpStatusCode.ServiceUnavailable);
        };

        var refresh = page.주문진행새로고침Async();
        await entered.Task.WaitAsync(Timeout);
        Assert.Same(previous, page.상세.상세);
        Assert.True(page.상세.처리중);
        Assert.True(page.상세.수령확인표시);
        Assert.False(page.상세.수령확인가능);
        Assert.False(await page.주문수령확인Async());
        Assert.Empty(service.Receipts);
        release.TrySetResult();
        await refresh.WaitAsync(Timeout);
        Assert.Same(previous, page.상세.상세);
        Assert.True(page.상세.오류발생);
        Assert.False(page.상세.최신상태확인됨);
        Assert.Equal("문 앞에서 받았습니다.", page.상세.수령확인메모);
        Assert.False(await page.주문수령확인Async());

        service.OnRead = null;
        await page.주문진행새로고침Async();
        Assert.True(page.상세.최신상태확인됨);
        Assert.True(page.상세.수령확인가능);
        Assert.Equal("문 앞에서 받았습니다.", page.상세.수령확인메모);
    }

    [Fact]
    public async Task 취소초안은_같은주문실패에남지만_정본재확인전에는POST하지않는다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service);
        주문자음식주문취소ViewModelTests.Review(page.취소!);
        service.OnRead = (_, _) => throw new HttpRequestException("연결 실패");
        await page.주문진행새로고침Async();
        Assert.Equal("같은 주문을 두 번 넣었습니다.", page.취소!.사유);
        Assert.True(page.취소.검토확인);
        Assert.False(page.취소.제출가능);
        Assert.False(await page.주문취소Async());
        Assert.Empty(service.Cancellations);

        service.OnRead = null;
        service.Detail = CancelReady(revision: 8);
        await page.주문진행새로고침Async();
        Assert.True(page.취소.제출가능);
        Assert.True(await page.주문취소Async());
        Assert.Equal(8, Assert.Single(service.Cancellations).Request.예상Revision);
    }

    [Theory]
    [InlineData(음식주문상태코드.주문확인)]
    [InlineData(음식주문상태코드.전달완료)]
    public async Task 비활성과연결없음은자동조회를억제하고_복귀는종료주문도즉시한번정본조회한다(string state)
    {
        var service = new FoodService { Detail = CancelReady(state: state) };
        using var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        await controller.앱조회상태변경Async(false);
        var detailCalls = service.DetailCalls;
        var listCalls = service.ListCalls;
        Assert.False(await controller.자동새로고침Async());
        await controller.앱조회상태변경Async(false);
        Assert.Equal(detailCalls, service.DetailCalls);
        Assert.Equal(listCalls, service.ListCalls);
        Assert.False(page.상세.최신상태확인됨);

        service.Detail = CancelReady(state: 음식주문상태코드.수령확인);
        await controller.앱조회상태변경Async(true);
        Assert.Equal(detailCalls + 1, service.DetailCalls);
        Assert.Equal(listCalls + 1, service.ListCalls);
        Assert.Equal(음식주문상태코드.수령확인, page.상세.상세!.주문.상태);
        Assert.True(page.상세.최신상태확인됨);
        await controller.앱조회상태변경Async(true);
        Assert.Equal(detailCalls + 1, service.DetailCalls);
    }

    [Fact]
    public async Task 선택없는앱복귀는_목록만갱신하고_익명복귀는개인API를호출하지않는다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        page.주문선택해제();
        await controller.앱조회상태변경Async(false);
        var details = service.DetailCalls;
        var lists = service.ListCalls;
        await controller.앱조회상태변경Async(true);
        Assert.Equal(details, service.DetailCalls);
        Assert.Equal(lists + 1, service.ListCalls);
        await page.로그아웃Async();
        await controller.앱조회상태변경Async(false);
        lists = service.ListCalls;
        await controller.앱조회상태변경Async(true);
        Assert.Equal(details, service.DetailCalls);
        Assert.Equal(lists, service.ListCalls);
    }

    [Fact]
    public async Task 비활성중취소를무시한늦은자동응답은적용하지않고_복귀조회로만교체한다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        var previous = page.상세.상세;
        var entered = Signal();
        var release = Signal();
        CancellationToken readToken = default;
        service.OnRead = async (_, token) =>
        {
            readToken = token;
            entered.TrySetResult();
            await release.Task; // 의도적으로 취소를 무시하는 응답으로 세대·token 적용 경계를 검증합니다.
            return CancelReady(state: 음식주문상태코드.조리중);
        };
        var pending = controller.자동새로고침Async();
        await entered.Task.WaitAsync(Timeout);
        await controller.앱조회상태변경Async(false);
        Assert.True(readToken.IsCancellationRequested);
        Assert.False(page.상세.최신상태확인됨);
        release.TrySetResult();
        Assert.False(await pending.WaitAsync(Timeout));
        Assert.Same(previous, page.상세.상세);

        service.OnRead = null;
        service.Detail = CancelReady(state: 음식주문상태코드.기사배정);
        await controller.앱조회상태변경Async(true);
        Assert.Equal(음식주문상태코드.기사배정, page.상세.상세!.주문.상태);
        Assert.True(page.상세.최신상태확인됨);
    }

    [Fact]
    public async Task 복귀조회는실행중명령뒤에기다리며_주문전환후선택한정확한주문만조회한다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        var entered = Signal();
        var release = Signal();
        var command = controller.작업실행Async(async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            service.Detail = CancelReady("FOOD-B");
            await page.주문선택Async("FOOD-B", token);
        });
        await entered.Task.WaitAsync(Timeout);
        await controller.앱조회상태변경Async(false);
        var resume = controller.앱조회상태변경Async(true);
        Assert.False(resume.IsCompleted);
        Assert.Equal(1, service.DetailCalls);
        release.TrySetResult();
        await Task.WhenAll(command, resume).WaitAsync(Timeout);
        Assert.Equal(["FOOD-A", "FOOD-B", "FOOD-B"], service.DetailOrders);
        Assert.Equal("FOOD-B", page.상세.상세!.주문.주문번호);
        Assert.True(page.상세.최신상태확인됨);
    }

    [Fact]
    public async Task 복귀중다시비활성화하면_이전복귀응답을버리고_다음복귀응답을적용한다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        await controller.앱조회상태변경Async(false);
        var previous = page.상세.상세;
        var entered = Signal();
        var release = Signal();
        service.OnRead = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            return CancelReady(state: 음식주문상태코드.조리중);
        };
        var firstResume = controller.앱조회상태변경Async(true);
        await entered.Task.WaitAsync(Timeout);
        await controller.앱조회상태변경Async(false);
        service.OnRead = null;
        service.Detail = CancelReady(state: 음식주문상태코드.기사배정);
        var secondResume = controller.앱조회상태변경Async(true);
        release.TrySetResult();
        await firstResume.WaitAsync(Timeout);
        await secondResume.WaitAsync(Timeout);
        Assert.NotSame(previous, page.상세.상세);
        Assert.Equal(음식주문상태코드.기사배정, page.상세.상세!.주문.상태);
    }

    [Fact]
    public async Task 다른주문조회시_이전상세와메모는즉시제거하고_불일치응답을노출하지않는다()
    {
        var service = new FoodService { Detail = ReceiptReady() };
        using var page = await CreatePageAsync(service);
        page.상세.수령확인메모 = "A 주문 메모";
        var entered = Signal();
        var release = Signal();
        service.OnRead = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            return ReceiptReady(); // B 요청에 A 응답을 돌려주는 잘못된 서버 응답
        };
        var selecting = page.주문선택Async("FOOD-B");
        await entered.Task.WaitAsync(Timeout);
        Assert.Null(page.상세.상세);
        Assert.Empty(page.상세.수령확인메모);
        release.TrySetResult();
        await selecting.WaitAsync(Timeout);
        Assert.Null(page.상세.상세);
        Assert.True(page.상세.오류발생);
        Assert.Equal("FOOD-B", page.상세.요청OrderNo);
        Assert.False(page.상세.최신상태확인됨);
    }

    [Fact]
    public async Task 계정전환은_이전상세와초안을즉시제거하고_늦은이전계정응답을버린다()
    {
        var service = new FoodService();
        var auth = new AuthenticationService();
        using var page = await CreatePageAsync(service, auth);
        주문자음식주문취소ViewModelTests.Review(page.취소!);
        var entered = Signal();
        var release = Signal();
        service.OnRead = async (_, _) => { entered.TrySetResult(); await release.Task; return CancelReady(); };
        var pending = page.상세.조회Async("FOOD-A");
        await entered.Task.WaitAsync(Timeout);
        auth.UserId = "user-b";
        await page.인증.로그인Async("user-b", "test-password");
        Assert.Null(page.상세.상세);
        Assert.Null(page.상세.요청OrderNo);
        Assert.Empty(page.취소!.사유);
        release.TrySetResult();
        Assert.False(await pending.WaitAsync(Timeout));
        Assert.Null(page.상세.상세);
        Assert.False(page.상세.최신상태확인됨);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task 만료와소유권거절은_마지막개인상세를남기지않는다(HttpStatusCode status)
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service);
        service.OnRead = (_, _) => throw new HttpRequestException("접근 거절", null, status);
        Assert.False(await page.상세.조회Async("FOOD-A"));
        Assert.Null(page.상세.상세);
        Assert.False(page.상세.최신상태확인됨);
        await page.인증오류복구Async();
        Assert.Equal(status != HttpStatusCode.Unauthorized, page.인증.로그인됨);
    }

    [Fact]
    public async Task 실제모바일인증adapter_최종401과재로그인은미확정제출을보존하고_명시로그아웃만삭제한다()
    {
        var requestId = Guid.Parse("d1d65d5e-2346-4b72-92c6-704bff299140");
        var pending = new PendingStore(new("user-a", new()
        {
            클라이언트요청Id = requestId, 음식점Id = 7,
            상품목록 = []
        }, DateTime.UtcNow));
        var session = new ClientAuthSession(new TokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(new("synthetic-initial-token", DateTime.UtcNow.AddHours(1),
            "synthetic-initial-refresh", DateTime.UtcNow.AddDays(1), "user-a", "검증 주문자", ["주문자"]));
        using var handler = new LoginHandler();
        using var http = new HttpClient(handler) { BaseAddress = new("http://controlled.invalid/") };
        var mobileAuthentication = new OrdererSessionService(session, new(http, session), pending);
        var service = new FoodService();
        using var page = new 주문자음식주문PageViewModel(new(new AccessService()), new(mobileAuthentication),
            new(service), new(service, service), new(service, service));
        await page.초기화Async("FOOD-A");
        await using var controller = new 주문자음식주문새로고침Controller(page);
        service.OnRead = (_, _) => throw new HttpRequestException("세션 만료", null, HttpStatusCode.Unauthorized);

        Assert.True(await controller.자동새로고침Async());
        Assert.True(page.재로그인필요);
        Assert.False(page.인증.로그인됨);
        Assert.False(session.IsAuthenticated);
        Assert.Null(page.상세.상세);
        Assert.Equal(requestId, pending.Value!.Request.클라이언트요청Id);
        Assert.Equal(0, pending.ClearCalls);
        await page.인증오류복구Async();
        Assert.Equal(requestId, pending.Value.Request.클라이언트요청Id);

        service.OnRead = null;
        Assert.True(await page.로그인Async(new("user-a", "synthetic-password"), "FOOD-A"));
        Assert.True(session.IsAuthenticated);
        Assert.Equal(requestId, pending.Value.Request.클라이언트요청Id);
        Assert.Equal(0, pending.ClearCalls);
        Assert.Equal("/api/v1/auth/login", Assert.Single(handler.Paths));
        Assert.Empty(service.Cancellations);
        Assert.Empty(service.Receipts);

        Assert.True(await page.로그아웃Async());
        Assert.False(session.IsAuthenticated);
        Assert.Null(pending.Value);
        Assert.Equal(1, pending.ClearCalls);
    }

    [Fact]
    public async Task 만료정리중익명과선택비움은_미확정취소시도를보존하고_로그인정본에서만다시활성화한다()
    {
        var service = new FoodService
        {
            OnCancel = (_, _, _) => throw new HttpRequestException("세션 만료", null, HttpStatusCode.Unauthorized)
        };
        var auth = new AuthenticationService();
        using var page = await CreatePageAsync(service, auth);
        주문자음식주문취소ViewModelTests.Review(page.취소!);
        Assert.False(await page.주문취소Async());
        var id = page.취소!.현재시도요청Id;
        Assert.NotNull(id);
        var entered = Signal();
        var release = Signal();
        auth.OnExpire = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); };

        var expiring = page.인증오류복구Async();
        await entered.Task.WaitAsync(Timeout);
        Assert.False(page.인증.로그인됨);
        Assert.Null(page.상세.요청OrderNo);
        Assert.Null(page.상세.상세);
        Assert.False(page.취소.작성열림);
        Assert.Equal(id, page.취소.현재시도요청Id);
        Assert.Equal("같은 주문을 두 번 넣었습니다.", page.취소.사유);
        release.TrySetResult();
        await expiring.WaitAsync(Timeout);
        Assert.Equal(id, page.취소.현재시도요청Id);

        Assert.True(await page.로그인Async(new("user-a", "synthetic-password"), "FOOD-A"));
        Assert.True(page.상세.최신상태확인됨);
        Assert.True(page.취소.작성열림);
        Assert.Equal(id, page.취소.현재시도요청Id);
        Assert.True(page.취소.제출가능);
        Assert.Single(service.Cancellations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 상세렌더는_갱신중에도내용을유지하고_작은실패안내와잠긴행동을표시한다(bool receipt)
    {
        var service = new FoodService { Detail = receipt ? ReceiptReady() : CancelReady() };
        using var page = await CreatePageAsync(service);
        await RenderAsync(page, null, async html =>
        {
            if (!receipt) 주문자음식주문취소ViewModelTests.Review(page.취소!);
            var entered = Signal();
            var release = Signal();
            service.OnRead = async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                throw new HttpRequestException("일시 실패", null, HttpStatusCode.ServiceUnavailable);
            };
            var refresh = page.주문진행새로고침Async();
            await entered.Task.WaitAsync(Timeout);
            Assert.Contains("FOOD-A", html());
            Assert.Contains("검증용 상품", html());
            Assert.Contains("진행 상태를 확인하고 있습니다.", html());
            Assert.DoesNotContain("주문 상세를 불러오고 있습니다.", html());
            AssertDisabledButton(html(), receipt ? "음식 수령 확인" : "주문 취소 확인");
            release.TrySetResult();
            await refresh.WaitAsync(Timeout);
            Assert.Contains("검증용 상품", html());
            Assert.Contains("마지막으로 확인한 주문 정보입니다.", html());
            Assert.Contains("다시 확인", html());
            Assert.DoesNotContain("주문 상세를 불러오지 못했습니다.", html());
            AssertDisabledButton(html(), receipt ? "음식 수령 확인" : "주문 취소 확인");
        });
    }

    [Fact]
    public async Task 실제Workspace생명주기구독은_일시정지와연결상실뒤복귀조회하고_처리종료시구독을해제한다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service);
        var lifecycle = new 역할앱생명주기State();
        lifecycle.초기화("검증", true);
        lifecycle.전환(역할앱생명주기단계.활성);
        await RenderAsync(page, lifecycle, async _ =>
        {
            lifecycle.전환(역할앱생명주기단계.일시정지);
            Assert.False(page.상세.최신상태확인됨);
            var calls = service.DetailCalls;
            var entered = Signal();
            service.OnRead = (_, _) => { entered.TrySetResult(); return Task.FromResult(service.Detail); };
            lifecycle.전환(역할앱생명주기단계.활성);
            await entered.Task.WaitAsync(Timeout);
            Assert.Equal(calls + 1, service.DetailCalls);
            lifecycle.연결상태변경(false);
            Assert.False(page.상세.최신상태확인됨);
            calls = service.DetailCalls;
            entered = Signal();
            lifecycle.연결상태변경(true);
            await entered.Task.WaitAsync(Timeout);
            Assert.Equal(calls + 1, service.DetailCalls);
        });
        var afterDispose = service.DetailCalls;
        lifecycle.전환(역할앱생명주기단계.일시정지);
        lifecycle.전환(역할앱생명주기단계.활성);
        Assert.Equal(afterDispose, service.DetailCalls);
    }

    private static void AssertDisabledButton(string html, string text)
    {
        var button = Regex.Matches(html, "<button\\b[^>]*>.*?</button>", RegexOptions.Singleline)
            .Cast<Match>().Single(match => match.Value.Contains(text, StringComparison.Ordinal));
        Assert.Contains("disabled", button.Value.Split('>')[0]);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static 주문자음식주문상세응답 CancelReady(string orderNo = "FOOD-A", long revision = 7,
        string state = 음식주문상태코드.주문대기)
        => new()
        {
            주문 = new() { 주문번호 = orderNo, 상태 = state, 상품요약 = "검증용 상품", 총주문금액 = 18000 },
            상품목록 = [new() { 상품명 = "검증용 상품", 수량 = 1, 단가 = 18000 }],
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.주문취소,
                RevisionKindCode = 업무Revision종류Codes.음식주문, ExpectedRevision = revision }]
        };
    private static 주문자음식주문상세응답 ReceiptReady()
    {
        var detail = CancelReady(state: 음식주문상태코드.전달완료);
        detail.AvailableActions = [new() { ActionId = 음식배달가능행동Ids.주문수령확인 }];
        detail.배달진행 = new() { 배차요청됨 = true, 기사전달완료 = true };
        return detail;
    }

    private static async Task<주문자음식주문PageViewModel> CreatePageAsync(FoodService service,
        AuthenticationService? auth = null)
    {
        var page = new 주문자음식주문PageViewModel(new(new AccessService()), new(auth ?? new()),
            new(service), new(service, service), new(service, service));
        await page.초기화Async("FOOD-A");
        return page;
    }

    private static async Task RenderAsync(주문자음식주문PageViewModel page,
        역할앱생명주기State? lifecycle, Func<Func<string>, Task> verify)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddCommerceUiFixture();
        services.AddSingleton<IJSRuntime, NoopJs>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton(page);
        if (lifecycle is not null) services.AddSingleton(lifecycle);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var root = await renderer.RenderComponentAsync<OrdererFoodOrderWorkspace>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(OrdererFoodOrderWorkspace.OrderNo)] = "FOOD-A" }));
            await verify(() => WebUtility.HtmlDecode(root.ToHtmlString()));
        });
    }

    private sealed class FoodService : I주문자음식주문읽기Service,
        I주문자음식주문취소Service, I주문자음식주문수령확인Service
    {
        public 주문자음식주문상세응답? Detail { get; set; } = CancelReady();
        public Func<string, CancellationToken, Task<주문자음식주문상세응답?>>? OnRead { get; set; }
        public Func<string, 주문자음식주문취소요청, CancellationToken, Task<음식주문응답>>? OnCancel { get; set; }
        public int DetailCalls => DetailOrders.Count;
        public int ListCalls { get; private set; }
        public List<string> DetailOrders { get; } = [];
        public List<(string OrderNo, 주문자음식주문취소요청 Request)> Cancellations { get; } = [];
        public List<(string OrderNo, 주문자음식주문수령확인요청 Request)> Receipts { get; } = [];
        public Task<주문자음식주문목록응답> 목록Async(주문자음식주문목록조회요청 request,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(new 주문자음식주문목록응답 { Items = Detail is null ? [] : [Detail.주문],
                TotalCount = Detail is null ? 0 : 1 });
        }
        public Task<주문자음식주문상세응답?> 상세Async(string orderNo, CancellationToken cancellationToken = default)
        {
            DetailOrders.Add(orderNo);
            return OnRead?.Invoke(orderNo, cancellationToken) ?? Task.FromResult(Detail);
        }
        public Task<음식주문응답> 취소Async(string orderNo, 주문자음식주문취소요청 request,
            CancellationToken cancellationToken = default)
        {
            Cancellations.Add((orderNo, request));
            if (OnCancel is not null) return OnCancel(orderNo, request, cancellationToken);
            Detail = new() { 주문 = new() { 주문번호 = orderNo, 상태 = 음식주문상태코드.취소 } };
            return Task.FromResult(new 음식주문응답 { 주문번호 = orderNo, 상태 = 음식주문상태코드.취소 });
        }
        public Task<음식주문응답> 수령확인Async(string orderNo, 주문자음식주문수령확인요청 request,
            CancellationToken cancellationToken = default)
        {
            Receipts.Add((orderNo, request));
            Detail = new() { 주문 = new() { 주문번호 = orderNo, 상태 = 음식주문상태코드.수령확인 } };
            return Task.FromResult(new 음식주문응답 { 주문번호 = orderNo, 상태 = 음식주문상태코드.수령확인 });
        }
    }
    private sealed class AccessService : I음식배달페이지접근Service
    {
        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    private sealed class AuthenticationService : I주문자앱인증Service
    {
        public string UserId { get; set; } = "user-a";
        public Func<CancellationToken, Task>? OnExpire { get; set; }
        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
            => Task.FromResult(new 주문자앱인증결과(new(true, UserId, "검증 주문자")));
        public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password,
            CancellationToken cancellationToken = default) => 복원Async(cancellationToken);
        public Task 로그아웃Async(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task 세션만료Async(CancellationToken cancellationToken = default)
            => OnExpire?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/food?orderNo=FOOD-A");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class PendingStore(FoodOrderPendingSubmission? initial) : IFoodOrderPendingSubmissionStore
    {
        public FoodOrderPendingSubmission? Value { get; private set; } = initial;
        public int ClearCalls { get; private set; }
        public Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Value);
        public Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default)
        { Value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            if (Value?.Request.클라이언트요청Id == requestId) Value = null;
            return Task.CompletedTask;
        }
    }
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { value = null; return Task.CompletedTask; }
    }
    private sealed class LoginHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new 토큰응답
                {
                    UserId = "user-a", UserName = "검증 주문자", Roles = ["주문자"],
                    AccessToken = "synthetic-relogin-token", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                    RefreshToken = "synthetic-relogin-refresh", RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1)
                })
            });
        }
    }
}
