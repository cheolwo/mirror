using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

public sealed partial class 분산이행PageViewModel
{
    private readonly IWorkflowRecoveryStore recoveryStore;
    private bool recoveryLoaded;
    private DateTime? recoveryCreatedAtUtc;
    private Guid? expiredRecoveryId;
    private string RecoveryScope => $"distributed.{RoleKey}.{Screen}.v1";
    public bool RecoveryExpired => expiredRecoveryId is not null;
    private static readonly Type[] BodyTypes = [typeof(DistributedCommandRequest), typeof(DistributedSiteCreateRequest),
        typeof(DistributedConsentRequest), typeof(DistributedLocationCreateRequest), typeof(DistributedExistingLotRequest),
        typeof(DistributedInboundPlanRequest), typeof(DistributedInboundReceiptRequest), typeof(DistributedWaveCreateRequest),
        typeof(DistributedOrderRequest), typeof(DistributedOrderLedgerLinkRequest), typeof(DistributedRoundPlanRequest),
        typeof(DistributedRoundAcceptRequest), typeof(DistributedPickRequest), typeof(DistributedDeliveryRequest),
        typeof(DistributedCancelRequest), typeof(DistributedReturnRequest), typeof(DistributedAcknowledgementRequest),
        typeof(DistributedStorageAgreementRequest), typeof(DistributedStorageAgreementConsentRequest),
        typeof(DistributedSiteAcceptancePolicyRequest), typeof(DistributedInboundProposalRequest),
        typeof(DistributedInboundConsentRequest), typeof(DistributedInboundDecisionRequest), typeof(DistributedInboundNoteRequest),
        typeof(DistributedInboundReceiptBatchRequest), typeof(DistributedPickBatchRequest),
        typeof(DistributedDeliveryBatchRequest), typeof(DistributedReturnBatchRequest), typeof(DistributedStockReleaseProposalRequest),
        typeof(DistributedStockReleaseConsentRequest), typeof(DistributedStockReleaseHandoverRequest), typeof(DistributedSiteCloseRequest)];
    private Task PersistRecoveryAsync(CancellationToken ct)
    {
        RequireCurrent(ct);
        var id = pending?.Body.RequestId ?? Photo.CompletionRequestId;
        if (id is null) return Task.CompletedTask;
        recoveryCreatedAtUtc ??= DateTime.UtcNow;
        var saved = pending is null ? null : new SavedCommand(pending.Action, pending.Suffix, pending.Body.GetType().Name,
            JsonSerializer.Serialize(pending.Body, pending.Body.GetType()), pending.AggregateId, pending.Quantity,
            pending.ReservationId, pending.Sent, pending.Acknowledgement);
        return recoveryStore.SaveAsync(new(owner!.OwnerId!, RecoveryScope, id.Value,
            JsonSerializer.Serialize(new SavedWorkflow(SelectedId, saved, Photo.Snapshot(), photoOperation)),
            recoveryCreatedAtUtc.Value, SelectedId), ct);
    }
    private async Task RestoreRecoveryAsync(CancellationToken ct)
    {
        if (recoveryLoaded) return;
        var record = await recoveryStore.LoadAsync(owner!.OwnerId!, RecoveryScope, ct); RequireCurrent(ct);
        if (record is null) { recoveryLoaded = true; return; }
        if (record.Owner != owner.OwnerId || record.Scope != RecoveryScope) throw new InvalidDataException("이전 요청의 계정과 역할이 다릅니다.");
        recoveryCreatedAtUtc = record.CreatedAtUtc;
        if (record.PayloadJson is null)
        {
            expiredRecoveryId = record.RequestId; SelectedId = CleanId(record.Target);
            var receipt = await TryReceiptAsync(record.RequestId, ct); RequireCurrent(ct);
            if (receipt is not null) await FinishRecoveredReceiptAsync(receipt, ct);
            else Message = "이전 입력 원문은 72시간 뒤 삭제했습니다. 원 요청 결과가 없으므로 새 작업을 시작할지 선택해 주세요.";
        }
        else
        {
            var saved = JsonSerializer.Deserialize<SavedWorkflow>(record.PayloadJson) ?? throw new InvalidDataException("이전 업무 요청을 확인하지 못했습니다.");
            SelectedId = CleanId(saved.SelectedId);
            if (saved.Command is { } command)
            {
                var type = BodyTypes.SingleOrDefault(type => type.Name == command.BodyType) ?? throw new InvalidDataException("이전 요청의 업무 종류를 확인하지 못했습니다.");
                var body = (DistributedCommandRequest?)JsonSerializer.Deserialize(command.BodyJson, type) ?? throw new InvalidDataException("이전 요청 원문을 확인하지 못했습니다.");
                if (body.RequestId != record.RequestId) throw new InvalidDataException("이전 요청 번호가 다릅니다.");
                pending = new(command.Action, command.Suffix, body, command.AggregateId, command.Quantity, command.ReservationId)
                    { Sent = command.Sent, Acknowledgement = command.Acknowledgement };
            }
            if (saved.Photo is { } photo)
            {
                if (분산사진업무Session.Body(photo.Work)?.RequestId != record.RequestId) throw new InvalidDataException("이전 사진의 완료 번호가 다릅니다.");
                Photo.Restore(photo); photoOperation = saved.PhotoOperation;
            }
            var receipt = await TryReceiptAsync(record.RequestId, ct); RequireCurrent(ct);
            if (receipt is not null)
            {
                if (pending is not null)
                {
                    pending.Acknowledgement = new(receipt.RequestId, receipt.AggregateId, receipt.Revision, "Recorded", true);
                    await SendPendingAsync(ct); // 영수증이 있으므로 외부 POST를 보내지 않습니다.
                }
                else await FinishRecoveredReceiptAsync(receipt, ct);
            }
            else if (Photo.Work is not null)
            {
                await Photo.RecoverUploadsAsync(client, ct, () => RequireCurrent(ct)); RequireCurrent(ct);
                Message = Photo.NeedsRecapture ? "이전 사진 원본은 보관하지 않았습니다. 저장되지 않은 사진은 현재 업무에서 다시 촬영해 주세요."
                    : "사진 저장을 다시 확인했습니다. 완료 기록은 아래 버튼을 눌러 이어서 요청할 수 있습니다.";
            }
            else Message = "이전 요청을 복원했습니다. 같은 요청 결과 확인을 눌러 직접 이어가세요.";
        }
        recoveryLoaded = true;
    }
    private async Task<DistributedReceiptDto?> TryReceiptAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await client.ReceiptAsync(id, ct); RequireCurrent(ct);
            if (result.RequestId != id || result.Revision < 1) throw new InvalidDataException("원 요청의 저장 근거가 다릅니다.");
            return result;
        }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404) { return null; }
        catch (SsalddelApiException ex) when (ex.StatusCode == 404) { return null; }
    }
    private async Task FinishRecoveredReceiptAsync(DistributedReceiptDto receipt, CancellationToken ct)
    {
        if (Photo.Work is { } work && photoOperation is { } operation)
        {
            var action = operation.Action is "import-lot" or "handover-stock-release" ? operation.Action : operation.Action + "-batch";
            if (receipt.RequestId != Photo.CompletionRequestId || receipt.AggregateId != work.AggregateId
                || receipt.Kind != ReceiptKind(action) || receipt.Revision <= 분산사진업무Session.Body(work)!.ExpectedRevision
                || operation.Quantity is { } quantity && receipt.Quantity != quantity)
                throw new InvalidDataException("이전 사진 완료의 업무·대상·수량을 확인하지 못했습니다.");
        }
        await RefreshAsync(ct); RequireCurrent(ct);
        if (FindRevision(receipt.AggregateId) is not { } revision || revision < receipt.Revision)
            throw new InvalidDataException("이전 요청의 최신 원장을 확인하지 못했습니다.");
        await recoveryStore.RemoveAsync(owner!.OwnerId!, RecoveryScope, receipt.RequestId, ct); RequireCurrent(ct);
        pending = null; Photo.Clear(); photoOperation = null; expiredRecoveryId = null; recoveryCreatedAtUtc = null;
        Message = "이전에 저장된 처리 결과와 최신 업무 상태를 확인했습니다.";
    }
    public Task DiscardExpiredRecoveryAsync() => !RecoveryExpired || IsBusy ? Task.CompletedTask : RunAsync(async ct =>
    {
        var id = expiredRecoveryId!.Value; var receipt = await TryReceiptAsync(id, ct); RequireCurrent(ct);
        if (receipt is not null) { await FinishRecoveredReceiptAsync(receipt, ct); return; }
        await recoveryStore.RemoveAsync(owner!.OwnerId!, RecoveryScope, id, ct); RequireCurrent(ct);
        expiredRecoveryId = null; recoveryCreatedAtUtc = null; await RefreshAsync(ct);
        Message = "미접수 결과를 확인했습니다. 현재 업무 조건으로 새 작업을 시작할 수 있습니다.";
    });
    private sealed record SavedCommand(string Action, string Suffix, string BodyType, string BodyJson,
        string? AggregateId, int? Quantity, string? ReservationId, bool Sent, DistributedCommandResponse? Acknowledgement);
    private sealed record SavedWorkflow(string? SelectedId, SavedCommand? Command, DistributedPhotoSessionRecovery? Photo, PhotoOperation? PhotoOperation);
}
