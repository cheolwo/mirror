using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private void OpenCompose()
    {
        OpenCommunityMode();
        Composer.Open();
    }

    private Task LoadLocalComposerDraftAsync()
        => Composer.LoadLocalDraftAsync();

    private async Task OpenMyLedgerComposeAsync()
    {
        OpenCompose();
        await OpenLedgerPickerAsync();
    }

    private async Task OpenLedgerPickerAsync()
    {
        OpenCommunityMode();
        isComposeOpen = true;
        LedgerPicker.Open(form.커뮤니티원장Id);
        nodeDetailPanelNode = null;
        await LoadMyLedgersAsync();
    }

    private async Task LoadMyLedgersAsync()
        => await LedgerPicker.LoadAsync();

    private async Task OpenPendingLedgerDetailAsync()
        => ApplyCommandResult(await LedgerPicker.OpenPendingDetailAsync());

    private async Task HandleLedgerPickerItemClickAsync(PlatformCommunityPostLedgerChoiceResponse ledger)
    {
        pendingLedgerId = ledger.원장Id;
        if (string.Equals(ledger.원장템플릿Key, CommunityLedgerTemplateKeys.GroupPurchase, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ledger.원장템플릿Key, CommunityLedgerTemplateKeys.GroupImport, StringComparison.OrdinalIgnoreCase))
        {
            await OpenPendingLedgerDetailAsync();
        }
    }

    private void OpenHierarchyLedgerDiagram(PlatformCommunityPostLedgerContextResponse context)
        => LedgerPicker.OpenHierarchyLedgerDiagram(context);

    private async Task RefreshLedgerDetailAsync()
        => await LedgerPicker.RefreshDetailAsync();

    private void SelectMyLedger(PlatformCommunityPostLedgerChoiceResponse ledger)
    {
        form.커뮤니티원장Id = ledger.원장Id;
        form.WorkflowTag = ledger.WorkflowTag;
        if (string.Equals(form.Category, "자유", StringComparison.OrdinalIgnoreCase))
        {
            form.Category = "생활 원장";
        }

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            form.Title = $"[{ledger.원장템플릿명}] {ledger.제목}";
        }

        if (string.IsNullOrWhiteSpace(form.Body))
        {
            form.Body = $"{ledger.제목} 원장의 진행 상황과 관련해 이야기를 나눕니다.\n\n확인하고 싶은 내용: ";
        }

        ledgerSharingSettings = null;
        statusSeverity = Severity.Success;
        statusMessage = $"'{ledger.제목}' 원장을 글에 첨부했습니다.";
    }

    private void ReturnToComposeFromLedgerPicker()
    {
        LedgerPicker.ReturnToCompose();
        isComposeOpen = true;
    }

    private void ReturnToLedgerPicker()
        => LedgerPicker.ReturnToPicker();

    private void ReturnFromLedgerDetail()
        => LedgerPicker.ReturnFromDetail();

    private void AttachPendingLedgerAndReturn()
    {
        var ledger = myLedgers.FirstOrDefault(item =>
            string.Equals(item.원장Id, pendingLedgerId, StringComparison.OrdinalIgnoreCase));
        if (ledger is null)
        {
            statusSeverity = Severity.Warning;
            statusMessage = "첨부할 원장을 다시 선택해 주세요.";
            return;
        }

        SelectMyLedger(ledger);
        isLedgerDetailOpen = false;
        isOrderLedgerHierarchyOpen = false;
        isLedgerPickerOpen = false;
        isComposeOpen = true;
        pendingLedgerId = null;
        ledgerDetailOpenedFromHierarchy = false;
        orderLedgerHierarchyContext = null;
    }

    private void ClearSelectedLedger()
    {
        form.커뮤니티원장Id = string.Empty;
        pendingLedgerId = null;
        ledgerSharingSettings = null;
        statusSeverity = Severity.Info;
        statusMessage = "글에서 원장 첨부를 해제했습니다.";
    }

    private async Task OpenLedgerSharingSettingsAsync()
        => ApplyCommandResult(
            await LedgerPicker.LoadSharingSettingsAsync(form.커뮤니티원장Id));

    private async Task SaveLedgerSharingSettingsAsync()
        => ApplyCommandResult(await LedgerPicker.SaveSharingSettingsAsync());

    private void CloseLedgerSharingSettings()
    {
        ledgerSharingSettings = null;
    }

    private async Task ReuseSharedLedgerAsync(PlatformCommunityPostLedgerContextResponse context)
    {
        var result = await LedgerPicker.ReuseSharedLedgerAsync(context.원장Id);
        ApplyCommandResult(result.Command);
        if (result.ReusedLedger is not { } reused)
        {
            return;
        }

        OpenCompose();
        form.커뮤니티원장Id = reused.원장Id;
        form.WorkflowTag = CommunityWorkClassificationCatalog.FindByLedgerTemplate(reused.원장템플릿Key)?.WorkflowTag
                           ?? form.WorkflowTag;
        if (string.IsNullOrWhiteSpace(form.Title))
        {
            form.Title = $"[{CommunityLedgerTemplateCatalog.Find(reused.원장템플릿Key).DisplayName}] {reused.제목}";
        }

        isLedgerPickerOpen = false;
    }

    private void CloseCompose()
    {
        isComposeOpen = false;
        isLedgerDetailOpen = false;
        isLedgerPickerOpen = false;
        pendingLedgerId = null;
        ledgerSharingSettings = null;
    }
}
