using SsalddelApp.Services;

namespace SsalddelApp.ViewModels.Shipper;

/// <summary>화주 화면의 조회를 직렬화하고 최신 요청·로그인 세션·화면 수명에만 결과를 적용합니다.</summary>
public sealed class ShipperQueryLifetime : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _queryGate = new(1, 1);
    private readonly IAuthSession _session;
    private CancellationTokenSource? _activeQuery;
    private long _queryRevision;
    private long _sessionRevision;
    private bool _disposed;

    public ShipperQueryLifetime(IAuthSession session)
    {
        _session = session;
        _sessionRevision = session.SessionRevision;
        session.Changed += OnSessionChanged;
    }

    public event Action? SessionInvalidated;
    public bool IsDisposed { get { lock (_gate) return _disposed; } }

    public async Task RunAsync<T>(
        Func<CancellationToken, Task<T>> query,
        Action<T> apply,
        Action<Exception> failed,
        Action finished,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource source;
        CancellationTokenSource? previous;
        long revision;
        long sessionRevision;
        lock (_gate)
        {
            if (_disposed || !_session.IsLoggedIn)
            {
                return;
            }

            previous = _activeQuery;
            source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeQuery = source;
            revision = ++_queryRevision;
            sessionRevision = _session.SessionRevision;
        }

        Cancel(previous);
        var entered = false;
        try
        {
            await _queryGate.WaitAsync(source.Token);
            entered = true;
            source.Token.ThrowIfCancellationRequested();
            var result = await query(source.Token);
            ApplyIfCurrent(() => apply(result));
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ApplyIfCurrent(() => failed(ex));
        }
        finally
        {
            ApplyIfCurrent(finished, allowCancellation: true);
            if (entered)
            {
                _queryGate.Release();
            }

            lock (_gate)
            {
                if (ReferenceEquals(_activeQuery, source))
                {
                    _activeQuery = null;
                }
            }
            source.Dispose();
        }

        void ApplyIfCurrent(Action action, bool allowCancellation = false)
        {
            lock (_gate)
            {
                if (!_disposed && revision == _queryRevision && _session.IsLoggedIn
                    && sessionRevision == _session.SessionRevision
                    && (allowCancellation || !source.IsCancellationRequested))
                {
                    action();
                }
            }
        }
    }

    public void CancelPending()
    {
        CancellationTokenSource? source;
        lock (_gate)
        {
            ++_queryRevision;
            source = _activeQuery;
            _activeQuery = null;
        }
        Cancel(source);
    }

    private void OnSessionChanged()
    {
        lock (_gate)
        {
            if (_disposed || _sessionRevision == _session.SessionRevision)
            {
                return;
            }
            _sessionRevision = _session.SessionRevision;
        }
        CancelPending();
        SessionInvalidated?.Invoke();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _session.Changed -= OnSessionChanged;
        CancelPending();
        // 이탈 뒤 미협조 응답의 finally도 gate를 반환하므로 semaphore는 즉시 폐기하지 않습니다.
    }

    private static void Cancel(CancellationTokenSource? source)
    {
        try { source?.Cancel(); }
        catch (ObjectDisposedException) { }
    }
}
