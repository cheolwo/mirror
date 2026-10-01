using Hongdal.Contracts.Common.Community;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private IReadOnlyList<도형레이어배지> 도형레이어배지목록생성(
        원장블록노드 node,
        int nodeIndex,
        IReadOnlyList<원장블록노드> nodes)
    {
        var badges = new List<도형레이어배지>();

        if (IsDiagramLayerVisible(DiagramLayerRisk))
        {
            var riskSignal = 도형리스크신호해결(node, nodeIndex, nodes);
            if (riskSignal is not null)
            {
                badges.Add(riskSignal.Badge);
            }
        }

        if (IsDiagramLayerVisible(DiagramLayerEvidence))
        {
            var evidenceSignal = 도형증빙신호해결(node, nodeIndex, nodes);
            if (evidenceSignal is not null)
            {
                badges.Add(evidenceSignal.Badge);
            }
        }

        if (IsDiagramLayerVisible(DiagramLayerState))
        {
            var state = 원장블록처리상태해결(nodeIndex, nodes);
            if (state is 원장블록처리상태.진행중)
            {
                badges.Add(레이어배지생성(
                    DiagramLayerState,
                    "현재",
                    "현재 처리 중인 단계입니다.",
                    Icons.Material.Filled.Schedule,
                    "platform-ledger-flow-node-layer-badge--state"));
            }
        }

        if (IsDiagramLayerVisible(DiagramLayerApi))
        {
            var surfaceCount = 흐름노드처리표면해결(node).Count;
            if (surfaceCount > 0)
            {
                badges.Add(레이어배지생성(
                    DiagramLayerApi,
                    $"API {surfaceCount}",
                    "이 노드와 연결되는 기존 API 처리 표면이 있습니다.",
                    Icons.Material.Filled.OpenInNew,
                    "platform-ledger-flow-node-layer-badge--api"));
            }
        }

        return badges
            .OrderByDescending(badge => badge.ConflictPriority)
            .ThenBy(badge => badge.DisplayOrder)
            .ToList();
    }

    private static string 도형입력요약라벨생성(IReadOnlyList<도형입력항목> fields)
        => fields.Count == 0 ? "입력 양식 없음" : $"입력 양식 {fields.Count}";

    private static string 도형입력요약제목생성(IReadOnlyList<도형입력항목> fields)
        => fields.Count == 0
            ? "이 노드에 제안된 입력 양식이 아직 없습니다."
            : $"입력 양식: {string.Join(", ", fields.Select(field => field.Label))}";

    private IReadOnlyList<CommunityLedgerBlockResponse> 흐름노드관련블록해결(원장블록노드 node)
        => SelectedLedgerTemplate.LedgerBlocks
            .Where(block => 흐름노드가블록과맞는가(node, block))
            .ToList();

    private IReadOnlyList<CommunityLedgerCompositionRuleResponse> 흐름노드관련규칙해결(
        원장블록노드 node,
        IReadOnlyList<CommunityLedgerBlockResponse> relatedBlocks)
    {
        var relatedRuleCodes = relatedBlocks
            .SelectMany(block => block.CompositionRuleCodes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return SelectedLedgerTemplate.CompositionRules
            .Where(rule => relatedRuleCodes.Contains(rule.Code) || 흐름노드가규칙과맞는가(node, rule))
            .ToList();
    }

    private IReadOnlyList<CommunityLedgerProcessingSurfaceResponse> 흐름노드처리표면해결(원장블록노드 node)
        => SelectedLedgerTemplate.ProcessingSurfaces
            .Where(surface => 흐름노드가처리표면과맞는가(node, surface))
            .ToList();

    private string 흐름노드블록값해결(CommunityLedgerBlockResponse block)
    {
        var value = Get원장블록입력값(block.Code);
        return string.IsNullOrWhiteSpace(value) ? "아직 입력 없음" : value.Trim();
    }

    private static bool 흐름노드가블록과맞는가(원장블록노드 node, CommunityLedgerBlockResponse block)
    {
        var text = $"{block.DisplayName} {block.UiSectionHint} {block.BlockType} {block.Purpose}";
        return node.Kind switch
        {
            "sales-channel" => block.BlockType is CommunityLedgerBlockTypes.Order
                or CommunityLedgerBlockTypes.Item
                || 판매채널성격섹션인가(text)
                || 상품성격섹션인가(text),
            "product" => block.BlockType is CommunityLedgerBlockTypes.Item
                or CommunityLedgerBlockTypes.Order
                or CommunityLedgerBlockTypes.Quantity
                || 상품성격섹션인가(text),
            "warehouse" => block.BlockType is CommunityLedgerBlockTypes.Inventory
                or CommunityLedgerBlockTypes.Place
                || 창고성격섹션인가(text),
            "work" => block.BlockType is CommunityLedgerBlockTypes.State
                or CommunityLedgerBlockTypes.Inventory
                || 작업성격섹션인가(text),
            "delivery" => block.BlockType is CommunityLedgerBlockTypes.Handoff
                or CommunityLedgerBlockTypes.Place
                || 배송성격섹션인가(text),
            "confirm" => block.BlockType is CommunityLedgerBlockTypes.Evidence
                or CommunityLedgerBlockTypes.Settlement
                or CommunityLedgerBlockTypes.State
                || 확인성격섹션인가(text),
            _ => text.Contains(node.Title, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool 흐름노드가규칙과맞는가(원장블록노드 node, CommunityLedgerCompositionRuleResponse rule)
    {
        var text = $"{rule.Title} {rule.Description} {string.Join(' ', rule.RequiredUiSectionHints)} {string.Join(' ', rule.GatedActionHints)}";
        return node.Kind switch
        {
            "sales-channel" => 판매채널성격섹션인가(text) || 상품성격섹션인가(text),
            "product" => 상품성격섹션인가(text),
            "warehouse" => 창고성격섹션인가(text),
            "work" => 작업성격섹션인가(text),
            "delivery" => 배송성격섹션인가(text),
            "confirm" => 확인성격섹션인가(text),
            _ => text.Contains(node.Title, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool 흐름노드가처리표면과맞는가(원장블록노드 node, CommunityLedgerProcessingSurfaceResponse surface)
    {
        var text = $"{surface.ApiEndpointKey} {surface.Method} {surface.RoutePattern} {surface.Purpose} {surface.ServiceHint}";
        return node.Kind switch
        {
            "sales-channel" => ContainsAny(text, "판매", "채널", "스마트스토어", "쿠팡", "스토어", "주문", "sales", "channel", "commerce", "order"),
            "product" => ContainsAny(text, "상품", "주문", "수요", "의뢰", "inventory"),
            "warehouse" => ContainsAny(text, "창고", "재고", "입고", "출고", "warehouse", "inventory"),
            "work" => ContainsAny(text, "피킹", "검수", "포장", "picking", "pack"),
            "delivery" => ContainsAny(text, "배송", "운송", "인계", "배차", "재위탁", "transport", "dispatch", "reconsignment"),
            "confirm" => ContainsAny(text, "완료", "확인", "증빙", "수령", "complete", "proof"),
            _ => text.Contains(node.Title, StringComparison.OrdinalIgnoreCase)
        };
    }
}
