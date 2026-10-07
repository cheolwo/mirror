using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.RoleWorkspace;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.WebApp.Services;

public sealed class WebRoleWorkspacePrimaryAuth : IRoleWorkspacePrimaryAuth, IDisposable
{
    private readonly WebAuthSessionService _auth;
    private long _revision;
    private bool _restored;
    public event Action? Changed;
    public WebRoleWorkspacePrimaryAuth(WebAuthSessionService auth) { _auth = auth; _auth.Changed += OnChanged; }
    public RoleWorkspaceIdentity Identity => new(_auth.UserId, _revision, _auth.IsLoggedIn);
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_restored) return;
        _restored = true;
        try { await _auth.RestoreAsync(ct); }
        catch { _restored = false; throw; }
    }
    public Task SignInAsync(string name, string password, CancellationToken ct) => _auth.LoginAsync(name, password, ct);
    public Task SignOutAsync(CancellationToken ct) => _auth.ClearAsync(ct);
    public Task<string?> GetTokenAsync(CancellationToken ct) => ((Ssalddel.Ui.Common.Areas.App.Services.ISsalddelAccessTokenProvider)_auth).GetAccessTokenAsync(ct);
    private void OnChanged() { Interlocked.Increment(ref _revision); Changed?.Invoke(); }
    public void Dispose() => _auth.Changed -= OnChanged;
}
public sealed class WebRoleWorkspaceTokenStoreFactory(IJSRuntime js) : IRoleWorkspaceTokenStoreFactory
{
    public IClientSecureTokenStore Create(string roleKey) => new Store(js, "ssalddel.role.auth.v1." + roleKey);
    private sealed class Store(IJSRuntime js, string key) : IClientSecureTokenStore
    {
        public async Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken ct = default)
        {
            var json = await js.InvokeAsync<string?>("sessionStorage.getItem", ct, key);
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return System.Text.Json.JsonSerializer.Deserialize<ClientAuthTokenSnapshot>(json); }
            catch (System.Text.Json.JsonException) { await ClearAsync(ct); return null; }
        }
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken ct = default)
            => js.InvokeVoidAsync("sessionStorage.setItem", ct, key, System.Text.Json.JsonSerializer.Serialize(value)).AsTask();
        public Task ClearAsync(CancellationToken ct = default) => js.InvokeVoidAsync("sessionStorage.removeItem", ct, key).AsTask();
    }
}
public sealed class WebRoleWorkspaceLocationProvider(IJSRuntime js) : IRoleWorkspaceLocationProvider
{
    public async Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", ct, "./js/role-workspace-location.js");
        return await module.InvokeAsync<RoleWorkspaceLocation?>("current", ct);
    }
}
