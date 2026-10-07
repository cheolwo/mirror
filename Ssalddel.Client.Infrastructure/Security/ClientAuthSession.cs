namespace Ssalddel.Client.Infrastructure.Security;

public enum ClientAuthSessionRestoreState
{
    Anonymous,
    Authenticated,
    RefreshRequired
}

/// <summary>
/// 클라이언트 인증 토큰의 메모리 상태와 보안 저장소 동기화만 담당합니다.
/// 로그인·갱신 HTTP 호출과 앱별 역할 판단은 각 클라이언트에서 처리합니다.
/// </summary>
public sealed class ClientAuthSession
{
    private readonly IClientSecureTokenStore _tokenStore;
    private readonly IClientSessionGuard _sessionGuard;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _storageGate = new(1, 1);
    private readonly object _stateLock = new();
    private long _revision;
    private bool _restored;
    private ClientAuthSessionRestoreState _restoreState = ClientAuthSessionRestoreState.Anonymous;

    public ClientAuthSession(
        IClientSecureTokenStore tokenStore,
        IClientSessionGuard sessionGuard,
        TimeProvider? timeProvider = null)
    {
        _tokenStore = tokenStore;
        _sessionGuard = sessionGuard;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string? AccessToken { get; private set; }
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime RefreshTokenExpiresAtUtc { get; private set; }
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public long Revision => Interlocked.Read(ref _revision);
    public (string? OwnerId, long Revision) CaptureIdentity()
    {
        lock (_stateLock) return (UserId, Revision);
    }
    public ClientAuthSessionRestoreState CurrentState => EvaluateCurrentState();
    public bool IsAuthenticated => CurrentState == ClientAuthSessionRestoreState.Authenticated
                                   && !string.IsNullOrWhiteSpace(AccessToken)
                                   && !string.IsNullOrWhiteSpace(UserId);

    public async Task<ClientAuthSessionRestoreState> RestoreAsync(
        CancellationToken cancellationToken = default)
    {
        if (_restored) return CurrentState;
        var revision = Revision;
        await _storageGate.WaitAsync(cancellationToken);
        try
        {
            if (_restored || revision != Revision) return CurrentState;
            var snapshot = await _tokenStore.LoadAsync(cancellationToken);
            lock (_stateLock)
            {
                if (revision != Revision) return CurrentState;
                if (snapshot is not null) ApplySnapshot(snapshot);
                _restored = true;
                _restoreState = EvaluateCurrentState();
            }
            if (_restoreState == ClientAuthSessionRestoreState.Anonymous)
            {
                ResetSnapshot();
                await _tokenStore.ClearAsync(cancellationToken);
            }
            return CurrentState;
        }
        finally { _storageGate.Release(); }
    }

    public async Task ApplyAsync(
        ClientAuthTokenSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await ApplyCoreAsync(snapshot, expectedRevision: null, cancellationToken);
    }

    public Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedRevision,
        CancellationToken cancellationToken = default)
        => ApplyCoreAsync(snapshot, expectedRevision, cancellationToken);

    private async Task<bool> ApplyCoreAsync(ClientAuthTokenSnapshot snapshot, long? expectedRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _storageGate.WaitAsync(cancellationToken);
        try
        {
            long revision;
            lock (_stateLock)
            {
                if (expectedRevision.HasValue && expectedRevision.Value != Revision) return false;
                revision = Interlocked.Increment(ref _revision);
            }
            await _tokenStore.SaveAsync(snapshot, cancellationToken);
            lock (_stateLock)
            {
                if (revision != Revision) return false;
                ApplySnapshot(snapshot);
                _restored = true;
                _restoreState = EvaluateCurrentState();
                return true;
            }
        }
        finally { _storageGate.Release(); }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await ClearCoreAsync(expectedRevision: null, cancellationToken);
    }

    public Task<bool> TryClearAsync(long expectedRevision, CancellationToken cancellationToken = default)
        => ClearCoreAsync(expectedRevision, cancellationToken);

    private async Task<bool> ClearCoreAsync(long? expectedRevision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long revision;
        lock (_stateLock)
        {
            if (expectedRevision.HasValue && expectedRevision.Value != Revision) return false;
            revision = Interlocked.Increment(ref _revision);
            ResetSnapshot();
            _restored = true;
            _restoreState = ClientAuthSessionRestoreState.Anonymous;
        }
        // 메모리에서 로그아웃한 뒤에는 저장소 정리를 끝내야 재시작 때 토큰이 복원되지 않습니다.
        await _storageGate.WaitAsync(CancellationToken.None);
        try
        {
            if (revision != Revision) return false;
            await _tokenStore.ClearAsync(CancellationToken.None);
        }
        finally { _storageGate.Release(); }
        return true;
    }

    private ClientAuthSessionRestoreState EvaluateCurrentState()
    {
        lock (_stateLock)
        {
            if (string.IsNullOrWhiteSpace(UserId)) return ClientAuthSessionRestoreState.Anonymous;
            var snapshot = new ClientAuthTokenSnapshot(AccessToken ?? "", AccessTokenExpiresAtUtc,
                RefreshToken ?? "", RefreshTokenExpiresAtUtc, UserId, UserName ?? "", Roles);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            return _sessionGuard.IsAccessTokenUsable(snapshot, now)
                ? ClientAuthSessionRestoreState.Authenticated
                : _sessionGuard.IsRefreshTokenUsable(snapshot, now)
                    ? ClientAuthSessionRestoreState.RefreshRequired
                    : ClientAuthSessionRestoreState.Anonymous;
        }
    }

    private void ResetSnapshot()
    {
        AccessToken = null;
        AccessTokenExpiresAtUtc = default;
        RefreshToken = null;
        RefreshTokenExpiresAtUtc = default;
        UserId = null;
        UserName = null;
        Roles = [];
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
    }
}
