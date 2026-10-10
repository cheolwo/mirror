using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.BackOffice.Components.PrivacySupport;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class 보호지원관리UiTests
{
    [Fact]
    public async Task 복구응답뒤처리기록미확정은재전송없이조회로만이어간다()
    {
        var client = RecoveryClient(); NeighborhoodMemberRecoveryRequest? receipt = null; var showReceipt = false;
        client.RecoveryFactory = (_, request) => Task.FromResult(RecoveryContext(request, showReceipt ? receipt : null));
        client.RecoveryCommand = (_, request) =>
        {
            receipt = request;
            return Task.FromResult(new NeighborhoodMemberRecoveryResponse { RequestId = "delivery-a", Revision = 8, StateCode = "Assigned" });
        };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        model.Summary = "사건 처리 초안";
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync();
        Assert.True(model.HasRecoveryPending); Assert.False(model.CanRetryRecovery);
        Assert.Contains("처리 기록은 아직 확인되지", model.Notice);
        model.RecoveryResolution = "CancelBeforePickup"; await model.CheckRecoveryAsync();
        var reads = client.RecoveryReads; await model.RetryRecoveryAsync();
        Assert.Equal(reads, client.RecoveryReads); Assert.Single(client.RecoveryCommands); Assert.True(model.HasRecoveryPending);
        showReceipt = true; await model.CheckRecoveryAsync();
        Assert.False(model.HasPending); Assert.False(model.RecoveryConfirmed); Assert.Single(client.RecoveryCommands);
        Assert.Equal("사건 처리 초안", model.Summary); Assert.Equal("Resume", receipt!.ResolutionCode);
        Assert.Contains("수령·정산·지급 완료는 별개", model.Notice);
    }

    [Fact]
    public async Task 사건명령미확정중에는연결배송을조회하거나변경하지않는다()
    {
        var client = RecoveryClientWithCaseActions();
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true;
        model.Action = "add-evidence"; model.PrivateEvidence = "사건 처리 입력"; client.FailCommand = true;
        await model.SubmitAsync(); var recoveryReads = client.RecoveryReads;
        Assert.True(model.HasPending); Assert.False(model.HasRecoveryPending); Assert.False(model.CanSubmitRecovery);
        await model.LoadRecoveryAsync(); await model.SubmitRecoveryAsync();
        Assert.Equal(recoveryReads, client.RecoveryReads); Assert.Empty(client.RecoveryCommands);
        Assert.Single(client.Commands); Assert.Equal("사건 처리 입력", model.PrivateEvidence);
    }

    [Fact]
    public async Task 배송복구미확정중에는사건명령과비공개열람을시작하지않는다()
    {
        var client = RecoveryClientWithCaseActions(); client.RecoveryCommand = (_, _) => throw new HttpRequestException();
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync();
        model.Action = "add-evidence"; model.PrivateEvidence = "유지할 사건 입력";
        var reads = client.Reads;
        Assert.True(model.HasRecoveryPending); Assert.False(model.CanSubmit);
        await model.SubmitAsync(); await model.VerifyRecipientsAsync(); await model.LoadAsync("case-a");
        Assert.Equal(reads, client.Reads); Assert.Equal(0, client.EvidenceReads); Assert.Empty(client.Commands);
        Assert.Single(client.RecoveryCommands); Assert.Equal("유지할 사건 입력", model.PrivateEvidence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 다른사건선택이나종료뒤늦은복구응답은기존문맥을되살리지않는다(bool dispose)
    {
        var client = RecoveryClientWithCaseActions();
        var wait = new TaskCompletionSource<NeighborhoodMemberRecoveryResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.RecoveryCommand = (_, _) => wait.Task;
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; var submit = model.SubmitRecoveryAsync();
        Assert.True(model.IsBusy); Assert.Single(client.RecoveryCommands);
        if (dispose) model.Dispose(); else await model.LoadAsync("case-b");
        var noticeBeforeLateResponse = model.Notice;
        wait.SetResult(new() { RequestId = "delivery-a", Revision = 8, StateCode = "Assigned" }); await submit;
        Assert.Null(model.Recovery); Assert.False(model.HasPending); Assert.False(model.RecoveryConfirmed);
        Assert.Equal(noticeBeforeLateResponse, model.Notice); Assert.False(model.IsBusy);
        Assert.DoesNotContain("배송 복구 요청의 처리 기록을 확인했습니다", model.Notice ?? string.Empty);
        if (dispose) Assert.Null(model.Detail); else Assert.Equal("case-b", model.Detail!.CaseId);
        Assert.Equal(2, client.RecoveryReads); // 초기 조회와 전송 전 조회까지만 수행합니다.
    }

    [Theory]
    [InlineData("request")]
    [InlineData("resolution")]
    [InlineData("revision")]
    public async Task 복구처리기록은원요청번호와결정및판본상승이모두맞아야한다(string mismatch)
    {
        var client = RecoveryClient(); var changed = false;
        client.RecoveryFactory = (_, request) => Task.FromResult(changed ? new NeighborhoodMemberRecoveryContextResponse
        {
            RequestId = "delivery-a", SupportCaseId = "case-a", StateCode = "Assigned", Revision = mismatch == "revision" ? 7 : 8,
            AppliedClientRequestId = mismatch == "request" ? Guid.NewGuid() : request.ClientRequestId,
            AppliedResolutionCode = mismatch == "resolution" ? "CancelBeforePickup" : request.ResolutionCode
        } : RecoveryContext(request));
        client.RecoveryCommand = (_, _) => { changed = true; throw new HttpRequestException(); };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync(); await model.CheckRecoveryAsync();
        Assert.False(model.HasPending); Assert.Null(model.Notice); Assert.Contains("성공은 확인되지", model.Error);
        Assert.Single(client.RecoveryCommands);
    }

    [Fact]
    public async Task 추출한배송복구화면은같은입력과미확정버튼을별도래퍼없이표시한다()
    {
        var client = RecoveryClient(); client.RecoveryCommand = (_, _) => throw new HttpRequestException();
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true;
        var selection = await RenderMemberRecoveryPanelAsync(model, showPendingResult: false);
        Assert.StartsWith("<section", selection.TrimStart()); Assert.Contains("aria-label=\"연결된 배송 복구\"", selection);
        Assert.Contains("id=\"member-recovery-resolution\"", selection); Assert.Contains("선택한 배송 처리 요청", selection);
        Assert.DoesNotContain("<main", selection); Assert.DoesNotContain("<form", selection); Assert.DoesNotContain("<div", selection);
        Assert.DoesNotContain("<fieldset disabled", selection);
        await model.SubmitRecoveryAsync(); await model.CheckRecoveryAsync();
        var pending = await RenderMemberRecoveryPanelAsync(model, showPendingResult: true);
        var blockedSelection = await RenderMemberRecoveryPanelAsync(model, showPendingResult: false);
        Assert.StartsWith("<section", pending.TrimStart()); Assert.Contains("결과 다시 확인", pending);
        Assert.Contains("같은 복구 요청 다시 확인", pending); Assert.DoesNotContain("처리 결과 확인</h2>", pending);
        Assert.Contains("<fieldset disabled", blockedSelection); Assert.Contains("수령 확인·정산·환불·지급을 완료하지 않습니다", blockedSelection);
    }

    private static Client RecoveryClientWithCaseActions()
    {
        var client = RecoveryClient();
        client.DetailFactory = id => Task.FromResult(new 보호지원CaseResponse
        {
            CaseId = id, Kind = 보호지원종류Codes.분쟁, StatusCode = "closed", SourceKind = 보호지원출처Codes.생활배송,
            SourceId = id == "case-a" ? "delivery-a" : "delivery-b", Revision = 1, AllowedActions = ["add-evidence"]
        });
        return client;
    }

    private static async Task<string> RenderMemberRecoveryPanelAsync(보호지원관리ViewModel model, bool showPendingResult)
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<MemberDeliveryRecoveryPanel>(
            ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(MemberDeliveryRecoveryPanel.Model)] = model,
                [nameof(MemberDeliveryRecoveryPanel.ShowPendingResult)] = showPendingResult
            }))).ToHtmlString()));
    }
}
