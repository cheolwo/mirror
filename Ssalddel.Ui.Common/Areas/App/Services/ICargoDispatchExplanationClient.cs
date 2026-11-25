using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public sealed record CargoDispatchExplanationIdentity(string? UserId, long SessionRevision, bool IsAuthenticated);

/// <summary>실제 저장된 배차 설명과 본인의 비공개 의견만 조회·접수합니다.</summary>
public interface ICargoDispatchExplanationClient
{
    const string Endpoint = "api/v1/cargo-dispatch-explanations";
    CargoDispatchExplanationIdentity Identity { get; }
    event Action? SessionChanged;
    Task RestoreAsync(CancellationToken cancellationToken = default);
    Task<CargoDispatchExplanationResponse> ReadAsync(string requestId, CancellationToken cancellationToken = default);
    Task<CargoDispatchFeedbackReceipt> SubmitAsync(string requestId, CargoDispatchFeedbackRequest request,
        CancellationToken cancellationToken = default);
    Task<CargoDispatchFeedbackReceipt?> ReadFeedbackAsync(string requestId, string evidenceId, Guid clientRequestId,
        CancellationToken cancellationToken = default);
}
