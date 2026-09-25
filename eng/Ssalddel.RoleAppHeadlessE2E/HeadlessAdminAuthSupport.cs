using System.Net.Http.Json;
using Ssalddel.Contracts.Common;

namespace SsalddelAdminApp.Services;

/// <summary>
/// 장치 보안 저장소만 대체하는 headless 인증 상태입니다.
/// 관리자 업무 HTTP 호출은 실제 앱의 AdminAuthenticatedApiClient를 그대로 사용합니다.
/// </summary>
public sealed class AdminAuthSession
{
    public string? AccessToken { get; private set; }

    public void Apply(토큰응답 response)
        => AccessToken = response.AccessToken;
}

public sealed record AdminLoginResult(bool Succeeded, string? ErrorMessage)
{
    public static AdminLoginResult Success() => new(true, null);
    public static AdminLoginResult Fail(string message) => new(false, message);
}

public sealed class AdminAuthService(HttpClient httpClient, AdminAuthSession session)
{
    public async Task<AdminLoginResult> LoginAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/auth/login",
            new 로그인요청 { UserNameOrEmail = userNameOrEmail, Password = password },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return AdminLoginResult.Fail($"관리자 로그인 실패 ({(int)response.StatusCode})");
        }

        var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
        if (token is null
            || string.IsNullOrWhiteSpace(token.AccessToken)
            || !token.Roles.Contains("서버관리자", StringComparer.Ordinal))
        {
            return AdminLoginResult.Fail("서버관리자 권한 토큰을 확인할 수 없습니다.");
        }

        session.Apply(token);
        return AdminLoginResult.Success();
    }

    public Task<string?> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(string.IsNullOrWhiteSpace(session.AccessToken)
            ? "관리자 로그인 세션이 없습니다."
            : null);
    }
}
