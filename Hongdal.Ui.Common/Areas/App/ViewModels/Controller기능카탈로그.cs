using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class Controller기능카탈로그
{
    public static IReadOnlyList<Controller기능정의> 공통 { get; } =
    [
        new("app.common-contents", "앱 공통 콘텐츠", "api/v1/app/common-contents"),
        new("security.isms-p-transport", "ISMS-P 전송 보안", "api/v1/security/isms-p/transport"),
        new("common.agricultural-fisheries", "농수산 정보", "api/v1/agricultural-fisheries"),
        new("common.driver-availability", "커뮤니티 기사 운행 공개", "api/v1/community/driver-availability"),
        new("common.driver-inquiries", "기사 커뮤니티 의뢰", "api/v1/driver/community-inquiries"),
        new("common.keyword-notifications", "커뮤니티 키워드 알림", "api/v1/community/keyword-notifications"),
        new("common.keyword-subscriptions", "커뮤니티 키워드 구독", "api/v1/community/keyword-subscriptions"),
        new("common.ledger-block-assignees", "원장 블록 담당자", "api/v1/community/ledgers/{ledgerId}/blocks/{blockId}/assignees"),
        new("common.ledger-role-access", "원장 역할 접근", "api/v1/community/ledgers/{ledgerId}/role-access"),
        new("common.post-opportunities", "게시글 기회", "api/v1/community/posts/{postId:long}/opportunities"),
        new("common.kie-ai", "KIE AI 콜백", "api/v1/kie-ai"),
        new("common.import-readiness", "축산물 수입 준비", "api/v1/agricultural-fisheries/import-readiness"),
        new("common.mobile-push", "모바일 푸시 설치", "api/v1/mobile/push/installations"),
        new("common.public-data-apis", "공공데이터 API 메타데이터", "api/v1/public-data/apis"),
        new("common.sales-channels", "판매 채널", "api/v1/sales-channels"),
        new("common.sample-images", "샘플 이미지", "api/v1/sample-images"),
        new("common.market-logistics-hubs", "전통시장 물류 거점", "api/v1/traditional-market-logistics-hubs"),
        new("common.traditional-markets", "전통시장", "api/v1/traditional-markets"),
        new("common.version-feature-flags", "버전 기능 플래그", "api/v1/version-feature-flags"),
        new("common.view-settings", "화면 설정", "api/v1/view-settings"),
        new("common.visa-support", "비자 지원", "api/v1/immigration/visa-support-requests"),
        new("common.warehouse-operations", "창고 운영", "api/v1/warehouse-operations"),
        new("common.work-relationships", "업무 인연 스냅샷", "api/v1/work-relationship-snapshots"),
        new("common.gratitude", "감사 메시지", "api/v1/gratitude"),
        new("common.customs-hs-codes", "공동수입 HS 코드", "api/v1/customs/hs-codes"),
        new("common.education-courses", "교육 과정", "api/v1/education/courses"),
        new("common.education-operations", "교육 과정 운영", "api/v1/education/operations"),
        new("common.education-participation", "교육 참여", "api/v1/education"),
        new("common.node-stickers", "노드 스티커 상점", "api/v1/community/node-sticker-store"),
        new("common.product-images", "상품 상세 이미지", "api/v1/product-detail-images"),
        new("common.product-journeys", "상품 여정", "api/v1/products"),
        new("common.transport-ledgers", "운송 원장", "api/v1/transport-request-ledgers"),
        new("common.connections", "인연 연결", "api/v1/connections"),
        new("common.auth", "인증", "api/v1/auth"),
        new("common.market-councils", "전통시장 생활권 협의", "api/v1/traditional-market-councils"),
        new("common.order-ledgers", "주문 원장", "api/v1/community/order-ledgers"),
        new("common.community-posts", "커뮤니티 게시글", "api/v1/community/posts"),
        new("common.community-boards", "커뮤니티 게시판", "api/v1/community/boards"),
        new("common.diagram-conversations", "커뮤니티 대화", "api/v1/community/diagram-conversations"),
        new("common.ledger-sharing", "커뮤니티 원장 공유", "api/v1/community/ledgers/{원장Id}/sharing"),
        new("common.community-votes", "커뮤니티 투표", "api/v1/community/votes"),
        new("common.activity-signals", "커뮤니티 활동 신호", "api/v1/community/activity-signals"),
        new("common.customs", "통관 연동", "api/v1/customs"),
        new("common.files", "파일 업로드", "api/v1/files"),
        new("common.field-experiences", "현장 체험 활동", "api/v1/education/field-experiences")
    ];

    public static IReadOnlyList<Controller기능정의> 기사 { get; } =
    [
        new("driver.command-feature-settings", "기사 명령 기능 설정", "api/v1/driver/command-feature-settings"),
        new("driver.dev-snapshot", "기사 개발 스냅샷", "api/v1/driver/dev-snapshot"),
        new("driver.dispatch-actions", "기사 배차 액션", "api/v1/driver/dispatch-actions"),
        new("driver.exploration-campaigns", "기사 탐색 캠페인", "api/v1/driver/exploration-campaigns"),
        new("driver.home", "기사 홈", "api/v1/driver/home"),
        new("driver.notifications", "기사 알림", "api/v1/driver/notifications"),
        new("driver.preferences", "기사 환경 설정", "api/v1/driver/preferences"),
        new("driver.public-dispatches", "기사 공개 배차", "api/v1/driver/public-dispatches"),
        new("driver.recommendations", "기사 배차 추천", "api/v1/driver/recommendations"),
        new("driver.requests", "기사 운송 의뢰", "api/v1/driver/requests"),
        new("driver.reservations", "기사 예약", "api/v1/driver/reservations"),
        new("driver.settlements", "기사 정산", "api/v1/driver/settlements"),
        new("driver.shifts", "기사 근무", "api/v1/driver/shifts"),
        new("driver.transports", "기사 운송", "api/v1/driver/transports"),
        new("driver.work", "기사 운행", "api/v1/driver/work"),
        new("driver.directory", "기사 디렉터리", "api/v1/drivers"),
        new("driver.shift-detail", "기사별 근무", "api/v1/drivers/{driverId}/shifts")
    ];

    public static IReadOnlyList<Controller기능정의> 화주 { get; } =
    [
        new("shipper.requests", "화주 운송 의뢰", "api/v1/shipper/requests"),
        new("shipper.exploration-inbox", "화주 탐색 문의", "api/v1/shipper/exploration-inbox"),
        new("shipper.payments", "화주 결제", "api/v1/payments"),
        new("shipper.oversea-manufacturers", "수입식품 해외 제조업소", "api/v1/shipper/import-food/oversea-manufacturers")
    ];

    public static IReadOnlyList<Controller기능정의> 주문자 { get; } =
    [
        new("orderer.fulfillment-plans", "공동구매 이행 계획", "api/v1/orderer/domestic-group-purchases/{campaignId:guid}/fulfillment-plans"),
        new("orderer.negotiation", "공동구매 협상", "api/v1/orderer/domestic-group-purchases/{campaignId:guid}/negotiation"),
        new("orderer.producer-connections", "공동구매 생산자 연결", "api/v1/orderer/domestic-group-purchases/{campaignId:guid}/producer-connections"),
        new("orderer.vehicle-recommendations", "공동구매 차량 추천", "api/v1/orderer/domestic-group-purchases/{campaignId:guid}/vehicle-recommendations"),
        new("orderer.public-data", "주문자 공공데이터", "api/v1/orderer/public-data"),
        new("orderer.restaurant-search-policy", "음식점 검색 정책", "api/v1/orderer/restaurant-search-policy"),
        new("orderer.logistics-workflows", "공동구매 물류 워크플로", "api/v1/orderer/group-purchase-logistics-workflows"),
        new("orderer.demand-votes", "공동구매 수요 투표", "api/v1/orderer/group-purchase-demand-votes"),
        new("orderer.auto-groups", "공동구매 자동 집단화", "api/v1/orderer/group-purchase-auto-groups"),
        new("orderer.commerce-fulfillment", "공동구매 커머스 이행", "api/v1/orderer/group-purchase-commerce-fulfillment-plans"),
        new("orderer.overseas-shipments", "공동구매 해외 선적", "api/v1/orderer/group-purchase-overseas-shipments"),
        new("orderer.group-entities", "주문자 집단 운영 주체", "api/v1/orderer/orderer-group-operating-entities")
    ];

    public static IReadOnlyList<Controller기능정의> 음식 { get; } =
    [
        new("food.orders", "음식 주문", "api/v1/food-orders"),
        new("food.dispatch-address", "음식 배차 주소", "api/v1/food-orders/dispatch/address-form"),
        new("food.delivery-tickets", "음식 배달권", "api/v1/food-delivery-tickets"),
        new("food.settlements", "음식 배달 정산", "api/v1/food-delivery-settlements"),
        new("food.pricing", "음식 배달 요금", "api/v1/food-delivery-pricing"),
        new("food.restaurants", "음식점", "api/v1/restaurants")
    ];

    public static IReadOnlyList<Controller기능정의> 음식배달기사 { get; } =
    [
        new("food-driver.deliveries", "음식 배달 기사 업무", "api/v1/driver/food-deliveries"),
        new("food-driver.monthly-settlements", "배달 기사 월정산", "api/v1/drivers/{driverId}/monthly-settlements")
    ];

    public static IReadOnlyList<Controller기능정의> 관리자 { get; } =
    [
        new("admin.restaurant-search-policy", "음식점 검색 정책 관리", "api/v1/admin/orderer/restaurant-search-policy"),
        new("admin.view-policies", "화면 정책", "api/v1/admin/view-policies"),
        new("admin.auxiliary-features", "보조 기능 설정", "api/v1/admin/auxiliary-feature-settings"),
        new("admin.activity-logs", "사용자 행위 로그", "api/v1/admin/activity-logs"),
        new("admin.dashboard", "관리자 대시보드", "api/v1/admin/dashboard"),
        new("admin.dispatch-wait", "배차 대기", "api/v1/dispatch/wait"),
        new("admin.operating-drivers", "기사 운행 현황", "api/v1/admin/drivers/operating"),
        new("admin.dispatch-plans", "배차 계획", "api/v1/admin/dispatch-plans"),
        new("admin.transport-events", "운송 이벤트", "api/v1/transport-events"),
        new("admin.transports", "운송 진행 관리", "api/v1/admin/transports"),
        new("admin.documents", "문서 관리", "api/v1/admin/documents"),
        new("admin.pod-files", "POD 파일", "api/v1/admin/files/pod"),
        new("admin.driver-settlements", "기사 월정산 관리", "api/v1/admin/driver-settlements"),
        new("admin.contact-search", "관리자 연락처 검색", "api/v1/admin/contact-search"),
        new("admin.drivers", "기사 관리", "api/v1/admin/drivers"),
        new("admin.partners", "업체·화주 관리", "api/v1/admin/partners"),
        new("admin.fare-configurations", "운임 구성", "api/v1/fare-configurations"),
        new("admin.vehicle-rates", "차량 단가", "api/v1/vehicle-rates"),
        new("admin.vehicle-recommendations", "차량 추천 관리", "api/v1/admin/vehicle-recommendations"),
        new("admin.community", "커뮤니티 운영", "api/v1/admin/community-management"),
        new("admin.hongik-hakdang", "홍익학당 카드", "api/v1/admin/content/hongik-hakdang/cards"),
        new("admin.typecast-voices", "Typecast 음성", "api/v1/admin/speech/typecast/voices"),
        new("admin.youtube", "YouTube 채널 감시", "api/v1/admin/content/youtube"),
        new("admin.common-contents", "공통 콘텐츠", "api/v1/admin/common-contents"),
        new("admin.education", "교육 과정 관리", "api/v1/admin/education"),
        new("admin.hs-codes", "HS 코드 운영", "api/v1/admin/hs-codes"),
        new("admin.ai-judgment-cases", "배차 AI 판단 사례", "api/v1/admin/dispatch/ai-judgment-cases"),
        new("admin.cargo-ai-review", "화물 배차 AI 검토", "api/v1/admin/dispatch/domestic-cargo-ai-review"),
        new("admin.food-ai-review", "음식 배차 AI 검토", "api/v1/admin/dispatch/food-delivery-ai-review"),
        new("admin.hr-contracts", "근로 계약", "api/v1/admin/hr-employment-contracts"),
        new("admin.hr-benefits", "참여 혜택", "api/v1/admin/hr-participation-benefits"),
        new("admin.hr-roles", "인사 역할", "api/v1/admin/hr-roles"),
        new("admin.hr-social-insurance", "사회보험 신고", "api/v1/admin/hr-social-insurance-filings"),
        new("admin.orderer-logistics", "주문자 물류 워크플로 관리", "api/v1/admin/orderer/group-purchase-logistics-workflows"),
        new("admin.orderer-commerce", "주문자 커머스 이행 관리", "api/v1/admin/orderer/group-purchase-commerce-fulfillment-plans"),
        new("admin.orderer-shipments", "주문자 해외 선적 관리", "api/v1/admin/orderer/group-purchase-overseas-shipments"),
        new("admin.orderer-entities", "주문자 집단 운영 주체 관리", "api/v1/admin/orderer/orderer-group-operating-entities"),
        new("admin.platform-profit-returns", "플랫폼 이익 환원", "api/v1/admin/platform-profit-returns"),
        new("admin.market-logistics-hubs", "전통시장 물류 거점 관리", "api/v1/admin/traditional-market-logistics-hubs"),
        new("admin.restaurant-reviews", "음식점 리뷰 관리", "api/v1/admin/restaurant-reviews"),
        new("admin.food-pricing-policy", "음식 배달 요금 정책", "api/v1/admin/food-delivery-pricing-policy")
    ];
}

public sealed class 공통Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 공통Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.공통)
    {
    }

    public Controller기능ViewModel 인증 => this["common.auth"];
    public Controller기능ViewModel 파일 => this["common.files"];
    public Controller기능ViewModel 창고운영 => this["common.warehouse-operations"];
    public Controller기능ViewModel 커뮤니티게시글 => this["common.community-posts"];
}

public sealed class 기사Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 기사Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.기사)
    {
    }

    public Controller기능ViewModel 홈 => this["driver.home"];
    public Controller기능ViewModel 근무 => this["driver.shifts"];
    public Controller기능ViewModel 운행 => this["driver.work"];
    public Controller기능ViewModel 추천 => this["driver.recommendations"];
    public Controller기능ViewModel 운송 => this["driver.transports"];
    public Controller기능ViewModel 정산 => this["driver.settlements"];
}

public sealed class 화주Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 화주Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.화주)
    {
    }

    public Controller기능ViewModel 운송의뢰 => this["shipper.requests"];
    public Controller기능ViewModel 탐색문의 => this["shipper.exploration-inbox"];
    public Controller기능ViewModel 결제 => this["shipper.payments"];
}

public sealed class 주문자Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 주문자Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.주문자)
    {
    }
}

public sealed class 음식Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 음식Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.음식)
    {
    }

    public Controller기능ViewModel 주문 => this["food.orders"];
    public Controller기능ViewModel 음식점 => this["food.restaurants"];
}

public sealed class 음식배달기사Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 음식배달기사Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.음식배달기사)
    {
    }
}

public sealed class 관리자Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 관리자Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.관리자)
    {
    }

    public Controller기능ViewModel 대시보드 => this["admin.dashboard"];
    public Controller기능ViewModel 기사관리 => this["admin.drivers"];
    public Controller기능ViewModel 운송관리 => this["admin.transports"];
    public Controller기능ViewModel 인사역할 => this["admin.hr-roles"];
}

/// <summary>
/// 관리자 화면에서 관리자·공통 Controller 기능을 한 번에 조립할 때 사용하는 루트 ViewModel입니다.
/// </summary>
public sealed class 관리자전체Api기능모음ViewModel : 조립ViewModelBase
{
    public 관리자전체Api기능모음ViewModel(
        관리자Controller기능모음ViewModel 관리자Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.관리자Controllers = 하위ViewModel등록(관리자Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 관리자Controller기능모음ViewModel 관리자Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}

public sealed class 인사Controller기능모음ViewModel : Controller기능모음ViewModel
{
    private static readonly string[] Keys =
    [
        "admin.hr-contracts",
        "admin.hr-benefits",
        "admin.hr-roles",
        "admin.hr-social-insurance"
    ];

    public 인사Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.관리자.Where(x => Keys.Contains(x.Key, StringComparer.Ordinal)))
    {
    }
}

public sealed class 창고Controller기능모음ViewModel : Controller기능모음ViewModel
{
    public 창고Controller기능모음ViewModel(IHongdalJsonApiClient client)
        : base(client, Controller기능카탈로그.공통.Where(x => x.Key == "common.warehouse-operations"))
    {
    }

    public Controller기능ViewModel 창고운영 => this["common.warehouse-operations"];
}
