using Hongdal.Contracts.Common.ViewSettings;

namespace HongdalApp.Services;

public sealed class ShipperViewVisibilityService
{
    private const string AppKey = App식별자.HongdalApp;
    private IReadOnlyList<View가시성항목응답> _items = [];

    public ShipperViewVisibilityService()
    {
    }

    public event Action? Changed;

    public IReadOnlyList<View가시성항목응답> Items => _items;
    public IReadOnlyList<View가시성항목응답> VisibleItems => _items.Where(x => x.EffectiveVisible).OrderBy(x => x.SortOrder).ToArray();
    public bool IsLoaded { get; private set; }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return Task.CompletedTask;
        }

        return ReloadAsync(cancellationToken);
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _items = IsLoaded
            ? _items.OrderBy(x => x.SortOrder).ToArray()
            : CreateDefaultItems();
        IsLoaded = true;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task UpdateVisibilityAsync(string viewKey, bool isVisible, CancellationToken cancellationToken = default)
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
        return Task.CompletedTask;
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

    private static IReadOnlyList<View가시성항목응답> CreateDefaultItems()
    {
        return
        [
            CreateItem(View식별자.Shipper.Home, "Home", ShipperRoutes.Home, "home", isRequired: true, sortOrder: 10),
            CreateItem(View식별자.Shipper.TransportWorkspace, "운송 업무", ShipperRoutes.TransportWorkspace, "local_shipping", isRequired: true, sortOrder: 15),
            CreateItem(View식별자.Shipper.Request, "화물운송의뢰 등록", ShipperRoutes.Request, "add_box", isRequired: true, sortOrder: 20),
            CreateItem(View식별자.Shipper.PublicCargo, "공개 화물정보", ShipperRoutes.PublicCargo, "view_list", sortOrder: 30),
            CreateItem(View식별자.Shipper.ExplorationInbox, "받은 탐색 문의함", ShipperRoutes.ExplorationInbox, "inbox", sortOrder: 40),
            CreateItem(View식별자.Shipper.WarehouseWorkspace, "창고 작업대", ShipperRoutes.WarehouseWorkspace, "warehouse", sortOrder: 50),
            CreateItem(View식별자.Shipper.InboundDashboard, "입고 대시보드", ShipperRoutes.InboundDashboard, "dashboard", sortOrder: 60),
            CreateItem(View식별자.Shipper.InboundRequests, "입고 현황", ShipperRoutes.InboundRequests, "inventory_2", sortOrder: 70),
            CreateItem(View식별자.Shipper.WarehouseInventory, "재고 목록", ShipperRoutes.WarehouseInventory, "warehouse", sortOrder: 80),
            CreateItem(View식별자.Shipper.ReconsignmentOrders, "재위탁 운송", ShipperRoutes.ReconsignmentOrders, "local_shipping", sortOrder: 90),
            CreateItem(View식별자.Shipper.SalesChannels, "판매채널 연결", ShipperRoutes.SalesChannels, "storefront", sortOrder: 100),
            CreateItem(View식별자.Shipper.ProductListings, "출품 관리", ShipperRoutes.ProductListings, "sell", sortOrder: 110),
            CreateItem(View식별자.Shipper.OrderFulfillment, "주문 출고 알림", ShipperRoutes.OrderFulfillment, "outbox", sortOrder: 120),
            CreateItem(View식별자.Shipper.CustomsHsReviews, "HS 코드 검토", ShipperRoutes.CustomsHsReviews, "fact_check", sortOrder: 130),
            CreateItem(View식별자.Shipper.ViewSettings, "화면 설정", ShipperRoutes.ViewSettings, "settings", isRequired: true, sortOrder: 140)
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
            RoleName = "화주",
            IsRequired = isRequired,
            PolicyEnabled = true,
            UserVisible = true,
            EffectiveVisible = true,
            SortOrder = sortOrder
        };
    }
}
