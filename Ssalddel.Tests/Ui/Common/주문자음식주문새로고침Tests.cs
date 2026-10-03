using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 주문자음식주문새로고침Tests
{
    [Theory]
    [InlineData(음식배달위치추적상태코드.추적전)]
    [InlineData(음식배달위치추적상태코드.추적중)]
    [InlineData(음식배달위치추적상태코드.갱신지연)]
    [InlineData(음식배달위치추적상태코드.종료)]
    public async Task 기사위치공유와무관하게_배차대기주문은_자동갱신된다(string locationState)
    {
        var service = new FakeOrderService
        {
            Response = Detail(음식주문상태코드.주문확인, locationState)
        };
        var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        service.Response = Detail(음식주문상태코드.기사배정, 음식배달위치추적상태코드.추적전);

        Assert.True(await controller.자동새로고침Async());

        Assert.Equal(2, service.DetailCalls);
        Assert.Equal(2, service.ListCalls);
        Assert.Equal(음식주문상태코드.기사배정, page.상세.상세?.주문.상태);
        Assert.Equal(음식배달위치추적상태코드.추적전, page.상세.상세?.기사위치.상태);
    }

    [Theory]
    [InlineData(음식주문상태코드.전달완료)]
    [InlineData(음식주문상태코드.수령확인)]
    [InlineData(음식주문상태코드.거절)]
    [InlineData(음식주문상태코드.취소)]
    public async Task 종료한주문은_추적중위치가남아도_자동조회하지않는다(string orderState)
    {
        var service = new FakeOrderService { Response = Detail(orderState, 음식배달위치추적상태코드.추적중) };
        var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);

        Assert.False(await controller.자동새로고침Async());
        Assert.Equal(1, service.DetailCalls);
        Assert.Equal(1, service.ListCalls);
    }

    [Theory]
    [InlineData("NoSelection")]
    [InlineData("LoggedOut")]
    [InlineData("NotFound")]
    [InlineData("FeatureDisabled")]
    public async Task 선택없음과로그아웃과접근차단은_자동개인조회하지않는다(string stopReason)
    {
        var service = new FakeOrderService
        {
            Response = stopReason == "NotFound" ? null : Detail(음식주문상태코드.주문대기)
        };
        var page = await CreatePageAsync(service, enabled: stopReason != "FeatureDisabled");
        await using var controller = new 주문자음식주문새로고침Controller(page);
        if (stopReason == "NoSelection")
        {
            page.주문선택해제();
        }
        else if (stopReason == "LoggedOut")
        {
            await page.로그아웃Async();
        }

        var detailCalls = service.DetailCalls;
        var listCalls = service.ListCalls;
        Assert.False(await controller.자동새로고침Async());
        Assert.Equal(detailCalls, service.DetailCalls);
        Assert.Equal(listCalls, service.ListCalls);
    }

    [Fact]
    public async Task 일시적조회실패로상세가비어도_다음자동갱신에서같은주문을다시조회한다()
    {
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문확인) };
        var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        service.ReadDetail = (_, _) => throw new HttpRequestException("일시적인 연결 실패");

        Assert.True(await controller.자동새로고침Async());
        Assert.True(page.상세.오류발생);
        Assert.Null(page.상세.상세);

        service.ReadDetail = null;
        service.Response = Detail(음식주문상태코드.조리중);
        Assert.True(await controller.자동새로고침Async());
        Assert.Equal(음식주문상태코드.조리중, page.상세.상세?.주문.상태);
        Assert.Equal(["FOOD-1", "FOOD-1", "FOOD-1"], service.OrderNos);
    }

    [Fact]
    public async Task 자동조회가진행중이면_다음tick은건너뛰고_수동선택은조회뒤에직렬실행한다()
    {
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문확인) };
        var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ReadDetail = async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return service.Response;
        };

        var firstRefresh = controller.자동새로고침Async();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await controller.자동새로고침Async());
        var selection = controller.작업실행Async(token => page.주문선택Async("FOOD-2", token));
        Assert.False(selection.IsCompleted);
        Assert.Equal("FOOD-1", page.상세.요청OrderNo);

        release.TrySetResult();
        await Task.WhenAll(firstRefresh, selection).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, service.MaxConcurrentDetailCalls);
        Assert.Equal(["FOOD-1", "FOOD-1", "FOOD-2"], service.OrderNos);
        Assert.Equal("FOOD-2", page.상세.요청OrderNo);
    }

    [Fact]
    public async Task 수동상세조회중에는_자동갱신이상세선택을초기화하지않는다()
    {
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문대기) };
        var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ReadDetail = async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return service.Response;
        };

        var manual = page.주문선택Async("FOOD-2");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await controller.자동새로고침Async());
        Assert.Equal("FOOD-2", page.상세.요청OrderNo);
        Assert.Equal(2, service.DetailCalls);
        Assert.Equal(1, service.ListCalls);

        release.TrySetResult();
        await manual.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Dispose는_진행중조회와timer를취소하고_이후API호출을차단한다()
    {
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문확인) };
        var page = await CreatePageAsync(service);
        var controller = new 주문자음식주문새로고침Controller(page);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        service.ReadDetail = async (_, token) =>
        {
            requestToken = token;
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return service.Response;
        };
        controller.시작(action => action());
        var pending = controller.자동새로고침Async();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await controller.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await pending);
        Assert.True(requestToken.IsCancellationRequested);
        Assert.True(controller.중지됨);
        var detailCalls = service.DetailCalls;
        Assert.False(await controller.자동새로고침Async());
        await controller.작업실행Async(token => page.주문선택Async("FOOD-2", token));
        await controller.DisposeAsync();
        Assert.Equal(detailCalls, service.DetailCalls);
    }

    [Fact]
    public async Task 목록조회후로그아웃은_늦은응답을비우고_대기중인이전목록요청은조회하지않는다()
    {
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문확인) };
        var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ReadList = async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return new 주문자음식주문목록응답
            {
                Items = [new 주문자음식주문요약응답 { 주문번호 = "FOOD-PRIVATE" }],
                TotalCount = 1
            };
        };

        var search = controller.목록작업실행Async(token => page.목록검색Async(token));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var logout = controller.작업실행Async(token => page.로그아웃Async(token));
        var previousPageRequest = controller.목록작업실행Async(token => page.페이지변경Async(2, token));
        Assert.False(logout.IsCompleted);
        Assert.False(previousPageRequest.IsCompleted);
        Assert.True(page.인증.로그인됨);

        release.TrySetResult();
        await Task.WhenAll(search, logout, previousPageRequest).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(page.인증.로그인됨);
        Assert.Empty(page.목록.주문목록);
        Assert.False(page.목록.초기화됨);
        Assert.Equal(2, service.ListCalls);
    }

    [Fact]
    public async Task Dispose는_목록HTTP와대기중페이지요청을취소하여_추가목록조회를막는다()
    {
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문확인) };
        var page = await CreatePageAsync(service);
        var controller = new 주문자음식주문새로고침Controller(page);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        service.ReadList = async (_, token) =>
        {
            requestToken = token;
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new 주문자음식주문목록응답();
        };

        var search = controller.목록작업실행Async(token => page.목록검색Async(token));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var nextPage = controller.목록작업실행Async(token => page.페이지변경Async(2, token));
        Assert.False(nextPage.IsCompleted);

        await controller.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(search, nextPage).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(requestToken.IsCancellationRequested);
        Assert.True(page.목록.취소됨);
        Assert.Equal(2, service.ListCalls);
        await controller.목록작업실행Async(token => page.목록새로고침Async(token));
        Assert.Equal(2, service.ListCalls);
    }

    [Fact]
    public async Task 로그아웃뒤대기중인_상세선택과수령과진행조회는_개인API를호출하지않는다()
    {
        var service = new FakeOrderService { Response = ReceiptReadyDetail() };
        var page = await CreatePageAsync(service);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ReadDetail = async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return service.Response;
        };

        var first = controller.작업실행Async(token => page.주문선택Async("FOOD-1", token));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var logout = controller.작업실행Async(token => page.로그아웃Async(token));
        var selection = controller.작업실행Async(token => page.주문선택Async("FOOD-2", token));
        var receipt = controller.작업실행Async(token => page.주문수령확인Async(token));
        var refresh = controller.작업실행Async(page.주문진행새로고침Async);
        Assert.False(selection.IsCompleted);
        Assert.False(receipt.IsCompleted);

        release.TrySetResult();
        await Task.WhenAll(first, logout, selection, receipt, refresh).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(page.인증.로그인됨);
        Assert.False(page.개인주문조회가능);
        Assert.Null(page.상세.요청OrderNo);
        Assert.Equal(["FOOD-1", "FOOD-1"], service.OrderNos);
        Assert.Equal(1, service.ListCalls);
        Assert.Equal(0, service.ReceiptCalls);
    }

    [Theory]
    [InlineData("Detail")]
    [InlineData("List")]
    [InlineData("Receipt")]
    public async Task 최종401은_개인상태와자동조회를비우고_재로그인뒤같은주문을복원한다(string failureSource)
    {
        var authentication = new FakeAuthenticationService();
        var service = new FakeOrderService
        {
            Response = failureSource == "Receipt" ? ReceiptReadyDetail() : Detail(음식주문상태코드.주문확인),
            ReadList = (_, _) => Task.FromResult(new 주문자음식주문목록응답
            {
                Items = [new 주문자음식주문요약응답 { 주문번호 = "FOOD-1" }],
                TotalCount = 1
            })
        };
        var page = await CreatePageAsync(service, authentication: authentication);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        Assert.Single(page.목록.주문목록);
        page.상세.수령확인메모 = "이전 입력";

        if (failureSource == "List")
        {
            service.ReadList = (_, _) => throw ApiFailure(401);
        }
        else if (failureSource == "Receipt")
        {
            service.ConfirmReceipt = (_, _, _) => throw ApiFailure(401);
        }
        else
        {
            service.ReadDetail = (_, _) => throw ApiFailure(401);
        }

        if (failureSource == "Receipt")
        {
            await controller.작업실행Async(token => page.주문수령확인Async(token));
        }
        else
        {
            Assert.True(await controller.자동새로고침Async());
        }

        Assert.True(page.재로그인필요);
        Assert.False(page.개인주문조회가능);
        Assert.False(page.인증.로그인됨);
        Assert.Equal(1, authentication.LogoutCalls);
        Assert.Empty(page.목록.주문목록);
        Assert.Null(page.상세.요청OrderNo);
        Assert.Null(page.상세.상세);
        Assert.Empty(page.상세.수령확인메모);
        var detailCalls = service.DetailCalls;
        var listCalls = service.ListCalls;
        Assert.False(await controller.자동새로고침Async());
        await controller.목록작업실행Async(token => page.목록검색Async(token));
        await controller.작업실행Async(token => page.주문선택Async("FOOD-OLD", token));
        Assert.Equal(detailCalls, service.DetailCalls);
        Assert.Equal(listCalls, service.ListCalls);

        service.ReadDetail = null;
        service.ReadList = null;
        await controller.작업실행Async(token => page.로그인Async(
            new 공통로그인요청("orderer", "test-password"), "FOOD-1", token));

        Assert.False(page.재로그인필요);
        Assert.True(page.개인주문조회가능);
        Assert.True(page.인증.로그인됨);
        Assert.Equal("FOOD-1", page.상세.요청OrderNo);
        Assert.NotNull(page.상세.상세);
        Assert.Equal(detailCalls + 1, service.DetailCalls);
        Assert.Equal(listCalls + 1, service.ListCalls);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(500)]
    public async Task 권한또는서버실패를_로그인만료로바꾸지않고_연결복구후재조회한다(int statusCode)
    {
        var authentication = new FakeAuthenticationService();
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문확인) };
        var page = await CreatePageAsync(service, authentication: authentication);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        service.ReadDetail = (_, _) => throw ApiFailure(statusCode);

        Assert.True(await controller.자동새로고침Async());
        Assert.True(page.상세.오류발생);
        Assert.False(page.재로그인필요);
        Assert.True(page.인증.로그인됨);
        Assert.Equal(0, authentication.LogoutCalls);

        service.ReadDetail = null;
        Assert.True(await controller.자동새로고침Async());
        Assert.Equal("FOOD-1", page.상세.요청OrderNo);
        Assert.NotNull(page.상세.상세);
    }

    [Fact]
    public async Task 만료뒤토큰저장소정리가실패해도_개인정보와대기조회는복원되지않는다()
    {
        var authentication = new FakeAuthenticationService { LogoutFailure = new IOException("저장소 정리 실패") };
        var service = new FakeOrderService { Response = Detail(음식주문상태코드.주문확인) };
        var page = await CreatePageAsync(service, authentication: authentication);
        await using var controller = new 주문자음식주문새로고침Controller(page);
        service.ReadDetail = (_, _) => throw ApiFailure(401);

        Assert.True(await controller.자동새로고침Async());

        Assert.True(page.재로그인필요);
        Assert.True(page.인증.오류발생);
        Assert.False(page.개인주문조회가능);
        Assert.Empty(page.목록.주문목록);
        Assert.Null(page.상세.요청OrderNo);
        Assert.Null(page.상세.상세);
        var calls = service.DetailCalls;
        Assert.False(await controller.자동새로고침Async());
        await controller.작업실행Async(token => page.주문선택Async("FOOD-PRIVATE", token));
        await controller.목록작업실행Async(token => page.목록검색Async(token));
        Assert.Equal(calls, service.DetailCalls);
        Assert.Equal(2, service.ListCalls);
    }

    private static SsalddelApiException ApiFailure(int statusCode)
        => new("API 실패", statusCode, "음식 주문 확인", string.Empty, null);

    private static 주문자음식주문상세응답 ReceiptReadyDetail()
        => new()
        {
            주문 = new 주문자음식주문요약응답 { 주문번호 = "FOOD-1", 상태 = 음식주문상태코드.전달완료 },
            AvailableActions = [new 업무가능행동Dto { ActionId = 음식배달가능행동Ids.주문수령확인 }]
        };

    private static 주문자음식주문상세응답 Detail(string state, string locationState = 음식배달위치추적상태코드.추적전)
        => new()
        {
            주문 = new 주문자음식주문요약응답 { 주문번호 = "FOOD-1", 상태 = state },
            기사위치 = new 주문자음식배달위치추적응답 { 상태 = locationState }
        };

    private static async Task<주문자음식주문PageViewModel> CreatePageAsync(
        FakeOrderService service,
        bool enabled = true,
        FakeAuthenticationService? authentication = null)
    {
        var page = new 주문자음식주문PageViewModel(
            new 음식배달페이지접근ViewModel(new FakeAccessService(enabled)),
            new 주문자앱인증ViewModel(authentication ?? new FakeAuthenticationService()),
            new 주문자음식주문목록ViewModel(service),
            new 주문자음식주문상세ViewModel(service, service));
        await page.초기화Async("FOOD-1");
        return page;
    }

    private sealed class FakeAccessService(bool enabled) : I음식배달페이지접근Service
    {
        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
            => Task.FromResult(enabled);
    }

    private sealed class FakeAuthenticationService : I주문자앱인증Service
    {
        public int LogoutCalls { get; private set; }
        public Exception? LogoutFailure { get; init; }

        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
            => Task.FromResult(new 주문자앱인증결과(new 주문자앱세션상태(true, "user-1", "주문자")));

        public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
            => 복원Async(cancellationToken);

        public Task 로그아웃Async(CancellationToken cancellationToken = default)
        {
            LogoutCalls++;
            return LogoutFailure is null ? Task.CompletedTask : Task.FromException(LogoutFailure);
        }
    }

    private sealed class FakeOrderService : I주문자음식주문읽기Service, I주문자음식주문수령확인Service
    {
        private int _activeDetailCalls;
        public int DetailCalls { get; private set; }
        public int ListCalls { get; private set; }
        public int ReceiptCalls { get; private set; }
        public int MaxConcurrentDetailCalls { get; private set; }
        public List<string> OrderNos { get; } = [];
        public 주문자음식주문상세응답? Response { get; set; }
        public Func<string, CancellationToken, Task<주문자음식주문상세응답?>>? ReadDetail { get; set; }
        public Func<주문자음식주문목록조회요청, CancellationToken, Task<주문자음식주문목록응답>>? ReadList { get; set; }
        public Func<string, 주문자음식주문수령확인요청, CancellationToken, Task<음식주문응답>>? ConfirmReceipt { get; set; }

        public Task<주문자음식주문목록응답> 목록Async(주문자음식주문목록조회요청 request, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return ReadList is null
                ? Task.FromResult(new 주문자음식주문목록응답())
                : ReadList(request, cancellationToken);
        }

        public async Task<주문자음식주문상세응답?> 상세Async(string orderNo, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            OrderNos.Add(orderNo);
            var active = Interlocked.Increment(ref _activeDetailCalls);
            MaxConcurrentDetailCalls = Math.Max(MaxConcurrentDetailCalls, active);
            try
            {
                return ReadDetail is null ? Response : await ReadDetail(orderNo, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeDetailCalls);
            }
        }

        public Task<음식주문응답> 수령확인Async(string orderNo, 주문자음식주문수령확인요청 request, CancellationToken cancellationToken = default)
        {
            ReceiptCalls++;
            return ConfirmReceipt is null
                ? throw new NotSupportedException()
                : ConfirmReceipt(orderNo, request, cancellationToken);
        }
    }
}
