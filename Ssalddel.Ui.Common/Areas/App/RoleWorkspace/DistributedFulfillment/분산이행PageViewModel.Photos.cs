using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

public sealed partial class 분산이행PageViewModel
{
    private PhotoOperation? photoOperation;
    public 분산사진업무Session Photo { get; }
    public bool HasPendingPhoto => (Photo.UploadStarted || Photo.Restored) && Photo.Work is not null && !HasPendingCommand;
    public bool PhotoInputsLocked => Photo.UploadStarted || Photo.Restored || HasPendingCommand || RecoveryExpired || IsBusy;
    public void ClearPhotos()
    { if (HasPendingCommand || Photo.HasUnresolvedUpload) return; Photo.Clear(); photoOperation = null; }
    public void RemovePhoto(int index)
    { if (!PhotoInputsLocked) { Photo.Remove(index); Changed?.Invoke(); } }
    public Task CapturePhotoAsync(DistributedEvidenceWorkRequest work, string title, string action, string suffix,
        IReadOnlyList<string> allowedActions, int? quantity = null)
    {
        if (!Allows(action, allowedActions) || PhotoInputsLocked) return Task.CompletedTask;
        if (!ValidPhotoWork(work, action)) { ClearPhotos(); return InvalidInputAsync("현재 업무의 위치·상품·남은 수량을 다시 확인해 주세요."); }
        if (!Photo.Bind(work, title)) return InvalidInputAsync("먼저 원래 사진 전송과 완료 요청의 결과를 확인해 주세요.");
        photoOperation = new(action, suffix, quantity);
        return RunAsync(async ct =>
        {
            try
            {
                var captured = await Photo.CaptureAsync(ct); RequireCurrent(ct);
                Message = captured ? "사진을 확인한 뒤 보내고 완료해 주세요." : "사진 촬영이 취소되었습니다.";
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            { RequireCurrent(ct); ValidationError = ex.Message; }
        });
    }
    private bool ValidPhotoWork(DistributedEvidenceWorkRequest work, string action)
        => new 분산사진준비Policy(Commands, CurrentUserId, SelectedInbound).IsValid(work, action);
    public Task CompletePhotoAsync(bool confirmed)
    {
        if (!confirmed || !CanEdit || photoOperation is null || Photo.Work is null) return Task.CompletedTask;
        if (!Photo.HasPhotos) return InvalidInputAsync("완료 사진을 먼저 촬영하고 확인해 주세요.");
        return RunAsync(UploadAndCompleteAsync);
    }
    public Task RetryPhotoAsync() => !HasPendingPhoto ? Task.CompletedTask : RunAsync(async ct =>
    {
        if (Photo.CompletionRequestId is { } id)
        {
            var receipt = await TryReceiptAsync(id, ct); RequireCurrent(ct);
            if (receipt is not null) { await FinishRecoveredReceiptAsync(receipt, ct); return; }
        }
        if (Photo.Restored)
        {
            await Photo.RecoverUploadsAsync(client, ct, () => RequireCurrent(ct)); RequireCurrent(ct);
            if (Photo.NeedsRecapture)
            { await RefreshAsync(ct); Message = "저장되지 않은 사진의 원본은 보관하지 않았습니다. 사진 작업을 닫고 현재 업무에서 다시 촬영해 주세요."; return; }
        }
        await UploadAndCompleteAsync(ct);
    });
    public Task CancelPhotoWorkAsync()
    {
        if (IsBusy || HasPendingCommand) return Task.CompletedTask;
        return RunAsync(async ct =>
        {
            if (Photo.CompletionRequestId is { } id)
            {
                var receipt = await TryReceiptAsync(id, ct); RequireCurrent(ct);
                if (receipt is not null) { await FinishRecoveredReceiptAsync(receipt, ct); return; }
                await recoveryStore.RemoveAsync(owner!.OwnerId!, RecoveryScope, id, ct); RequireCurrent(ct);
            }
            Photo.Clear(); photoOperation = null; recoveryCreatedAtUtc = null; await RefreshAsync(ct);
            Message = "완료 기록이 없는 사진 작업을 닫았습니다. 현재 위치·수량을 확인하고 다시 촬영해 주세요.";
        });
    }
    private async Task UploadAndCompleteAsync(CancellationToken ct)
    {
        var operation = photoOperation ?? throw new InvalidOperationException("확인할 사진 업무가 없습니다.");
        await Photo.UploadAsync(client, ct, () => RequireCurrent(ct), () => PersistRecoveryAsync(ct)); RequireCurrent(ct);
        var body = Photo.CompletionBody();
        pending = 분산명령영수증Session.Freeze(분산명령영수증Session.PhotoCompletionAction(operation.Action), operation.Suffix, body,
            Photo.Work!.AggregateId, operation.Quantity, null);
        await SendPendingAsync(ct);
    }
    private sealed record PhotoOperation(string Action, string Suffix, int? Quantity);
}
