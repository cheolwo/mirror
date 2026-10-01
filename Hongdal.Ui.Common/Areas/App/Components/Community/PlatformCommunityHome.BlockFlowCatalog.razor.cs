using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private static 원장블록흐름도 원장블록흐름도생성(CommunityLedgerTemplateResponse template)
    {
        var nodes = template.Key switch
        {
            CommunityLedgerTemplateKeys.HongdalMart =>
            [
                상품노드("마트 주문", "주문 블록", "주문 상품, 수량, 수령 조건을 먼저 구조화합니다."),
                창고노드("도심 재고", "재고 거점 블록", "가까운 창고의 재고 근거와 예약 수량을 잡습니다."),
                작업노드("피킹/포장", "피킹·포장 블록", "재고를 꺼내고 포장 완료 상태를 한 노드에서 처리합니다.", "포장 완료 뒤 픽업과 전달이 열립니다."),
                배송노드("픽업/전달", "기사 픽업/고객 전달", "기사 픽업, 이동, 고객 전달, 수령 증빙을 한 노드에서 닫습니다.")
            ],
            CommunityLedgerTemplateKeys.WarehouseOutbound =>
            [
                상품노드("출고 요청", "출고 품목 블록", "출고할 상품, 수량, 목적, 도착 조건을 먼저 정리합니다."),
                작업노드("피킹/검수", "피킹·검수 블록", "창고 안에서 찾고 수량과 상태를 확인하는 단계를 묶습니다."),
                작업노드("포장", "포장 블록", "포장 완료, 라벨, 송장, 출고 가능 상태를 처리합니다.", "포장 완료 뒤 창고 출고가 열립니다."),
                창고노드("창고 출고", "출고 경계 블록", "확정된 출고 품목을 운송 원장으로 넘기는 경계입니다. 오른쪽 또는 아래쪽 출력으로만 연결합니다.", connectionRole: DiagramNodeConnectionRole.WarehouseOutbound),
                배송노드("운송 상차", "운송 상차 블록", "창고 출고 품목과 증빙을 입력으로 받아 상차 완료와 출발 가능 상태를 기록합니다."),
                배송노드("운송 하차", "운송 하차 블록", "이동을 마친 품목과 하차 증빙을 도착 창고의 입고 입력으로 넘깁니다."),
                창고노드("창고 입고", "입고 경계 블록", "운송 하차 품목과 증빙을 받아 입고 검수로 넘기는 경계입니다. 왼쪽 또는 위쪽 입력으로만 연결합니다.", connectionRole: DiagramNodeConnectionRole.WarehouseInbound)
            ],
            CommunityLedgerTemplateKeys.WarehouseInbound =>
            [
                상품노드("입고 요청", "입고 예정 블록", "입고될 상품, 수량, 납품 조건을 먼저 정리합니다."),
                배송노드("운송 하차", "운송 하차 블록", "연결된 운송 원장이 있으면 도착, 하차, 지연과 인수 증빙을 함께 확인합니다.", "운송 원장이 없는 직접 납품에서는 도착 확인 단계로 사용합니다."),
                창고노드("창고 입고", "입고 경계 블록", "운송 하차 또는 직접 납품 내용을 입력으로 받아 입고 검수를 엽니다. 왼쪽 또는 위쪽 입력으로만 연결합니다.", connectionRole: DiagramNodeConnectionRole.WarehouseInbound),
                작업노드("검수/이상", "검수·이상 블록", "수량, 파손, 누락, 보완 조치를 한 노드에서 확인합니다."),
                창고노드("보관/재고화", "보관 위치/재고 전환", "검수 뒤 보관 위치를 잡고 실제 재고로 전환합니다.")
            ],
            CommunityLedgerTemplateKeys.LocalSale =>
            [
                상품노드("판매/예약", "판매 물건/예약", "판매 물건, 예약 조건, 구매 의향을 정리합니다."),
                창고노드("보관/준비", "판매자 보관 또는 창고", "상품 보관 위치와 전달 전 준비 상태를 확인합니다."),
                배송노드("전달", "직거래/배송 블록", "직거래, 택배, 가까운 배송을 한 노드에서 처리합니다."),
                확인노드("정산/확인", "확인/정산 표시", "수령 확인, 입금 표시, 거래 마감 메모를 남깁니다.")
            ],
            CommunityLedgerTemplateKeys.GroupPurchase =>
            [
                상품노드("수요 모집", "공동구매 수요 블록", "참여자별 개별 주문, 희망 수량, 희망가와 공동 조건을 모읍니다."),
                작업노드("구매 확정", "공동 조건·구매 결정", "모인 수요와 투표 근거를 보고 구매 수량, 가격과 구매처를 확정합니다."),
                창고노드("수령 거점", "공동 수령·보관 거점", "구매 상품을 받을 공동 거점과 참여자별 보관·인수 조건을 정합니다."),
                배송노드("분배", "참여자 분배 블록", "거점 수령, 참여자별 분배와 개별 수령 확인을 한 노드에서 처리합니다."),
                확인노드("정산/수령", "정산 표시·수령 확인", "입금 표시, 수령 확인과 공동구매 마감 메모를 남깁니다.")
            ],
            CommunityLedgerTemplateKeys.GroupImport =>
            [
                상품노드("원천 공동구매", "공동구매 원장 연결", "확정된 공동구매 원장과 수량 합계를 수입의 원천 근거로 연결합니다."),
                작업노드("수입 결정", "수량·가격·방식 결정", "모인 수요를 보고 수입 진행 여부, FCL/LCL, 공급자 조건을 정합니다."),
                작업노드("해외 선적", "해외 발주·선적 블록", "발주, 인보이스, 패킹리스트, B/L 또는 AWB 추적을 묶습니다."),
                작업노드("통관/반출", "통관 상태·국내 반출 블록", "HS 코드, 문서관리번호, 통관 상태, 반출 가능 조건을 한 노드에서 봅니다.", "통관과 반출 조건이 확인되어야 국내 분배가 열립니다."),
                창고노드("3PL 입고", "창고 입고 블록", "국내 3PL 창고에 입고할지, 바로 세대 분배할지 판단할 재고 근거를 잡습니다."),
                배송노드("세대 분배", "세대 배송·거점 분배 블록", "국내 운송, 세대 배송, 거점 분배, 수령 확인을 한 노드에서 넘깁니다."),
                확인노드("정산/수령", "정산 표시·수령 확인", "입금 표시, 수령 확인, 분배 마감 메모를 남깁니다.")
            ],
            CommunityLedgerTemplateKeys.FoodOrder =>
            [
                상품노드("음식 주문", "메뉴/주문 블록", "메뉴, 수량, 주문자와 수령 방식을 먼저 정리합니다."),
                작업노드("주문 수락", "음식점 수락 블록", "주문 가능 여부, 품절 변경, 예상 준비 시간을 확인합니다."),
                작업노드("조리", "조리 상태 블록", "조리 시작부터 준비 완료까지 주문 원장 안에서 추적합니다."),
                확인노드("준비 완료", "수령 방식/배달 인계", "직접 수령이면 주문 원장을 닫고, 배달이 필요하면 분할·재배달을 포함해 별도 배달 원장을 필요한 수만큼 엽니다.")
            ],
            CommunityLedgerTemplateKeys.FoodDelivery =>
            [
                상품노드("배달 회차", "원주문/분할 항목 블록", "원주문, 배달 회차, 이번에 전달할 메뉴와 수량, 재배달 사유를 정리합니다."),
                작업노드("배차", "기사 배정 블록", "이 배달 회차만을 위한 기사 추천, 배정, 재배차 상태를 확인합니다."),
                배송노드("픽업/이동", "픽업·이동 블록", "해당 회차의 픽업 도착, 픽업 완료, 이동 중 상태를 추적합니다."),
                확인노드("전달/수령", "전달 증빙/배달비 정산", "해당 회차의 전달 결과, 수령 증빙, 실패 사유와 배달비 정산 표시를 남깁니다.")
            ],
            CommunityLedgerTemplateKeys.Errand =>
            [
                상품노드("요청", "요청 내용 블록", "도움이 필요한 일, 시간, 장소, 조건을 먼저 정리합니다."),
                작업노드("수행", "진행 상태 블록", "참여, 진행 중, 지연, 보류 같은 수행 상태를 한 노드에서 봅니다."),
                확인노드("확인/마감", "확인/증빙 블록", "완료 확인, 증빙, 후속 메모를 남겨 생활 요청을 닫습니다.")
            ],
            CommunityLedgerTemplateKeys.CargoTransport =>
            [
                상품노드("운송 의뢰", "의뢰/배차 블록", "화물, 상하차지, 시간, 운임 조건과 배차 진입 조건을 정리합니다."),
                확인노드("상차", "상차 블록", "상차지 도착, 상차 완료, 상차 증빙을 한 노드 안에서 처리합니다."),
                확인노드("하차", "하차 블록", "하차지 도착, 하차 완료, 수령/인수 확인을 한 노드 안에서 처리합니다."),
                확인노드("결제/정산", "결제/정산 블록", "결제 확보, 후불 승인, 정산대기, 보류, 완료, 환불 상태를 한 노드 안에서 표시합니다.")
            ],
            _ => 기본원장블록노드생성(template)
        };

        return new 원장블록흐름도(nodes, 원장블록흐름규칙생성(template));
    }

    private static IReadOnlyList<string> 원장블록흐름규칙생성(CommunityLedgerTemplateResponse template)
    {
        var rules = new List<string>();

        if (상품수요원장인가(template))
        {
            rules.Add("상품을 원하는 원장은 상품/주문 블록, 창고 또는 재고 근거 블록, 배송/전달 블록이 함께 있어야 완결됩니다.");
            rules.Add("판매자 창고인지 대행업체 창고인지는 나중에 정해져도 되지만, 어떤 창고/재고에서 나가는지는 반드시 연결해야 합니다.");
        }

        if (창고또는재고를사용하는가(template))
        {
            rules.Add("창고 간 이동은 창고 출고 -> 운송 상차 -> 운송 하차 -> 창고 입고 순서로 연결합니다.");
            rules.Add("창고 출고는 오른쪽·아래쪽 출력만, 창고 입고는 왼쪽·위쪽 입력만 사용하며 운송 하차의 품목과 증빙을 입고의 근거로 넘깁니다.");
        }

        if (string.Equals(template.Key, CommunityLedgerTemplateKeys.GroupPurchase, StringComparison.OrdinalIgnoreCase))
        {
            rules.Add("공동구매는 개별 주문 연결 -> 수요·조건 확정 -> 구매 확정 -> 수령 거점 -> 참여자 분배 순서로 배치합니다.");
            rules.Add("해외 선적이나 통관이 필요한 경우 공동구매 원장에 상태를 섞지 않고 별도의 공동수입 원장을 연결합니다.");
        }

        if (string.Equals(template.Key, CommunityLedgerTemplateKeys.GroupImport, StringComparison.OrdinalIgnoreCase))
        {
            rules.Add("공동수입은 원천 공동구매 연결 -> 수입 결정 -> 해외 선적 -> 통관/반출 -> 3PL 입고 또는 세대 분배 순서로 배치합니다.");
            rules.Add("통관 상태와 국내 반출 조건이 확인되기 전에는 3PL 입고, 국내 운송, 세대 분배 노드를 실제 처리 단계로 열지 않습니다.");
        }

        if (string.Equals(template.Key, CommunityLedgerTemplateKeys.FoodOrder, StringComparison.OrdinalIgnoreCase))
        {
            rules.Add("음식 주문 원장은 준비 완료까지를 담당하며 픽업, 이동, 전달 상태를 직접 포함하지 않습니다.");
            rules.Add("직접 수령이면 배달 원장이 없고, 분할 배달이나 재배달이 필요하면 하나의 주문에 여러 음식 배달 원장을 연결합니다.");
        }

        if (string.Equals(template.Key, CommunityLedgerTemplateKeys.FoodDelivery, StringComparison.OrdinalIgnoreCase))
        {
            rules.Add("음식 배달 원장 하나는 한 번의 배달 회차를 나타내며 원주문, 분할 항목, 배달 회차를 함께 기록합니다.");
            rules.Add("재배달은 기존 실패 원장을 덮어쓰지 않고 다음 회차의 새 배달 원장으로 남깁니다.");
        }

        if (!string.Equals(template.Key, CommunityLedgerTemplateKeys.FoodOrder, StringComparison.OrdinalIgnoreCase))
        {
            rules.Add("배송 블록은 포장, 검수, 출고 준비처럼 앞선 조건이 충족된 뒤 열리는 후속 블록입니다.");
        }
        rules.Add("이 다이어그램은 화면 구성과 원장 조건을 보여주는 연결도이며, 실제 처리는 아래 API 경로 후보나 OS 스케줄링 단계에서 이어집니다.");

        return rules;
    }

    private static IReadOnlyList<원장블록노드> 기본원장블록노드생성(CommunityLedgerTemplateResponse template)
    {
        var nodes = new List<원장블록노드>();

        if (template.UiSectionHints.Any(상품성격섹션인가))
        {
            nodes.Add(상품노드("요청", "상품/요청 블록", "사용자가 원하는 대상과 조건을 먼저 구조화합니다."));
        }

        if (template.UiSectionHints.Any(창고성격섹션인가))
        {
            nodes.Add(창고노드("거점", "창고/재고 블록", "상품이나 물건이 머무는 장소와 근거를 잡습니다."));
        }

        if (template.UiSectionHints.Any(작업성격섹션인가))
        {
            nodes.Add(작업노드("작업", "상태/작업 블록", "피킹, 검수, 포장, 조리 같은 중간 작업을 구조화합니다."));
        }

        if (template.UiSectionHints.Any(배송성격섹션인가))
        {
            nodes.Add(배송노드("배송/전달", "배송 블록", "장소와 장소 사이의 이동을 별도 배송 블록으로 표시합니다."));
        }

        if (template.UiSectionHints.Any(확인성격섹션인가) || nodes.Count == 0)
        {
            nodes.Add(확인노드("확인", "확인/증빙 블록", "완료, 수령, 증빙, 메모를 남깁니다."));
        }

        return nodes.Count > 1
            ? nodes
            : [상품노드("요청", "원장 시작 블록", "사용자가 원하는 일을 먼저 적습니다."), 확인노드("확인", "완료 확인 블록", "처리 결과와 증빙을 남깁니다.")];
    }

    private static bool 상품수요원장인가(CommunityLedgerTemplateResponse template)
    {
        if (string.Equals(template.Key, CommunityLedgerTemplateKeys.FoodOrder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return template.Key is CommunityLedgerTemplateKeys.HongdalMart
            or CommunityLedgerTemplateKeys.WarehouseOutbound
            or CommunityLedgerTemplateKeys.WarehouseInbound
            or CommunityLedgerTemplateKeys.LocalSale
            or CommunityLedgerTemplateKeys.GroupPurchase
            or CommunityLedgerTemplateKeys.GroupImport
            || template.UiSectionHints.Any(상품성격섹션인가);
    }

    private static bool 창고또는재고를사용하는가(CommunityLedgerTemplateResponse template)
        => template.UiSectionHints.Any(창고성격섹션인가)
            || template.EngineHints.Any(engine => engine.Contains("출고", StringComparison.Ordinal) || engine.Contains("피킹", StringComparison.Ordinal));

    private static bool 상품성격섹션인가(string section)
        => ContainsAny(section, "상품", "물건", "품목", "메뉴", "주문", "화물", "모집 수량");

    private static bool 판매채널성격섹션인가(string section)
        => ContainsAny(section, "판매", "판매채널", "채널", "스토어", "스마트스토어", "쿠팡", "마켓", "상점", "주문 연동", "sales", "channel", "commerce", "store", "marketplace");

    private static bool 창고성격섹션인가(string section)
        => ContainsAny(section, "창고", "재고", "입고", "출고", "보관", "상차지", "픽업지", "도심 재고", "분배");

    private static bool 작업성격섹션인가(string section)
        => ContainsAny(section, "피킹", "검수", "포장", "조리", "진행 상태", "상태", "납품");

    private static bool 배송성격섹션인가(string section)
        => ContainsAny(section, "배송", "전달", "운송", "인계", "픽업", "하차", "도착지", "고객 전달");

    private static bool 확인성격섹션인가(string section)
        => ContainsAny(section, "확인", "증빙", "수령", "정산", "마감", "이상 기록", "타임라인");

    private static bool ContainsAny(string value, params string[] keywords)
        => keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static 원장블록노드 상품노드(string title, string groupLabel, string description, string? condition = null)
        => new(title, groupLabel, description, "product", Color.Primary, condition);

    private static 원장블록노드 창고노드(
        string title,
        string groupLabel,
        string description,
        string? condition = null,
        DiagramNodeConnectionRole connectionRole = DiagramNodeConnectionRole.Standard)
        => new(title, groupLabel, description, "warehouse", Color.Success, condition, ConnectionRole: connectionRole);

    private static 원장블록노드 작업노드(string title, string groupLabel, string description, string? condition = null)
        => new(title, groupLabel, description, "work", Color.Warning, condition);

    private static 원장블록노드 배송노드(string title, string groupLabel, string description, string? condition = null)
        => new(title, groupLabel, description, "delivery", Color.Secondary, condition);

    private static 원장블록노드 확인노드(string title, string groupLabel, string description, string? condition = null)
        => new(title, groupLabel, description, "confirm", Color.Info, condition);
}
