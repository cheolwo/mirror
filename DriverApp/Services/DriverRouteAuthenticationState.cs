namespace DriverApp.Services;

public enum DriverRouteAuthenticationStatus
{
    Public,
    Checking,
    LoginRequired,
    Unavailable,
    Authenticated
}

/// <summary>
/// 기사 업무 라우트의 인증 확인과 세션별 화면 수명만 조립합니다.
/// 권한·업무 상태·토큰 갱신은 기존 인증 Client와 서버가 소유합니다.
/// </summary>
public sealed class DriverRouteAuthenticationState : IDisposable
{
    private readonly IAuthSession _auth;
    private readonly CancellationTokenSource _lifetime = new();
    private string? _route;
    private long _generation;
    private long? _verifiedSessionRevision;
    private bool _checking;
    private bool _disposed;

    public DriverRouteAuthenticationState(IAuthSession auth)
    {
        _auth = auth;
        _auth.Changed += OnAuthenticationChanged;
    }

    public event Action? Changed;
    public string? ErrorMessage { get; private set; }
    public bool RequiresAuthentication => IsDriverRoute(_route);
    public long? WorkspaceSessionRevision => _verifiedSessionRevision;
    public string LoginRoute => DriverRoutes.LoginFor(SafeReturnRoute(_route));

    public DriverRouteAuthenticationStatus Status => !RequiresAuthentication
        ? DriverRouteAuthenticationStatus.Public
        : _checking
            ? DriverRouteAuthenticationStatus.Checking
            : ErrorMessage is not null
                ? DriverRouteAuthenticationStatus.Unavailable
                : _verifiedSessionRevision != _auth.SessionRevision
                    ? DriverRouteAuthenticationStatus.Checking
                    : _auth.IsAuthenticated
                        ? DriverRouteAuthenticationStatus.Authenticated
                        : DriverRouteAuthenticationStatus.LoginRequired;

    public void SetRoute(string relativePath)
    {
        if (_disposed || string.Equals(_route, relativePath, StringComparison.Ordinal))
            return;
        _route = relativePath;
        _generation++;
        _verifiedSessionRevision = null;
        _checking = false;
        ErrorMessage = null;
        Changed?.Invoke();
    }

    public async Task CheckAsync()
    {
        if (_disposed || !RequiresAuthentication || _checking
            || Status is DriverRouteAuthenticationStatus.Authenticated
                or DriverRouteAuthenticationStatus.LoginRequired)
            return;

        var generation = ++_generation;
        _checking = true;
        ErrorMessage = null;
        Changed?.Invoke();
        try
        {
            await _auth.RestoreAsync(_lifetime.Token);
            if (_disposed || generation != _generation)
                return;
            _verifiedSessionRevision = _auth.SessionRevision;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            if (_disposed || generation != _generation)
                return;
            _verifiedSessionRevision = null;
            ErrorMessage = "로그인 정보를 확인하지 못했습니다. 다시 시도해 주세요.";
        }
        finally
        {
            if (!_disposed && generation == _generation)
            {
                _checking = false;
                Changed?.Invoke();
            }
        }
    }

    public static bool IsDriverRoute(string? route)
    {
        var path = RoutePath(route);
        return path.Equals("/driver", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/driver/", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeReturnRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return DriverRoutes.HomeSummary;
        var candidate = route.StartsWith('/') ? route : "/" + route;
        var fragment = candidate.IndexOf('#');
        if (fragment >= 0)
            candidate = candidate[..fragment];
        // Keep the existing Login page's internal /driver/ returnUrl contract.
        if (!candidate.StartsWith("/driver/", StringComparison.Ordinal)
            || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.Contains("://", StringComparison.Ordinal)
            || candidate.Contains('\\') || candidate.Any(char.IsControl))
            return DriverRoutes.HomeSummary;
        return candidate;
    }

    private static string RoutePath(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return string.Empty;
        var path = route.Split('?', '#')[0];
        if (!path.StartsWith('/'))
            path = "/" + path;
        try { return Uri.UnescapeDataString(path); }
        catch (UriFormatException) { return path; }
    }

    private void OnAuthenticationChanged()
    {
        if (_disposed)
            return;
        _generation++;
        _verifiedSessionRevision = _auth.SessionRevision;
        _checking = false;
        ErrorMessage = null;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _generation++;
        _auth.Changed -= OnAuthenticationChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
