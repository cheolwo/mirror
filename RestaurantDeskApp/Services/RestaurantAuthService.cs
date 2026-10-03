using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;

namespace RestaurantDeskApp.Services;

public sealed record RestaurantAuthResult(bool IsSuccess, string ErrorMessage, bool RequiresLogin = false)
{
    public static RestaurantAuthResult Success { get; } = new(true, string.Empty);
}

public sealed class RestaurantAuthService(
    HttpClient httpClient,
    ClientAuthSession session)
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    public event Action<bool>? SessionEnding;

    public ClientAuthSession Session => session;

    public async Task<RestaurantAuthResult> LoginAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail)
            || string.IsNullOrWhiteSpace(password))
        {
            return new RestaurantAuthResult(false, "아이디와 비밀번호를 입력해 주세요.");
        }

        return await SendTokenRequestAsync(
            "api/v1/auth/login",
            new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail.Trim(),
                Password = password
            },
            "로그인에 실패했습니다. 음식점 계정과 비밀번호를 확인해 주세요.",
            cancellationToken);
    }

    public async Task<RestaurantAuthResult> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var state = await session.RestoreAsync(cancellationToken);
            if (!forceRefresh && state == ClientAuthSessionRestoreState.Authenticated)
            {
                return RestaurantAuthResult.Success;
            }

            if (string.IsNullOrWhiteSpace(session.UserId)
                || string.IsNullOrWhiteSpace(session.RefreshToken)
                || session.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                await InvalidateRejectedSessionAsync(cancellationToken);
                return new RestaurantAuthResult(
                    false,
                    "로그인 세션이 없습니다. 음식점 계정으로 로그인해 주세요.",
                    RequiresLogin: true);
            }

            var result = await SendTokenRequestAsync(
                "api/v1/auth/refresh",
                new 토큰갱신요청
                {
                    UserId = session.UserId,
                    RefreshToken = session.RefreshToken
                },
                "로그인 세션을 갱신하지 못했습니다. 다시 로그인해 주세요.",
                cancellationToken);
            if (!result.IsSuccess && result.RequiresLogin)
            {
                await InvalidateRejectedSessionAsync(cancellationToken);
            }

            return result;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        SessionEnding?.Invoke(true);
        return session.ClearAsync(cancellationToken);
    }

    internal async Task InvalidateRejectedSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SessionEnding?.Invoke(false);
        try
        {
            await session.ClearAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // ClearAsync has already made this in-memory session anonymous.
            // A secure-store failure must not hide the definitive authentication rejection.
        }
    }

    private async Task<RestaurantAuthResult> SendTokenRequestAsync<TRequest>(
        string path,
        TRequest request,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var requiresLogin = response.StatusCode is HttpStatusCode.BadRequest
                    or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
                return new RestaurantAuthResult(false,
                    requiresLogin ? failureMessage : "인증 서버에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.",
                    requiresLogin);
            }

            var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken)
                || string.IsNullOrWhiteSpace(token.UserId) || token.Roles is null
                || token.AccessTokenExpiresAtUtc <= DateTime.UtcNow
                || string.IsNullOrWhiteSpace(token.RefreshToken)
                || token.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                return new RestaurantAuthResult(false, "서버 인증 응답을 읽을 수 없습니다.");
            }

            if (!token.Roles.Contains("음식점", StringComparer.OrdinalIgnoreCase))
            {
                return new RestaurantAuthResult(
                    false,
                    "음식점 권한이 있는 계정으로 로그인해 주세요.",
                    RequiresLogin: true);
            }

            await session.ApplyAsync(token.ToClientAuthTokenSnapshot(), cancellationToken);
            return RestaurantAuthResult.Success;
        }
        catch (HttpRequestException)
        {
            return new RestaurantAuthResult(false, "살뜰 서버에 연결할 수 없습니다.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RestaurantAuthResult(false, "인증 서버 응답 시간이 초과되었습니다.");
        }
        catch (JsonException)
        {
            return new RestaurantAuthResult(false, "서버 인증 응답을 확인하지 못했습니다. 잠시 후 다시 시도해 주세요.");
        }
    }
}
