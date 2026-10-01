using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private void PrepareLedgerCommunityDraft()
    {
        var template = SelectedLedgerTemplate;
        editingPostId = null;
        selectedFiles.Clear();
        form.Category = template.Category;
        form.WorkflowTag = template.WorkflowTag;
        form.RoleTag = ResolveLedgerRoleTag(template);
        form.Title = 원함입력됨
            ? $"[{template.DisplayName}] {원함제목요약()}"
            : $"[{template.DisplayName}] 생활 원장 초안";
        form.Body = Build원함포함원장본문(template);
        form.SharedLinkUrl = string.Empty;
        form.IsReportBoardPost = false;
        form.ReporterDisplayName = string.Empty;
        form.ReportedDisplayName = string.Empty;
        HomeModeState.SetWorkMode(false);
        isComposeOpen = true;
        statusSeverity = Severity.Info;
        statusMessage = 원함입력됨
            ? "원함과 홍달 지원 범위를 포함해 원장 초안을 글쓰기 폼에 채웠습니다. 부족한 조건은 참여자와 확인해 보완하세요."
            : "생활 원장 초안을 글쓰기 폼에 채웠습니다. 역할 이름과 진행 항목은 참여자 상황에 맞게 바꿔 등록하세요.";
    }

    private void PrepareLedgerCommunityDraft(string templateKey)
    {
        selectedLedgerTemplateKey = templateKey;
        PrepareLedgerCommunityDraft();
    }

    private void 현재원장다이어그램열기()
    {
        원장블록흐름도배치초기화();
        HomeModeState.SetWorkMode(false);
        DiagramPalette.SetDiagramMode(true);
        statusSeverity = Severity.Info;
        statusMessage = $"{SelectedLedgerTemplate.DisplayName} 원장 컨텍스트를 다이어그램 모드로 열었습니다.";
    }

    private void OpenSharedLedgerDiagram(PlatformCommunityPostLedgerContextResponse context)
    {
        if (context.다이어그램 is not { Nodes.Count: > 0 } diagram)
        {
            statusSeverity = Severity.Warning;
            statusMessage = "공개된 다이어그램 구조가 없습니다.";
            return;
        }

        selectedLedgerTemplateKey = context.원장템플릿Key;
        선택현재원장Id = context.원장Id;
        원장블록흐름도배치초기화();

        sharedLedgerDiagramSnapshot = NormalizeSharedLedgerDiagramSnapshot(diagram);
        diagramNodeOrder.AddRange(sharedLedgerDiagramSnapshot.Nodes.Select(node => node.Title));
        diagramNodeStackOrder.Synchronize(diagramNodeOrder);
        선택원장블록노드제목 = diagramNodeOrder.FirstOrDefault();

        var nodeTitlesById = sharedLedgerDiagramSnapshot.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeId))
            .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Title, StringComparer.OrdinalIgnoreCase);

        foreach (var edge in sharedLedgerDiagramSnapshot.Edges)
        {
            if (!nodeTitlesById.TryGetValue(edge.FromNodeId, out var fromTitle) ||
                !nodeTitlesById.TryGetValue(edge.ToNodeId, out var toTitle))
            {
                continue;
            }

            customDiagramEdges.Add(new 원장블록연결선(
                $"shared:{context.원장Id}:{edge.EdgeId}",
                fromTitle,
                toTitle,
                string.IsNullOrWhiteSpace(edge.Label) ? "연결" : edge.Label,
                IsCustom: true,
                DiagramConnectionHandleKind.Right,
                DiagramConnectionHandleKind.Left,
                DiagramEdgeStyleKind.Curve));
        }

        원장Api경로변수값["communityLedgerId"] = context.원장Id;
        원장Api경로변수값["ledgerId"] = context.원장Id;
        isComposeOpen = false;
        HomeModeState.SetWorkMode(false);
        DiagramPalette.SetDiagramMode(true);
        statusSeverity = Severity.Info;
        statusMessage = $"{context.제목} 공개 다이어그램과 대화방을 열었습니다.";
    }

    private void StartFromCompletionCase(PlatformCommunityPostLedgerContextResponse context)
    {
        selectedLedgerTemplateKey = context.원장템플릿Key;
        선택현재원장Id = null;
        sharedLedgerDiagramSnapshot = null;
        원장블록흐름도배치초기화();
        OpenWorkMode();
        statusSeverity = Severity.Success;
        statusMessage = $"{context.원장템플릿명} 새 원장 작성 화면을 열었습니다. 공개 사례의 절차를 참고해 필요한 조건만 입력하세요.";
    }

    private static DiagramSnapshotDto NormalizeSharedLedgerDiagramSnapshot(DiagramSnapshotDto diagram)
    {
        var usedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodes = new List<DiagramNodeDto>(diagram.Nodes.Count);
        foreach (var node in diagram.Nodes)
        {
            var baseTitle = string.IsNullOrWhiteSpace(node.Title) ? "공개 노드" : node.Title.Trim();
            var title = baseTitle;
            var sequence = 2;
            while (!usedTitles.Add(title))
            {
                title = $"{baseTitle} {sequence++}";
            }

            nodes.Add(new DiagramNodeDto
            {
                NodeId = node.NodeId,
                Kind = node.Kind,
                Title = title,
                GroupLabel = node.GroupLabel,
                Description = node.Description,
                X = node.X,
                Y = node.Y,
                RelatedRoute = node.RelatedRoute,
                OrganizationReferences = node.OrganizationReferences,
                Data = node.Data
            });
        }

        return new DiagramSnapshotDto
        {
            DiagramId = diagram.DiagramId,
            DiagramName = diagram.DiagramName,
            LedgerId = diagram.LedgerId,
            LedgerTemplateKey = diagram.LedgerTemplateKey,
            WorkflowModeKey = diagram.WorkflowModeKey,
            Nodes = nodes,
            Edges = diagram.Edges,
            Metadata = diagram.Metadata
        };
    }

    private void 원함분석하기()
    {
        if (!원함입력됨)
        {
            statusSeverity = Severity.Warning;
            statusMessage = "먼저 사용자가 원하는 일을 한 문장이라도 적어주세요.";
            return;
        }

        원함분석결과 = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = 원함입력.Trim(),
            Body = 원함조건입력.Trim(),
            Attributes = new Dictionary<string, string>
            {
                ["원함"] = 원함입력.Trim(),
                ["조건"] = 원함조건입력.Trim(),
                ["앱"] = AppName,
                ["역할"] = RoleLabel
            }
        });

        if (!홍달처리범위밖신호있음() &&
            !string.IsNullOrWhiteSpace(원함분석결과.PrimaryCandidate.TemplateKey))
        {
            selectedLedgerTemplateKey = 원함분석결과.PrimaryCandidate.TemplateKey;
        }

        statusSeverity = 원장화판정Severity;
        statusMessage = $"{원장화판정}: {원함추천템플릿.DisplayName} 기준으로 홍달이 도울 수 있는 범위를 정리했습니다.";
    }

    private string Build원함포함원장본문(CommunityLedgerTemplateResponse template)
    {
        var draftBody = CommunityLedgerTemplateCatalog.BuildDraftBody(template.Key, AppName, RoleLabel);

        if (!원함입력됨 && !HasAny원장블록입력)
        {
            return draftBody;
        }

        var lines = new List<string>();

        if (원함입력됨)
        {
            lines.Add("사용자가 먼저 적은 원함:");
            lines.Add($"- 원함: {원함입력.Trim()}");

            if (!string.IsNullOrWhiteSpace(원함조건입력))
            {
                lines.Add($"- 조건/참여자: {원함조건입력.Trim()}");
            }

            if (원함분석결과 is not null)
            {
                lines.Add($"- 원장화 판정: {원장화판정}");
                lines.Add($"- 추천 원장: {template.DisplayName}");
                lines.Add($"- 홍달 안내: {원함판정설명}");
            }
        }

        if (HasAny원장블록입력)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("입력된 원장 블록:");
            foreach (var block in template.LedgerBlocks)
            {
                var value = Get원장블록입력값(block.Code);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    lines.Add($"- {block.DisplayName}: {value.Trim()}");
                }
            }
        }

        lines.Add(string.Empty);
        lines.Add(draftBody);
        return string.Join(Environment.NewLine, lines);
    }

    private string Get원장블록입력값(string blockCode)
        => 원장블록입력값.TryGetValue(blockCode, out var value) ? value : string.Empty;

    private void Set원장블록입력값(string blockCode, string? value)
    {
        원장블록입력값[blockCode] = value ?? string.Empty;
        원장전송결과메시지 = null;
    }

    private string 원함제목요약()
    {
        var title = 원함입력.Trim().ReplaceLineEndings(" ");
        return title.Length <= 34 ? title : $"{title[..34]}...";
    }

    private bool 홍달처리범위밖신호있음()
    {
        if (string.IsNullOrWhiteSpace(원함전체문장))
        {
            return false;
        }

        return 홍달처리범위밖키워드.Any(keyword =>
            원함전체문장.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly IReadOnlyList<string> 홍달처리범위밖키워드 =
    [
        "법적 보장",
        "계약 보장",
        "강제 이행",
        "무조건 지급",
        "자동 결제 확정",
        "플랫폼 보증",
        "책임져",
        "대신 받아",
        "대신 소송"
    ];

}
