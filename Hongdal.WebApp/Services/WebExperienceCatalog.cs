using Hongdal.WebApp.Models;
using MudBlazor;

namespace Hongdal.WebApp.Services;

public static class WebExperienceCatalog
{
    public static IReadOnlyList<WebExperienceRole> Roles { get; } =
    [
        new(
            "community-orderer",
            "role-experience--community",
            "COMMUNITY · ORDERER",
            "주문자·커뮤니티",
            "동네 글을 읽고 공동구매나 공동수입 제안을 구체화하는 화면입니다.",
            "/images/role-previews/community-orderer.png",
            "모바일 살뜰 게시판 화면",
            Icons.Material.Filled.Groups,
            "/community",
            "게시판 열기",
            [
                new("살뜰 게시판", "동네 글과 댓글, 연결된 업무 원장을 함께 봅니다.", "/community", Icons.Material.Filled.Forum, "커뮤니티", "바로 보기"),
                new("공동구매 제안", "참여자와 수량, 가격, 공급 조건을 맞춥니다.", "/community/group-purchase", Icons.Material.Filled.GroupAdd, "공동구매", "흐름 체험"),
                new("공동수입 검토", "해외 공급과 국내 수요를 수입 흐름으로 연결합니다.", "/community/group-import", Icons.Material.Filled.Public, "공동수입", "흐름 체험"),
                new("3개국 농수산물 가격", "한국·미국·호주의 공식 가격 자료를 단위와 조사 단계별로 비교합니다.", "/information/public-data", Icons.Material.Filled.Insights, "공개 정보", "바로 보기")
            ]),
        new(
            "global-supplier",
            "role-experience--global",
            "OVERSEAS SUPPLIER",
            "해외 공급자",
            "한국 시장에 상품을 소개하고 공급 조건과 수입 가능성을 확인하는 화면입니다.",
            "/images/role-previews/global-supplier.png?v=20260717",
            "Hongdal Global 상품 탐색 화면",
            Icons.Material.Filled.Language,
            GlobalTradeRoutes.Home,
            "Global 화면 열기",
            [
                new("글로벌 상품", "한국 시장을 찾는 해외 상품과 공급 조건을 둘러봅니다.", GlobalTradeRoutes.Home, Icons.Material.Filled.TravelExplore, "Global", "바로 보기"),
                new("상품 제출", "회사, 상품, MOQ와 무역 조건을 제출합니다.", GlobalTradeRoutes.SupplierApply, Icons.Material.Filled.AddBusiness, "공급자", "세션 체험"),
                new("상품 상세", "샘플, HS 코드 제안과 수입 검토 정보를 확인합니다.", GlobalTradeRoutes.Product("indonesian-rattan-storage-basket"), Icons.Material.Filled.Inventory2, "상품", "바로 보기"),
                new("무역 대화", "해외 공급자와 국내 참여자가 조건을 공개적으로 맞춥니다.", GlobalTradeRoutes.CommunityThread(101), Icons.Material.Filled.Translate, "대화", "세션 체험")
            ]),
        new(
            "shipper-seller",
            "role-experience--shipper",
            "SHIPPER · SELLER",
            "화주·판매자",
            "운송 의뢰부터 입고, 재고, 판매채널과 주문 출고까지 이어지는 화면입니다.",
            "/images/role-previews/shipper-seller.png",
            "화주 역할이 선택된 살뜰 앱 화면",
            Icons.Material.Filled.LocalShipping,
            ShipperRoutes.Home,
            "화주 작업공간 열기",
            [
                new("운송 의뢰", "화물, 상하차지, 차량과 결제 조건을 입력합니다.", ShipperRoutes.Request, Icons.Material.Filled.PostAdd, "운송", "로그인 후 실행"),
                new("입고 대시보드", "입고 예정과 완료, 보관 재고를 한눈에 봅니다.", ShipperRoutes.InboundDashboard, Icons.Material.Filled.MoveToInbox, "입고", "화면 체험"),
                new("판매채널", "판매채널 계정과 연결 상태를 관리합니다.", ShipperRoutes.SalesChannels, Icons.Material.Filled.Storefront, "판매", "화면 체험"),
                new("통관·FCL/LCL", "수입량과 비용을 비교하고 운송 방식을 검토합니다.", ShipperRoutes.FclLclPlanner, Icons.Material.Filled.Public, "통관", "화면 체험")
            ]),
        new(
            "driver",
            "role-experience--driver",
            "CARGO DRIVER",
            "운송 기사",
            "추천 운송을 지도에서 확인하고 상차, 하차, 증빙과 정산을 처리하는 화면입니다.",
            "/images/role-previews/driver.png",
            "운송 기사의 추천 경로 지도 화면",
            Icons.Material.Filled.Route,
            "/driver/home",
            "기사 홈 열기",
            [
                new("기사 홈", "현재 위치와 진행 중 운송을 중심으로 봅니다.", "/driver/home", Icons.Material.Filled.Route, "운행", "화면 체험"),
                new("추천 운송", "거리, 예상 수익과 상하차 조건을 비교합니다.", "/driver/recommendations", Icons.Material.Filled.TaskAlt, "배차", "로그인 후 실행"),
                new("진행 중 운송", "수락한 운송의 현재 단계와 다음 행동을 확인합니다.", "/driver/transports/current", Icons.Material.Filled.LocalShipping, "운송", "로그인 후 실행"),
                new("운송 증빙", "상차와 하차 사진, 예외 증빙 흐름을 확인합니다.", "/driver/transport/proof", Icons.Material.Filled.AddAPhoto, "증빙", "화면 체험")
            ]),
        new(
            "warehouse",
            "role-experience--warehouse",
            "WAREHOUSE OPERATOR",
            "창고 관리자",
            "입고 확인, 검수, 적재, 피킹과 포장을 현장 순서대로 처리하는 화면입니다.",
            "/images/role-previews/warehouse.png",
            "창고 관리자 역할이 선택된 살뜰 앱 화면",
            Icons.Material.Filled.Warehouse,
            WarehouseManagerRoutes.Home,
            "창고 작업공간 열기",
            [
                new("작업 보드", "입고와 출고 작업, 연결 대상을 한곳에서 봅니다.", WarehouseManagerRoutes.WorkBoard, Icons.Material.Filled.ViewKanban, "현장", "화면 체험"),
                new("입고 상품 확인", "상품 바코드로 입고 예정 항목을 찾습니다.", WarehouseManagerRoutes.InboundProductScan, Icons.Material.Filled.QrCodeScanner, "입고", "화면 체험"),
                new("입고 검수", "수량 차이, 파손과 보관 조건을 확인합니다.", WarehouseManagerRoutes.InboundInspection, Icons.Material.Filled.FactCheck, "검수", "화면 체험"),
                new("마트 피킹·포장", "도심 주문을 피킹하고 포장 완료까지 처리합니다.", WarehouseManagerRoutes.MartPickingPacking, Icons.Material.Filled.ShoppingCartCheckout, "출고", "화면 체험")
            ])
    ];

    public static WebExperienceRole DefaultRole => Roles[0];

    public static WebExperienceRole Find(string? key)
        => Roles.FirstOrDefault(role =>
               string.Equals(role.Key, key, StringComparison.OrdinalIgnoreCase))
           ?? DefaultRole;
}
