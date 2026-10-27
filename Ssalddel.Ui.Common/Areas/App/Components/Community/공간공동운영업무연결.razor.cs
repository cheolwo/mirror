using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community;

public partial class 공간공동운영업무연결
{
    [Parameter, EditorRequired] public 공간공동운영상세Response Detail { get; set; } = new();
    [Parameter, EditorRequired] public 공간공동운영통합Response Snapshot { get; set; } = new();
    [Parameter] public string? InitialCycleId { get; set; }
    private 공간공동운영업무연결Model flow = new();
    private 공간공동운영흐름현황 progress = new();
    private string? selectedCycle;
    private string? ledgerId;
    private string? initialCycle;
    private readonly string arrowId = "work-flow-arrow-" + Guid.NewGuid().ToString("N");
    private static readonly string[] GuidePaths = ["M232 55 H254", "M488 55 H510", "M628 110 V140", "M232 197 H510", "M744 197 H766", "M884 252 V282"];
    protected override void OnParametersSet()
    {
        if (ledgerId != Detail.LedgerId || initialCycle != InitialCycleId) { ledgerId = Detail.LedgerId; initialCycle = InitialCycleId; selectedCycle = InitialCycleId; }
        RefreshProgress();
    }
    private void ChangeCycle(ChangeEventArgs args)
    {
        selectedCycle = args.Value?.ToString();
        RefreshProgress();
    }
    private void RefreshProgress() { progress = 공간공동운영흐름현황Presentation.Build(Detail, Snapshot, selectedCycle); flow = progress.Flow; }
    private string NodeSummary(공간공동운영업무노드 node)
    {
        if (node.Stage.Key == "order" && Snapshot.OrdersReadStatusCode != "current") return "주문 자료 확인 필요";
        var count = node.Sources.Count + node.Orders.Count;
        var attention = node.Sources.Count(source => !source.Verified || source.RequiresAttention);
        return count == 0 ? "연결 자료 없음" : $"자료 {count}개" + (attention > 0 ? $" · 확인 필요 {attention}개" : "");
    }
    private static string OperatingSystemName(string id) => OperatingSystemLifecycleCatalog.TryGet(id, out var lifecycle) ? lifecycle.Name : "담당 업무 영역 확인 필요";
    private static string StageName(string id, string? stageId) => OperatingSystemLifecycleCatalog.TryGet(id, out var lifecycle) ? lifecycle.Stages.FirstOrDefault(stage => stage.StageId == stageId)?.Name ?? "절차 확인 필요" : "절차 확인 필요";
}
