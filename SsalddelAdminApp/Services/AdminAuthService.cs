using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;

namespace SsalddelAdminApp.Services;

public sealed class AdminAuthService
{
    private const string AuthenticationChanged = "관리자 로그인 상태가 변경되었습니다. 현재 계정으로 다시 조회해 주세요.";
    private readonly HttpClient httpClient;
    private readonly AdminAuthSession session;
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    public AdminAuthService(HttpClient httpClient, AdminAuthSession session)
    { this.httpClient = httpClient; this.session = session; }

    public async Task<AdminLoginResult> LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        var intent = session.BeginAuthenticationChange();
        await session.RestoreAsync(cancellationToken);
        if (!session.TryCaptureAuthenticationAfterRestore(intent, out var context))
            return AdminLoginResult.Fail(AuthenticationChanged);
        var result = await SendTokenRequestAsync("api/v1/auth/login",
            new 로그인요청 { UserNameOrEmail = userNameOrEmail, Password = password },
            "로그인에 실패했습니다.", context, isRefresh: false, cancellationToken);
        return result.Succeeded ? AdminLoginResult.Success() : AdminLoginResult.Fail(result.ErrorMessage ?? "로그인에 실패했습니다.");
    }

    public Task<string?> EnsureAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        => EnsureAccessTokenCoreAsync(forceRefresh, expected: null, cancellationToken);

    internal Task<string?> EnsureAccessTokenForRequestAsync(bool forceRefresh, AdminAuthenticationContext expected,
        CancellationToken cancellationToken = default)
        => EnsureAccessTokenCoreAsync(forceRefresh, expected, cancellationToken);

    private async Task<string?> EnsureAccessTokenCoreAsync(bool forceRefresh, AdminAuthenticationContext? expected,
        CancellationToken cancellationToken)
    {
        var origin = expected ?? session.CaptureAuthentication();
        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            var state = await session.RestoreAsync(cancellationToken);
            if (!session.TryCaptureAuthenticationAfterRestore(origin, out var context)) return AuthenticationChanged;
            if (state == ClientAuthSessionRestoreState.Authenticated
                && (!forceRefresh || (expected.HasValue
                    && !string.Equals(expected.Value.AccessToken, context.AccessToken, StringComparison.Ordinal))))
                return null;
            if (string.IsNullOrWhiteSpace(context.UserId) || string.IsNullOrWhiteSpace(context.RefreshToken)
                || session.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
                return "관리자 로그인 세션이 만료되었습니다. 다시 로그인해 주세요.";

            var result = await SendTokenRequestAsync("api/v1/auth/refresh",
                new 토큰갱신요청 { UserId = context.UserId, RefreshToken = context.RefreshToken },
                "관리자 로그인 세션을 갱신하지 못했습니다.", context, isRefresh: true, cancellationToken);
            if (!result.Succeeded && result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                await session.TryLogoutAsync(context);
            return result.ErrorMessage;
        }
        finally { refreshGate.Release(); }
    }

    private async Task<AdminTokenRequestResult> SendTokenRequestAsync<TRequest>(string path, TRequest request,
        string failureMessage, AdminAuthenticationContext context, bool isRefresh, CancellationToken cancellationToken)
    {
        try
        {
            // 로그인 의도는 정상 토큰 갱신을 지나도 유지하고, 갱신 응답은 정확한 토큰 판본에만 적용합니다.
            bool IsCurrent() => isRefresh ? session.IsCurrentAuthenticationContext(context) : session.IsSameAuthentication(context);
            if (!IsCurrent()) return new(false, AuthenticationChanged);
            using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken);
            if (!IsCurrent()) return new(false, AuthenticationChanged);
            if (!response.IsSuccessStatusCode)
                return new(false, $"{failureMessage} ({(int)response.StatusCode})", response.StatusCode);

            var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.UserId))
                return new(false, "서버에서 로그인 토큰을 받지 못했습니다.");
            if (token.Roles?.Contains("서버관리자", StringComparer.Ordinal) != true)
                return new(false, "서버관리자 권한이 있는 계정만 사용할 수 있습니다.", HttpStatusCode.Unauthorized);
            var applied = isRefresh
                ? await session.TryRefreshAsync(token, context)
                : await session.TryApplyAuthenticationAsync(token, context);
            return applied ? AdminTokenRequestResult.Success : new(false, AuthenticationChanged);
        }
        catch (HttpRequestException) { return new(false, "살뜰 서비스에 연결할 수 없습니다."); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, "관리자 인증 응답 시간이 초과되었습니다."); }
    }

    private sealed record AdminTokenRequestResult(bool Succeeded, string? ErrorMessage = null, HttpStatusCode? StatusCode = null)
    { public static AdminTokenRequestResult Success { get; } = new(true); }
}

public sealed record AdminLoginResult(bool Succeeded, string? ErrorMessage)
{
    public static AdminLoginResult Success() => new(true, null);
    public static AdminLoginResult Fail(string message) => new(false, message);
}
