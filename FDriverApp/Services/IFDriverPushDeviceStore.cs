namespace FDriverApp.Services;

// 설치·계정 경계만 저장하고 주문·고객·주소 정보는 보관하지 않습니다.
public sealed record FDriverPushDeviceState(
    string DeviceInstallationId,
    string? PushToken = null,
    string? RegisteredOwnerId = null,
    string? RegisteredTokenHash = null,
    DateTime? RegisteredAtUtc = null,
    bool RevocationPending = false);

public interface IFDriverPushDeviceStore
{
    bool IsConfigured { get; }
    bool AreNotificationsEnabled => true;
    Task<FDriverPushDeviceState> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(FDriverPushDeviceState state, CancellationToken cancellationToken = default);
}

public enum FDriverPushRegistrationState
{
    Registered, ConfigurationRequired, TokenUnavailable, AuthenticationRequired, Failed
}

public sealed record FDriverPushRegistrationResult(FDriverPushRegistrationState State, string? Message = null);
