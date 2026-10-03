using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;

namespace DriverApp.Services;

public sealed class AuthApiService
{
    private const string SessionChangedMessage = "로그인 정보가 변경되었습니다. 현재 업무를 다시 확인해 주세요.";
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public AuthApiService(HttpClient httpClient, IAuthSession authSession)
    {
        _httpClient = httpClient;
        _authSession = authSession;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "아이디와 비밀번호를 입력해 주세요.");
        }

        var result = await SendTokenRequestAsync(
            "api/v1/auth/login",
            new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail.Trim(),
                Password = password
            },
            "서버 로그인에 실패했습니다. 아이디, 비밀번호, 서버 실행 상태를 확인해 주세요.",
            _authSession.Version,
            startsNewSession: true,
            cancellationToken);
        return (result.IsSuccess, result.ErrorMessage ?? string.Empty);
    }

    public async Task<string?> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default,
        long? expectedSessionVersion = null)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            await _authSession.RestoreAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var sessionVersion = _authSession.Version;
            if (expectedSessionVersion is { } expected && expected != sessionVersion)
                return SessionChangedMessage;
            if (!forceRefresh
                && !string.IsNullOrWhiteSpace(_authSession.AccessToken)
                && _authSession.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddSeconds(30))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(_authSession.UserId)
                || string.IsNullOrWhiteSpace(_authSession.RefreshToken)
                || _authSession.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                if (!await TryEndSessionAsync(sessionVersion, cancellationToken))
                    return SessionChangedMessage;
                return "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.";
            }

            var result = await SendTokenRequestAsync(
                "api/v1/auth/refresh",
                new 토큰갱신요청
                {
                    UserId = _authSession.UserId,
                    RefreshToken = _authSession.RefreshToken
                },
                "로그인 정보를 갱신하지 못했습니다. 잠시 후 다시 시도해 주세요.",
                sessionVersion,
                startsNewSession: false,
                cancellationToken);
            if (!result.IsSuccess
                && result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                if (!await TryEndSessionAsync(sessionVersion, cancellationToken))
                    return SessionChangedMessage;
                return "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.";
            }

            return result.ErrorMessage;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<bool> TryEndSessionAsync(long expectedVersion, CancellationToken cancellationToken)
    {
        try
        {
            return await _authSession.TryClearAsync(expectedVersion, cancellationToken);
        }
        catch when (!_authSession.IsAuthenticated && _authSession.Version == expectedVersion + 1
            && !cancellationToken.IsCancellationRequested)
        {
            // In-memory sign-out remains authoritative if secure storage removal fails.
            return true;
        }
    }

    private async Task<TokenRequestResult> SendTokenRequestAsync<TRequest>(
        string path,
        TRequest request,
        string failureMessage,
        long expectedSessionVersion,
        bool startsNewSession,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!response.IsSuccessStatusCode)
            {
                return new TokenRequestResult(false, failureMessage, response.StatusCode);
            }

            var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return new TokenRequestResult(false, "서버 인증 응답을 읽을 수 없습니다.");
            }

            if (!await _authSession.TryApplyAsync(token.ToClientAuthTokenSnapshot(), expectedSessionVersion, cancellationToken, startsNewSession))
                return new TokenRequestResult(false, SessionChangedMessage);
            return TokenRequestResult.Success;
        }
        catch (HttpRequestException)
        {
            return new TokenRequestResult(false, "살뜰 서비스에 연결할 수 없습니다.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TokenRequestResult(false, "로그인 응답 시간이 초과되었습니다.");
        }
    }

    private sealed record TokenRequestResult(
        bool IsSuccess,
        string? ErrorMessage = null,
        HttpStatusCode? StatusCode = null)
    {
        public static TokenRequestResult Success { get; } = new(true);
    }
}
