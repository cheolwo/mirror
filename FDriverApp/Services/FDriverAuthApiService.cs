using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;

namespace FDriverApp.Services;

public sealed class FDriverAuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly IFDriverAuthSession _session;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private long _sessionEpoch;

    public FDriverAuthApiService(HttpClient httpClient, IFDriverAuthSession session)
    {
        _httpClient = httpClient;
        _session = session;
        _session.SessionChanged += (_, _) => Interlocked.Increment(ref _sessionEpoch);
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

        var result = await SendTokenRequestAsync(
            "api/v1/auth/login",
            new 로그인요청
            {
                UserNameOrEmail = userNameOrEmail.Trim(),
                Password = password
            },
            "로그인에 실패했습니다. 기사 계정과 서버 상태를 확인해 주세요.",
            cancellationToken);
        return result.ErrorMessage;
    }

    public async Task<string?> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
        => (await EnsureAccessTokenResultAsync(forceRefresh, cancellationToken)).ErrorMessage;

    public async Task<FDriverTokenRequestResult> EnsureAccessTokenResultAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var state = await _session.RestoreAsync(cancellationToken);
            if (!forceRefresh && state == ClientAuthSessionRestoreState.Authenticated)
            {
                return FDriverTokenRequestResult.Success;
            }

            if (string.IsNullOrWhiteSpace(_session.UserId)
                || string.IsNullOrWhiteSpace(_session.RefreshToken)
                || _session.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                return new FDriverTokenRequestResult(false,
                    "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.", HttpStatusCode.Unauthorized);
            }

            var rejectedRevision = _session.SessionRevision;
            var rejectedEpoch = Interlocked.Read(ref _sessionEpoch);
            var rejectedOwner = _session.UserId;
            var result = await SendTokenRequestAsync(
                "api/v1/auth/refresh",
                new 토큰갱신요청
                {
                    UserId = _session.UserId,
                    RefreshToken = _session.RefreshToken
                },
                "로그인 세션을 갱신하지 못했습니다. 다시 시도해 주세요.",
                cancellationToken);
            if (!result.IsSuccess
                && result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                try
                {
                    if (rejectedEpoch != Interlocked.Read(ref _sessionEpoch) || rejectedOwner != _session.UserId
                        || !await _session.TryClearAsync(rejectedRevision, cancellationToken))
                        throw new OperationCanceledException("계정이 변경되어 이전 인증 거절을 적용하지 않습니다.", cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException
                    && _session.CurrentState == ClientAuthSessionRestoreState.Anonymous)
                {
                    // SecureStorage removal must not hide a server rejection after memory was cleared.
                }
                return result with { StatusCode = HttpStatusCode.Unauthorized };
            }

            return result;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<FDriverTokenRequestResult> SendTokenRequestAsync<TRequest>(
        string path,
        TRequest request,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var epoch = Interlocked.Read(ref _sessionEpoch);
        var revision = _session.SessionRevision;
        var owner = _session.UserId;
        var accessToken = _session.AccessToken;
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
            EnsureCurrentRequest();
            if (!response.IsSuccessStatusCode)
            {
                return new FDriverTokenRequestResult(false, failureMessage, response.StatusCode);
            }

            var token = await response.Content.ReadFromJsonAsync<토큰응답>(cancellationToken);
            EnsureCurrentRequest();
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return new FDriverTokenRequestResult(false, "서버 인증 응답을 읽을 수 없습니다.");
            }

            if (!token.Roles.Contains("Driver", StringComparer.OrdinalIgnoreCase)
                && !token.Roles.Contains("기사", StringComparer.OrdinalIgnoreCase))
            {
                return new FDriverTokenRequestResult(
                    false,
                    "기사 권한이 있는 계정으로 로그인해 주세요.",
                    HttpStatusCode.Unauthorized);
            }

            if (!await _session.TryApplyAsync(token.ToClientAuthTokenSnapshot(), revision, cancellationToken))
                throw new OperationCanceledException("계정이 변경되어 이전 인증 응답을 적용하지 않습니다.", cancellationToken);
            return FDriverTokenRequestResult.Success;
        }
        catch (HttpRequestException)
        {
            EnsureCurrentRequest();
            return new FDriverTokenRequestResult(false, "살뜰 서비스에 연결할 수 없습니다.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            EnsureCurrentRequest();
            return new FDriverTokenRequestResult(false, "로그인 응답 시간이 초과되었습니다.");
        }

        void EnsureCurrentRequest()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (epoch != Interlocked.Read(ref _sessionEpoch) || revision != _session.SessionRevision
                || owner != _session.UserId || accessToken != _session.AccessToken)
                throw new OperationCanceledException("계정이 변경되어 이전 인증 응답을 적용하지 않습니다.", cancellationToken);
        }
    }

}

public sealed record FDriverTokenRequestResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    HttpStatusCode? StatusCode = null)
{
    public static FDriverTokenRequestResult Success { get; } = new(true);
}
