using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private void 원장블록노드선택(원장블록노드 node)
    {
        선택원장블록노드제목 = node.Title;
        selectedDiagramEdgeId = null;
        isDiagramEdgeOptionDockCollapsed = true;
    }

    private void 원장블록노드클릭처리(원장블록노드 node)
    {
        if (!string.IsNullOrWhiteSpace(connectionStartNodeTitle) &&
            !string.Equals(connectionStartNodeTitle, node.Title, StringComparison.OrdinalIgnoreCase))
        {
            if (AddCustomDiagramEdge(connectionStartNodeTitle, node.Title))
            {
                선택원장블록노드제목 = node.Title;
                connectionStartNodeTitle = null;
            }

            return;
        }

        원장블록노드선택(node);
        CloseNodeDetailPanel();
    }

    private async Task 원장블록노드컨텍스트메뉴처리Async(원장블록노드 node)
    {
        원장블록노드선택(node);
        OpenNodeDetailPanel(node);
        WarehouseProxy.Close();
        await Task.CompletedTask;
    }

    private void OpenNodeDetailPanel(원장블록노드 node)
    {
        nodeDetailPanelNode = node;
        selectedDiagramEdgeId = null;
        isDiagramEdgeOptionDockCollapsed = true;
        connectionStartNodeTitle = null;
    }

    private void CloseNodeDetailPanel()
    {
        nodeDetailPanelNode = null;
    }

    private async Task 노드에서창고대행신청패널열기Async()
    {
        var node = nodeDetailPanelNode ?? 선택원장블록노드;
        if (node is null)
        {
            statusSeverity = Severity.Warning;
            statusMessage = "물류 대행을 신청할 창고 노드를 먼저 선택하세요.";
            return;
        }

        if (!창고대행신청노드인가(node))
        {
            statusSeverity = Severity.Info;
            statusMessage = "물류 대행 신청은 창고 또는 재고 성격의 노드에서 열 수 있습니다.";
            return;
        }

        CloseNodeDetailPanel();
        await WarehouseProxy.OpenAsync(new(
            node.Title,
            node.GroupLabel,
            node.Description));
    }

    private void NavigateToNodeDetailPage()
    {
        var node = nodeDetailPanelNode;
        if (node is null)
        {
            return;
        }

        var action = BuildNodeDetailAction(node);
        CloseNodeDetailPanel();
        WarehouseProxy.Close();

        if (action.Url.StartsWith("/community", StringComparison.OrdinalIgnoreCase))
        {
            HomeModeState.SetWorkMode(false);
        }
        else
        {
            HomeModeState.SetWorkMode(true);
        }

        DiagramPalette.SetDiagramMode(false);
        Navigation.NavigateTo(action.Url);
    }

    private void NavigateToWarehouseProxyWorkspace(string targetUrl)
    {
        HomeModeState.SetWorkMode(true);
        DiagramPalette.SetDiagramMode(false);
        Navigation.NavigateTo(targetUrl);
    }
}
