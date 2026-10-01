using HongdalApp.Services.Application;

namespace HongdalApp.Services.Samples.Events;

public sealed record ShipperRequestAddedEvent(
    string TransportRequestId,
    string CargoName,
    string PickupLocation,
    string DropoffLocation,
    DateTime OccurredAt) : IAppEvent;
