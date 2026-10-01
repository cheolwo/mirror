using HongdalApp.Services.Application;

namespace HongdalApp.Services.Warehouse.Reconsignment.Events;

public sealed record ReconsignmentOrderCreatedEvent(
    string TransportRequestId,
    long InventoryItemId,
    int RequestedQuantity,
    DateTime OccurredAt) : IAppEvent;
