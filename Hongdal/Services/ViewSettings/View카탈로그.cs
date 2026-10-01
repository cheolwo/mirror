using Hongdal.Contracts.Common.ViewSettings;
using 홍달.Data;

namespace 홍달.Services.ViewSettings;

public sealed record View카탈로그항목(
    string AppKey,
    string ViewKey,
    string DisplayName,
    string Route,
    string IconKey,
    string RoleName,
    bool IsRequired,
    bool DefaultPolicyEnabled,
    int SortOrder);

public static class View카탈로그
{
    private static readonly IReadOnlyList<View카탈로그항목> Items =
    [
        new(App식별자.DriverApp, View식별자.Driver.Home, "홈", "/", Icons.Driver.Home, 역할명.기사, true, true, 10),
        new(App식별자.DriverApp, View식별자.Driver.DriverHome, "기사 홈", "/driver/home", Icons.Driver.DriverHome, 역할명.기사, true, true, 20),
        new(App식별자.DriverApp, View식별자.Driver.Recommendations, "추천 목록", "/driver/recommendations", Icons.Driver.Recommendations, 역할명.기사, false, true, 40),
        new(App식별자.DriverApp, View식별자.Driver.ExplorationCampaigns, "보낸 탐색 문의함", "/driver/exploration/campaigns", Icons.Driver.ExplorationCampaigns, 역할명.기사, false, true, 50),
        new(App식별자.DriverApp, View식별자.Driver.Reservations, "예약", "/driver/reservations", Icons.Driver.Reservations, 역할명.기사, false, true, 60),
        new(App식별자.DriverApp, View식별자.Driver.CurrentTransport, "진행 중 운송", "/driver/transports/current", Icons.Driver.CurrentTransport, 역할명.기사, true, true, 70),
        new(App식별자.DriverApp, View식별자.Driver.Settlements, "월 정산", "/driver/settlements/current-month", Icons.Driver.Settlements, 역할명.기사, false, true, 80),
        new(App식별자.DriverApp, View식별자.Driver.Notifications, "알림함", "/driver/notifications", Icons.Driver.Notifications, 역할명.기사, false, true, 90),
        new(App식별자.DriverApp, View식별자.Driver.ViewSettings, "화면 설정", "/driver/settings/views", Icons.Common.Settings, 역할명.기사, true, true, 100),

        new(App식별자.HongdalApp, View식별자.Shipper.Home, "Home", "/", Icons.Common.Home, 역할명.화주, true, true, 10),
        new(App식별자.HongdalApp, View식별자.Shipper.TransportWorkspace, "운송 업무", "/shipper/transport", Icons.Shipper.TransportWorkspace, 역할명.화주, true, true, 15),
        new(App식별자.HongdalApp, View식별자.Shipper.Request, "화물운송의뢰 등록", "/shipper/request", Icons.Shipper.Request, 역할명.화주, true, true, 20),
        new(App식별자.HongdalApp, View식별자.Shipper.PublicCargo, "공개 화물정보", "/shipper/public-cargo", Icons.Shipper.PublicCargo, 역할명.화주, false, true, 30),
        new(App식별자.HongdalApp, View식별자.Shipper.ExplorationInbox, "받은 탐색 문의함", "/shipper/exploration/inbox", Icons.Shipper.ExplorationInbox, 역할명.화주, false, true, 40),
        new(App식별자.HongdalApp, View식별자.Shipper.WarehouseWorkspace, "창고 작업대", "/shipper/warehouse/workspace", Icons.Shipper.WarehouseWorkspace, 역할명.화주, false, true, 50),
        new(App식별자.HongdalApp, View식별자.Shipper.InboundDashboard, "입고 대시보드", "/shipper/inbound/dashboard", Icons.Shipper.InboundDashboard, 역할명.화주, false, true, 60),
        new(App식별자.HongdalApp, View식별자.Shipper.InboundRequests, "입고 현황", "/shipper/inbound/requests", Icons.Shipper.InboundRequests, 역할명.화주, false, true, 70),
        new(App식별자.HongdalApp, View식별자.Shipper.WarehouseInventory, "재고 목록", "/shipper/warehouse/inventory", Icons.Shipper.WarehouseInventory, 역할명.화주, false, true, 80),
        new(App식별자.HongdalApp, View식별자.Shipper.ReconsignmentOrders, "재위탁 운송", "/shipper/reconsignment/orders", Icons.Shipper.ReconsignmentOrders, 역할명.화주, false, true, 90),
        new(App식별자.HongdalApp, View식별자.Shipper.SalesChannels, "판매채널 연결", "/shipper/sales/channels", Icons.Shipper.SalesChannels, 역할명.화주, false, true, 100),
        new(App식별자.HongdalApp, View식별자.Shipper.ProductListings, "출품 관리", "/shipper/sales/listings", Icons.Shipper.ProductListings, 역할명.화주, false, true, 110),
        new(App식별자.HongdalApp, View식별자.Shipper.OrderFulfillment, "주문 출고 알림", "/shipper/sales/orders", Icons.Shipper.OrderFulfillment, 역할명.화주, false, true, 120),
        new(App식별자.HongdalApp, View식별자.Shipper.CustomsHsReviews, "HS 코드 검토", "/shipper/customs/hs-reviews", Icons.Shipper.CustomsHsReviews, 역할명.화주, false, true, 130),
        new(App식별자.HongdalApp, View식별자.Shipper.ViewSettings, "화면 설정", "/shipper/settings/views", Icons.Common.Settings, 역할명.화주, true, true, 140),

        new(App식별자.HongdalAdmin, View식별자.Admin.Home, "홈", "/", Icons.Common.Home, 역할명.서버관리자, true, true, 10),
        new(App식별자.HongdalAdmin, View식별자.Admin.Dashboard, "01. 업무 안내판", "/dashboard", Icons.Admin.Dashboard, 역할명.서버관리자, true, true, 20),
        new(App식별자.HongdalAdmin, View식별자.Admin.DispatchWait, "02. 유입/배차대기", "/dispatch/wait", Icons.Admin.DispatchWait, 역할명.서버관리자, true, true, 30),
        new(App식별자.HongdalAdmin, View식별자.Admin.ExplorationCampaigns, "03. 탐색 캠페인", "/exploration/campaigns", Icons.Admin.ExplorationCampaigns, 역할명.서버관리자, false, true, 40),
        new(App식별자.HongdalAdmin, View식별자.Admin.Requests, "04. 의뢰 관리", "/requests", Icons.Admin.Requests, 역할명.서버관리자, true, true, 50),
        new(App식별자.HongdalAdmin, View식별자.Admin.Payments, "05. 결제 관리", "/payments", Icons.Admin.Payments, 역할명.서버관리자, false, true, 60),
        new(App식별자.HongdalAdmin, View식별자.Admin.Transports, "06. 운송 진행", "/transports", Icons.Admin.Transports, 역할명.서버관리자, false, true, 70),
        new(App식별자.HongdalAdmin, View식별자.Admin.DriverOperating, "07. 기사 운행현황", "/drivers/operating", Icons.Admin.DriverOperating, 역할명.서버관리자, false, true, 80),
        new(App식별자.HongdalAdmin, View식별자.Admin.FilesPod, "08. 파일/POD", "/files/pod", Icons.Admin.FilesPod, 역할명.서버관리자, false, true, 90),
        new(App식별자.HongdalAdmin, View식별자.Admin.Settlements, "09. 정산 관리", "/settlements", Icons.Admin.Settlements, 역할명.서버관리자, false, true, 100),
        new(App식별자.HongdalAdmin, View식별자.Admin.Drivers, "10. 기사 관리", "/drivers", Icons.Admin.Drivers, 역할명.서버관리자, false, true, 110),
        new(App식별자.HongdalAdmin, View식별자.Admin.Partners, "11. 업체/화주 관리", "/partners", Icons.Admin.Partners, 역할명.서버관리자, false, true, 120),
        new(App식별자.HongdalAdmin, View식별자.Admin.PublicCargo, "12. 공개 화물정보", "/cargo", Icons.Admin.PublicCargo, 역할명.서버관리자, false, true, 130),
        new(App식별자.HongdalAdmin, View식별자.Admin.VehicleManagement, "13. 차량 추천/단가 관리", "/vehicle-management", Icons.Admin.VehicleManagement, 역할명.서버관리자, false, true, 140),
        new(App식별자.HongdalAdmin, View식별자.Admin.Documents, "14. 문서 관리", "/documents", Icons.Admin.Documents, 역할명.서버관리자, false, true, 150),
        new(App식별자.HongdalAdmin, View식별자.Admin.ViewPolicies, "15. 화면 정책", "/view-policies", Icons.Common.Settings, 역할명.서버관리자, true, true, 160),
        new(App식별자.HongdalAdmin, View식별자.Admin.ActivityLogs, "16. 사용자 행위 로그", "/activity-logs", Icons.Admin.ActivityLogs, 역할명.서버관리자, true, true, 170),
        new(App식별자.HongdalAdmin, View식별자.Admin.FoodOperations, "17. 음식 운영", "/food/operations", Icons.Admin.FoodOperations, 역할명.서버관리자, false, true, 180),
        new(App식별자.HongdalAdmin, View식별자.Admin.HsCodeOperations, "18. HS 코드 운영", "/customs/hs-codes", Icons.Admin.HsCodeOperations, 역할명.서버관리자, false, true, 190),

        new(App식별자.HongdalAdmin, View식별자.CustomsBroker.HsCodeOperations, "HS 코드 보정", "/customs/hs-codes", Icons.Admin.HsCodeOperations, 역할명.관세사, true, true, 10)
    ];

    public static IReadOnlyList<View카탈로그항목> 전체() => Items;

    public static IReadOnlyList<View카탈로그항목> 앱별(string appKey, string roleName)
    {
        return Items
            .Where(x => x.AppKey == appKey && x.RoleName == roleName)
            .OrderBy(x => x.SortOrder)
            .ToArray();
    }

    public static View카탈로그항목? 찾기(string appKey, string roleName, string viewKey)
    {
        return Items.FirstOrDefault(x => x.AppKey == appKey && x.RoleName == roleName && x.ViewKey == viewKey);
    }

    public static class Icons
    {
        public static class Common
        {
            public const string Home = "home";
            public const string Settings = "settings";
        }

        public static class Driver
        {
            public const string Home = "home";
            public const string DriverHome = "directions_car";
            public const string Recommendations = "recommend";
            public const string ExplorationCampaigns = "outbox";
            public const string Reservations = "event";
            public const string CurrentTransport = "local_shipping";
            public const string Settlements = "receipt_long";
            public const string Notifications = "notifications";
        }

        public static class Shipper
        {
            public const string TransportWorkspace = "local_shipping";
            public const string Request = "add_box";
            public const string PublicCargo = "view_list";
            public const string ExplorationInbox = "inbox";
            public const string WarehouseWorkspace = "warehouse";
            public const string InboundDashboard = "dashboard";
            public const string InboundRequests = "inventory_2";
            public const string WarehouseInventory = "warehouse";
            public const string ReconsignmentOrders = "local_shipping";
            public const string SalesChannels = "storefront";
            public const string ProductListings = "sell";
            public const string OrderFulfillment = "outbox";
            public const string CustomsHsReviews = "fact_check";
        }

        public static class Admin
        {
            public const string Dashboard = "bi bi-info-circle-nav-menu";
            public const string DispatchWait = "bi bi-arrow-repeat-nav-menu";
            public const string ExplorationCampaigns = "bi bi-megaphone-nav-menu";
            public const string Requests = "bi bi-file-earmark-text-nav-menu";
            public const string Payments = "bi bi-credit-card-nav-menu";
            public const string Transports = "bi bi-truck-nav-menu";
            public const string DriverOperating = "bi bi-person-badge-nav-menu";
            public const string FilesPod = "bi bi-paperclip-nav-menu";
            public const string Settlements = "bi bi-calculator-nav-menu";
            public const string Drivers = "bi bi-people-nav-menu";
            public const string Partners = "bi bi-building-nav-menu";
            public const string PublicCargo = "bi bi-box-seam-nav-menu";
            public const string VehicleManagement = "bi bi-truck-flatbed-nav-menu";
            public const string Documents = "bi bi-folder2-open-nav-menu";
            public const string ActivityLogs = "bi bi-journal-text";
            public const string FoodOperations = "bi bi-shop-window";
            public const string HsCodeOperations = "bi bi-tags";
        }
    }
}
