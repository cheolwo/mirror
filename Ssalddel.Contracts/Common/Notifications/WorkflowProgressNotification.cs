using System.Globalization;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Contracts.Common.Notifications;

/// <summary>전문 원장의 완료를 설명할 뿐 상태 전이 또는 정산을 실행하지 않는 알림 계약.</summary>
public sealed record WorkflowProgressStage(string Code, string WorkflowKey, string Title, string Body)
{
    public string OperatingSystemId => WorkflowKey switch
    {
        "food" => OperatingSystemIds.FoodDelivery,
        "warehouse" => OperatingSystemIds.WarehouseCommerceFulfillment,
        "cargo" => OperatingSystemIds.DomesticCargoTransport,
        _ => throw new InvalidOperationException("알림 업무 영역이 올바르지 않습니다.")
    };
    public string LifecycleStageId => Code switch
    {
        "food.submitted" => OperatingSystemLifecycleStageIds.FoodOrder,
        "food.accepted" or "food.rejected" => OperatingSystemLifecycleStageIds.FoodRestaurantResponse,
        "food.cooking" or "food.ready" => OperatingSystemLifecycleStageIds.FoodCooking,
        "food.assigned" => OperatingSystemLifecycleStageIds.FoodDispatch,
        "food.arrived" or "food.picked-up" => OperatingSystemLifecycleStageIds.FoodPickup,
        "food.delivered" or "food.received" => OperatingSystemLifecycleStageIds.FoodDelivery,
        "food.cancelled" => OperatingSystemLifecycleStageIds.FoodCancellationCompensation,
        "warehouse.requested" => OperatingSystemLifecycleStageIds.WarehouseInboundPlan,
        "warehouse.received" => OperatingSystemLifecycleStageIds.WarehouseReceiving,
        "warehouse.inspected" => OperatingSystemLifecycleStageIds.WarehouseInspection,
        "warehouse.stored" => OperatingSystemLifecycleStageIds.WarehousePutAwayInventory,
        "warehouse.picking" or "warehouse.picked" => OperatingSystemLifecycleStageIds.WarehousePicking,
        "warehouse.packed" => OperatingSystemLifecycleStageIds.WarehousePacking,
        "warehouse.ready" or "warehouse.handed-over" => OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff,
        "cargo.accepted" => OperatingSystemLifecycleStageIds.CargoDispatch,
        "cargo.approaching" or "cargo.arrived-pickup" or "cargo.picked-up" => OperatingSystemLifecycleStageIds.CargoPickup,
        "cargo.arrived-dropoff" or "cargo.delivered" => OperatingSystemLifecycleStageIds.CargoDropoff,
        "cargo.issue" => OperatingSystemLifecycleStageIds.CargoInterruptionRecovery,
        _ => throw new InvalidOperationException("알림 단계가 올바르지 않습니다.")
    };
}

public static class WorkflowProgressNotification
{
    public const string MessageType = "WorkflowProgress";

    private static readonly IReadOnlyDictionary<string, WorkflowProgressStage> Stages = new[]
    {
        Stage("food.submitted", "주문 접수", "음식점 확인을 기다리고 있습니다."),
        Stage("food.accepted", "주문 수락", "음식점에서 주문을 확인했습니다."),
        Stage("food.cooking", "조리 시작", "음식을 준비하고 있습니다."),
        Stage("food.ready", "음식 준비 완료", "픽업을 기다리고 있습니다."),
        Stage("food.rejected", "주문 거절", "주문 내역을 확인해 주세요."),
        Stage("food.cancelled", "주문 취소", "주문 내역을 확인해 주세요."),
        Stage("food.assigned", "기사 배정", "기사님이 픽업지로 이동합니다."),
        Stage("food.arrived", "기사 도착", "기사님이 음식점에 도착했습니다."),
        Stage("food.picked-up", "픽업 완료", "전달지로 이동합니다."),
        Stage("food.delivered", "전달 완료", "음식을 받으셨는지 확인해 주세요."),
        Stage("food.received", "수령 확인", "주문자가 음식 수령을 확인했습니다."),
        Stage("warehouse.requested", "입고 신청", "입고 요청을 확인해 주세요."),
        Stage("warehouse.received", "입고 완료", "입고된 물품을 확인해 주세요."),
        Stage("warehouse.inspected", "검수 완료", "검수 결과를 확인해 주세요."),
        Stage("warehouse.stored", "적재 완료", "보관 상태를 확인해 주세요."),
        Stage("warehouse.picking", "피킹 시작", "물품을 찾고 있습니다."),
        Stage("warehouse.picked", "피킹 완료", "다음 작업을 확인해 주세요."),
        Stage("warehouse.packed", "포장 완료", "출고 준비를 확인해 주세요."),
        Stage("warehouse.ready", "출고 준비 완료", "기사 인계를 기다리고 있습니다."),
        Stage("warehouse.handed-over", "기사 인계 완료", "배송 진행을 확인해 주세요."),
        Stage("cargo.accepted", "기사 배정", "기사님이 운송을 수락했습니다."),
        Stage("cargo.approaching", "기사 접근", "픽업 준비를 확인해 주세요."),
        Stage("cargo.arrived-pickup", "상차지 도착", "기사님이 상차지에 도착했습니다."),
        Stage("cargo.picked-up", "상차 완료", "전달지로 이동합니다."),
        Stage("cargo.arrived-dropoff", "하차지 도착", "기사님이 하차지에 도착했습니다."),
        Stage("cargo.delivered", "전달 완료", "운송 내역을 확인해 주세요."),
        Stage("cargo.issue", "운송 확인 필요", "접수된 문제를 확인해 주세요.")
    }.ToDictionary(stage => stage.Code, StringComparer.Ordinal);

    public static IReadOnlyCollection<WorkflowProgressStage> All => Stages.Values.ToArray();
    public static WorkflowProgressStage Get(string code) => Stages.TryGetValue(code, out var stage)
        ? stage : throw new ArgumentException("지원하지 않는 업무 알림 단계입니다.", nameof(code));
    public static bool TryGet(string code, out WorkflowProgressStage stage) => Stages.TryGetValue(code, out stage!);

    // 서버가 만든 기존 앱 경로만 사용한다. 로그인 후에도 업무 API가 접근 권한을 다시 확인한다.
    public static bool IsSafeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || route.Length > 1000 || !route.StartsWith('/')
            || route.StartsWith("//", StringComparison.Ordinal) || route.Contains('\\')
            || route.Contains('#') || route.Any(char.IsControl)) return false;
        string decoded;
        try { decoded = Uri.UnescapeDataString(route); }
        catch (UriFormatException) { return false; }
        if (decoded.Contains("..", StringComparison.Ordinal) || decoded.Contains('\\') || decoded.Contains('%')
            || decoded.Contains("//", StringComparison.Ordinal)
            || decoded.Any(char.IsControl)) return false;
        var path = route.Split('?')[0];
        if (new[] { "/workspace/orderer", "/workspace/shipper", "/workspace/warehouse", "/workspace/food-driver" }
            .Contains(path, StringComparer.Ordinal))
        {
            if (!route.Contains('?')) return true;
            var query = decoded[(decoded.IndexOf('?') + 1)..];
            if (!query.StartsWith("selected=", StringComparison.Ordinal)) return false;
            var selected = query[9..];
            return selected.Length is > 0 and <= 200
                && selected.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
        }
        if (route.Contains('?')) return false;
        return new[] { "/orderer/food/", "/orderer/orders/", "/shipper/request/", "/shipper/requests/",
            "/shipper/inbound/", "/shipper/warehouse/", "/work/", "/driver/", "/fdriver/", "/warehouse/" }
            .Any(prefix => route.StartsWith(prefix, StringComparison.Ordinal));
    }

    public static bool TryParse(IReadOnlyDictionary<string, string> data, out WorkflowProgressHint? hint)
    {
        hint = null;
        string Read(string key) => data.TryGetValue(key, out var value) ? value : string.Empty;
        var stageCode = Read("stageCode");
        var eventId = Read("eventId");
        var sourceId = Read("sourceId");
        var userId = Read("userId");
        var route = Read("deepLink");
        if (Read("type") != MessageType || !TryGet(stageCode, out var stage)
            || Read("workflowKey") != stage.WorkflowKey || eventId.Length != 64 || !eventId.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 200
            || sourceId.Any(char.IsControl) || sourceId != sourceId.Trim()
            || string.IsNullOrWhiteSpace(userId) || userId.Length > 450 || userId.Any(char.IsControl)
            || userId != userId.Trim() || !IsSafeRoute(route)
            || !DateTimeOffset.TryParse(Read("occurredAtUtc"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var occurred)) return false;
        hint = new(eventId, stage.Code, stage.WorkflowKey, sourceId, userId, route, occurred.ToUniversalTime());
        return true;
    }

    private static WorkflowProgressStage Stage(string code, string title, string body)
        => new(code, code[..code.IndexOf('.')], title, body);
}

public sealed record WorkflowProgressHint(string EventId, string StageCode, string WorkflowKey,
    string SourceId, string RecipientUserId, string Route, DateTimeOffset OccurredAtUtc)
{
    public WorkflowProgressStage Stage => WorkflowProgressNotification.Get(StageCode);
}
