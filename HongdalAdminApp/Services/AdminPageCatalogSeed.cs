using Hongdal.Ui.Common.Areas.BackOffice.ViewModels;

namespace HongdalAdminApp.Services;

internal static class AdminPageCatalogSeed
{
    private static readonly DateTimeOffset VerifiedAt = new(2026, 7, 18, 9, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<AdminManagedPageSnapshot> Create()
        =>
        [
            Page(
                "admin-home", "admin", "살뜰 관리자", "admin-operations", "관리자 운영",
                "관리자 공통 홈", "/", "HongdalAdminApp/Components/Pages/Home.razor",
                "커뮤니티 운영, 자료 검토와 내부 업무 진입을 모읍니다.", "서버관리자",
                ["서버관리자"], lifecycle: AdminPageLifecycle.Internal,
                review: AdminPageReviewState.Verified, navigation: AdminPageNavigationState.Primary,
                desktop: true, mobile: true, authentication: true),
            Page(
                "admin-page-catalog", "admin", "살뜰 관리자", "admin-operations", "관리자 운영",
                "페이지 운영 카탈로그", "/page-catalog", "HongdalAdminApp/Components/Pages/PageCatalog.razor",
                "0.0 핵심 페이지의 route, 노출, 실행 모드와 검증 상태를 관리합니다.", "서버관리자",
                ["서버관리자"], lifecycle: AdminPageLifecycle.Internal,
                execution: AdminPageExecutionMode.Simulation, review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Primary, desktop: true, mobile: true, authentication: true),
            Page(
                "admin-community-management", "admin", "살뜰 관리자", "admin-operations", "관리자 운영",
                "커뮤니티 운영", "/community-management", "HongdalAdminApp/Components/Pages/CommunityManagement.razor",
                "사용자 활동, 게시글, 댓글과 연락 조치를 감사 기록과 함께 처리합니다.", "커뮤니티 운영자",
                ["서버관리자", "커뮤니티 운영자"], lifecycle: AdminPageLifecycle.Internal,
                execution: AdminPageExecutionMode.Operational, navigation: AdminPageNavigationState.Primary,
                authentication: true, externalEffects: true),
            Page(
                "admin-information-review", "admin", "살뜰 관리자", "admin-content", "자료·콘텐츠",
                "자료 검토·글쓰기", "/information-review", "HongdalAdminApp/Components/Pages/CommunityInformationReview.razor",
                "공개 자료의 출처와 기준을 확인하고 커뮤니티 글 초안으로 넘깁니다.", "콘텐츠 운영자",
                ["서버관리자", "콘텐츠 운영자"], lifecycle: AdminPageLifecycle.Internal,
                execution: AdminPageExecutionMode.Operational, review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Primary, desktop: true, mobile: true,
                authentication: true, externalEffects: true),
            Page(
                "admin-prajna", "admin", "살뜰 관리자", "admin-content", "자료·콘텐츠",
                "반야 운영 허브", "/prajna", "HongdalAdminApp/Components/Pages/PrajnaHub.razor",
                "반야 카드와 영상 자료의 내부 승인 상태를 확인합니다.", "콘텐츠 운영자",
                ["서버관리자", "콘텐츠 운영자"], lifecycle: AdminPageLifecycle.Internal,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual,
                authentication: true),

            Page(
                "community-home", "web-community", "Hongdal Web", "community", "커뮤니티",
                "커뮤니티 홈", "/community", "Hongdal.WebApp/Pages/CommunityPage.razor",
                "게시판을 고르고 가볍게 글을 읽는 커뮤니티 첫 진입 화면입니다.", "커뮤니티 운영자",
                ["방문자", "회원", "전문가"], review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Primary, desktop: true, mobile: true),
            Page(
                "community-boards", "web-community", "Hongdal Web", "community", "커뮤니티",
                "게시판 목록", "/community/boards", "Hongdal.WebApp/Pages/CommunityBoardPage.razor",
                "목적별 게시판을 탐색하고 게시판별 작성 조건을 확인합니다.", "커뮤니티 운영자",
                ["방문자", "회원"], review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Primary, desktop: true, mobile: true),
            Page(
                "community-write", "web-community", "Hongdal Web", "community", "커뮤니티",
                "글쓰기", "/community/write", "Hongdal.WebApp/Pages/CommunityWorkspacePage.razor",
                "가벼운 글에서 다이어그램과 공동행동 초안으로 확장합니다.", "작성자",
                ["방문자", "회원", "전문가"], execution: AdminPageExecutionMode.Operational,
                review: AdminPageReviewState.Verified, navigation: AdminPageNavigationState.Contextual,
                desktop: true, mobile: true, externalEffects: true),
            Page(
                "community-post-detail", "web-community", "Hongdal Web", "community", "커뮤니티",
                "게시글 상세", "/community/posts/{PostId:long}", "Hongdal.WebApp/Pages/CommunityWorkspacePage.razor",
                "게시글 본문, 참여 맥락과 연결된 업무 흐름을 확인합니다.", "커뮤니티 운영자",
                ["방문자", "회원", "전문가"], review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Contextual, desktop: true, mobile: true,
                previewPath: "/community/posts/1"),
            Page(
                "community-actions", "web-community", "Hongdal Web", "collective-action", "공동행동",
                "공동행동 진행", "/community/actions/{PageKey}", "Hongdal.WebApp/Pages/CommunityCollectiveActionsPage.razor",
                "마음이 모인 가원장의 역할, 수량, 시장 입고와 이행 단계를 확인합니다.", "공동행동 참여자",
                ["구매자", "판매자", "생산자", "물류", "전문가"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Contextual, desktop: true, mobile: true,
                previewPath: "/community/actions/in-progress"),
            Page(
                "community-group-purchase", "web-community", "Hongdal Web", "collective-action", "공동행동",
                "공동구매", "/community/group-purchase", "Hongdal.WebApp/Pages/CommunityGroupPurchasePage.razor",
                "참여 수량과 조건을 모아 공동구매 초안을 구성합니다.", "공동구매 참여자",
                ["구매자", "판매자", "공급자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "community-group-import", "web-community", "Hongdal Web", "collective-action", "공동행동",
                "공동수입", "/community/group-import", "Hongdal.WebApp/Pages/CommunityGroupImportPage.razor",
                "수입 수요와 수출자·수입자·통관·물류 역할 슬롯을 구성합니다.", "공동수입 참여자",
                ["구매자", "수출자", "수입자", "관세사", "물류"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "community-food-discovery", "web-community", "Hongdal Web", "community", "커뮤니티",
                "음식 자료 발견", "/community/discover/food", "Hongdal.WebApp/Pages/CommunityFoodDiscoveryPage.razor",
                "음식 관련 영상과 공개 자료를 글과 공동 수요 대화로 연결합니다.", "콘텐츠 운영자",
                ["방문자", "회원", "콘텐츠 운영자"], lifecycle: AdminPageLifecycle.Preview,
                navigation: AdminPageNavigationState.Contextual),
            Page(
                "community-decoration", "web-community", "Hongdal Web", "personalization", "개인 설정·꾸미기",
                "꾸미기 상점", "/community/decorations", "Hongdal.WebApp/Pages/CommunityPersonalPage.razor",
                "게시판·글 목록·본문에 적용할 읽기 테마 패키지를 선택합니다.", "디자인 운영자",
                ["회원", "디자이너"], execution: AdminPageExecutionMode.Simulation,
                review: AdminPageReviewState.Verified, navigation: AdminPageNavigationState.Contextual,
                desktop: true, mobile: true),
            Page(
                "community-personal", "web-community", "Hongdal Web", "personalization", "개인 설정·꾸미기",
                "내 정보", "/community/me/{SectionKey}", "Hongdal.WebApp/Pages/CommunityPersonalPage.razor",
                "사용 설정, 꾸미기와 사용자의 개인 활동을 탐색합니다.", "회원",
                ["회원"], navigation: AdminPageNavigationState.Primary, authentication: true,
                previewPath: "/community/me/profile"),
            Page(
                "community-safety", "web-community", "Hongdal Web", "safety", "안전·분쟁",
                "안전센터", "/community/safety", "Hongdal.WebApp/Pages/CommunitySafetyCenterPage.razor",
                "신고, 분쟁과 공개 범위 관련 요청을 별도 처리합니다.", "안전 운영자",
                ["방문자", "회원", "안전 운영자"], execution: AdminPageExecutionMode.Operational,
                navigation: AdminPageNavigationState.Contextual, externalEffects: true),

            Page(
                "restaurant-home", "restaurant", "RestaurantDeskApp", "restaurant", "음식점 운영",
                "음식점 홈", "/", "RestaurantDeskApp/Components/Pages/Home.razor",
                "음식점 커뮤니티와 주문·공급 업무의 첫 진입 화면입니다.", "음식점 운영자",
                ["음식점 운영자"], review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Primary, desktop: true, mobile: true),
            Page(
                "restaurant-ingredient-supply", "restaurant", "RestaurantDeskApp", "restaurant-supply", "식재료 공급",
                "식재료 공급 요청", "/ingredients/supply-request", "RestaurantDeskApp/Components/Pages/IngredientSupplyRequest.razor",
                "국내 산지와 수입 공동공급의 예상 도착단가를 비교하고 요청 초안을 저장합니다.", "음식점 구매 담당자",
                ["음식점 운영자", "구매 담당자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, review: AdminPageReviewState.Verified,
                navigation: AdminPageNavigationState.Primary, desktop: true, mobile: true),
            Page(
                "restaurant-address", "restaurant", "RestaurantDeskApp", "restaurant-logistics", "음식점 물류",
                "상차·하차 주소", "/dispatch/address-form", "RestaurantDeskApp/Components/Pages/DispatchAddressForm.razor",
                "음식점 물류 인계를 위한 상차·하차 주소와 접근 조건을 입력합니다.", "음식점 운영자",
                ["음식점 운영자", "물류 담당자"], execution: AdminPageExecutionMode.Simulation,
                navigation: AdminPageNavigationState.Contextual),
            Page(
                "restaurant-nearby", "restaurant", "RestaurantDeskApp", "restaurant-discovery", "음식점 탐색",
                "가까운 음식점", "/restaurants/nearby", "RestaurantDeskApp/Components/Pages/NearbyRestaurants.razor",
                "근거리 음식점 노출과 생활권 탐색 흐름을 확인합니다.", "음식점 운영자",
                ["음식점 운영자"], navigation: AdminPageNavigationState.Contextual),
            Page(
                "restaurant-popular", "restaurant", "RestaurantDeskApp", "restaurant-discovery", "음식점 탐색",
                "인기 음식점", "/restaurants/popular", "RestaurantDeskApp/Components/Pages/PopularRestaurants.razor",
                "인기 음식점 노출과 순위 기준을 확인합니다.", "음식점 운영자",
                ["음식점 운영자"], navigation: AdminPageNavigationState.Contextual),
            Page(
                "restaurant-review", "restaurant", "RestaurantDeskApp", "restaurant-review", "리뷰 운영",
                "리뷰 운영", "/reviews/moderation", "RestaurantDeskApp/Components/Pages/ReviewModeration.razor",
                "사진 리뷰와 저평점 리뷰의 게시 정책을 관리합니다.", "음식점 운영자",
                ["음식점 운영자", "리뷰 운영자"], execution: AdminPageExecutionMode.Operational,
                navigation: AdminPageNavigationState.Contextual, authentication: true, externalEffects: true),

            Page(
                "warehouse-home", "warehouse", "WarehouseManagerApp", "warehouse", "창고 공통",
                "창고 작업공간", "/warehouse", "WarehouseManagerApp/Components/Pages/WarehouseWorkspace.razor",
                "일반·수입·공동주택·마트 창고 역할별 작업공간으로 진입합니다.", "창고 관리자",
                ["창고 관리자", "작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Primary),
            Page(
                "warehouse-expected-inbounds", "warehouse", "WarehouseManagerApp", "warehouse-inbound", "입고",
                "입고 예정 조회", "/warehouse/inbounds/expected", "WarehouseManagerApp/Components/Pages/ExpectedInbounds.razor",
                "업체명·업체 코드로 입고 예정품과 납품 조건을 조회합니다.", "입고 관리자",
                ["창고 관리자", "입고 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Primary),
            Page(
                "warehouse-inbound-products", "warehouse", "WarehouseManagerApp", "warehouse-inbound", "입고",
                "입고 상품 스캔", "/work/inbound/products", "WarehouseManagerApp/Components/Pages/InboundProductScan.razor",
                "입고 상품과 lot를 스캔해 예정 정보와 대조합니다.", "입고 작업자",
                ["입고 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "warehouse-inbound-inspection", "warehouse", "WarehouseManagerApp", "warehouse-inbound", "입고",
                "입고 검수", "/work/inbound/inspection", "WarehouseManagerApp/Components/Pages/InboundInspection.razor",
                "수량·품질·온도·파손과 보관 조건을 확인합니다.", "검수 작업자",
                ["입고 작업자", "검수자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "warehouse-import-arrival", "warehouse", "WarehouseManagerApp", "warehouse-import", "수입·보세",
                "수입 화물 도착", "/warehouse/import/arrival", "WarehouseManagerApp/Components/Pages/ImportArrival.razor",
                "수입 화물 도착과 보세 인계 전 상태를 확인합니다.", "수입 창고 관리자",
                ["수입 창고 관리자", "보세 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "warehouse-import-customs", "warehouse", "WarehouseManagerApp", "warehouse-import", "수입·보세",
                "통관 상태", "/warehouse/import/customs", "WarehouseManagerApp/Components/Pages/ImportCustoms.razor",
                "통관·검역 상태와 반출 가능 여부를 확인합니다.", "수입 창고 관리자",
                ["수입 창고 관리자", "관세사"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "warehouse-import-release", "warehouse", "WarehouseManagerApp", "warehouse-import", "수입·보세",
                "수입 반출", "/warehouse/import/release", "WarehouseManagerApp/Components/Pages/ImportRelease.razor",
                "적법한 반출 승인 뒤 국내 물류 인계 단계를 확인합니다.", "수입 창고 관리자",
                ["수입 창고 관리자", "출고 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "warehouse-apartment-arrivals", "warehouse", "WarehouseManagerApp", "warehouse-apartment", "공동주택 물류",
                "단지 도착", "/warehouse/apartment/arrivals", "WarehouseManagerApp/Components/Pages/ApartmentArrivals.razor",
                "공동주택 단지 공동물량의 도착과 인계 상태를 확인합니다.", "단지 물류 관리자",
                ["단지 물류 관리자", "입고 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "warehouse-apartment-allocation", "warehouse", "WarehouseManagerApp", "warehouse-apartment", "공동주택 물류",
                "세대 배분", "/warehouse/apartment/allocation", "WarehouseManagerApp/Components/Pages/ApartmentAllocation.razor",
                "단지 입고 물량을 참여 세대와 수령 거점에 배분합니다.", "단지 물류 관리자",
                ["단지 물류 관리자", "배분 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),
            Page(
                "warehouse-mart-home", "warehouse", "WarehouseManagerApp", "warehouse-mart", "마트 도심창고",
                "마트 작업 홈", "/mart", "WarehouseManagerApp/Components/Pages/MartHome.razor",
                "도심 마트 창고의 주문, 피킹과 포장 업무로 진입합니다.", "마트 창고 관리자",
                ["마트 창고 관리자", "피킹 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Primary),
            Page(
                "warehouse-mart-picking", "warehouse", "WarehouseManagerApp", "warehouse-mart", "마트 도심창고",
                "마트 피킹·포장", "/mart/picking", "WarehouseManagerApp/Components/Pages/MartPickingPacking.razor",
                "주문별 피킹, 검수, 포장과 배송 인계를 처리합니다.", "피킹 작업자",
                ["피킹 작업자", "포장 작업자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual),

            Page(
                "driver-home", "driver", "DriverApp", "driver", "기사 업무",
                "기사 홈", "/driver/home", "DriverApp/Components/Pages/Home.razor",
                "기사의 운행·추천·알림·정산 진입을 모읍니다.", "운송 기사",
                ["운송 기사"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Primary,
                authentication: true),
            Page(
                "driver-current-transport", "driver", "DriverApp", "driver", "기사 업무",
                "진행 중 운송", "/driver/transports/current", "DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor",
                "기사 본인이 수락한 운송의 상차·이동·하차 상태를 확인합니다.", "운송 기사",
                ["운송 기사"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Primary,
                authentication: true),

            Page(
                "orderer-home", "orderer", "OrdererApp", "orderer", "주문자 업무",
                "주문자 홈", "/", "OrdererApp/Components/Pages/Home.razor",
                "화물, 음식, 마트와 공동구매 주문으로 진입합니다.", "주문자",
                ["주문자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Primary),
            Page(
                "orderer-group-purchase", "orderer", "OrdererApp", "orderer", "주문자 업무",
                "공동구매 참여", "/group-purchase", "OrdererApp/Components/Pages/GroupPurchaseIntent.razor",
                "공동구매 의향과 필요한 수량·수령 조건을 등록합니다.", "공동구매 참여자",
                ["주문자", "공동구매 참여자"], lifecycle: AdminPageLifecycle.Preview,
                execution: AdminPageExecutionMode.Simulation, navigation: AdminPageNavigationState.Contextual)
        ];

    private static AdminManagedPageSnapshot Page(
        string pageKey,
        string appKey,
        string appName,
        string areaKey,
        string areaName,
        string title,
        string routeTemplate,
        string sourcePath,
        string purpose,
        string ownerRole,
        IReadOnlyList<string> audienceRoles,
        AdminPageLifecycle lifecycle = AdminPageLifecycle.Active,
        AdminPageExecutionMode execution = AdminPageExecutionMode.ReadOnly,
        AdminPageReviewState review = AdminPageReviewState.NeedsReview,
        AdminPageNavigationState navigation = AdminPageNavigationState.Contextual,
        bool desktop = false,
        bool mobile = false,
        bool authentication = false,
        bool externalEffects = false,
        string? previewPath = null)
        => new(
            pageKey,
            appKey,
            appName,
            areaKey,
            areaName,
            title,
            routeTemplate,
            previewPath ?? routeTemplate,
            sourcePath,
            purpose,
            ownerRole,
            audienceRoles,
            lifecycle,
            execution,
            review,
            navigation,
            true,
            desktop,
            mobile,
            authentication,
            externalEffects,
            review == AdminPageReviewState.Verified ? VerifiedAt : null,
            review == AdminPageReviewState.Verified ? "로컬 렌더링 검증" : null,
            string.Empty);
}
