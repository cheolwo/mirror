namespace FDriverApp.Services;

// 계정·제안 식별자만 보관하며 알림 문구에 수령자 정보를 싣지 않는다.
public sealed record FDriverFoodNotificationReceipt(string UserId, IReadOnlyList<string> SeenOfferIds);
public sealed record FDriverFoodNotificationTarget(string UserId, string OfferId);

public interface IFDriverFoodNotificationService
{
    FDriverFoodNotificationReceipt? ReadReceipt();
    void WriteReceipt(FDriverFoodNotificationReceipt receipt);
    FDriverFoodNotificationTarget? ReadOpenTarget();
    void SetOpenTarget(FDriverFoodNotificationTarget target);
    void ClearOpenTarget();
    bool Publish(string userId, string offerId, DateTime expiresAtUtc);
    bool PublishForWorkKind(string userId, string offerId, DateTime expiresAtUtc, string? workKind)
        => workKind == Ssalddel.Contracts.Driver.Settings.DriverWorkKinds.FoodDelivery && Publish(userId, offerId, expiresAtUtc);
    bool PublishChecked(string userId, string offerId, DateTime expiresAtUtc, string? workKind,
        CancellationToken cancellationToken, Func<bool> isCurrent)
        => FDriverNotificationPublicationGate.CanContinuePublication(cancellationToken, isCurrent)
            && PublishForWorkKind(userId, offerId, expiresAtUtc, workKind);
    void Cancel();
    void ClearAccount();
    void ApplySettings(string ownerId, Ssalddel.Contracts.Driver.Notification.기사알림설정응답 settings, DateTime checkedAtUtc) { }
    void BeginSettingsChange(string ownerId, DateTime beganAtUtc) => Cancel();
    void ConfirmSettings(string ownerId, Ssalddel.Contracts.Driver.Notification.기사알림설정응답 settings, DateTime readStartedAtUtc)
        => ApplySettings(ownerId, settings, readStartedAtUtc);
}

public sealed class NullFDriverFoodNotificationService : IFDriverFoodNotificationService
{
    public static NullFDriverFoodNotificationService Instance { get; } = new();
    public FDriverFoodNotificationReceipt? ReadReceipt() => null;
    public void WriteReceipt(FDriverFoodNotificationReceipt receipt) { }
    public FDriverFoodNotificationTarget? ReadOpenTarget() => null;
    public void SetOpenTarget(FDriverFoodNotificationTarget target) { }
    public void ClearOpenTarget() { }
    public bool Publish(string userId, string offerId, DateTime expiresAtUtc) => false;
    public void Cancel() { }
    public void ClearAccount() { }
}
