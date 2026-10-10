using System.Text.Json;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>계정·업무별 미확정 요청입니다. 만료 뒤에는 비공개 원문을 지우고 결과 조회용 번호만 남깁니다.</summary>
public sealed record WorkflowRecoveryRecord(string Owner, string Scope, Guid RequestId,
    string? PayloadJson, DateTime CreatedAtUtc, string? Target = null);

public interface IWorkflowRecoveryStore
{
    Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct);
    Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct);
    Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct);
}

public static class WorkflowRecoveryPolicy
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(72);
    public static WorkflowRecoveryRecord Expire(WorkflowRecoveryRecord record, DateTime now)
        => now - record.CreatedAtUtc >= Retention ? record with { PayloadJson = null } : record;
    public static void Validate(WorkflowRecoveryRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Owner) || record.Owner.Length > 200
            || string.IsNullOrWhiteSpace(record.Scope) || record.Scope.Length > 120
            || record.RequestId == Guid.Empty || record.CreatedAtUtc.Kind != DateTimeKind.Utc
            || record.Target?.Length > 200 || record.PayloadJson?.Length > 131072)
            throw new InvalidDataException("이전 요청의 계정과 업무 정보를 확인하지 못했습니다.");
        if (record.PayloadJson is null) return;
        using var json = JsonDocument.Parse(record.PayloadJson);
        if (HasPhoto(json.RootElement)) throw new InvalidDataException("사진 원본은 요청 복구 기록에 보관할 수 없습니다.");
    }
    private static bool HasPhoto(JsonElement value)
        => value.ValueKind == JsonValueKind.Object
            ? value.EnumerateObject().Any(p => p.Name.Contains("base64", StringComparison.OrdinalIgnoreCase)
                || p.Name.Equals("Bytes", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind is JsonValueKind.Array or JsonValueKind.String
                || HasPhoto(p.Value))
            : value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(HasPhoto);
}

/// <summary>Web 호스트의 같은 scope 재진입용입니다. 앱 재시작 보존은 플랫폼 보호 저장 구현이 담당합니다.</summary>
public sealed class VolatileWorkflowRecoveryStore(TimeProvider? time = null) : IWorkflowRecoveryStore
{
    private readonly Dictionary<(string Owner, string Scope), WorkflowRecoveryRecord> records = [];
    public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (records)
        {
            if (!records.TryGetValue((owner, scope), out var record)) return Task.FromResult<WorkflowRecoveryRecord?>(null);
            record = WorkflowRecoveryPolicy.Expire(record, (time ?? TimeProvider.System).GetUtcNow().UtcDateTime);
            records[(owner, scope)] = record; return Task.FromResult<WorkflowRecoveryRecord?>(record);
        }
    }
    public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); WorkflowRecoveryPolicy.Validate(record);
        lock (records)
        {
            if (records.TryGetValue((record.Owner, record.Scope), out var old) && old.RequestId != record.RequestId)
                throw new InvalidOperationException("이전에 보낸 요청 결과를 먼저 확인해 주세요.");
            if (old is not null) record = record with { CreatedAtUtc = old.CreatedAtUtc,
                PayloadJson = old.PayloadJson is null ? null : record.PayloadJson };
            records[(record.Owner, record.Scope)] = WorkflowRecoveryPolicy.Expire(record, (time ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        }
        return Task.CompletedTask;
    }
    public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (records) if (records.GetValueOrDefault((owner, scope))?.RequestId == expectedRequestId) records.Remove((owner, scope));
        return Task.CompletedTask;
    }
}
