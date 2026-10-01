using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private void StartDiagramEdgeFromSelectedNode()
    {
        var selectedNode = 선택원장블록노드;
        if (selectedNode is null || !CanStartDiagramConnection(selectedNode))
        {
            return;
        }

        DiagramCanvas.BeginConnection(
            selectedNode.Title,
            BuildDefaultEdgeLabel(selectedNode, null),
            BuildDiagramConnectionGuidance(selectedNode));
    }

    private static 원장블록노드 ToSharedLedgerDiagramNode(DiagramNodeDto node)
        => new(
            node.Title,
            string.IsNullOrWhiteSpace(node.GroupLabel) ? "공개 원장" : node.GroupLabel,
            string.IsNullOrWhiteSpace(node.Description) ? "공개된 다이어그램 노드" : node.Description,
            string.IsNullOrWhiteSpace(node.Kind) ? "state" : node.Kind,
            ResolveSharedDiagramNodeColor(node.Kind));

    private static Color ResolveSharedDiagramNodeColor(string? kind)
        => kind?.Trim().ToLowerInvariant() switch
        {
            "order" or "request" => Color.Primary,
            "warehouse" or "inventory" => Color.Warning,
            "delivery" or "transport" => Color.Info,
            "payment" or "settlement" => Color.Success,
            _ => Color.Secondary
        };

    private void CancelDiagramEdgeCreation()
    {
        connectionStartNodeTitle = null;
        diagramConnectionMessage = null;
        CancelDiagramHandleDrag();
    }

    private bool AddCustomDiagramEdge(string fromTitle, string toTitle)
        => AddCustomDiagramEdge(
            fromTitle,
            toTitle,
            DiagramConnectionHandleKind.Right,
            DiagramConnectionHandleKind.Left);

    private bool AddCustomDiagramEdge(
        string fromTitle,
        string toTitle,
        DiagramConnectionHandleKind fromHandle,
        DiagramConnectionHandleKind toHandle)
    {
        var nodes = 선택원장블록흐름도.Nodes;
        var fromNode = nodes.FirstOrDefault(node =>
            string.Equals(node.Title, fromTitle, StringComparison.OrdinalIgnoreCase));
        var toNode = nodes.FirstOrDefault(node =>
            string.Equals(node.Title, toTitle, StringComparison.OrdinalIgnoreCase));
        if (fromNode is null ||
            toNode is null ||
            !CanStartDiagramConnection(fromNode, fromHandle) ||
            !CanCompleteDiagramConnection(toNode, toHandle))
        {
            diagramConnectionSeverity = Severity.Warning;
            diagramConnectionMessage = "선택한 연결점의 입력·출력 방향을 확인해 주세요.";
            return false;
        }

        if (!CanConnectDiagramNodes(fromNode, toNode))
        {
            diagramConnectionSeverity = Severity.Warning;
            diagramConnectionMessage = BuildDiagramFormConnectionFailureMessage(fromNode, toNode);
            return false;
        }

        var mutation = DiagramCanvas.AddOrSelectCustomEdge(
            fromTitle,
            toTitle,
            fromHandle,
            toHandle,
            newConnectionLabel);
        if (!mutation.IsNew)
        {
            diagramConnectionSeverity = Severity.Info;
            diagramConnectionMessage = $"'{fromNode.Title}'과(와) '{toNode.Title}'은 이미 연결되어 있습니다.";
            return true;
        }

        diagramConnectionSeverity = Severity.Success;
        diagramConnectionMessage = $"'{fromNode.Title}'에서 '{toNode.Title}'로 연결했습니다.";
        return true;
    }

    private void BeginDiagramHandlePointerDrag(PlatformCommunityDiagramHandlePointerStart request)
    {
        if (request.Button != 0)
        {
            return;
        }

        if (activeDiagramHandleDrag is not null)
        {
            CompleteDiagramHandleDrag(request.Node, request.Handle);
            return;
        }

        if (!CanStartDiagramConnection(request.Node, request.Handle))
        {
            return;
        }

        StartDiagramHandleDrag(request.Node, request.Handle);
        diagramDragPointer = request.Point;
    }

    private void StartDiagramHandleDrag(원장블록노드 node, DiagramConnectionHandleKind handle)
    {
        if (!CanStartDiagramConnection(node, handle))
        {
            return;
        }

        DiagramCanvas.BeginHandleDrag(
            node.Title,
            handle,
            BuildDefaultEdgeLabel(node, null),
            BuildDiagramConnectionGuidance(node));
    }

    private void CompleteDiagramHandlePointerUp(PlatformCommunityDiagramDesktopHandle handle)
        => CompleteDiagramHandleDrag(handle.Node, handle.Handle);

    private void HandleDiagramDesktopHandleClick(PlatformCommunityDiagramDesktopHandle handle)
        => HandleDiagramHandleClick(handle.Node, handle.Handle);

    private void HandleDiagramHandleClick(원장블록노드 node, DiagramConnectionHandleKind handle)
    {
        if (DiagramCanvas.ConsumeSuppressedHandleClick())
        {
            return;
        }

        if (activeDiagramHandleDrag is null)
        {
            if (!CanStartDiagramConnection(node, handle))
            {
                return;
            }

            StartDiagramHandleDrag(node, handle);
            return;
        }

        CompleteDiagramHandleDrag(node, handle);
    }

    private void HandleDiagramDragPointerChanged(DiagramDragPoint point)
    {
        if (activeDiagramHandleDrag is null)
        {
            return;
        }

        diagramHandleDragMoved = true;
        diagramDragPointer = point;
    }

    private void CompleteDiagramHandleDrop(DiagramHandleHit? hit)
    {
        if (activeDiagramHandleDrag is null)
        {
            return;
        }

        if (hit is null || !TryParseDiagramHandle(hit.Handle, out var targetHandle))
        {
            if (diagramHandleDragMoved)
            {
                CancelDiagramHandleDrag();
            }

            return;
        }

        var targetNode = 선택원장블록흐름도.Nodes.FirstOrDefault(node =>
            string.Equals(node.Title, hit.NodeTitle, StringComparison.OrdinalIgnoreCase));
        if (targetNode is null)
        {
            CancelDiagramHandleDrag();
            return;
        }

        CompleteDiagramHandleDrag(targetNode, targetHandle);
    }
    private void CompleteDiagramHandleDrag(원장블록노드 node, DiagramConnectionHandleKind targetHandle)
    {
        if (activeDiagramHandleDrag is null)
        {
            return;
        }

        var drag = activeDiagramHandleDrag;
        var completed = false;
        if (!CanCompleteDiagramConnection(node, targetHandle))
        {
            CancelDiagramHandleDrag();
            return;
        }

        if (string.Equals(drag.NodeTitle, node.Title, StringComparison.OrdinalIgnoreCase))
        {
            CancelDiagramHandleDrag();
            return;
        }

        if (!string.Equals(drag.NodeTitle, node.Title, StringComparison.OrdinalIgnoreCase))
        {
            completed = AddCustomDiagramEdge(drag.NodeTitle, node.Title, drag.Handle, targetHandle);
            if (completed)
            {
                선택원장블록노드제목 = node.Title;
            }
        }

        CancelDiagramHandleDrag();
        suppressNextDiagramHandleClick = completed;
    }

    private void CancelDiagramHandleDrag()
        => DiagramCanvas.CancelHandleDrag();

    private void SelectDiagramEdge(원장블록연결선 edge)
    {
        DiagramCanvas.SelectEdge(edge.Id);
        선택원장블록노드제목 = null;
        nodeDetailPanelNode = null;
    }

    private void ClearDiagramEdgeSelection()
        => DiagramCanvas.ClearEdgeSelection();

    private void UpdateSelectedDiagramEdgeLabel(string value)
        => DiagramCanvas.UpdateSelectedEdgeLabel(value);

    private void UpdateSelectedDiagramEdgeStyle(DiagramEdgeStyleKind style)
        => DiagramCanvas.UpdateSelectedEdgeStyle(style);

    private void DeleteSelectedDiagramEdge()
        => DiagramCanvas.DeleteSelectedEdge();

    private static string? BuildDiagramConnectionGuidance(원장블록노드 node)
        => string.Equals(node.Kind, "form", StringComparison.OrdinalIgnoreCase)
            ? $"'{node.Title}' 연결 대상: {PlatformDiagramFormNodeCatalog.GetConnectionRule(node.FormKind).Description}"
            : null;

}
