using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private int 현재원장활성노드순서해결(
        현재원장컨텍스트 ledger,
        IReadOnlyList<원장블록노드> nodes)
    {
        var normalizedState = ledger.StateLabel.Trim();
        if (string.IsNullOrWhiteSpace(normalizedState))
        {
            return nodes.Count == 0 ? -1 : 0;
        }

        if (IsLedgerStateFullyCompleted(normalizedState))
        {
            return nodes.Count;
        }

        var mappedTitle = 현재원장활성노드제목해결(ledger.TemplateKey, normalizedState);
        if (!string.IsNullOrWhiteSpace(mappedTitle))
        {
            var mappedIndex = 원장블록노드순서찾기(nodes, mappedTitle);
            if (mappedIndex >= 0)
            {
                return mappedIndex;
            }
        }

        var directIndex = FindFirstStateMatchingNodeIndex(nodes, normalizedState);
        return directIndex >= 0 ? directIndex : 0;
    }

    private static bool IsLedgerStateFullyCompleted(string stateLabel)
        => ContainsAny(stateLabel, "전체 완료", "종료", "닫힘", "정산 완료", "완료됨");

    private static string? 현재원장활성노드제목해결(string templateKey, string stateLabel)
    {
        if (templateKey is CommunityLedgerTemplateKeys.CargoTransport)
        {
            return ResolveCargoTransportActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.HongdalMart)
        {
            return ResolveHongdalMartActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.WarehouseInbound)
        {
            return ResolveWarehouseInboundActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.WarehouseOutbound)
        {
            return ResolveWarehouseOutboundActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.FoodOrder)
        {
            return ResolveFoodOrderActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.FoodDelivery)
        {
            return ResolveFoodDeliveryActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.GroupPurchase)
        {
            return ResolveGroupPurchaseActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.GroupImport)
        {
            return ResolveGroupImportActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.LocalSale)
        {
            return ResolveLocalSaleActiveNodeTitle(stateLabel);
        }

        if (templateKey is CommunityLedgerTemplateKeys.Errand)
        {
            return ResolveErrandActiveNodeTitle(stateLabel);
        }

        return null;
    }

    private static string? ResolveCargoTransportActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(
                stateLabel,
                "정산",
                "결제",
                "후불",
                "환불",
                "보류",
                "분쟁",
                "완료됨",
                "전체 완료",
                "종료",
                "닫힘"))
        {
            return "결제/정산";
        }

        if (ContainsAny(stateLabel, "하차", "수령", "인수", "POD"))
        {
            return "하차";
        }

        if (ContainsAny(stateLabel, "상차", "운송 중", "이동 중", "출발"))
        {
            return "상차";
        }

        if (ContainsAny(stateLabel, "기사 확인", "기사 수락", "배차 확인", "배차 대기", "배차 추천", "추천", "운임확정"))
        {
            return "운송 의뢰";
        }

        return "운송 의뢰";
    }

    private static string? ResolveHongdalMartActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "배송", "픽업", "전달", "기사", "수령", "완료"))
        {
            return "픽업/전달";
        }

        if (ContainsAny(stateLabel, "피킹", "포장"))
        {
            return "피킹/포장";
        }

        return ContainsAny(stateLabel, "재고", "창고")
            ? "도심 재고"
            : "마트 주문";
    }

    private static string? ResolveWarehouseInboundActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "보관", "재고", "마감", "입고 완료", "완료"))
        {
            return "보관/재고화";
        }

        if (ContainsAny(stateLabel, "검수", "이상", "파손", "누락"))
        {
            return "검수/이상";
        }

        if (ContainsAny(stateLabel, "하차", "납품", "도착", "배송"))
        {
            return "운송 하차";
        }

        return ContainsAny(stateLabel, "입고 시작", "입고 대기", "인수")
            ? "창고 입고"
            : "입고 요청";
    }

    private static string? ResolveWarehouseOutboundActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "창고 입고", "입고 완료"))
        {
            return "창고 입고";
        }

        if (ContainsAny(stateLabel, "하차", "도착", "수령", "인수"))
        {
            return "운송 하차";
        }

        if (ContainsAny(stateLabel, "상차", "배송", "운송", "인계", "재위탁"))
        {
            return "운송 상차";
        }

        if (ContainsAny(stateLabel, "출고 확정", "출고 완료", "완료"))
        {
            return "창고 출고";
        }

        if (ContainsAny(stateLabel, "포장"))
        {
            return "포장";
        }

        return ContainsAny(stateLabel, "피킹", "검수", "출고")
            ? "피킹/검수"
            : "출고 요청";
    }

    private static string? ResolveFoodOrderActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "픽업대기", "준비 완료", "조리 완료", "완료", "직접 수령"))
        {
            return "준비 완료";
        }

        if (ContainsAny(stateLabel, "조리", "주방"))
        {
            return "조리";
        }

        return ContainsAny(stateLabel, "수락", "접수")
            ? "주문 수락"
            : "음식 주문";
    }

    private static string? ResolveFoodDeliveryActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "전달", "수령", "정산", "완료", "부재", "실패"))
        {
            return "전달/수령";
        }

        if (ContainsAny(stateLabel, "픽업", "이동", "배송 중", "배달 중"))
        {
            return "픽업/이동";
        }

        return ContainsAny(stateLabel, "배차", "기사", "추천", "재배차")
            ? "배차"
            : "배달 회차";
    }

    private static string? ResolveGroupPurchaseActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "정산", "수령", "마감", "완료"))
        {
            return "정산/수령";
        }

        if (ContainsAny(stateLabel, "분배", "전달", "인계", "배송"))
        {
            return "분배";
        }

        if (ContainsAny(stateLabel, "거점", "창고", "입고", "보관"))
        {
            return "수령 거점";
        }

        if (ContainsAny(stateLabel, "구매", "결정", "확정", "가격", "수량"))
        {
            return "구매 확정";
        }

        return "수요 모집";
    }

    private static string? ResolveGroupImportActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "정산", "수령", "마감", "완료"))
        {
            return "정산/수령";
        }

        if (ContainsAny(stateLabel, "분배", "국내 운송", "세대 배송"))
        {
            return "세대 분배";
        }

        if (ContainsAny(stateLabel, "3PL", "창고", "입고"))
        {
            return "3PL 입고";
        }

        if (ContainsAny(stateLabel, "통관", "반출", "HS", "관세", "검역", "보세", "검토"))
        {
            return "통관/반출";
        }

        if (ContainsAny(stateLabel, "해외", "발주", "선적", "인보이스", "패킹", "ImportInProgress"))
        {
            return "해외 선적";
        }

        if (ContainsAny(stateLabel, "수입", "결정", "확정", "FCL", "LCL", "가격", "수량", "ImportDecision", "Cancelled"))
        {
            return "수입 결정";
        }

        return "원천 공동구매";
    }

    private static string? ResolveLocalSaleActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "정산", "결제", "입금", "확인", "완료", "마감"))
        {
            return "정산/확인";
        }

        if (ContainsAny(stateLabel, "전달", "배송", "직거래", "택배", "수령"))
        {
            return "전달";
        }

        return ContainsAny(stateLabel, "보관", "준비", "예약")
            ? "보관/준비"
            : "판매/예약";
    }

    private static string? ResolveErrandActiveNodeTitle(string stateLabel)
    {
        if (ContainsAny(stateLabel, "확인", "완료", "보류", "증빙", "마감"))
        {
            return "확인/마감";
        }

        return ContainsAny(stateLabel, "진행", "수행", "작업", "참여")
            ? "수행"
            : "요청";
    }

    private static int FindFirstStateMatchingNodeIndex(
        IReadOnlyList<원장블록노드> nodes,
        string stateLabel)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (ContainsAny(stateLabel, node.Title, node.GroupLabel) ||
                ContainsAny($"{node.Title} {node.GroupLabel} {node.Description}", stateLabel))
            {
                return i;
            }
        }

        return -1;
    }

    private static int 원장블록노드순서찾기(
        IReadOnlyList<원장블록노드> nodes,
        string nodeTitle)
        => nodes
            .Select((node, index) => new { node, index })
            .FirstOrDefault(item => string.Equals(item.node.Title, nodeTitle, StringComparison.OrdinalIgnoreCase))
            ?.index ?? -1;
}
