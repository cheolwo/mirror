namespace HongdalApp.Services;

public sealed record HongdalClientNavigationItem(
    string DisplayName,
    string Route,
    string IconKey,
    string GroupKey,
    int SortOrder);

public static class HongdalClientNavigationCatalog
{
    public static readonly IReadOnlyList<HongdalClientNavigationItem> WarehouseManagerItems =
    [
        new("홈", ShipperRoutes.Home, "home", "primary", 10),
        new("창고 작업대", ShipperRoutes.WarehouseWorkspace, "warehouse", "warehouse", 20),
        new("입고 현황", ShipperRoutes.InboundRequests, "inventory_2", "warehouse", 30),
        new("재고 목록", ShipperRoutes.WarehouseInventory, "warehouse", "warehouse", 40),
        new("현장 스캔", ShipperRoutes.WarehouseScan, "qr_code_scanner", "warehouse", 50),
        new("출고·판매 주문", ShipperRoutes.OrderFulfillment, "outbox", "sales", 60),
        new("배송 연결", ShipperRoutes.ReconsignmentOrders, "local_shipping", "transport", 70),
        new("화면 설정", ShipperRoutes.ViewSettings, "settings", "settings", 80),
        new("프로필 설정", ShipperRoutes.ProfileSettings, "person", "settings", 90)
    ];
}
