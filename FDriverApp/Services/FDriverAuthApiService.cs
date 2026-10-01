using System.Net.Http.Json;
using Hongdal.Client.Infrastructure.Security;
using Hongdal.Contracts.Common;

namespace FDriverApp.Services;

public sealed class FDriverAuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly IFDriverAuthSession _session;

    public FDriverAuthApiService(HttpClient httpClient, IFDriverAuthSession session)
    {
        _httpClient = httpClient;
        _session = session;
    }

    public async Task<string?> LoginAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return "아이디와 비밀번호를 입력해 주세요.";
        }

        try
        {
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
                return "로그인에 실패했습니다. 기사 계정과 서버 상태를 확인해 주세요.";
            }

            var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
            if (token is null || !token.Roles.Contains("Driver", StringComparer.OrdinalIgnoreCase)
                              && !token.Roles.Contains("기사", StringComparer.OrdinalIgnoreCase))
            {
                return "기사 권한이 있는 계정으로 로그인해 주세요.";
            }

            await _session.ApplyAsync(token.ToClientAuthTokenSnapshot(), cancellationToken);
            return null;
        }
        catch (HttpRequestException)
        {
            return "살뜰 서비스에 연결할 수 없습니다.";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "로그인 응답 시간이 초과되었습니다.";
        }
    }
}
