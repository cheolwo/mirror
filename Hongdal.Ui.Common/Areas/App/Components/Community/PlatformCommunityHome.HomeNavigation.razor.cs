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
    private void OpenCommunityMode()
    {
        isCompactHomeSummary = false;
        DiagramPalette.SetDiagramMode(false);
        HomeModeState.SetWorkMode(false);
        diagramConnectionMessage = null;
    }

    private void OpenCompactHomeSummary()
    {
        isComposeOpen = false;
        isLedgerDetailOpen = false;
        isLedgerPickerOpen = false;
        isOrderLedgerHierarchyOpen = false;
        isCompactHomeSummary = UseCompactHomeSummary;
        DiagramPalette.SetDiagramMode(false);
        HomeModeState.SetWorkMode(false);
        diagramConnectionMessage = null;
    }

    private async Task 공통홈게시판열기Async()
        => await OpenBaguaAnchorAsync(workMode: false, "community-board-list");

    private void 공통홈글쓰기열기()
    {
        OpenCommunityMode();
        OpenCompose();
    }

    private async Task 공통홈베스트글열기Async(공통홈베스트글요약 베스트글)
    {
        OpenCommunityMode();
        실시간베스트글열기(베스트글);
        if (베스트글.게시글Id is long 게시글Id)
        {
            try
            {
                await LoadPostDetailAsync(게시글Id);
            }
            catch (HttpRequestException)
            {
                statusSeverity = Severity.Warning;
                statusMessage = "게시글 상세 정보를 불러오지 못했습니다.";
            }
        }

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
        var currentPage = Navigation.Uri.Split('#', 2)[0];
        Navigation.NavigateTo($"{currentPage}#community-forum-list", replace: true);
    }

    private void OpenWorkMode()
    {
        isCompactHomeSummary = false;
        isComposeOpen = false;
        isLedgerDetailOpen = false;
        isLedgerPickerOpen = false;
        DiagramPalette.SetDiagramMode(false);
        HomeModeState.SetWorkMode(true);
    }

    private IReadOnlyList<HongdalCardinalNavigationOption> EffectiveCardinalNavigationOptions
    {
        get
        {
            var inferredOptions = BuildRoleCardinalNavigationOptions();
            if (CardinalNavigationOptions.Count == 0)
            {
                return inferredOptions;
            }

            return inferredOptions
                .Select(fallback => CardinalNavigationOptions.FirstOrDefault(option =>
                    string.Equals(option.TrigramKey, fallback.TrigramKey, StringComparison.OrdinalIgnoreCase)) ?? fallback)
                .ToArray();
        }
    }

    private string EffectiveBaguaCenterSymbol
        => string.IsNullOrWhiteSpace(BaguaCenterSymbol)
            ? DecorationState.BaguaSymbol
            : BaguaCenterSymbol;

    private EventCallback BaguaCenterSelectedCallback
        => BaguaCenterOpensCommunity && !BaguaCenterShowsTaegeuk
            ? EventCallback.Factory.Create(this, HandleBaguaCenterSelectedAsync)
            : default;

    private EventCallback BaguaTaegeukYangSelectedCallback
        => BaguaCenterShowsTaegeuk
            ? EventCallback.Factory.Create(this, HandleBaguaCenterSelectedAsync)
            : default;

    private EventCallback BaguaTaegeukYinSelectedCallback
        => BaguaCenterShowsTaegeuk
            ? EventCallback.Factory.Create(this, HandleBaguaCenterStoreSelected)
            : default;

    private EventCallback PrajnaUpayaUpayaSelectedCallback
        => ShowPrajnaUpayaNavigator
            ? EventCallback.Factory.Create(this, HandlePrajnaUpayaUpayaSelected)
            : default;

    private EventCallback PrajnaUpayaPrajnaSelectedCallback
        => !string.IsNullOrWhiteSpace(PrajnaHref)
            ? EventCallback.Factory.Create(this, () => NavigateFromPrajnaUpaya(PrajnaHref))
            : default;

    private IReadOnlyList<HongdalCardinalNavigationOption> BuildRoleCardinalNavigationOptions()
    {
        var roleActions = QuickActions
            .Select(action => (Title: action.Title, Href: action.Href))
            .Concat(Workspaces.Select(workspace => (Title: workspace.Title, Href: workspace.EntryHref)))
            .Where(action => !string.IsNullOrWhiteSpace(action.Href))
            .DistinctBy(action => action.Href, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        var defaults = new[]
        {
            new HongdalCardinalNavigationOption("li", "리", "☲", "남", "판매", HongdalCardinalNavigationActionKinds.Diagram),
            new HongdalCardinalNavigationOption("dui", "태", "☱", "서", "창고", HongdalCardinalNavigationActionKinds.Work),
            new HongdalCardinalNavigationOption("kan", "감", "☵", "북", "운송", HongdalCardinalNavigationActionKinds.CommunityHome),
            new HongdalCardinalNavigationOption("zhen", "진", "☳", "동", "주문", HongdalCardinalNavigationActionKinds.Compose)
        };

        return defaults
            .Select((fallback, index) => index < roleActions.Length
                ? fallback with
                {
                    DestinationLabel = roleActions[index].Title,
                    ActionKind = HongdalCardinalNavigationActionKinds.Route,
                    Target = roleActions[index].Href
                }
                : fallback)
            .ToArray();
    }

    private async Task HandleBaguaDestinationSelectedAsync(HongdalCardinalNavigationOption option)
    {
        isBaguaNavigatorOpen = false;
        if (option.ActionKind == HongdalCardinalNavigationActionKinds.Route &&
            !string.IsNullOrWhiteSpace(option.Target))
        {
            Navigation.NavigateTo(option.Target);
            return;
        }

        switch (option.ActionKind)
        {
            case HongdalCardinalNavigationActionKinds.CommunityHome:
                await OpenBaguaAnchorAsync(workMode: false, "community-home-top");
                break;
            case HongdalCardinalNavigationActionKinds.Compose:
                OpenCompose();
                break;
            case HongdalCardinalNavigationActionKinds.Diagram:
                현재원장다이어그램열기();
                break;
            case HongdalCardinalNavigationActionKinds.Work:
                await OpenBaguaAnchorAsync(workMode: true, "community-work-panel");
                break;
        }
    }

    private Task HandleBaguaCenterTransferCompletedAsync(HongdalCardinalNavigationOption option)
    {
        isBaguaNavigatorOpen = false;
        if (UseBaguaRoleTransitionPages)
        {
            var route = string.IsNullOrWhiteSpace(BaguaPerspectiveRoleCode)
                ? BaguaRoleTransitionRoutes.BuildRolePicker(option.TrigramKey, BaguaTrigramKeys.Gen)
                : BaguaRoleTransitionRoutes.Build(
                    BaguaPerspectiveRoleCode,
                    option.TrigramKey,
                    BaguaTrigramKeys.Gen);
            Navigation.NavigateTo(route);
            return Task.CompletedTask;
        }

        Navigation.NavigateTo("/community/group-purchase");
        return Task.CompletedTask;
    }

    private async Task HandleBaguaCenterSelectedAsync()
    {
        isBaguaNavigatorOpen = false;
        await OpenBaguaAnchorAsync(workMode: false, "community-forum-list");
    }

    private void HandleBaguaCenterStoreSelected()
    {
        isBaguaNavigatorOpen = false;
        OpenDecorationStore();
    }

    private void NavigateFromPrajnaUpaya(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return;
        }

        isBaguaNavigatorOpen = false;
        Navigation.NavigateTo(href);
    }

    private void HandlePrajnaUpayaUpayaSelected()
    {
        if (!string.IsNullOrWhiteSpace(UpayaHref))
        {
            NavigateFromPrajnaUpaya(UpayaHref);
            return;
        }

        isBaguaNavigatorOpen = false;
        OpenWorkMode();
    }

    private void OpenDecorationStore()
        => Navigation.NavigateTo("/community/decorations");

    private async Task OpenBaguaAnchorAsync(bool workMode, string anchorId)
    {
        if (workMode)
        {
            OpenWorkMode();
        }
        else
        {
            OpenCommunityMode();
        }

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
        var currentPage = Navigation.Uri.Split('#', 2)[0];
        Navigation.NavigateTo($"{currentPage}#{anchorId}", replace: true);
    }

}
