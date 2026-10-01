using HongdalApp.Services.Application;

namespace HongdalApp.Services.Customs.Events;

public sealed record CustomsBrokerAssignedEvent(
    long ReviewId,
    string BrokerName,
    DateTime OccurredAt) : IAppEvent;
