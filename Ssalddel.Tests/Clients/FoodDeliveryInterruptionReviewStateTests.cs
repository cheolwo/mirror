using System.Net;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class FoodDeliveryInterruptionReviewStateTests
{
    [Fact]
    public async Task 검토는_선택한중단시도의판본과근거를보내고_같은주문을재조회한다()
    {
        var client = new Client();
        using var state = await Ready(client);
        var request = Prepare(state);
        client.Review = (_, body, _) =>
        {
            client.Current = Trace(revision: 8, responsibility: 운영배차책임Code.플랫폼);
            return Task.FromResult(client.Current.배달시도목록[0]);
        };
        await state.SubmitAsync();
        Assert.Single(client.Requests);
        Assert.Equal("attempt-1", client.Requests[0].Attempt);
        Assert.Equal(7, client.Requests[0].Body.예상Revision);
        Assert.NotEqual(Guid.Empty, client.Requests[0].Body.클라이언트요청Id);
        Assert.Equal(request, client.Requests[0].Body.판정사유);
        Assert.Equal(2, client.ReadOrders.Count);
        Assert.All(client.ReadOrders, order => Assert.Equal("FOOD-1", order));
        Assert.Equal(8, state.SelectedAttempt!.Revision);
        Assert.True(state.Saved);
        Assert.False(state.Confirmed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 명시선택과근거확인이없으면_조회나로그인만으로검토를저장하지않는다(bool chooseAttempt)
    {
        var client = new Client();
        using var state = await Ready(client);
        if (chooseAttempt) { state.SelectAttempt("attempt-1"); state.DecisionCode = 음식배달중단검토판정Code.보호; state.Reason = "근거"; }
        await state.SubmitAsync();
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task 보호중단을기사책임으로바꾸려면_악용사람확인이필요하다()
    {
        var client = new Client { Current = Trace(responsibility: 운영배차책임Code.보호대상) };
        using var state = await Ready(client);
        Prepare(state);
        state.DecisionCode = 음식배달중단검토판정Code.기사책임;
        await state.SubmitAsync();
        Assert.Empty(client.Requests);
        state.AbuseConfirmed = true;
        await state.SubmitAsync();
        Assert.True(client.Requests[0].Body.악용확정여부);
    }

    [Fact]
    public async Task 완료된시도와천자초과근거는_저장하지않는다()
    {
        var client = new Client { Current = Trace(status: 음식배달시도상태Code.전달완료) };
        using var state = await Ready(client);
        Prepare(state);
        await state.SubmitAsync();
        Assert.Empty(client.Requests);
        client.Current = Trace();
        await state.RefreshAsync();
        state.Reason = new string('가', 1001);
        state.Confirmed = true;
        await state.SubmitAsync();
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task 중복클릭은_저장중요청을추가하지않는다()
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        var pending = new TaskCompletionSource<음식배달시도운영응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Review = (_, _, _) => pending.Task;
        var first = state.SubmitAsync();
        state.Confirmed = true;
        await state.SubmitAsync();
        Assert.Single(client.Requests);
        pending.SetResult(Trace().배달시도목록[0]);
        await first;
    }

    [Fact]
    public async Task 응답유실뒤같은입력은_원요청Id와판본으로명시재시도한다()
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        client.Review = (_, _, _) =>
        {
            client.Current = Trace(revision: 8);
            return Task.FromException<음식배달시도운영응답>(new HttpRequestException("synthetic response lost"));
        };
        await state.SubmitAsync();
        Assert.False(state.Saved);
        Assert.Equal(8, state.SelectedAttempt!.Revision);
        Assert.False(state.CanSubmit);
        var original = client.Requests[0].Body;
        client.Review = (_, _, _) => Task.FromResult(client.Current.배달시도목록[0]);
        state.Confirmed = true;
        await state.SubmitAsync();
        Assert.Equal(original.클라이언트요청Id, client.Requests[1].Body.클라이언트요청Id);
        Assert.Equal(original.예상Revision, client.Requests[1].Body.예상Revision);
    }

    [Fact]
    public async Task 충돌뒤에는_최신판본과새요청Id로다시검토한다()
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        client.Review = (_, _, _) =>
        {
            client.Current = Trace(revision: 9);
            return Task.FromException<음식배달시도운영응답>(new HttpRequestException("conflict", null, HttpStatusCode.Conflict));
        };
        await state.SubmitAsync();
        Assert.False(state.Saved);
        var originalId = client.Requests[0].Body.클라이언트요청Id;
        client.Review = (_, _, _) => Task.FromResult(client.Current.배달시도목록[0]);
        state.Confirmed = true;
        await state.SubmitAsync();
        Assert.Equal(9, client.Requests[1].Body.예상Revision);
        Assert.NotEqual(originalId, client.Requests[1].Body.클라이언트요청Id);
    }

    [Fact]
    public async Task 응답유실후근거를바꾸면_원요청Id를재사용하지않는다()
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        client.Review = (_, _, _) => Task.FromException<음식배달시도운영응답>(new HttpRequestException("lost"));
        await state.SubmitAsync();
        var originalId = client.Requests[0].Body.클라이언트요청Id;
        state.Reason = "다른 확인 근거";
        state.Confirmed = true;
        client.Review = (_, _, _) => Task.FromResult(client.Current.배달시도목록[0]);
        await state.SubmitAsync();
        Assert.NotEqual(originalId, client.Requests[1].Body.클라이언트요청Id);
    }

    [Fact]
    public async Task 저장실패뒤정본조회도실패하면_이전표시로저장할수없다()
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        client.Review = (_, _, _) => Task.FromException<음식배달시도운영응답>(new HttpRequestException("lost"));
        client.Read = (_, _) => Task.FromException<음식주문운영추적응답?>(new HttpRequestException("read failed"));
        await state.SubmitAsync();
        state.Confirmed = true;
        Assert.True(state.RequiresRefresh);
        Assert.False(state.CanSubmit);
        await state.SubmitAsync();
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task 저장후재조회실패는_최신확인성공으로표시하지않는다()
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        client.Read = (_, _) => Task.FromException<음식주문운영추적응답?>(new HttpRequestException("read failed"));
        await state.SubmitAsync();
        Assert.False(state.Saved);
        Assert.True(state.RequiresRefresh);
        Assert.Contains("저장 응답은 받았지만", state.Message);
    }

    [Fact]
    public async Task 최종401은_업무근거를제거하고자동재저장을하지않는다()
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        client.Review = (_, _, _) => Task.FromException<음식배달시도운영응답>(new HttpRequestException("expired", null, HttpStatusCode.Unauthorized));
        await state.SubmitAsync();
        Assert.True(state.RequiresLogin);
        Assert.Null(state.Trace);
        Assert.False(state.CanSubmit);
        Assert.Single(client.ReadOrders); // 만료된 자격으로 후속 조회하지 않는다.
        await state.RefreshAsync();
        await state.SubmitAsync();
        Assert.Single(client.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 이탈이나주문변경후늦은저장은_새화면이나후속조회에적용하지않는다(bool changeOrder)
    {
        var client = new Client();
        using var state = await Ready(client);
        Prepare(state);
        var pending = new TaskCompletionSource<음식배달시도운영응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Review = (_, _, _) => pending.Task;
        var saving = state.SubmitAsync();
        if (changeOrder) state.BindOrder("FOOD-2"); else state.Dispose();
        pending.SetResult(Trace().배달시도목록[0]);
        await saving;
        Assert.Single(client.ReadOrders);
        Assert.False(state.Saved);
        if (changeOrder) { Assert.Equal("FOOD-2", state.OrderNo); Assert.Null(state.Trace); Assert.Equal("", state.Reason); }
    }

    [Fact]
    public async Task 계정변경후늦은조회와검토는_새계정에적용하지않는다()
    {
        var client = new Client();
        var identity = "admin-a";
        using var state = new FoodDeliveryInterruptionReviewState(client, () => identity);
        state.BindOrder("FOOD-1");
        var read = new TaskCompletionSource<음식주문운영추적응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Read = (_, _) => read.Task;
        var loading = state.RefreshAsync();
        identity = "admin-b";
        state.BindOrder("FOOD-2");
        read.SetResult(Trace());
        await loading;
        Assert.Null(state.Trace);
        Assert.Equal("FOOD-2", state.OrderNo);
        Assert.Empty(client.Requests);
    }

    private static async Task<FoodDeliveryInterruptionReviewState> Ready(Client client)
    {
        var state = new FoodDeliveryInterruptionReviewState(client, () => "synthetic-admin");
        state.BindOrder("FOOD-1");
        await state.RefreshAsync();
        return state;
    }

    private static string Prepare(FoodDeliveryInterruptionReviewState state)
    {
        state.SelectAttempt("attempt-1");
        state.DecisionCode = 음식배달중단검토판정Code.플랫폼책임;
        state.Reason = "확인된 합성 운영 근거";
        state.Confirmed = true;
        return state.Reason;
    }

    private static 음식주문운영추적응답 Trace(long revision = 7, string responsibility = 운영배차책임Code.미확정, string status = 음식배달시도상태Code.중단)
        => new()
        {
            주문번호 = "FOOD-1",
            배달시도목록 = [new() { 시도StableId = "attempt-1", 시도순번 = 1, Revision = revision, 상태Code = status, 책임Code = responsibility }]
        };

    private sealed class Client : IFoodOrderInterruptionReviewClient
    {
        public 음식주문운영추적응답 Current { get; set; } = Trace();
        public Func<string, CancellationToken, Task<음식주문운영추적응답?>>? Read { get; set; }
        public Func<string, 음식배달중단검토요청, CancellationToken, Task<음식배달시도운영응답>>? Review { get; set; }
        public List<string> ReadOrders { get; } = [];
        public List<(string Attempt, 음식배달중단검토요청 Body)> Requests { get; } = [];
        public Task<음식주문운영추적응답?> 조회Async(string orderNo, CancellationToken token = default)
        {
            ReadOrders.Add(orderNo);
            return Read?.Invoke(orderNo, token) ?? Task.FromResult<음식주문운영추적응답?>(Current);
        }
        public Task<음식배달시도운영응답> 중단검토Async(string attemptId, 음식배달중단검토요청 body, CancellationToken token = default)
        {
            Requests.Add((attemptId, new() { 클라이언트요청Id = body.클라이언트요청Id, 예상Revision = body.예상Revision, 판정Code = body.판정Code, 악용확정여부 = body.악용확정여부, 판정사유 = body.판정사유 }));
            return Review?.Invoke(attemptId, body, token) ?? Task.FromResult(Current.배달시도목록[0]);
        }
    }
}
