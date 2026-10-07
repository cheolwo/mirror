using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Admin.Dispatch;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

/// <summary>제품 adapter의 표시 경로에 DTO 예시만 공급합니다. 외부 요청과 업무 저장은 하지 않습니다.</summary>
public sealed class PreviewRoleApi(string key, IRoleWorkspaceAccess access, NavigationManager? navigation = null) : IRoleWorkspaceApi
{
    private const string FoodOrderId = "preview-order-1";
    private const string RequestId = "preview-request-1";
    private const string WarehouseRoot = "api/v1/warehouse-operations/";
    private string scenario = "progress";
    private bool noMap;
    private DateTime recordedAt = DateTime.UtcNow;

    // 완료 이력을 검토할 때 선택한 예시 ID입니다. 제품의 다음 업무 정책은 실제 adapter가 그대로 적용합니다.
    public string? DefaultSelection
    {
        get
        {
            ReadSettings();
            if (key == "restaurant" && PreviewFoodFixtures.RestaurantRequiresSelection(scenario)) return PreviewFoodFixtures.OrderId;
            return scenario == "complete" ? key switch
            {
                "restaurant" => FoodOrderId, "cargo-driver" => "101", "warehouse" => "inbound:501", _ => null
            } : scenario == "next" ? key switch { "cargo-driver" => "101", "warehouse" => "inbound:501", _ => null } : null;
        }
    }

    private void ReadSettings()
    {
        if (navigation is null) return;
        var query = new Uri(navigation.Uri).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Split('=', 2)).Where(value => value.Length == 2)
            .GroupBy(value => value[0], StringComparer.Ordinal).ToDictionary(group => group.Key,
                group => Uri.UnescapeDataString(group.Last()[1]), StringComparer.Ordinal);
        // 선택/복귀 링크가 scenario를 생략해도 같은 예시 조회가 이어집니다.
        if (query.TryGetValue("scenario", out var value) && (PreviewFoodFixtures.IsScenario(value) || value is
            ("waiting" or "progress" or "complete" or "nomap" or "expired" or "receipt" or "recooking" or "next" or "map-failure" or "sample-map"
             or "held" or "reviewed" or "inspection" or "outbound-ready" or "outbound-wait"
             or "pickupdone" or "driver-wait" or "ready-no-permission" or "ready" or "assignment")))
        {
            var missingMap = value == "nomap" || query.GetValueOrDefault("map") == "none";
            if (scenario != value || noMap != missingMap) recordedAt = DateTime.UtcNow;
            scenario = value; noMap = missingMap;
        }
    }

    private void Guard(string role, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (role != key) throw new RoleWorkspaceAccessException(403, "미리보기 역할을 확인해 주세요.");
        if (!access.GetIdentity(role).IsAuthenticated) throw new RoleWorkspaceAccessException(401, "로그인 후 업무를 확인해 주세요.");
        ReadSettings();
    }

    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
    {
        Guard(roleKey, cancellationToken);
        object value = key switch
        {
            "orderer" => ReadOrderer(path), "restaurant" => ReadRestaurant(path), "operator" => ReadOperator(path),
            "food-driver" => PreviewFoodFixtures.ReadDriver(path, scenario, noMap, recordedAt),
            "shipper" => ReadShipper(path), "cargo-driver" => ReadCargoDriver(path), "warehouse" => ReadWarehouse(path),
            _ => throw Unsupported()
        };
        return Task.FromResult((T)value);
    }

    public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
    {
        Guard(roleKey, cancellationToken);
        // 제품 창고 adapter가 조회 전에 수행하는 진입 권한 확인만 로컬에서 응답합니다.
        if (key == "warehouse" && path == WarehouseRoot + "work-entry/verify" && body is 창고작업진입확인요청 request)
            return Task.FromResult<T?>((T)(object)new 창고작업진입확인응답
            { IsAllowed = request.ProcessCode == "inbound" || scenario is "outbound-ready" or "outbound-wait", RoleName = "창고 담당자", OperatorName = "담당자 1" });
        throw new InvalidOperationException("화면 배치 미리보기입니다. 실제 업무 요청은 실행하지 않습니다.");
    }
    public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => throw new InvalidOperationException("미리보기에서는 업무를 저장하지 않습니다.");
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        => throw new InvalidOperationException("미리보기에서는 증빙을 업로드하지 않습니다.");
    private static Exception Unsupported() => new InvalidOperationException("미리보기에서 지원하지 않는 조회입니다.");

    private 주문자음식주문상세응답 Orderer() => PreviewFoodFixtures.Orderer(scenario, noMap, recordedAt);
    private object ReadOrderer(string path)
    {
        var detail = Orderer();
        return path switch
        {
            "api/v1/food-orders?page=1&pageSize=50" => new 주문자음식주문목록응답 { Items = [detail.주문], TotalCount = 1 },
            "api/v1/food-orders/" + FoodOrderId => detail, _ => throw Unsupported()
        };
    }

    private 음식주문응답 Restaurant() => PreviewFoodFixtures.Restaurant(scenario, noMap, recordedAt);
    private object ReadRestaurant(string path) => path switch
    {
        "api/v1/food-orders/restaurant/inbox/" + FoodOrderId => Restaurant(),
        _ when path.StartsWith("api/v1/food-orders/restaurant/inbox?", StringComparison.Ordinal) => new 음식점주문수신함응답
            { Items = PreviewFoodFixtures.RestaurantRequiresSelection(scenario) ? [] : [Restaurant()],
              TotalCount = PreviewFoodFixtures.RestaurantRequiresSelection(scenario) ? 0 : 1, ServerTimeUtc = DateTime.UtcNow },
        _ => throw Unsupported()
    };
    private object ReadOperator(string path)
    {
        var complete = scenario == "complete";
        var waiting = scenario == "waiting";
        if (path == "api/v1/admin/dispatch/food-delivery-ai-review")
        {
            if (scenario == "map-failure") throw new HttpRequestException("Optional preview map unavailable");
            // actual을 사용하는 제품 분기의 DTO 형식 예시이며 실제 주문 좌표라는 증거가 아닙니다.
            return new FoodDeliveryDispatchAIReviewWorkspaceDto { Source = scenario == "sample-map" ? "scope-sample" : "actual",
                Orders = noMap || complete ? [] : [new() { OrderNo = FoodOrderId, RestaurantLatitude = 37.580m,
                    RestaurantLongitude = 127.087m, CustomerLatitude = 37.583m, CustomerLongitude = 127.089m }] };
        }
        if (path == "api/v1/admin/food-orders/operations?page=1&pageSize=50")
            return new AdminFoodOrderListDto { Items = [new() { OrderNo = FoodOrderId, RestaurantName = "음식점 1",
                OrderStatus = complete ? 음식주문상태코드.수령확인 : waiting ? 음식주문상태코드.주문대기 : 음식주문상태코드.픽업완료,
                DispatchStatus = complete ? 음식주문배차상태코드.배달완료 : 음식주문배차상태코드.기사배정,
                CreatedAtUtc = recordedAt.AddMinutes(-20), UpdatedAtUtc = recordedAt }], TotalCount = 1 };
        if (path == "api/v1/admin/food-orders/" + FoodOrderId + "/operations-trace")
            return new 음식주문운영추적응답 { 주문번호 = FoodOrderId, 음식점명 = "음식점 1", 최근변경시각Utc = recordedAt, 조회시각Utc = DateTime.UtcNow,
                생명주기조화 = new() { 현재단계Code = complete ? 음식배달운영생명주기단계Codes.종료 : waiting ? 음식배달운영생명주기단계Codes.음식점응답대기 : 음식배달운영생명주기단계Codes.배송,
                    현재책임주체Codes = complete ? [] : waiting ? [음식배달운영책임주체Codes.음식점] : [음식배달운영책임주체Codes.플랫폼운영자], 운영자확인필요여부 = !complete && !waiting },
                경고목록 = complete || waiting ? [] : ["배달 중단 사유와 재배정 조건을 확인해 주세요."],
                배달시도목록 = complete || waiting ? [] : [new() { 시도StableId = "preview-attempt-1", 상태Code = 음식배달시도상태Code.중단 }] };
        throw Unsupported();
    }

    private 화주운송의뢰응답 Shipper() => new()
    {
        의뢰Id = RequestId, 픽업지 = "서울 중랑구 사가정로, 상차 장소 1", 하차지 = "서울 중랑구 면목로, 하차 장소 1",
        화물 = new() { 화물종류 = "상온 보관 물품", 수량 = 12 }, 최종운임 = 34000,
        의뢰상태 = scenario == "complete" ? "완료" : "배차확정", 운송상태 = scenario == "complete" ? "인수완료" : scenario == "waiting" ? "배차대기" : "운송중",
        배차상태 = scenario == "waiting" ? "배차대기" : "배차확정", 결제상태 = "확인 대기", 정산상태 = "미확정",
        픽업위도 = noMap ? null : 37.580m, 픽업경도 = noMap ? null : 127.087m, 하차위도 = noMap ? null : 37.583m, 하차경도 = noMap ? null : 127.089m,
        확정기사Id = scenario == "waiting" ? null : "preview-driver-1", 확정기사명 = scenario == "waiting" ? null : "기사 1", 확정기사차량 = "화물 차량 1",
        기사최근위도 = noMap || scenario is "waiting" or "complete" ? null : 37.581m,
        기사최근경도 = noMap || scenario is "waiting" or "complete" ? null : 127.087m,
        기사최근위치시각Utc = scenario is "waiting" or "complete" ? null : scenario == "expired" ? recordedAt.AddMinutes(-11) : recordedAt,
        비정상운송검토보류중 = scenario == "held",
        비정상운송사건목록 = scenario == "held" ? [new() { 사건StableId = "preview-incident", 상태Code = "OperationsReviewPending",
            업무통제상태Code = "PartiallyHeld", 보류범위Code = "AffectedQuantity", 정산보류적용여부 = true }] : []
    };
    private object ReadShipper(string path) => path switch
    {
        "api/v1/shipper/requests?page=1&pageSize=200" => new[] { Shipper() },
        "api/v1/shipper/requests/" + RequestId => Shipper(), _ => throw Unsupported()
    };

    private 기사운송상세응답 Transport(long id) => new()
    {
        Id = id, 운송번호 = id == 101 ? "운송 1" : "운송 2",
        상태 = scenario == "complete" || scenario == "next" && id == 101 ? "인수완료" : scenario is "held" or "reviewed" ? "상차지도착" : "운송중",
        출발지 = "서울 중랑구 사가정로, 상차 장소 1", 도착지 = "서울 중랑구 면목로, 하차 장소 1",
        운임 = 34000, 예상거리Km = 3.2m, 거리계산방식 = "예시 운송 거리", 전달요청 = "담당자에게 물품을 인계해 주세요.",
        픽업위도 = noMap ? null : 37.580m, 픽업경도 = noMap ? null : 127.087m,
        하차위도 = noMap ? null : 37.583m, 하차경도 = noMap ? null : 127.089m, UpdatedAt = recordedAt,
        상차시간창시작일시 = scenario is "held" or "reviewed" ? recordedAt : null,
        상차시간창종료일시 = scenario is "held" or "reviewed" ? recordedAt.AddHours(1) : null,
        인수증필요 = scenario is "held" or "reviewed",
        예외신고됨 = scenario is "held" or "reviewed", 관리자확인필요 = scenario == "held",
        운송진행보류 = scenario == "held", 정산보류 = scenario == "held", 최근예외메시지 = scenario is "held" or "reviewed" ? "현장 물품 상태 확인" : "",
        다음행동안내 = scenario == "held" ? "전체 운송이 보류되었습니다. 운영 담당자의 재개 확인을 기다려 주세요."
            : scenario == "reviewed" ? "운송 예외 검토 결과를 확인하고 현재 업무를 진행해 주세요." : "",
        가능한행동 = scenario == "held" ? ["issue"] : scenario == "reviewed" ? ["pickup", "issue"] : null,
        예외검토목록 = scenario is "held" or "reviewed" ? [new("preview-incident", "상차", "화물훼손",
            scenario == "held" ? "OperationsReviewPending" : "ActionDecided", "PlatformOperationsReview",
            scenario == "held" ? "FullyHeld" : "Resumed", scenario == "held" ? "EntireTransport" : "None", scenario == "held", 2, recordedAt)] : []
    };
    private object ReadCargoDriver(string path)
    {
        var waiting = scenario is "waiting" or "expired";
        if (path == "api/v1/driver/transports/workspace")
        {
            var transport = scenario == "complete" || waiting ? null : Transport(scenario == "next" ? 102 : 101);
            return new 기사화물운송작업공간응답 { 활성운송목록 = transport is null ? [] : [transport], 다음행동운송 = transport };
        }
        if (path == "api/v1/driver/transports/101") return Transport(101);
        if (path == "api/v1/driver/transports/102") return Transport(102);
        if (path == "api/v1/driver/recommendations")
            return waiting ? new[] { new 기사배차추천항목응답 { 의뢰Id = RequestId, 화물종류 = "상온 보관 물품",
                픽업지 = "서울 중랑구 사가정로, 상차 장소 1", 하차지 = "서울 중랑구 면목로, 하차 장소 1", 운송거리Km = 3.2m,
                픽업_위도 = noMap ? null : 37.580m, 픽업_경도 = noMap ? null : 127.087m,
                하차_위도 = noMap ? null : 37.583m, 하차_경도 = noMap ? null : 127.089m,
                추천만료시각 = scenario == "expired" ? recordedAt.AddMinutes(-1) : recordedAt.AddMinutes(10), 차량적합여부 = true } }
                : Array.Empty<기사배차추천항목응답>();
        throw Unsupported();
    }

    private 입고요청항목응답 Inbound(long id) => new()
    {
        Id = id, 창고Id = 9, 예정상품명 = "상온 보관 물품", 예정SKU = "SKU-01", 예정수량 = 12,
        공급처명 = "공급처 1", 보관조건 = "상온", CreatedAtUtc = recordedAt,
        상태 = scenario == "complete" || scenario == "next" && id == 501 ? 입고상태코드.완료
            : scenario == "waiting" ? 입고상태코드.예정 : 입고상태코드.운송중,
        입고완료일시 = scenario == "complete" || scenario == "next" && id == 501 ? recordedAt : null
    };
    private object ReadWarehouse(string path) => PreviewWarehouseFixtures.Read(path, scenario, recordedAt) ?? (path switch
    {
        WarehouseRoot + "warehouses" => new 창고목록응답 { Items = [new() { Id = 9, 창고명 = "창고 1", 주소 = "서울 중랑구 사가정로, 작업 장소 1",
            IsActive = true, 위도 = noMap ? null : 37.580m, 경도 = noMap ? null : 127.087m }] },
        WarehouseRoot + "inbounds" => new 입고요청목록응답 { Items = scenario is "complete" or "inspection" or "outbound-ready" or "outbound-wait" ? [] : [Inbound(scenario == "next" ? 502 : 501)] },
        WarehouseRoot + "inbounds/501" => Inbound(501), WarehouseRoot + "inbounds/502" => Inbound(502), _ => throw Unsupported()
    });
}
