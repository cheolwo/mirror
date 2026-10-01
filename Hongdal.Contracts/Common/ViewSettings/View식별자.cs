namespace Hongdal.Contracts.Common.ViewSettings;

public static class App식별자
{
    public const string DriverApp = "DriverApp";
    public const string CargoYongdalDriverApp = "CargoYongdalDriverApp";
    public const string FoodDeliveryDriverApp = "FoodDeliveryDriverApp";
    public const string HongdalApp = "HongdalApp";
    public const string WarehouseManagerApp = "WarehouseManagerApp";
    public const string HongdalAdmin = "HongdalAdmin";
    public const string RestaurantDeskApp = "RestaurantDeskApp";
}

public static class View식별자
{
    public static class Driver
    {
        public const string Home = "driver.home";
        public const string DriverHome = "driver.driver-home";
        public const string Recommendations = "driver.recommendations";
        public const string ExplorationCampaigns = "driver.exploration-campaigns";
        public const string Reservations = "driver.reservations";
        public const string CurrentTransport = "driver.current-transport";
        public const string CommunityInquiries = "driver.community-inquiries";
        public const string Settlements = "driver.settlements";
        public const string Notifications = "driver.notifications";
        public const string ViewSettings = "driver.view-settings";
    }

    public static class Shipper
    {
        public const string Home = "shipper.home";
        public const string TransportWorkspace = "shipper.transport-workspace";
        public const string Request = "shipper.request";
        public const string PublicCargo = "shipper.public-cargo";
        public const string ExplorationInbox = "shipper.exploration-inbox";
        public const string InboundDashboard = "shipper.inbound-dashboard";
        public const string InboundRequests = "shipper.inbound-requests";
        public const string WarehouseWorkspace = "shipper.warehouse-workspace";
        public const string WarehouseInventory = "shipper.warehouse-inventory";
        public const string ReconsignmentOrders = "shipper.reconsignment-orders";
        public const string SalesChannels = "shipper.sales-channels";
        public const string ProductListings = "shipper.product-listings";
        public const string OrderFulfillment = "shipper.order-fulfillment";
        public const string CustomsHsReviews = "shipper.customs-hs-reviews";
        public const string ViewSettings = "shipper.view-settings";
    }

    public static class Admin
    {
        public const string Home = "admin.home";
        public const string Dashboard = "admin.dashboard";
        public const string DispatchWait = "admin.dispatch-wait";
        public const string ExplorationCampaigns = "admin.exploration-campaigns";
        public const string Requests = "admin.requests";
        public const string Payments = "admin.payments";
        public const string Transports = "admin.transports";
        public const string DriverOperating = "admin.driver-operating";
        public const string FilesPod = "admin.files-pod";
        public const string Settlements = "admin.settlements";
        public const string Drivers = "admin.drivers";
        public const string Partners = "admin.partners";
        public const string PublicCargo = "admin.public-cargo";
        public const string VehicleManagement = "admin.vehicle-management";
        public const string Documents = "admin.documents";
        public const string ViewPolicies = "admin.view-policies";
        public const string ActivityLogs = "admin.activity-logs";
        public const string FoodOperations = "admin.food-operations";
        public const string HsCodeOperations = "admin.hs-code-operations";
        public const string Warehouses = "admin.warehouses";
        public const string WarehouseUsers = "admin.warehouse-users";
        public const string InboundAudit = "admin.inbound-audit";
    }

    public static class CustomsBroker
    {
        public const string HsCodeOperations = "customs-broker.hs-code-operations";
    }
}
