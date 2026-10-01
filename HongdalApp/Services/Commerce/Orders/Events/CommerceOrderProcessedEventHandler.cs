using HongdalApp.Services.Application;

namespace HongdalApp.Services.Commerce.Orders.Events;

public sealed class CommerceOrderProcessedEventHandler : IAppEventHandler<CommerceOrderProcessedEvent>
{
    private readonly InMemoryShipperStore _store;

    public CommerceOrderProcessedEventHandler(InMemoryShipperStore store)
    {
        _store = store;
    }

    public Task HandleAsync(CommerceOrderProcessedEvent appEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.AddAppEventLog(
            nameof(CommerceOrderProcessedEvent),
            $"{appEvent.OrderScope} 주문 {appEvent.ChannelOrderNo} 처리, 출고 알림 {appEvent.NotificationCount}건",
            appEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
