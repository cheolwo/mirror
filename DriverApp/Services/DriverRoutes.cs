namespace DriverApp.Services;

public static class DriverRoutes
{
    public const string Login = "/login";
    public const string Home = "/driver/home";
    public const string Menu = "/driver/menu";
    public const string WorkStart = "/driver/work/start";
    public const string CommunityInquiries = "/driver/work/community-inquiries";
    public const string Recommendations = "/driver/recommendations";
    public const string Reservations = "/driver/reservations";
    public const string CurrentTransport = "/driver/transports/current";
    public const string DeliveryHistory = "/driver/transports/history";
    public const string CurrentMonthSettlement = "/driver/settlements/current-month";
    public const string SettlementInfo = "/driver/settlements/info";
    public const string BankAccount = "/driver/account/bank";
    public const string NotificationSettings = "/driver/notifications/settings";

    public static string RecommendationDetail(string requestId) => $"/driver/recommendations/{requestId}";

    public static string RecommendationDecision(string requestId) => $"/driver/recommendations/{requestId}/decision";

    public static string TransportPickup(long transportId) => $"/driver/transports/{transportId}/pickup";

    public static string TransportDropoff(long transportId) => $"/driver/transports/{transportId}/dropoff";
}
