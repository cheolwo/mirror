using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum BaguaApi요청형식
{
    Json,
    MultipartForm
}

public enum BaguaCqrs요청유형
{
    조회,
    명령
}

/// <summary>
/// Bagua 업무 영역에서 사용하는 Controller 액션 한 개를 설명합니다.
/// 경로 매개변수는 실제 원장·업무가 선택된 뒤 채웁니다.
/// </summary>
public sealed record BaguaApi기능정의(
    string ControllerKey,
    string Key,
    HttpMethod Method,
    string RelativePath,
    string 표시명,
    BaguaApi요청형식 요청형식)
{
    public bool 경로값필요 => RelativePath.Contains('{');
    public bool JsonClient호출가능 => 요청형식 == BaguaApi요청형식.Json;
    public BaguaCqrs요청유형 Cqrs요청유형
        => Method.Equals(HttpMethod.Get) ? BaguaCqrs요청유형.조회 : BaguaCqrs요청유형.명령;
    public 업무조각유형 업무유형 => 분류();
    public bool 명령다이얼로그사용가능
        => Cqrs요청유형 == BaguaCqrs요청유형.명령 && JsonClient호출가능;
    public 명령다이얼로그정책? 다이얼로그정책
        => 명령다이얼로그사용가능 ? 명령다이얼로그정책.기본(표시명, 업무유형) : null;

    private 업무조각유형 분류()
    {
        if (Method.Equals(HttpMethod.Get))
        {
            return 경로값필요 ? 업무조각유형.상세조회 : 업무조각유형.목록조회;
        }

        if (Method.Equals(HttpMethod.Delete))
        {
            return 업무조각유형.삭제;
        }

        if (Method.Equals(HttpMethod.Put) || Method.Equals(HttpMethod.Patch))
        {
            return 업무조각유형.수정;
        }

        return Key.StartsWith("create-", StringComparison.OrdinalIgnoreCase)
               || Key.StartsWith("add-", StringComparison.OrdinalIgnoreCase)
            ? 업무조각유형.등록
            : 업무조각유형.처리;
    }
}

public sealed record Bagua업무영역정의(
    string BusinessCode,
    IReadOnlyList<string> ControllerKeys,
    IReadOnlyList<BaguaApi기능정의> Api기능);

/// <summary>
/// 5개 업무 영역과 그 영역을 실제로 처리하는 Controller API를 연결합니다.
/// 25개 전환은 이 정의를 출발·도착 모듈로 재사용합니다.
/// </summary>
public static class Bagua업무영역카탈로그
{
    public static IReadOnlyList<Bagua업무영역정의> All { get; } =
    [
        new(
            BaguaBusinessCodes.Order,
            ["common.order-ledgers"],
            [
                Api("common.order-ledgers", "detail", HttpMethod.Get, "{주문원장Id}", "주문 원장 통합 조회"),
                Api("common.order-ledgers", "view-orderer", HttpMethod.Get, "{주문원장Id}/views/orderer", "주문자 관점 조회"),
                Api("common.order-ledgers", "view-seller", HttpMethod.Get, "{주문원장Id}/views/seller", "판매자 관점 조회"),
                Api("common.order-ledgers", "view-warehouse", HttpMethod.Get, "{주문원장Id}/views/warehouse", "창고 관점 조회"),
                Api("common.order-ledgers", "view-transport", HttpMethod.Get, "{주문원장Id}/views/transport", "운송 관점 조회"),
                Api("common.order-ledgers", "attach-child", HttpMethod.Post, "{주문원장Id}/children", "하위 원장 연결"),
                Api("common.order-ledgers", "detach-child", HttpMethod.Delete, "{주문원장Id}/children/{하위원장Id}", "하위 원장 분리"),
                Api("common.order-ledgers", "signature", HttpMethod.Get, "{주문원장Id}/signature", "서명 상태 조회"),
                Api("common.order-ledgers", "prepare-signature", HttpMethod.Post, "{주문원장Id}/signature-request", "서명 요청 준비"),
                Api("common.order-ledgers", "sign", HttpMethod.Post, "{주문원장Id}/signatures", "서명 등록"),
                Api("common.order-ledgers", "request-disclosure", HttpMethod.Post, "{주문원장Id}/disclosure-requests", "원장 공개 요청"),
                Api("common.order-ledgers", "decide-disclosure", HttpMethod.Post, "{주문원장Id}/disclosure-requests/{요청Id}/decision", "원장 공개 결정"),
                Api("common.order-ledgers", "disclosure-inbox", HttpMethod.Get, "disclosure-requests/inbox", "받은 공개 요청 조회")
            ]),
        new(
            BaguaBusinessCodes.Sales,
            ["common.sales-channels"],
            [
                Api("common.sales-channels", "accounts", HttpMethod.Get, "accounts", "판매 채널 계정 조회"),
                Api("common.sales-channels", "create-account", HttpMethod.Post, "accounts", "판매 채널 계정 등록"),
                Api("common.sales-channels", "update-account", HttpMethod.Put, "accounts/{accountId}", "판매 채널 계정 수정"),
                Api("common.sales-channels", "delete-account", HttpMethod.Delete, "accounts/{accountId}", "판매 채널 계정 삭제"),
                Api("common.sales-channels", "products", HttpMethod.Get, "products", "판매 상품 조회"),
                Api("common.sales-channels", "create-product", HttpMethod.Post, "products", "판매 상품 등록"),
                Api("common.sales-channels", "update-product", HttpMethod.Put, "products/{productId}", "판매 상품 수정"),
                Api("common.sales-channels", "delete-product", HttpMethod.Delete, "products/{productId}", "판매 상품 삭제"),
                Api("common.sales-channels", "seed-products", HttpMethod.Post, "products/seed-samples", "샘플 상품 생성"),
                Api("common.sales-channels", "listings", HttpMethod.Get, "listings", "출품 조회"),
                Api("common.sales-channels", "create-listing", HttpMethod.Post, "listings", "출품 등록"),
                Api("common.sales-channels", "update-listing", HttpMethod.Put, "listings/{listingId}", "출품 수정"),
                Api("common.sales-channels", "delete-listing", HttpMethod.Delete, "listings/{listingId}", "출품 삭제")
            ]),
        new(
            BaguaBusinessCodes.Warehouse,
            ["common.warehouse-operations"],
            [
                Api("common.warehouse-operations", "warehouses", HttpMethod.Get, "warehouses", "창고 조회"),
                Api("common.warehouse-operations", "create-warehouse", HttpMethod.Post, "warehouses", "창고 생성"),
                Api("common.warehouse-operations", "update-warehouse", HttpMethod.Put, "warehouses/{warehouseId}", "창고 수정"),
                Api("common.warehouse-operations", "delete-warehouse", HttpMethod.Delete, "warehouses/{warehouseId}", "창고 삭제"),
                Api("common.warehouse-operations", "warehouse-users", HttpMethod.Get, "warehouses/{warehouseId}/users", "창고 사용자 조회"),
                Api("common.warehouse-operations", "add-warehouse-user", HttpMethod.Post, "warehouses/{warehouseId}/users", "창고 사용자 추가"),
                Api("common.warehouse-operations", "update-warehouse-user", HttpMethod.Put, "warehouses/{warehouseId}/users/{warehouseUserId}", "창고 사용자 수정"),
                Api("common.warehouse-operations", "delete-warehouse-user", HttpMethod.Delete, "warehouses/{warehouseId}/users/{warehouseUserId}", "창고 사용자 삭제"),
                Api("common.warehouse-operations", "inbounds", HttpMethod.Get, "inbounds", "입고 요청 조회"),
                Api("common.warehouse-operations", "create-inbound", HttpMethod.Post, "inbounds", "입고 요청 생성"),
                Api("common.warehouse-operations", "update-inbound", HttpMethod.Put, "inbounds/{inboundId}", "입고 요청 수정"),
                Api("common.warehouse-operations", "delete-inbound", HttpMethod.Delete, "inbounds/{inboundId}", "입고 요청 취소"),
                Api("common.warehouse-operations", "complete-inbound", HttpMethod.Post, "inbounds/{inboundId}/complete", "입고 완료"),
                Api("common.warehouse-operations", "inventory", HttpMethod.Get, "inventory", "재고 조회"),
                Api("common.warehouse-operations", "inspect", HttpMethod.Post, "inventory/{inboundItemId}/inspect", "입고 검수"),
                Api("common.warehouse-operations", "put-away", HttpMethod.Post, "inventory/{inboundItemId}/put-away", "적재 위치 배정"),
                Api("common.warehouse-operations", "pack", HttpMethod.Post, "inventory/{inboundItemId}/pack", "포장 작업"),
                Api("common.warehouse-operations", "reconsignment", HttpMethod.Post, "inventory/reconsignment", "재위탁 운송 생성")
            ]),
        new(
            BaguaBusinessCodes.Transport,
            ["shipper.requests", "common.transport-ledgers"],
            [
                Api("shipper.requests", "requests", HttpMethod.Get, "", "운송 의뢰 조회"),
                Api("shipper.requests", "public", HttpMethod.Get, "public", "공개 화물 조회"),
                Api("shipper.requests", "recommend-vehicle", HttpMethod.Post, "recommend-vehicle", "차량 추천"),
                Api("shipper.requests", "fare-estimate", HttpMethod.Post, "fare-estimate", "기준 운임 견적"),
                Api("shipper.requests", "create-request", HttpMethod.Post, "", "운송 의뢰 생성"),
                Api("shipper.requests", "request-detail", HttpMethod.Get, "{requestId}", "운송 의뢰 상세"),
                Api("shipper.requests", "update-request", HttpMethod.Put, "{requestId}", "운송 의뢰 수정"),
                Api("shipper.requests", "delete-request", HttpMethod.Delete, "{requestId}", "운송 의뢰 삭제"),
                Api("shipper.requests", "bulk-preview", HttpMethod.Post, "bulk/preview", "운송 의뢰 일괄 미리보기", BaguaApi요청형식.MultipartForm),
                Api("shipper.requests", "bulk-confirm", HttpMethod.Post, "bulk/confirm", "운송 의뢰 일괄 등록", BaguaApi요청형식.MultipartForm),
                Api("shipper.requests", "bulk-confirm-preview", HttpMethod.Post, "bulk/confirm-preview", "미리보기 확정 등록"),
                Api("shipper.requests", "offline-settlement", HttpMethod.Post, "{requestId}/settlement/offline", "현장 지급 처리"),
                Api("shipper.requests", "postpay-approve", HttpMethod.Post, "{requestId}/settlement/postpay/approve", "후불 승인"),
                Api("shipper.requests", "receipt", HttpMethod.Post, "{requestId}/settlement/receipt", "인수증 등록"),
                Api("common.transport-ledgers", "events", HttpMethod.Get, "{requestId}/events", "운송 원장 이벤트 조회")
            ]),
        new(
            BaguaBusinessCodes.Agreement,
            ["common.community-votes", "orderer.demand-votes", "orderer.negotiation"],
            [
                Api("common.community-votes", "votes", HttpMethod.Get, "", "합의 투표 조회"),
                Api("common.community-votes", "vote-detail", HttpMethod.Get, "{voteId}", "합의 투표 상세"),
                Api("common.community-votes", "create-vote", HttpMethod.Post, "", "합의 투표 생성"),
                Api("common.community-votes", "cast-vote", HttpMethod.Post, "{voteId}/votes", "투표 참여"),
                Api("common.community-votes", "close-vote", HttpMethod.Post, "{voteId}/close", "투표 마감"),
                Api("common.community-votes", "resolution-draft", HttpMethod.Post, "{voteId}/resolution-documents", "결의문 초안 생성"),
                Api("common.community-votes", "resolution-ready", HttpMethod.Post, "{voteId}/resolution-documents/ready-to-sign", "결의문 서명 가능 전환"),
                Api("common.community-votes", "resolution-sign", HttpMethod.Post, "{voteId}/resolution-documents/signatures", "결의문 서명")
            ])
    ];

    public static Bagua업무영역정의 Find(string businessCode)
        => All.FirstOrDefault(definition =>
               string.Equals(definition.BusinessCode, businessCode, StringComparison.OrdinalIgnoreCase))
           ?? throw new KeyNotFoundException($"등록되지 않은 Bagua 업무 영역입니다: {businessCode}");

    private static BaguaApi기능정의 Api(
        string controllerKey,
        string key,
        HttpMethod method,
        string relativePath,
        string 표시명,
        BaguaApi요청형식 requestFormat = BaguaApi요청형식.Json)
        => new(controllerKey, key, method, relativePath, 표시명, requestFormat);
}

/// <summary>
/// 업무 영역 하나가 사용할 Controller들과 API 기능 정의를 보관하는 하위 ViewModel입니다.
/// </summary>
public sealed class Bagua업무영역ViewModel : 조립ViewModelBase
{
    private readonly IReadOnlyDictionary<string, Controller기능ViewModel> _controllers;

    public Bagua업무영역ViewModel(
        IHongdalJsonApiClient client,
        BaguaBusinessAreaDefinition area,
        Bagua업무영역정의 definition,
        IReadOnlyDictionary<string, Controller기능정의> controllerDefinitions)
    {
        영역 = area;
        정의 = definition;
        _controllers = definition.ControllerKeys
            .Select(key => controllerDefinitions.TryGetValue(key, out var controller)
                ? 하위ViewModel등록(
                    new Controller기능ViewModel(client, controller),
                    수명소유: true)
                : throw new KeyNotFoundException($"Bagua 업무 영역에 필요한 Controller가 없습니다: {key}"))
            .ToDictionary(controller => controller.Key, StringComparer.OrdinalIgnoreCase);
    }

    public BaguaBusinessAreaDefinition 영역 { get; }
    public Bagua업무영역정의 정의 { get; }
    public string BusinessCode => 영역.BusinessCode;
    public string 표시명 => 영역.BusinessName;
    public IReadOnlyDictionary<string, Controller기능ViewModel> Controllers => _controllers;
    public IReadOnlyList<BaguaApi기능정의> Api기능 => 정의.Api기능;

    public Controller기능ViewModel Controller(string key)
        => _controllers.TryGetValue(key, out var controller)
            ? controller
            : throw new KeyNotFoundException($"{표시명} 업무에 연결되지 않은 Controller입니다: {key}");

    public BaguaApi기능정의 Api(string key)
        => Api기능.FirstOrDefault(feature =>
               string.Equals(feature.Key, key, StringComparison.OrdinalIgnoreCase))
           ?? throw new KeyNotFoundException($"{표시명} 업무에 연결되지 않은 API 기능입니다: {key}");

    public string Api경로(
        string key,
        IReadOnlyDictionary<string, string>? 경로값 = null)
    {
        var feature = Api(key);
        return Controller(feature.ControllerKey).경로(feature.RelativePath, 경로값);
    }
}

public interface IBagua업무영역ViewModelFactory
{
    IReadOnlyDictionary<string, Bagua업무영역ViewModel> CreateAll();
}

public sealed class Bagua업무영역ViewModelFactory : IBagua업무영역ViewModelFactory
{
    private static readonly IReadOnlyDictionary<string, Controller기능정의> ControllerDefinitions =
        Controller기능카탈로그.공통
            .Concat(Controller기능카탈로그.화주)
            .Concat(Controller기능카탈로그.주문자)
            .ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

    private readonly IHongdalJsonApiClient _client;

    public Bagua업무영역ViewModelFactory(IHongdalJsonApiClient client)
    {
        _client = client;
    }

    public IReadOnlyDictionary<string, Bagua업무영역ViewModel> CreateAll()
        => Bagua업무영역카탈로그.All
            .Select(definition =>
            {
                var area = BaguaTransitionCatalog.Areas.Single(candidate =>
                    string.Equals(
                        candidate.BusinessCode,
                        definition.BusinessCode,
                        StringComparison.OrdinalIgnoreCase));
                return new Bagua업무영역ViewModel(
                    _client,
                    area,
                    definition,
                    ControllerDefinitions);
            })
            .ToDictionary(viewModel => viewModel.BusinessCode, StringComparer.OrdinalIgnoreCase);
}
