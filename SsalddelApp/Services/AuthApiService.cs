using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    private readonly SemaphoreSlim _restoreGate = new(1, 1);

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

        using var change = _authSession.BeginAuthenticationChange(cancellationToken);
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail.Trim(),
                Password = password
            }, change.Token);
            change.Token.ThrowIfCancellationRequested();
            if (!response.IsSuccessStatusCode)
                return (false, "서버 로그인에 실패했습니다. 아이디, 비밀번호, 서버 실행 상태를 확인해 주세요.");

            var token = await response.Content.ReadFromJsonAsync<토큰응답>(change.Token);
            change.Token.ThrowIfCancellationRequested();
            if (token is null || token.Roles is null) return (false, "서버 로그인 응답을 읽을 수 없습니다.");

            var result = await _authSession.TryApplyAuthenticationAsync(token.ToClientAuthTokenSnapshot(), change,
                async ct =>
                {
                    if (_authSession.IsLoggedIn)
                        await _mobilePushInstallationClient.DeactivateCurrentOwnerAsync(ct);
                    ct.ThrowIfCancellationRequested();
                    _꾸미기보유권동기화Service.ClearVisibleEntitlements();
                },
                async ct =>
                {
                    try
                    {
                        await _mobilePushInstallationClient.EnsureRegisteredAsync(ct);
                        ct.ThrowIfCancellationRequested();
                        await _꾸미기보유권동기화Service.RestoreAndSynchronizeAsync(ct);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    { /* 선택적 동기화의 내부 timeout은 이미 저장한 인증을 실패로 바꾸지 않습니다. */ }
                    catch (HttpRequestException) { }
                    catch (JsonException) { }
                    // 선택적 실패와 사용자 취소/새 인증 의도는 구분합니다.
                    ct.ThrowIfCancellationRequested();
                });
            return result switch
            {
                AuthSessionCommitResult.Applied => (true, string.Empty),
                AuthSessionCommitResult.InvalidToken => (false, "서버 인증 응답을 읽을 수 없습니다."),
                _ => (false, "로그인 세션이 변경되었습니다. 다시 확인해 주세요.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && change.Token.IsCancellationRequested)
        {
            return (false, "로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "로그인 요청 시간이 초과되었습니다. 다시 시도해 주세요.");
        }
        catch (HttpRequestException)
        {
            return (false, "서버에 연결하지 못했습니다. 연결 상태를 확인해 주세요.");
        }
        catch (JsonException)
        {
            return (false, "서버 인증 응답을 읽을 수 없습니다.");
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        // 호출자가 취소해도 기존 로그아웃의 로컬 종료는 유지합니다.
        using var change = _authSession.BeginAuthenticationChange();
        try
        {
            await _authSession.TryEndAuthenticationAsync(change, async ct =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellationToken);
                _꾸미기보유권동기화Service.ClearVisibleEntitlements();
                await _mobilePushInstallationClient.DeactivateCurrentOwnerAsync(linked.Token);
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && change.Token.IsCancellationRequested)
        { /* 새 로그인 의도는 이전 로그아웃이 지우지 않습니다. */ }
    }

    public async Task<string?> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
        => (await PrepareAuthenticationAsync(forceRefresh, cancellationToken)).ErrorMessage;

    internal async Task<(long Revision, string? UserId, string AccessToken)> PrepareRequestAuthenticationAsync(
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAuthenticationAsync(false, cancellationToken);
        if (prepared.ErrorMessage is not null)
            throw new InvalidOperationException(prepared.ErrorMessage);
        if (string.IsNullOrWhiteSpace(prepared.AccessToken))
            throw new InvalidOperationException("서버 인증 정보가 없어 화주 API를 호출할 수 없습니다.");
        return (prepared.SessionRevision, prepared.UserId, prepared.AccessToken);
    }

    private async Task<AuthenticationPreparation> PrepareAuthenticationAsync(bool forceRefresh,
        CancellationToken cancellationToken)
    {
        long sessionRevision;
        string? userId;
        // 첫 복원을 직렬화해 한 호출의 취소가 다른 대기 호출의 복원을 취소하지 않게 합니다.
        await _restoreGate.WaitAsync(cancellationToken);
        try
        {
            await _authSession.RestoreAsync(cancellationToken);
            sessionRevision = _authSession.SessionRevision;
            userId = _authSession.UserId;
        }
        finally { _restoreGate.Release(); }
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            // 대기 전에 시작한 요청이 새 로그인의 복원·갱신을 맡지 못하게 합니다.
            if (!IsCurrentSession(sessionRevision, userId))
                return new("로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
            if (!forceRefresh
                && !string.IsNullOrWhiteSpace(_authSession.AccessToken)
                && _authSession.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddSeconds(30))
            {
                return CapturePreparedAuthentication(sessionRevision, userId);
            }

            var refreshToken = _authSession.RefreshToken;
            if (!IsCurrentSession(sessionRevision, userId))
                return new("로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
            if (string.IsNullOrWhiteSpace(userId)
                || string.IsNullOrWhiteSpace(refreshToken)
                || _authSession.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                await _authSession.TryClearAsync(sessionRevision, refreshToken, cancellationToken);
                return new("로그인 세션이 만료되었습니다. 다시 로그인해 주세요.");
            }

            HttpStatusCode responseStatus;
            토큰응답? token = null;
            // 전송/본문 오류만 안전 안내로 바꿉니다. 이후 주 보안 저장의 실패는 숨기지 않습니다.
            try
            {
                if (!IsCurrentRefreshSession(sessionRevision, userId, refreshToken))
                    return new("로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
                using var response = await _httpClient.PostAsJsonAsync(
                    "api/v1/auth/refresh",
                    new 토큰갱신요청 { UserId = userId, RefreshToken = refreshToken },
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentRefreshSession(sessionRevision, userId, refreshToken))
                    return new("로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
                responseStatus = response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsCurrentRefreshSession(sessionRevision, userId, refreshToken))
                        return new("로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new("로그인 세션 갱신 요청 시간이 초과되었습니다. 다시 확인해 주세요.");
            }
            catch (HttpRequestException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new("서버에 연결하지 못했습니다. 연결 상태를 확인해 주세요.");
            }
            catch (JsonException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new("서버 인증 응답을 읽을 수 없습니다.");
            }
            if ((int)responseStatus is < 200 or >= 300)
            {
                if (responseStatus is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                {
                    await _authSession.TryClearAsync(sessionRevision, refreshToken, cancellationToken);
                }

                return new("로그인 세션을 갱신하지 못했습니다. 다시 로그인해 주세요.");
            }

            if (token is null || token.Roles is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return new("서버 인증 응답을 읽을 수 없습니다.");
            }

            if (!await _authSession.TryRefreshAsync(token.ToClientAuthTokenSnapshot(), sessionRevision, refreshToken, cancellationToken))
            {
                return new(IsCurrentRefreshSession(sessionRevision, userId, refreshToken)
                    ? "서버 인증 응답을 읽을 수 없습니다."
                    : "로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
            }
            if (!await _authSession.RunIfCurrentAsync(sessionRevision, token.RefreshToken,
                async ct => { await _mobilePushInstallationClient.EnsureRegisteredAsync(ct); }, cancellationToken))
                return new("로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
            return CapturePreparedAuthentication(sessionRevision, userId);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private sealed record AuthenticationPreparation(string? ErrorMessage, long SessionRevision = 0,
        string? UserId = null, string? AccessToken = null);

    private AuthenticationPreparation CapturePreparedAuthentication(long revision, string? userId)
    {
        var accessToken = _authSession.AccessToken;
        return IsCurrentSession(revision, userId)
            ? new(null, revision, userId, accessToken)
            : new("로그인 세션이 변경되었습니다. 다시 확인해 주세요.");
    }

    private bool IsCurrentSession(long revision, string? userId)
        => _authSession.SessionRevision == revision
           && string.Equals(_authSession.UserId, userId, StringComparison.Ordinal);

    private bool IsCurrentRefreshSession(long revision, string? userId, string? refreshToken)
        => IsCurrentSession(revision, userId)
           && string.Equals(_authSession.RefreshToken, refreshToken, StringComparison.Ordinal);
}
