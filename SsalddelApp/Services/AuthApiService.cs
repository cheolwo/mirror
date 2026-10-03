using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Contracts.Common;

namespace SsalddelApp.Services;

public sealed class AuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly 꾸미기보유권동기화Service _꾸미기보유권동기화Service;
    private readonly SsalddelMobilePushInstallationClient _mobilePushInstallationClient;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public AuthApiService(
        HttpClient httpClient,
        IAuthSession authSession,
        꾸미기보유권동기화Service 꾸미기보유권동기화Service,
        SsalddelMobilePushInstallationClient mobilePushInstallationClient)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _꾸미기보유권동기화Service = 꾸미기보유권동기화Service;
        _mobilePushInstallationClient = mobilePushInstallationClient;
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
        await _mobilePushInstallationClient.EnsureRegisteredAsync(cancellationToken);
        await _꾸미기보유권동기화Service.RestoreAndSynchronizeAsync(cancellationToken);
        return (true, string.Empty);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _꾸미기보유권동기화Service.ClearVisibleEntitlements();
        await _authSession.ClearAsync(cancellationToken);
    }

    public async Task<string?> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            await _authSession.RestoreAsync(cancellationToken);
            if (!forceRefresh
                && !string.IsNullOrWhiteSpace(_authSession.AccessToken)
                && _authSession.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddSeconds(30))
            {
                return null;
            }

            var sessionRevision = _authSession.SessionRevision;
            var userId = _authSession.UserId;
            var refreshToken = _authSession.RefreshToken;
            if (string.IsNullOrWhiteSpace(_authSession.UserId)
                || string.IsNullOrWhiteSpace(_authSession.RefreshToken)
                || _authSession.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                await _authSession.TryClearAsync(sessionRevision, refreshToken, cancellationToken);
                return "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.";
            }

            using var response = await _httpClient.PostAsJsonAsync(
                "api/v1/auth/refresh",
                new 토큰갱신요청
                {
                    UserId = userId,
                    RefreshToken = refreshToken
                },
                cancellationToken);
            if (!IsCurrentRefreshSession(sessionRevision, userId, refreshToken))
            {
                return "로그인 세션이 변경되었습니다. 다시 확인해 주세요.";
            }
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                {
                    await _authSession.TryClearAsync(sessionRevision, refreshToken, cancellationToken);
                }

                return "로그인 세션을 갱신하지 못했습니다. 다시 로그인해 주세요.";
            }

            var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
            if (!IsCurrentRefreshSession(sessionRevision, userId, refreshToken))
            {
                return "로그인 세션이 변경되었습니다. 다시 확인해 주세요.";
            }
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return "서버 인증 응답을 읽을 수 없습니다.";
            }

            if (!await _authSession.TryRefreshAsync(token.ToClientAuthTokenSnapshot(), sessionRevision, refreshToken, cancellationToken))
            {
                return "로그인 세션이 변경되었습니다. 다시 확인해 주세요.";
            }
            await _mobilePushInstallationClient.EnsureRegisteredAsync(cancellationToken);
            return null;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private bool IsCurrentRefreshSession(long revision, string? userId, string? refreshToken)
        => _authSession.SessionRevision == revision
           && string.Equals(_authSession.UserId, userId, StringComparison.Ordinal)
           && string.Equals(_authSession.RefreshToken, refreshToken, StringComparison.Ordinal);
}
