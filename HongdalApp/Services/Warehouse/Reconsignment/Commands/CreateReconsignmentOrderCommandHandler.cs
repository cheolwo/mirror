using Hongdal.Contracts.Shipper.Request;
using HongdalApp.Services.Application;
using HongdalApp.Services.Warehouse.Reconsignment.Events;

namespace HongdalApp.Services.Warehouse.Reconsignment.Commands;

public sealed class CreateReconsignmentOrderCommandHandler : IAppCommandHandler<CreateReconsignmentOrderCommand, 화주운송의뢰응답?>
{
    private readonly InMemoryShipperStore _store;
    private readonly IAppEventPublisher _eventPublisher;

    public CreateReconsignmentOrderCommandHandler(InMemoryShipperStore store, IAppEventPublisher eventPublisher)
    {
        _store = store;
        _eventPublisher = eventPublisher;
    }

    public async Task<화주운송의뢰응답?> HandleAsync(CreateReconsignmentOrderCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var created = _store.CreateReconsignment(command.Payload, command.UserId);
        await _eventPublisher.PublishAsync(
            new ReconsignmentOrderCreatedEvent(
                created.의뢰Id,
                command.Payload.입고상품Id,
                command.Payload.요청수량,
                DateTime.UtcNow),
            cancellationToken);

        return created;
    }
}
