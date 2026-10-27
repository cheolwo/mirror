using FDriverApp.Services;

namespace FDriverApp.PageModels;

// Owns the foreground read lifetime and ordering; the ViewModel owns UI application.
internal sealed class FDriverWorkspaceRefreshSession
{
    private CancellationTokenSource _lifetime = new();
    private long _snapshotGeneration;
    private bool _backgroundRefreshInProgress;

    internal CancellationTokenSource Lifetime => _lifetime;
    internal long SnapshotGeneration => Interlocked.Read(ref _snapshotGeneration);
    internal ReadFailure? Failure { get; private set; }

    internal void EnsureLifetime()
    {
        if (!_lifetime.IsCancellationRequested) return;
        _lifetime.Dispose();
        _lifetime = new CancellationTokenSource();
    }

    internal void CancelLifetime() => _lifetime.Cancel();
    internal bool IsLifetimeCurrent(CancellationTokenSource lifetime)
        => ReferenceEquals(_lifetime, lifetime) && !lifetime.IsCancellationRequested;
    internal long AdvanceSnapshot() => Interlocked.Increment(ref _snapshotGeneration);

    internal bool TryBeginBackgroundRefresh()
    {
        if (_backgroundRefreshInProgress) return false;
        _backgroundRefreshInProgress = true;
        return true;
    }

    internal void EndBackgroundRefresh() => _backgroundRefreshInProgress = false;
    internal void RecordFailure(string? owner, long generation, string message, FDriverApiException error)
        => Failure = new(owner, generation, message, error);
    internal void ClearFailure() => Failure = null;

    internal ReadFailure? EarlierFailure(string? owner, long generation, CancellationTokenSource lifetime)
        => Failure is { } failure && failure.Owner == owner && failure.Generation < generation
            && ReferenceEquals(lifetime, _lifetime) ? failure : null;

    // Account comparison remains internal and is never formatted as a record value.
    internal sealed class ReadFailure(string? owner, long generation, string message, FDriverApiException error)
    {
        internal string? Owner { get; } = owner;
        internal long Generation { get; } = generation;
        internal string Message { get; } = message;
        internal FDriverApiException Error { get; } = error;
    }
}
