using HongdalApp.Services.Application;

namespace HongdalApp.Services.Commerce.Orders.Events;

public sealed record CommerceOrderProcessedEvent(
    string ChannelType,
    string ChannelOrderNo,
    string OrderScope,
    int NotificationCount,
    DateTime OccurredAt) : IAppEvent;
