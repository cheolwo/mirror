namespace HongdalApp.Services.Commerce.Orders;

using HongdalApp.Services.Warehouse.Fulfillment;

public interface ICommerceOrderFulfillmentService
{
    Task<CommerceOrderFulfillmentResult> ProcessOrderAsync(ExternalCommerceOrder order, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehouseOutboundNotification>> GetNotificationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketInventorySnapshot>> GetMarketInventoryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InboundRestockNotification>> GetRestockNotificationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SellerRestockNotificationPreference>> GetSellerRestockNotificationPreferencesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestockKakaoTalkOutboxMessage>> GetRestockKakaoTalkOutboxMessagesAsync(CancellationToken cancellationToken = default);

    Task UpdateSellerRestockNotificationPreferenceAsync(
        string sellerUserId,
        bool? adminAllowsKakaoTalk = null,
        bool? sellerWantsKakaoTalk = null,
        bool? useInternalNotification = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehouseOrderPickingTask>> GetPickingTasksAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehousePackingTask>> GetPackingTasksAsync(CancellationToken cancellationToken = default);

    Task<WarehousePickingScanResult> ScanPickingBarcodeAsync(long taskId, string barcode, CancellationToken cancellationToken = default);

    Task<WarehousePickingScanResult> HoldPickingTaskAsync(long taskId, string reason, CancellationToken cancellationToken = default);

    Task<WarehousePickingScanResult> CancelPickingTaskAsync(long taskId, string reason, CancellationToken cancellationToken = default);

    Task<WarehousePackingActionResult> StartPackingTaskAsync(long packingTaskId, CancellationToken cancellationToken = default);

    Task<WarehousePackingActionResult> CompletePackingTaskAsync(long packingTaskId, CancellationToken cancellationToken = default);
}
