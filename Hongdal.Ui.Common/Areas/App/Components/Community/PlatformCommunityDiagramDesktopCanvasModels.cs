using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public sealed record PlatformCommunityDiagramDesktopEdge(
    원장블록연결선 Edge,
    DiagramEdgeGeometry Geometry,
    string PathClass,
    string LabelClass,
    string LabelStyle);

public sealed record PlatformCommunityDiagramDesktopNode(
    원장블록노드 Node,
    string CssClass,
    string Style,
    string? RoleLabel,
    string Icon,
    string? StickerUrl,
    string StickerAltText,
    string StickerTitle,
    string FormSummaryTitle,
    노드입력준비도 Readiness,
    IReadOnlyList<도형레이어배지> Badges,
    bool IsSelected);

public sealed record PlatformCommunityDiagramDesktopHandle(
    원장블록노드 Node,
    DiagramConnectionHandleKind Handle,
    string CssClass,
    string Style,
    string HandleKey,
    string Label);

public sealed record PlatformCommunityDiagramDesktopCanvasPresentation(
    string CssClass,
    string Style,
    string ViewBox,
    IReadOnlyList<PlatformCommunityDiagramDesktopEdge> Edges,
    IReadOnlyList<PlatformCommunityDiagramDesktopNode> Nodes,
    IReadOnlyList<PlatformCommunityDiagramDesktopHandle> Handles,
    string? PreviewPath,
    DiagramHandleDrag? ActiveDrag);

public sealed record PlatformCommunityDiagramHandlePointerStart(
    원장블록노드 Node,
    DiagramConnectionHandleKind Handle,
    long Button,
    DiagramDragPoint? Point);
