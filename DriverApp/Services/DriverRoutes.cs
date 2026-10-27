namespace DriverApp.Services;

public static class DriverRoutes
{
    public const string Login = "/login";
    public const string PublicCargo = "/driver/public-cargo";
    public const string Home = "/driver/home";
    public const string HomeSummary = "/driver/home/summary";
    public const string Menu = "/driver/menu";
    public const string WorkStart = "/driver/work/start";
    public const string CommunityInquiries = "/driver/work/community-inquiries";
    public const string Recommendations = "/driver/recommendations";
    public const string ExplorationCampaigns = "/driver/exploration/campaigns";
    public const string Reservations = "/driver/reservations";
    public const string FoodDeliveries = "/driver/food-deliveries";
    public const string CurrentTransport = "/driver/transports/current";
    public const string DeliveryHistory = "/driver/transports/history";
    public const string Payouts = "/driver/payouts";
    public const string CurrentMonthSettlement = "/driver/settlements/current-month";
    public const string SettlementInfo = "/driver/settlements/info";
    public const string BankAccount = "/driver/account/bank";
    public const string Notifications = "/driver/notifications";
    public const string NotificationSettings = "/driver/notifications/settings";

    public static string LoginFor(string returnRoute)
        => $"{Login}?returnUrl={Uri.EscapeDataString(returnRoute)}";

    public static string RecommendationDetail(string requestId) => $"/driver/recommendations/{requestId}";

    public static string RecommendationDecision(string requestId) => $"/driver/recommendations/{requestId}/decision";

    public static string TransportPickup(long transportId) => $"/driver/transports/{transportId}/pickup";

    public static string TransportDropoff(long transportId) => $"/driver/transports/{transportId}/dropoff";

    public static string PayoutList(int year, int month) => $"{Payouts}?year={year}&month={month}";

    public static string PayoutDetail(long transportId, int year, int month)
        => $"{Payouts}/{transportId}?year={year}&month={month}";
}
