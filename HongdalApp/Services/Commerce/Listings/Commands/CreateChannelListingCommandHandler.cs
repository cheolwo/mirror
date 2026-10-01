using Hongdal.Contracts.Common.Sales;
using HongdalApp.Services.Application;
using HongdalApp.Services.Commerce.Listings.Events;

namespace HongdalApp.Services.Commerce.Listings.Commands;

public sealed class CreateChannelListingCommandHandler : IAppCommandHandler<CreateChannelListingCommand, 채널출품항목응답?>
{
    private readonly InMemoryShipperStore _store;
    private readonly ICommerceChannelListingService _channelListingService;
    private readonly IAppEventPublisher _eventPublisher;

    public CreateChannelListingCommandHandler(
        InMemoryShipperStore store,
        ICommerceChannelListingService channelListingService,
        IAppEventPublisher eventPublisher)
    {
        _store = store;
        _channelListingService = channelListingService;
        _eventPublisher = eventPublisher;
    }

    public async Task<채널출품항목응답?> HandleAsync(CreateChannelListingCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var created = _store.CreateListing(command.Payload);
        var account = _store.FindAccount(command.Payload.판매채널계정Id);
        var product = _store.FindProduct(command.Payload.판매상품Id);

        if (account is not null && product is not null)
        {
            var preparation = await _channelListingService.PrepareListingAsync(account, product, cancellationToken);
            _store.UpdateListingSync(created.Id, preparation.SyncStatus, preparation.Message);
        }

        await _eventPublisher.PublishAsync(
            new ChannelListingCreatedEvent(created.Id, created.판매상품Id, created.판매채널계정Id, created.동기화상태, DateTime.UtcNow),
            cancellationToken);

        return created;
    }
}
