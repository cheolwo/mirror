using System.Net;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Clients;

public sealed class AdminFoodOperationsStateTests
{
    [Fact]
    public async Task 로그인전에는_음식업무를조회하지않는다()
    {
        var client = new Client();
        using var state = new AdminFoodOperationsState(client, () => string.Empty);
        await state.RefreshListAsync("");
        await state.RefreshTraceAsync("FOOD-ONE");
        Assert.Equal(0, client.ReadCount);
        Assert.Null(state.List); Assert.Null(state.Trace); Assert.True(state.RequiresLogin);
    }

    [Fact]
    public async Task 음식목록과선택상세는_읽기만수행하고_중단검토를자동실행하지않는다()
    {
        var client = new Client();
        using var state = new AdminFoodOperationsState(client, () => "admin");
        await state.RefreshListAsync(" 식당 ");
        Assert.Equal("식당", client.LastQuery); Assert.False(state.RequiresRefresh);
        await state.RefreshTraceAsync("FOOD-ONE");
        Assert.True(state.CanReview); Assert.Equal(0, client.ReviewCount);
    }

    [Fact]
    public async Task 동일주문의_조회실패는_이전내용을유지하되_검토진입을막는다()
    {
        var client = new Client();
        using var state = new AdminFoodOperationsState(client, () => "admin");
        await state.RefreshTraceAsync("FOOD-ONE");
        client.TraceRead = (_, _) => throw new HttpRequestException("offline");
        await state.RefreshTraceAsync("FOOD-ONE");
        Assert.NotNull(state.Trace); Assert.True(state.RequiresRefresh); Assert.False(state.CanReview);
        Assert.Contains("다시 조회", state.Message);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 인증권한실패는_캐시업무를숨기고_로그인으로복귀한다(int status)
    {
        var client = new Client();
        using var state = new AdminFoodOperationsState(client, () => "admin");
        await state.RefreshTraceAsync("FOOD-ONE");
        client.TraceRead = (_, _) => throw new HttpRequestException("denied", null, (HttpStatusCode)status);
        await state.RefreshTraceAsync("FOOD-ONE");
        Assert.True(state.RequiresLogin); Assert.Null(state.Trace); Assert.False(state.CanReview);
    }

    [Fact]
    public async Task 음식기능비활성은_주문미존재와구분한다()
    {
        var client = new Client { TraceRead = (_, _) => throw new AdminFoodWorkflowUnavailableException() };
        using var state = new AdminFoodOperationsState(client, () => "admin");
        await state.RefreshTraceAsync("FOOD-ONE");
        Assert.True(state.Unavailable); Assert.True(state.RequiresRefresh); Assert.Null(state.Trace);
        Assert.DoesNotContain("찾지 못", state.Message);
    }

    [Fact]
    public async Task 다른주문응답을_현재주문상세로표시하지않는다()
    {
        var client = new Client { TraceRead = (_, _) => Task.FromResult<음식주문운영추적응답?>(Trace("FOOD-OTHER")) };
        using var state = new AdminFoodOperationsState(client, () => "admin");
        await state.RefreshTraceAsync("FOOD-ONE");
        Assert.Null(state.Trace); Assert.True(state.RequiresRefresh); Assert.False(state.CanReview);
    }

    [Fact]
    public async Task 페이지가바뀐뒤_이전검색의늦은응답은_새목록을덮지않는다()
    {
        var late = new TaskCompletionSource<AdminFoodOrderListDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { ListRead = (_, page, _) => page == 1 ? late.Task : Task.FromResult(List("NEW", page)) };
        using var state = new AdminFoodOperationsState(client, () => "admin");
        var old = state.RefreshListAsync("", 1);
        await state.RefreshListAsync("", 2);
        late.SetResult(List("OLD", 1)); await old;
        Assert.Equal(2, state.Page); Assert.Equal("NEW", Assert.Single(state.List!.Items).OrderNo);
        Assert.False(state.IsBusy);
    }

    [Fact]
    public async Task 계정변경뒤_이전계정응답은_표시되지않는다()
    {
        var actor = "first";
        var late = new TaskCompletionSource<음식주문운영추적응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { TraceRead = (_, _) => late.Task };
        using var state = new AdminFoodOperationsState(client, () => actor);
        var old = state.RefreshTraceAsync("FOOD-ONE");
        actor = "second"; state.SynchronizeAuthentication();
        late.SetResult(Trace("FOOD-ONE")); await old;
        Assert.Null(state.Trace); Assert.False(state.CanReview); Assert.False(state.IsBusy);
    }

    [Fact]
    public async Task 페이지폐기뒤_완료된응답을저장하지않는다()
    {
        var late = new TaskCompletionSource<AdminFoodOrderListDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { ListRead = (_, _, _) => late.Task };
        var state = new AdminFoodOperationsState(client, () => "admin");
        var read = state.RefreshListAsync("");
        state.Dispose(); late.SetResult(List("LATE", 1)); await read;
        Assert.Null(state.List); Assert.False(state.AuthenticationCurrent);
    }

    [Theory]
    [InlineData(음식배달운영생명주기단계Codes.기사확보대기, "기사 배정 대기")]
    [InlineData(음식배달운영생명주기단계Codes.음식점응답대기, "음식점 응답 대기")]
    [InlineData(음식배달운영생명주기단계Codes.조리배차병행, "조리·픽업 준비")]
    [InlineData(음식배달운영생명주기단계Codes.픽업인계, "픽업")]
    [InlineData(음식배달운영생명주기단계Codes.배송, "전달 중")]
    [InlineData(음식배달운영생명주기단계Codes.수령확인대기, "수령 확인 대기")]
    [InlineData(음식배달운영생명주기단계Codes.종료, "종료")]
    public void 서버업무단계를_사용자말로표현한다(string code, string expected)
        => Assert.Equal(expected, AdminFoodOperationsState.StageText(code));

    private static 음식주문운영추적응답 Trace(string order) => new()
    {
        주문번호 = order,
        배달시도목록 = [new() { 시도StableId = "attempt", 상태Code = 음식배달시도상태Code.중단, Revision = 2 }]
    };
    private static AdminFoodOrderListDto List(string order, int page) => new()
    { Page = page, TotalCount = 30, Items = [new() { OrderNo = order, RestaurantName = "식당" }] };
    private sealed class Client : IAdminFoodOperationsClient
    {
        public int ReadCount, ReviewCount;
        public string LastQuery = "";
        public Func<string, int, CancellationToken, Task<AdminFoodOrderListDto>>? ListRead;
        public Func<string, CancellationToken, Task<음식주문운영추적응답?>>? TraceRead;
        public Task<AdminFoodOrderListDto> ListAsync(string query, int page, CancellationToken token = default)
        { ReadCount++; LastQuery = query; return ListRead?.Invoke(query, page, token) ?? Task.FromResult(List("FOOD-ONE", page)); }
        public Task<음식주문운영추적응답?> 조회Async(string order, CancellationToken token = default)
        { ReadCount++; return TraceRead?.Invoke(order, token) ?? Task.FromResult<음식주문운영추적응답?>(Trace(order)); }
        public Task<음식배달시도운영응답> 중단검토Async(string attempt, 음식배달중단검토요청 body, CancellationToken token = default)
        { ReviewCount++; throw new InvalidOperationException("읽기에서 검토 요청을 보내면 안 됩니다."); }
    }
}
