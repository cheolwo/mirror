using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Ui.Common.Areas.BackOffice.Components.Operations;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OperationsAlertsWorkflowR21Tests
{
    [Fact]
    public async Task 확인한알림도기본미해결목록에남는다()
    {
        var client = new Client(); using var state = State(client);
        await state.LoadAsync("alert"); await state.AcknowledgeAsync();
        Assert.Equal("Acknowledged", state.Detail!.Status);
        await state.LoadAsync(null);
        Assert.Equal("Unresolved", client.LastStatus);
        Assert.Equal("Acknowledged", Assert.Single(state.Page!.Items).Status);
        Assert.Contains("확인됨 · 미해결", await Render(state));
    }

    [Fact]
    public async Task 확인응답유실은자동재전송하지않고조회로확정한다()
    {
        var client = new Client { LoseAcknowledgement = true }; using var state = State(client);
        await state.LoadAsync("alert"); await state.AcknowledgeAsync();
        Assert.True(state.AcknowledgementOutcomeUnknown); Assert.False(state.CanAcknowledge);
        await state.AcknowledgeAsync(); Assert.Equal(1, client.Acknowledgements);
        await state.LoadAsync("alert");
        Assert.False(state.AcknowledgementOutcomeUnknown); Assert.Null(state.Error);
        Assert.Equal("Acknowledged", state.Detail!.Status); Assert.Equal(1, client.Acknowledgements);
    }

    [Fact]
    public async Task 다른알림조회실패에이전알림이남지않는다()
    {
        var client = new Client(); using var state = State(client);
        await state.LoadAsync("alert"); client.FailRead = true; await state.LoadAsync("different");
        Assert.Null(state.Detail); Assert.NotNull(state.Error); Assert.False(state.CanAcknowledge);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 같은알림조회실패는유효한이전자료만남기고명령을막는다(bool current)
    {
        var client = new Client { Row = Row() with { SourceCurrent = current } }; using var state = State(client);
        await state.LoadAsync("alert"); client.FailRead = true; await state.LoadAsync("alert");
        Assert.Equal(current, state.Detail is not null); Assert.False(state.CanAcknowledge); Assert.NotNull(state.Error);
    }

    [Fact]
    public async Task 이전계정의늦은응답과폐기뒤요청을버린다()
    {
        var pending = new TaskCompletionSource<OperationsAlertDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Pending = pending }; using var state = State(client);
        var load = state.LoadAsync("alert"); state.SetOwner("different-admin"); pending.SetResult(Row()); await load;
        Assert.Null(state.Detail); Assert.False(state.CanAcknowledge);
        state.Dispose(); state.Dispose(); await state.LoadAsync("alert"); Assert.False(state.CanAcknowledge);
    }

    [Fact]
    public async Task 다른대상의상세응답을선택알림으로사용하지않는다()
    {
        var client = new Client { Row = Row() with { Id = "other" } }; using var state = State(client);
        await state.LoadAsync("alert"); Assert.Null(state.Detail); Assert.NotNull(state.Error);
    }

    [Fact]
    public async Task 미로그인조회는통신하지않는다()
    {
        var client = new Client(); using var state = new OperationsAlertsState(client);
        await state.LoadAsync(null); await state.AcknowledgeAsync(); Assert.Equal(0, client.Reads);
    }

    [Fact]
    public void 목록필터페이지와상세복귀문맥을왕복한다()
    {
        var list = OperationsAlertWorkflowPresentation.ListHref("Acknowledged", 7);
        var detail = OperationsAlertWorkflowPresentation.DetailHref("alert / 한글", list);
        var query = QueryHelpers.ParseQuery(detail.Split('?', 2)[1]);
        Assert.Equal(list, OperationsAlertWorkflowPresentation.ResolveListHref(query["from"]));
    }

    [Theory]
    [InlineData("https://outside.invalid/")]
    [InlineData("//outside.invalid")]
    [InlineData("/operations-alerts?status=Open&status=Resolved")]
    [InlineData("/operations-alerts?page=0")]
    [InlineData("/operations-alerts?page=10001")]
    [InlineData("/operations-alerts?next=outside")]
    [InlineData("/operations-alerts#fragment")]
    public void 잘못된복귀는미해결목록으로돌아간다(string href)
        => Assert.Equal(OperationsAlertWorkflowPresentation.ListHref(), OperationsAlertWorkflowPresentation.ResolveListHref(href));

    [Fact]
    public async Task 실제공통화면에서담당자검토와원장재조회및목표시간을구분한다()
    {
        var client = new Client { Row = Row() with { Handling = new("OpenUnresolved", "사건 담당자", "사건 근거 확인", null,
            new DateTime(2026, 10, 22, 1, 2, 0, DateTimeKind.Utc), "WorkingHoursNotConfigured", "응답 6시간 · 처리 24시간 · 추가 처리 누적 72시간") } };
        using var state = State(client); await state.LoadAsync("alert"); var html = await Render(state, "alert");
        Assert.Matches("data-operator-review[^>]*>미설정", html);
        Assert.Matches("data-source-revalidation[^>]*>10.22 10:02", html);
        Assert.Contains("첫 응답 미확인 · 근무시간 기준 미설정", html);
        Assert.Contains("알림 확인은 고객 응답·문제 해결이 아닙니다", html); Assert.DoesNotContain("기한 초과", html);
    }

    [Theory]
    [InlineData("/operations", "운영 현황")]
    [InlineData("/privacy-support/case", "사건 보기")]
    [InlineData("/food-operations/orders/order", "주문 보기")]
    [InlineData("https://outside.invalid/", "업무 연결 확인 필요")]
    public void 지원되는업무링크만실제범위로표시한다(string href, string label)
    {
        var view = new OperationsAlertWorkflowPresentation(Row() with { SourceHref = href }); Assert.Equal(label, view.SourceLabel);
        if (href.StartsWith("https:", StringComparison.Ordinal)) Assert.Null(view.SourceHref);
    }

    [Fact]
    public async Task 구판응답의조회실패도공통복구안내와목록복귀를남긴다()
    {
        var client = new Client(); using var state = State(client);
        await state.LoadAsync("alert"); client.FailRead = true; await state.LoadAsync("alert");
        var html = await Render(state, "alert");
        Assert.Contains("안내 보기", html); Assert.Contains("알림 목록", html); Assert.Contains("다음 검토", html);
        Assert.Contains("미설정", html); Assert.DoesNotContain("notice error", html); Assert.DoesNotContain("private-network-detail", html);
    }

    private static OperationsAlertsState State(Client client) { var state = new OperationsAlertsState(client); state.SetOwner("admin"); return state; }
    private static OperationsAlertDto Row() => new("alert", 1, 2, "Urgent", "Open", "거래 문제 접수", "사건 확인", "/privacy-support/case", DateTime.UtcNow, DateTime.UtcNow, true, []);
    private sealed class Client : IOperationsAlertsClient
    {
        public OperationsAlertDto Row = OperationsAlertsWorkflowR21Tests.Row();
        public bool FailRead, LoseAcknowledgement;
        public int Reads, Acknowledgements;
        public string? LastStatus;
        public TaskCompletionSource<OperationsAlertDto?>? Pending;
        public Task<OperationsAlertsPage> ListAsync(string status, int page, CancellationToken ct)
        { Reads++; LastStatus = status; return Task.FromResult(new OperationsAlertsPage(true, false, [Row], page, false)); }
        public Task<OperationsAlertDto?> DetailAsync(string id, CancellationToken ct)
        { Reads++; if (FailRead) throw new HttpRequestException("private-network-detail"); return Pending?.Task ?? Task.FromResult<OperationsAlertDto?>(Row); }
        public Task<OperationsAlertDto> AcknowledgeAsync(string id, OperationsAlertAcknowledgeRequest input, CancellationToken ct)
        { Acknowledgements++; Row = Row with { Status = "Acknowledged", Revision = Row.Revision + 1 }; if (LoseAcknowledgement) throw new HttpRequestException("private-network-detail"); return Task.FromResult(Row); }
    }
    private static async Task<string> Render(OperationsAlertsState state, string? id = null)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, Js>(); services.AddSingleton<NavigationManager, Navigation>();
        using var provider = services.BuildServiceProvider(); await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<OperationsAlertsView>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["State"] = state, ["Authenticated"] = true, ["DetailId"] = id }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://r21.invalid/", "https://r21.invalid/operations-alerts");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
    private sealed class Js : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!);
    }
}
