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
    private readonly SemaphoreSlim _sessionWriteGate = new(1, 1);
    private long _sessionGeneration;
    public event Action<bool>? SessionEnding;
    internal event Func<bool, CancellationToken, Task>? SessionEndingAsync;

    public ClientAuthSession Session => session;
    public RestaurantOrderReturnContext OrderReturn { get; } = new();
    private string? _sessionCleanupNotice;
    public string? ConsumeSessionCleanupNotice()
        => Interlocked.Exchange(ref _sessionCleanupNotice, null);
    internal long SessionGeneration => Volatile.Read(ref _sessionGeneration);

    internal void ThrowIfActorChanged(string? ownerId, long generation)
    {
        if (generation != SessionGeneration || !string.Equals(ownerId, session.UserId, StringComparison.Ordinal))
            throw new OperationCanceledException("계정이 변경되어 이전 인증 요청을 적용하지 않습니다.");
    }

    internal async Task<(string Owner, long Generation, string AccessToken)> CaptureRequestCredentialsAsync(
        string? expectedOwner, long? expectedGeneration, CancellationToken cancellationToken)
    {
        await _sessionWriteGate.WaitAsync(cancellationToken);
        try
        {
            if (expectedGeneration is { } generation) ThrowIfActorChanged(expectedOwner, generation);
            if (!session.IsAuthenticated || string.IsNullOrWhiteSpace(session.UserId) || string.IsNullOrWhiteSpace(session.AccessToken))
                throw new UnauthorizedAccessException("로그인 확인이 필요합니다.");
            return (session.UserId, SessionGeneration, session.AccessToken);
        }
        finally { _sessionWriteGate.Release(); }
    }

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

        var generation = Interlocked.Increment(ref _sessionGeneration);
        return await SendTokenRequestAsync(
            "api/v1/auth/login",
            new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail.Trim(),
                Password = password
            },
            "로그인에 실패했습니다. 음식점 계정과 비밀번호를 확인해 주세요.",
            cancellationToken, generation);
    }

    public Task<RestaurantAuthResult> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
        => EnsureAccessTokenCoreAsync(forceRefresh, cancellationToken);

    internal Task<RestaurantAuthResult> EnsureAccessTokenForRequestAsync(
        string ownerId, long generation, bool forceRefresh, CancellationToken cancellationToken)
        => EnsureAccessTokenCoreAsync(forceRefresh, cancellationToken, ownerId, generation);

    private async Task<RestaurantAuthResult> EnsureAccessTokenCoreAsync(
        bool forceRefresh, CancellationToken cancellationToken, string? requestedOwner = null, long? requestedGeneration = null)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (requestedGeneration is { } expected) ThrowIfActorChanged(requestedOwner, expected);
            var generation = SessionGeneration;
            ClientAuthSessionRestoreState state;
            await _sessionWriteGate.WaitAsync(cancellationToken);
            try
            {
                if (generation != SessionGeneration) throw new OperationCanceledException("새 인증 요청이 시작되었습니다.");
                state = await session.RestoreAsync(cancellationToken);
                if (generation != SessionGeneration)
                    throw new OperationCanceledException("계정이 변경되어 이전 세션 복원을 적용하지 않습니다.");
            }
            finally { _sessionWriteGate.Release(); }
            if (requestedGeneration is { } restoredExpected) ThrowIfActorChanged(requestedOwner, restoredExpected);
            var owner = session.UserId;
            var accessToken = session.AccessToken;
            var refreshToken = session.RefreshToken;
            if (!forceRefresh && state == ClientAuthSessionRestoreState.Authenticated)
            {
                return RestaurantAuthResult.Success;
            }

            if (string.IsNullOrWhiteSpace(session.UserId)
                || string.IsNullOrWhiteSpace(session.RefreshToken)
                || session.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                await InvalidateRejectedSessionForRequestAsync(owner, accessToken, generation, cancellationToken);
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
                cancellationToken, generation, owner, refreshToken);
            if (!result.IsSuccess && result.RequiresLogin)
            {
                await InvalidateRejectedSessionForRequestAsync(owner, accessToken, generation, cancellationToken);
            }

            return result;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref _sessionGeneration);
        await _sessionWriteGate.WaitAsync(cancellationToken);
        try
        {
            if (generation != SessionGeneration) throw new OperationCanceledException("새 인증 요청이 시작되었습니다.");
            OrderReturn.EndSession(explicitLogout: true, session.UserId);
            SessionEnding?.Invoke(true);
            try { await NotifySessionEndingAsync(true, cancellationToken); }
            catch (RestaurantPendingStorageException ex)
            {
                _sessionCleanupNotice = ex.Message;
                throw;
            }
            finally { await session.ClearAsync(cancellationToken); }
        }
        finally { _sessionWriteGate.Release(); }
    }

    internal Task InvalidateRejectedSessionAsync(CancellationToken cancellationToken)
        => InvalidateRejectedSessionForRequestAsync(session.UserId, session.AccessToken, SessionGeneration, cancellationToken);

    private async Task NotifySessionEndingAsync(bool explicitLogout, CancellationToken cancellationToken)
    {
        if (SessionEndingAsync is not { } handlers) return;
        foreach (Func<bool, CancellationToken, Task> handler in handlers.GetInvocationList())
            await handler(explicitLogout, cancellationToken);
    }

    internal async Task InvalidateRejectedSessionForRequestAsync(
        string? ownerId, string? rejectedAccessToken, long generation, CancellationToken cancellationToken)
    {
        await _sessionWriteGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfActorChanged(ownerId, generation);
            if (!string.Equals(rejectedAccessToken, session.AccessToken, StringComparison.Ordinal)
                || Interlocked.CompareExchange(ref _sessionGeneration, generation + 1, generation) != generation)
                throw new OperationCanceledException("새 인증 상태를 이전 거절 응답으로 종료하지 않습니다.");
            var rejectedRevision = session.Revision;
            OrderReturn.EndSession(explicitLogout: false, session.UserId);
            SessionEnding?.Invoke(false);
            // A page may cancel its own requests when the confirmed session end is announced.
            // Complete this cleanup independently, without clearing a newer account's session.
            try { await session.TryClearAsync(rejectedRevision, CancellationToken.None); }
            catch (Exception)
            {
                // Memory is already anonymous; a secure-store failure must not hide rejection.
            }
        }
        finally { _sessionWriteGate.Release(); }
    }

    private async Task<RestaurantAuthResult> SendTokenRequestAsync<TRequest>(
        string path,
        TRequest request,
        string failureMessage,
        CancellationToken cancellationToken, long generation, string? refreshOwner = null, string? expectedRefreshToken = null)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken);
            if (generation != SessionGeneration
                || (expectedRefreshToken is not null && (!string.Equals(refreshOwner, session.UserId, StringComparison.Ordinal)
                    || !string.Equals(expectedRefreshToken, session.RefreshToken, StringComparison.Ordinal))))
                throw new OperationCanceledException("계정이 변경되어 이전 인증 응답을 적용하지 않습니다.");
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

            if (expectedRefreshToken is not null && !string.Equals(token.UserId, refreshOwner, StringComparison.Ordinal))
                return new RestaurantAuthResult(false, "서버 인증 응답을 읽을 수 없습니다.");

            await _sessionWriteGate.WaitAsync(cancellationToken);
            try
            {
                if (generation != SessionGeneration
                    || (expectedRefreshToken is not null && (!string.Equals(refreshOwner, session.UserId, StringComparison.Ordinal)
                        || !string.Equals(expectedRefreshToken, session.RefreshToken, StringComparison.Ordinal))))
                    throw new OperationCanceledException("계정이 변경되어 이전 인증 응답을 적용하지 않습니다.");
                await session.ApplyAsync(token.ToClientAuthTokenSnapshot(), cancellationToken);
            }
            finally { _sessionWriteGate.Release(); }
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
