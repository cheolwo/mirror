using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private static DiagramPoint BuildDiagramPoint(
        int nodeIndex,
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
    {
        var row = nodeIndex / 4;
        var column = nodeIndex % 4;
        var rowCount = GetDiagramRowCount(nodes.Count);
        var diagramHeight = GetDiagramHeight(nodes, minHeight);
        var occupiedHeight = DiagramNodeHeight + ((rowCount - 1) * DiagramRowSpacing);
        var firstRowCenterY = ((diagramHeight - occupiedHeight) / 2) + DiagramNodeHalfHeight;
        return new DiagramPoint(12 + (column * 25), firstRowCenterY + (row * DiagramRowSpacing));
    }

    private static string 원장블록노드스타일생성(
        int nodeIndex,
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
    {
        var point = BuildDiagramPoint(nodeIndex, nodes, minHeight);
        return FormattableString.Invariant($"left: calc({point.X:0.##}% - 78px); top: {point.Y - DiagramNodeHalfHeight:0.##}px;");
    }

    private static DiagramEdgeGeometry? BuildDiagramEdgeGeometry(
        원장블록연결선 edge,
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
    {
        var fromIndex = FindDiagramNodeIndex(nodes, edge.FromTitle);
        var toIndex = FindDiagramNodeIndex(nodes, edge.ToTitle);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            return null;
        }

        var from = BuildDiagramConnectionPoint(fromIndex, edge.FromHandle, nodes, minHeight);
        var to = BuildDiagramConnectionPoint(toIndex, edge.ToHandle, nodes, minHeight);
        return BuildDiagramEdgeGeometry(from, to, edge.Style);
    }

    private static DiagramEdgeGeometry? BuildDiagramPreviewEdgeGeometry(
        DiagramHandleDrag drag,
        IReadOnlyList<원장블록노드> nodes,
        DiagramDragPoint pointer,
        double minHeight = DefaultDiagramCanvasMinHeight)
    {
        var fromIndex = FindDiagramNodeIndex(nodes, drag.NodeTitle);
        if (fromIndex < 0)
        {
            return null;
        }

        var from = BuildDiagramConnectionPoint(fromIndex, drag.Handle, nodes, minHeight);
        var to = new DiagramPoint(pointer.X, pointer.Y);
        return BuildDiagramEdgeGeometry(from, to, DiagramEdgeStyleKind.Curve);
    }

    private static DiagramEdgeGeometry BuildDiagramEdgeGeometry(
        DiagramPoint from,
        DiagramPoint to,
        DiagramEdgeStyleKind style)
        => style switch
        {
            DiagramEdgeStyleKind.Straight => BuildStraightDiagramEdgeGeometry(from, to),
            DiagramEdgeStyleKind.Elbow => BuildElbowDiagramEdgeGeometry(from, to),
            _ => BuildCurveDiagramEdgeGeometry(from, to)
        };

    private static DiagramEdgeGeometry BuildCurveDiagramEdgeGeometry(DiagramPoint from, DiagramPoint to)
    {
        var startX = from.X;
        var endX = to.X;
        var startY = from.Y;
        var endY = to.Y;
        var midX = (startX + endX) / 2;
        var midY = (startY + endY) / 2;
        if (Math.Abs(startY - endY) < 12)
        {
            midY -= 38;
        }

        var path = FormattableString.Invariant($"M {startX:0.##} {startY:0.##} Q {midX:0.##} {midY:0.##} {endX:0.##} {endY:0.##}");
        return new DiagramEdgeGeometry(
            path,
            Math.Clamp(midX, 8, 92),
            Math.Max(12, midY - 16));
    }

    private static DiagramEdgeGeometry BuildStraightDiagramEdgeGeometry(DiagramPoint from, DiagramPoint to)
    {
        var startX = from.X;
        var endX = to.X;
        var startY = from.Y;
        var endY = to.Y;
        var midX = (startX + endX) / 2;
        var midY = (startY + endY) / 2;
        var path = FormattableString.Invariant($"M {startX:0.##} {startY:0.##} L {endX:0.##} {endY:0.##}");
        return new DiagramEdgeGeometry(
            path,
            Math.Clamp(midX, 8, 92),
            Math.Max(12, midY - 16));
    }

    private static DiagramEdgeGeometry BuildElbowDiagramEdgeGeometry(DiagramPoint from, DiagramPoint to)
    {
        var startX = from.X;
        var endX = to.X;
        var startY = from.Y;
        var endY = to.Y;
        var midX = (startX + endX) / 2;

        if (Math.Abs(startY - endY) < 12)
        {
            var routeY = Math.Max(18, Math.Min(startY, endY) - 42);
            var sameRowPath = FormattableString.Invariant(
                $"M {startX:0.##} {startY:0.##} L {startX:0.##} {routeY:0.##} L {endX:0.##} {routeY:0.##} L {endX:0.##} {endY:0.##}");
            return new DiagramEdgeGeometry(
                sameRowPath,
                Math.Clamp(midX, 8, 92),
                Math.Max(12, routeY - 16));
        }

        var path = FormattableString.Invariant(
            $"M {startX:0.##} {startY:0.##} L {midX:0.##} {startY:0.##} L {midX:0.##} {endY:0.##} L {endX:0.##} {endY:0.##}");
        return new DiagramEdgeGeometry(
            path,
            Math.Clamp(midX, 8, 92),
            Math.Max(12, ((startY + endY) / 2) - 16));
    }

    private static DiagramPoint BuildDiagramConnectionPoint(
        int nodeIndex,
        DiagramConnectionHandleKind handle,
        IReadOnlyList<원장블록노드> nodes,
        double minHeight = DefaultDiagramCanvasMinHeight)
    {
        var center = BuildDiagramPoint(nodeIndex, nodes, minHeight);
        return handle switch
        {
            DiagramConnectionHandleKind.Top => new DiagramPoint(center.X, center.Y - DiagramNodeHalfHeight),
            DiagramConnectionHandleKind.Right => new DiagramPoint(center.X + 9, center.Y),
            DiagramConnectionHandleKind.Bottom => new DiagramPoint(center.X, center.Y + DiagramNodeHalfHeight),
            DiagramConnectionHandleKind.Left => new DiagramPoint(center.X - 9, center.Y),
            _ => center
        };
    }

    private static int FindDiagramNodeIndex(IReadOnlyList<원장블록노드> nodes, string title)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (string.Equals(nodes[i].Title, title, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
