using Hongdal.WebApp.Models;
using MudBlazor;

namespace Hongdal.WebApp.Services;

public static class WebNavigationCatalog
{
    public const string DiagramRoute = "/diagram";

    private static readonly WebNavigationItem Home =
        new("역할별 화면", "/", Icons.Material.Filled.Dashboard, true);

    private static readonly WebNavigationItem Diagram =
        new("업무 다이어그램", DiagramRoute, Icons.Material.Filled.AccountTree);

    private static readonly IReadOnlyList<WebNavigationItem> GuestItems =
    [
        Home,
        new("웹 로그인", "/login", Icons.Material.Filled.Login),
        new("글로벌 상품 탐색", GlobalTradeRoutes.Home, Icons.Material.Filled.Language)
    ];

    private static readonly IReadOnlyList<WebNavigationItem> DriverItems =
    [
        new("기사 홈", "/driver/home", Icons.Material.Filled.Route),
        new("운행 시작", "/driver/work/start", Icons.Material.Filled.PlayArrow),
        new("추천 배차", "/driver/recommendations", Icons.Material.Filled.TaskAlt),
        new("진행 중 운송", "/driver/transports/current", Icons.Material.Filled.LocalShipping),
        new("기사 알림함", "/driver/notifications", Icons.Material.Filled.Notifications)
    ];

    private static readonly IReadOnlyList<WebNavigationItem> ShipperItems =
    [
        new("화주·판매자 홈", ShipperRoutes.Home, Icons.Material.Filled.LocalShipping),
        new("운송 의뢰 작성", ShipperRoutes.Request, Icons.Material.Filled.PostAdd),
        new("입고 대시보드", ShipperRoutes.InboundDashboard, Icons.Material.Filled.MoveToInbox),
        new("재고 목록", ShipperRoutes.WarehouseInventory, Icons.Material.Filled.Inventory2),
        new("주문 출고", ShipperRoutes.OrderFulfillment, Icons.Material.Filled.Outbox)
    ];

    private static readonly IReadOnlyList<WebNavigationItem> WarehouseItems =
    [
        new("창고·현장 홈", WarehouseManagerRoutes.Home, Icons.Material.Filled.Warehouse),
        new("작업 보드", WarehouseManagerRoutes.WorkBoard, Icons.Material.Filled.ViewKanban),
        new("스캔 스테이션", WarehouseManagerRoutes.Scan, Icons.Material.Filled.QrCodeScanner),
        new("입고 검수", WarehouseManagerRoutes.InboundInspection, Icons.Material.Filled.FactCheck),
        new("피킹 배치", WarehouseManagerRoutes.PickingBatch, Icons.Material.Filled.Inventory2)
    ];

    private static readonly IReadOnlyList<WebNavigationItem> OrdererItems =
    [
        new("주문자·공동주문 홈", "/orderer", Icons.Material.Filled.Groups),
        new("예정 품목 문서", "/tools/expected-item-documents", Icons.Material.Filled.Print),
        new("번호 QR·바코드", "/tools/identifier-codes", Icons.Material.Filled.QrCodeScanner)
    ];

    private static readonly IReadOnlyList<WebNavigationItem> CustomsItems =
    [
        new("글로벌 상품 탐색", GlobalTradeRoutes.Home, Icons.Material.Filled.Language),
        new("글로벌 수입 요청함", GlobalTradeRoutes.ImportRequests, Icons.Material.Filled.Handshake),
        new("HS 코드 검토", ShipperRoutes.CustomsHsReviews, Icons.Material.Filled.FactCheck),
        new("FCL/LCL 판단", ShipperRoutes.FclLclPlanner, Icons.Material.Filled.Public)
    ];

    private static readonly IReadOnlyList<WebNavigationItem> GeneralItems =
    [
        Home,
        new("글로벌 상품 탐색", GlobalTradeRoutes.Home, Icons.Material.Filled.Language),
        new("화주·판매자", ShipperRoutes.Home, Icons.Material.Filled.LocalShipping),
        new("기사", "/driver/home", Icons.Material.Filled.Route),
        new("창고·현장", WarehouseManagerRoutes.Home, Icons.Material.Filled.Warehouse),
        new("주문자·공동주문", "/orderer", Icons.Material.Filled.Groups)
    ];

    public static IReadOnlyList<WebNavigationItem> CommunityItems { get; } =
    [
        new("내 정보", "/community/me", Icons.Material.Filled.AccountCircle, true),
        new("내 글", "/community/me/posts", Icons.Material.Filled.Article),
        new("참여 중", "/community/me/actions", Icons.Material.Filled.Groups),
        new("내 원장", "/community/me/ledgers", Icons.Material.Filled.Assignment),
        new("알림", "/community/me/notifications", Icons.Material.Filled.Notifications),
        new("꾸미기", "/community/decorations", Icons.Material.Filled.Palette),
        new("사용 설정", "/community/me/settings", Icons.Material.Filled.Settings)
    ];

    public static IReadOnlyList<WebNavigationItem> GetBusinessItems(string? themeCode)
    {
        var roleItems = themeCode?.Trim().ToLowerInvariant() switch
        {
            "guest" => GuestItems,
            "driver" => DriverItems,
            "shipper" => ShipperItems,
            "warehouse" => WarehouseItems,
            "orderer" => OrdererItems,
            "customs" => CustomsItems,
            _ => GeneralItems
        };

        return themeCode?.Equals("guest", StringComparison.OrdinalIgnoreCase) == true
            ? roleItems
            : [.. roleItems, Diagram];
    }

    public static string GetBusinessHome(string? themeCode)
        => themeCode?.Trim().ToLowerInvariant() switch
        {
            "driver" => "/driver/home",
            "shipper" => ShipperRoutes.Home,
            "warehouse" => WarehouseManagerRoutes.Home,
            "orderer" => "/orderer",
            "customs" => GlobalTradeRoutes.ImportRequests,
            _ => "/"
        };

    public static bool IsCommunityRoute(string? relativePath)
    {
        var path = NormalizePath(relativePath);
        return path.Equals("community", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("community/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? relativePath)
        => (relativePath ?? string.Empty)
            .Split('?', '#')[0]
            .Trim('/');
}
