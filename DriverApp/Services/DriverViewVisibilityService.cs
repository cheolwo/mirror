using Hongdal.Contracts.Common.ViewSettings;

namespace DriverApp.Services;

public sealed class DriverViewVisibilityService
{
    private const string AppKey = App식별자.DriverApp;
    private IReadOnlyList<View가시성항목응답> _items = [];

    public DriverViewVisibilityService()
    {
    }

    public event Action? Changed;

    public IReadOnlyList<View가시성항목응답> Items => _items;
    public IReadOnlyList<View가시성항목응답> VisibleItems => _items.Where(x => x.EffectiveVisible).OrderBy(x => x.SortOrder).ToArray();
    public bool IsLoaded { get; private set; }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return;
        }

        await ReloadAsync(cancellationToken);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _items = IsLoaded
            ? _items.OrderBy(x => x.SortOrder).ToArray()
            : CreateDefaultItems();
        IsLoaded = true;
        Changed?.Invoke();
        await Task.CompletedTask;
    }

    public async Task UpdateVisibilityAsync(string viewKey, bool isVisible, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsLoaded)
        {
            _items = CreateDefaultItems();
            IsLoaded = true;
        }

        var target = _items.FirstOrDefault(x => string.Equals(x.ViewKey, viewKey, StringComparison.Ordinal));
        if (target is null)
        {
            throw new InvalidOperationException("View 정의를 찾을 수 없습니다.");
        }

        if (target.IsRequired)
        {
            throw new InvalidOperationException("필수 View는 숨길 수 없습니다.");
        }

        target.UserVisible = isVisible;
        target.EffectiveVisible = target.IsRequired || (target.PolicyEnabled && target.UserVisible);
        Changed?.Invoke();
        await Task.CompletedTask;
    }

    public View가시성항목응답? GetBlockingItem(string relativePath)
    {
        if (!IsLoaded)
        {
            return null;
        }

        var normalizedPath = NormalizePath(relativePath);
        var matched = _items
            .Where(x => IsMatchingRoute(x.Route, normalizedPath))
            .OrderByDescending(x => x.Route.Length)
            .FirstOrDefault();

        return matched is { EffectiveVisible: false } ? matched : null;
    }

    private static IReadOnlyList<View가시성항목응답> CreateDefaultItems()
    {
        return
        [
            CreateItem(View식별자.Driver.DriverHome, "기사 홈", DriverRoutes.Home, "directions_car", isRequired: true, sortOrder: 10),
            CreateItem(View식별자.Driver.Recommendations, "추천 목록", DriverRoutes.Recommendations, "recommend", sortOrder: 20),
            CreateItem(View식별자.Driver.Reservations, "예약", "/driver/reservations", "event", sortOrder: 30),
            CreateItem(View식별자.Driver.CurrentTransport, "진행 중 운송", DriverRoutes.CurrentTransport, "local_shipping", isRequired: true, sortOrder: 40),
            CreateItem(View식별자.Driver.CommunityInquiries, "커뮤니티 개별 의뢰", DriverRoutes.CommunityInquiries, "forum", sortOrder: 50),
            CreateItem(View식별자.Driver.Settlements, "월 정산", "/driver/settlements/current-month", "receipt_long", sortOrder: 60),
            CreateItem(View식별자.Driver.Notifications, "알림함", "/driver/notifications", "notifications", sortOrder: 70),
            CreateItem(View식별자.Driver.ViewSettings, "화면 설정", "/driver/settings/views", "settings", isRequired: true, sortOrder: 80)
        ];
    }

    private static View가시성항목응답 CreateItem(
        string viewKey,
        string displayName,
        string route,
        string iconKey,
        bool isRequired = false,
        int sortOrder = 0)
    {
        return new View가시성항목응답
        {
            AppKey = AppKey,
            ViewKey = viewKey,
            DisplayName = displayName,
            Route = route,
            IconKey = iconKey,
            RoleName = "기사",
            IsRequired = isRequired,
            PolicyEnabled = true,
            UserVisible = true,
            EffectiveVisible = true,
            SortOrder = sortOrder
        };
    }

    private static bool IsMatchingRoute(string route, string path)
    {
        var normalizedRoute = NormalizePath(route);
        if (normalizedRoute == "/")
        {
            return path == "/";
        }

        if (path.Equals(normalizedRoute, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith(normalizedRoute + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.StartsWith('/') ? path : "/" + path;
        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }
}
