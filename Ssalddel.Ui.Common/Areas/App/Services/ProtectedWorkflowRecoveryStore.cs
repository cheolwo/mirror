using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>플랫폼 보호 IO를 위임받아 계정·업무별 원 요청과 최초 보관 기한을 지킵니다.</summary>
public sealed class ProtectedWorkflowRecoveryStore(
    Func<string, RoleWorkspaceIdentity> identity,
    Func<string, Task<string?>> read,
    Func<string, string, Task> write,
    Action<string> remove,
    TimeProvider? time = null) : IWorkflowRecoveryStore
{
    // 서로 다른 UI scope의 보호 저장소도 동일 단말 키를 동시에 덮지 않습니다.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public async Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct)
    {
        var expected = RequireOwner(owner, scope, ct); await Gate.WaitAsync(ct);
        try
        {
            RequireOwner(owner, scope, ct, expected);
            var record = await ReadAsync(owner, scope); RequireOwner(owner, scope, ct, expected);
            if (record is null) return null;
            var current = WorkflowRecoveryPolicy.Expire(record, (time ?? TimeProvider.System).GetUtcNow().UtcDateTime);
            if (current != record)
            {
                await write(Key(owner, scope), JsonSerializer.Serialize(current)); RequireOwner(owner, scope, ct, expected);
            }
            return current;
        }
        finally { Gate.Release(); }
    }
    public async Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
    {
        var expected = RequireOwner(record.Owner, record.Scope, ct); WorkflowRecoveryPolicy.Validate(record); await Gate.WaitAsync(ct);
        try
        {
            RequireOwner(record.Owner, record.Scope, ct, expected);
            var old = await ReadAsync(record.Owner, record.Scope); RequireOwner(record.Owner, record.Scope, ct, expected);
            if (old is not null && old.RequestId != record.RequestId) throw new InvalidOperationException("이전에 보낸 요청을 먼저 확인해 주세요.");
            if (old is not null) record = record with { CreatedAtUtc = old.CreatedAtUtc,
                PayloadJson = old.PayloadJson is null ? null : record.PayloadJson };
            await write(Key(record.Owner, record.Scope), JsonSerializer.Serialize(
                WorkflowRecoveryPolicy.Expire(record, (time ?? TimeProvider.System).GetUtcNow().UtcDateTime)));
            RequireOwner(record.Owner, record.Scope, ct, expected);
        }
        finally { Gate.Release(); }
    }
    public async Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct)
    {
        var expected = RequireOwner(owner, scope, ct); await Gate.WaitAsync(ct);
        try
        {
            RequireOwner(owner, scope, ct, expected);
            var old = await ReadAsync(owner, scope); RequireOwner(owner, scope, ct, expected);
            if (old?.RequestId == expectedRequestId) remove(Key(owner, scope));
            RequireOwner(owner, scope, ct, expected);
        }
        finally { Gate.Release(); }
    }
    private async Task<WorkflowRecoveryRecord?> ReadAsync(string owner, string scope)
    {
        var json = await read(Key(owner, scope)); if (string.IsNullOrWhiteSpace(json)) return null;
        var record = JsonSerializer.Deserialize<WorkflowRecoveryRecord>(json) ?? throw new InvalidDataException("이전 요청을 확인하지 못했습니다.");
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != owner || record.Scope != scope) throw new InvalidDataException("이전 요청의 계정과 업무가 다릅니다.");
        return record;
    }
    private RoleWorkspaceIdentity RequireOwner(string owner, string scope, CancellationToken ct, RoleWorkspaceIdentity? expected = null)
    {
        ct.ThrowIfCancellationRequested();
        var current = identity(scope);
        if (string.IsNullOrWhiteSpace(owner) || !current.IsAuthenticated || current.OwnerId != owner
            || expected is not null && current != expected)
            throw new OperationCanceledException("현재 로그인한 계정의 요청만 확인할 수 있습니다.", ct);
        return current;
    }
    private static string Key(string owner, string scope) => "ssalddel.workflow.pending.v1."
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner + "\n" + scope))).ToLowerInvariant();
}
