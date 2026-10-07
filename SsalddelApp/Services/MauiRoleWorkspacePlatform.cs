using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.RoleWorkspace;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace SsalddelApp.Services;

public sealed class MauiRoleWorkspacePrimaryAuth : IRoleWorkspacePrimaryAuth, IDisposable
{
    private readonly IAuthSession _session;
    private readonly AuthApiService _auth;
    public event Action? Changed;
    public MauiRoleWorkspacePrimaryAuth(IAuthSession session, AuthApiService auth) { _session = session; _auth = auth; _session.Changed += OnChanged; }
    public RoleWorkspaceIdentity Identity => new(_session.UserId, _session.SessionRevision, _session.IsLoggedIn);
    public Task InitializeAsync(CancellationToken ct) => _session.RestoreAsync(ct);
    public async Task SignInAsync(string name, string password, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(name, password, ct);
        if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage);
    }
    public Task SignOutAsync(CancellationToken ct) => _auth.LogoutAsync(ct);
    public async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (!_session.IsLoggedIn) return null;
        var error = await _auth.EnsureAccessTokenAsync(cancellationToken: ct);
        if (error is not null) throw new InvalidOperationException(error);
        return _session.AccessToken;
    }
    private void OnChanged() => Changed?.Invoke();
    public void Dispose() => _session.Changed -= OnChanged;
}
public sealed class MauiRoleWorkspaceTokenStoreFactory : IRoleWorkspaceTokenStoreFactory
{
    public IClientSecureTokenStore Create(string roleKey) => new Store("ssalddel.role.auth.v1." + roleKey);
    private sealed class Store(string key) : IClientSecureTokenStore
    {
        public async Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var json = await SecureStorage.Default.GetAsync(key);
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return System.Text.Json.JsonSerializer.Deserialize<ClientAuthTokenSnapshot>(json); }
            catch (System.Text.Json.JsonException) { await ClearAsync(ct); return null; }
        }
        public async Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); await SecureStorage.Default.SetAsync(key, System.Text.Json.JsonSerializer.Serialize(value)); }
        public Task ClearAsync(CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); SecureStorage.Default.Remove(key); return Task.CompletedTask; }
    }
}
public sealed class MauiRoleWorkspaceLocationProvider : IRoleWorkspaceLocationProvider
{
    public async Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            // 현재 위치 갱신·운행 시작 등 사용자의 행동에서만 앱 사용 중 권한을 요청합니다.
            var permission = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                ct.ThrowIfCancellationRequested();
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                ct.ThrowIfCancellationRequested();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                ct.ThrowIfCancellationRequested();
                return status;
            });
            ct.ThrowIfCancellationRequested();
            if (permission != PermissionStatus.Granted) return null;

            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(12)), ct);
            ct.ThrowIfCancellationRequested();
            if (location is null || location.IsFromMockProvider) return null;
            return new(location.Latitude, location.Longitude, location.Accuracy, location.Timestamp);
        }
        catch (PermissionException) { ct.ThrowIfCancellationRequested(); return null; }
        catch (FeatureNotEnabledException) { ct.ThrowIfCancellationRequested(); return null; }
        catch (FeatureNotSupportedException) { ct.ThrowIfCancellationRequested(); return null; }
    }
}
