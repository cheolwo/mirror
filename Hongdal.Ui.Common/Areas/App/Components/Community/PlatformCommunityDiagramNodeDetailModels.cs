using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public sealed record PlatformCommunityDiagramNodeDetailPresentation(
    원장블록노드 Node,
    string ProcessingStateLabel,
    Color ProcessingStateColor,
    string KindLabel,
    string? StackLabel,
    string? FormKindLabel,
    노드입력준비도 Readiness,
    string ReadinessClass,
    string ReadinessStyle,
    IReadOnlyList<KeyValuePair<string, string>> ContextValues,
    IReadOnlyList<도형입력항목> InputFields,
    string? LedgerStatusLabel,
    도형상세동작 Action,
    bool IsDiagramMode,
    bool CanBringToFront,
    bool CanSendToBack,
    bool CanRequestWarehouseProxy);

public sealed record PlatformCommunityLedgerBlockValueChange(
    string BlockCode,
    string? Value);

public sealed record PlatformCommunityDiagramFormValueChange(
    도형입력항목 Field,
    string? Value);
