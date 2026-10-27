using Ssalddel.Ui.Common.Areas.App.ViewModels;
using MudBlazor;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community;

internal static class PlatformCommunityDiagramCatalog
{
    internal const string DiagramLayerStructure = "structure";
    internal const string DiagramLayerProcedure = "procedure";
    internal const string DiagramLayerRole = "role";
    internal const string DiagramLayerState = "state";
    internal const string DiagramLayerEvidence = "evidence";
    internal const string DiagramLayerRisk = "risk";
    internal const string DiagramLayerApi = "api";
    internal static IReadOnlyList<다이어그램레이어정의> All { get; } =
    [
        new(DiagramLayerStructure, "구조", "원장 노드 자체입니다. 캔버스의 기준이므로 항상 표시됩니다.", 10, 10, Icons.Material.Filled.Hub, Color.Primary, true, true),
        new(DiagramLayerProcedure, "절차", "노드 사이의 화살표와 흐름 의미입니다.", 20, 20, Icons.Material.Filled.AccountTree, Color.Secondary, true),
        new(DiagramLayerRole, "역할", "각 노드를 주로 처리하는 참여자 역할입니다.", 30, 30, Icons.Material.Filled.PushPin, Color.Info, true),
        new(DiagramLayerState, "상태", "완료, 현재 단계, 대기 상태입니다.", 40, 40, Icons.Material.Filled.Schedule, Color.Success, true),
        new(DiagramLayerEvidence, "증빙", "필수 확인, 사진, 수령, 정산 근거입니다.", 50, 50, Icons.Material.Filled.FactCheck, Color.Warning, true),
        new(DiagramLayerRisk, "리스크", "신고, 분쟁, 오류, 보류처럼 다른 표시를 덮어야 하는 신호입니다.", 60, 60, Icons.Material.Filled.HelpOutline, Color.Error, true),
        new(DiagramLayerApi, "API", "노드에서 호출 가능한 처리 표면과 기존 API 경로입니다.", 70, 35, Icons.Material.Filled.OpenInNew, Color.Info, true)
    ];
}
