namespace Hongdal.WebApp.Services;

public static class GlobalTradeRoutes
{
    public const string Home = "/global";
    public const string SupplierApply = "/global/suppliers/apply";
    public const string ImportRequests = "/global/import-requests";

    public static string Product(string slug)
        => $"/global/products/{Uri.EscapeDataString(slug)}";

    public static string CommunityThread(long threadId)
        => $"/community/global-trade/{threadId}";

    public static string ImportOrder(string orderCode)
        => $"/global/orders/{Uri.EscapeDataString(orderCode)}";
}
