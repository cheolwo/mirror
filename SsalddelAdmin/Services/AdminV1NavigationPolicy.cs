namespace SsalddelAdmin.Services;

public sealed record AdminV1NavigationItem(
    string Label,
    string Route,
    string IconKey,
    bool Exact = false,
    bool ServerAdminOnly = true);

public static class AdminV1NavigationPolicy
{
    public static IReadOnlyList<AdminV1NavigationItem> MenuItems { get; } =
    [
        new("커뮤니티 운영", "/community", "home", true),
        new("사용자·콘텐츠 관리", "/community/users", "manage_search"),
        new("공통 콘텐츠 관리", "/common-contents", "description"),
        new("공간자료 보관함", "/spatial-catalog", "public"),
        new("운영 감사 기록", "/activity-logs", "fact_check"),
        new("후속 처리 복구", "/operations/follow-up-recovery", "healing"),
        new("공개 범위 정책", "/view-policies", "settings")
    ];

    private static readonly HashSet<string> ExactRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/community",
        "/community/users",
        "/common-contents",
        "/spatial-catalog",
        "/activity-logs",
        "/view-policies",
        "/dashboard",
        "/trade-readiness",
        "/customs/hs-codes",
        "/dispatch/wait",
        "/food/order-trace",
        "/operations/follow-up-recovery",
        "/admin/food-delivery",
        "/admin/food-delivery/operations",
        "/admin/food-delivery/order-trace",
        "/admin/food-delivery/dispatch-ai-review",
        "/admin/freight-delivery",
        "/admin/freight-delivery/requests",
        "/admin/freight-delivery/drivers",
        "/admin/freight-delivery/dispatch-wait",
        "/admin/freight-delivery/dispatch-ai-review",
        "/admin/freight-delivery/transports",
        "/admin/freight-delivery/vehicles",
        "/admin/order-warehouse",
        "/admin/order-warehouse/dashboard",
        "/admin/order-warehouse/outbound-requests",
        "/admin/order-warehouse/outbound-transports",
        "/admin/order-warehouse/documents",
        "/drivers/operating",
        "/payments",
        "/settlements",
        "/documents",
        "/documents/upload",
        "/documents/policies",
        "/documents/logs",
        "/files/pod",
        "/login",
        "/error",
        "/not-found"
    };

    private static readonly string[] PrefixRoutes =
    [
        "/requests/",
        "/transports/",
        "/documents/",
    ];

    public static bool IsAllowedRoute(string? relativePath)
    {
        var path = NormalizePath(relativePath);
        return ExactRoutes.Contains(path)
               || PrefixRoutes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static string ToHref(string route)
        => NormalizePath(route).TrimStart('/');

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim();
        var queryIndex = normalized.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            normalized = normalized[..queryIndex];
        }

        normalized = normalized.StartsWith('/') ? normalized : "/" + normalized;
        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }
}
