using System.Globalization;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace DriverApp.Services;

public interface IDriverCargoCompletionRecovery
{
    WorkflowRecoverySnapshot Capture();
    bool IsCurrent(WorkflowRecoverySnapshot snapshot);
    Task<WorkflowRecoveryRecord?> LoadAsync(WorkflowRecoverySnapshot snapshot, DriverTransportCompletionPhotoKind kind,
        long transportId, CancellationToken cancellationToken = default);
    Task<WorkflowRecoveryRecord> BeginAsync(WorkflowRecoverySnapshot snapshot, DriverTransportCompletionPhotoKind kind,
        long transportId, Guid localRecordId, CancellationToken cancellationToken = default);
    Task ClearAsync(WorkflowRecoverySnapshot snapshot, DriverTransportCompletionPhotoKind kind, long transportId,
        Guid expectedRecordId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 완료 요청을 다시 보내지 않도록 계정·서버·운송·상하차별 관측 번호만 보호 보관합니다.
/// RequestId는 단말의 상관 키이며 서버의 멱등 요청 ID가 아닙니다. 사진·주소·서명은 저장하지 않습니다.
/// </summary>
public sealed class DriverCargoCompletionRecovery(IWorkflowRecoveryStore store, IWorkflowRecoveryContext context)
    : IDriverCargoCompletionRecovery
{
    public WorkflowRecoverySnapshot Capture() => context.Capture();
    public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => context.IsCurrent(snapshot);

    public async Task<WorkflowRecoveryRecord?> LoadAsync(WorkflowRecoverySnapshot snapshot,
        DriverTransportCompletionPhotoKind kind, long transportId, CancellationToken cancellationToken = default)
    {
        RequireCurrent(snapshot, cancellationToken);
        var scope = Scope(snapshot, kind, transportId);
        var record = await store.LoadAsync(snapshot.Owner, scope, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        if (record is not null) Validate(record, snapshot, scope, transportId);
        return record;
    }

    public async Task<WorkflowRecoveryRecord> BeginAsync(WorkflowRecoverySnapshot snapshot,
        DriverTransportCompletionPhotoKind kind, long transportId, Guid localRecordId, CancellationToken cancellationToken = default)
    {
        if (localRecordId == Guid.Empty) throw new ArgumentException("보호 보관할 관측 번호가 필요합니다.", nameof(localRecordId));
        var existing = await LoadAsync(snapshot, kind, transportId, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        if (existing is not null) throw new InvalidOperationException("이전 완료 요청의 결과를 먼저 확인해 주세요.");
        var record = new WorkflowRecoveryRecord(snapshot.Owner, Scope(snapshot, kind, transportId), localRecordId,
            null, DateTime.UtcNow, transportId.ToString(CultureInfo.InvariantCulture));
        await store.SaveAsync(record, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        var persisted = await LoadAsync(snapshot, kind, transportId, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        if (persisted?.RequestId != record.RequestId)
            throw new IOException("완료 요청의 보호 보관을 확인하지 못했습니다. 요청을 보내지 않았습니다.");
        return persisted;
    }

    public async Task ClearAsync(WorkflowRecoverySnapshot snapshot, DriverTransportCompletionPhotoKind kind,
        long transportId, Guid expectedRecordId, CancellationToken cancellationToken = default)
    {
        if (expectedRecordId == Guid.Empty) throw new ArgumentException("확인할 완료 요청 번호가 필요합니다.", nameof(expectedRecordId));
        RequireCurrent(snapshot, cancellationToken);
        await store.RemoveAsync(snapshot.Owner, Scope(snapshot, kind, transportId), expectedRecordId, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        if (await LoadAsync(snapshot, kind, transportId, cancellationToken) is not null)
            throw new IOException("완료 요청의 보호 기록 정리를 확인하지 못했습니다. 다시 확인해 주세요.");
        RequireCurrent(snapshot, cancellationToken);
    }

    private void RequireCurrent(WorkflowRecoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.IsCurrent(snapshot)) throw new OperationCanceledException("현재 계정과 서버의 업무만 확인할 수 있습니다.", cancellationToken);
    }

    private static string Scope(WorkflowRecoverySnapshot snapshot, DriverTransportCompletionPhotoKind kind, long transportId)
        => snapshot.Scope(LogicalScope(kind, transportId));

    internal static string LogicalScope(DriverTransportCompletionPhotoKind kind, long transportId)
    {
        if (transportId <= 0 || !Enum.IsDefined(kind)) throw new ArgumentException("완료할 운송과 단계를 확인해 주세요.");
        return $"cargo-driver.completion.{(kind == DriverTransportCompletionPhotoKind.Pickup ? "pickup" : "dropoff")}.{transportId.ToString(CultureInfo.InvariantCulture)}.v1";
    }

    private static void Validate(WorkflowRecoveryRecord record, WorkflowRecoverySnapshot snapshot, string scope, long transportId)
    {
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != snapshot.Owner || record.Scope != scope
            || record.Target != transportId.ToString(CultureInfo.InvariantCulture))
            throw new InvalidDataException("이전 완료 요청의 계정과 운송을 확인하지 못했습니다.");
    }
}

/// <summary>페이지 한 인스턴스의 복구 확인 수명입니다. 새 명령은 저장 확인 이후에만 허용합니다.</summary>
internal sealed class DriverCargoCompletionRecoverySession(IDriverCargoCompletionRecovery? recovery,
    DriverTransportCompletionPhotoKind kind)
{
    private long _generation;
    private long _transportId;
    private WorkflowRecoverySnapshot? _snapshot;
    private Guid? _submittedRecordId;
    public bool Enabled => recovery is not null;
    public bool Checked { get; private set; } = recovery is null;
    public WorkflowRecoveryRecord? Pending { get; private set; }
    public bool CanSubmit => !Enabled || Checked && Pending is null;
    public bool ScopeChanged { get; private set; }

    public void Reset(long transportId)
    {
        _generation++; _transportId = transportId; _snapshot = null;
        Pending = null; Checked = !Enabled;
        _submittedRecordId = null;
        ScopeChanged = false;
    }

    public WorkflowRecoverySnapshot? StartQuery()
    {
        Checked = !Enabled;
        var snapshot = recovery?.Capture();
        ScopeChanged = _snapshot is not null && _snapshot != snapshot;
        if (ScopeChanged) { _generation++; _snapshot = null; Pending = null; _submittedRecordId = null; }
        return snapshot;
    }

    public void EnsureCurrent(WorkflowRecoverySnapshot? snapshot)
    {
        if (recovery is not null && (snapshot is null || !recovery.IsCurrent(snapshot)))
            throw new OperationCanceledException("계정이나 서버가 변경되어 이전 결과를 적용하지 않았습니다.");
    }

    public async Task ObserveAsync(WorkflowRecoverySnapshot? snapshot, bool canonicalComplete,
        bool confirmedRejection, CancellationToken cancellationToken)
    {
        if (recovery is null) return;
        var generation = _generation; var id = _transportId;
        var rejectedRecordId = _submittedRecordId;
        var rejectionMatchesPending = confirmedRejection && Pending?.RequestId == rejectedRecordId && rejectedRecordId is not null;
        EnsureCurrent(snapshot);
        var record = await recovery.LoadAsync(snapshot!, kind, id, cancellationToken);
        RequireCurrent(generation, snapshot, cancellationToken);
        if (record is null && Pending is not null && !canonicalComplete && !rejectionMatchesPending)
            throw new InvalidDataException("이전 완료 요청의 보호 기록을 확인하지 못했습니다.");
        Pending = record;
        if (record is not null && (canonicalComplete || confirmedRejection && record.RequestId == rejectedRecordId))
        {
            await recovery.ClearAsync(snapshot!, kind, id, record.RequestId, cancellationToken);
            RequireCurrent(generation, snapshot, cancellationToken);
            Pending = null;
            _submittedRecordId = null;
        }
        _snapshot = snapshot;
        Checked = true;
    }

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (recovery is null) return;
        if (!CanSubmit || _snapshot is null) throw new InvalidOperationException("이전 완료 요청을 먼저 확인해 주세요.");
        var generation = _generation; var snapshot = _snapshot; var id = _transportId;
        Checked = false;
        var localRecord = new WorkflowRecoveryRecord(snapshot.Owner,
            snapshot.Scope(DriverCargoCompletionRecovery.LogicalScope(kind, id)), Guid.NewGuid(), null,
            DateTime.UtcNow, id.ToString(CultureInfo.InvariantCulture));
        Pending = localRecord;
        _submittedRecordId = localRecord.RequestId;
        var record = await recovery.BeginAsync(snapshot, kind, id, localRecord.RequestId, cancellationToken);
        RequireCurrent(generation, snapshot, cancellationToken);
        Pending = record; Checked = true;
    }

    private void RequireCurrent(long generation, WorkflowRecoverySnapshot? snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); EnsureCurrent(snapshot);
        if (generation != _generation) throw new OperationCanceledException("선택한 운송이 변경되었습니다.", cancellationToken);
    }
}
