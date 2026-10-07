using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace FDriverApp.Services;

public interface IFDriverAuthSession : ISsalddelAccessTokenProvider
{
    event EventHandler? SessionChanged;
    DateTime AccessTokenExpiresAtUtc { get; }
    string? RefreshToken { get; }
    DateTime RefreshTokenExpiresAtUtc { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    ClientAuthSessionRestoreState CurrentState { get; }
    long SessionRevision => 0;
    Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken cancellationToken = default);
    Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    async Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedRevision, CancellationToken cancellationToken = default)
    {
        if (SessionRevision != expectedRevision) return false;
        await ApplyAsync(snapshot, cancellationToken); return true;
    }
    async Task<bool> TryClearAsync(long expectedRevision, CancellationToken cancellationToken = default)
    {
        if (SessionRevision != expectedRevision) return false;
        await ClearAsync(cancellationToken); return true;
    }
}

public sealed class FDriverAuthSession : IFDriverAuthSession
{
    public event EventHandler? SessionChanged;
    private const string StorageKey = "ssalddel.fdriver.authToken.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IClientSessionGuard _sessionGuard;
    private readonly IClientSecureTokenStore _tokenStore;
    private readonly SemaphoreSlim _restoreGate = new(1, 1);
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private bool _restored;
    private long _version;

    public FDriverAuthSession(IClientSessionGuard sessionGuard, IClientSecureTokenStore? tokenStore = null)
    {
        _sessionGuard = sessionGuard;
        _tokenStore = tokenStore ?? new DeviceTokenStore();
    }

    public string? AccessToken { get; private set; }
    public string? AuthenticationOwnerId => UserId;
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime RefreshTokenExpiresAtUtc { get; private set; }
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public ClientAuthSessionRestoreState CurrentState
    {
        get
        {
            var snapshot = CreateSnapshot();
            if (_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow))
            {
                return ClientAuthSessionRestoreState.Authenticated;
            }

            return _sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow)
                ? ClientAuthSessionRestoreState.RefreshRequired
                : ClientAuthSessionRestoreState.Anonymous;
        }
    }
    public bool IsAuthenticated => CurrentState == ClientAuthSessionRestoreState.Authenticated;
    public long SessionRevision => Interlocked.Read(ref _version);

    public async Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await _restoreGate.WaitAsync(cancellationToken);
        try
        {
            if (_restored) return CurrentState;
            var version = Interlocked.Read(ref _version);
            ClientAuthTokenSnapshot? snapshot;
            try { snapshot = await _tokenStore.LoadAsync(cancellationToken); }
            catch (Exception ex) when (ex is not OperationCanceledException
                && (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsWindows()))
            {
                snapshot = null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            await _mutationGate.WaitAsync(cancellationToken);
            try
            {
                // A new login or logout owns the session even while device restore is delayed.
                if (_restored || version != Interlocked.Read(ref _version)) return CurrentState;
                _restored = true;
                if (_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
                    || _sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow))
                    ApplySnapshot(snapshot!);
                else
                {
                    ClearSnapshot();
                    await _tokenStore.ClearAsync(cancellationToken);
                }
                return CurrentState;
            }
            finally { _mutationGate.Release(); }
        }
        finally { _restoreGate.Release(); }
    }

    public async Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        => await ApplyCoreAsync(snapshot, null, cancellationToken);

    public Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedRevision, CancellationToken cancellationToken = default)
        => ApplyCoreAsync(snapshot, expectedRevision, cancellationToken);

    private async Task<bool> ApplyCoreAsync(ClientAuthTokenSnapshot snapshot, long? expectedRevision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expectedRevision is { } expected && expected != SessionRevision) return false;
            _restored = true;
            ApplySnapshot(snapshot);
            await _tokenStore.SaveAsync(snapshot, cancellationToken);
            return true;
        }
        finally { _mutationGate.Release(); }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
        => await ClearCoreAsync(null, cancellationToken);

    public Task<bool> TryClearAsync(long expectedRevision, CancellationToken cancellationToken = default)
        => ClearCoreAsync(expectedRevision, cancellationToken);

    private async Task<bool> ClearCoreAsync(long? expectedRevision, CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expectedRevision is { } expected && expected != SessionRevision) return false;
            _restored = true;
            ClearSnapshot();
            await _tokenStore.ClearAsync(cancellationToken);
            return true;
        }
        finally { _mutationGate.Release(); }
    }

    private void ClearSnapshot()
    {
        AccessToken = null;
        AccessTokenExpiresAtUtc = default;
        RefreshToken = null;
        RefreshTokenExpiresAtUtc = default;
        UserId = null;
        UserName = null;
        Roles = [];
        Interlocked.Increment(ref _version);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplySnapshot(ClientAuthTokenSnapshot snapshot)
    {
        AccessToken = snapshot.AccessToken;
        AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtc;
        RefreshToken = snapshot.RefreshToken;
        RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtc;
        UserId = snapshot.UserId;
        UserName = snapshot.UserName;
        Roles = snapshot.Roles.ToArray();
        Interlocked.Increment(ref _version);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class DeviceTokenStore : IClientSecureTokenStore
    {
        public async Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        {
            var json = await SecureStorage.Default.GetAsync(StorageKey);
            cancellationToken.ThrowIfCancellationRequested();
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ClientAuthTokenSnapshot>(json, JsonOptions);
        }
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecureStorage.Default.Remove(StorageKey);
            return Task.CompletedTask;
        }
    }

    private ClientAuthTokenSnapshot? CreateSnapshot()
        => string.IsNullOrWhiteSpace(UserId)
            ? null
            : new ClientAuthTokenSnapshot(
                AccessToken ?? string.Empty,
                AccessTokenExpiresAtUtc,
                RefreshToken ?? string.Empty,
                RefreshTokenExpiresAtUtc,
                UserId,
                UserName ?? string.Empty,
                Roles);
}
