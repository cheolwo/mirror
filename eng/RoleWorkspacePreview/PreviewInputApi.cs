using Microsoft.AspNetCore.Components;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

// 독립 업무 화면도 같은 역할 DTO 예시를 읽습니다. 실제 업무 저장/외부 요청은 차단합니다.
public sealed class PreviewInputApi(PreviewActionApi actions, IRoleWorkspaceAccess access, NavigationManager navigation) : IRoleWorkspaceApi
{
    private readonly Dictionary<string, PreviewRoleApi> roles = new(StringComparer.Ordinal);
    public PreviewRoleApi For(string role) => roles.TryGetValue(role, out var api) ? api : roles[role] = new(role, access, navigation);
    private static bool ActionSample(string role, string path) => role == "orderer" && path.StartsWith("api/v1/food-orders/order-preview", StringComparison.Ordinal);
    public Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (PreviewHandoffFixtures.Read(role, path, access, navigation.Uri) is { } fixture) return Task.FromResult((T)fixture);
        return ActionSample(role, path) ? actions.GetAsync<T>(role, path, ct) : For(role).GetAsync<T>(role, path, ct);
    }
    public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct) => ActionSample(role, path)
        ? actions.PostAsync<T>(role, path, body, ct) : For(role).PostAsync<T>(role, path, body, ct);
    public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct)
        => throw new InvalidOperationException("미리보기에서는 업무를 저장하지 않습니다.");
    public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct)
        => throw new InvalidOperationException("미리보기에서는 증빙을 업로드하지 않습니다.");
}
