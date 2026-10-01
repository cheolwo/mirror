using HongdalApp.Services.Application;
using HongdalApp.Services.Commerce.Orders.Commands;
using HongdalApp.Services.Warehouse.Fulfillment;

namespace HongdalApp.Services.Commerce.Orders;

public sealed class CommerceOrderFulfillmentService : ICommerceOrderFulfillmentService
{
    private readonly InMemoryShipperStore _store;
    private readonly IAppCommandHandler<ProcessCommerceOrderCommand, CommerceOrderFulfillmentResult> _processOrderHandler;

    public CommerceOrderFulfillmentService(
        InMemoryShipperStore store,
        IAppCommandHandler<ProcessCommerceOrderCommand, CommerceOrderFulfillmentResult> processOrderHandler)
    {
        _store = store;
        _processOrderHandler = processOrderHandler;
    }

    public Task<CommerceOrderFulfillmentResult> ProcessOrderAsync(ExternalCommerceOrder order, CancellationToken cancellationToken = default)
        => _processOrderHandler.HandleAsync(new ProcessCommerceOrderCommand(order), cancellationToken);

    public Task<IReadOnlyList<WarehouseOutboundNotification>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetWarehouseOutboundNotifications());
    }

    public Task<IReadOnlyList<MarketInventorySnapshot>> GetMarketInventoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetMarketInventorySnapshots());
    }

    public Task<IReadOnlyList<InboundRestockNotification>> GetRestockNotificationsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetInboundRestockNotifications());
    }

    public Task<IReadOnlyList<SellerRestockNotificationPreference>> GetSellerRestockNotificationPreferencesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetSellerRestockNotificationPreferences());
    }

    public Task<IReadOnlyList<RestockKakaoTalkOutboxMessage>> GetRestockKakaoTalkOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetRestockKakaoTalkOutboxMessages());
    }

    public Task UpdateSellerRestockNotificationPreferenceAsync(
        string sellerUserId,
        bool? adminAllowsKakaoTalk = null,
        bool? sellerWantsKakaoTalk = null,
        bool? useInternalNotification = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.UpdateSellerRestockNotificationPreference(
            sellerUserId,
            adminAllowsKakaoTalk,
            sellerWantsKakaoTalk,
            useInternalNotification);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WarehouseOrderPickingTask>> GetPickingTasksAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetOrderPickingTasks());
    }

    public Task<IReadOnlyList<WarehousePackingTask>> GetPackingTasksAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetPackingTasks());
    }

    public Task<WarehousePickingScanResult> ScanPickingBarcodeAsync(long taskId, string barcode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.ScanOrderPickingTask(taskId, barcode));
    }

    public Task<WarehousePickingScanResult> HoldPickingTaskAsync(long taskId, string reason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.HoldOrderPickingTask(taskId, reason));
    }

    public Task<WarehousePickingScanResult> CancelPickingTaskAsync(long taskId, string reason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.CancelOrderPickingTask(taskId, reason));
    }

    public Task<WarehousePackingActionResult> StartPackingTaskAsync(long packingTaskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.StartPackingTask(packingTaskId));
    }

    public Task<WarehousePackingActionResult> CompletePackingTaskAsync(long packingTaskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.CompletePackingTask(packingTaskId));
    }

}
