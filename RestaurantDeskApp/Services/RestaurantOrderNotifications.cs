using RestaurantDeskApp.Models.Restaurant;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

public sealed record RestaurantNotificationActivation(string OrderNo, string OwnerId);

public interface IRestaurantOrderNotificationPlatform
{
    bool IsSupported { get; }
    event Action<RestaurantNotificationActivation>? Activated;
    RestaurantNotificationActivation? TakePendingActivation();
    Task<bool> RequestPermissionAsync(CancellationToken cancellationToken);
    Task ShowAsync(string orderNo, string ownerId, CancellationToken cancellationToken);
    void Clear();
}

public sealed class NoRestaurantOrderNotifications : IRestaurantOrderNotificationPlatform
{
    public bool IsSupported => false;
    public event Action<RestaurantNotificationActivation>? Activated { add { } remove { } }
    public RestaurantNotificationActivation? TakePendingActivation() => null;
    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    public Task ShowAsync(string orderNo, string ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
    public void Clear() { }
}

// Hub는 갱신 신호일 뿐이다. 인증된 정본을 확인한 뒤 generic 알림만 OS에 표시한다.
// 앱 프로세스/Hub가 유지되는 동안의 로컬 알림이며 강제 종료 중 원격 push provider는 아니다.
public sealed class RestaurantOrderNotificationCoordinator : IDisposable
{
    private readonly I음식점주문SignalRClientService _realtime;
    private readonly I음식주문ApiClient _orders;
    private readonly RestaurantAuthService _auth;
    private readonly ClientAuthSession _session;
    private readonly I주문알림Service _sound;
    private readonly IRestaurantOrderNotificationPlatform _platform;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<string> _seenOrder = new();
    private CancellationTokenSource _notificationCancellation = new();
    private RestaurantNotificationActivation? _pending;
    private string? _owner;
    private long _generation;
    private bool _disposed;

    public RestaurantOrderNotificationCoordinator(I음식점주문SignalRClientService realtime,
        I음식주문ApiClient orders, RestaurantAuthService auth, ClientAuthSession session,
        I주문알림Service sound, IRestaurantOrderNotificationPlatform platform)
    {
        _realtime = realtime; _orders = orders; _auth = auth; _session = session; _sound = sound; _platform = platform;
        _realtime.주문수신 += ReceiveAsync;
        _realtime.상태변경 += OnConnectionChangedAsync;
        _platform.Activated += OnActivated;
        _auth.SessionEnding += EndSession;
        _pending = _platform.TakePendingActivation();
    }

    public bool SupportsSystemNotification => _platform.IsSupported;
    public event Action? NavigationRequested;
    public event Func<Task>? CanonicalRefreshRequested;
    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken = default)
        => _platform.RequestPermissionAsync(cancellationToken);

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        var auth = await _auth.EnsureAccessTokenAsync(cancellationToken: cancellationToken);
        if (_disposed || !auth.IsSuccess) return;
        BindOwner();
        try { await _realtime.연결Async(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { /* Canonical page reads remain available; no synthetic success status. */ }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await SynchronizeAsync(cancellationToken);
        if (_disposed || !_session.IsAuthenticated) return;
        var owner = _owner;
        var generation = _generation;
        var handlers = CanonicalRefreshRequested;
        if (handlers is not null)
            foreach (Func<Task> callback in handlers.GetInvocationList())
            {
                if (!IsCurrent(owner, generation)) return;
                await callback();
            }
        if (IsCurrent(owner, generation) && _pending is not null) NavigationRequested?.Invoke();
    }

    private void BindOwner()
    {
        if (string.Equals(_owner, _session.UserId, StringComparison.Ordinal)) return;
        _owner = _session.UserId;
        Interlocked.Increment(ref _generation);
        lock (_stateGate)
        {
            _notificationCancellation.Cancel(); _notificationCancellation.Dispose();
            _notificationCancellation = new();
            _seen.Clear(); _seenOrder.Clear();
        }
        _platform.Clear();
        if (_pending is not null && !string.Equals(_pending.OwnerId, _owner, StringComparison.Ordinal)) _pending = null;
    }

    public void EndSession(bool clearPending = true)
    {
        Interlocked.Increment(ref _generation); _owner = null;
        if (clearPending) _pending = null;
        lock (_stateGate)
        {
            _notificationCancellation.Cancel();
            _seen.Clear(); _seenOrder.Clear();
        }
        _platform.Clear();
    }

    private bool IsCurrent(string? owner, long generation)
        => !_disposed && generation == _generation && _session.IsAuthenticated
            && !string.IsNullOrWhiteSpace(owner) && string.Equals(owner, _session.UserId, StringComparison.Ordinal)
            && string.Equals(owner, _owner, StringComparison.Ordinal);

    private async Task ReceiveAsync(음식점주문수신알림 signal)
    {
        if (_disposed || !_session.IsAuthenticated || string.IsNullOrWhiteSpace(signal.주문번호)) return;
        BindOwner();
        var owner = _owner;
        var generation = _generation;
        CancellationToken token;
        lock (_stateGate) token = _notificationCancellation.Token;
        await _gate.WaitAsync();
        try
        {
            if (!IsCurrent(owner, generation)) return;
            lock (_stateGate) { if (_seen.Contains(signal.주문번호)) return; }
            var order = await _orders.주문상세조회Async(signal.주문번호, token);
            if (!IsCurrent(owner, generation) || order is null || order.음식점Id != signal.음식점Id
                || order.상태 != 음식주문상태코드.주문대기) return;
            await _platform.ShowAsync(order.주문번호, owner!, token);
            if (!IsCurrent(owner, generation)) { _platform.Clear(); return; }
            lock (_stateGate)
            {
                _seen.Add(order.주문번호); _seenOrder.Enqueue(order.주문번호);
                while (_seenOrder.Count > 256) _seen.Remove(_seenOrder.Dequeue());
            }
            await _sound.신규주문알림재생Async(token);
        }
        catch { /* Page retry/reconnect recovers the inbox; an OS failure cannot reject the order. */ }
        finally { _gate.Release(); }
    }

    private Task OnConnectionChangedAsync(음식점실시간연결상태변경 change)
    {
        if (change.상태 == 음식점실시간연결상태.인증필요) EndSession(clearPending: false);
        return Task.CompletedTask;
    }

    private void OnActivated(RestaurantNotificationActivation activation)
    {
        if (_disposed || string.IsNullOrWhiteSpace(activation.OrderNo) || string.IsNullOrWhiteSpace(activation.OwnerId)) return;
        _pending = activation;
        NavigationRequested?.Invoke();
    }

    public async Task<string?> ResolvePendingNavigationAsync(CancellationToken cancellationToken = default)
    {
        var activation = _pending;
        if (_disposed || activation is null) return null;
        var auth = await _auth.EnsureAccessTokenAsync(cancellationToken: cancellationToken);
        if (_disposed || !ReferenceEquals(_pending, activation)) return null;
        if (!auth.IsSuccess) return auth.RequiresLogin ? "/login" : null;
        BindOwner();
        if (!ReferenceEquals(_pending, activation)
            || !string.Equals(_session.UserId, activation.OwnerId, StringComparison.Ordinal)) return null;
        var generation = _generation;
        var order = await _orders.주문상세조회Async(activation.OrderNo, cancellationToken);
        if (!IsCurrent(activation.OwnerId, generation) || !ReferenceEquals(_pending, activation)) return null;
        _pending = null;
        return order is null ? "/orders" : $"/orders/{Uri.EscapeDataString(order.주문번호)}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        EndSession();
        _realtime.주문수신 -= ReceiveAsync;
        _realtime.상태변경 -= OnConnectionChangedAsync;
        _platform.Activated -= OnActivated;
        _auth.SessionEnding -= EndSession;
    }
}
