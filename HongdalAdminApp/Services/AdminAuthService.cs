using System.Net.Http.Json;
using Hongdal.Contracts.Common;

namespace HongdalAdminApp.Services;

public sealed class AdminAuthService
{
    private readonly HttpClient httpClient;
    private readonly AdminAuthSession session;

    public AdminAuthService(HttpClient httpClient, AdminAuthSession session)
    {
        this.httpClient = httpClient;
        this.session = session;
    }

    public async Task<AdminLoginResult> LoginAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/auth/login",
            new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail,
                Password = password
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return AdminLoginResult.Fail($"로그인에 실패했습니다. ({(int)response.StatusCode})");
        }

        var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return AdminLoginResult.Fail("서버에서 로그인 토큰을 받지 못했습니다.");
        }

        if (!token.Roles.Contains("서버관리자", StringComparer.Ordinal))
        {
            return AdminLoginResult.Fail("서버관리자 권한이 있는 계정만 사용할 수 있습니다.");
        }

        await session.ApplyAsync(token);
        return AdminLoginResult.Success();
    }
}

public sealed record AdminLoginResult(bool Succeeded, string? ErrorMessage)
{
    public static AdminLoginResult Success() => new(true, null);
    public static AdminLoginResult Fail(string message) => new(false, message);
}
