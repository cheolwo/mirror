using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private bool 원장블록노드선택됨(원장블록노드 node)
        => string.Equals(선택원장블록노드제목, node.Title, StringComparison.OrdinalIgnoreCase);

    private string 원장블록노드클래스생성(
        원장블록노드 node,
        int nodeIndex,
        IReadOnlyList<원장블록노드> nodes)
    {
        var selected = 원장블록노드선택됨(node)
            ? " platform-ledger-flow-node--selected"
            : string.Empty;
        var processingState = IsDiagramLayerVisible(DiagramLayerState)
            ? $" {원장블록처리상태클래스생성(원장블록처리상태해결(nodeIndex, nodes))}"
            : string.Empty;
        var prioritySignal = 최우선도형레이어신호해결(node, nodeIndex, nodes);
        var prioritySignalClass = prioritySignal is null ? string.Empty : $" {prioritySignal.CssClass}";

        return $"platform-ledger-flow-node platform-ledger-flow-node--{node.Kind}{processingState}{prioritySignalClass}{selected}";
    }

    private string BuildMobileDiagramNodeClass(
        원장블록노드 node,
        int nodeIndex,
        IReadOnlyList<원장블록노드> nodes)
    {
        var selected = 원장블록노드선택됨(node)
            ? " platform-ledger-mobile-node--selected"
            : string.Empty;
        var state = $" {원장블록처리상태클래스생성(원장블록처리상태해결(nodeIndex, nodes))}";
        var connection = string.IsNullOrWhiteSpace(connectionStartNodeTitle)
            ? string.Empty
            : string.Equals(connectionStartNodeTitle, node.Title, StringComparison.OrdinalIgnoreCase)
                ? " platform-ledger-mobile-node--connection-source"
                : " platform-ledger-mobile-node--connection-target";

        return $"platform-ledger-mobile-node{state}{selected}{connection}";
    }

    private string BuildMobileDiagramConnectorClass(원장블록연결선? edge)
    {
        var selected = edge is not null &&
                       string.Equals(selectedDiagramEdgeId, edge.Id, StringComparison.OrdinalIgnoreCase)
            ? " platform-ledger-mobile-connector--selected"
            : string.Empty;
        var empty = edge is null ? " platform-ledger-mobile-connector--empty" : string.Empty;
        return $"platform-ledger-mobile-connector{selected}{empty}";
    }

    private IReadOnlyList<PlatformCommunityDiagramMobileStep> BuildMobileDiagramSteps(
        IReadOnlyList<원장블록노드> nodes,
        IReadOnlyList<원장블록연결선> edges)
    {
        var steps = new List<PlatformCommunityDiagramMobileStep>(nodes.Count);
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var state = 원장블록처리상태해결(index, nodes);
            var readiness = 노드입력준비도해결(node, state);
            var sticker = 원장블록노드스티커해결(node);
            var nextNode = index < nodes.Count - 1 ? nodes[index + 1] : null;
            var primaryEdge = nextNode is null
                ? null
                : edges.FirstOrDefault(edge =>
                    string.Equals(edge.FromTitle, node.Title, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(edge.ToTitle, nextNode.Title, StringComparison.OrdinalIgnoreCase));
            var extraEdges = edges
                .Where(edge =>
                    string.Equals(edge.FromTitle, node.Title, StringComparison.OrdinalIgnoreCase)
                    && (nextNode is null || !string.Equals(edge.ToTitle, nextNode.Title, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            steps.Add(new(
                index + 1,
                node,
                BuildMobileDiagramNodeClass(node, index, nodes),
                BuildNodeReadinessStyle(readiness),
                흐름노드역할라벨해결(node),
                BuildNodeProcessingStateLabel(state),
                원장블록노드아이콘해결(node),
                sticker?.이미지Url,
                sticker?.대체Text ?? node.Title,
                readiness,
                도형레이어배지목록생성(node, index, nodes),
                원장블록노드선택됨(node),
                CanStartDiagramConnection(node),
                primaryEdge,
                BuildMobileDiagramConnectorClass(primaryEdge),
                extraEdges));
        }

        return steps;
    }

    private PlatformCommunityDiagramDesktopCanvasPresentation BuildDesktopDiagramPresentation(
        IReadOnlyList<원장블록노드> nodes,
        IReadOnlyList<원장블록연결선> edges,
        double minimumHeight,
        bool useStageCanvasStyle = true)
    {
        var presentedEdges = edges
            .Select(edge => new
            {
                Edge = edge,
                Geometry = BuildDiagramEdgeGeometry(edge, nodes, minimumHeight)
            })
            .Where(item => item.Geometry is not null)
            .Select(item => new PlatformCommunityDiagramDesktopEdge(
                item.Edge,
                item.Geometry!,
                BuildDiagramEdgePathClass(item.Edge),
                BuildDiagramEdgeLabelClass(item.Edge),
                BuildDiagramEdgeLabelStyle(item.Geometry!)))
            .ToArray();
        var presentedNodes = new List<PlatformCommunityDiagramDesktopNode>(nodes.Count);
        var presentedHandles = new List<PlatformCommunityDiagramDesktopHandle>();

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var inputFields = 도형입력항목해결(node);
            var sticker = 원장블록노드스티커해결(node);
            var readiness = 노드입력준비도해결(node, 원장블록처리상태해결(index, nodes));
            var nodeStyle = $"{원장블록노드스타일생성(index, nodes, minimumHeight)} "
                + $"{BuildDiagramNodeStackStyle(node, nodes)} "
                + BuildNodeReadinessStyle(readiness);

            presentedNodes.Add(new(
                node,
                원장블록노드클래스생성(node, index, nodes),
                nodeStyle,
                IsDiagramLayerVisible(DiagramLayerRole) ? 흐름노드역할라벨해결(node) : null,
                원장블록노드아이콘해결(node),
                sticker?.이미지Url,
                sticker?.대체Text ?? node.Title,
                sticker?.표시명 ?? node.Title,
                도형입력요약제목생성(inputFields),
                readiness,
                도형레이어배지목록생성(node, index, nodes),
                원장블록노드선택됨(node)));

            foreach (var handle in ResolveDiagramConnectionHandles(node))
            {
                presentedHandles.Add(new(
                    node,
                    handle,
                    BuildDiagramHandleClass(handle),
                    $"{BuildDiagramHandleStyle(index, handle, nodes, minimumHeight)} {BuildDiagramHandleStackStyle(node, nodes)}",
                    BuildDiagramHandleKey(handle),
                    BuildDiagramHandleLabel(handle)));
            }
        }

        var previewPath = activeDiagramHandleDrag is not null && diagramDragPointer is not null
            ? BuildDiagramPreviewEdgeGeometry(activeDiagramHandleDrag, nodes, diagramDragPointer, minimumHeight)?.Path
            : null;

        var stageCanvasClass = useStageCanvasStyle
            ? " platform-ledger-flow-diagram--stage-canvas platform-ledger-desktop-canvas"
            : string.Empty;
        return new(
            BuildDiagramCanvasClass($"platform-ledger-flow-diagram platform-ledger-flow-diagram--canvas{stageCanvasClass}"),
            BuildDiagramCanvasStyle(nodes, minimumHeight),
            BuildDiagramViewBox(nodes, minimumHeight),
            presentedEdges,
            presentedNodes,
            presentedHandles,
            previewPath,
            activeDiagramHandleDrag);
    }

    private 원장블록처리상태 원장블록처리상태해결(
        int nodeIndex,
        IReadOnlyList<원장블록노드> nodes)
    {
        var ledger = 선택현재원장;
        if (ledger is null)
        {
            return 원장블록처리상태.대기;
        }

        var activeIndex = 현재원장활성노드순서해결(ledger, nodes);
        if (activeIndex >= nodes.Count)
        {
            return 원장블록처리상태.완료;
        }

        if (activeIndex < 0)
        {
            return 원장블록처리상태.대기;
        }

        if (nodeIndex < activeIndex)
        {
            return 원장블록처리상태.완료;
        }

        return nodeIndex == activeIndex
            ? 원장블록처리상태.진행중
            : 원장블록처리상태.대기;
    }

    private static string 원장블록처리상태클래스생성(원장블록처리상태 state)
        => state switch
        {
            원장블록처리상태.완료 => "platform-ledger-flow-node--state-completed",
            원장블록처리상태.진행중 => "platform-ledger-flow-node--state-active",
            _ => "platform-ledger-flow-node--state-pending"
        };

}
