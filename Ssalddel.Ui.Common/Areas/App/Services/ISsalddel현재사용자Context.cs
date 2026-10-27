using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Orderer;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>
/// 화면이 표시와 입력 제어에 사용할 로그인 사용자 스냅샷입니다.
/// 서버 권한 판정은 이 값이 아니라 요청의 Bearer 토큰과 서버 클레임을 기준으로 합니다.
/// </summary>
public sealed record 주문자집단배송권Snapshot(
    string ScopeKey,
    string DisplayName,
    string Basis);

public sealed record 현재사용자Snapshot(
    string? UserId,
    string? UserName,
    IReadOnlyList<string> Roles,
    주문자집단배송권Snapshot? 주문자집단배송권 = null)
{
    public static 현재사용자Snapshot 익명 { get; } = new(null, null, []);

    public bool 인증됨 => !string.IsNullOrWhiteSpace(UserId);

    public bool 역할보유(string role)
        => !string.IsNullOrWhiteSpace(role)
           && Roles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);
}

public interface ISsalddel현재사용자Context
{
    현재사용자Snapshot 현재사용자 { get; }
}

/// <summary>로그인 저장소가 제공하는 변경 신호입니다. 같은 계정의 토큰 갱신도 포함할 수 있습니다.</summary>
public interface ISsalddel현재사용자변경알림
{
    event Action? Changed;
}

/// <summary>
/// 앱의 인증 토큰 공급자와 같은 수명 범위에서 현재 사용자 정보를 읽습니다.
/// 토큰 내용은 UI 제어에만 사용하며 서버 접근 권한을 대신하지 않습니다.
/// </summary>
internal sealed class SsalddelAccessToken현재사용자Context(
    ISsalddelAccessTokenProvider accessTokenProvider) : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
{
    private string? _마지막Token;
    private 현재사용자Snapshot _마지막Snapshot = 현재사용자Snapshot.익명;

    // 기존 세션의 이벤트를 그대로 연결하며 Context 자체의 별도 영구 구독을 만들지 않는다.
    public event Action? Changed
    {
        add { if (accessTokenProvider is ISsalddel현재사용자변경알림 source) source.Changed += value; }
        remove { if (accessTokenProvider is ISsalddel현재사용자변경알림 source) source.Changed -= value; }
    }

    public 현재사용자Snapshot 현재사용자
    {
        get
        {
            var token = accessTokenProvider.AccessToken?.Trim();
            if (string.Equals(token, _마지막Token, StringComparison.Ordinal))
            {
                return _마지막Snapshot;
            }

            _마지막Token = token;
            _마지막Snapshot = Parse(token);
            return _마지막Snapshot;
        }
    }

    private static 현재사용자Snapshot Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return 현재사용자Snapshot.익명;
        }

        var segments = token.Split('.');
        if (segments.Length < 2)
        {
            return 현재사용자Snapshot.익명;
        }

        try
        {
            using var document = JsonDocument.Parse(DecodeBase64Url(segments[1]));
            var root = document.RootElement;
            var userId = FirstString(
                root,
                "sub",
                "nameid",
                "userId",
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 현재사용자Snapshot.익명;
            }

            var userName = FirstString(
                root,
                "name",
                "unique_name",
                "preferred_username",
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
            var roles = ReadStrings(
                    root,
                    "role",
                    "roles",
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var scopeKey = FirstString(root, 주문자집단배송권ClaimTypes.ScopeKey);
            var ordererGroupScope = string.IsNullOrWhiteSpace(scopeKey)
                ? null
                : new 주문자집단배송권Snapshot(
                    scopeKey.Trim(),
                    FirstString(root, 주문자집단배송권ClaimTypes.DisplayName)?.Trim() ?? scopeKey.Trim(),
                    FirstString(root, 주문자집단배송권ClaimTypes.Basis)?.Trim() ?? string.Empty);

            return new 현재사용자Snapshot(userId.Trim(), userName?.Trim(), roles, ordererGroupScope);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return 현재사용자Snapshot.익명;
        }
    }

    private static string? FirstString(JsonElement root, params string[] names)
        => names.Select(name => TryReadString(root, name))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? TryReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<string> ReadStrings(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                yield return value.GetString()!;
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        yield return item.GetString()!;
                    }
                }
            }
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += new string('=', (4 - normalized.Length % 4) % 4);
        return Convert.FromBase64String(normalized);
    }
}
