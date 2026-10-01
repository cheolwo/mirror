using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public sealed record 도형상세동작(
        string Url,
        string Description,
        string Icon,
        Color Color);

public sealed record 도형레이어배지(
        string LayerKey,
        string Label,
        string Description,
        string Icon,
        int DisplayOrder,
        int ConflictPriority,
        string CssClass);

public sealed record 도형레이어신호(
        string LayerKey,
        int DisplayOrder,
        int ConflictPriority,
        string CssClass,
        도형레이어배지 Badge,
        bool ShouldEmphasizeNode);

public sealed record 도형입력항목(
        string Label,
        string InputType,
        string Description,
        bool IsRequired);

public sealed record 노드입력준비도(
        int Percent,
        int CompletedCount,
        int TrackedCount,
        bool TracksRequiredBlocks,
        IReadOnlyList<string> MissingBlockNames,
        IReadOnlyList<CommunityLedgerBlockResponse> TrackedBlocks)
    {
        public string CountLabel => TrackedCount == 0
            ? "상태 기준"
            : $"{(TracksRequiredBlocks ? "필수" : "입력")} {CompletedCount}/{TrackedCount}";

        public string GuidanceLabel => MissingBlockNames.Count switch
        {
            0 when Percent >= 100 => "필요한 정보가 모두 준비됐어요",
            0 => "처리 상태를 따라 준비도를 표시해요",
            1 => $"{MissingBlockNames[0]} 입력이 남았어요",
            _ => $"{MissingBlockNames.Count}개 입력이 남았어요"
        };
    }

public enum 원장블록처리상태
    {
        대기,
        진행중,
        완료
    }

public sealed record 현재원장컨텍스트(
        string Id,
        string Title,
        string TemplateKey,
        string StateLabel,
        string LastUpdatedLabel,
        string Wish,
        string ConditionSummary,
        string Summary,
        IReadOnlyDictionary<string, string> ContextValues)
    {
        public IReadOnlyList<현재원장컨텍스트연결선> DiagramEdges { get; init; } = [];
    }

public sealed record 현재원장컨텍스트연결선(
        string FromTitle,
        string ToTitle,
        string Label,
        DiagramConnectionHandleKind FromHandle = DiagramConnectionHandleKind.Right,
        DiagramConnectionHandleKind ToHandle = DiagramConnectionHandleKind.Left,
        DiagramEdgeStyleKind Style = DiagramEdgeStyleKind.Curve);

public sealed record 원장블록흐름도(
        IReadOnlyList<원장블록노드> Nodes,
        IReadOnlyList<string> Rules);

public sealed record 원장블록노드(
        string Title,
        string GroupLabel,
        string Description,
        string Kind,
        Color Color,
        string? Condition = null,
        string? 스티커이미지Key = null,
        DiagramNodeConnectionRole ConnectionRole = DiagramNodeConnectionRole.Standard,
        string? FormKind = null);

public sealed record DiagramPoint(double X, double Y);

public sealed record DiagramHandleHit(string NodeTitle, string Handle);

public sealed record DiagramEdgeGeometry(
    string Path,
    double LabelX,
    double LabelY);
