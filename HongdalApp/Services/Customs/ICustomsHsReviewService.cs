using HongdalApp.Models.Shipper;

namespace HongdalApp.Services.Customs;

public interface ICustomsHsReviewService
{
    Task<CustomsHsReviewRequest?> RequestReviewForTransportAsync(ShipperRequestItem request, string shipperUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomsHsReviewRequest>> GetReviewsAsync(CancellationToken cancellationToken = default);

    Task AssignBrokerAsync(long reviewId, string brokerId, CancellationToken cancellationToken = default);

    Task CompleteReviewAsync(long reviewId, string hsCode, string comment, CancellationToken cancellationToken = default);

    IReadOnlyList<CustomsBrokerProfile> GetAvailableBrokers();
}
