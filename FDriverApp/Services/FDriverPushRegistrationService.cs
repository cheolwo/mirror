using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Notifications;

namespace FDriverApp.Services;

public sealed class FDriverPushRegistrationService
{
    private const string Path = "api/v1/mobile/push/installations";
    private readonly HttpClient _http;
    private readonly IFDriverAuthSession _session;
    private readonly FDriverAuthApiService _auth;
    private readonly IFDriverPushDeviceStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _sessionRevision;
    public bool AreNotificationsEnabled => _store.AreNotificationsEnabled;

    public FDriverPushRegistrationService(HttpClient http, IFDriverAuthSession session,
        FDriverAuthApiService auth, IFDriverPushDeviceStore store)
    {
        _http = http; _session = session; _auth = auth; _store = store;
        _session.SessionChanged += (_, _) => Interlocked.Increment(ref _sessionRevision);
    }

    public async Task<FDriverPushRegistrationResult> UpdateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2000)
            return new(FDriverPushRegistrationState.TokenUnavailable);
        return await WithGateAsync(async () =>
        {
            var device = await _store.LoadAsync(cancellationToken);
            await _store.SaveAsync(device with { PushToken = token.Trim() }, cancellationToken);
            return await RegisterCoreAsync(cancellationToken);
        }, cancellationToken);
    }

    // SDK callback work only persists its token. Registration remains foreground
    // work; Firebase retains the current token for the next SDK GetToken retry.
    public async Task CacheTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2000) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await _store.LoadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await _store.SaveAsync(state with { PushToken = token.Trim() }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { _gate.Release(); }
    }

    public Task<FDriverPushRegistrationResult> EnsureRegisteredAsync(CancellationToken cancellationToken = default)
        => WithGateAsync(() => RegisterCoreAsync(cancellationToken), cancellationToken);

    public Task<FDriverPushRegistrationResult> DeactivateCurrentOwnerAsync(CancellationToken cancellationToken = default)
        => WithGateAsync(async () =>
        {
            var owner = _session.UserId;
            if (string.IsNullOrWhiteSpace(owner)) return new(FDriverPushRegistrationState.AuthenticationRequired);
            var state = await _store.LoadAsync(cancellationToken);
            if (state.RegisteredOwnerId != owner)
                return new(FDriverPushRegistrationState.Registered);
            // 해제 결과를 받기 전에 앱이 종료되어도 원래 소유자의 요청을 복구합니다.
            state = state with { RevocationPending = true };
            await _store.SaveAsync(state, cancellationToken);
            var credentials = await CredentialsAsync(owner, cancellationToken);
            if (credentials is null) return Failed("알림 해제를 확인하지 못했습니다. 같은 기사 계정으로 로그인하면 다시 확인합니다.");
            var result = await DeleteAsync(state, credentials.Value.Token, cancellationToken);
            EnsureCurrentOwner(owner, credentials.Value.Revision, cancellationToken);
            if (!result) return Failed("알림 해제를 확인하지 못했습니다. 같은 기사 계정으로 로그인하면 다시 확인합니다.");
            await _store.SaveAsync(state with { RegisteredOwnerId = null, RegisteredTokenHash = null,
                RegisteredAtUtc = null, RevocationPending = false }, cancellationToken);
            return new(FDriverPushRegistrationState.Registered);
        }, cancellationToken);

    private async Task<FDriverPushRegistrationResult> RegisterCoreAsync(CancellationToken cancellationToken)
    {
        if (!_store.IsConfigured) return new(FDriverPushRegistrationState.ConfigurationRequired,
            "푸시 설정 전 · 앱 복귀와 새로고침으로 요청 확인");
        var owner = _session.UserId;
        if (string.IsNullOrWhiteSpace(owner) || !HasDriverRole()) return new(FDriverPushRegistrationState.AuthenticationRequired);
        var credentials = await CredentialsAsync(owner, cancellationToken);
        if (credentials is null) return new(FDriverPushRegistrationState.AuthenticationRequired);
        var state = await _store.LoadAsync(cancellationToken);
        EnsureCurrentOwner(owner, credentials.Value.Revision, cancellationToken);
        if (state.RevocationPending && state.RegisteredOwnerId == owner)
        {
            if (!await DeleteAsync(state, credentials.Value.Token, cancellationToken))
                return Failed("이전 알림 해제를 확인하지 못했습니다. 새로고침하면 다시 확인합니다.");
            EnsureCurrentOwner(owner, credentials.Value.Revision, cancellationToken);
            state = state with { RegisteredOwnerId = null, RegisteredTokenHash = null, RegisteredAtUtc = null, RevocationPending = false };
            await _store.SaveAsync(state, cancellationToken);
        }
        // 다른 계정의 설치 해제 요청에는 현재 계정의 자격 증명을 사용하지 않습니다.
        if (string.IsNullOrWhiteSpace(state.PushToken)) return new(FDriverPushRegistrationState.TokenUnavailable,
            "알림 연결 대기 · 앱 복귀와 새로고침으로 요청 확인");
        var tokenHash = Hash(state.PushToken);
        if (!state.RevocationPending && state.RegisteredOwnerId == owner && state.RegisteredTokenHash == tokenHash
            && state.RegisteredAtUtc > DateTime.UtcNow.AddDays(-1))
            return new(FDriverPushRegistrationState.Registered);
        using var request = Authorized(HttpMethod.Put, Path, credentials.Value.Token);
        request.Content = JsonContent.Create(new SsalddelMobilePushInstallationUpsertRequest(
            InstallationForOwner(state.DeviceInstallationId, owner), 기사앱식별자.FoodDeliveryDriverApp,
            "android", state.PushToken));
        using var response = await _http.SendAsync(request, cancellationToken);
        EnsureCurrentOwner(owner, credentials.Value.Revision, cancellationToken);
        if (!response.IsSuccessStatusCode) return Failed("알림 연결을 확인하지 못했습니다. 앱 복귀와 새로고침으로 요청을 확인하세요.");
        await _store.SaveAsync(state with { RegisteredOwnerId = owner, RegisteredTokenHash = tokenHash,
            RegisteredAtUtc = DateTime.UtcNow, RevocationPending = false }, cancellationToken);
        EnsureCurrentOwner(owner, credentials.Value.Revision, cancellationToken);
        return new(FDriverPushRegistrationState.Registered);
    }

    private async Task<(string Token, long Revision)?> CredentialsAsync(string owner, CancellationToken cancellationToken)
    {
        var refresh = await _auth.EnsureAccessTokenResultAsync(cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // A legitimate same-owner refresh changes SessionChanged. Owner still must match.
        if (_session.UserId != owner) throw new OperationCanceledException("알림 계정이 변경되었습니다.", cancellationToken);
        if (!refresh.IsSuccess || !_session.IsAuthenticated || !HasDriverRole() || string.IsNullOrWhiteSpace(_session.AccessToken)) return null;
        var revision = Interlocked.Read(ref _sessionRevision);
        return (_session.AccessToken, revision);
    }

    private void EnsureCurrentOwner(string owner, long revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (owner != _session.UserId || revision != Interlocked.Read(ref _sessionRevision) || !_session.IsAuthenticated || !HasDriverRole())
            throw new OperationCanceledException("알림 계정이 변경되었습니다.", cancellationToken);
    }

    private bool HasDriverRole() => _session.Roles.Any(role => role == "기사" || string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase));
    private async Task<bool> DeleteAsync(FDriverPushDeviceState state, string token, CancellationToken cancellationToken)
    {
        using var request = Authorized(HttpMethod.Delete,
            $"{Path}/{Uri.EscapeDataString(InstallationForOwner(state.DeviceInstallationId, state.RegisteredOwnerId!))}", token);
        using var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
    }

    private async Task<FDriverPushRegistrationResult> WithGateAsync(Func<Task<FDriverPushRegistrationResult>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await action(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Failed("알림 연결 중 계정이 변경되었습니다. 현재 계정으로 다시 확인하세요."); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return Failed("알림 연결을 확인하지 못했습니다. 앱 복귀와 새로고침으로 요청을 확인하세요."); }
        finally { _gate.Release(); }
    }

    public static string InstallationForOwner(string deviceId, string ownerId) => $"{deviceId}-{Hash(ownerId)[..16]}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static FDriverPushRegistrationResult Failed(string message) => new(FDriverPushRegistrationState.Failed, message);
    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    { var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return request; }
}
