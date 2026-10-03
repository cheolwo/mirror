using Ssalddel.Client.Infrastructure.Security;

namespace SsalddelApp.Services;

public sealed class AuthSession : IAuthSession
{
    private readonly IClientSecureTokenStore _tokenStore;
    private readonly IClientSessionGuard _sessionGuard;
    private readonly object _mutationGate = new();
    private readonly SemaphoreSlim _storageGate = new(1, 1);
    private bool _restored;
    private long _sessionRevision;
    private long _storageRevision;
    private Task? _restoreTask;

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
    public string? UserName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken);
    public long SessionRevision => Interlocked.Read(ref _sessionRevision);
    public event Action? Changed;

    public Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        lock (_mutationGate)
        {
            if (_restoreTask is { IsCompleted: false }) return _restoreTask.WaitAsync(cancellationToken);
            if (_restored) return Task.CompletedTask;
            return _restoreTask = RestoreCoreAsync(cancellationToken);
        }
    }

    private async Task RestoreCoreAsync(CancellationToken cancellationToken)
    {
        _restored = true;
        var revision = Interlocked.Read(ref _storageRevision);
        ClientAuthTokenSnapshot? snapshot;
        try
        {
            snapshot = await _tokenStore.LoadAsync(cancellationToken);
        }
        catch
        {
            lock (_mutationGate)
            {
                if (revision == _storageRevision) _restored = false;
            }
            throw;
        }

        bool clear;
        long storageRevision;
        lock (_mutationGate)
        {
            if (revision != _storageRevision) return;
            clear = !_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
                && !_sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow);
            if (clear) ClearSnapshot();
            else ApplySnapshot(snapshot!, startsNewSession: true);
            storageRevision = ++_storageRevision;
        }
        Changed?.Invoke();
        if (clear) await PersistAsync(null, storageRevision, cancellationToken);
    }

    public async Task ApplyAsync(
        ClientAuthTokenSnapshot snapshot,
        CancellationToken cancellationToken = default,
        bool startsNewSession = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        long storageRevision;
        lock (_mutationGate)
        {
            _restored = true;
            ApplySnapshot(snapshot, startsNewSession);
            storageRevision = ++_storageRevision;
        }
        Changed?.Invoke();
        await PersistAsync(snapshot, storageRevision, cancellationToken);
    }

    public async Task<bool> TryRefreshAsync(
        ClientAuthTokenSnapshot snapshot,
        long expectedRevision,
        string? expectedRefreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        long storageRevision;
        lock (_mutationGate)
        {
            if (!MatchesSession(expectedRevision, expectedRefreshToken)
                || !string.Equals(UserId, snapshot.UserId, StringComparison.Ordinal))
                return false;
            _restored = true;
            ApplySnapshot(snapshot, startsNewSession: false);
            storageRevision = ++_storageRevision;
        }
        Changed?.Invoke();
        await PersistAsync(snapshot, storageRevision, cancellationToken);
        return true;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        long storageRevision;
        lock (_mutationGate)
        {
            _restored = true;
            ClearSnapshot();
            storageRevision = ++_storageRevision;
        }
        Changed?.Invoke();
        await PersistAsync(null, storageRevision, cancellationToken);
    }

    public async Task<bool> TryClearAsync(
        long expectedRevision,
        string? expectedRefreshToken,
        CancellationToken cancellationToken = default)
    {
        long storageRevision;
        lock (_mutationGate)
        {
            if (!MatchesSession(expectedRevision, expectedRefreshToken)) return false;
            _restored = true;
            ClearSnapshot();
            storageRevision = ++_storageRevision;
        }
        Changed?.Invoke();
        await PersistAsync(null, storageRevision, cancellationToken);
        return true;
    }

    private bool MatchesSession(long revision, string? refreshToken)
        => _sessionRevision == revision
           && string.Equals(RefreshToken, refreshToken, StringComparison.Ordinal);

    private async Task PersistAsync(ClientAuthTokenSnapshot? snapshot, long revision, CancellationToken cancellationToken)
    {
        await _storageGate.WaitAsync(cancellationToken);
        try
        {
            lock (_mutationGate)
            {
                if (revision != _storageRevision) return;
            }
            // 진행 중 저장 뒤 새 변경의 저장/삭제가 직렬로 이어져 마지막 세션을 보존합니다.
            if (snapshot is null) await _tokenStore.ClearAsync(cancellationToken);
            else await _tokenStore.SaveAsync(snapshot, cancellationToken);
        }
        finally
        {
            _storageGate.Release();
        }
    }

    private void ApplySnapshot(ClientAuthTokenSnapshot snapshot, bool startsNewSession)
    {
        var newSession = startsNewSession || !IsLoggedIn
            || !string.Equals(UserId, snapshot.UserId, StringComparison.Ordinal);
        AccessToken = snapshot.AccessToken;
        RefreshToken = snapshot.RefreshToken;
        AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtc;
        RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtc;
        UserId = snapshot.UserId;
        UserName = snapshot.UserName;
        Roles = snapshot.Roles;
        if (newSession) Interlocked.Increment(ref _sessionRevision);
    }

    private void ClearSnapshot()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = default;
        RefreshTokenExpiresAtUtc = default;
        UserId = null;
        UserName = null;
        Roles = Array.Empty<string>();
        Interlocked.Increment(ref _sessionRevision);
    }
}
