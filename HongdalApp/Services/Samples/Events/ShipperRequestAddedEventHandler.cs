using HongdalApp.Services.Application;

namespace HongdalApp.Services.Samples.Events;

public sealed class ShipperRequestAddedEventHandler : IAppEventHandler<ShipperRequestAddedEvent>
{
    private readonly InMemoryShipperStore _store;

    public ShipperRequestAddedEventHandler(InMemoryShipperStore store)
    {
        _store = store;
    }

    public Task HandleAsync(ShipperRequestAddedEvent appEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.AddAppEventLog(
            nameof(ShipperRequestAddedEvent),
            $"운송의뢰 {appEvent.TransportRequestId} 등록: {appEvent.CargoName} / {appEvent.PickupLocation} -> {appEvent.DropoffLocation}",
            appEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
