using HongdalApp.Services.Application;
using HongdalApp.Services.Customs;
using HongdalApp.Services.Samples.Events;

namespace HongdalApp.Services.Samples.Commands;

public sealed class AddShipperRequestCommandHandler : IAppCommandHandler<AddShipperRequestCommand, bool>
{
    private readonly InMemoryShipperStore _store;
    private readonly ICustomsHsReviewService _customsHsReviewService;
    private readonly IAppEventPublisher _eventPublisher;

    public AddShipperRequestCommandHandler(
        InMemoryShipperStore store,
        ICustomsHsReviewService customsHsReviewService,
        IAppEventPublisher eventPublisher)
    {
        _store = store;
        _customsHsReviewService = customsHsReviewService;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> HandleAsync(AddShipperRequestCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _store.AddRequest(command.Request);
        await _eventPublisher.PublishAsync(
            new ShipperRequestAddedEvent(
                command.Request.의뢰Id,
                command.Request.화물종류,
                command.Request.픽업지 ?? string.Empty,
                command.Request.하차지 ?? string.Empty,
                DateTime.UtcNow),
            cancellationToken);

        await _customsHsReviewService.RequestReviewForTransportAsync(command.Request, command.ShipperUserId, cancellationToken);
        return true;
    }
}
