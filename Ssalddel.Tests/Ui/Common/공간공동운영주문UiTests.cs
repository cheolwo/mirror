using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영주문UiTests
{
    [Fact]
    public async Task 연결된_본인수요후보의_참조를_보존하고_조건확인후_제안한다()
    {
        var client = new Client(); using var vm = Make(client);
        await vm.LoadAsync("hub", "cycle"); vm.SelectCandidate(0); vm.ConfirmTerms = true;
        await vm.ProposeAsync();
        var command = Assert.Single(client.Commands);
        Assert.Equal("propose", command.Action); Assert.True(command.ConfirmTerms);
        Assert.Equal(3, command.ExpectedRevision); Assert.Equal(0, command.ExpectedTermsVersion);
        Assert.Equal("lot", command.Terms?.LotId); Assert.Equal("site", command.Terms?.SiteId);
        Assert.Equal("cargo", command.Terms?.CargoRequestId); Assert.Equal(1, client.ReadCalls);
        Assert.False(vm.HasPending); Assert.False(vm.Items[0].PaymentConfirmed);
    }

    [Fact]
    public async Task 다른주문_확인체크를_현재주문의_동의로_사용하지_않는다()
    {
        var client = new Client(); using var vm = Make(client); await vm.LoadAsync("hub", "cycle");
        vm.ConfirmOrder(Guid.NewGuid(), true); await vm.ExecuteAsync(client.Order, "agree");
        Assert.Empty(client.Commands); Assert.Contains("해당 주문", vm.Error);
        vm.ConfirmOrder(client.Order.OrderRequestId, true); await vm.ExecuteAsync(client.Order, "agree");
        var command = Assert.Single(client.Commands); Assert.Equal(client.Order.TermsVersion, command.ExpectedTermsVersion);
    }

    [Fact]
    public async Task 주문전송_유실은_영수증조회_전까지_재전송하지_않는다()
    {
        var client = new Client { Failure = new IOException("비공개 내용") }; using var vm = Make(client);
        await vm.LoadAsync("hub", "cycle"); vm.ConfirmOrder(client.Order.OrderRequestId, true);
        await vm.ExecuteAsync(client.Order, "handoff");
        Assert.True(vm.HasPending); Assert.DoesNotContain("비공개", vm.Error ?? "");
        await vm.RetryAsync(); Assert.Single(client.Commands);
        await vm.RecoverAsync(); Assert.Single(client.Commands); Assert.Equal(client.Commands[0].ClientRequestId, client.LastReceipt);
        client.Failure = null; await vm.RetryAsync();
        Assert.Equal(client.Commands[0].ClientRequestId, client.Commands[1].ClientRequestId); Assert.False(vm.HasPending);
    }

    [Fact]
    public async Task 다른주문_영수증은_원요청을_완료하지_않는다()
    {
        var client = new Client { Failure = new IOException() }; using var vm = Make(client);
        await vm.LoadAsync("hub", "cycle"); vm.ConfirmOrder(client.Order.OrderRequestId, true); await vm.ExecuteAsync(client.Order, "agree");
        client.Receipt = new() { LedgerId = "hub", OrderRequestId = Guid.NewGuid(), Revision = 4 };
        await vm.RecoverAsync(); Assert.True(vm.HasPending); Assert.Contains("다른 주문", vm.Error);
    }

    [Fact]
    public async Task 조회실패는_빈_주문_성공과_구분한다()
    {
        var client = new Client { ListFailure = new IOException() }; using var vm = Make(client);
        await vm.LoadAsync("hub", "cycle"); Assert.False(vm.Loaded); Assert.NotNull(vm.Error);
    }

    [Fact]
    public async Task 오래된후보판본이_거절되면_새후보조회와_재확인없이는_다시제안하지_않는다()
    {
        var client = new Client { Failure = new SsalddelApiException("stale", 409, "주문", "", null) }; using var vm = Make(client);
        await vm.LoadAsync("hub", "cycle"); vm.SelectCandidate(0); vm.ConfirmTerms = true; await vm.ProposeAsync();
        Assert.False(vm.HasPending); Assert.Null(vm.Draft); Assert.False(vm.ConfirmTerms); Assert.Empty(vm.Candidates);
        await vm.ProposeAsync(); Assert.Single(client.Commands);
        client.Failure = null; await vm.LoadAsync("hub", "cycle"); vm.SelectCandidate(0); vm.ConfirmTerms = true; await vm.ProposeAsync();
        Assert.Equal(2, client.Commands.Count);
    }

    private static 공간공동운영주문ViewModel Make(Client client)
        => new(client, new User(), new Scope(), new());
    private sealed class User : ISsalddel현재사용자Context { public 현재사용자Snapshot 현재사용자 { get; } = new("one", "이웃", []); }
    private sealed class Scope : IWorkflowRecoveryContext
    {
        public WorkflowRecoverySnapshot Capture() => new("one", 1, new('a', 32));
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => snapshot == Capture();
    }
    private sealed class Client : I공간공동운영주문Client
    {
        public 공간공동운영주문Response Order = new() { LedgerId = "hub", OrderRequestId = Guid.NewGuid(), Revision = 3, TermsVersion = 2, ProductName = "상온 상품", AllowedActions = ["agree", "handoff", "resume"] };
        public readonly List<공간공동운영주문명령Request> Commands = [];
        public Guid? LastReceipt;
        public int ReadCalls;
        public Exception? Failure, ListFailure;
        public 공간공동운영주문Response? Receipt;
        public Task<공간공동운영주문목록Response> ListAsync(string ledgerId, CancellationToken ct)
            => ListFailure is null ? Task.FromResult(new 공간공동운영주문목록Response { Items = [Order] }) : Task.FromException<공간공동운영주문목록Response>(ListFailure);
        public Task<공간공동운영주문후보목록Response> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct)
            => Task.FromResult(new 공간공동운영주문후보목록Response { LedgerId = ledgerId, Revision = 3, Items = [new() { Label = "내 상온 상품 수요", ProductName = "상온 상품", Terms = new() { OrderRequestId = Order.OrderRequestId, Quantity = 1, UnitPriceKrw = 1000, DeadlineUtc = DateTime.UtcNow.AddDays(1), CycleId = cycleId, SiteId = "site", LotId = "lot", WaveId = "wave", CargoRequestId = "cargo" } }] });
        public Task<공간공동운영주문Response?> ReadAsync(string ledgerId, Guid orderRequestId, CancellationToken ct) { ReadCalls++; return Task.FromResult<공간공동운영주문Response?>(Order); }
        public Task<공간공동운영주문Response?> CommandAsync(string ledgerId, Guid orderRequestId, 공간공동운영주문명령Request request, CancellationToken ct)
        { Commands.Add(request); return Failure is null ? Task.FromResult<공간공동운영주문Response?>(Order) : Task.FromException<공간공동운영주문Response?>(Failure); }
        public Task<공간공동운영주문Response?> ReceiptAsync(string ledgerId, Guid clientRequestId, CancellationToken ct) { LastReceipt = clientRequestId; return Task.FromResult(Receipt); }
    }
}
