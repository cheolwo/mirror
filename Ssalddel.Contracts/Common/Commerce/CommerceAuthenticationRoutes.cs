using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Contracts.Common.Commerce;

/// <summary>인증에 전달하는 보호 업무의 경로만 보존합니다. 자격 입력·요청 내용·업무 실행은 보존하지 않습니다.</summary>
public static class CommerceAuthenticationRoutes
{
    public const string Notices = "/commerce/notices";
    public const string Seller = "/commerce/seller";
    public const string Privacy = "/commerce/privacy";
    public const string Disputes = "/commerce/disputes";

    public static string PrivacyRoute(string? caseId)
        => ReturnRoute(Privacy + CaseSuffix(caseId));

    public static string DisputeRoute(string? caseId, string? sourceKind = null, string? sourceId = null, string? returnPath = null)
    {
        var route = ReturnRoute(Disputes + (string.IsNullOrWhiteSpace(caseId)
            ? string.IsNullOrWhiteSpace(sourceKind) && string.IsNullOrWhiteSpace(sourceId) ? ""
                : "?sourceKind=" + Uri.EscapeDataString(sourceKind ?? "") + "&sourceId=" + Uri.EscapeDataString(sourceId ?? "")
            : CaseSuffix(caseId)));
        return route == Notices ? route : ReturnRoute(PageNavigationContext.WithReturnPath(route, returnPath));
    }

    public static string LoginHref(string loginRoute, string? returnRoute)
    {
        // 호출자는 앱에 존재하는 인증 route를 지정합니다. 잘못된 값은 링크로 만들지 않습니다.
        if (loginRoute is not ("/login" or "/community/login")) throw new ArgumentException("지원하는 로컬 인증 경로를 지정해 주세요.", nameof(loginRoute));
        return loginRoute + "?returnUrl=" + Uri.EscapeDataString(ReturnRoute(returnRoute));
    }

    public static string ReturnRoute(string? returnRoute)
        => TryReturnRoute(returnRoute, out var route) ? route : Notices;

    public static bool TryReturnRoute(string? returnRoute, out string route)
    {
        route = Notices;
        var candidate = returnRoute?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 2048
            || !candidate.StartsWith('/') || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.Contains('\\') || candidate.Contains('#') || candidate.Any(char.IsControl)
            || !Uri.TryCreate(candidate, UriKind.Relative, out _)) return false;

        var parts = candidate.Split('?', 2);
        var path = Uri.UnescapeDataString(parts[0]);
        if (path.Contains('%') || path.Contains('\\') || path.Contains('?') || path.Contains('#') || path.Any(char.IsControl)
            || path.Count(c => c == '/') != parts[0].Count(c => c == '/')
            || path.Split('/').Any(segment => segment is "." or "..")) return false;

        if (path is Notices or Seller or Privacy)
        {
            if (parts.Length > 1 && parts[1].Length > 0) return false;
            route = path;
            return true;
        }

        foreach (var prefix in new[] { Privacy, Disputes })
        {
            if (!path.StartsWith(prefix + "/", StringComparison.Ordinal)) continue;
            var caseId = path[(prefix.Length + 1)..];
            if (!ValidIdentifier(caseId) || caseId.Contains('/')) return false;
            if (prefix == Privacy && parts.Length > 1 && parts[1].Length > 0) return false;
            if (prefix == Disputes)
            {
                if (!TryDisputeQuery(parts.Length > 1 ? parts[1] : "", allowSource: false, out var caseQuery)) return false;
                route = PageNavigationContext.WithReturnPath(prefix + "/" + Uri.EscapeDataString(caseId),
                    caseQuery.GetValueOrDefault(PageNavigationQueryNames.ReturnPath));
                return true;
            }
            route = prefix + "/" + Uri.EscapeDataString(caseId);
            return true;
        }

        if (path != Disputes) return false;
        if (parts.Length == 1 || parts[1].Length == 0) { route = Disputes; return true; }
        if (!TryDisputeQuery(parts[1], allowSource: true, out var query)) return false;
        var from = query.GetValueOrDefault(PageNavigationQueryNames.ReturnPath);
        if (!query.ContainsKey("sourceKind") && !query.ContainsKey("sourceId"))
        { route = PageNavigationContext.WithReturnPath(Disputes, from); return true; }
        if (!query.TryGetValue("sourceKind", out var kind) || !query.TryGetValue("sourceId", out var id)) return false;
        if (kind.Length == 0 && id.Length == 0) { route = PageNavigationContext.WithReturnPath(Disputes, from); return true; }
        if (kind is not ("food-order" or "neighborhood-delivery" or "neighborhood-collaboration" or "cargo-request") || !ValidIdentifier(id)) return false;
        route = PageNavigationContext.WithReturnPath(Disputes + "?sourceKind=" + Uri.EscapeDataString(kind) + "&sourceId=" + Uri.EscapeDataString(id), from);
        return true;
    }

    private static bool TryDisputeQuery(string raw, bool allowSource, out Dictionary<string, string> query)
    {
        query = new(StringComparer.Ordinal);
        if (raw.Length == 0) return true;
        foreach (var pair in raw.Split('&'))
        {
            var item = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(item[0].Replace('+', ' '));
            var value = item.Length == 2 ? Uri.UnescapeDataString(item[1].Replace('+', ' ')) : "";
            if (key != PageNavigationQueryNames.ReturnPath && (!allowSource || key is not ("sourceKind" or "sourceId"))
                || !query.TryAdd(key, value) || value.Any(char.IsControl)) return false;
        }
        if (query.TryGetValue(PageNavigationQueryNames.ReturnPath, out var from))
        {
            var normalized = PageNavigationContext.NormalizeReturnPath(from);
            if (normalized is null) return false;
            query[PageNavigationQueryNames.ReturnPath] = normalized;
        }
        return true;
    }

    private static string CaseSuffix(string? caseId)
        => string.IsNullOrWhiteSpace(caseId) ? "" : "/" + Uri.EscapeDataString(caseId);
    private static bool ValidIdentifier(string id)
        => !string.IsNullOrWhiteSpace(id) && id.Length <= 160 && !id.Any(char.IsControl);
}
