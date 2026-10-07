using System.Net;
using System.Net.Http.Json;
using Ssalddel.Contracts.Common;

namespace Ssalddel.Client.Infrastructure.Security;

public sealed record ClientAuthApiResult(bool IsSuccess, string? ErrorMessage = null, bool SessionRejected = false);

/// <summary>인증 HTTP·직렬 갱신·세션 판본만 관리하며 업무 요청을 재전송하지 않습니다.</summary>
public sealed class ClientAuthApiSessionService(HttpClient httpClient, ClientAuthSession session)
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private long _loginGeneration;

    public async Task<ClientAuthApiResult> LoginAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
            return new(false, "아이디와 비밀번호를 입력해 주세요.");
        var generation = Interlocked.Increment(ref _loginGeneration);
        await session.ClearAsync(cancellationToken);
        if (generation != Interlocked.Read(ref _loginGeneration))
            return new(false, "로그인 상태가 변경되었습니다.", true);
        return await SendAsync("api/v1/auth/login", new 로그인요청 { UserNameOrEmail = name.Trim(), Password = password },
            session.Revision, "로그인에 실패했습니다. 아이디와 비밀번호를 확인해 주세요.", cancellationToken,
            isCurrent: () => generation == Interlocked.Read(ref _loginGeneration));
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var state = await session.RestoreAsync(cancellationToken);
        if (state == ClientAuthSessionRestoreState.RefreshRequired)
        {
            var result = await RefreshAsync(session.UserId ?? "", session.RefreshToken ?? "", cancellationToken);
            if (!result.IsSuccess && !result.SessionRejected)
                throw new HttpRequestException(result.ErrorMessage ?? "로그인 세션을 갱신하지 못했습니다.");
        }
        return session.IsAuthenticated ? session.AccessToken : null;
    }

    public async Task<string?> RefreshAfterUnauthorizedAsync(string? rejectedToken, CancellationToken cancellationToken = default)
        => await RefreshAfterUnauthorizedAsync(rejectedToken, session.UserId, cancellationToken);

    public async Task<string?> RefreshAfterUnauthorizedAsync(string? rejectedToken, string? expectedOwnerId, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(expectedOwnerId, session.UserId, StringComparison.Ordinal)) return null;
        var result = await RefreshCoreAsync(expectedOwnerId ?? "", session.RefreshToken ?? "", rejectedToken, cancellationToken);
        if (!result.IsSuccess && !result.SessionRejected)
            throw new HttpRequestException(result.ErrorMessage ?? "로그인 세션을 갱신하지 못했습니다.");
        return session.IsAuthenticated && string.Equals(expectedOwnerId, session.UserId, StringComparison.Ordinal) ? session.AccessToken : null;
    }

    public Task<ClientAuthApiResult> RefreshAsync(string userId, string refreshToken, CancellationToken cancellationToken = default)
        => RefreshCoreAsync(userId, refreshToken, rejectedToken: null, cancellationToken);

    private async Task<ClientAuthApiResult> RefreshCoreAsync(string owner, string refreshToken, string? rejectedToken,
        CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            await session.RestoreAsync(cancellationToken);
            var revision = session.CaptureIdentity().Revision;
            if (string.IsNullOrWhiteSpace(owner) || !string.Equals(owner, session.UserId, StringComparison.Ordinal))
                return new(false, "로그인 상태가 변경되었습니다.", true);
            if (session.IsAuthenticated && (rejectedToken is null || !string.Equals(rejectedToken, session.AccessToken, StringComparison.Ordinal)))
                return new(true);
            if (string.IsNullOrWhiteSpace(refreshToken) || !string.Equals(refreshToken, session.RefreshToken, StringComparison.Ordinal))
                return new(false, "로그인 상태가 변경되었습니다.", true);
            var result = await SendAsync("api/v1/auth/refresh", new 토큰갱신요청 { UserId = owner, RefreshToken = refreshToken },
                revision, "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.", cancellationToken, expectedOwner: owner);
            if (result.SessionRejected) await session.TryClearAsync(revision, cancellationToken);
            return result;
        }
        finally { _refreshGate.Release(); }
    }

    private async Task<ClientAuthApiResult> SendAsync<T>(string path, T request, long revision, string failureMessage,
        CancellationToken cancellationToken, string? expectedOwner = null, Func<bool>? isCurrent = null)
    {
        using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var rejected = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest;
            return new(false, rejected ? failureMessage : "인증 서버에 일시적으로 연결하지 못했습니다. 다시 시도해 주세요.", rejected);
        }
        var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.UserId))
            return new(false, "서버 인증 응답을 읽을 수 없습니다.");
        if ((isCurrent is not null && !isCurrent())
            || (expectedOwner is not null && !string.Equals(token.UserId, expectedOwner, StringComparison.Ordinal)))
            return new(false, "로그인 상태가 변경되었습니다.", true);
        return await session.TryApplyAsync(token.ToClientAuthTokenSnapshot(), revision, cancellationToken)
            ? new(true)
            : new(false, "로그인 상태가 변경되었습니다.", true);
    }
}
