using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 음식주문페이지ViewModelTests
{
    [Fact]
    public async Task 인증ViewModel은_익명복원뒤로그인사용자세션을반영한다()
    {
        var service = new FakeAuthenticationService();
        var viewModel = new 주문자앱인증ViewModel(service);

        Assert.True(await viewModel.복원Async());
        Assert.True(viewModel.초기화됨);
        Assert.False(viewModel.로그인됨);

        Assert.True(await viewModel.로그인Async("orderer", "password"));
        Assert.True(viewModel.로그인됨);
        Assert.Equal("주문자", viewModel.현재사용자표시);
    }

    [Fact]
    public async Task 목록ViewModel은_검색상태와페이지를서버요청에전달한다()
    {
        var service = new FakeFoodOrderService
        {
            ListResponse = new 주문자음식주문목록응답
            {
                Items = [new 주문자음식주문요약응답 { 주문번호 = "FOOD-2" }],
                TotalCount = 13,
                Page = 2,
                PageSize = 12
            }
        };
        var viewModel = new 주문자음식주문목록ViewModel(service)
        {
            검색어 = "김밥",
            상태필터 = 음식주문상태코드.조리중
        };

        Assert.True(await viewModel.페이지조회Async(2));

        Assert.Equal("김밥", service.LastRequest!.검색어);
        Assert.Equal(음식주문상태코드.조리중, service.LastRequest.상태);
        Assert.Equal(2, service.LastRequest.Page);
        Assert.Equal(13, viewModel.전체건수);
        Assert.Equal(2, viewModel.총페이지수);
    }

    [Fact]
    public async Task 정확한상세가없어도다른주문으로대체하지않는다()
    {
        var service = new FakeFoodOrderService { DetailResponse = null };
        var viewModel = new 주문자음식주문상세ViewModel(
            service,
            service);

        var succeeded = await viewModel.조회Async("FOOD-MISSING");

        Assert.True(succeeded);
        Assert.Equal("FOOD-MISSING", viewModel.요청OrderNo);
        Assert.True(viewModel.찾을수없음);
        Assert.Null(viewModel.상세);
    }

    [Fact]
    public async Task 기사전달완료뒤_주문자수령확인은_같은주문번호를다시조회한다()
    {
        var service = new FakeFoodOrderService
        {
            DetailResponse = new 주문자음식주문상세응답
            {
                주문 = new 주문자음식주문요약응답
                {
                    주문번호 = "FOOD-RECEIPT",
                    상태 = 음식주문상태코드.전달완료
                },
                배달진행 = new 주문자음식배달진행응답
                {
                    기사전달완료 = true,
                    수령확인가능 = true
                },
                AvailableActions =
                [
                    new 업무가능행동Dto { ActionId = 음식배달가능행동Ids.주문수령확인 }
                ]
            },
            DetailAfterReceipt = new 주문자음식주문상세응답
            {
                주문 = new 주문자음식주문요약응답
                {
                    주문번호 = "FOOD-RECEIPT",
                    상태 = 음식주문상태코드.수령확인
                },
                배달진행 = new 주문자음식배달진행응답
                {
                    기사전달완료 = true,
                    주문자수령확인됨 = true
                }
            }
        };
        var viewModel = new 주문자음식주문상세ViewModel(service, service);
        await viewModel.조회Async("FOOD-RECEIPT");
        viewModel.수령확인메모 = "정상 수령";

        var succeeded = await viewModel.수령확인Async();

        Assert.True(succeeded);
        Assert.Equal("FOOD-RECEIPT", service.ReceiptOrderNo);
        Assert.NotEqual(Guid.Empty, service.ReceiptRequest?.클라이언트요청Id);
        Assert.Equal("정상 수령", service.ReceiptRequest?.확인메모);
        Assert.Equal(["FOOD-RECEIPT", "FOOD-RECEIPT"], service.DetailOrderNos);
        Assert.True(viewModel.상세?.배달진행.주문자수령확인됨);
        Assert.False(viewModel.수령확인가능);
    }

    [Fact]
    public async Task 기능비활성은_인증과개인주문API를호출하지않는다()
    {
        var accessService = new FakeFoodAccessService(false);
        var authenticationService = new FakeAuthenticationService();
        var orderService = new FakeFoodOrderService();
        var page = CreatePage(accessService, authenticationService, orderService);

        await page.초기화Async("FOOD-PRIVATE");

        Assert.True(page.접근.기능비활성);
        Assert.Equal(0, authenticationService.RestoreCalls);
        Assert.Empty(orderService.ListRequests);
        Assert.Empty(orderService.DetailOrderNos);
    }

    [Fact]
    public async Task 익명세션은_개인주문API를호출하지않는다()
    {
        var accessService = new FakeFoodAccessService(true);
        var authenticationService = new FakeAuthenticationService();
        var orderService = new FakeFoodOrderService();
        var page = CreatePage(accessService, authenticationService, orderService);

        await page.초기화Async("FOOD-PRIVATE");

        Assert.True(page.접근.사용가능);
        Assert.True(page.인증.초기화됨);
        Assert.False(page.인증.로그인됨);
        Assert.Equal(1, authenticationService.RestoreCalls);
        Assert.Empty(orderService.ListRequests);
        Assert.Empty(orderService.DetailOrderNos);
    }

    [Fact]
    public async Task 로그인세션복원은_목록과경로의정확한주문을조회한다()
    {
        var accessService = new FakeFoodAccessService(true);
        var authenticationService = new FakeAuthenticationService
        {
            RestoreResult = SignedInResult()
        };
        var orderService = new FakeFoodOrderService
        {
            DetailResponse = new 주문자음식주문상세응답()
        };
        var page = CreatePage(accessService, authenticationService, orderService);

        await page.초기화Async("  FOOD-EXACT-9  ");

        Assert.Single(orderService.ListRequests);
        Assert.Equal(["FOOD-EXACT-9"], orderService.DetailOrderNos);
        Assert.Equal("FOOD-EXACT-9", page.상세.요청OrderNo);
        Assert.NotNull(page.상세.상세);
    }

    [Fact]
    public async Task 경로변경은_같은주문을중복조회하지않고_빈경로에서선택을해제한다()
    {
        var orderService = new FakeFoodOrderService
        {
            DetailResponse = new 주문자음식주문상세응답()
        };
        var page = CreatePage(
            new FakeFoodAccessService(true),
            new FakeAuthenticationService { RestoreResult = SignedInResult() },
            orderService);
        await page.초기화Async("FOOD-1");

        await page.경로선택반영Async("FOOD-1");
        await page.경로선택반영Async("FOOD-2");
        await page.경로선택반영Async(null);

        Assert.Equal(["FOOD-1", "FOOD-2"], orderService.DetailOrderNos);
        Assert.Null(page.상세.요청OrderNo);
        Assert.Null(page.상세.상세);
    }

    [Fact]
    public async Task 명시적로그인뒤_목록과정확한주문을조회하고_로그아웃은개인상태를비운다()
    {
        var authenticationService = new FakeAuthenticationService();
        var orderService = new FakeFoodOrderService
        {
            ListResponse = new 주문자음식주문목록응답
            {
                Items = [new 주문자음식주문요약응답 { 주문번호 = "FOOD-LOGIN" }],
                TotalCount = 1,
                Page = 1,
                PageSize = 12
            },
            DetailResponse = new 주문자음식주문상세응답()
        };
        var page = CreatePage(
            new FakeFoodAccessService(true),
            authenticationService,
            orderService);
        await page.초기화Async("FOOD-LOGIN");

        Assert.True(await page.로그인Async(
            new 공통로그인요청("orderer", "password"),
            "FOOD-LOGIN"));
        Assert.True(page.목록.초기화됨);
        Assert.Equal("FOOD-LOGIN", page.상세.요청OrderNo);

        Assert.True(await page.로그아웃Async());

        Assert.Equal(1, authenticationService.LoginCalls);
        Assert.Equal(1, authenticationService.LogoutCalls);
        Assert.False(page.인증.로그인됨);
        Assert.False(page.목록.초기화됨);
        Assert.Empty(page.목록.주문목록);
        Assert.Null(page.상세.요청OrderNo);
    }

    private static 주문자음식주문PageViewModel CreatePage(
        I음식배달페이지접근Service accessService,
        I주문자앱인증Service authenticationService,
        I주문자음식주문읽기Service orderService)
        => new(
            new 음식배달페이지접근ViewModel(accessService),
            new 주문자앱인증ViewModel(authenticationService),
            new 주문자음식주문목록ViewModel(orderService),
            new 주문자음식주문상세ViewModel(
                orderService,
                (I주문자음식주문수령확인Service)orderService));

    private static 주문자앱인증결과 SignedInResult()
        => new(new 주문자앱세션상태(true, "user-1", "주문자"));

    private sealed class FakeFoodAccessService(bool enabled) : I음식배달페이지접근Service
    {
        public int Calls { get; private set; }

        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(enabled);
        }
    }

    private sealed class FakeAuthenticationService : I주문자앱인증Service
    {
        public 주문자앱인증결과 RestoreResult { get; init; } = new(주문자앱세션상태.익명);
        public 주문자앱인증결과 LoginResult { get; init; } = SignedInResult();
        public int RestoreCalls { get; private set; }
        public int LoginCalls { get; private set; }
        public int LogoutCalls { get; private set; }

        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            return Task.FromResult(RestoreResult);
        }

        public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
        {
            LoginCalls++;
            return Task.FromResult(LoginResult);
        }

        public Task 로그아웃Async(CancellationToken cancellationToken = default)
        {
            LogoutCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFoodOrderService :
        I주문자음식주문읽기Service,
        I주문자음식주문수령확인Service
    {
        public 주문자음식주문목록응답 ListResponse { get; init; } = new();
        public 주문자음식주문상세응답? DetailResponse { get; set; }
        public 주문자음식주문상세응답? DetailAfterReceipt { get; init; }
        public 주문자음식주문목록조회요청? LastRequest { get; private set; }
        public List<주문자음식주문목록조회요청> ListRequests { get; } = [];
        public List<string> DetailOrderNos { get; } = [];
        public string? ReceiptOrderNo { get; private set; }
        public 주문자음식주문수령확인요청? ReceiptRequest { get; private set; }

        public Task<주문자음식주문목록응답> 목록Async(주문자음식주문목록조회요청 request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            ListRequests.Add(request);
            return Task.FromResult(ListResponse);
        }

        public Task<주문자음식주문상세응답?> 상세Async(string orderNo, CancellationToken cancellationToken = default)
        {
            DetailOrderNos.Add(orderNo);
            return Task.FromResult(DetailResponse);
        }

        public Task<음식주문응답> 수령확인Async(
            string orderNo,
            주문자음식주문수령확인요청 request,
            CancellationToken cancellationToken = default)
        {
            ReceiptOrderNo = orderNo;
            ReceiptRequest = request;
            DetailResponse = DetailAfterReceipt ?? DetailResponse;
            return Task.FromResult(new 음식주문응답
            {
                주문번호 = orderNo,
                상태 = 음식주문상태코드.수령확인
            });
        }
    }
}
