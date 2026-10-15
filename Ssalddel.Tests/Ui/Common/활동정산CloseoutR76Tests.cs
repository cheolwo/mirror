using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Components.Settlement;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 활동정산CloseoutR76Tests
{
    [Fact]
    public async Task 지급자는_서버상한과진술동의로_기존지급신고API에정확히보낸다()
    {
        var f = Payer(); using var model = f.Model(); await model.LoadAsync("agreement-1");
        await model.ReportPaymentAsync("entry-1", 801, "지급 확인", true);
        await model.ReportPaymentAsync("entry-1", 800, "지급 확인", false);
        Assert.Empty(f.Commands);
        await model.ReportPaymentAsync("entry-1", 800, "  본인 이체 내역 확인  ", true);
        var command = Assert.Single(f.Commands); Assert.Equal("ReportPayment", command.Kind);
        var report = Assert.IsType<NeighborhoodSettlementPaymentReportRequest>(command.Command);
        Assert.Equal(7, report.ExpectedRevision); Assert.NotEqual(Guid.Empty, report.RequestId);
        Assert.Equal(800m, report.AmountKrw); Assert.Equal("본인 이체 내역 확인", report.EvidenceReference);
        Assert.Equal("entry-1", Assert.Single(report.Allocations).EntryId); Assert.False(model.HasPending);
    }

    [Fact]
    public async Task 지급신고응답이유실되면_동일요청을조회하고_POST를다시보내지않는다()
    {
        var f = Payer(); f.Send = (_, _, _) => Task.FromException<NeighborhoodSettlementReceiptDto?>(new HttpRequestException("response lost"));
        using var model = f.Model(); await model.LoadAsync("agreement-1");
        await model.ReportPaymentAsync("entry-1", 300, "지급 진술", true);
        var command = Assert.Single(f.Commands); Assert.True(model.HasPending);
        await model.ReportPaymentAsync("entry-1", 300, "지급 진술", true); Assert.Single(f.Commands);
        f.Recovered = new() { RequestId = command.Command.RequestId, AgreementId = command.Id, Kind = command.Kind, Revision = 8 };
        f.Value.Agreement.Revision = 8; await model.RecoverAsync();
        Assert.Equal(command.Command.RequestId, f.LastReceiptId); Assert.Single(f.Commands); Assert.False(model.HasPending);
        Assert.NotNull(model.Detail); Assert.False(model.ShowWeekly);
    }

    [Fact]
    public async Task 수령자는_다른지급자의신고를만들지않고_계정교체후늦은응답은폐기한다()
    {
        var recipient = Payer(); recipient.Current = new("owner", "owner", []);
        using (var other = recipient.Model())
        { await other.LoadAsync("agreement-1"); await other.ReportPaymentAsync("entry-1", 100, "다른 지급", true); Assert.Empty(recipient.Commands); }
        var f = Payer(); var delayed = new TaskCompletionSource<NeighborhoodSettlementReceiptDto?>();
        f.Send = (_, _, _) => delayed.Task; using var model = f.Model(); await model.LoadAsync("agreement-1");
        var send = model.ReportPaymentAsync("entry-1", 100, "지급 진술", true); var command = Assert.Single(f.Commands);
        f.Current = new("changed-user", "changed-user", []); model.SynchronizeOwner();
        delayed.SetResult(new() { RequestId = command.Command.RequestId, Kind = command.Kind, AgreementId = command.Id, Revision = 8 });
        await send; Assert.False(model.HasPending); Assert.Null(model.Detail); Assert.Null(model.Weekly);
        Assert.False(model.Loaded); Assert.Equal("changed-user", model.CurrentUserId);
    }

    [Fact]
    public async Task 주간조회실패는_정상빈목록이나0원명세로보이지않는다()
    {
        var f = new 활동정산TestFixture { WeeklyRead = () => Task.FromException<WorkerWeeklyStatementDto?>(new HttpRequestException("finance unavailable")) };
        using var model = f.Model(); await model.LoadWeeklyAsync();
        Assert.True(model.ShowWeekly); Assert.False(model.Loaded); Assert.Null(model.Weekly); Assert.NotNull(model.Error);
        Assert.Empty(f.Commands); Assert.Empty(model.Items); Assert.Null(model.Detail);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("bank")]
    [InlineData("date")]
    public async Task 다른본인_실입금승격_기간외자료는_표시하지않는다(string invalid)
    {
        var value = Week();
        if (invalid == "owner") value.WorkerUserId = "other";
        if (invalid == "bank") value.ActualBankVerifiedAmountKrw = 4000;
        if (invalid == "date") value.Lines = [new() { StableId = "outside", CompletedAtUtc = value.PeriodEndUtc }];
        var f = new 활동정산TestFixture { WeeklyValue = value }; using var model = f.Model(); await model.LoadWeeklyAsync();
        Assert.Null(model.Weekly); Assert.False(model.Loaded); Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 주간명세계정교체중의늦은응답을버리고_모드를독립적으로전환한다()
    {
        var f = new 활동정산TestFixture { WeeklyValue = Week() }; using var model = f.Model();
        await model.LoadAsync("agreement-1"); await model.LoadWeeklyAsync();
        Assert.True(model.ShowWeekly); Assert.NotNull(model.Weekly); Assert.Null(model.Detail); Assert.False(model.CanAccrueReward);
        await model.LoadAsync(); Assert.False(model.ShowWeekly); Assert.Null(model.Weekly); Assert.Single(model.Items);
        var delayed = new TaskCompletionSource<WorkerWeeklyStatementDto?>(); f.WeeklyRead = () => delayed.Task;
        var read = model.LoadWeeklyAsync(); f.Current = new("other", "other", []); model.SynchronizeOwner();
        delayed.SetResult(Week()); await read;
        Assert.Null(model.Weekly); Assert.False(model.Loaded); Assert.False(model.ShowWeekly);
    }

    [Fact]
    public async Task 주간명세SSR은_미확정과보류와지급예정일을_실입금과구분한다()
    {
        var value = Week(); value.AccruedAmountKrw = null; value.UnknownAmountCount = 1; value.KnownAccruedAmountKrw = 4000;
        value.Lines = [new() { StableId = "cargo:1", SourceKind = NeighborhoodSettlementCodes.CargoRequest,
            ActivityKind = NeighborhoodSettlementCodes.Delivery, CompletedAtUtc = value.PeriodStartUtc,
            WaitingReason = "화주 수납 확인이 필요합니다.", PayoutScheduledAtUtc = value.PeriodEndUtc.AddDays(2) }];
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<WorkerWeeklyStatementPanel>(ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(WorkerWeeklyStatementPanel.Value)] = value }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        Assert.Contains("금액 미확정", html); Assert.Contains("4,000원", html); Assert.Contains("화주 수납 확인이 필요합니다", html);
        Assert.Contains("지급예정일", html); Assert.Contains("은행 입금 확인", html); Assert.Contains("확인 자료 없음", html);
        Assert.DoesNotContain("송금 완료", html); Assert.DoesNotContain("private", html);
    }

    private static 활동정산TestFixture Payer()
    {
        var f = new 활동정산TestFixture(); var entry = f.Value.Entries[0];
        entry.PayerUserId = "worker"; entry.PayeeUserId = "owner"; entry.ReportablePaymentAmountKrw = 800;
        entry.StatusCode = NeighborhoodSettlementCodes.Confirmed; entry.PaymentReleaseReady = true; entry.AllowedActions = ["report-payment"];
        return f;
    }
    private static WorkerWeeklyStatementDto Week() => new()
    {
        WorkerUserId = "worker", CompletionWeekStartKst = new(2026, 10, 7),
        PeriodStartUtc = new(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc), PeriodEndUtc = new(2026, 10, 13, 15, 0, 0, DateTimeKind.Utc), AccruedAmountKrw = 0
    };
}
