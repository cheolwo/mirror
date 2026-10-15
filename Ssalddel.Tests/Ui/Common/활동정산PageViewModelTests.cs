using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 활동정산PageViewModelTests
{
    [Fact]
    public async Task 익명은_정산조회와명령을_보내지않는다()
    {
        var fixture = new 활동정산TestFixture(null);
        using var model = fixture.Model();
        await model.LoadAsync("agreement-1"); await model.ConsentAsync(true); await model.ConfirmAsync("entry-1");
        Assert.Equal(0, fixture.ReadCalls); Assert.Empty(fixture.Commands); Assert.True(model.RequiresLogin);
    }

    [Fact]
    public async Task 허용행동이없는항목은_상태만보고명령을만들지않는다()
    {
        var fixture = new 활동정산TestFixture(); fixture.Value.Agreement.AllowedActions = [];
        fixture.Value.Entries[0].AllowedActions = []; fixture.Value.Payments[0].AllowedActions = [];
        using var model = fixture.Model(); await model.LoadAsync("agreement-1");
        await model.ConsentAsync(true); await model.ConfirmAsync("entry-1"); await model.DisputeAsync("entry-1", "금액 확인");
        await model.ReceiveAsync("payment-1", 1000m, "확인");
        Assert.Empty(fixture.Commands);
    }

    [Fact]
    public async Task 응답유실은_요청을보존하고_결과조회만한다()
    {
        var fixture = new 활동정산TestFixture { Send = (_, _, _) => throw new IOException("private source") };
        var session = new 활동정산Session();
        using (var first = fixture.Model(session))
        {
            await first.LoadAsync("agreement-1"); await first.ConsentAsync(true);
            Assert.True(first.HasPending); Assert.DoesNotContain("private source", first.Error ?? "");
        }
        using var next = fixture.Model(session); await next.LoadAsync("agreement-1");
        await next.ConsentAsync(true); await next.ConfirmAsync("entry-1"); await next.RecoverAsync();
        var sent = Assert.Single(fixture.Commands);
        Assert.True(next.HasPending); Assert.Equal(sent.Command.RequestId, fixture.LastReceiptId); Assert.NotNull(next.Notice);
        fixture.Recovered = new() { AgreementId = "agreement-1", Kind = "Consent", RequestId = sent.Command.RequestId, Revision = 8 };
        fixture.Value.Agreement.Revision = 8;
        await next.RecoverAsync();
        Assert.False(next.HasPending); Assert.Equal(8, next.Detail?.Agreement.Revision); Assert.Single(fixture.Commands);
    }

    [Fact]
    public async Task 다른요청영수증이나_낮은정본판본은_성공으로확정하지않는다()
    {
        var fixture = new 활동정산TestFixture { Send = (_, _, _) => throw new IOException() };
        using var model = fixture.Model(); await model.LoadAsync("agreement-1"); await model.ConfirmAsync("entry-1");
        var sent = Assert.Single(fixture.Commands);
        fixture.Recovered = new() { AgreementId = "another-agreement", Kind = "ConfirmEntry", RequestId = sent.Command.RequestId, Revision = 8 };
        await model.RecoverAsync(); Assert.True(model.HasPending); Assert.NotNull(model.Error);
        fixture.Recovered.AgreementId = "agreement-1";
        await model.RecoverAsync(); Assert.True(model.HasPending); Assert.NotNull(model.Error);
        Assert.Single(fixture.Commands);
    }

    [Fact]
    public async Task 계정전환뒤_늦은조회는_다른계정에표시하지않는다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodSettlementSummaryDto?>();
        var fixture = new 활동정산TestFixture { Read = _ => delayed.Task };
        using var model = fixture.Model(); var loading = model.LoadAsync("agreement-1");
        fixture.Current = new("another-user", "다른 사용자", []); model.SynchronizeOwner();
        delayed.SetResult(fixture.Value); await loading;
        Assert.Null(model.Detail); Assert.Empty(model.Items); Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 계정전환뒤_늦은명령은_새계정내역과요청을갱신하지않는다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodSettlementReceiptDto?>();
        var fixture = new 활동정산TestFixture { Send = (_, _, _) => delayed.Task };
        var session = new 활동정산Session(); using var model = fixture.Model(session);
        await model.LoadAsync("agreement-1"); var sending = model.ConsentAsync(true);
        var sent = Assert.Single(fixture.Commands);
        fixture.Current = new("another-user", "다른 사용자", []); model.SynchronizeOwner();
        delayed.SetResult(new() { AgreementId = "agreement-1", Kind = "Consent", RequestId = sent.Command.RequestId, Revision = 8 }); await sending;
        Assert.Null(model.Detail); Assert.Null(session.Command); Assert.False(model.IsSending);
    }

    [Fact]
    public async Task 잘못연결된상세는_표시하지않는다()
    {
        var fixture = new 활동정산TestFixture(); fixture.Value.Entries[0].AgreementId = "another-agreement";
        using var model = fixture.Model(); await model.LoadAsync("agreement-1");
        Assert.Null(model.Detail); Assert.NotNull(model.Error); Assert.False(model.Loaded);
    }

    [Fact]
    public async Task 이의제기_입력을고친뒤_같은화면에서접수할수있다()
    {
        var fixture = new 활동정산TestFixture(); using var model = fixture.Model(); await model.LoadAsync("agreement-1");
        await model.DisputeAsync("entry-1", ""); Assert.NotNull(model.ValidationError); Assert.Empty(fixture.Commands);
        await model.DisputeAsync("entry-1", " 입고 수량 재확인 ");
        var sent = Assert.Single(fixture.Commands); var request = Assert.IsType<NeighborhoodSettlementDisputeRequest>(sent.Command);
        Assert.Equal("입고 수량 재확인", request.Reason); Assert.Equal(7, request.ExpectedRevision);
        Assert.False(model.HasPending); Assert.Equal(8, model.Detail?.Agreement.Revision);
    }

    [Fact]
    public async Task 수령확인은_본인수령액증가와_허용행동을검사한다()
    {
        var fixture = new 활동정산TestFixture(); fixture.Value.Payments[0].RecipientConfirmedAmountKrw = 400;
        using var model = fixture.Model(); await model.LoadAsync("agreement-1");
        await model.ReceiveAsync("payment-1", 400, "변화 없음"); await model.ReceiveAsync("payment-1", 1100, "초과");
        Assert.Empty(fixture.Commands); Assert.NotNull(model.ValidationError);
        await model.ReceiveAsync("payment-1", 1000, "수령액 확인");
        var sent = Assert.Single(fixture.Commands); Assert.IsType<NeighborhoodSettlementPaymentReceiptRequest>(sent.Command);
        Assert.False(model.HasPending);
    }

    [Fact]
    public async Task 성과금발생은_사전확인과_표시한계산지문을결속한다()
    {
        var fixture = new 활동정산TestFixture(); fixture.Value.Agreement.AllowedActions = ["accrue-activity-reward"];
        fixture.Value.Agreement.Terms.ActivityRewardTerms = new() { PayerUserId = "worker" };
        fixture.Value.ActivityReward = new() { StatusCode = NeighborhoodActivityRewardCodes.Ready, CalculationDigest = "calculation-original" };
        using var model = fixture.Model(); await model.LoadAsync("agreement-1");
        await model.AccrueRewardAsync(false); Assert.Empty(fixture.Commands);
        await model.AccrueRewardAsync(true);
        var request = Assert.IsType<NeighborhoodSettlementActivityRewardAccrualRequest>(Assert.Single(fixture.Commands).Command);
        Assert.Equal("calculation-original", request.ExpectedCalculationDigest); Assert.Equal(7, request.ExpectedRevision);
    }

    [Fact]
    public async Task 근거대기나이미발생한성과금은_새발생명령을만들지않는다()
    {
        var fixture = new 활동정산TestFixture(); fixture.Value.Agreement.AllowedActions = ["accrue-activity-reward"];
        fixture.Value.Agreement.Terms.ActivityRewardTerms = new() { PayerUserId = "worker" };
        fixture.Value.ActivityReward = new() { CalculationDigest = "same" };
        using var model = fixture.Model(); await model.LoadAsync("agreement-1"); await model.AccrueRewardAsync(true);
        fixture.Value.ActivityReward.StatusCode = NeighborhoodActivityRewardCodes.Ready; fixture.Value.ActivityReward.AlreadyAccrued = true;
        await model.AccrueRewardAsync(true); Assert.Empty(fixture.Commands);
    }
}

internal sealed class 활동정산TestFixture(string? userId = "worker") : I활동정산Client, ISsalddel현재사용자Context
{
    public 현재사용자Snapshot Current { get; set; } = userId is null ? 현재사용자Snapshot.익명 : new(userId, userId, []);
    public 현재사용자Snapshot 현재사용자 => Current;
    public NeighborhoodSettlementSummaryDto Value { get; set; } = Summary();
    public IReadOnlyList<NeighborhoodSettlementSummaryDto>? ListValues { get; set; }
    public Func<string, Task<NeighborhoodSettlementSummaryDto?>>? Read { get; set; }
    public Func<string, string, NeighborhoodSettlementCommand, Task<NeighborhoodSettlementReceiptDto?>>? Send { get; set; }
    public NeighborhoodSettlementReceiptDto? Recovered { get; set; }
    public WorkerWeeklyStatementDto? WeeklyValue { get; set; }
    public Func<Task<WorkerWeeklyStatementDto?>>? WeeklyRead { get; set; }
    public Guid? LastReceiptId { get; private set; }
    public int ReadCalls { get; private set; }
    public List<(string Id, string Kind, NeighborhoodSettlementCommand Command)> Commands { get; } = [];
    public 활동정산PageViewModel Model(활동정산Session? session = null) => new(this, session ?? new(), this);
    public Task<IReadOnlyList<NeighborhoodSettlementSummaryDto>> MineAsync(CancellationToken ct) { ReadCalls++; return Task.FromResult<IReadOnlyList<NeighborhoodSettlementSummaryDto>>(ListValues ?? [Value]); }
    public Task<NeighborhoodSettlementSummaryDto?> DetailAsync(string id, CancellationToken ct) { ReadCalls++; return Read?.Invoke(id) ?? Task.FromResult<NeighborhoodSettlementSummaryDto?>(Value); }
    public Task<NeighborhoodSettlementReceiptDto?> ReceiptAsync(Guid requestId, CancellationToken ct) { LastReceiptId = requestId; return Task.FromResult(Recovered); }
    public Task<NeighborhoodSettlementReceiptDto?> ConsentAsync(string id, NeighborhoodSettlementConsentRequest request, CancellationToken ct) => Write(id, "Consent", request);
    public Task<NeighborhoodSettlementReceiptDto?> ConfirmAsync(string id, NeighborhoodSettlementEntryConfirmRequest request, CancellationToken ct) => Write(id, "ConfirmEntry", request);
    public Task<NeighborhoodSettlementReceiptDto?> DisputeAsync(string id, NeighborhoodSettlementDisputeRequest request, CancellationToken ct) => Write(id, "Dispute", request);
    public Task<NeighborhoodSettlementReceiptDto?> ReceiveAsync(string id, NeighborhoodSettlementPaymentReceiptRequest request, CancellationToken ct) => Write(id, "ConfirmReceipt", request);
    public Task<NeighborhoodSettlementReceiptDto?> AccrueRewardAsync(string id, NeighborhoodSettlementActivityRewardAccrualRequest request, CancellationToken ct) => Write(id, "AccrueActivityReward", request);
    public Task<NeighborhoodSettlementReceiptDto?> ReportPaymentAsync(string id, NeighborhoodSettlementPaymentReportRequest request, CancellationToken ct) => Write(id, "ReportPayment", request);
    public Task<WorkerWeeklyStatementDto?> WeeklyAsync(DateOnly? completionDateKst, CancellationToken ct) => WeeklyRead?.Invoke() ?? Task.FromResult(WeeklyValue);
    private Task<NeighborhoodSettlementReceiptDto?> Write(string id, string kind, NeighborhoodSettlementCommand request)
    {
        Commands.Add((id, kind, request));
        if (Send is not null) return Send(id, kind, request);
        Value.Agreement.Revision = request.ExpectedRevision + 1;
        return Task.FromResult<NeighborhoodSettlementReceiptDto?>(new() { AgreementId = id, Kind = kind, RequestId = request.RequestId, Revision = Value.Agreement.Revision });
    }
    public static NeighborhoodSettlementSummaryDto Summary() => new()
    {
        Agreement = new() { AgreementId = "agreement-1", Revision = 7, TermsVersion = 2, StatusCode = NeighborhoodSettlementCodes.Active,
            AllowedActions = ["consent", "revoke"], RequiredConsentUserIds = ["worker"], Terms = new()
            { ActivityKind = NeighborhoodSettlementCodes.StoredGoods, Lines = [new() { Kind = NeighborhoodSettlementCodes.Inbound, LineKey = "inbound-1", PayerUserId = "owner", PayeeUserId = "worker", AmountKrw = null }] } },
        Entries = [new() { EntryId = "entry-1", AgreementId = "agreement-1", Kind = NeighborhoodSettlementCodes.Inbound,
            PayerUserId = "owner", PayeeUserId = "worker", AmountKrw = 1000, StatusCode = NeighborhoodSettlementCodes.Accrued,
            AllowedActions = ["confirm-entry", "dispute"], CalculationBasis = "확인한 입고 수량", WorkEvidenceReferences = ["private-evidence-original"] }],
        Payments = [new() { PaymentId = "payment-1", AgreementId = "agreement-1", PayerUserId = "owner", PayeeUserId = "worker",
            AmountKrw = 1000, StatusCode = NeighborhoodSettlementCodes.Reported, AllowedActions = ["confirm-receipt"], EvidenceReference = "private-bank-original" }]
    };
}
