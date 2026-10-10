using Microsoft.Maui.Storage;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelApp.Services;

/// <summary>비공개 요청은 단말 보호 저장소에만 보관합니다. 사진 원본·인증값은 저장하지 않습니다.</summary>
public sealed class MauiWorkflowRecoveryStore : IWorkflowRecoveryStore
{
    private readonly ProtectedWorkflowRecoveryStore inner;
    public MauiWorkflowRecoveryStore(IAuthSession session, Func<string, Task<string?>>? read = null,
        Func<string, string, Task>? write = null, Action<string>? remove = null, TimeProvider? time = null,
        IRoleWorkspaceAccess? access = null)
    {
        RoleWorkspaceIdentity Identity(string scope)
        {
            var parts = scope.Split('.');
            if (access is not null && parts.Length == 4 && parts[0] == "distributed") return access.GetIdentity(parts[1]);
            return new(session.UserId, session.SessionRevision, session.IsLoggedIn);
        }
        inner = new(Identity, read ?? SecureStorage.Default.GetAsync, write ?? SecureStorage.Default.SetAsync,
            remove ?? (key => SecureStorage.Default.Remove(key)), time);
    }
    public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => inner.LoadAsync(owner, scope, ct);
    public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) => inner.SaveAsync(record, ct);
    public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct) => inner.RemoveAsync(owner, scope, expectedRequestId, ct);
}
