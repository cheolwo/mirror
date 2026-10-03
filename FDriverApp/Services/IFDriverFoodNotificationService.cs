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
    void Cancel();
    void ClearAccount();
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
