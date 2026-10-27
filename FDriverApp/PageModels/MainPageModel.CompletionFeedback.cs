using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    public FDriverCompletionFeedbackState Completion { get; } = new();
    private long completionReadGeneration = -1;
    private string? completionReadOwner;

    // A successful GET of an empty list is not a delivery receipt. The matching
    // command response must independently prove this particular attempt's result.
    private void RecordFoodCompletion(FoodDeliveryCompletionTarget target,
        FoodDeliveryDriverActionResponse? response, string? owner)
    {
        if (!_workspacePageVisible || !_workspaceActive || !IsAuthenticated || owner != _authSession.UserId
            || completionReadGeneration != _workspaceRefresh.SnapshotGeneration || completionReadOwner != owner
            || HasWorkspaceWarning || !IsFoodCompletionReceipt(target, response)) return;

        var current = ActiveDeliveryItems.FirstOrDefault(x => x.OfferId == target.OfferId);
        if (target.IsFinal ? current is not null
            : current is null || current.DeliveryAttemptId != target.AttemptId || current.AttemptRevision < response!.AttemptRevision!.Value
                || current.WorkStatus is not (DriverWorkOfferStatus.PickupConfirmed or DriverWorkOfferStatus.MovingToDropoff)) return;

        var quick = target.WorkKind == Ssalddel.Contracts.Driver.Settings.DriverWorkKinds.QuickDelivery;
        Completion.Record(new($"food:{target.OfferId}:{target.AttemptId}:{(target.IsFinal ? "delivery" : "pickup")}",
            target.IsFinal ? "전달을 완료했습니다" : quick ? "상품을 픽업했습니다" : "음식을 픽업했습니다",
            target.IsFinal ? "해당 배달의 전달 완료 기록을 확인했습니다." : "해당 배달의 픽업 완료 기록을 확인했습니다.",
            target.IsFinal ? "수령 확인·정산·지급은 각 내역에서 확인해 주세요." : "다음 단계: 전달 장소로 이동",
            target.IsFinal, quick ? WorkflowCompletionVisuals.CargoIconPath : WorkflowCompletionVisuals.FoodIconPath,
            quick ? "#DC2626" : "#2563EB"));
    }

    private static bool IsFoodCompletionReceipt(FoodDeliveryCompletionTarget target, FoodDeliveryDriverActionResponse? response)
        => response is not null && response.OfferId == target.OfferId && !string.IsNullOrWhiteSpace(target.AttemptId)
            && response.DeliveryAttemptId == target.AttemptId && response.AttemptRevision is { } revision && revision > target.Revision
            && response.Status == (target.IsFinal ? DriverWorkOfferStatus.Completed : DriverWorkOfferStatus.MovingToDropoff);

    private sealed record FoodDeliveryCompletionTarget(string OfferId, string AttemptId, long Revision, bool IsFinal, string? WorkKind);
}
