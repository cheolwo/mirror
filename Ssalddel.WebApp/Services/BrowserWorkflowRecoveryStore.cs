using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.WebApp.Services;

/// <summary>브라우저 재실행에도 남는 암호화 기록. 현재 계정·서버·세션만 접근하며 요청을 실행하지 않습니다.</summary>
public sealed class BrowserWorkflowRecoveryStore(IJSRuntime js, IWorkflowRecoveryContext context,
    IRoleWorkspaceAccess? access = null, HttpClient? http = null) : IWorkflowRecoveryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // 서버 문맥이 없는 기존 업무는 이번 변경으로 저장 범위를 추측하지 않습니다.
    private readonly VolatileWorkflowRecoveryStore legacy = new();

    public async Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct)
    {
        if (!scope.Contains(".server-", StringComparison.Ordinal)) return await legacy.LoadAsync(owner, scope, ct);
        var identity = RequireCurrent(owner, scope, ct);
        await using var module = await ModuleAsync(ct);
        RequireCurrent(owner, scope, ct, identity);
        var json = await module.InvokeAsync<string?>("read", ct, Key(owner, scope), DateTime.UtcNow);
        RequireCurrent(owner, scope, ct, identity);
        return Parse(json, owner, scope);
    }

    public async Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
    {
        WorkflowRecoveryPolicy.Validate(record);
        if (!record.Scope.Contains(".server-", StringComparison.Ordinal))
        { await legacy.SaveAsync(record, ct); return; }
        var identity = RequireCurrent(record.Owner, record.Scope, ct);
        await using var module = await ModuleAsync(ct);
        RequireCurrent(record.Owner, record.Scope, ct, identity);
        var json = await module.InvokeAsync<string>("save", ct, Key(record.Owner, record.Scope),
            JsonSerializer.Serialize(record, Json), DateTime.UtcNow);
        RequireCurrent(record.Owner, record.Scope, ct, identity);
        var saved = Parse(json, record.Owner, record.Scope);
        if (saved?.RequestId != record.RequestId) throw new InvalidDataException("이전 요청의 확인 기록이 다릅니다.");
    }

    public async Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct)
    {
        if (!scope.Contains(".server-", StringComparison.Ordinal))
        { await legacy.RemoveAsync(owner, scope, expectedRequestId, ct); return; }
        var identity = RequireCurrent(owner, scope, ct);
        await using var module = await ModuleAsync(ct);
        RequireCurrent(owner, scope, ct, identity);
        await module.InvokeVoidAsync("remove", ct, Key(owner, scope), expectedRequestId.ToString());
        RequireCurrent(owner, scope, ct, identity);
    }

    private ValueTask<IJSObjectReference> ModuleAsync(CancellationToken ct)
        => js.InvokeAsync<IJSObjectReference>("import", ct, "./js/workflow-recovery.js");

    private WorkflowRecoverySnapshot RequireCurrent(string owner, string scope, CancellationToken ct,
        WorkflowRecoverySnapshot? expected = null)
    {
        ct.ThrowIfCancellationRequested();
        var role = scope.StartsWith("cargo.cargo-driver.", StringComparison.Ordinal) ? RoleWorkspaceCatalog.CargoDriver
            : scope.StartsWith("distributed.shipper.prepayment.server-", StringComparison.Ordinal) ? RoleWorkspaceCatalog.Shipper : null;
        var current = role is not null && access is not null && http is not null
            ? new WorkflowRecoveryContext(() => access.GetIdentity(role), () => http.BaseAddress).Capture()
            : context.Capture();
        if (current.Owner != owner || expected is not null && current != expected
            || !scope.EndsWith(".server-" + current.EndpointKey, StringComparison.Ordinal))
            throw new OperationCanceledException("현재 로그인한 계정과 서버의 요청만 확인할 수 있습니다.", ct);
        return current;
    }

    private static WorkflowRecoveryRecord? Parse(string? json, string owner, string scope)
    {
        if (json is null) return null;
        var record = JsonSerializer.Deserialize<WorkflowRecoveryRecord>(json, Json)
            ?? throw new InvalidDataException("이전 요청을 읽지 못했습니다.");
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != owner || record.Scope != scope)
            throw new InvalidDataException("이전 요청의 계정과 서버가 다릅니다.");
        return record;
    }

    private static string Key(string owner, string scope) => "workflow-v1-"
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner + "\n" + scope))).ToLowerInvariant();
}
