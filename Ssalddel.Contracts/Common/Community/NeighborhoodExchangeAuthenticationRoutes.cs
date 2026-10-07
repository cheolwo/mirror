using Ssalddel.Contracts.Common.Commerce;

namespace Ssalddel.Contracts.Common.Community;

/// <summary>계정 인증과 생활 교류 업무의 복귀 경계를 분리합니다.</summary>
public static class NeighborhoodExchangeAuthenticationRoutes
{
    public const string Login = "/community/login";

    public static string LoginHref(string? returnUrl)
        => $"{Login}?returnUrl={Uri.EscapeDataString(ReturnRoute(returnUrl))}";

    public static string ReturnRoute(string? returnUrl)
    {
        if (CommerceAuthenticationRoutes.TryReturnRoute(returnUrl, out var commerceRoute)) return commerceRoute;
        var candidate = returnUrl?.Trim() ?? string.Empty;
        if (!candidate.StartsWith('/') || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.Contains('\\') || candidate.Any(char.IsControl)
            || !Uri.TryCreate(candidate, UriKind.Relative, out _))
        {
            return NeighborhoodExchange.Home;
        }

        var path = candidate.Split('?', '#')[0];
        var decodedPath = Uri.UnescapeDataString(path);
        if (decodedPath.Contains('%') || decodedPath.Contains('\\') || decodedPath.Any(char.IsControl)
            || decodedPath.Contains('?') || decodedPath.Contains('#')
            || decodedPath.Count(character => character == '/') != path.Count(character => character == '/')
            || decodedPath.Split('/').Any(segment => segment is "." or ".."))
        {
            return NeighborhoodExchange.Home;
        }

        return string.Equals(decodedPath, "/community/map", StringComparison.Ordinal)
               || string.Equals(decodedPath, "/workspace/community", StringComparison.Ordinal)
               || string.Equals(decodedPath, NeighborhoodExchange.Home, StringComparison.Ordinal)
               || decodedPath.StartsWith(NeighborhoodExchange.Home + "/", StringComparison.Ordinal)
            ? candidate
            : NeighborhoodExchange.Home;
    }
}
