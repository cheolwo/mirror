using System.Net;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 주문자음식주문취소ViewModelTests
{
    [Theory]
    [InlineData("NoAction")]
    [InlineData("NoRevision")]
    [InlineData("WrongRevisionKind")]
    [InlineData("DifferentOrder")]
    public void 현재주문의서버취소행동과판본이있어야_취소검토를연다(string missing)
    {
        var service = new FoodService();
        using var vm = Create(service);
        var detail = Ready();
        if (missing == "NoAction") detail.AvailableActions = [];
        if (missing == "NoRevision") detail.AvailableActions[0].ExpectedRevision = null;
        if (missing == "WrongRevisionKind") detail.AvailableActions[0].RevisionKindCode = 업무Revision종류Codes.음식배달시도;
        if (missing == "DifferentOrder") detail.주문.주문번호 = "FOOD-OTHER";
        vm.문맥반영("FOOD-A", "user-a", detail);
        vm.작성시작();
        Assert.False(vm.취소가능);
        Assert.False(vm.작성열림);
        Assert.Empty(service.Requests);
    }

    [Theory]
    [InlineData("NoReason")]
    [InlineData("UnknownReason")]
    [InlineData("OtherEmpty")]
    [InlineData("LongReason")]
    [InlineData("NotConfirmed")]
    public async Task 사유와명시검토가없으면_취소를전송하지않는다(string missing)
    {
        var service = new FoodService();
        using var vm = Create(service);
        Review(vm);
        if (missing == "NoReason") vm.사유Code = string.Empty;
        if (missing == "UnknownReason") vm.사유Code = "Unknown";
        if (missing == "OtherEmpty") { vm.사유Code = 운영배차주문자취소사유Code.기타; vm.사유 = " "; }
        if (missing == "LongReason") vm.사유 = new string('가', 501);
        vm.검토확인 = missing != "NotConfirmed";
        Assert.False(await vm.취소Async());
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task 취소는_동일주문판본사유와새멱등키를보내고_정본을다시읽는다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => Task.FromResult(Cancelled()) };
        using var vm = Create(service);
        Review(vm);
        service.OnCancel = (_, _, _) => { service.Detail = CancelledDetail(); return Task.FromResult(Cancelled()); };
        Assert.True(await vm.취소Async());
        var sent = Assert.Single(service.Requests);
        Assert.Equal("FOOD-A", sent.OrderNo);
        Assert.Equal(7, sent.Request.예상Revision);
        Assert.Equal(운영배차주문자취소사유Code.중복주문, sent.Request.사유Code);
        Assert.Equal("같은 주문을 두 번 넣었습니다.", sent.Request.사유);
        Assert.NotEqual(Guid.Empty, sent.Request.클라이언트요청Id);
        Assert.Equal(["FOOD-A"], service.DetailOrders);
        Assert.Equal(음식주문상태코드.취소, vm.확인한정본?.주문.상태);
        Assert.False(vm.작성열림);
        Assert.Null(vm.현재시도요청Id);
    }

    [Fact]
    public async Task 응답유실이지만정본이취소이면_재전송없이결과를확정한다()
    {
        var service = new FoodService();
        service.OnCancel = (_, _, _) => { service.Detail = CancelledDetail(); throw new HttpRequestException("응답 유실"); };
        using var vm = Create(service);
        Review(vm);
        Assert.True(await vm.취소Async());
        Assert.Single(service.Requests);
        Assert.Null(vm.오류);
        Assert.Contains("취소를 확인", vm.안내);
    }

    [Fact]
    public async Task 결과미확인재시도는_같은키판본사유를유지하고입력과중복전송을잠근다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => throw new HttpRequestException("응답 유실") };
        using var vm = Create(service);
        Review(vm);
        Assert.False(await vm.취소Async());
        var first = Assert.Single(service.Requests).Request;
        vm.사유 = "재시도 도중 바꾼 사유";
        vm.사유Code = 운영배차주문자취소사유Code.기타;
        vm.작성닫기();
        vm.작성시작();
        vm.문맥반영("FOOD-A", "user-a", Ready(revision: 9));
        Assert.False(await vm.취소Async());
        var retry = service.Requests[1].Request;
        Assert.Equal(first.클라이언트요청Id, retry.클라이언트요청Id);
        Assert.Equal(first.예상Revision, retry.예상Revision);
        Assert.Equal(first.사유Code, retry.사유Code);
        Assert.Equal(first.사유, retry.사유);
        Assert.True(vm.입력잠김);
    }

    [Fact]
    public async Task 경합409는_최신정본을읽고_자동재시도없이다시검토한다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => throw HttpFailure(HttpStatusCode.Conflict) };
        using var vm = Create(service);
        Review(vm);
        service.Detail = Ready(revision: 9);
        Assert.False(await vm.취소Async());
        var firstId = Assert.Single(service.Requests).Request.클라이언트요청Id;
        Assert.False(vm.검토확인);
        Assert.Null(vm.현재시도요청Id);
        Assert.False(vm.제출가능);
        Assert.Equal(409, vm.오류?.Http상태코드);
        vm.검토확인 = true;
        Assert.False(await vm.취소Async());
        Assert.NotEqual(firstId, service.Requests[1].Request.클라이언트요청Id);
        Assert.Equal(9, service.Requests[1].Request.예상Revision);
    }

    [Fact]
    public async Task 서버시간초과는_화면이탈로삼키지않고_미확인취소키를보존한다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => throw new TaskCanceledException("서버 응답 시간 초과") };
        using var vm = Create(service);
        Review(vm);
        Assert.False(await vm.취소Async());
        Assert.NotNull(vm.현재시도요청Id);
        Assert.Equal("timeout", vm.오류?.코드);
        Assert.Single(service.DetailOrders);
    }

    [Fact]
    public async Task 충돌409뒤이미수락한정본에는_취소행동을열지않는다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => throw HttpFailure(HttpStatusCode.Conflict) };
        using var vm = Create(service);
        Review(vm);
        service.Detail = new() { 주문 = new() { 주문번호 = "FOOD-A", 상태 = 음식주문상태코드.주문확인 } };
        Assert.False(await vm.취소Async());
        Assert.False(vm.취소가능);
        vm.검토확인 = true;
        Assert.False(await vm.취소Async());
        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task Post성공이어도정본조회실패는_취소완료로표시하지않는다()
    {
        var service = new FoodService { OnRead = (_, _) => throw new IOException("정본 확인 불가") };
        using var vm = Create(service);
        Review(vm);
        Assert.False(await vm.취소Async());
        Assert.NotNull(vm.현재시도요청Id);
        Assert.NotNull(vm.오류);
        Assert.Null(vm.확인한정본);
        Assert.Contains("확인하지 못했습니다", vm.안내);
    }

    [Theory]
    [InlineData("DifferentOrder")]
    [InlineData("DifferentUser")]
    [InlineData("Logout")]
    [InlineData("Dispose")]
    public async Task 이탈계정변경과늦은응답은_다음문맥에취소결과를남기지않는다(string change)
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FoodService { OnCancel = (_, _, _) => completion.Task };
        var vm = Create(service);
        Review(vm);
        var pending = vm.취소Async();
        Assert.True(vm.처리중);
        Assert.False(await vm.취소Async());
        Assert.Single(service.Requests);
        if (change == "DifferentOrder") vm.문맥반영("FOOD-B", "user-a", Ready("FOOD-B"));
        if (change == "DifferentUser") vm.문맥반영("FOOD-A", "user-b", Ready());
        if (change == "Logout") vm.세션초기화();
        if (change == "Dispose") vm.Dispose();
        completion.SetResult(Cancelled());
        Assert.False(await pending);
        Assert.Empty(service.DetailOrders);
        Assert.Null(vm.확인한정본);
        Assert.Null(vm.현재시도요청Id);
        Assert.Null(vm.안내);
        Assert.Empty(vm.사유);
        vm.Dispose();
    }

    [Fact]
    public async Task 최종401뒤같은계정은_동일취소시도를보존하고로그인으로자동전송하지않는다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => throw HttpFailure(HttpStatusCode.Unauthorized) };
        var auth = new AuthenticationService();
        using var page = await CreatePageAsync(service, auth);
        Review(page.취소!);
        Assert.False(await page.주문취소Async());
        var requestId = Assert.Single(service.Requests).Request.클라이언트요청Id;
        await page.인증오류복구Async();
        Assert.True(page.인증화면표시);
        Assert.Null(page.상세.상세);
        Assert.False(page.취소!.작성열림);
        Assert.True(await page.로그인Async(new("user-a", "test-password"), "FOOD-A"));
        Assert.False(page.인증화면표시);
        Assert.Equal("FOOD-A", page.상세.요청OrderNo);
        Assert.Equal(requestId, page.취소.현재시도요청Id);
        Assert.Single(service.Requests);
        Assert.Null(page.취소.오류);
        Assert.False(await page.주문취소Async());
        Assert.Equal(requestId, service.Requests[1].Request.클라이언트요청Id);
    }

    [Fact]
    public async Task 최종401뒤다른계정은_전계정의취소초안과멱등키를전달하지않는다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => throw HttpFailure(HttpStatusCode.Unauthorized) };
        var auth = new AuthenticationService();
        using var page = await CreatePageAsync(service, auth);
        Review(page.취소!);
        Assert.False(await page.주문취소Async());
        await page.인증오류복구Async();
        auth.UserId = "user-b";
        Assert.True(await page.로그인Async(new("user-b", "test-password"), "FOOD-A"));
        Assert.Null(page.취소!.현재시도요청Id);
        Assert.False(page.취소.작성열림);
        Assert.Empty(page.취소.사유);
        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task 로그인복귀의정본이이미취소면_재전송없이취소확인을표시한다()
    {
        var service = new FoodService { OnCancel = (_, _, _) => throw HttpFailure(HttpStatusCode.Unauthorized) };
        using var page = await CreatePageAsync(service, new AuthenticationService());
        Review(page.취소!);
        Assert.False(await page.주문취소Async());
        await page.인증오류복구Async();
        service.Detail = CancelledDetail();
        Assert.True(await page.로그인Async(new("user-a", "test-password"), "FOOD-A"));
        Assert.Single(service.Requests);
        Assert.Null(page.취소!.현재시도요청Id);
        Assert.Equal("주문 취소를 확인했습니다.", page.취소.안내);
    }

    [Fact]
    public async Task 취소후같은목록과선택주문을다시읽고_정본상태를표시한다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service, new AuthenticationService());
        Review(page.취소!);
        service.OnCancel = (_, _, _) => { service.Detail = CancelledDetail(); return Task.FromResult(Cancelled()); };
        Assert.True(await page.주문취소Async());
        Assert.Equal(음식주문상태코드.취소, page.상세.상세?.주문.상태);
        Assert.Equal(음식주문상태코드.취소, Assert.Single(page.목록.주문목록).상태);
        Assert.All(service.DetailOrders, order => Assert.Equal("FOOD-A", order));
    }

    private static 주문자음식주문취소ViewModel Create(FoodService service)
    {
        var vm = new 주문자음식주문취소ViewModel(service, service);
        vm.문맥반영("FOOD-A", "user-a", Ready());
        return vm;
    }

    internal static void Review(주문자음식주문취소ViewModel vm)
    {
        vm.작성시작();
        vm.사유Code = 운영배차주문자취소사유Code.중복주문;
        vm.사유 = "같은 주문을 두 번 넣었습니다.";
        vm.검토확인 = true;
    }

    internal static 주문자음식주문상세응답 Ready(string orderNo = "FOOD-A", long revision = 7)
        => new()
        {
            주문 = new() { 주문번호 = orderNo, 상태 = 음식주문상태코드.주문대기 },
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.주문취소,
                RevisionKindCode = 업무Revision종류Codes.음식주문, ExpectedRevision = revision }]
        };
    private static 주문자음식주문상세응답 CancelledDetail()
        => new() { 주문 = new() { 주문번호 = "FOOD-A", 상태 = 음식주문상태코드.취소 } };
    private static 음식주문응답 Cancelled()
        => new() { 주문번호 = "FOOD-A", 상태 = 음식주문상태코드.취소 };
    private static HttpRequestException HttpFailure(HttpStatusCode status)
        => new("합성 HTTP 실패", null, status);

    internal static async Task<주문자음식주문PageViewModel> CreatePageAsync(FoodService service, AuthenticationService auth)
    {
        var page = new 주문자음식주문PageViewModel(new(new AccessService()), new(auth), new(service),
            new(service, service), new(service, service));
        await page.초기화Async("FOOD-A");
        return page;
    }

    internal sealed class FoodService : I주문자음식주문읽기Service,
        I주문자음식주문취소Service, I주문자음식주문수령확인Service
    {
        public 주문자음식주문상세응답? Detail { get; set; } = Ready();
        public Func<string, CancellationToken, Task<주문자음식주문상세응답?>>? OnRead { get; set; }
        public Func<string, 주문자음식주문취소요청, CancellationToken, Task<음식주문응답>>? OnCancel { get; set; }
        public List<(string OrderNo, 주문자음식주문취소요청 Request)> Requests { get; } = [];
        public List<string> DetailOrders { get; } = [];
        public Task<주문자음식주문목록응답> 목록Async(주문자음식주문목록조회요청 request, CancellationToken cancellationToken = default)
            => Task.FromResult(new 주문자음식주문목록응답 { Items = Detail is null ? [] : [Detail.주문], TotalCount = Detail is null ? 0 : 1 });
        public Task<주문자음식주문상세응답?> 상세Async(string orderNo, CancellationToken cancellationToken = default)
        {
            DetailOrders.Add(orderNo);
            return OnRead?.Invoke(orderNo, cancellationToken) ?? Task.FromResult(Detail);
        }
        public Task<음식주문응답> 취소Async(string orderNo, 주문자음식주문취소요청 request, CancellationToken cancellationToken = default)
        {
            Requests.Add((orderNo, request));
            return OnCancel?.Invoke(orderNo, request, cancellationToken) ?? Task.FromResult(Cancelled());
        }
        public Task<음식주문응답> 수령확인Async(string orderNo, 주문자음식주문수령확인요청 request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class AccessService : I음식배달페이지접근Service
    {
        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    internal sealed class AuthenticationService : I주문자앱인증Service
    {
        public string UserId { get; set; } = "user-a";
        public bool SignedIn { get; init; } = true;
        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
            => Task.FromResult(new 주문자앱인증결과(SignedIn ? new(true, UserId, "합성 주문자") : 주문자앱세션상태.익명));
        public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(new 주문자앱인증결과(new(true, UserId, "합성 주문자")));
        public Task 로그아웃Async(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
