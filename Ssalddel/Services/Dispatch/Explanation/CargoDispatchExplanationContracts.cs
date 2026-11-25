using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Explanation;

public interface ICargoDispatchExplanationRecorder
{
    Task<운송이벤트?> ReserveAsync(운송원장 queue, string driverId, DateTime decisionAtUtc,
        string methodCode, 국내화물배차판정근거? sourceEvidence, CancellationToken ct = default,
        CargoDispatchExplanationDecisionFacts? decisionFacts = null);
}

public sealed record CargoDispatchExplanationDecisionFacts(bool? FullPlanFeasible = null,
    bool? SegmentLoadingVerified = null, int? OwnPickupOrder = null, int? OwnDropoffOrder = null);

public interface ICargoDispatchExplanationService
{
    Task<CargoDispatchExplanationResponse> GetAsync(string userId, string requestId, CancellationToken ct = default);
    Task<CargoDispatchNoticeContext> GetNoticeContextAsync(string userId, string requestId, string evidenceId, CancellationToken ct = default);
    Task<CargoDispatchFeedbackReceipt> SubmitFeedbackAsync(string userId, string requestId,
        CargoDispatchFeedbackRequest request, CancellationToken ct = default);
    Task<CargoDispatchFeedbackReceipt?> GetFeedbackAsync(string userId, string requestId, string evidenceId,
        Guid clientRequestId, CancellationToken ct = default);
}

public interface ICargoDispatchFeedbackStore
{
    Task<CargoDispatchFeedbackStored> InsertAsync(CargoDispatchFeedbackStored candidate, CancellationToken ct = default);
    Task<CargoDispatchFeedbackStored?> GetAsync(string requestId, string evidenceId, string userId,
        Guid clientRequestId, CancellationToken ct = default);
    Task<IReadOnlyList<CargoDispatchFeedbackStored>> ListAsync(string requestId, string evidenceId,
        string userId, CancellationToken ct = default);
}

public sealed record CargoDispatchFeedbackStored(string RequestId, string EvidenceId, string UserId,
    Guid ClientRequestId, string CategoryCode, string Comment, DateTime SubmittedAtUtc, string FeedbackId)
{
    public CargoDispatchFeedbackReceipt ToReceipt() => new()
    {
        FeedbackId = FeedbackId, ClientRequestId = ClientRequestId, EvidenceId = EvidenceId,
        Status = "received", CategoryCode = CategoryCode, SubmittedAtUtc = SubmittedAtUtc,
        SubmissionFingerprint = CargoDispatchFeedbackFingerprint.Compute(CategoryCode, Comment)
    };
}

public sealed class CargoDispatchExplanationNotFoundException() : InvalidOperationException("CargoDispatchExplanationNotFound");
public sealed class CargoDispatchFeedbackConflictException() : InvalidOperationException("CargoDispatchFeedbackRequestConflict");
public sealed class CargoDispatchExplanationUnavailableException(Exception? inner = null)
    : InvalidOperationException("CargoDispatchExplanationUnavailable", inner);
public sealed class CargoDispatchFeedbackUnavailableException(Exception? inner = null)
    : InvalidOperationException("CargoDispatchFeedbackUnavailable", inner);
