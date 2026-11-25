using System.Runtime.ExceptionServices;
using Ssalddel.Client.Infrastructure.Security;

namespace SsalddelApp.Services;

public enum AuthSessionCommitResult { Applied, Superseded, InvalidToken }

/// <summary>한 화주 로그인/로그아웃 의도입니다. 시작만으로 현재 인증을 지우지 않습니다.</summary>
public sealed class AuthSessionChange : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    private int _disposed;
    internal AuthSessionChange(AuthSession owner, long generation, long revision, CancellationToken cancellationToken)
    {
        Owner = owner; Generation = generation; ExpectedRevision = revision;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Token = _cancellation.Token;
    }
    internal AuthSession Owner { get; }
    internal long Generation { get; }
    internal long ExpectedRevision { get; set; }
    internal long? AppliedRevision { get; set; }
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    public CancellationToken Token { get; }
    internal void Cancel()
    {
        try { _cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { /* 이전 부가 작업의 취소 콜백이 새 인증 의도를 막지 않습니다. */ }
    }
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) _cancellation.Dispose(); }
}

public sealed class AuthSession : IAuthSession
{
    private readonly IClientSecureTokenStore _tokenStore;
    private readonly IClientSessionGuard _sessionGuard;
    private readonly object _mutationGate = new();
    private readonly SemaphoreSlim _storageGate = new(1, 1);
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private bool _restored;
    private long _sessionRevision;
    private long _storageRevision;
    private Task? _restoreTask;
    private long _authenticationGeneration;
    private AuthSessionChange? _authenticationChange;

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
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken);
    public long SessionRevision => Interlocked.Read(ref _sessionRevision);
    public event Action? Changed;

    public AuthSessionChange BeginAuthenticationChange(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuthSessionChange? previous;
        AuthSessionChange current;
        lock (_mutationGate)
        {
            previous = _authenticationChange;
            current = new(this, ++_authenticationGeneration, _sessionRevision, cancellationToken);
            _authenticationChange = current;
        }
        previous?.Cancel();
        return current;
    }

    public async Task<AuthSessionCommitResult> TryApplyAuthenticationAsync(ClientAuthTokenSnapshot snapshot,
        AuthSessionChange change, Func<CancellationToken, Task> beforeApply, Func<CancellationToken, Task> afterApply)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.UserId) || snapshot.Roles is null
            || !_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
            || !_sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow))
            return AuthSessionCommitResult.InvalidToken;
        long storageRevision;
        await _transitionGate.WaitAsync(change.Token);
        try
        {
            lock (_mutationGate) { if (!CurrentPending(change)) return AuthSessionCommitResult.Superseded; }
            await beforeApply(change.Token);
            lock (_mutationGate)
            {
                // 의도 판정과 상태 적용은 같은 lock 안에서 수행합니다.
                if (!CurrentPending(change)) return AuthSessionCommitResult.Superseded;
                change.Token.ThrowIfCancellationRequested();
                _restored = true;
                ApplySnapshot(snapshot, startsNewSession: true);
                change.AppliedRevision = _sessionRevision;
                storageRevision = ++_storageRevision;
            }
        }
        finally { _transitionGate.Release(); }
        Changed?.Invoke();
        // 새 의도가 취소하더라도 확정한 메모리는 기존 최신 저장 판본 규칙으로 마무리합니다.
        await PersistAsync(snapshot, storageRevision, CancellationToken.None);
        await _transitionGate.WaitAsync(change.Token);
        try
        {
            lock (_mutationGate) { if (!CurrentApplied(change)) return AuthSessionCommitResult.Superseded; }
            // 부가 작업을 분리 실행하지 않아 늦은 응답도 같은 owner 수명 안에서 끝납니다.
            await afterApply(change.Token);
            lock (_mutationGate)
                return CurrentApplied(change) ? AuthSessionCommitResult.Applied : AuthSessionCommitResult.Superseded;
        }
        finally { _transitionGate.Release(); }
    }

    public async Task<bool> TryEndAuthenticationAsync(AuthSessionChange change, Func<CancellationToken, Task> beforeClear)
    {
        long storageRevision = 0;
        bool cleared = false;
        Exception? failure = null;
        // 사용자 취소도 기존 로그아웃의 로컬 종료를 유지하되, 새 의도를 지우지 않습니다.
        await _transitionGate.WaitAsync(CancellationToken.None);
        try
        {
            lock (_mutationGate) { if (!CurrentPending(change, allowCancellation: true)) return false; }
            try { await beforeClear(change.Token); }
            catch (Exception error) { failure = error; }
            lock (_mutationGate)
            {
                if (CurrentPending(change, allowCancellation: true))
                {
                    _restored = true; ClearSnapshot();
                    change.AppliedRevision = _sessionRevision;
                    storageRevision = ++_storageRevision; cleared = true;
                }
            }
        }
        finally { _transitionGate.Release(); }
        if (cleared)
        {
            Changed?.Invoke();
            await PersistAsync(null, storageRevision, CancellationToken.None);
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return cleared;
    }

    private bool CurrentPending(AuthSessionChange change, bool allowCancellation = false)
        => ReferenceEquals(change.Owner, this) && ReferenceEquals(change, _authenticationChange)
            && change.Generation == _authenticationGeneration && !change.IsDisposed && change.AppliedRevision is null
            && change.ExpectedRevision == _sessionRevision && (allowCancellation || !change.Token.IsCancellationRequested);

    public async Task<bool> RunIfCurrentAsync(long expectedRevision, string? expectedRefreshToken,
        Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            lock (_mutationGate) { if (!MatchesSession(expectedRevision, expectedRefreshToken)) return false; }
            await action(cancellationToken);
            lock (_mutationGate) return MatchesSession(expectedRevision, expectedRefreshToken);
        }
        finally { _transitionGate.Release(); }
    }
    private bool CurrentApplied(AuthSessionChange change)
        => ReferenceEquals(change.Owner, this) && ReferenceEquals(change, _authenticationChange)
            && change.Generation == _authenticationGeneration && !change.IsDisposed
            && change.AppliedRevision == _sessionRevision && !change.Token.IsCancellationRequested;

    private long InvalidateAuthenticationChange()
    {
        AuthSessionChange? previous;
        long generation;
        lock (_mutationGate)
        { previous = _authenticationChange; _authenticationChange = null; generation = ++_authenticationGeneration; }
        previous?.Cancel();
        return generation;
    }

    private void RebasePendingChange(long previousRevision)
    {
        if (_authenticationChange is { AppliedRevision: null, IsDisposed: false } change
            && change.ExpectedRevision == previousRevision)
            change.ExpectedRevision = _sessionRevision;
    }

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
        try { await _transitionGate.WaitAsync(cancellationToken); }
        catch
        {
            lock (_mutationGate) { if (revision == _storageRevision) _restored = false; }
            throw;
        }
        try
        {
            lock (_mutationGate)
            {
                if (revision != _storageRevision) return;
                var previousRevision = _sessionRevision;
                clear = !_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
                    && !_sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow);
                if (clear) ClearSnapshot();
                else ApplySnapshot(snapshot!, startsNewSession: true);
                RebasePendingChange(previousRevision);
                storageRevision = ++_storageRevision;
            }
        }
        finally { _transitionGate.Release(); }
        Changed?.Invoke();
        if (clear) await PersistAsync(null, storageRevision, cancellationToken);
    }

    public async Task ApplyAsync(
        ClientAuthTokenSnapshot snapshot,
        CancellationToken cancellationToken = default,
        bool startsNewSession = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var generation = InvalidateAuthenticationChange();
        long storageRevision;
        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            lock (_mutationGate)
            {
                if (generation != _authenticationGeneration) return;
                cancellationToken.ThrowIfCancellationRequested();
                _restored = true;
                ApplySnapshot(snapshot, startsNewSession);
                storageRevision = ++_storageRevision;
            }
        }
        finally { _transitionGate.Release(); }
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
        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            lock (_mutationGate)
            {
                if (!MatchesSession(expectedRevision, expectedRefreshToken)
                    || !string.Equals(UserId, snapshot.UserId, StringComparison.Ordinal))
                    return false;
                cancellationToken.ThrowIfCancellationRequested();
                // 정상 HTTP 응답도 로그인과 같은 토큰 유효성 계약을 통과해야 합니다.
                // 검증 실패는 기존 메모리·저장 판본과 화면 구독 상태를 바꾸지 않습니다.
                if (string.IsNullOrWhiteSpace(snapshot.UserId) || snapshot.Roles is null
                    || !_sessionGuard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
                    || !_sessionGuard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow))
                    return false;
                _restored = true;
                ApplySnapshot(snapshot, startsNewSession: false);
                storageRevision = ++_storageRevision;
            }
        }
        finally { _transitionGate.Release(); }
        Changed?.Invoke();
        // 이미 적용한 갱신은 최신 저장 판본 규칙으로 마무리하고 나서 호출자 취소를 알립니다.
        await PersistAsync(snapshot, storageRevision, CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generation = InvalidateAuthenticationChange();
        long storageRevision;
        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            lock (_mutationGate)
            {
                if (generation != _authenticationGeneration) return;
                cancellationToken.ThrowIfCancellationRequested();
                _restored = true;
                ClearSnapshot();
                storageRevision = ++_storageRevision;
            }
        }
        finally { _transitionGate.Release(); }
        Changed?.Invoke();
        await PersistAsync(null, storageRevision, cancellationToken);
    }

    public async Task<bool> TryClearAsync(
        long expectedRevision,
        string? expectedRefreshToken,
        CancellationToken cancellationToken = default)
    {
        long storageRevision;
        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            lock (_mutationGate)
            {
                if (!MatchesSession(expectedRevision, expectedRefreshToken)) return false;
                cancellationToken.ThrowIfCancellationRequested();
                var previousRevision = _sessionRevision;
                _restored = true;
                ClearSnapshot();
                storageRevision = ++_storageRevision;
                RebasePendingChange(previousRevision);
            }
        }
        finally { _transitionGate.Release(); }
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
