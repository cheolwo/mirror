using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private void 현재원장컨텍스트불러오기(string ledgerId)
    {
        var snapshot = 현재원장스냅샷목록.FirstOrDefault(ledger =>
            string.Equals(ledger.Id, ledgerId, StringComparison.OrdinalIgnoreCase));
        if (snapshot is null)
        {
            statusSeverity = Severity.Warning;
            statusMessage = "선택한 원장을 찾지 못했습니다.";
            return;
        }

        선택현재원장Id = snapshot.Id;
        selectedLedgerTemplateKey = snapshot.TemplateKey;
        원함입력 = snapshot.Wish;
        원함조건입력 = snapshot.ConditionSummary;
        원함분석결과 = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = snapshot.Wish,
            Body = snapshot.ConditionSummary,
            UiSectionHints = snapshot.ContextValues.Keys.ToArray(),
            StateHints = [snapshot.StateLabel],
            Attributes = new Dictionary<string, string>
            {
                ["원장Id"] = snapshot.Id,
                ["상태"] = snapshot.StateLabel,
                ["요약"] = snapshot.Summary
            }
        });

        원장블록입력값.Clear();
        var template = SelectedLedgerTemplate;
        foreach (var block in template.LedgerBlocks)
        {
            var value = 현재원장블록값해결(block, snapshot);
            if (!string.IsNullOrWhiteSpace(value))
            {
                원장블록입력값[block.Code] = value;
            }
        }

        원장Api경로변수값.Clear();
        원장Api경로변수값["communityLedgerId"] = snapshot.Id;
        원장Api경로변수값["ledgerId"] = snapshot.Id;

        현재원장다이어그램불러오기(snapshot);

        statusSeverity = Severity.Success;
        statusMessage = $"'{snapshot.Title}' 원장을 현재 컨텍스트로 불러왔습니다. 원함, 블록 입력, 다이어그램, 관련 업무 정보를 이 원장 기준으로 다시 채웠습니다.";
    }

    private static string 현재원장블록값해결(
        CommunityLedgerBlockResponse block,
        현재원장컨텍스트 snapshot)
    {
        var blockText = $"{block.DisplayName} {block.UiSectionHint} {block.BlockType} {block.Purpose}";
        foreach (var pair in snapshot.ContextValues)
        {
            if (blockText.Contains(pair.Key, StringComparison.OrdinalIgnoreCase) ||
                pair.Key.Contains(block.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                block.DisplayName.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return block.BlockType switch
        {
            CommunityLedgerBlockTypes.Participant when snapshot.ContextValues.TryGetValue("참여자", out var participants) => participants,
            CommunityLedgerBlockTypes.Place when snapshot.ContextValues.TryGetValue("상차", out var pickup) => pickup,
            CommunityLedgerBlockTypes.Place when snapshot.ContextValues.TryGetValue("하차", out var dropoff) => dropoff,
            CommunityLedgerBlockTypes.Item when snapshot.ContextValues.TryGetValue("화물", out var cargo) => cargo,
            CommunityLedgerBlockTypes.Item when snapshot.ContextValues.TryGetValue("주문", out var order) => order,
            CommunityLedgerBlockTypes.Inventory when snapshot.ContextValues.TryGetValue("재고", out var inventory) => inventory,
            CommunityLedgerBlockTypes.State when snapshot.ContextValues.TryGetValue("검수", out var inspection) => inspection,
            CommunityLedgerBlockTypes.State when snapshot.ContextValues.TryGetValue("피킹", out var picking) => picking,
            CommunityLedgerBlockTypes.State when snapshot.ContextValues.TryGetValue("포장", out var packing) => packing,
            CommunityLedgerBlockTypes.Evidence when snapshot.ContextValues.TryGetValue("증빙", out var evidence) => evidence,
            CommunityLedgerBlockTypes.Settlement when snapshot.ContextValues.TryGetValue("정산", out var settlement) => settlement,
            _ => string.Empty
        };
    }

    private void 현재원장다이어그램불러오기(현재원장컨텍스트 snapshot)
    {
        원장블록흐름도배치초기화();

        var nodes = 원장블록흐름도생성(SelectedLedgerTemplate).Nodes;
        diagramNodeOrder.AddRange(nodes.Select(node => node.Title));
        선택원장블록노드제목 = diagramNodeOrder.FirstOrDefault();

        var nodeTitles = nodes
            .Select(node => node.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in snapshot.DiagramEdges.Where(edge =>
                     nodeTitles.Contains(edge.FromTitle) &&
                     nodeTitles.Contains(edge.ToTitle)))
        {
            customDiagramEdges.Add(new 원장블록연결선(
                $"loaded:{snapshot.Id}:{edge.FromTitle}->{edge.ToTitle}",
                edge.FromTitle,
                edge.ToTitle,
                edge.Label,
                IsCustom: true,
                edge.FromHandle,
                edge.ToHandle,
                edge.Style));
        }
    }

    private PlatformHomeWorkspaceProfile? 현재원장업무공간해결(CommunityLedgerTemplateResponse template)
        => UnifiedWorkspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.LedgerTemplateKey, template.Key, StringComparison.OrdinalIgnoreCase));

    private static string ResolveDiagramWorkflowModeLabel(string? workflowModeKey)
        => workflowModeKey?.Trim() switch
        {
            "cargo-v1" => "업무 흐름: 운송 1.0",
            "warehouse-inbound" => "업무 흐름: 창고 입고",
            "warehouse-outbound" => "업무 흐름: 창고 출고",
            "food-delivery" => "업무 흐름: 음식 배달",
            "mart-instant" => "업무 흐름: 알뜰살뜰 마트",
            "community" => "업무 흐름: 커뮤니티",
            _ => "업무 흐름: 공통"
        };

    private static string 원장처리체계표시명(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "처리 체계";
        }

        return value.Trim().Replace(" OS", " 처리 체계", StringComparison.OrdinalIgnoreCase);
    }

    private string 현재원장컨텍스트요약생성(CommunityLedgerTemplateResponse template)
    {
        if (선택현재원장 is { } ledger)
        {
            return $"{ledger.Title} 원장 상태를 기준으로 커뮤니티 대화, 다이어그램 블록, 업무/API 후보를 다시 채웠습니다.";
        }

        if (원함분석결과 is not null &&
            !string.Equals(원함추천템플릿.Key, template.Key, StringComparison.OrdinalIgnoreCase))
        {
            return $"원함은 {원함추천템플릿.DisplayName} 후보로도 읽힙니다. 현재는 {template.DisplayName} 기준으로 커뮤니티, 다이어그램, 업무 메뉴를 묶어 봅니다.";
        }

        return $"{template.DisplayName} 기준으로 커뮤니티 대화, 다이어그램 블록, 업무/API 후보를 같은 맥락에서 봅니다.";
    }

    private string 현재원장원함제목생성(CommunityLedgerTemplateResponse template)
    {
        if (원함입력됨)
        {
            return 원함제목요약();
        }

        return template.원함확인질문;
    }

    private string 현재원장원함상세생성(CommunityLedgerTemplateResponse template)
    {
        if (!원함입력됨)
        {
            return template.원함확인설명;
        }

        if (!string.IsNullOrWhiteSpace(원함조건입력))
        {
            return 원함조건입력.Trim();
        }

        return 원함판정설명;
    }

    private IReadOnlyList<string> 현재원장컨텍스트보완항목생성(
        CommunityLedgerTemplateResponse template,
        IReadOnlyList<원장블록노드> flowNodes,
        IReadOnlyList<원장블록연결선> diagramEdges)
    {
        var gaps = new List<string>();

        if (template.LedgerBlocks.Any(block => block.RequiredForAiJudgment) &&
            !HasAny원장블록입력)
        {
            gaps.Add("판단 블록 입력 필요");
        }

        if (상품수요원장인가(template) &&
            !flowNodes.Any(node => node.Kind.Equals("delivery", StringComparison.OrdinalIgnoreCase)))
        {
            gaps.Add("배송 블록 필요");
        }

        if (창고또는재고를사용하는가(template) &&
            !flowNodes.Any(node => node.Kind.Equals("warehouse", StringComparison.OrdinalIgnoreCase)))
        {
            gaps.Add("창고 블록 필요");
        }

        if (flowNodes.Count > 1 && diagramEdges.Count == 0)
        {
            gaps.Add("블록 연결 필요");
        }

        if (원함분석결과 is not null)
        {
            gaps.AddRange(원함추천후보.MissingRequiredSignals.Take(2).Select(signal => $"{signal} 보완"));
        }

        if (gaps.Count == 0)
        {
            gaps.Add(template.ProcessingSurfaces.Count > 0 ? "업무 호출 가능" : "커뮤니티 공유 가능");
        }

        return gaps
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    private IReadOnlyList<string> SelectedApiRouteParameterNames
        => SelectedLedgerTemplate.ProcessingSurfaces
            .SelectMany(surface => GetApiRouteParameters(surface).Select(parameter => parameter.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private 원장블록흐름도 선택원장블록흐름도
    {
        get
        {
            if (sharedLedgerDiagramSnapshot is { Nodes.Count: > 0 } sharedDiagram)
            {
                return new 원장블록흐름도(
                    sharedDiagram.Nodes.Select(ToSharedLedgerDiagramNode).ToArray(),
                    ["생성자가 공개를 허용한 다이어그램 구조만 표시합니다."]);
            }

            var diagram = 원장블록흐름도생성(SelectedLedgerTemplate);
            if (팔레트원장블록노드목록.Count == 0)
            {
                return diagram;
            }

            return diagram with
            {
                Nodes = diagram.Nodes
                    .Concat(팔레트원장블록노드목록)
                    .ToList()
            };
        }
    }

    private 원장블록노드? 선택원장블록노드
        => string.IsNullOrWhiteSpace(선택원장블록노드제목)
            ? null
            : 선택원장블록흐름도.Nodes.FirstOrDefault(node =>
                string.Equals(node.Title, 선택원장블록노드제목, StringComparison.OrdinalIgnoreCase));

}
