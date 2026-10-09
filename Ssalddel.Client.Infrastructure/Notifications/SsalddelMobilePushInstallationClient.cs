using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Client.Infrastructure.Notifications;

public enum SsalddelMobilePushRegistrationState
{
    Registered,
    TokenNotAvailable,
    AuthenticationRequired,
    Failed
}

public sealed record SsalddelMobilePushRegistrationResult(
    SsalddelMobilePushRegistrationState State,
    string? ErrorMessage = null);

public sealed class SsalddelMobilePushInstallationClient
{
    private const string InstallationsPath = "api/v1/mobile/push/installations";

    private readonly HttpClient _httpClient;
    private readonly ISsalddelMobilePushTokenProvider _pushTokenProvider;
    private readonly Func<string?> _accessTokenProvider;
    private readonly IMemberDeliveryPushStateStore? _stateStore;
    private readonly Func<string?>? _ownerProvider;
    private readonly Func<long>? _sessionRevisionProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SsalddelMobilePushInstallationClient(
        HttpClient httpClient,
        ISsalddelMobilePushTokenProvider pushTokenProvider,
        Func<string?> accessTokenProvider)
    {
        _httpClient = httpClient;
        _pushTokenProvider = pushTokenProvider;
        _accessTokenProvider = accessTokenProvider;
    }

    public SsalddelMobilePushInstallationClient(HttpClient httpClient,
        ISsalddelMobilePushTokenProvider pushTokenProvider, Func<string?> accessTokenProvider,
        IMemberDeliveryPushStateStore stateStore, Func<string?> ownerProvider, Func<long> sessionRevisionProvider)
        : this(httpClient, pushTokenProvider, accessTokenProvider)
    {
        _stateStore = stateStore;
        _ownerProvider = ownerProvider;
        _sessionRevisionProvider = sessionRevisionProvider;
    }

    public async Task<SsalddelMobilePushRegistrationResult> EnsureRegisteredAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await EnsureRegisteredCoreAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return new(SsalddelMobilePushRegistrationState.Failed, "알림 연결을 확인하지 못했습니다. 앱에서 다시 확인해 주세요."); }
        finally { _gate.Release(); }
    }

    private async Task<SsalddelMobilePushRegistrationResult> EnsureRegisteredCoreAsync(CancellationToken cancellationToken)
    {
        var owner = _ownerProvider?.Invoke();
        var revision = _sessionRevisionProvider?.Invoke();
        var snapshot = await _pushTokenProvider.GetCurrentAsync(cancellationToken);
        EnsureOwner(owner, revision, cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.PushToken))
        {
            // OS 알림 권한 철회도 다음 앱 복귀 때 서버 설치 해제를 재시도합니다.
            if (_stateStore is not null && owner is not null)
                await DeactivateCoreAsync(owner, revision, cancellationToken, clearAccountPreferences: false);
            return new SsalddelMobilePushRegistrationResult(
                SsalddelMobilePushRegistrationState.TokenNotAvailable);
        }

        var accessToken = _accessTokenProvider();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new SsalddelMobilePushRegistrationResult(
                SsalddelMobilePushRegistrationState.AuthenticationRequired);
        }

        MemberDeliveryPushState? state = null;
        if (_stateStore is not null)
        {
            if (string.IsNullOrWhiteSpace(owner)) return new(SsalddelMobilePushRegistrationState.AuthenticationRequired);
            state = await _stateStore.LoadAsync(cancellationToken);
            EnsureOwner(owner, revision, cancellationToken);
            if (state.RevocationPending && state.RegisteredOwnerId == owner)
            {
                var revoked = await DeactivateCoreAsync(owner, revision, cancellationToken, clearAccountPreferences: false);
                if (revoked.State != SsalddelMobilePushRegistrationState.Registered) return revoked;
                state = await _stateStore.LoadAsync(cancellationToken);
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, InstallationsPath)
        {
            Content = JsonContent.Create(new SsalddelMobilePushInstallationUpsertRequest(
                snapshot.InstallationId,
                snapshot.AppKey,
                snapshot.Platform,
                snapshot.PushToken,
                snapshot.AppVersion,
                snapshot.DeviceModel))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            EnsureOwner(owner, revision, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var recorded = _stateStore is null;
                if (_stateStore is not null)
                    await _stateStore.UpdateAsync(current =>
                    {
                        EnsureOwner(owner, revision, cancellationToken);
                        // GetToken/OnNewToken가 이미 회전시켰으면 이전 등록 응답을 저장하지 않습니다.
                        if (current.PushToken != snapshot.PushToken) return current;
                        recorded = true;
                        return current with { RegisteredOwnerId = owner,
                            RegisteredTokenHash = Hash(snapshot.PushToken), RegisteredAtUtc = DateTimeOffset.UtcNow,
                            RevocationPending = false };
                    }, cancellationToken);
                if (!recorded) return new(SsalddelMobilePushRegistrationState.Failed,
                    "알림 토큰이 변경되었습니다. 새 연결을 다시 확인합니다.");
                return new SsalddelMobilePushRegistrationResult(
                    SsalddelMobilePushRegistrationState.Registered);
            }

            return new SsalddelMobilePushRegistrationResult(
                SsalddelMobilePushRegistrationState.Failed,
                $"모바일 Push 설치 등록 실패: HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException)
        {
            return new SsalddelMobilePushRegistrationResult(
                SsalddelMobilePushRegistrationState.Failed,
                "알림 연결을 확인하지 못했습니다. 앱에서 다시 확인해 주세요.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SsalddelMobilePushRegistrationResult(
                SsalddelMobilePushRegistrationState.Failed,
                "모바일 Push 설치 등록 시간이 초과되었습니다.");
        }
    }

    public async Task<SsalddelMobilePushRegistrationResult> DeactivateCurrentOwnerAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await DeactivateCoreAsync(_ownerProvider?.Invoke(), _sessionRevisionProvider?.Invoke(), cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return new(SsalddelMobilePushRegistrationState.Failed, "알림 해제를 확인하지 못했습니다. 같은 계정으로 다시 연결하면 재시도합니다."); }
        finally { _gate.Release(); }
    }

    private async Task<SsalddelMobilePushRegistrationResult> DeactivateCoreAsync(string? owner, long? revision,
        CancellationToken cancellationToken, bool clearAccountPreferences = true)
    {
        if (_stateStore is null) return new(SsalddelMobilePushRegistrationState.Registered);
        var state = await _stateStore.LoadAsync(cancellationToken);
        EnsureOwner(owner, revision, cancellationToken);
        if (owner is null || state.RegisteredOwnerId != owner) return new(SsalddelMobilePushRegistrationState.AuthenticationRequired);
        await _stateStore.UpdateAsync(current =>
        {
            EnsureOwner(owner, revision, cancellationToken);
            // OS 권한만 철회한 경우 소리·진동 선택은 보존합니다. 명시적 로그아웃은 계정 설정을 지웁니다.
            return current with { RevocationPending = true, OpenTarget = null, Published = [],
                Preferences = clearAccountPreferences ? new(Enabled: false) : current.Preferences,
                PreferencesOwnerId = clearAccountPreferences ? null : current.PreferencesOwnerId,
                PreferencesCheckedAtUtc = clearAccountPreferences ? null : current.PreferencesCheckedAtUtc };
        }, cancellationToken);
        var accessToken = _accessTokenProvider();
        if (string.IsNullOrWhiteSpace(accessToken)) return new(SsalddelMobilePushRegistrationState.AuthenticationRequired);
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{InstallationsPath}/{Uri.EscapeDataString(state.DeviceInstallationId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        EnsureOwner(owner, revision, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            return new(SsalddelMobilePushRegistrationState.Failed, "알림 해제를 확인하지 못했습니다. 같은 계정으로 다시 연결하면 재시도합니다.");
        await _stateStore.UpdateAsync(current =>
        {
            EnsureOwner(owner, revision, cancellationToken);
            return current with { RegisteredOwnerId = null, RegisteredAtUtc = null, RegisteredTokenHash = null,
                RevocationPending = false };
        }, cancellationToken);
        return new(SsalddelMobilePushRegistrationState.Registered);
    }

    private void EnsureOwner(string? owner, long? revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_ownerProvider is not null && (owner != _ownerProvider() || revision != _sessionRevisionProvider?.Invoke()))
            throw new OperationCanceledException("알림 계정이 변경되었습니다.");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
