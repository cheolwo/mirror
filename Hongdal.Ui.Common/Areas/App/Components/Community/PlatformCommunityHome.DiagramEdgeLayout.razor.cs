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
    private IReadOnlyList<원장블록노드> 정렬된원장블록노드목록가져오기(원장블록흐름도 diagram)
    {
        var sourceNodes = diagram.Nodes;
        var nodeOrder = DiagramCanvas.SynchronizeNodeOrder(sourceNodes.Select(node => node.Title));
        return nodeOrder
            .Select(title => sourceNodes.FirstOrDefault(node =>
                string.Equals(node.Title, title, StringComparison.OrdinalIgnoreCase)))
            .Where(node => node is not null)
            .Cast<원장블록노드>()
            .ToList();
    }

    private IReadOnlyList<원장블록연결선> BuildDiagramEdges(IReadOnlyList<원장블록노드> nodes)
    {
        var edges = new List<원장블록연결선>();
        if (sharedLedgerDiagramSnapshot is null)
        {
            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var from = nodes[i];
                var to = nodes[i + 1];
                var edgeId = $"default:{from.Title}->{to.Title}";
                edges.Add(new 원장블록연결선(
                    edgeId,
                    from.Title,
                    to.Title,
                    ResolveDiagramEdgeLabel(edgeId, BuildDefaultEdgeLabel(from, to)),
                    IsCustom: false,
                    DiagramConnectionHandleKind.Right,
                    DiagramConnectionHandleKind.Left,
                    ResolveDiagramEdgeStyle(edgeId, DiagramEdgeStyleKind.Curve)));
            }
        }

        var nodeTitles = nodes
            .Select(node => node.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        edges.AddRange(customDiagramEdges
            .Where(edge =>
                nodeTitles.Contains(edge.FromTitle) &&
                nodeTitles.Contains(edge.ToTitle))
            .Select(edge => edge with
            {
                Label = ResolveDiagramEdgeLabel(edge.Id, edge.Label),
                Style = ResolveDiagramEdgeStyle(edge.Id, edge.Style)
            }));

        return edges;
    }

    private string ResolveDiagramEdgeLabel(string edgeId, string fallback)
        => diagramEdgeLabels.TryGetValue(edgeId, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : fallback;

    private DiagramEdgeStyleKind ResolveDiagramEdgeStyle(string edgeId, DiagramEdgeStyleKind fallback)
        => diagramEdgeStyles.TryGetValue(edgeId, out var style) ? style : fallback;

    private static string BuildDefaultEdgeLabel(원장블록노드 from, 원장블록노드? to)
    {
        if (string.Equals(from.Kind, "form", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDiagramFormConnectionLabel(from.FormKind);
        }

        if (to is null)
        {
            var outgoingLabel = from.Title switch
            {
                "창고 출고" => "출고 품목·증빙 인계",
                "운송 상차" => "운송 이동",
                "운송 하차" => "하차 품목·증빙 인계",
                _ => null
            };
            if (outgoingLabel is not null)
            {
                return outgoingLabel;
            }

            return from.Kind switch
            {
                "sales-channel" => "주문 연동",
                "product" => "요청 조건 확정",
                "place" => "장소 확인 후",
                "warehouse" => "재고 근거 확인",
                "work" => "작업 완료 후",
                "delivery" => "전달 완료 후",
                "confirm" => "확인 후",
                "form" => "폼 제출",
                _ => "다음 단계"
            };
        }

        var boundaryLabel = (from.Title, to.Title) switch
        {
            ("입고 요청", "운송 하차") => "도착·하차 확인",
            ("포장", "창고 출고") => "출고 확정",
            ("창고 출고", "운송 상차") => "출고 품목·증빙 인계",
            ("운송 상차", "운송 하차") => "운송 이동",
            ("운송 하차", "창고 입고") => "하차 품목·증빙 인계",
            ("창고 입고", "검수/이상") => "입고 검수 시작",
            _ => null
        };
        if (boundaryLabel is not null)
        {
            return boundaryLabel;
        }

        return (from.Kind, to.Kind) switch
        {
            ("sales-channel", "product") => "주문 상품 확정",
            ("sales-channel", "warehouse") => "출고 요청",
            ("sales-channel", "work") => "처리 요청",
            ("sales-channel", "delivery") => "배송 요청",
            ("product", "sales-channel") => "판매채널 연결",
            ("product", "place") => "요청 조건 확정",
            ("product", "warehouse") => "요청 조건 확정",
            ("place", "delivery") => "상차 준비 후",
            ("warehouse", "work") => "재고 근거 확인",
            ("work", "delivery") => "작업 완료 후",
            ("delivery", "confirm") => "전달 완료 후",
            ("delivery", "place") => "도착 예정",
            ("place", "confirm") => "현장 확인 후",
            ("warehouse", "delivery") => "출고 준비 후",
            ("confirm", "delivery") => "보완 배송 요청",
            ("form", _) => "폼 제출",
            (_, "form") => "입력 요청",
            _ => "다음 단계"
        };
    }

    private static int GetDiagramRowCount(int nodeCount)
        => Math.Max(1, (int)Math.Ceiling(nodeCount / 4d));

    private const double DefaultDiagramCanvasMinHeight = 224;
    private const double DiagramModeCanvasMinHeight = 420;
    private const double DiagramNodeHalfHeight = 69;
    private const double DiagramRowSpacing = 180;
    private const double DiagramNodeHeight = DiagramNodeHalfHeight * 2;

    private static double GetDiagramHeight(
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
        => Math.Max(
            minHeight,
            170 + ((GetDiagramRowCount(nodes.Count) - 1) * DiagramRowSpacing));

    private static string BuildDiagramCanvasStyle(
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
        => $"height: {GetDiagramHeight(nodes, minHeight).ToString("0", CultureInfo.InvariantCulture)}px;";

    private static string BuildDiagramViewBox(
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
        => $"0 0 100 {GetDiagramHeight(nodes, minHeight).ToString("0", CultureInfo.InvariantCulture)}";

    private string BuildDiagramEdgePathClass(원장블록연결선 edge)
    {
        var selected = string.Equals(selectedDiagramEdgeId, edge.Id, StringComparison.OrdinalIgnoreCase)
            ? " platform-ledger-edge-path--selected"
            : string.Empty;
        var custom = edge.IsCustom ? " platform-ledger-edge-path--custom" : string.Empty;
        return $"platform-ledger-edge-path platform-ledger-edge-path--{BuildDiagramEdgeStyleKey(edge.Style)}{selected}{custom}";
    }

    private string BuildDiagramEdgeLabelClass(원장블록연결선 edge)
    {
        var selected = string.Equals(selectedDiagramEdgeId, edge.Id, StringComparison.OrdinalIgnoreCase)
            ? " platform-ledger-edge-label--selected"
            : string.Empty;
        var custom = edge.IsCustom ? " platform-ledger-edge-label--custom" : string.Empty;
        return $"platform-ledger-edge-label{selected}{custom}";
    }

    private static string BuildDiagramEdgeLabelStyle(DiagramEdgeGeometry geometry)
        => FormattableString.Invariant($"left: calc({geometry.LabelX:0.##}% - 74px); top: {geometry.LabelY:0.##}px;");

    private static string BuildDiagramEdgeStyleKey(DiagramEdgeStyleKind style)
        => style switch
        {
            DiagramEdgeStyleKind.Straight => "straight",
            DiagramEdgeStyleKind.Elbow => "elbow",
            _ => "curve"
        };

    private static string BuildDiagramHandleClass(DiagramConnectionHandleKind handle)
    {
        var role = IsDiagramOutputHandle(handle)
            ? "output"
            : IsDiagramInputHandle(handle)
                ? "input"
                : "neutral";
        return $"platform-ledger-flow-handle platform-ledger-flow-handle--{BuildDiagramHandleKey(handle)} platform-ledger-flow-handle--{role}";
    }

    private static string BuildDiagramHandleStyle(
        int nodeIndex,
        DiagramConnectionHandleKind handle,
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
    {
        var point = BuildDiagramConnectionPoint(nodeIndex, handle, nodes, minHeight);
        return FormattableString.Invariant($"left: calc({point.X:0.##}% - 8px); top: {point.Y - 8:0.##}px; right: auto; bottom: auto;");
    }

    private static string BuildDiagramHandleKey(DiagramConnectionHandleKind handle)
        => handle switch
        {
            DiagramConnectionHandleKind.Top => "top",
            DiagramConnectionHandleKind.Right => "right",
            DiagramConnectionHandleKind.Bottom => "bottom",
            DiagramConnectionHandleKind.Left => "left",
            _ => "right"
        };

    private static string BuildDiagramHandleLabel(DiagramConnectionHandleKind handle)
    {
        var direction = handle switch
        {
            DiagramConnectionHandleKind.Top => "위쪽",
            DiagramConnectionHandleKind.Right => "오른쪽",
            DiagramConnectionHandleKind.Bottom => "아래쪽",
            DiagramConnectionHandleKind.Left => "왼쪽",
            _ => "오른쪽"
        };

        var role = IsDiagramOutputHandle(handle)
            ? "출력"
            : IsDiagramInputHandle(handle)
                ? "입력"
                : "연결";

        return $"{direction} {role}";
    }

    private static bool IsDiagramOutputHandle(DiagramConnectionHandleKind handle)
        => handle is DiagramConnectionHandleKind.Right or DiagramConnectionHandleKind.Bottom;

    private static bool IsDiagramInputHandle(DiagramConnectionHandleKind handle)
        => handle is DiagramConnectionHandleKind.Left or DiagramConnectionHandleKind.Top;

    private static IReadOnlyList<DiagramConnectionHandleKind> ResolveDiagramConnectionHandles(원장블록노드 node)
        => node.ConnectionRole switch
        {
            DiagramNodeConnectionRole.WarehouseOutbound =>
            [DiagramConnectionHandleKind.Right, DiagramConnectionHandleKind.Bottom],
            DiagramNodeConnectionRole.WarehouseInbound =>
            [DiagramConnectionHandleKind.Top, DiagramConnectionHandleKind.Left],
            _ => DiagramConnectionHandles
        };

    private static bool CanStartDiagramConnection(원장블록노드 node)
        => ResolveDiagramConnectionHandles(node).Any(IsDiagramOutputHandle);

    private static bool CanStartDiagramConnection(
        원장블록노드 node,
        DiagramConnectionHandleKind handle)
        => IsDiagramOutputHandle(handle) && ResolveDiagramConnectionHandles(node).Contains(handle);

    private static bool CanCompleteDiagramConnection(
        원장블록노드 node,
        DiagramConnectionHandleKind handle)
        => IsDiagramInputHandle(handle) && ResolveDiagramConnectionHandles(node).Contains(handle);

    private static bool TryParseDiagramHandle(string? value, out DiagramConnectionHandleKind handle)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        handle = normalized switch
        {
            "top" => DiagramConnectionHandleKind.Top,
            "right" => DiagramConnectionHandleKind.Right,
            "bottom" => DiagramConnectionHandleKind.Bottom,
            "left" => DiagramConnectionHandleKind.Left,
            _ => DiagramConnectionHandleKind.Right
        };

        return normalized is "top" or "right" or "bottom" or "left";
    }

}
