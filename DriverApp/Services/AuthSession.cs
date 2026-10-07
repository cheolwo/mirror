using Ssalddel.Client.Infrastructure.Security;

namespace DriverApp.Services;

public sealed class AuthSession : IAuthSession
{
    private readonly IClientSecureTokenStore _tokenStore;
    private readonly IClientSessionGuard _sessionGuard;
    private bool _restored;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private long _version;
    private long _sessionRevision;

    public AuthSession(IClientSecureTokenStore tokenStore, IClientSessionGuard sessionGuard)
    {
        _tokenStore = tokenStore;
        _sessionGuard = sessionGuard;
    }

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public DateTime RefreshTokenExpiresAtUtc { get; private set; }
    public string? UserId { get; private set; }
    public string? AuthenticationOwnerId => UserId;
    public string? UserName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
    public long Version => Interlocked.Read(ref _version);
    public long SessionRevision => Interlocked.Read(ref _sessionRevision);
    public event Action? Changed;

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (_restored)
                return;

            var snapshot = await _tokenStore.LoadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _restored = true;
            if (!_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
                && !_sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow))
            {
                await ClearCoreAsync(cancellationToken);
                return;
            }

            ApplySnapshot(snapshot!);
            Changed?.Invoke();
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            await ApplyCoreAsync(snapshot, cancellationToken, startsNewSession: true);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            await ClearCoreAsync(cancellationToken);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion, CancellationToken cancellationToken = default, bool startsNewSession = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (Version != expectedVersion)
                return false;
            await ApplyCoreAsync(snapshot, cancellationToken, startsNewSession);
            return true;
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (Version != expectedVersion)
                return false;
            await ClearCoreAsync(cancellationToken);
            return true;
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task ApplyCoreAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken, bool startsNewSession)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _tokenStore.SaveAsync(snapshot, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _restored = true;
        ApplySnapshot(snapshot, startsNewSession);
        Changed?.Invoke();
    }

    private async Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _restored = true;
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = default;
        RefreshTokenExpiresAtUtc = default;
        UserId = null;
        UserName = null;
        Roles = Array.Empty<string>();
        Interlocked.Increment(ref _version);
        Interlocked.Increment(ref _sessionRevision);
        try
        {
            await _tokenStore.ClearAsync(cancellationToken);
        }
        finally
        {
            // Private UI must clear even if device storage removal fails.
            Changed?.Invoke();
        }
    }

    private void ApplySnapshot(ClientAuthTokenSnapshot snapshot, bool startsNewSession = false)
    {
        var sessionChanged = startsNewSession || !string.Equals(UserId, snapshot.UserId, StringComparison.Ordinal);
        AccessToken = snapshot.AccessToken;
        RefreshToken = snapshot.RefreshToken;
        AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtc;
        RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtc;
        UserId = snapshot.UserId;
        UserName = snapshot.UserName;
        Roles = snapshot.Roles;
        Interlocked.Increment(ref _version);
        if (sessionChanged)
            Interlocked.Increment(ref _sessionRevision);
    }
}
