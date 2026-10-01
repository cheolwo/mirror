using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public sealed record PlatformCommunityDiagramMobileStep(
    int Number,
    원장블록노드 Node,
    string NodeClass,
    string ReadinessStyle,
    string RoleLabel,
    string ProcessingStateLabel,
    string Icon,
    string? StickerUrl,
    string StickerAltText,
    노드입력준비도 Readiness,
    IReadOnlyList<도형레이어배지> Badges,
    bool IsSelected,
    bool CanStartConnection,
    원장블록연결선? PrimaryEdge,
    string ConnectorClass,
    IReadOnlyList<원장블록연결선> ExtraEdges);
