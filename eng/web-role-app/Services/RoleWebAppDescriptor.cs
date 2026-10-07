using System.Reflection;
using MudBlazor;

namespace Ssalddel.WebApp.Services;

public sealed record RoleWebAppNavigationItem(string Label, string Path);

public sealed record RoleWebAppMobileNavigationItem(
    string Label,
    string Path,
    string Icon);

public sealed record RoleWebAppSwitchItem(
    string Code,
    string Label,
    string BasePath);

public sealed record RoleWebAppDescriptor(
    string Code,
    string Label,
    string Description,
    string HomePath,
    string AccentClass,
    IReadOnlyList<RoleWebAppNavigationItem> NavigationItems)
{
    public const string CommunityAssemblyName = "Ssalddel.Web.CommunityApp";
    public const string OrdererAssemblyName = "Ssalddel.Web.OrdererApp";
    public const string ShipperAssemblyName = "Ssalddel.Web.ShipperApp";
    public const string DriverAssemblyName = "Ssalddel.Web.DriverApp";
    public const string WarehouseAssemblyName = "Ssalddel.Web.WarehouseApp";

    public static IReadOnlyList<RoleWebAppSwitchItem> Apps { get; } =
    [
        new("01", "커뮤니티", "/roles/01/"),
        new("02", "주문자", "/roles/02/"),
        new("03", "화주", "/roles/03/"),
        new("04", "기사", "/roles/04/"),
        new("05", "창고", "/roles/05/")
    ];

    public string HeaderTitle => Code switch
    {
        "01" => "살뜰",
        "02" => "살뜰 주문자",
        "03" => "살뜰",
        "04" => "살뜰 기사",
        "05" => "살뜰 창고",
        _ => $"살뜰 {Label}"
    };

    public string HeaderSubtitle => Code switch
    {
        "02" => "주문 탐색",
        "03" => "화주 · 판매자",
        "04" => "운행 업무",
        "05" => "일반 입출고",
        _ => string.Empty
    };

    public string HeaderUtilityLabel => Code switch
    {
        "02" => "가격",
        "03" => "KO",
        "04" => "알림",
        "05" => "A-01",
        _ => string.Empty
    };

    public string HeaderUtilityPath => Code switch
    {
        "02" => "/information/kamis-domestic-price-comparison",
        "03" => "/shipper/settings/profile",
        "04" => "/driver/notifications",
        "05" => "/warehouse/work-board",
        _ => "/community/boards/directory"
    };

    public string ProfileLabel => Code switch
    {
        "01" => "방",
        "02" => "O",
        "03" => "S",
        "04" => "D",
        "05" => "W",
        _ => Code
    };

    public IReadOnlyList<RoleWebAppMobileNavigationItem> MobileNavigationItems
        => Code switch
        {
            "01" =>
            [
                new("지도", "/community/map", Icons.Material.Filled.Map),
                new("내 정보", "/community/me", Icons.Material.Filled.PersonOutline),
                new("내 글", "/community/me/posts", Icons.Material.Filled.Article)
            ],
            "02" =>
            [
                new("홈", "/orderer", Icons.Material.Filled.Home),
                new("가격", "/information/kamis-domestic-price-comparison", Icons.Material.Filled.PriceCheck),
                new("같이 주문", "/community/group-purchase", Icons.Material.Filled.GroupWork),
                new("수입 목표", "/orderer/group-import/fcl-goal", Icons.Material.Filled.Inventory2)
            ],
            "03" =>
            [
                new("홈", "/shipper", Icons.Material.Filled.Home),
                new("의뢰", "/shipper/request", Icons.Material.Filled.AddRoad),
                new("입고", "/shipper/inbound/dashboard", Icons.Material.Filled.MoveToInbox),
                new("판매", "/shipper/sales/orders", Icons.Material.Filled.Storefront)
            ],
            "04" =>
            [
                new("홈", "/driver", Icons.Material.Filled.Home),
                new("추천", "/driver/recommendations", Icons.Material.Filled.Route),
                new("운송", "/driver/transports/current", Icons.Material.Filled.LocalShipping),
                new("정산", "/driver/settlements/current-month", Icons.Material.Filled.Payments)
            ],
            "05" =>
            [
                new("홈", "/warehouse", Icons.Material.Filled.Home),
                new("입고", "/warehouse/inbounds/expected", Icons.Material.Filled.MoveToInbox),
                new("작업", "/warehouse/work-board", Icons.Material.Filled.ViewKanban),
                new("출고", "/warehouse/general/outbound-plan-review", Icons.Material.Filled.Outbox)
            ],
            _ => []
        };

    public static RoleWebAppDescriptor FromAssembly(Assembly? assembly)
        => FromAssembly(assembly?.GetName().Name);

    public static RoleWebAppDescriptor FromAssembly(string? assemblyName)
        => assemblyName switch
        {
            CommunityAssemblyName => new(
                "01",
                "커뮤니티",
                "지역 이야기와 공공정보에서 공동행동을 시작합니다.",
                "/community/map",
                "role-shell--community",
                [
                    new("생활 지도", "/community/map"),
                    new("생활 교류", "/community/exchange"),
                    new("게시판 전체", "/community/boards/directory"),
                    new("지역 문화·특산물", "/community/regions"),
                    new("공동행동", "/community/actions"),
                    new("내 활동", "/community/me")
                ]),
            OrdererAssemblyName => new(
                "02",
                "주문자",
                "가격을 비교하고 같이 주문하거나 같이 수입할 의향을 검토합니다.",
                "/orderer",
                "role-shell--orderer",
                [
                    new("주문자 홈", "/orderer"),
                    new("KAMIS 국내 가격", "/information/kamis-domestic-price-comparison"),
                    new("같이 주문", "/community/group-purchase/demand"),
                    new("같이 수입", "/community/group-import"),
                    new("FCL 공동목표", "/orderer/group-import/fcl-goal"),
                    new("수입 원장", "/orderer/ledgers/individual-import")
                ]),
            ShipperAssemblyName => new(
                "03",
                "화주",
                "운송 의뢰와 입고·수입 물류 준비를 관리합니다.",
                "/shipper",
                "role-shell--shipper",
                [
                    new("화주 홈", "/shipper"),
                    new("운송 의뢰", "/shipper/request"),
                    new("의뢰 검토", "/shipper/request/review"),
                    new("입고 관리", "/shipper/inbound/dashboard"),
                    new("FCL·LCL 검토", "/shipper/international/fcl-lcl"),
                    new("전체 업무", "/shipper/workspace")
                ]),
            DriverAssemblyName => new(
                "04",
                "기사",
                "추천 운송과 현재 운송, 증빙과 정산을 관리합니다.",
                "/driver",
                "role-shell--driver",
                [
                    new("기사 홈", "/driver"),
                    new("추천 운송", "/driver/recommendations"),
                    new("커뮤니티 개별 의뢰", "/driver/community-requests"),
                    new("탐색 문의", "/driver/exploration/campaigns"),
                    new("운행 예약", "/driver/reservations"),
                    new("현재 운송", "/driver/transports/current"),
                    new("운송 이력", "/driver/transports/history"),
                    new("정산", "/driver/settlements/current-month")
                ]),
            WarehouseAssemblyName => new(
                "05",
                "창고",
                "입고·재고·피킹·포장·운송 인계를 관리합니다.",
                "/warehouse",
                "role-shell--warehouse",
                [
                    new("창고 홈", "/warehouse"),
                    new("입고 예정", "/warehouse/inbounds/expected"),
                    new("입고 검수", "/warehouse/work/inbound/inspection"),
                    new("재고", "/warehouse/inventory"),
                    new("피킹", "/warehouse/work/picking-batch"),
                    new("작업 보드", "/warehouse/work-board"),
                    new("예외 처리", "/warehouse/exceptions"),
                    new("작업 이력", "/warehouse/history"),
                    new("창고 설정", "/warehouse/settings"),
                    new("보세·통관 상태", "/warehouse/bonded-customs")
                ]),
            _ => throw new InvalidOperationException(
                $"지원하지 않는 역할 WebApp assembly입니다: {assemblyName ?? "(null)"}")
        };

    public string ToRelativePath(string path)
        => path.TrimStart('/');
}
