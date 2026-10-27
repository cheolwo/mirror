namespace Ssalddel.Contracts.Common.Community;

/// <summary>공동운영 원장·선택 회차·업무 단계로 돌아가는 경로만 만듭니다. 실행 명령과 연락처는 전달하지 않습니다.</summary>
public static class 공간공동운영업무Routes
{
    public static IReadOnlyList<string> StageKeys { get; } =
        ["production", "inbound", "storage", "order", "outbound", "delivery", "settlement"];

    public static bool IsKnownStage(string? stageKey) => StageKeys.Contains(stageKey ?? "", StringComparer.Ordinal);

    public static string Detail(string ledgerId, string stageKey, string cycleId)
    {
        if (!ValidLedgerId(ledgerId)) throw new ArgumentException("공동운영 원장 경로를 확인해 주세요.", nameof(ledgerId));
        if (!IsKnownStage(stageKey)) throw new ArgumentException("공동운영 업무 단계를 확인해 주세요.", nameof(stageKey));
        if (!ValidId(cycleId)) throw new ArgumentException("선택한 회차를 확인해 주세요.", nameof(cycleId));
        return 공간공동운영Routes.Home + "/" + Uri.EscapeDataString(ledgerId) + "/work/" + stageKey
            + "?cycle=" + Uri.EscapeDataString(cycleId);
    }

    public static string Overview(string ledgerId, string? cycleId = null)
    {
        if (!ValidLedgerId(ledgerId)) throw new ArgumentException("공동운영 원장 경로를 확인해 주세요.", nameof(ledgerId));
        if (cycleId is not null && !ValidId(cycleId)) throw new ArgumentException("선택한 회차를 확인해 주세요.", nameof(cycleId));
        return 공간공동운영Routes.Home + "/" + Uri.EscapeDataString(ledgerId)
            + (cycleId is null ? "" : "?cycle=" + Uri.EscapeDataString(cycleId));
    }

    /// <summary>기존 원장 상세 또는 회차가 있는 업무 상세만 정규화합니다. 목록·작성 화면은 기존 인증 경계가 따로 허용합니다.</summary>
    public static bool TryCanonical(string? route, out string canonical)
    {
        canonical = "";
        if (string.IsNullOrWhiteSpace(route) || route != route.Trim() || !route.StartsWith('/')
            || route.StartsWith("//", StringComparison.Ordinal) || route.Contains('\\') || route.Contains('#')
            || route.Any(char.IsControl) || !Uri.TryCreate(route, UriKind.Relative, out _)) return false;
        var pieces = route.Split('?', 2);
        var path = pieces[0];
        var decodedPath = Uri.UnescapeDataString(path);
        if (decodedPath.Contains('%') || decodedPath.Contains('\\') || decodedPath.Contains('?') || decodedPath.Contains('#')
            || decodedPath.Any(char.IsControl) || decodedPath.Count(c => c == '/') != path.Count(c => c == '/')
            || decodedPath.Split('/').Any(segment => segment is "." or "..")) return false;
        var prefix = 공간공동운영Routes.Home + "/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal) || !decodedPath.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var segments = decodedPath[prefix.Length..].Split('/');
        if (!ValidLedgerId(segments[0])) return false;
        string? cycleId = null;
        if (pieces.Length == 2)
        {
            // 다른 query·중복 cycle·조작 의도는 가져오지 않습니다. '+'는 호스트 query 바인딩과 같은 공백 의미로 해석합니다.
            if (!pieces[1].StartsWith("cycle=", StringComparison.Ordinal) || pieces[1].Contains('&') || pieces[1].Contains('?')) return false;
            cycleId = Uri.UnescapeDataString(pieces[1][6..].Replace('+', ' '));
            if (!ValidId(cycleId)) return false;
        }
        if (segments.Length == 1) { canonical = Overview(segments[0], cycleId); return true; }
        if (segments.Length != 3 || segments[1] != "work" || !IsKnownStage(segments[2]) || cycleId is null) return false;
        canonical = Detail(segments[0], segments[2], cycleId);
        return true;
    }

    private static bool ValidLedgerId(string? value) => ValidId(value)
        && !value!.Equals("new", StringComparison.OrdinalIgnoreCase)
        && !value.Equals("create", StringComparison.OrdinalIgnoreCase)
        && !value.Equals("login", StringComparison.OrdinalIgnoreCase);
    private static bool ValidId(string? value) => value is { Length: > 0 and <= 191 } && value == value.Trim()
        && value is not "." and not ".." && !value.Any(char.IsControl)
        && !value.Any(c => c is '/' or '\\' or '%' or '?' or '#');
}
