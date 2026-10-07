using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>창고 업무 입력의 서버 허용 상태와 현장 확인을 독립된 화면에서 관리합니다.</summary>
public sealed class WarehouseTaskActionViewModel : IDisposable
{
    private readonly WarehouseWorkspaceApiClient _client;
    private 입고요청항목응답? _inbound;
    private 적재작업상세응답? _putAway;
    private 피킹작업상세응답? _picking;
    private 포장작업상세응답? _packing;
    private 출고인계준비상세응답? _handoff;
    public WarehouseTaskActionViewModel(WarehouseWorkspaceApiClient client, IRoleWorkspaceAccess access)
    {
        _client = client; Lifetime = new(access, RoleWorkspaceCatalog.Warehouse, Clear);
    }
    public CargoInputLifetime Lifetime { get; }
    public string Kind { get; private set; } = "";
    public string ItemId { get; private set; } = "";
    public bool Loaded { get; private set; }
    public bool Saved { get; private set; }
    public IReadOnlyList<RoleWorkspaceAction> NextActions { get; private set; } = [];
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public int DefectQuantity { get; set; }
    public string StorageLocation { get; set; } = "";
    public string PackagingType { get; set; } = 포장유형코드.일반포장;
    public string Memo { get; set; } = "";
    public bool FirstConfirmed { get; set; }
    public bool SecondConfirmed { get; set; }
    public bool SaveConfirmed { get; set; }
    public string Status => _inbound?.상태 ?? _putAway?.InventoryStatus ?? _picking?.Status ?? _packing?.InventoryStatus ?? _handoff?.OutboundStatus ?? "미확인";
    public string ReturnHref => CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.Warehouse, NextItemId ?? Kind + ":" + ItemId);
    public string? NextItemId { get; private set; }
    public bool CanStartPicking => _picking?.CanStart == true;
    public bool ServerAllows => Kind switch
    {
        "inbound" => _inbound is not null && _inbound.상태 is not (입고상태코드.완료 or 입고상태코드.취소),
        "put-away" => _putAway?.CanPutAway == true,
        "picking" => _picking?.CanStart == true || _picking?.CanComplete == true,
        "packing" => _packing?.CanPack == true,
        "handoff" => _handoff?.CanConfirmHandoff == true,
        _ => false
    };
    public bool CanSubmit => !Lifetime.IsBusy && !Lifetime.HasError && !Saved && Loaded && ServerAllows && SaveConfirmed
        && (CanStartPicking || FirstConfirmed && SecondConfirmed && InputValid);
    public string Title => Kind switch { "inbound" => "입고 수령 확인", "put-away" => "적재 확인", "picking" => "피킹 확인", "packing" => "포장 확인", "handoff" => "출고 인계 준비", _ => "창고 업무" };
    public string PrimaryLabel => CanStartPicking ? "피킹 시작" : Title + " 저장";
    public string FirstLabel => Kind switch { "inbound" => "반입한 물품과 수량을 확인했습니다", "put-away" => "검수 결과를 확인했습니다", "picking" => "선택한 물품을 확인했습니다", "packing" => "재고와 수량을 확인했습니다", _ => "포장 봉인을 확인했습니다" };
    public string SecondLabel => Kind switch { "inbound" => "불량 수량과 보관 위치를 확인했습니다", "put-away" => "보관 위치 라벨을 확인했습니다", "picking" => "피킹 수량을 확인했습니다", "packing" => "포장 라벨을 확인했습니다", _ => "운송 조건을 확인했습니다" };
    private bool InputValid => Kind switch
    {
        "inbound" => !string.IsNullOrWhiteSpace(ProductName) && Quantity > 0 && DefectQuantity >= 0 && DefectQuantity <= Quantity && !string.IsNullOrWhiteSpace(StorageLocation),
        "put-away" or "picking" => !string.IsNullOrWhiteSpace(StorageLocation),
        "packing" => Quantity > 0 && Quantity <= _packing?.AvailableQuantity && 포장유형코드.IsValid(PackagingType),
        "handoff" => Quantity > 0 && Quantity == _handoff?.AvailableQuantity,
        _ => false
    };

    public async Task LoadAsync(string kind, string id)
    {
        if (Kind != kind || ItemId != id) { Lifetime.Reset(); Kind = kind; ItemId = id; }
        await Lifetime.RunAsync(async ct =>
        {
            _ = WarehouseWorkspaceAdapter.ParseId(kind + ":" + id);
            await RefreshAsync(ct, initializeDraft: !Loaded);
        });
    }

    private async Task RefreshAsync(CancellationToken ct, bool initializeDraft)
    {
        switch (Kind)
        {
            case "inbound":
                var inbound = await _client.DetailAsync<입고요청항목응답>(Kind, ItemId, ct);
                Lifetime.RequireCurrent(ct); RequireId(CargoWorkspaceRoutes.Number(inbound.Id)); _inbound = inbound;
                if (initializeDraft) { ProductName = inbound.예정상품명; Sku = inbound.예정SKU; Quantity = inbound.예정수량 ?? 0; }
                break;
            case "put-away":
                var putAway = await _client.DetailAsync<적재작업상세응답>(Kind, ItemId, ct);
                Lifetime.RequireCurrent(ct); RequireId(CargoWorkspaceRoutes.Number(putAway.InboundItemId)); _putAway = putAway;
                if (initializeDraft) { ProductName = putAway.ProductName; Sku = putAway.Sku; StorageLocation = putAway.StorageLocation; }
                break;
            case "picking":
                var picking = await _client.DetailAsync<피킹작업상세응답>(Kind, ItemId, ct);
                Lifetime.RequireCurrent(ct); RequireId(picking.TaskKey); _picking = picking;
                if (initializeDraft) { ProductName = picking.ProductName; Sku = picking.Sku; Quantity = picking.Quantity; StorageLocation = picking.RackCode; }
                break;
            case "packing":
                var packing = await _client.DetailAsync<포장작업상세응답>(Kind, ItemId, ct);
                Lifetime.RequireCurrent(ct); RequireId(CargoWorkspaceRoutes.Number(packing.InboundItemId)); _packing = packing;
                if (initializeDraft) { ProductName = packing.ProductName; Sku = packing.Sku; Quantity = packing.AvailableQuantity; StorageLocation = packing.StorageLocation; }
                break;
            case "handoff":
                var handoff = await _client.DetailAsync<출고인계준비상세응답>(Kind, ItemId, ct);
                Lifetime.RequireCurrent(ct); RequireId(CargoWorkspaceRoutes.Number(handoff.InboundItemId)); _handoff = handoff;
                if (initializeDraft) { ProductName = handoff.ProductName; Sku = handoff.Sku; Quantity = handoff.AvailableQuantity; StorageLocation = handoff.StorageLocation; }
                break;
        }
        Loaded = true;
    }

    public async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        var wasStart = CanStartPicking;
        await Lifetime.RunAsync(async ct =>
        {
            IReadOnlyList<RoleWorkspaceAction> nextActions = [];
            string? nextItemId = null;
            await RefreshAsync(ct, initializeDraft: false);
            Lifetime.RequireCurrent(ct);
            if (!ServerAllows || wasStart != CanStartPicking || (!wasStart && !InputValid))
                throw new InvalidOperationException("작업 상태가 바뀌었습니다. 현재 입력을 다시 확인해 주세요.");
            switch (Kind)
            {
                case "inbound":
                    var inbound = await _client.CompleteAsync<입고상품목록응답>(Kind, ItemId, new 입고완료요청
                    { Items = [new() { 상품명 = ProductName.Trim(), SKU = Sku.Trim(), 입고수량 = Quantity, 불량수량 = DefectQuantity, 보관위치 = StorageLocation.Trim() }] }, ct);
                    Lifetime.RequireCurrent(ct);
                    if (inbound is null || inbound.Items.Count == 0 || inbound.Items.Any(item => CargoWorkspaceRoutes.Number(item.입고요청Id) != ItemId))
                        throw new InvalidOperationException("입고 완료 결과를 확인하지 못했습니다.");
                    RequireTransition(inbound.Items.All(item => item.입고완료일시 > DateTime.MinValue));
                    var inspectionActions = new List<RoleWorkspaceAction>();
                    foreach (var received in inbound.Items)
                    {
                        var inventoryId = CargoWorkspaceRoutes.Number(received.Id);
                        var inspection = await _client.DetailAsync<입고검수대상상세응답>("inspection", inventoryId, ct);
                        Lifetime.RequireCurrent(ct);
                        RequireTransition(inspection.InboundItemId == received.Id && inspection.InboundId == received.입고요청Id
                            && inspection.WarehouseId == received.창고Id);
                        inspectionActions.Add(new("inspection", inspection.CanInspect ? "수령한 물품 검수" : "수령한 물품 검수 기록",
                            CargoWorkspaceRoutes.WarehouseInput("inspection", inventoryId), RequiresConfirmation: false));
                    }
                    nextActions = inspectionActions;
                    nextItemId = "inspection:" + CargoWorkspaceRoutes.Number(inbound.Items[0].Id);
                    break;
                case "put-away":
                    var putAway = await _client.CompleteAsync<적재작업결과응답>(Kind, ItemId, new 적재작업완료요청
                    { StorageLocation = StorageLocation.Trim(), Memo = Memo.Trim(), InspectionResultConfirmed = FirstConfirmed, LocationLabelConfirmed = SecondConfirmed }, ct);
                    Lifetime.RequireCurrent(ct); RequireId(putAway is null ? "" : CargoWorkspaceRoutes.Number(putAway.InboundItemId));
                    RequireTransition(putAway!.PutAwayAtUtc > DateTime.MinValue && putAway.InventoryStatus == "적재완료" && putAway.StorageLocation == StorageLocation.Trim());
                    break;
                case "picking" when wasStart:
                    var started = await _client.StartPickingAsync(ItemId, ct);
                    Lifetime.RequireCurrent(ct); RequireId(started?.TaskKey ?? "");
                    RequireTransition(started!.StartedAtUtc > DateTime.MinValue && started.Status == 피킹작업조회상태코드.진행중);
                    break;
                case "picking":
                    var picked = await _client.CompleteAsync<피킹작업결과응답>(Kind, ItemId, new 피킹작업완료요청
                    { RackCode = StorageLocation.Trim(), ProductConfirmed = FirstConfirmed, QuantityConfirmed = SecondConfirmed }, ct);
                    Lifetime.RequireCurrent(ct); RequireId(picked?.TaskKey ?? "");
                    RequireTransition(picked!.CompletedAtUtc > DateTime.MinValue && picked.Status == 피킹작업조회상태코드.완료);
                    break;
                case "packing":
                    var packed = await _client.CompleteAsync<포장작업결과응답>(Kind, ItemId, new 포장작업완료요청
                    { PackagingQuantity = Quantity, PackagingType = PackagingType, Memo = Memo.Trim(), InventoryConfirmed = FirstConfirmed, PackageLabelConfirmed = SecondConfirmed }, ct);
                    Lifetime.RequireCurrent(ct); RequireId(packed is null ? "" : CargoWorkspaceRoutes.Number(packed.InboundItemId));
                    RequireTransition(packed!.PackedAtUtc > DateTime.MinValue && packed.InventoryStatus == "포장완료-" + PackagingType
                        && packed.PackagingQuantity == Quantity && packed.PackagingType == PackagingType);
                    break;
                case "handoff":
                    var handoff = await _client.CompleteAsync<출고인계준비결과응답>(Kind, ItemId, new 출고인계준비완료요청
                    { HandoffQuantity = Quantity, Memo = Memo.Trim(), PackageSealConfirmed = FirstConfirmed, TransportConditionsConfirmed = SecondConfirmed }, ct);
                    Lifetime.RequireCurrent(ct); RequireId(handoff is null ? "" : CargoWorkspaceRoutes.Number(handoff.InboundItemId));
                    RequireTransition(handoff!.HandoffReadyAtUtc > DateTime.MinValue && handoff.OutboundPlanId > 0 && handoff.HandoffQuantity == Quantity
                        && !string.IsNullOrWhiteSpace(handoff.OutboundStatus) && handoff.OutboundStatus != "취소");
                    var planId = CargoWorkspaceRoutes.Number(handoff.OutboundPlanId);
                    var plan = await _client.DetailAsync<출고예정검토상세응답>("outbound", planId, ct);
                    Lifetime.RequireCurrent(ct);
                    RequireTransition(plan.OutboundPlanId == handoff.OutboundPlanId && plan.InboundItemId == handoff.InboundItemId);
                    nextActions = [new("outbound", "준비한 출고예정 검토", CargoWorkspaceRoutes.WarehouseInput("outbound", planId), RequiresConfirmation: false)];
                    nextItemId = "outbound:" + planId;
                    break;
            }
            await RefreshAsync(ct, initializeDraft: false);
            Lifetime.RequireCurrent(ct);
            RequireTransition(Kind switch
            {
                "inbound" => _inbound?.상태 == 입고상태코드.완료,
                "put-away" => _putAway?.PutAwayAtUtc > DateTime.MinValue && _putAway?.CanPutAway == false && _putAway.StorageLocation == StorageLocation.Trim(),
                "picking" when wasStart => _picking?.StartedAtUtc > DateTime.MinValue && _picking?.Status is 피킹작업조회상태코드.진행중 or 피킹작업조회상태코드.완료,
                "picking" => _picking?.CompletedAtUtc > DateTime.MinValue && _picking?.Status == 피킹작업조회상태코드.완료 && _picking.CanComplete == false,
                "packing" => _packing?.PackedAtUtc > DateTime.MinValue && _packing?.CanPack == false && _packing.PackingType == PackagingType,
                "handoff" => _handoff?.HandoffReadyAtUtc > DateTime.MinValue && _handoff?.CanConfirmHandoff == false && _handoff.OutboundPlanId > 0,
                _ => false
            });
            Saved = !wasStart;
            NextActions = nextActions; NextItemId = nextItemId;
            FirstConfirmed = SecondConfirmed = SaveConfirmed = false;
        }, wasStart ? "피킹을 시작했습니다. 물품과 수량을 확인한 뒤 완료해 주세요." : "저장된 작업 상태를 확인했습니다. 지도로 돌아가 다음 업무를 확인해 주세요.");
    }

    private static void RequireTransition(bool confirmed)
    {
        if (!confirmed) throw new InvalidOperationException("저장된 작업 상태를 확인하지 못했습니다. 지도로 돌아가 현재 업무를 다시 확인해 주세요.");
    }

    private void RequireId(string returnedId)
    {
        if (!string.Equals(ItemId, returnedId, StringComparison.Ordinal)) throw new InvalidOperationException("선택한 창고 업무와 처리 결과가 일치하지 않습니다.");
    }
    private void Clear()
    {
        _inbound = null; _putAway = null; _picking = null; _packing = null; _handoff = null;
        Loaded = Saved = FirstConfirmed = SecondConfirmed = SaveConfirmed = false;
        NextActions = []; NextItemId = null;
        ProductName = Sku = StorageLocation = Memo = ""; Quantity = DefectQuantity = 0; PackagingType = 포장유형코드.일반포장;
    }
    public void Dispose() => Lifetime.Dispose();
}
