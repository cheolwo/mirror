using Hongdal.Ui.Common.Areas.App.Services;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private const int DiagramNodeBaseZIndex = 10;
    private PlatformDiagramNodeStackOrder diagramNodeStackOrder => DiagramCanvas.StackOrder;

    private string BuildDiagramNodeStackStyle(
        원장블록노드 node,
        IReadOnlyList<원장블록노드> nodes)
    {
        SynchronizeDiagramNodeStackOrder(nodes);
        return FormattableString.Invariant($"z-index: {ResolveDiagramNodeZIndex(node.Title)};");
    }

    private string BuildDiagramHandleStackStyle(
        원장블록노드 node,
        IReadOnlyList<원장블록노드> nodes)
    {
        SynchronizeDiagramNodeStackOrder(nodes);
        return FormattableString.Invariant($"z-index: {ResolveDiagramNodeZIndex(node.Title) + 1};");
    }

    private bool CanBringSelectedDiagramNodeToFront()
    {
        SynchronizeDiagramNodeStackOrder(선택원장블록흐름도.Nodes);
        return DiagramCanvas.CanBringSelectedNodeToFront();
    }

    private bool CanSendSelectedDiagramNodeToBack()
    {
        SynchronizeDiagramNodeStackOrder(선택원장블록흐름도.Nodes);
        return DiagramCanvas.CanSendSelectedNodeToBack();
    }

    private void BringSelectedDiagramNodeToFront()
    {
        if (!DiagramCanvas.BringSelectedNodeToFront())
        {
            return;
        }

        diagramConnectionSeverity = MudBlazor.Severity.Info;
        diagramConnectionMessage = $"'{선택원장블록노드제목}' 노드를 맨 앞으로 보냈습니다.";
    }

    private void SendSelectedDiagramNodeToBack()
    {
        if (!DiagramCanvas.SendSelectedNodeToBack())
        {
            return;
        }

        diagramConnectionSeverity = MudBlazor.Severity.Info;
        diagramConnectionMessage = $"'{선택원장블록노드제목}' 노드를 맨 뒤로 보냈습니다.";
    }

    private void SynchronizeDiagramNodeStackOrder(IReadOnlyList<원장블록노드> nodes)
        => DiagramCanvas.SynchronizeStackOrder(nodes.Select(node => node.Title));

    private string BuildDiagramNodeStackLabel(원장블록노드 node)
    {
        SynchronizeDiagramNodeStackOrder(선택원장블록흐름도.Nodes);
        var layerIndex = DiagramCanvas.GetNodeLayerIndex(node.Title);
        if (layerIndex < 0)
        {
            return "겹침 순서 없음";
        }

        if (layerIndex == diagramNodeStackOrder.Count - 1)
        {
            return "맨 앞";
        }

        return layerIndex == 0
            ? "맨 뒤"
            : $"겹침 {layerIndex + 1}/{diagramNodeStackOrder.Count}";
    }

    private int ResolveDiagramNodeZIndex(string nodeTitle)
    {
        var layerIndex = DiagramCanvas.GetNodeLayerIndex(nodeTitle);
        return DiagramNodeBaseZIndex + (Math.Max(0, layerIndex) * 2);
    }
}
