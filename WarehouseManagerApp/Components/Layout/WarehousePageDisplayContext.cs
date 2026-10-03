namespace WarehouseManagerApp.Components.Layout;

/// <summary>실제로 표시된 인증 패널의 수명만 셸에 전달합니다. 세션·초안·권한은 소유하지 않습니다.</summary>
public sealed class WarehousePageDisplayContext
{
    private object? _owner;
    private long _generation;
    private bool _disposed;
    private string? _location;

    public bool AuthenticationOnly { get; private set; }
    public event Action? Changed;

    public PageLease Acquire(string location)
    {
        var owner = new object();
        _owner = owner;
        _location = location;
        var generation = ++_generation;
        SetMode(true);
        return new PageLease(this, owner, generation, location);
    }

    public void ResetForNavigation(string location)
    {
        // Router가 먼저 새 패널을 조립했어도 그 패널의 소유권을 무효화하지 않습니다.
        if (string.Equals(_location, location, StringComparison.Ordinal)) return;
        _location = location;
        ClearOwner();
    }

    private void ClearOwner()
    {
        _owner = null;
        _generation++;
        SetMode(false);
    }

    public void Stop()
    {
        _disposed = true;
        _owner = null;
        _generation++;
        Changed = null;
    }

    private bool IsCurrent(object owner, long generation)
        => !_disposed && ReferenceEquals(_owner, owner) && generation == _generation;

    private void SetMode(bool authenticationOnly)
    {
        if (_disposed || AuthenticationOnly == authenticationOnly) return;
        AuthenticationOnly = authenticationOnly;
        Changed?.Invoke();
    }

    public sealed class PageLease(
        WarehousePageDisplayContext context,
        object owner,
        long generation,
        string location) : IDisposable
    {
        private bool _released;

        public bool IsCurrent => !_released && context.IsCurrent(owner, generation);

        public bool IsFor(string pageLocation)
            => IsCurrent && string.Equals(location, pageLocation, StringComparison.Ordinal);

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            if (!context.IsCurrent(owner, generation)) return;
            context.ClearOwner();
        }
    }
}
