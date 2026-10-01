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
    protected override Task OnInitializedAsync()
    {
        ViewModel.Configure(AppKey, ResolveRoleTag(RoleLabel));
        HomeModeState.Changed += HandleModeChanged;
        DiagramPalette.Changed += HandleDiagramPaletteChanged;
        DecorationState.Changed += HandleDecorationStateChanged;
        DiagramPalette.BlockRequested += HandlePaletteBlockRequested;
        DiagramPalette.WorkflowPresetRequested += HandleWorkflowPresetRequested;
        isWorkMode = HomeModeState.IsWorkMode;
        isCompactHomeSummary = UseCompactHomeSummary && !isWorkMode && !DiagramPalette.IsDiagramMode;
        HandlePaletteBlockRequested();
        HandleWorkflowPresetRequested();
        _ = LoadCommunityDataInStagesAsync();
        return Task.CompletedTask;
    }

    protected override void OnParametersSet()
    {
        ViewModel.Configure(AppKey, ResolveRoleTag(RoleLabel));
        ApplyRequestedBoardSelection();
        if (!BoardIndexOnly && !ListOnly)
        {
            ApplyCommunityQueryParameters();
            ApplySeedPostRouteSelection();
            if (!ApplyPendingCommunityPostDraft() && StartInComposeMode)
            {
                OpenCompose();
            }
        }
    }

    private void ApplyRequestedBoardSelection()
    {
        var requestedBoard = string.IsNullOrWhiteSpace(QueryBoardName)
            ? InitialBoard
            : QueryBoardName;
        if (string.IsNullOrWhiteSpace(requestedBoard))
        {
            return;
        }

        selectedBoardFilter = requestedBoard.Trim();
        selectedForumPostId = null;
        selectedForumSeedPostTitle = null;
        if (!string.Equals(selectedBoardFilter, "전체", StringComparison.OrdinalIgnoreCase))
        {
            form.Category = selectedBoardFilter;
        }
    }

    private void ApplySeedPostRouteSelection()
    {
        if (string.IsNullOrWhiteSpace(QuerySeedPostTitle))
        {
            return;
        }

        var requestedSeedPost = SeedPosts.FirstOrDefault(post =>
            string.Equals(post.Title, QuerySeedPostTitle, StringComparison.Ordinal));
        if (requestedSeedPost is null)
        {
            return;
        }

        selectedBoardFilter = requestedSeedPost.Category;
        selectedForumPostId = null;
        selectedForumSeedPostTitle = requestedSeedPost.Title;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && !BoardIndexOnly && !ListOnly && ApplyCommunityQueryParameters())
        {
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !BoardIndexOnly && !ListOnly)
        {
            await LoadLocalComposerDraftAsync();
        }
    }

    private bool ApplyCommunityQueryParameters()
    {
        var hasChanges = false;
        var query = ParseCommunityQueryParameters(Navigation.Uri);
        var requestedLedgerTemplateKey = string.IsNullOrWhiteSpace(QueryLedgerTemplateKey)
            ? GetCommunityQueryValue(query, "ledgerTemplate")
            : QueryLedgerTemplateKey;
        var requestedDiagramMode = string.IsNullOrWhiteSpace(QueryDiagramMode)
            ? GetCommunityQueryValue(query, "diagram")
            : QueryDiagramMode;

        if (!string.IsNullOrWhiteSpace(requestedLedgerTemplateKey) &&
            LedgerTemplates.Any(template => string.Equals(template.Key, requestedLedgerTemplateKey, StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.Equals(selectedLedgerTemplateKey, requestedLedgerTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                selectedLedgerTemplateKey = requestedLedgerTemplateKey;
                선택현재원장Id = null;
                hasChanges = true;
            }
        }

        if (IsTruthyQueryValue(requestedDiagramMode))
        {
            DiagramPalette.SetDiagramMode(true);
            hasChanges = true;
        }

        return hasChanges;
    }

    private static bool IsTruthyQueryValue(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("diagram", StringComparison.OrdinalIgnoreCase));

    private async Task LoadCommunityDataInStagesAsync()
    {
        try
        {
            await InvokeAsync(StateHasChanged);

            await LoadPostsAsync();
            if (isDisposed)
            {
                return;
            }

            await InvokeAsync(StateHasChanged);

            await LoadBoardsAsync();
            if (isDisposed)
            {
                return;
            }

            await InvokeAsync(StateHasChanged);

            if (BoardIndexOnly || ListOnly)
            {
                return;
            }

            await LoadApiEndpointMetadataAsync();
            if (!isDisposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch when (isDisposed)
        {
            // 화면이 닫힌 뒤 남은 단계 로딩 결과는 버립니다.
        }
    }

    private async Task LoadApiEndpointMetadataAsync()
    {
        isApiEndpointMetadataLoading = true;
        try
        {
            var metadata = await CommunityService.GetVersionWorkflowMetadataAsync();
            featureFlagStates.Clear();
            foreach (var flag in metadata.Flags)
            {
                featureFlagStates[flag.Key] = flag.Value;
            }

            apiEndpointMetadata.Clear();
            foreach (var endpoint in metadata.ApiEndpoints)
            {
                if (!string.IsNullOrWhiteSpace(endpoint.EndpointKey))
                {
                    apiEndpointMetadata[endpoint.EndpointKey] = endpoint;
                }
            }
        }
        catch
        {
            // API metadata는 화면 보강 정보입니다. 서버가 꺼져 있어도 원장 초안 입력은 계속 가능해야 합니다.
        }
        finally
        {
            isApiEndpointMetadataLoading = false;
        }
    }

    private void HandleModeChanged()
    {
        isWorkMode = HomeModeState.IsWorkMode;
        InvokeAsync(StateHasChanged);
    }

    private void HandleDiagramPaletteChanged()
    {
        if (DiagramPalette.IsDiagramMode)
        {
            다이어그램팔레트원장템플릿동기화();
            var firstNode = 정렬된원장블록노드목록가져오기(선택원장블록흐름도).FirstOrDefault();
            선택원장블록노드제목 ??= firstNode?.Title;
        }
        else
        {
            DiagramChat.ClosePanel();
        }

        InvokeAsync(StateHasChanged);
    }

    private void HandleDecorationStateChanged()
        => _ = InvokeAsync(StateHasChanged);

    private void 다이어그램팔레트원장템플릿동기화()
    {
        if (string.IsNullOrWhiteSpace(DiagramPalette.LedgerTemplateKey))
        {
            return;
        }

        var template = LedgerTemplates.FirstOrDefault(item =>
            string.Equals(item.Key, DiagramPalette.LedgerTemplateKey, StringComparison.OrdinalIgnoreCase));
        if (template is null ||
            string.Equals(selectedLedgerTemplateKey, template.Key, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        selectedLedgerTemplateKey = template.Key;
        선택현재원장Id = null;
        팔레트원장블록노드목록.Clear();
        diagramFormValues.Clear();
        원장블록흐름도배치초기화();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            isDisposed = true;
            HomeModeState.Changed -= HandleModeChanged;
            DiagramPalette.Changed -= HandleDiagramPaletteChanged;
            DecorationState.Changed -= HandleDecorationStateChanged;
            DiagramPalette.BlockRequested -= HandlePaletteBlockRequested;
            DiagramPalette.WorkflowPresetRequested -= HandleWorkflowPresetRequested;
        }

        base.Dispose(disposing);
    }

}
