using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Mart;
using Ssalddel.Ui.Common.Areas.App.Components.Mart;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class MartReceiptResolutionUiR76Tests
{
    [Fact]
    public async Task 익명과서버비허용행동은보내지않고전량수량과근거확인을입력에서검사한다()
    {
        var f = new Fixture { Current = 현재사용자Snapshot.익명 }; using var model = f.Model();
        await model.LoadAsync(701); await model.ProposeAsync(true, 2, "근거", true); Assert.Equal(0, f.Reads); Assert.Empty(f.Sends);
        f.Current = new("buyer", "본인", []); await model.LoadAsync(701);
        await model.ProposeAsync(true, 1, "근거", true); await model.ProposeAsync(true, 2, "", true);
        await model.ProposeAsync(true, 2, "근거", false); Assert.Empty(f.Sends); Assert.NotNull(model.ValidationError);
        f.Value.CanProposeResolution = false; await model.ProposeAsync(true, 2, "근거", true); Assert.Empty(f.Sends);
    }

    [Fact]
    public async Task 응답유실을재진입해도새요청을막고같은요청결과와현재정본후에만잠금을해제한다()
    {
        var f = new Fixture { SendError = new IOException("private-error") }; var store = new VolatileWorkflowRecoveryStore();
        Guid id;
        using (var first = f.Model(store))
        {
            await first.LoadAsync(701); await first.ProposeAsync(true, 2, "개인 근거 진술", true);
            Assert.True(first.HasPending); Assert.DoesNotContain("private-error", first.Error); id = Assert.Single(f.Sends);
            var saved = await store.LoadAsync("buyer", "mart-receipt-resolution:701", default);
            Assert.NotNull(saved); Assert.DoesNotContain("개인 근거 진술", saved!.PayloadJson!);
        }
        using var next = f.Model(store); await next.LoadAsync(701);
        await next.ProposeAsync(true, 2, "새 요청", true); await next.RecoverAsync();
        Assert.True(next.HasPending); Assert.Single(f.Sends); Assert.Equal(id, f.LastResult);
        f.Value.Revision = 8; f.Result = Copy(f.Value, id);
        await next.RecoverAsync(); Assert.False(next.HasPending); Assert.Equal(8, next.Value!.Revision);
        Assert.Null(await store.LoadAsync("buyer", "mart-receipt-resolution:701", default));
    }

    [Fact]
    public async Task 다른요청이나낡은정본조회는이전요청을없애지않는다()
    {
        var f = new Fixture { SendError = new IOException() }; using var model = f.Model(); await model.LoadAsync(701);
        await model.ProposeAsync(true, 2, "직접 대조", true);
        f.Result = Copy(f.Value, Guid.NewGuid(), 8); await model.RecoverAsync(); Assert.True(model.HasPending);
        f.Result = Copy(f.Value, f.Sends.Single(), 8); await model.RecoverAsync(); Assert.True(model.HasPending);
        Assert.Single(f.Sends); Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 계정이바뀐뒤늦게도착한조회와쓰기응답은새계정화면에붙이지않는다()
    {
        var f = new Fixture(); using var model = f.Model(); var wait = new TaskCompletionSource<마트수령문제해결Dto?>();
        f.Read = () => wait.Task; var load = model.LoadAsync(701);
        f.Current = new("seller", "판매자", []); wait.SetResult(f.Value); await load;
        Assert.Null(model.Value); Assert.False(model.HasPending);
        f.Read = null; f.Value.MyRole = "Seller"; await model.LoadAsync(701);
        var response = new TaskCompletionSource<마트수령문제해결Dto?>(); f.Send = _ => response.Task;
        var send = model.ProposeAsync(true, 2, "직접 대조", true);
        f.Current = new("another", "다른 사용자", []); response.SetResult(Copy(f.Value, f.Sends.Single(), 8)); await send;
        Assert.Null(model.Value); Assert.False(model.HasPending); Assert.False(model.IsSending);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task 명령거절표시가있는경우만미확정잠금을해제한다(bool rejected, bool pending)
    {
        var f = new Fixture { SendError = new SsalddelApiException("거절", 409, "수령 해결", rejected ? "{\"commandRejected\":true}" : "{}", null) };
        using var model = f.Model(); await model.LoadAsync(701); await model.ProposeAsync(true, 2, "직접 대조", true);
        Assert.Equal(pending, model.HasPending); Assert.Single(f.Sends);
    }

    [Fact]
    public async Task 실제Razor출력은다음담당과명시확인을표시하고입금완료와비공개근거는노출하지않는다()
    {
        var f = new Fixture(); using var model = f.Model(); await model.LoadAsync(701);
        var html = await Render(model);
        Assert.Contains("해결 조건 제안 대기", html); Assert.Contains("구매자 또는 판매자", html); Assert.Contains("실제 확인 수량", html);
        Assert.Contains("개인정보 제외", html); Assert.Contains("입금 확인을 대신하지 않습니다", html);
        Assert.DoesNotContain("private-raw-note", html); Assert.DoesNotContain("입금 완료", html);
        f.SendError = new IOException(); await model.ProposeAsync(true, 2, "private-raw-note", true);
        html = await Render(model); Assert.Contains("이전 요청 결과 확인", html); Assert.DoesNotContain("<legend>해결 조건 제안", html);
    }

    private static async Task<string> Render(마트수령문제해결ViewModel model)
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<마트수령문제해결Form>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(마트수령문제해결Form.Model)] = model }))).ToHtmlString()));
    }
    private static 마트수령문제해결Dto Copy(마트수령문제해결Dto value, Guid id, long? revision = null)
        => new() { MartOrderId = value.MartOrderId, AttemptStableId = value.AttemptStableId, Revision = revision ?? value.Revision, MyRole = value.MyRole,
            TotalOrderQuantity = value.TotalOrderQuantity, HoldRecorded = true, ResolutionStatus = value.ResolutionStatus, ResponseRequestId = id };
    private sealed class Fixture : I마트수령문제해결Client, ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot Current { get; set; } = new("buyer", "본인", []);
        public 현재사용자Snapshot 현재사용자 => Current;
        public 마트수령문제해결Dto Value { get; set; } = new() { MartOrderId = 701, AttemptStableId = "attempt", Revision = 7, MyRole = "Buyer",
            TotalOrderQuantity = 2, HoldRecorded = true, HoldReasonCode = "Missing", ResolutionStatus = "AwaitingProposal", NextActor = "BuyerOrSeller", CanProposeResolution = true };
        public 마트수령문제해결Dto? Result { get; set; }
        public Exception? SendError { get; set; }
        public Func<Task<마트수령문제해결Dto?>>? Read { get; set; }
        public Func<Guid, Task<마트수령문제해결Dto?>>? Send { get; set; }
        public List<Guid> Sends { get; } = [];
        public Guid? LastResult { get; private set; }
        public int Reads { get; private set; }
        public 마트수령문제해결ViewModel Model(IWorkflowRecoveryStore? store = null) => new(this, store ?? new VolatileWorkflowRecoveryStore(), this);
        public Task<마트수령문제해결Dto?> ReadAsync(long id, CancellationToken ct) { Reads++; return Read?.Invoke() ?? Task.FromResult<마트수령문제해결Dto?>(Value); }
        public Task<마트수령문제해결Dto?> ResultAsync(long id, Guid requestId, CancellationToken ct) { LastResult = requestId; return Task.FromResult(Result); }
        public Task<마트수령문제해결Dto?> ProposeAsync(long id, 마트수령해결제안요청 request, CancellationToken ct) => Write(request.ClientRequestId);
        public Task<마트수령문제해결Dto?> ConsentAsync(long id, 마트수령해결동의요청 request, CancellationToken ct) => Write(request.ClientRequestId);
        private Task<마트수령문제해결Dto?> Write(Guid id) { Sends.Add(id); if (SendError is not null) throw SendError; return Send?.Invoke(id) ?? Task.FromResult<마트수령문제해결Dto?>(Copy(Value, id, Value.Revision + 1)); }
    }
}
