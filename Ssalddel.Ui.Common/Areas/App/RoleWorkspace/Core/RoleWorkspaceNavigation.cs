namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>업무 데이터 대신 역할 키와 stable ID만 복귀 경로에 포함합니다.</summary>
public static class RoleWorkspaceNavigation
{
    public static string? StableId(string? value)
        => value is { Length: > 0 and <= 256 }
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':')
                ? value : null;

    public static string Href(string roleKey, string? selectedId = null, string? businessCode = null)
    {
        var role = RoleWorkspaceCatalog.Find(roleKey);
        if (role is null) return "/";
        var context = RoleWorkspaceBusinessContext.Resolve(role.Key, businessCode);
        if (context is null) return "/";
        var id = StableId(selectedId);
        var query = new List<string>();
        // 고유 역할의 기존 URL은 유지하고 두 관리 진입만 명시적으로 구분한다.
        if (role.Key == RoleWorkspaceCatalog.Operator && businessCode is not null)
            query.Add("business=" + Uri.EscapeDataString(context.BusinessCode!));
        if (id is not null) query.Add("selected=" + Uri.EscapeDataString(id));
        return role.Href + (query.Count == 0 ? "" : "?" + string.Join('&', query));
    }

    public static string? LocalRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || !route.StartsWith('/') || route.StartsWith("//", StringComparison.Ordinal)
            || route.Any(character => character is '\\' or '#' || char.IsControl(character))
            || !Uri.TryCreate(route, UriKind.Relative, out _)) return null;
        try
        {
            var path = Uri.UnescapeDataString(route.Split('?', 2)[0]);
            return path.StartsWith("//", StringComparison.Ordinal)
                || path.Any(character => character is '\\' || char.IsControl(character))
                || path.Split('/').Any(segment => segment is "." or "..") ? null : route;
        }
        catch (UriFormatException) { return null; }
    }

    public static string? WithReturn(string? route, string returnHref)
    {
        var local = LocalRoute(route);
        if (local is null) return null;
        var split = local.Split('?', 2);
        var query = split.Length == 2 ? split[1].Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.Split('=', 2)[0].Equals("returnUrl", StringComparison.OrdinalIgnoreCase)).ToList() : [];
        query.Add("returnUrl=" + Uri.EscapeDataString(returnHref));
        return split[0] + "?" + string.Join('&', query);
    }
}
