using System.Globalization;
using System.Text.Json;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed partial class InboundRequestPageViewModel
{
    private const string InboundRecoveryScope = "warehouse-inbound-mutation.v1";
    private readonly IWorkflowRecoveryStore _inboundRecoveryStore = recoveryStore ?? new VolatileWorkflowRecoveryStore();
    private WorkflowRecoverySnapshot? _inboundContext;
    private WorkflowRecoveryRecord? _inboundPending;
    private bool _inboundRecoveryLoaded;
    private sealed record SavedInboundOutcome(int Version, string OutcomeCode);

    private bool RecoveryCurrent(WorkflowRecoverySnapshot context)
        => !_disposed && _inboundContext == context && (recoveryContext?.IsCurrent(context) ?? true);

    private async Task<WorkflowRecoverySnapshot?> EnsureInboundRecoveryAsync(CancellationToken ct, bool refresh = false)
    {
        WorkflowRecoverySnapshot? context = null;
        try
        {
            if ((recoveryStore is null) != (recoveryContext is null))
                throw new InvalidOperationException("입고 복구 저장소와 계정 구성을 확인해 주세요.");
            if (access is not null) await access.EnsureInitializedAsync("shipper", ct);
            context = recoveryContext?.Capture() ?? new WorkflowRecoverySnapshot("local-session", 0, new string('0', 32));
            if (_inboundContext is not null && _inboundContext != context) ClearInboundPrivateState();
            _inboundContext = context;
            if (_inboundRecoveryLoaded && !State.RecoveryBlocked && !refresh) return context;
            var record = await _inboundRecoveryStore.LoadAsync(context.Owner, context.Scope(InboundRecoveryScope), ct);
            RequireRecoveryCurrent(context);
            if (record is not null)
            {
                ValidateInboundRecord(record, context);
                _inboundPending = record;
                if (ReadOutcome(record) == 업무처리결과Codes.미적용)
                    await RemoveInboundRecoveryAsync(context, record, ct);
                else
                {
                    ApplyInboundRecord(record);
                    if (IsServerRequest(record) && ReadTarget(record) is (false, null))
                        await RestoreCreatedInboundAsync(context, record, ct);
                }
            }
            else if (_inboundPending is not null && ReadOutcome(_inboundPending) == 업무처리결과Codes.미적용)
            {
                // 확정 거절 뒤 삭제 확인만 끊겼던 경우입니다. 사업 결과 미확정 기록에는 적용하지 않습니다.
                ClearInboundPendingState();
            }
            State.RecoveryBlocked = false;
            _inboundRecoveryLoaded = true;
            return context;
        }
        catch (Exception)
        {
            _inboundRecoveryLoaded = false;
            if (context is not null && !RecoveryCurrent(context)) { DiscardForeignRecovery(context); return null; }
            if (context is null && _inboundContext is not null)
            {
                ClearInboundPrivateState(); _inboundContext = null;
            }
            State.RecoveryBlocked = true;
            SetFailure(new InvalidOperationException("입고 복구 기록 확인 실패"),
                OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.조회,
                "이전 신청을 확인하지 못했습니다. 다시 확인해 주세요.");
            return null;
        }
    }

    private async Task<WorkflowRecoverySnapshot?> PrepareRecoveryForMutationAsync(CancellationToken ct)
    {
        State.IsBusy = true; _mutationInProgress = true;
        try { return await EnsureInboundRecoveryAsync(ct); }
        finally { State.IsBusy = false; _mutationInProgress = false; }
    }

    private async Task<bool> WriteInboundAheadAsync(WorkflowRecoverySnapshot context, bool complete, long? id, CancellationToken ct)
    {
        RequireRecoveryCurrent(context);
        if (_inboundPending is not null || State.RecoveryBlocked) return false;
        var requestId = !complete && CreateDraft.PhotoRequestId is { } photoId ? photoId : Guid.NewGuid();
        if (requestId == Guid.Empty) throw new InvalidDataException("가 입고 신청 번호를 확인해 주세요.");
        var record = new WorkflowRecoveryRecord(context.Owner, context.Scope(InboundRecoveryScope), requestId,
            JsonSerializer.Serialize(new SavedInboundOutcome(1, 업무처리결과Codes.미확정)), DateTime.UtcNow,
            Target(complete, id, serverRequest: !complete));
        // 새 신청은 보호 저장한 번호를 서버에도 전달합니다. 구판 create 번호는 소급 연결하지 않습니다.
        await SaveInboundVerifiedAsync(context, record, ct);
        ApplyInboundRecord(_inboundPending!);
        return true;
    }

    private async Task SaveInboundVerifiedAsync(WorkflowRecoverySnapshot context, WorkflowRecoveryRecord record, CancellationToken ct)
    {
        await _inboundRecoveryStore.SaveAsync(record, ct); RequireRecoveryCurrent(context);
        var readback = await _inboundRecoveryStore.LoadAsync(context.Owner, record.Scope, ct); RequireRecoveryCurrent(context);
        if (readback is null) throw new InvalidDataException("입고 복구 기록 저장을 확인하지 못했습니다.");
        ValidateInboundRecord(readback, context);
        if (readback.RequestId != record.RequestId || readback.Target != record.Target
            || readback.CreatedAtUtc != record.CreatedAtUtc
            || readback.PayloadJson != record.PayloadJson && readback.PayloadJson is not null)
            throw new InvalidDataException("입고 복구 기록이 달라졌습니다.");
        _inboundPending = readback;
    }

    private async Task PersistInboundOutcomeAsync(WorkflowRecoverySnapshot context, bool complete, long? id,
        string outcome, CancellationToken ct)
    {
        RequireRecoveryCurrent(context);
        if (_inboundPending is null) throw new InvalidDataException("원 입고 복구 번호를 확인하지 못했습니다.");
        var record = _inboundPending with
        {
            Target = Target(complete, id, IsServerRequest(_inboundPending)),
            PayloadJson = _inboundPending.PayloadJson is null ? null : JsonSerializer.Serialize(new SavedInboundOutcome(1, outcome))
        };
        await SaveInboundVerifiedAsync(context, record, ct);
    }

    private async Task RestoreCreatedInboundAsync(WorkflowRecoverySnapshot context, WorkflowRecoveryRecord original, CancellationToken ct)
    {
        RequireRecoveryCurrent(context);
        var item = await warehouseService.GetInboundByClientRequestAsync(original.RequestId, ct);
        RequireRecoveryCurrent(context);
        // 미지원·404·아직 저장 중인 결과는 모두 미확정입니다. 새 신청으로 바꾸지 않습니다.
        if (item is null) return;
        if (item.ClientRequestId != original.RequestId || item.Id <= 0)
            throw new InvalidDataException("원 입고 신청의 결과가 다릅니다.");
        if (_inboundPending?.RequestId != original.RequestId)
            throw new InvalidDataException("확인 중인 입고 신청이 변경되었습니다.");

        var current = await _inboundRecoveryStore.LoadAsync(context.Owner, original.Scope, ct);
        RequireRecoveryCurrent(context);
        if (current is not null)
        {
            ValidateInboundRecord(current, context);
            var (complete, knownId) = ReadTarget(current);
            if (current.RequestId != original.RequestId || !IsServerRequest(current)
                || complete || knownId is > 0 && knownId != item.Id)
                throw new InvalidDataException("다른 입고 신청의 결과는 변경할 수 없습니다.");
            _inboundPending = current;
            await PersistInboundOutcomeAsync(context, false, item.Id, 업무처리결과Codes.적용, ct);
            await RemoveInboundRecoveryAsync(context, _inboundPending!, ct);
        }
        else
        {
            // 다른 화면에서 같은 요청의 확인을 이미 마쳤어도 확정 GET 결과만 표시합니다.
            ClearInboundPendingState();
        }
        RequireRecoveryCurrent(context);
        State.SavedInboundId = item.Id;
        State.Created = true;
        State.Failure = null;
        SetMessage("입고 신청을 확인했습니다.", InboundRequestPageMessageTone.Success);
    }

    private async Task ConfirmInboundRecoveryAsync(입고요청항목응답 item, WorkflowRecoverySnapshot context, CancellationToken ct)
    {
        if (_inboundPending is not { } record) return;
        var (complete, id) = ReadTarget(record);
        if (!complete && id is null && State.Created && State.SavedInboundId == item.Id)
        {
            // 현재 세션의 확정 응답 ID만 보완합니다. 재시작 후 목록 후보를 이 경로로 연결하지 않습니다.
            await PersistInboundOutcomeAsync(context, false, item.Id, 업무처리결과Codes.적용, ct);
            record = _inboundPending!; id = item.Id;
        }
        // 목록의 비슷한 행이나 다른 선택 ID를 원 요청의 성공 근거로 사용하지 않습니다.
        if (id != item.Id || complete && item.상태 != 입고상태코드.완료) return;
        await RemoveInboundRecoveryAsync(context, record, ct);
    }

    private async Task RemoveInboundRecoveryAsync(WorkflowRecoverySnapshot context, WorkflowRecoveryRecord record, CancellationToken ct)
    {
        await _inboundRecoveryStore.RemoveAsync(context.Owner, record.Scope, record.RequestId, ct); RequireRecoveryCurrent(context);
        if (await _inboundRecoveryStore.LoadAsync(context.Owner, record.Scope, ct) is not null)
            throw new InvalidDataException("입고 복구 기록 해제를 확인하지 못했습니다.");
        RequireRecoveryCurrent(context);
        ClearInboundPendingState();
    }

    private void ClearInboundPendingState()
    {
        _inboundPending = null;
        State.RegistrationResultPending = false;
        State.CompletionResultPendingId = null;
        State.CompletionApplied = false;
        State.RecoveryBlocked = false;
        State.CanQueryRegistrationResult = false;
    }

    private void ApplyInboundRecord(WorkflowRecoveryRecord record)
    {
        var (complete, id) = ReadTarget(record);
        var outcome = ReadOutcome(record);
        State.RegistrationResultPending = !complete;
        State.SavedInboundId = complete ? null : id;
        State.Created = !complete && outcome == 업무처리결과Codes.적용;
        State.CompletionResultPendingId = complete ? id : null;
        State.CompletionApplied = complete && outcome == 업무처리결과Codes.적용;
        State.CanQueryRegistrationResult = !complete && id is null && IsServerRequest(record);
        SetFailure(new InvalidOperationException("이전 입고 요청 결과 확인"),
            complete ? OperatingSystemLifecycleStageIds.WarehouseReceiving : OperatingSystemLifecycleStageIds.WarehouseInboundPlan,
            outcome, "이전 입고 요청의 결과를 확인해 주세요. 새 요청을 보내지 않습니다.");
    }

    private static void ValidateInboundRecord(WorkflowRecoveryRecord record, WorkflowRecoverySnapshot context)
    {
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != context.Owner || record.Scope != context.Scope(InboundRecoveryScope))
            throw new InvalidDataException("입고 복구 기록의 계정과 서버가 다릅니다.");
        _ = ReadTarget(record); _ = ReadOutcome(record);
    }

    private static string Target(bool complete, long? id, bool serverRequest = false)
        => complete ? $"complete:{id?.ToString(CultureInfo.InvariantCulture)}"
            : id is > 0 ? $"{(serverRequest ? "create-request" : "create")}:{id.Value.ToString(CultureInfo.InvariantCulture)}"
                : serverRequest ? "create-request" : "create";

    private static bool IsServerRequest(WorkflowRecoveryRecord record)
        => record.Target == "create-request" || record.Target?.StartsWith("create-request:", StringComparison.Ordinal) == true;

    private static (bool Complete, long? Id) ReadTarget(WorkflowRecoveryRecord record)
    {
        if (record.Target is "create" or "create-request") return (false, null);
        var parts = record.Target?.Split(':');
        if (parts?.Length != 2 || parts[0] is not ("create" or "create-request" or "complete")
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new InvalidDataException("이전 입고 요청의 업무 대상을 확인하지 못했습니다.");
        return (parts[0] == "complete", id);
    }

    private static string ReadOutcome(WorkflowRecoveryRecord record)
    {
        if (record.PayloadJson is null) return 업무처리결과Codes.미확정;
        var saved = JsonSerializer.Deserialize<SavedInboundOutcome>(record.PayloadJson);
        if (saved?.Version != 1 || saved.OutcomeCode is not (업무처리결과Codes.미확정 or 업무처리결과Codes.적용 or 업무처리결과Codes.미적용))
            throw new InvalidDataException("이전 입고 결과를 확인하지 못했습니다.");
        return saved.OutcomeCode;
    }

    private void RequireRecoveryCurrent(WorkflowRecoverySnapshot context)
    {
        if (!RecoveryCurrent(context)) throw new OperationCanceledException("입고 업무의 계정 또는 서버가 변경되었습니다.");
    }

    private void DiscardForeignRecovery(WorkflowRecoverySnapshot context)
    {
        if (_inboundContext != context) return;
        ClearInboundPrivateState(); _inboundContext = null;
    }

    private void ClearInboundPrivateState()
    {
        _inboundPending = null; _inboundRecoveryLoaded = false;
        State.Current = null; State.Items = []; State.TotalCount = 0; State.Warehouses = [];
        State.CompletionItems = []; State.Failure = null; State.Message = null;
        State.ListFilter = new(); State.MessageTone = InboundRequestPageMessageTone.Info;
        State.Created = State.RegistrationResultPending = State.CompletionApplied = State.RecoveryBlocked = false;
        State.CanQueryRegistrationResult = false;
        State.SavedInboundId = State.CompletionResultPendingId = null;
        CopyDefaults(new InboundRequestCreateDraft(), CreateDraft);
        CopyDefaults(new InboundRequestCompletionDraft(), CompletionDraft);
        CopyDefaults(new WarehouseRegistrationDraft(), WarehouseDraft);
        SelectedSalesProduct = null;
    }

    private static void CopyDefaults<T>(T source, T target)
    {
        foreach (var property in typeof(T).GetProperties().Where(property => property.CanRead && property.CanWrite))
            property.SetValue(target, property.GetValue(source));
    }
}
