namespace OrdererApp.Components.Layout;

/// <summary>현재 페이지의 인증 전용 표시 여부만 셸에 전달합니다. 세션·초안·권한을 소유하지 않습니다.</summary>
public sealed class OrdererPageDisplayContext
{
    private object? _owner;
    private long _generation;
    private bool _disposed;
    private string? _location;

    public bool AuthenticationOnly { get; private set; }
    public event Action? Changed;

    public PageLease Acquire(bool authenticationOnly, string location)
    {
        var owner = new object();
        _owner = owner;
        _location = location;
        var generation = ++_generation;
        SetMode(authenticationOnly);
        return new PageLease(this, owner, generation, location);
    }

    public void ResetForNavigation(string location)
    {
        // Router가 먼저 새 페이지를 조립했어도 그 페이지의 소유권을 다시 무효화하지 않습니다.
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
        OrdererPageDisplayContext context,
        object owner,
        long generation,
        string location) : IDisposable
    {
        private bool _released;

        public bool IsCurrent => !_released && context.IsCurrent(owner, generation);

        public bool IsFor(string pageLocation)
            => IsCurrent && string.Equals(location, pageLocation, StringComparison.Ordinal);

        public bool SetAuthenticationMode(bool authenticationOnly)
        {
            if (!IsCurrent) return false;
            context.SetMode(authenticationOnly);
            return true;
        }

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            if (!context.IsCurrent(owner, generation)) return;
            context.ClearOwner();
        }
    }
}
