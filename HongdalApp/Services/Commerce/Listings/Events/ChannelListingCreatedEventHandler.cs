using HongdalApp.Services.Application;

namespace HongdalApp.Services.Commerce.Listings.Events;

public sealed class ChannelListingCreatedEventHandler : IAppEventHandler<ChannelListingCreatedEvent>
{
    private readonly InMemoryShipperStore _store;

    public ChannelListingCreatedEventHandler(InMemoryShipperStore store)
    {
        _store = store;
    }

    public Task HandleAsync(ChannelListingCreatedEvent appEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.AddAppEventLog(
            nameof(ChannelListingCreatedEvent),
            $"채널출품 {appEvent.ListingId} 생성, 상품 {appEvent.ProductId}, 계정 {appEvent.AccountId}, 동기화 {appEvent.SyncStatus}",
            appEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
