using HongdalApp.Models.Shipper;
using HongdalApp.Services.Application;
using HongdalApp.Services.Customs.Events;

namespace HongdalApp.Services.Customs.Commands;

public sealed class RequestCustomsHsReviewCommandHandler : IAppCommandHandler<RequestCustomsHsReviewCommand, CustomsHsReviewRequest?>
{
    private readonly InMemoryShipperStore _store;
    private readonly IProductHsCodeInferenceService _inferenceService;
    private readonly IAppEventPublisher _eventPublisher;

    public RequestCustomsHsReviewCommandHandler(
        InMemoryShipperStore store,
        IProductHsCodeInferenceService inferenceService,
        IAppEventPublisher eventPublisher)
    {
        _store = store;
        _inferenceService = inferenceService;
        _eventPublisher = eventPublisher;
    }

    public async Task<CustomsHsReviewRequest?> HandleAsync(RequestCustomsHsReviewCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var flowDirection = ResolveFlowDirection(command.Request);
        if (flowDirection == CustomsFlowDirectionCodes.Domestic)
        {
            return null;
        }

        var existing = _store.FindCustomsHsReviewByTransportRequestId(command.Request.의뢰Id);
        if (existing is not null)
        {
            return existing;
        }

        var review = _store.CreateCustomsHsReview(new CustomsHsReviewRequest
        {
            TransportRequestId = command.Request.의뢰Id,
            ShipperUserId = command.ShipperUserId,
            CargoName = command.Request.화물종류,
            FlowDirection = flowDirection,
            PickupLocation = command.Request.픽업지 ?? string.Empty,
            DropoffLocation = command.Request.하차지 ?? string.Empty,
            Status = CustomsHsReviewStatusCodes.Requested,
            Suggestions = _inferenceService.Suggest(command.Request.화물종류, flowDirection)
        });

        await _eventPublisher.PublishAsync(
            new CustomsHsReviewRequestedEvent(review.Id, review.TransportRequestId, review.FlowDirection, review.CargoName, DateTime.UtcNow),
            cancellationToken);

        return review;
    }

    private static string ResolveFlowDirection(ShipperRequestItem request)
    {
        var text = $"{request.운송방식} {request.픽업지} {request.하차지}".ToLowerInvariant();
        if (text.Contains("수입") || text.Contains("import") || text.Contains("china") || text.Contains("shanghai") || text.Contains("usa") || text.Contains("overseas"))
        {
            return CustomsFlowDirectionCodes.Import;
        }

        if (text.Contains("수출") || text.Contains("export") || text.Contains("미국") || text.Contains("일본") || text.Contains("베트남") || text.Contains("해외"))
        {
            return CustomsFlowDirectionCodes.Export;
        }

        return CustomsFlowDirectionCodes.Domestic;
    }
}

public sealed class AssignCustomsBrokerCommandHandler : IAppCommandHandler<AssignCustomsBrokerCommand, bool>
{
    private readonly InMemoryShipperStore _store;
    private readonly ICustomsBrokerDirectory _brokerDirectory;
    private readonly IAppEventPublisher _eventPublisher;

    public AssignCustomsBrokerCommandHandler(
        InMemoryShipperStore store,
        ICustomsBrokerDirectory brokerDirectory,
        IAppEventPublisher eventPublisher)
    {
        _store = store;
        _brokerDirectory = brokerDirectory;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> HandleAsync(AssignCustomsBrokerCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var broker = _brokerDirectory.GetAvailableBrokers().FirstOrDefault(x => x.BrokerId == command.BrokerId)
            ?? throw new InvalidOperationException("관세사를 찾을 수 없습니다.");
        _store.AssignCustomsBroker(command.ReviewId, broker);

        await _eventPublisher.PublishAsync(new CustomsBrokerAssignedEvent(command.ReviewId, broker.BrokerName, DateTime.UtcNow), cancellationToken);
        return true;
    }
}

public sealed class CompleteCustomsHsReviewCommandHandler : IAppCommandHandler<CompleteCustomsHsReviewCommand, bool>
{
    private readonly InMemoryShipperStore _store;
    private readonly IAppEventPublisher _eventPublisher;

    public CompleteCustomsHsReviewCommandHandler(InMemoryShipperStore store, IAppEventPublisher eventPublisher)
    {
        _store = store;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> HandleAsync(CompleteCustomsHsReviewCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _store.CompleteCustomsHsReview(command.ReviewId, command.HsCode, command.Comment);
        await _eventPublisher.PublishAsync(new CustomsHsReviewCompletedEvent(command.ReviewId, command.HsCode, DateTime.UtcNow), cancellationToken);
        return true;
    }
}
