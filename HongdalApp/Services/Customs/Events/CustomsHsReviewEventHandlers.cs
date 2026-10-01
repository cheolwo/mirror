using HongdalApp.Services.Application;

namespace HongdalApp.Services.Customs.Events;

public sealed class CustomsHsReviewRequestedEventHandler : IAppEventHandler<CustomsHsReviewRequestedEvent>
{
    private readonly InMemoryShipperStore _store;

    public CustomsHsReviewRequestedEventHandler(InMemoryShipperStore store)
    {
        _store = store;
    }

    public Task HandleAsync(CustomsHsReviewRequestedEvent appEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.AddAppEventLog(
            nameof(CustomsHsReviewRequestedEvent),
            $"{appEvent.FlowDirection} 의뢰 {appEvent.TransportRequestId} HS 검토 요청 생성: {appEvent.CargoName}",
            appEvent.OccurredAt);
        return Task.CompletedTask;
    }
}

public sealed class CustomsBrokerAssignedEventHandler : IAppEventHandler<CustomsBrokerAssignedEvent>
{
    private readonly InMemoryShipperStore _store;

    public CustomsBrokerAssignedEventHandler(InMemoryShipperStore store)
    {
        _store = store;
    }

    public Task HandleAsync(CustomsBrokerAssignedEvent appEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.AddAppEventLog(
            nameof(CustomsBrokerAssignedEvent),
            $"HS 검토 {appEvent.ReviewId}에 {appEvent.BrokerName} 배정",
            appEvent.OccurredAt);
        return Task.CompletedTask;
    }
}

public sealed class CustomsHsReviewCompletedEventHandler : IAppEventHandler<CustomsHsReviewCompletedEvent>
{
    private readonly InMemoryShipperStore _store;

    public CustomsHsReviewCompletedEventHandler(InMemoryShipperStore store)
    {
        _store = store;
    }

    public Task HandleAsync(CustomsHsReviewCompletedEvent appEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.AddAppEventLog(
            nameof(CustomsHsReviewCompletedEvent),
            $"HS 검토 {appEvent.ReviewId} 완료: {appEvent.HsCode}",
            appEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
