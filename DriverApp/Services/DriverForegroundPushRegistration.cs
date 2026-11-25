namespace DriverApp.Services;

/// <summary>저장된 알림 연결의 확인을 전경·기사 세션에 묶습니다. 운송 상태와 오류는 변경하지 않습니다.</summary>
public sealed class DriverForegroundPushRegistration(IAuthSession session,
    Func<CancellationToken, Task> register) : IDisposable
{
    public const string FailureNotice = "알림 연결을 확인하지 못했습니다. 화면을 다시 열면 재확인합니다.";
    private static readonly TimeSpan RegistrationTimeout = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private readonly SemaphoreSlim _providerGate = new(1, 1);
    private Registration? _current;
    private bool _active;
    private string _notice = string.Empty;

    private sealed class Registration
    {
        public Registration(string owner, long revision)
        { Owner = owner; Revision = revision; Token = Lifetime.Token; }
        public string Owner { get; }
        public long Revision { get; }
        public CancellationTokenSource Lifetime { get; } = new();
        public CancellationToken Token { get; }
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public string Notice { get { lock (_gate) return _notice; } }
    public event Action? Changed;

    public void Resume()
    {
        lock (_gate)
        {
            if (_active) return;
            _active = true;
            session.Changed += OnSessionChanged;
        }
    }

    /// <summary>같은 전경·계정의 중복 호출은 같은 결과를 기다립니다. 새 수명만 새 확인을 시작합니다.</summary>
    public Task<bool> TryRegisterAsync()
    {
        Registration current;
        lock (_gate)
        {
            if (!_active || !session.IsAuthenticated || string.IsNullOrWhiteSpace(session.UserId))
                return Task.FromResult(false);
            if (_current is not null) return _current.Completion.Task;
            current = new(session.UserId, session.SessionRevision);
            _current = current;
        }
        _ = RunAsync(current);
        return current.Completion.Task;
    }

    private async Task RunAsync(Registration current)
    {
        var enteredProvider = false;
        var providerOwnsRelease = false;
        var succeeded = false;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(current.Token);
        bounded.CancelAfter(RegistrationTimeout);
        try
        {
            await _providerGate.WaitAsync(bounded.Token).ConfigureAwait(false);
            enteredProvider = true;
            bounded.Token.ThrowIfCancellationRequested();
            // 취소를 무시하는 이전 등록이 끝나기 전에는 다른 계정/수명의 등록도 겹치지 않습니다.
            var pending = register(bounded.Token);
            providerOwnsRelease = true;
            _ = pending.ContinueWith(completed =>
            {
                _ = completed.Exception; // UI 대기가 취소된 뒤의 늦은 실패도 관측하되 표시하지 않습니다.
                _providerGate.Release();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            await pending.WaitAsync(bounded.Token).ConfigureAwait(false);
            lock (_gate) succeeded = IsCurrent(current) && !bounded.IsCancellationRequested;
        }
        catch (Exception)
        {
            var changed = false;
            lock (_gate)
            {
                if (IsCurrent(current) && _notice != FailureNotice)
                { _notice = FailureNotice; changed = true; }
            }
            if (changed) Changed?.Invoke();
        }
        finally
        {
            if (enteredProvider && !providerOwnsRelease) _providerGate.Release();
            current.Completion.TrySetResult(succeeded);
        }
    }

    private bool IsCurrent(Registration registration) => _active && ReferenceEquals(_current, registration)
        && !registration.Token.IsCancellationRequested && session.IsAuthenticated
        && registration.Owner == session.UserId && registration.Revision == session.SessionRevision;

    private void OnSessionChanged()
    {
        var changed = false;
        lock (_gate)
        {
            if (_current is not { } current || IsCurrent(current)) return;
            changed = InvalidateCore();
        }
        if (changed) Changed?.Invoke();
    }

    public void Pause()
    {
        bool changed;
        lock (_gate)
        {
            if (_active) session.Changed -= OnSessionChanged;
            _active = false;
            changed = InvalidateCore();
        }
        if (changed) Changed?.Invoke();
    }

    private bool InvalidateCore()
    {
        var current = _current;
        _current = null;
        if (current is not null)
        {
            current.Lifetime.Cancel();
            current.Lifetime.Dispose();
        }
        var changed = _notice.Length > 0;
        _notice = string.Empty;
        return changed;
    }

    public void Dispose() => Pause();
}
