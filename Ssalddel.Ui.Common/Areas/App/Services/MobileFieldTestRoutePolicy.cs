using Microsoft.AspNetCore.Components;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>모바일 내부 테스트판에서 명시적으로 동결한 화면만 여는 표시 경계입니다.</summary>
public static class MobileFieldTestRoutePolicy
{
    public static bool IsAllowed(
        Type pageType,
        bool enforce,
        IReadOnlyCollection<string> allowedTemplates)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        ArgumentNullException.ThrowIfNull(allowedTemplates);
        if (!enforce)
        {
            return true;
        }

        return pageType.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Any(route => allowedTemplates.Contains(Normalize(route.Template), StringComparer.OrdinalIgnoreCase));
    }

    private static string Normalize(string route)
    {
        var normalized = string.IsNullOrWhiteSpace(route) ? "/" : route.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }
}
