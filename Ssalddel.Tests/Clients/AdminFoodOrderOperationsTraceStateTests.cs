using System.Net;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class AdminFoodOrderOperationsTraceStateTests
{
    [Fact]
    public async Task 추적조회는_선택주문과미존재를표시하고_성공시점에만지급입력을복원한다()
    {
        음식주문운영추적응답? response = Trace("FOOD-ONE");
        var state = new 음식주문운영추적ReadSession((_, _) => Task.FromResult(response));
        state.ChangeOrderNo(" FOOD-ONE ");
        var loaded = 0;
        await Load(state, onLoaded: value => { Assert.True(state.IsLoading); Assert.Same(response, value); loaded++; });
        Assert.Same(response, state.Trace); Assert.False(state.NotFound); Assert.False(state.IsLoading);
        Assert.Equal(" FOOD-ONE ", state.OrderNo); Assert.Equal(1, loaded);

        response = null;
        await Load(state);
        Assert.Null(state.Trace); Assert.True(state.NotFound); Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public async Task 다른주문응답은_선택주문으로적용하거나_지급입력에전파하지않는다()
    {
        var state = new 음식주문운영추적ReadSession((_, _) => Task.FromResult<음식주문운영추적응답?>(Trace("OTHER")));
        state.ChangeOrderNo("FOOD-ONE");
        var loaded = false;
        await Load(state, onLoaded: _ => loaded = true);
        Assert.Null(state.Trace); Assert.False(state.NotFound); Assert.False(loaded);
        Assert.Equal("운영 추적 정보를 불러오지 못했습니다: 조회한 주문번호가 요청과 다릅니다.", state.ErrorMessage);
    }

    [Theory]
    [InlineData(401, true, "로그인이 만료되었습니다. 다시 로그인해 주세요.")]
    [InlineData(403, false, "운영 추적 정보를 불러오지 못했습니다: denied")]
    public async Task 추적인증실패는_기존401복귀와기타조회오류를유지한다(int status, bool login, string message)
    {
        var state = new 음식주문운영추적ReadSession((_, _) => throw new HttpRequestException("denied", null, (HttpStatusCode)status));
        state.ChangeOrderNo("FOOD-ONE");
        await Load(state);
        Assert.Equal(login, state.RequiresLogin); Assert.Equal(message, state.ErrorMessage);
        Assert.Null(state.Trace); Assert.False(state.IsLoading); Assert.False(state.NotFound);
    }

    [Fact]
    public async Task 조회입력오류와주문변경은_지급실패와별개로정리한다()
    {
        var state = new 음식주문운영추적ReadSession((_, _) => throw new ArgumentException("주문번호를 입력해 주세요."));
        await Load(state);
        Assert.Equal("주문번호를 입력해 주세요.", state.ErrorMessage);
        state.RequireLogin();
        state.ChangeOrderNo("FOOD-TWO");
        Assert.Equal("FOOD-TWO", state.OrderNo); Assert.Null(state.ErrorMessage); Assert.Null(state.Trace);
        Assert.False(state.NotFound); Assert.True(state.RequiresLogin);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계정또는주문이바뀐뒤_늦은추적결과와오류는적용하지않는다(bool fail)
    {
        var pending = Pending<음식주문운영추적응답?>();
        var state = new 음식주문운영추적ReadSession((_, _) => pending.Task);
        var current = true;
        var loaded = false;
        var reading = Load(state, onLoaded: _ => loaded = true, isCurrent: () => current);
        current = false;
        if (fail) pending.SetException(new HttpRequestException("expired", null, HttpStatusCode.Unauthorized));
        else pending.SetResult(Trace("FOOD-ONE"));
        await reading;
        Assert.False(loaded); Assert.Null(state.Trace); Assert.Null(state.ErrorMessage);
        Assert.False(state.RequiresLogin); Assert.False(state.IsLoading);
    }

    [Fact]
    public async Task 이전세대조회가끝나도_새조회중표시를해제하지않는다()
    {
        var old = Pending<음식주문운영추적응답?>();
        var next = Pending<음식주문운영추적응답?>();
        var calls = 0;
        var state = new 음식주문운영추적ReadSession((_, _) => ++calls == 1 ? old.Task : next.Task);
        var oldCurrent = true;
        var first = Load(state, isCurrent: () => oldCurrent, ownsPendingState: () => oldCurrent);
        oldCurrent = false;
        state.InvalidatePending();
        state.ChangeOrderNo("FOOD-TWO");
        var second = Load(state);
        old.SetResult(Trace("FOOD-ONE")); await first;
        Assert.True(state.IsLoading); Assert.Null(state.Trace);
        next.SetResult(Trace("FOOD-TWO")); await second;
        Assert.False(state.IsLoading); Assert.Equal("FOOD-TWO", state.Trace!.주문번호);
    }

    [Fact]
    public async Task 취소한조회와지급은_실패나인증만료로표시하지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var state = new 음식주문운영추적ReadSession((_, token) => Task.FromCanceled<음식주문운영추적응답?>(token));
        await Load(state, cancellationToken: cancellation.Token);
        Assert.Null(state.ErrorMessage); Assert.False(state.RequiresLogin); Assert.False(state.IsLoading);

        var workflow = new 음식주문모의지급Workflow((_, _, token) => Task.FromCanceled<FoodDeliveryOrderSettlementDto>(token));
        var login = false;
        await Run(workflow, Settlement(), requireLogin: () => login = true, cancellationToken: cancellation.Token);
        Assert.Null(workflow.ErrorMessage); Assert.False(login); Assert.False(workflow.IsBusy);
    }

    [Theory]
    [InlineData("ready", true)]
    [InlineData("order", false)]
    [InlineData("missing-trace", false)]
    [InlineData("mode", false)]
    [InlineData("gross", false)]
    [InlineData("receipt", false)]
    [InlineData("paid", false)]
    [InlineData("quote", false)]
    public void 모의지급은_동일주문과기존서버정산조건을모두요구한다(string condition, bool expected)
    {
        var workflow = new 음식주문모의지급Workflow((_, _, _) => throw new InvalidOperationException("조회만으로 실행하면 안 됩니다."));
        var settlement = Settlement();
        if (condition == "mode") settlement.ServerExecutionModeCode = "Operational";
        if (condition == "gross") settlement.GrossAmount = null;
        if (condition == "receipt") settlement.ReceiptConfirmedAtUtc = null;
        if (condition == "paid") settlement.PayoutStatusCode = "SimulationSucceeded";
        if (condition == "quote") settlement.SettlementStatusCode = "BlockedMissingQuote";
        var tracedOrder = condition == "missing-trace" ? null : condition == "order" ? "FOOD-OTHER" : "FOOD-ONE";
        Assert.Equal(expected, workflow.CanSimulatePayout(" FOOD-ONE ", tracedOrder, settlement));
    }

    [Fact]
    public void 추적복원과수명초기화는_모의지급을자동실행하지않고_입력초기화범위를구분한다()
    {
        var calls = 0;
        var workflow = new 음식주문모의지급Workflow((_, _, _) => { calls++; return Task.FromResult(Settlement()); });
        var stored = Settlement();
        stored.DeductionAmount = 25; stored.DeductionEvidenceReference = "fixture-source";
        workflow.RestoreFixture(stored);
        Assert.Null(workflow.FixtureDeduction); Assert.Empty(workflow.FixtureEvidence);
        stored.DeductionEvidenceScopeCode = "SimulationFixture";
        workflow.RestoreFixture(stored);
        workflow.InvalidatePending();
        workflow.ChangeOrder();
        Assert.Equal(25m, workflow.FixtureDeduction); Assert.Equal("fixture-source", workflow.FixtureEvidence);
        workflow.ResetForTraceLoad();
        Assert.Null(workflow.FixtureDeduction); Assert.Empty(workflow.FixtureEvidence);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task 같은정산판본과시험입력의실패재시도는_동일멱등요청을명시적으로재사용한다()
    {
        var requests = new List<FoodDeliverySimulatedPayoutRequest>();
        var orders = new List<string>();
        var workflow = new 음식주문모의지급Workflow((order, request, _) =>
        {
            orders.Add(order); requests.Add(request); throw new HttpRequestException("offline");
        }) { FixtureDeduction = 125m, FixtureEvidence = " fixture-a " };
        var settlement = Settlement();
        await Run(workflow, settlement);
        Assert.Equal("offline", workflow.ErrorMessage); Assert.False(workflow.IsBusy);
        workflow.InvalidatePending();
        workflow.FixtureEvidence = "fixture-a";
        await Run(workflow, settlement);
        Assert.Same(requests[0], requests[1]); Assert.All(orders, order => Assert.Equal("FOOD-ONE", order));
        var request = requests[0];
        Assert.Equal(7, request.ExpectedSettlementRevision); Assert.Equal(3500m, request.ConfirmedGrossAmount);
        Assert.Equal(125m, request.ConfirmedDeductionAmount); Assert.Equal("fixture-a", request.DeductionEvidenceReference);
        Assert.Equal("Succeeded", request.OutcomeCode); Assert.Equal(32, request.IdempotencyKey.Length);

        workflow.FixtureDeduction = 126m; await Run(workflow, settlement);
        workflow.FixtureEvidence = "fixture-b"; await Run(workflow, settlement);
        settlement.Revision++; await Run(workflow, settlement);
        Assert.Equal(4, requests.Select(value => value.IdempotencyKey).Distinct().Count());

        workflow.ChangeOrder(); await Run(workflow, settlement);
        Assert.NotEqual(requests[^2].IdempotencyKey, requests[^1].IdempotencyKey);
        Assert.Equal(126m, workflow.FixtureDeduction); Assert.Equal("fixture-b", workflow.FixtureEvidence);
        workflow.ResetForTraceLoad();
        Assert.Null(workflow.FixtureDeduction); Assert.Empty(workflow.FixtureEvidence); Assert.Null(workflow.ErrorMessage);
        workflow.FixtureDeduction = 126m; workflow.FixtureEvidence = "fixture-b";
        await Run(workflow, settlement);
        Assert.NotEqual(requests[^2].IdempotencyKey, requests[^1].IdempotencyKey);
    }

    [Fact]
    public async Task 이전세대지급응답은_새지급중표시나정산을덮지않고_무효화만으로요청을버리지않는다()
    {
        var old = Pending<FoodDeliveryOrderSettlementDto>();
        var next = Pending<FoodDeliveryOrderSettlementDto>();
        var requests = new List<FoodDeliverySimulatedPayoutRequest>();
        var workflow = new 음식주문모의지급Workflow((_, request, _) =>
        {
            requests.Add(request); return requests.Count == 1 ? old.Task : next.Task;
        }) { FixtureDeduction = 0m, FixtureEvidence = "fixture" };
        var oldCurrent = true;
        var applied = new List<FoodDeliveryOrderSettlementDto>();
        var first = Run(workflow, Settlement(), applied.Add, isCurrent: () => oldCurrent, ownsPendingState: () => oldCurrent);
        Assert.True(workflow.IsBusy);
        oldCurrent = false; workflow.InvalidatePending();
        Assert.False(workflow.IsBusy);
        var second = Run(workflow, Settlement(), result => { Assert.True(workflow.IsBusy); applied.Add(result); });
        old.SetResult(Settlement()); await first;
        Assert.Empty(applied); Assert.True(workflow.IsBusy); Assert.Same(requests[0], requests[1]);
        var currentSettlement = Settlement(); next.SetResult(currentSettlement); await second;
        Assert.Same(currentSettlement, Assert.Single(applied)); Assert.False(workflow.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계정또는주문이바뀐뒤_늦은지급결과와인증오류는현재업무에적용하지않는다(bool fail)
    {
        var pending = Pending<FoodDeliveryOrderSettlementDto>();
        var workflow = new 음식주문모의지급Workflow((_, _, _) => pending.Task);
        var current = true;
        var applied = false; var login = false;
        var running = Run(workflow, Settlement(), _ => applied = true, () => login = true, () => current);
        current = false;
        if (fail) pending.SetException(new HttpRequestException("expired", null, HttpStatusCode.Unauthorized));
        else pending.SetResult(Settlement());
        await running;
        Assert.False(applied); Assert.False(login); Assert.Null(workflow.ErrorMessage); Assert.False(workflow.IsBusy);
    }

    [Fact]
    public async Task 지급인증만료는_추적을숨기고_지급실패를조회오류와구분한다()
    {
        var state = new 음식주문운영추적ReadSession((_, _) => Task.FromResult<음식주문운영추적응답?>(Trace("FOOD-ONE")));
        state.ChangeOrderNo("FOOD-ONE"); await Load(state);
        var workflow = new 음식주문모의지급Workflow((_, _, _) => throw new HttpRequestException("expired", null, HttpStatusCode.Unauthorized));
        await Run(workflow, Settlement(), requireLogin: state.RequireLogin);
        Assert.True(state.RequiresLogin); Assert.Null(state.Trace); Assert.Null(state.ErrorMessage);
        Assert.Equal("로그인이 만료되었습니다. 다시 로그인해 주세요.", workflow.ErrorMessage);
        Assert.False(workflow.IsBusy);
    }

    private static Task Load(음식주문운영추적ReadSession state,
        Action<음식주문운영추적응답?>? onLoaded = null, Func<bool>? isCurrent = null,
        Func<bool>? ownsPendingState = null, CancellationToken cancellationToken = default)
        => state.LoadAsync(state.OrderNo.Trim(), onLoaded ?? (_ => { }), isCurrent ?? (() => true),
            ownsPendingState ?? (() => true), cancellationToken);

    private static Task Run(음식주문모의지급Workflow workflow, FoodDeliveryOrderSettlementDto settlement,
        Action<FoodDeliveryOrderSettlementDto>? apply = null, Action? requireLogin = null,
        Func<bool>? isCurrent = null, Func<bool>? ownsPendingState = null, CancellationToken cancellationToken = default)
        => workflow.SimulateAsync("FOOD-ONE", settlement, apply ?? (_ => { }), requireLogin ?? (() => { }),
            isCurrent ?? (() => true), ownsPendingState ?? (() => true), cancellationToken);

    private static TaskCompletionSource<T> Pending<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static 음식주문운영추적응답 Trace(string orderNo) => new() { 주문번호 = orderNo };
    private static FoodDeliveryOrderSettlementDto Settlement() => new()
    {
        OrderNo = "FOOD-ONE", GrossAmount = 3500m, ReceiptConfirmedAtUtc = DateTime.UtcNow,
        ServerExecutionModeCode = "Simulation", SettlementStatusCode = "Ready", Revision = 7
    };
}
