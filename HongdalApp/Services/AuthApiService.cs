using System.Net.Http.Json;
using Hongdal.Client.Infrastructure.Security;
using Hongdal.Contracts.Common;

namespace HongdalApp.Services;

public sealed class AuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly 꾸미기보유권동기화Service _꾸미기보유권동기화Service;

    public AuthApiService(
        HttpClient httpClient,
        IAuthSession authSession,
        꾸미기보유권동기화Service 꾸미기보유권동기화Service)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _꾸미기보유권동기화Service = 꾸미기보유권동기화Service;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "아이디와 비밀번호를 입력해 주세요.");
        }

        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/auth/login",
            new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail.Trim(),
                Password = password
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (false, "서버 로그인에 실패했습니다. 아이디, 비밀번호, 서버 실행 상태를 확인해 주세요.");
        }

        var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
        if (token is null)
        {
            return (false, "서버 로그인 응답을 읽을 수 없습니다.");
        }

        await _authSession.ApplyAsync(token.ToClientAuthTokenSnapshot(), cancellationToken);
        await _꾸미기보유권동기화Service.RestoreAndSynchronizeAsync(cancellationToken);
        return (true, string.Empty);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _꾸미기보유권동기화Service.ClearVisibleEntitlements();
        await _authSession.ClearAsync(cancellationToken);
    }
}
