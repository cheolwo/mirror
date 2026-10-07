using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;

namespace RoleWorkspacePreview;

// 표시·독립 입력 진입만 검토하는 DTO 예시이며 실제 수령·검수·출고를 저장하지 않습니다.
internal static class PreviewWarehouseFixtures
{
    private const string Root = "api/v1/warehouse-operations/";
    public static object? Read(string path, string scenario, DateTime stamp)
    {
        var suffix = path.StartsWith(Root, StringComparison.Ordinal) ? path[Root.Length..] : "";
        var inspection = scenario == "inspection";
        var outbound = scenario is "outbound-ready" or "outbound-wait";
        if (suffix.StartsWith("inventory/inspection-targets?", StringComparison.Ordinal))
            return new 입고검수대상페이지응답 { Items = inspection ? [new() { InboundItemId = 601, InboundId = 501,
                WarehouseId = 9, WarehouseName = "창고 1", ProductName = "상온 보관 물품", Sku = "SKU-01",
                ReceivedQuantity = 12, InventoryStatus = "보관중", CanInspect = true, ReceivedAtUtc = stamp, UpdatedAtUtc = stamp }] : [] };
        if (suffix == "inventory/601/inspection-target")
            return new 입고검수대상상세응답 { InboundItemId = 601, InboundId = 501, WarehouseId = 9, WarehouseName = "창고 1",
                ProductName = "상온 보관 물품", Sku = "SKU-01", SupplierName = "공급처 1", ReceivedQuantity = 12, AvailableQuantity = 12,
                InventoryStatus = "보관중", StorageCondition = "상온", CanInspect = true, ReceivedAtUtc = stamp, UpdatedAtUtc = stamp };
        if (suffix.StartsWith("outbound-plan-reviews?", StringComparison.Ordinal))
            return new 출고예정검토목록페이지응답 { Items = outbound ? [new() { OutboundPlanId = 701, InboundItemId = 601,
                WarehouseId = 9, WarehouseName = "창고 1", ProductName = "상온 보관 물품", Sku = "SKU-01", Quantity = 12,
                OutboundStatus = "출고준비중", TransportRequestId = scenario == "outbound-ready" ? "preview-request-1" : null,
                ReviewStatus = scenario == "outbound-ready" ? "운송 연결" : "검토 대기", UpdatedAtUtc = stamp }] : [], PageSize = 50, TotalCount = outbound ? 1 : 0 };
        if (suffix == "outbound-plan-reviews/701")
        {
            var ready = scenario == "outbound-ready";
            return new 출고예정검토상세응답 { OutboundPlanId = 701, InboundItemId = 601, WarehouseId = 9, WarehouseName = "창고 1",
                ProductName = "상온 보관 물품", Sku = "SKU-01", Quantity = 12, OutboundStatus = "출고준비중",
                AvailableQuantity = 12, InventoryStatus = "포장완료-일반포장", StorageLocation = "보관 위치 1", PackagingType = "일반포장",
                WarehouseActive = true, PickupAddressConfigured = true, TransportRequestId = ready ? "preview-request-1" : null,
                TransportRequestStatus = ready ? "배차확정" : "", DispatchStatus = ready ? "배차확정" : "",
                TransportStatus = ready ? "상차지도착" : "", HandoffStatus = "인계 대기", AssignedDriverId = ready ? "preview-driver-1" : null,
                AssignedDriverVehicle = ready ? "화물 차량 1" : "", DriverAccepted = ready, VehicleConfirmed = ready, CanCompleteHandoff = ready,
                CanStartTransportRequestDraft = !ready, ReviewStatus = ready ? "운송 연결" : "검토 대기",
                NextStep = ready ? "실제 기사와 차량을 확인하고 물품을 인계해 주세요." : "출고예정을 검토하고 운송 의뢰를 확인해 주세요.",
                HandoffReadyAtUtc = stamp, UpdatedAtUtc = stamp, DestinationAddress = "서울 중랑구 면목로, 전달 장소 1" };
        }
        if (suffix.StartsWith("put-away-tasks?", StringComparison.Ordinal)) return new 적재작업목록페이지응답();
        if (suffix.StartsWith("picking-tasks?", StringComparison.Ordinal)) return new 피킹작업목록페이지응답();
        if (suffix.StartsWith("packing-tasks?", StringComparison.Ordinal)) return new 포장작업목록페이지응답();
        if (suffix.StartsWith("outbound-handoff-tasks?", StringComparison.Ordinal)) return new 출고인계준비목록페이지응답();
        return null;
    }
}
