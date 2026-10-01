using HongdalApp.Models.Shipper;
using HongdalApp.Services.Application;
using HongdalApp.Services.Customs.Commands;

namespace HongdalApp.Services.Customs;

public sealed class CustomsHsReviewService : ICustomsHsReviewService
{
    private readonly InMemoryShipperStore _store;
    private readonly ICustomsBrokerDirectory _brokerDirectory;
    private readonly IAppCommandHandler<RequestCustomsHsReviewCommand, CustomsHsReviewRequest?> _requestReviewHandler;
    private readonly IAppCommandHandler<AssignCustomsBrokerCommand, bool> _assignBrokerHandler;
    private readonly IAppCommandHandler<CompleteCustomsHsReviewCommand, bool> _completeReviewHandler;

    public CustomsHsReviewService(
        InMemoryShipperStore store,
        ICustomsBrokerDirectory brokerDirectory,
        IAppCommandHandler<RequestCustomsHsReviewCommand, CustomsHsReviewRequest?> requestReviewHandler,
        IAppCommandHandler<AssignCustomsBrokerCommand, bool> assignBrokerHandler,
        IAppCommandHandler<CompleteCustomsHsReviewCommand, bool> completeReviewHandler)
    {
        _store = store;
        _brokerDirectory = brokerDirectory;
        _requestReviewHandler = requestReviewHandler;
        _assignBrokerHandler = assignBrokerHandler;
        _completeReviewHandler = completeReviewHandler;
    }

    public Task<CustomsHsReviewRequest?> RequestReviewForTransportAsync(ShipperRequestItem request, string shipperUserId, CancellationToken cancellationToken = default)
        => _requestReviewHandler.HandleAsync(new RequestCustomsHsReviewCommand(request, shipperUserId), cancellationToken);

    public Task<IReadOnlyList<CustomsHsReviewRequest>> GetReviewsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.GetCustomsHsReviews());
    }

    public async Task AssignBrokerAsync(long reviewId, string brokerId, CancellationToken cancellationToken = default)
        => await _assignBrokerHandler.HandleAsync(new AssignCustomsBrokerCommand(reviewId, brokerId), cancellationToken);

    public async Task CompleteReviewAsync(long reviewId, string hsCode, string comment, CancellationToken cancellationToken = default)
        => await _completeReviewHandler.HandleAsync(new CompleteCustomsHsReviewCommand(reviewId, hsCode, comment), cancellationToken);

    public IReadOnlyList<CustomsBrokerProfile> GetAvailableBrokers()
        => _brokerDirectory.GetAvailableBrokers();
}
