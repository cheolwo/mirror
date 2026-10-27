using System.Globalization;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace DriverApp.Services;

public interface IDriverCargoExceptionRecovery
{
    WorkflowRecoverySnapshot Capture();
    bool IsCurrent(WorkflowRecoverySnapshot snapshot);
    Task<WorkflowRecoveryRecord?> LoadAsync(WorkflowRecoverySnapshot snapshot, string stage, long transportId,
        CancellationToken cancellationToken = default);
    Task<WorkflowRecoveryRecord> BeginAsync(WorkflowRecoverySnapshot snapshot, string stage, long transportId,
        Guid localRecordId, CancellationToken cancellationToken = default);
    Task ClearAsync(WorkflowRecoverySnapshot snapshot, string stage, long transportId, Guid expectedRecordId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 신고 원문 없이 계정·서버·운송·단계별 미확정 관측 번호만 보호 보관합니다.
/// 관측 번호는 서버 영수증이 아니므로 재실행 후 이전 신고 존재만으로 성공·미적용을 판정하지 않습니다.
/// </summary>
public sealed class DriverCargoExceptionRecovery(IWorkflowRecoveryStore store, IWorkflowRecoveryContext context)
    : IDriverCargoExceptionRecovery
{
    public WorkflowRecoverySnapshot Capture() => context.Capture();
    public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => context.IsCurrent(snapshot);

    public async Task<WorkflowRecoveryRecord?> LoadAsync(WorkflowRecoverySnapshot snapshot, string stage, long transportId,
        CancellationToken cancellationToken = default)
    {
        RequireCurrent(snapshot, cancellationToken);
        var scope = Scope(snapshot, stage, transportId);
        var record = await store.LoadAsync(snapshot.Owner, scope, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        if (record is not null)
        {
            WorkflowRecoveryPolicy.Validate(record);
            if (record.Owner != snapshot.Owner || record.Scope != scope || record.PayloadJson is not null
                || record.Target != transportId.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("이전 신고 기록의 계정과 대상을 확인하지 못했습니다.");
        }
        return record;
    }

    public async Task<WorkflowRecoveryRecord> BeginAsync(WorkflowRecoverySnapshot snapshot, string stage, long transportId,
        Guid localRecordId, CancellationToken cancellationToken = default)
    {
        if (localRecordId == Guid.Empty) throw new ArgumentException("신고 관측 번호가 필요합니다.", nameof(localRecordId));
        if (await LoadAsync(snapshot, stage, transportId, cancellationToken) is not null)
            throw new InvalidOperationException("이전 신고 결과를 먼저 확인해 주세요.");
        RequireCurrent(snapshot, cancellationToken);
        var record = new WorkflowRecoveryRecord(snapshot.Owner, Scope(snapshot, stage, transportId), localRecordId,
            null, DateTime.UtcNow, transportId.ToString(CultureInfo.InvariantCulture));
        await store.SaveAsync(record, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        var persisted = await LoadAsync(snapshot, stage, transportId, cancellationToken);
        if (persisted?.RequestId != record.RequestId)
            throw new IOException("신고 보호 보관을 확인하지 못했습니다. 신고를 전송하지 않았습니다.");
        return persisted;
    }

    public async Task ClearAsync(WorkflowRecoverySnapshot snapshot, string stage, long transportId, Guid expectedRecordId,
        CancellationToken cancellationToken = default)
    {
        if (expectedRecordId == Guid.Empty) throw new ArgumentException("확인할 신고 관측 번호가 필요합니다.", nameof(expectedRecordId));
        RequireCurrent(snapshot, cancellationToken);
        await store.RemoveAsync(snapshot.Owner, Scope(snapshot, stage, transportId), expectedRecordId, cancellationToken);
        RequireCurrent(snapshot, cancellationToken);
        if (await LoadAsync(snapshot, stage, transportId, cancellationToken) is not null)
            throw new IOException("신고 보호 기록 정리를 확인하지 못했습니다.");
    }

    private void RequireCurrent(WorkflowRecoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.IsCurrent(snapshot)) throw new OperationCanceledException("현재 계정과 서버의 신고만 확인할 수 있습니다.", cancellationToken);
    }

    private static string Scope(WorkflowRecoverySnapshot snapshot, string stage, long transportId)
    {
        if (transportId <= 0 || stage is not ("상차" or "하차")) throw new ArgumentException("신고할 운송과 단계를 확인해 주세요.");
        return snapshot.Scope($"cargo-driver.exception.{(stage == "상차" ? "pickup" : "dropoff")}.{transportId.ToString(CultureInfo.InvariantCulture)}.v1");
    }
}
