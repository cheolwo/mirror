namespace FDriverApp.PageModels;

// Map acquisition has a separate lifetime. This owns only operational uploads,
// including stop/start invalidation, serialized sends and confirmed heartbeat time.
internal sealed class FDriverLocationTransmissionSession(TimeSpan heartbeatInterval)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cancellation;
    private long _generation;
    private DateTime? _lastSentAtUtc;

    internal long Generation => Interlocked.Read(ref _generation);
    internal DateTime? LastSentAtUtc => _lastSentAtUtc;
    internal bool HeartbeatDue => !_lastSentAtUtc.HasValue
        || DateTime.UtcNow - _lastSentAtUtc.Value >= heartbeatInterval;

    internal void Cancel()
    {
        Interlocked.Increment(ref _generation);
        var previous = _cancellation;
        _cancellation = null;
        previous?.Cancel();
        previous?.Dispose();
        ClearLastConfirmation();
    }

    internal void ClearLastConfirmation() => _lastSentAtUtc = null;

    internal async Task<bool> TransmitAsync(long generation, CancellationTokenSource workspace,
        Func<bool> isCaptureCurrent, Func<bool> isFresh, Func<CancellationToken, Task> send,
        Action<DateTime> confirm, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!isCaptureCurrent() || generation != Generation || !isFresh()) return false;
            if (_lastSentAtUtc is { } sentAt && DateTime.UtcNow - sentAt < heartbeatInterval)
                return true;

            var transmission = _cancellation ??= new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, workspace.Token, transmission.Token);
            await send(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!isCaptureCurrent() || generation != Generation) return false;
            _lastSentAtUtc = DateTime.UtcNow;
            confirm(_lastSentAtUtc.Value);
            return true;
        }
        finally { _gate.Release(); }
    }
}
