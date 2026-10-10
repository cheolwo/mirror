using Microsoft.Maui.Storage;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Client.Infrastructure.Native;

/// <summary>역할 호스트의 실제 인증 판본과 단말 보호 저장소를 연결합니다.</summary>
public sealed class MauiRoleWorkflowRecoveryStore : IWorkflowRecoveryStore
{
    private readonly ProtectedWorkflowRecoveryStore store;
    public MauiRoleWorkflowRecoveryStore(IRoleWorkspaceAccess access, string defaultRole)
        : this(scope => access.GetIdentity(ScopeRole(scope, defaultRole))) { }

    /// <summary>전문 앱의 기존 세션을 그대로 사용합니다. 다른 역할의 인증을 만들어내지 않습니다.</summary>
    public MauiRoleWorkflowRecoveryStore(Func<RoleWorkspaceIdentity> identity, string defaultRole)
        : this(scope => ScopeRole(scope, defaultRole) == defaultRole ? identity()
            : throw new InvalidOperationException("이 앱에서 사용하는 역할의 요청만 확인할 수 있습니다.")) { }

    private MauiRoleWorkflowRecoveryStore(Func<string, RoleWorkspaceIdentity> identity)
        => store = new(identity, SecureStorage.Default.GetAsync, SecureStorage.Default.SetAsync,
            key => SecureStorage.Default.Remove(key));
    public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => store.LoadAsync(owner, scope, ct);
    public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) => store.SaveAsync(record, ct);
    public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct) => store.RemoveAsync(owner, scope, expectedRequestId, ct);

    private static string ScopeRole(string scope, string defaultRole)
    {
        if (!scope.StartsWith("distributed.", StringComparison.Ordinal)) return defaultRole;
        var parts = scope.Split('.');
        if (parts.Length != 4 || parts[3] != "v1"
            || parts[1] is not ("warehouse" or "shipper" or "cargo-driver" or "orderer" or "operator"))
            throw new InvalidDataException("이전 요청의 역할과 업무를 확인하지 못했습니다.");
        return parts[1];
    }
}
