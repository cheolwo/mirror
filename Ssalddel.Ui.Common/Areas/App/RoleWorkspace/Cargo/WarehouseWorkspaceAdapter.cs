using System.Globalization;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>서버가 허용한 창고 공정만 조회하고 현재 작업의 입력 화면으로 인계합니다.</summary>
public sealed class WarehouseWorkspaceAdapter(WarehouseWorkspaceApiClient client) : IRoleWorkspaceAdapter
{
    public string RoleKey => RoleWorkspaceCatalog.Warehouse;

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var inboundGate = client.VerifyAsync("inbound", cancellationToken);
        var inventoryGate = client.VerifyAsync("mart-replenishment", cancellationToken);
        var outboundGate = client.VerifyAsync("outbound", cancellationToken);
        await Task.WhenAll(inboundGate, inventoryGate, outboundGate);
        var inbound = inboundGate.Result.IsAllowed;
        var inventory = inventoryGate.Result.IsAllowed;
        var outbound = outboundGate.Result.IsAllowed;
        if (!inbound && !inventory && !outbound)
            throw new RoleWorkspaceAccessException(403, "현재 계정에 창고 업무를 수행할 권한이 없습니다.");

        var warehouses = await client.WarehousesAsync(cancellationToken);
        var items = new List<RoleWorkspaceItem>();
        // 공정별 권한을 확인한 뒤 독립 목록을 병렬로 조회한다. 조회 실패는 빈 목록으로 바꾸지 않는다.
        var inboundTask = inbound ? client.InboundsAsync(cancellationToken) : null;
        var inspectionTask = inbound ? client.InspectionsAsync(cancellationToken) : null;
        var inventoryTask = inventory ? client.PutAwayAsync(cancellationToken) : null;
        var pickingTask = outbound ? client.PickingAsync(cancellationToken) : null;
        var packingTask = outbound ? client.PackingAsync(cancellationToken) : null;
        var handoffTask = outbound ? client.HandoffAsync(cancellationToken) : null;
        var reviewTask = outbound ? client.OutboundReviewsAsync(cancellationToken) : null;
        await Task.WhenAll(new Task?[] { inboundTask, inspectionTask, inventoryTask, pickingTask, packingTask, handoffTask, reviewTask }.OfType<Task>());
        if (inboundTask is not null)
            items.AddRange(inboundTask.Result.Items.Where(item => item.상태 != 입고상태코드.완료 && item.상태 != 입고상태코드.취소)
                .Select(MapInbound));
        if (inspectionTask is not null)
            items.AddRange(inspectionTask.Result.Items.Select(MapInspection));
        if (inventoryTask is not null)
            items.AddRange(inventoryTask.Result.Items.Select(MapPutAway));
        if (pickingTask is not null)
            items.AddRange(pickingTask.Result.Items.Where(item => item.Status != 피킹작업조회상태코드.완료).Select(MapPicking));
        if (packingTask is not null)
            items.AddRange(packingTask.Result.Items.Select(MapPacking));
        if (handoffTask is not null)
            items.AddRange(handoffTask.Result.Items.Select(MapHandoff));
        if (reviewTask is not null)
            items.AddRange(reviewTask.Result.Items.Where(item => item.OutboundStatus is not ("출고완료" or "취소")).Select(MapOutbound));

        var selected = string.IsNullOrWhiteSpace(selectedId)
            ? items.FirstOrDefault(CanAct)?.Id ?? items.FirstOrDefault()?.Id : selectedId.Trim();
        if (selected is not null)
        {
            var (kind, id) = ParseId(selected);
            if (!(kind switch { "inbound" or "inspection" => inbound, "put-away" => inventory, _ => outbound }))
                throw new RoleWorkspaceAccessException(403, "현재 계정에 선택한 창고 공정의 권한이 없습니다.");
            var (detail, completed, linkedNext) = await LoadSelectedAsync(kind, id, cancellationToken);
            if (detail.Id != selected) throw new InvalidOperationException("선택한 창고 업무와 조회 결과가 일치하지 않습니다.");
            items.RemoveAll(item => item.Id == selected);
            items.Insert(0, detail);
            // 수령 원장과 새 재고 ID, 인계 준비 재고와 출고예정 ID는 서로 다른 식별자다.
            if (completed && kind == "inbound")
                linkedNext = inspectionTask?.Result.Items.FirstOrDefault(item => CargoWorkspaceRoutes.Number(item.InboundId) == id && item.CanInspect) is { } received
                    ? "inspection:" + CargoWorkspaceRoutes.Number(received.InboundItemId) : null;
            if (completed && kind is "inspection" or "packing")
            {
                var nextKind = kind == "inspection" ? "put-away" : "handoff";
                linkedNext = items.FirstOrDefault(item => item.Id == nextKind + ":" + id && CanAct(item))?.Id;
            }
            var next = linkedNext ?? items.FirstOrDefault(CanAct)?.Id;
            if (completed && next is not null && next != selected)
            {
                selected = next;
                var (nextKind, nextId) = ParseId(selected);
                var (nextDetail, nextCompleted, _) = await LoadSelectedAsync(nextKind, nextId, cancellationToken);
                if (nextDetail.Id != selected) throw new InvalidOperationException("다음 창고 업무와 조회 결과가 일치하지 않습니다.");
                if (linkedNext is not null && kind == "inbound" && nextKind == "inspection"
                    && Field(nextDetail, "입고 요청") != id)
                    throw new InvalidOperationException("수령한 입고 요청과 검수 재고가 일치하지 않습니다.");
                if (linkedNext is not null && kind == "handoff" && nextKind == "outbound"
                    && Field(nextDetail, "입고 상품") != id)
                    throw new InvalidOperationException("인계 준비 재고와 출고예정이 일치하지 않습니다.");
                items.RemoveAll(item => item.Id == selected);
                items.Insert(0, nextDetail);
                // 오래된 준비 링크가 이미 실물 인계까지 끝난 원장을 가리키면 같은 조회에서 현재 작업을 찾는다.
                if (kind == "handoff" && linkedNext is not null && nextKind == "outbound" && nextCompleted)
                {
                    foreach (var candidate in items.Where(item => item.Id != selected && CanAct(item)).ToArray())
                    {
                        var (candidateKind, candidateId) = ParseId(candidate.Id);
                        var (candidateDetail, candidateCompleted, _) = await LoadSelectedAsync(candidateKind, candidateId, cancellationToken);
                        if (candidateDetail.Id != candidate.Id) throw new InvalidOperationException("현재 창고 업무와 조회 결과가 일치하지 않습니다.");
                        items.RemoveAll(item => item.Id == candidate.Id);
                        items.Insert(0, candidateDetail);
                        if (!candidateCompleted && CanAct(candidateDetail)) { selected = candidateDetail.Id; break; }
                    }
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        items = items.Select(item => AddWarehouseMarker(item, warehouses.Items) with { IsCurrent = item.Id == selected }).ToList();
        var limited = !inbound || !inventory || !outbound;
        return new(RoleKey, items, limited ? "현재 계정에 허용된 창고 공정만 표시합니다."
            : items.Count == 0 ? "현재 처리할 창고 업무가 없습니다." : null, selected);
    }

    public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        => throw new InvalidOperationException("수량과 현장 확인이 필요한 창고 업무는 입력 화면에서 진행해 주세요.");
    public void Clear() { }

    private static bool CanAct(RoleWorkspaceItem item) => item.Actions?.Any(action => action.IsPrimary && action.Enabled) == true;
    private static string? Field(RoleWorkspaceItem item, string label)
        => item.Sections?.SelectMany(section => section.Fields).FirstOrDefault(field => field.Label == label)?.Value;

    private async Task<(RoleWorkspaceItem Item, bool Completed, string? LinkedNext)> LoadSelectedAsync(string kind, string id, CancellationToken ct)
    {
        // 다음 업무 전환은 행동 비활성만으로 판단하지 않고 서버가 제공한 완료 근거를 확인한다.
        switch (kind)
        {
            case "inbound":
                var inbound = await client.DetailAsync<입고요청항목응답>(kind, id, ct);
                return (MapInbound(inbound), inbound.상태 is 입고상태코드.완료 or 입고상태코드.취소, null);
            case "inspection":
                var inspection = await client.DetailAsync<입고검수대상상세응답>(kind, id, ct);
                return (MapInspectionDetail(inspection), inspection.InspectedAtUtc > DateTime.MinValue && !inspection.CanInspect, null);
            case "put-away":
                var putAway = await client.DetailAsync<적재작업상세응답>(kind, id, ct);
                return (MapPutAwayDetail(putAway), putAway.PutAwayAtUtc > DateTime.MinValue && !putAway.CanPutAway, null);
            case "picking":
                var picking = await client.DetailAsync<피킹작업상세응답>(kind, id, ct);
                return (MapPickingDetail(picking), picking.Status == 피킹작업조회상태코드.완료, null);
            case "packing":
                var packing = await client.DetailAsync<포장작업상세응답>(kind, id, ct);
                return (MapPackingDetail(packing), packing.PackedAtUtc > DateTime.MinValue && !packing.CanPack, null);
            case "handoff":
                var handoff = await client.DetailAsync<출고인계준비상세응답>(kind, id, ct);
                return (MapHandoffDetail(handoff), handoff.HandoffReadyAtUtc > DateTime.MinValue && !handoff.CanConfirmHandoff,
                    handoff.OutboundPlanId is > 0 ? "outbound:" + CargoWorkspaceRoutes.Number(handoff.OutboundPlanId.Value) : null);
            case "outbound":
                var plan = await client.DetailAsync<출고예정검토상세응답>(kind, id, ct);
                return (MapOutboundDetail(plan), plan.HandoffCompletedAtUtc > DateTime.MinValue || plan.OutboundStatus == "취소", null);
            default:
                throw new ArgumentException("지원하지 않는 창고 업무입니다.");
        }
    }

    public static (string Kind, string Id) ParseId(string itemId)
    {
        var parts = itemId.Split(':', 2);
        if (parts.Length != 2 || parts[0] is not ("inbound" or "inspection" or "put-away" or "picking" or "packing" or "handoff" or "outbound") || string.IsNullOrWhiteSpace(parts[1]))
            throw new ArgumentException("창고 업무 식별자가 올바르지 않습니다.", nameof(itemId));
        if (parts[0] != "picking") _ = CargoWorkspaceRoutes.PositiveNumber(parts[1]);
        return (parts[0], parts[1]);
    }

    public static RoleWorkspaceItem MapInbound(입고요청항목응답 item)
        => Item("inbound", CargoWorkspaceRoutes.Number(item.Id), "입고 수령 확인", item.예정상품명, item.상태, item.창고Id,
            item.예정수량?.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + (item.예정수량.HasValue ? "개" : "미확인"),
            item.예정SKU, null, item.상태 is not (입고상태코드.완료 or 입고상태코드.취소),
            [CargoWorkspacePresentation.Section("반입 정보", ("공급처", item.공급처명), ("보관 조건", item.보관조건),
                ("입고 방식", 입고흐름유형코드.GetDisplayName(item.입고흐름유형)))]);
    public static RoleWorkspaceItem MapInspection(입고검수대상목록항목응답 item)
        => Item("inspection", CargoWorkspaceRoutes.Number(item.InboundItemId), "입고 검수", item.ProductName, item.InventoryStatus,
            item.WarehouseId, item.ReceivedQuantity + "개", item.Sku, null, item.CanInspect,
            [CargoWorkspacePresentation.Section("수령 기록", ("입고 요청", CargoWorkspaceRoutes.Number(item.InboundId)),
                ("수령 수량", item.ReceivedQuantity + "개"), ("불량 수량", item.DefectiveQuantity + "개"))]);
    public static RoleWorkspaceItem MapPutAway(적재작업목록항목응답 item)
        => Item("put-away", CargoWorkspaceRoutes.Number(item.InboundItemId), "적재 확인", item.ProductName, item.InventoryStatus,
            item.WarehouseId, item.AvailableQuantity + "개", item.Sku, item.StorageLocation, item.CanPutAway);
    public static RoleWorkspaceItem MapPicking(피킹작업목록항목응답 item)
        => Item("picking", item.TaskKey, "피킹 확인", item.ProductName, item.Status, item.WarehouseId,
            item.Quantity + "개", item.Sku, item.RackCode, item.Status != 피킹작업조회상태코드.완료);
    public static RoleWorkspaceItem MapPacking(포장작업목록항목응답 item)
        => Item("packing", CargoWorkspaceRoutes.Number(item.InboundItemId), "포장 확인", item.ProductName, item.InventoryStatus,
            item.WarehouseId, item.AvailableQuantity + "개", item.Sku, item.StorageLocation, item.CanPack);
    public static RoleWorkspaceItem MapHandoff(출고인계준비목록항목응답 item)
        => Item("handoff", CargoWorkspaceRoutes.Number(item.InboundItemId), "출고 인계 준비", item.ProductName,
            item.IsHandoffReady ? "인계 준비 완료" : "인계 준비", item.WarehouseId, item.HandoffQuantity + "개", item.Sku, item.StorageLocation, !item.IsHandoffReady);
    private static RoleWorkspaceItem MapPutAwayDetail(적재작업상세응답 item)
        => Item("put-away", CargoWorkspaceRoutes.Number(item.InboundItemId), "적재 확인", item.ProductName, item.InventoryStatus,
            item.WarehouseId, item.AvailableQuantity + "개", item.Sku, item.StorageLocation, item.CanPutAway);
    private static RoleWorkspaceItem MapPickingDetail(피킹작업상세응답 item)
        => Item("picking", item.TaskKey, "피킹 확인", item.ProductName, item.Status, item.WarehouseId,
            item.Quantity + "개", item.Sku, item.RackCode, item.CanStart || item.CanComplete,
            [CargoWorkspacePresentation.Section("다음 행동", ("안내", item.NextStep))]);
    private static RoleWorkspaceItem MapPackingDetail(포장작업상세응답 item)
        => Item("packing", CargoWorkspaceRoutes.Number(item.InboundItemId), "포장 확인", item.ProductName, item.InventoryStatus,
            item.WarehouseId, item.AvailableQuantity + "개", item.Sku, item.StorageLocation, item.CanPack);
    private static RoleWorkspaceItem MapHandoffDetail(출고인계준비상세응답 item)
        => Item("handoff", CargoWorkspaceRoutes.Number(item.InboundItemId), "출고 인계 준비", item.ProductName, item.OutboundStatus,
            item.WarehouseId, item.AvailableQuantity + "개", item.Sku, item.StorageLocation, item.CanConfirmHandoff);
    private static RoleWorkspaceItem MapInspectionDetail(입고검수대상상세응답 item)
        => Item("inspection", CargoWorkspaceRoutes.Number(item.InboundItemId), "입고 검수", item.ProductName, item.InventoryStatus,
            item.WarehouseId, item.ReceivedQuantity + "개", item.Sku, item.StorageLocation, item.CanInspect,
            [CargoWorkspacePresentation.Section("수령·검수 정보", ("입고 요청", CargoWorkspaceRoutes.Number(item.InboundId)),
                ("수령 수량", item.ReceivedQuantity + "개"), ("가용 수량", item.AvailableQuantity + "개"),
                ("불량 수량", item.DefectiveQuantity + "개"), ("보관 조건", item.StorageCondition))]);
    public static RoleWorkspaceItem MapOutbound(출고예정검토목록항목응답 item)
        => Item("outbound", CargoWorkspaceRoutes.Number(item.OutboundPlanId), "출고예정 검토", item.ProductName,
            item.OutboundStatus, item.WarehouseId, item.Quantity + "개", item.Sku, null, true,
            [CargoWorkspacePresentation.Section("운송 연결", ("운송의뢰", item.TransportRequestId), ("검토 상태", item.ReviewStatus))]);
    private static RoleWorkspaceItem MapOutboundDetail(출고예정검토상세응답 item)
    {
        var action = item.CanCompleteHandoff ? "기사에게 실제 인계" : string.IsNullOrWhiteSpace(item.TransportRequestId)
            ? "출고예정 검토" : "기사 인계 상태 확인";
        var result = Item("outbound", CargoWorkspaceRoutes.Number(item.OutboundPlanId), action, item.ProductName,
            string.IsNullOrWhiteSpace(item.HandoffStatus) ? item.OutboundStatus : item.HandoffStatus,
            item.WarehouseId, item.Quantity + "개", item.Sku, item.StorageLocation, true,
            [CargoWorkspacePresentation.Section("출고·운송 정보", ("입고 상품", item.InboundItemId?.ToString(CultureInfo.InvariantCulture)),
                ("가용 수량", Quantity(item.AvailableQuantity)), ("예약 수량", Quantity(item.ReservedQuantity)),
                ("불량 수량", Quantity(item.DefectiveQuantity)), ("보관 조건", item.StorageCondition),
                ("운송의뢰", item.TransportRequestId), ("배차 상태", item.DispatchStatus), ("운송 상태", item.TransportStatus),
                ("안내", item.NextStep))]);
        var completed = item.HandoffCompletedAtUtc > DateTime.MinValue || item.OutboundStatus == "취소";
        return result with { Actions = [new("outbound", action,
            CargoWorkspaceRoutes.WarehouseInput(item.CanCompleteHandoff || !string.IsNullOrWhiteSpace(item.TransportRequestId) ? "transport-draft" : "outbound",
                CargoWorkspaceRoutes.Number(item.OutboundPlanId)), IsPrimary: !completed, RequiresConfirmation: false)] };
    }
    private static string Quantity(int? value) => value.HasValue ? value.Value + "개" : "미확인";

    private static RoleWorkspaceItem Item(string kind, string id, string action, string name, string status, long warehouse,
        string quantity, string sku, string? location, bool enabled, IReadOnlyList<RoleWorkspaceSection>? extra = null)
    {
        var sections = new List<RoleWorkspaceSection>
        {
            CargoWorkspacePresentation.Section("작업 물품", ("상품", CargoWorkspacePresentation.Display(name)),
                ("수량", quantity), ("SKU", sku), ("보관 위치", location)),
            CargoWorkspacePresentation.Section("작업 장소", ("창고", warehouse.ToString(CultureInfo.InvariantCulture)))
        };
        if (extra is not null) sections.AddRange(extra);
        return new(kind + ":" + id, CargoWorkspacePresentation.Display(name, action), CargoWorkspacePresentation.Display(status), action,
            sections, [new(kind, action, CargoWorkspaceRoutes.WarehouseInput(kind, id), IsPrimary: true,
                Enabled: enabled, DisabledReason: enabled ? null : "현재 상태에서는 이 업무를 처리할 수 없습니다.", RequiresConfirmation: false)],
            Summary: new("작업 창고", "창고 정보 미확인",
                [new("공정", action), new(kind switch { "inbound" => "예정 수량", "inspection" => "수령 수량", "outbound" => "출고 수량", _ => "수량" }, CargoWorkspacePresentation.Display(quantity)),
                 new("보관 위치", CargoWorkspacePresentation.Display(location))],
                extra?.SelectMany(section => section.Fields).FirstOrDefault(field => field.Label == "안내")
                    ?? extra?.SelectMany(section => section.Fields).FirstOrDefault(field => field.Label == "보관 조건")));
    }

    private static RoleWorkspaceItem AddWarehouseMarker(RoleWorkspaceItem item, IReadOnlyList<창고요약응답> warehouses)
    {
        var warehouseId = item.Sections?.SelectMany(section => section.Fields).FirstOrDefault(field => field.Label == "창고")?.Value;
        var warehouse = warehouses.FirstOrDefault(source => source.Id.ToString(CultureInfo.InvariantCulture) == warehouseId && source.IsActive);
        var sections = item.Sections?.ToList() ?? [];
        sections.RemoveAll(section => section.Title == "작업 장소");
        sections.Add(CargoWorkspacePresentation.Section("작업 장소", ("창고", warehouse?.창고명 ?? warehouseId), ("주소", warehouse?.주소)));
        var point = CargoWorkspacePresentation.Point(warehouse?.위도, warehouse?.경도);
        if (point is null)
            sections.Add(CargoWorkspacePresentation.Section("지도", ("위치", "확인된 창고 위치가 없어 목록으로 표시합니다.")));
        return item with
        {
            Sections = sections,
            Summary = item.Summary is null ? null : item.Summary with
            {
                Destination = warehouse is null ? "창고 정보 미확인"
                    : CargoWorkspacePresentation.Display(warehouse.창고명, "작업 창고")
                      + (string.IsNullOrWhiteSpace(warehouse.주소) ? string.Empty : " · " + warehouse.주소.Trim())
            },
            Markers = point is null ? [] : [new(item.Id + "|warehouse", CargoWorkspacePresentation.Display(warehouse?.창고명, "작업 창고"),
                NeighborhoodMapMarkerKinds.Pickup, point.Latitude, point.Longitude)]
        };
    }
}
