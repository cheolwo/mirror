using HongdalApp.Services.Application;

namespace HongdalApp.Services.Warehouse.Reconsignment.Events;

public sealed class ReconsignmentOrderCreatedEventHandler : IAppEventHandler<ReconsignmentOrderCreatedEvent>
{
    private readonly InMemoryShipperStore _store;

    public ReconsignmentOrderCreatedEventHandler(InMemoryShipperStore store)
    {
        _store = store;
    }

    public Task HandleAsync(ReconsignmentOrderCreatedEvent appEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.AddAppEventLog(
            nameof(ReconsignmentOrderCreatedEvent),
            $"재위탁 운송의뢰 {appEvent.TransportRequestId} 생성, 입고상품 {appEvent.InventoryItemId}, 수량 {appEvent.RequestedQuantity}",
            appEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
