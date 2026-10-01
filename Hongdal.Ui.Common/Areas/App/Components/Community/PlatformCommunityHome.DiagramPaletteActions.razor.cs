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
    private void HandlePaletteBlockRequested()
    {
        var pendingBlocks = DiagramPalette.ConsumePendingBlocks();
        if (pendingBlocks.Count == 0)
        {
            return;
        }

        foreach (var block in pendingBlocks)
        {
            AddPaletteBlockToCanvas(block);
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleWorkflowPresetRequested()
    {
        var pendingPresets = DiagramPalette.ConsumePendingWorkflowPresets();
        if (pendingPresets.Count == 0)
        {
            return;
        }

        foreach (var preset in pendingPresets)
        {
            ApplyWorkflowPresetToCanvas(preset);
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void ApplyWorkflowPresetToCanvas(PlatformDiagramWorkflowPreset preset)
    {
        if (!string.IsNullOrWhiteSpace(preset.LedgerTemplateKey))
        {
            selectedLedgerTemplateKey = preset.LedgerTemplateKey;
        }

        팔레트원장블록노드목록.Clear();
        diagramFormValues.Clear();
        diagramNodeOrder.Clear();
        diagramNodeStackOrder.Clear();
        DiagramCanvas.ResetConnections();

        var existingTitles = 원장블록흐름도생성(SelectedLedgerTemplate).Nodes
            .Select(node => node.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var block in preset.Nodes)
        {
            if (existingTitles.Contains(block.Title))
            {
                continue;
            }

            var node = new 원장블록노드(
                block.Title,
                block.GroupLabel,
                block.Description,
                원장블록종류정규화(block.Kind),
                원장블록노드색상해결(block.Kind),
                FormKind: block.FormKind);

            팔레트원장블록노드목록.Add(node);
            existingTitles.Add(node.Title);
        }

        var sourceTitles = 원장블록흐름도생성(SelectedLedgerTemplate).Nodes
            .Select(node => node.Title)
            .Concat(팔레트원장블록노드목록.Select(node => node.Title))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        diagramNodeOrder.AddRange(preset.Nodes
            .Select(node => node.Title)
            .Where(title => sourceTitles.Contains(title))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        foreach (var title in sourceTitles.Where(title => !diagramNodeOrder.Contains(title, StringComparer.OrdinalIgnoreCase)))
        {
            diagramNodeOrder.Add(title);
        }
        diagramNodeStackOrder.Synchronize(diagramNodeOrder);

        foreach (var connection in preset.Connections)
        {
            var edgeId = $"default:{connection.FromTitle}->{connection.ToTitle}";
            diagramEdgeLabels[edgeId] = string.IsNullOrWhiteSpace(connection.Label)
                ? "다음 단계"
                : connection.Label.Trim();
        }

        선택원장블록노드제목 = diagramNodeOrder.FirstOrDefault();
        statusSeverity = Severity.Success;
        statusMessage = $"{preset.Title}를 캔버스에 그렸습니다.";
    }

    private void AddPaletteBlockToCanvas(PlatformDiagramPaletteBlock block)
    {
        _ = 정렬된원장블록노드목록가져오기(선택원장블록흐름도);

        var node = new 원장블록노드(
            BuildUniquePaletteNodeTitle(block.Title),
            block.GroupLabel,
            block.Description,
            원장블록종류정규화(block.Kind),
            원장블록노드색상해결(block.Kind),
            FormKind: block.FormKind);

        팔레트원장블록노드목록.Add(node);
        diagramNodeOrder.Add(node.Title);
        diagramNodeStackOrder.Synchronize(diagramNodeOrder);
        선택원장블록노드제목 = node.Title;
        selectedDiagramEdgeId = null;
        isDiagramEdgeOptionDockCollapsed = true;
    }

    private string BuildUniquePaletteNodeTitle(string title)
    {
        var existingTitles = 원장블록흐름도생성(SelectedLedgerTemplate).Nodes
            .Select(node => node.Title)
            .Concat(팔레트원장블록노드목록.Select(node => node.Title))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingTitles.Contains(title))
        {
            return title;
        }

        var index = 2;
        var candidate = $"{title} {index}";
        while (existingTitles.Contains(candidate))
        {
            index++;
            candidate = $"{title} {index}";
        }

        return candidate;
    }

}
