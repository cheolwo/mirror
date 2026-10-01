using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public static class 창고운영ProfileCodes
{
    public const string 일반입출고 = "general-inout";
    public const string 보세수입 = "bonded-import";
    public const string 마트도심 = "urban-mart";
    public const string 공동주택물류 = "apartment-logistics";

    public static IReadOnlyList<string> 전체 { get; } =
    [
        일반입출고,
        보세수입,
        마트도심,
        공동주택물류
    ];

    public static bool 지원함(string? profileCode)
        => 전체.Contains(profileCode, StringComparer.OrdinalIgnoreCase);

    public static string 정규화(string? profileCode)
        => 전체.FirstOrDefault(code => string.Equals(code, profileCode, StringComparison.OrdinalIgnoreCase))
           ?? 일반입출고;
}

public static class 창고PageCodes
{
    public const string 홈 = "warehouse-home";
    public const string 작업보드 = "warehouse-work-board";
    public const string 작업시작 = "warehouse-work-start";
    public const string 작업대스캔 = "warehouse-workbench-scan";
    public const string 스캔 = "warehouse-scan";
    public const string 입고예정조회 = "warehouse-expected-inbounds";
    public const string 예외처리 = "warehouse-exception";
    public const string 작업이력 = "warehouse-history";
    public const string 설정 = "warehouse-settings";

    public const string 일반입고 = "general-inbound";
    public const string 일반재고 = "general-inventory";
    public const string 일반출고 = "general-outbound";
    public const string 일반운송인계 = "general-transport-handoff";

    public const string 수입화물반입 = "import-arrival";
    public const string 보세통관상태 = "bonded-customs-status";
    public const string 수입화물반출 = "import-release";
    public const string 수입국내운송인계 = "import-domestic-handoff";

    public const string 마트재고보충 = "mart-replenishment";
    public const string 마트주문처리 = "mart-order-fulfillment";
    public const string 마트피킹포장 = "mart-picking-packing";
    public const string 마트기사픽업 = "mart-driver-pickup";

    public const string 공동주택반입예정 = "apartment-arrival-schedule";
    public const string 공동주택입고확인 = "apartment-inbound-confirmation";
    public const string 공동주택세대배분 = "apartment-household-allocation";
    public const string 공동주택수령인계 = "apartment-resident-handoff";
    public const string 공동주택미수령관리 = "apartment-unclaimed-management";
}

public sealed record 창고운영ProfileDefinition(
    string 코드,
    string 표시명,
    string 설명);

public static class 창고운영ProfileCatalog
{
    public static IReadOnlyList<창고운영ProfileDefinition> 전체 { get; } =
    [
        new(
            창고운영ProfileCodes.일반입출고,
            "일반 입출고 창고",
            "입고, 검수, 적재, 재고, 피킹, 포장과 운송 인계를 처리합니다."),
        new(
            창고운영ProfileCodes.보세수입,
            "보세·수입 창고",
            "수입 화물 반입, 통관 상태 확인, 반출과 국내 운송 인계를 처리합니다."),
        new(
            창고운영ProfileCodes.마트도심,
            "마트 도심 창고",
            "재고 보충, 주문 단위 피킹·포장과 배달 기사 픽업을 처리합니다."),
        new(
            창고운영ProfileCodes.공동주택물류,
            "공동주택 물류 거점",
            "반출 완료 화물을 단지에 반입하고 세대별로 배분하여 입주민에게 인계합니다.")
    ];

    public static 창고운영ProfileDefinition 조회(string? profileCode)
    {
        var normalized = 창고운영ProfileCodes.정규화(profileCode);
        return 전체.First(profile => string.Equals(profile.코드, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record 창고PageDefinition(
    string 페이지코드,
    string 표시명,
    string 설명,
    string? 경로,
    int 순서)
{
    public bool 화면연결됨 => !string.IsNullOrWhiteSpace(경로);
}

public interface I창고작업구성Provider
{
    string 운영ProfileCode { get; }
    IReadOnlyList<창고PageDefinition> 전용페이지목록 { get; }
}

public interface I창고작업구성Resolver
{
    창고운영ProfileDefinition Profile조회(string? profileCode);
    IReadOnlyList<창고PageDefinition> 페이지목록조회(string? profileCode);
}

public sealed class 창고작업구성Resolver : I창고작업구성Resolver
{
    private static readonly IReadOnlyList<창고PageDefinition> 공통페이지목록 =
    [
        new(창고PageCodes.홈, "창고 홈", "현재 창고와 오늘의 작업 현황을 확인합니다.", WarehouseManagerRoutes.Warehouse, 10),
        new(창고PageCodes.작업보드, "작업 보드", "처리할 작업을 상태별로 조회합니다.", WarehouseManagerRoutes.WorkBoard, 20),
        new(창고PageCodes.스캔, "스캔 스테이션", "작업대, 상품과 위치 바코드를 확인합니다.", WarehouseManagerRoutes.Scan, 30),
        new(창고PageCodes.입고예정조회, "입고 예정 조회", "업체와 예정 품목을 기준으로 입고 대상을 조회합니다.", WarehouseManagerRoutes.ExpectedInbounds, 35),
        new(창고PageCodes.예외처리, "예외 처리", "수량 차이와 작업 실패를 처리합니다.", WarehouseManagerRoutes.WarehouseExceptions, 80),
        new(창고PageCodes.작업이력, "작업 이력", "작업, 정정과 취소 이력을 확인합니다.", WarehouseManagerRoutes.WarehouseHistory, 90),
        new(창고PageCodes.설정, "창고 설정", "창고 운영 역할과 담당자를 관리합니다.", WarehouseManagerRoutes.WarehouseSettings, 100)
    ];

    private readonly IReadOnlyDictionary<string, I창고작업구성Provider> _providers;

    public 창고작업구성Resolver(IEnumerable<I창고작업구성Provider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.운영ProfileCode,
            StringComparer.OrdinalIgnoreCase);

        var missingProfiles = 창고운영ProfileCodes.전체
            .Where(profileCode => !_providers.ContainsKey(profileCode))
            .ToArray();
        if (missingProfiles.Length > 0)
        {
            throw new InvalidOperationException($"창고 작업 구성 Provider가 없습니다: {string.Join(", ", missingProfiles)}");
        }
    }

    public 창고운영ProfileDefinition Profile조회(string? profileCode)
        => 창고운영ProfileCatalog.조회(profileCode);

    public IReadOnlyList<창고PageDefinition> 페이지목록조회(string? profileCode)
    {
        var normalized = 창고운영ProfileCodes.정규화(profileCode);
        return 공통페이지목록
            .Concat(_providers[normalized].전용페이지목록)
            .OrderBy(page => page.순서)
            .ToArray();
    }
}

public sealed class 일반입출고작업구성Provider : I창고작업구성Provider
{
    public string 운영ProfileCode => 창고운영ProfileCodes.일반입출고;

    public IReadOnlyList<창고PageDefinition> 전용페이지목록 { get; } =
    [
        new(창고PageCodes.일반입고, "입고 작업", "상품 확인, 검수와 적재를 처리합니다.", WarehouseManagerRoutes.InboundProductScan, 40),
        new(창고PageCodes.일반재고, "재고 현황", "재고 수량과 보관 위치를 확인합니다.", WarehouseManagerRoutes.GeneralInventory, 50),
        new(창고PageCodes.일반출고, "출고 작업", "피킹과 포장을 처리합니다.", WarehouseManagerRoutes.PickingBatch, 60),
        new(창고PageCodes.일반운송인계, "운송 인계", "출고 화물을 운송 업무로 인계합니다.", WarehouseManagerRoutes.GeneralTransportHandoff, 70)
    ];
}

public sealed class 보세수입작업구성Provider : I창고작업구성Provider
{
    public string 운영ProfileCode => 창고운영ProfileCodes.보세수입;

    public IReadOnlyList<창고PageDefinition> 전용페이지목록 { get; } =
    [
        new(창고PageCodes.수입화물반입, "수입 화물 반입", "선적·도착 예정과 실제 반입을 확인합니다.", WarehouseManagerRoutes.ImportArrival, 40),
        new(창고PageCodes.보세통관상태, "보세·통관 상태", "통관과 보세 상태 및 관련 문서를 확인합니다.", WarehouseManagerRoutes.ImportCustoms, 50),
        new(창고PageCodes.수입화물반출, "수입 화물 반출", "반출 가능 여부와 반출 지시를 관리합니다.", WarehouseManagerRoutes.ImportRelease, 60),
        new(창고PageCodes.수입국내운송인계, "국내 운송 인계", "반출 화물을 국내 운송으로 연결합니다.", WarehouseManagerRoutes.ImportDomesticHandoff, 70)
    ];
}

public sealed class 마트도심작업구성Provider : I창고작업구성Provider
{
    public string 운영ProfileCode => 창고운영ProfileCodes.마트도심;

    public IReadOnlyList<창고PageDefinition> 전용페이지목록 { get; } =
    [
        new(창고PageCodes.마트재고보충, "마트 재고 보충", "판매 구역과 피킹 구역의 재고를 보충합니다.", WarehouseManagerRoutes.MartReplenishmentWorkStart, 40),
        new(창고PageCodes.마트주문처리, "마트 주문 처리", "주문별 상품과 대체 상품을 확인합니다.", WarehouseManagerRoutes.MartWorkBoard, 50),
        new(창고PageCodes.마트피킹포장, "마트 피킹·포장", "주문 단위 피킹과 포장을 처리합니다.", WarehouseManagerRoutes.MartPickingPacking, 60),
        new(창고PageCodes.마트기사픽업, "배달 기사 픽업", "기사 확인 후 주문 상품을 인계합니다.", WarehouseManagerRoutes.MartDeliveryPickupWorkStart, 70)
    ];
}

public sealed class 공동주택물류작업구성Provider : I창고작업구성Provider
{
    public string 운영ProfileCode => 창고운영ProfileCodes.공동주택물류;

    public IReadOnlyList<창고PageDefinition> 전용페이지목록 { get; } =
    [
        new(창고PageCodes.공동주택반입예정, "공동주택 반입 예정", "보세창고에서 반출된 화물과 도착 일정을 확인합니다.", WarehouseManagerRoutes.ApartmentArrivals, 40),
        new(창고PageCodes.공동주택입고확인, "공동주택 입고 확인", "단지 도착 수량과 화물 상태를 확인합니다.", WarehouseManagerRoutes.ApartmentInbound, 50),
        new(창고PageCodes.공동주택세대배분, "세대별 배분", "공동구매 물품을 주문 세대별로 배분합니다.", WarehouseManagerRoutes.ApartmentAllocation, 60),
        new(창고PageCodes.공동주택수령인계, "입주민 수령 인계", "입주민 수령 또는 세대 배송을 확인합니다.", WarehouseManagerRoutes.ApartmentHandoff, 70),
        new(창고PageCodes.공동주택미수령관리, "미수령 관리", "미수령 물품과 반송 대상을 관리합니다.", WarehouseManagerRoutes.ApartmentUnclaimed, 75)
    ];
}
