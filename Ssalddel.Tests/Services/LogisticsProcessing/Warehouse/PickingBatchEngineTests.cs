using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.LogisticsProcessing.Warehouse;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class PickingBatchEngineTests
{
    [Fact]
    public async Task 계획Async_assigns_picking_and_packing_to_same_worker_when_mode_is_integrated()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-1",
            기본처리방식 = 피킹포장처리방식.피킹포장통합,
            출고라인목록 = [Line("L1", warehouseId: 10, quantity: 5)],
            작업자후보목록 =
            [
                Worker("worker-1", warehouseId: 10, "Picking", "Packing"),
                Worker("worker-2", warehouseId: 20, "Picking", "Packing")
            ]
        }, CancellationToken.None);

        Assert.True(result.IsComplete);
        var picking = Assert.Single(result.피킹작업목록);
        var packing = Assert.Single(result.포장작업목록);
        Assert.Equal("worker-1", picking.WorkerUserId);
        Assert.Equal("worker-1", packing.PackerUserId);
        Assert.Equal(packing.TaskKey, picking.포장작업Key);
        Assert.Equal(910, picking.출고예정Id);
        Assert.Equal(picking.출고예정Id, packing.출고예정Id);
    }

    [Fact]
    public async Task 계획Async_hands_picked_item_to_packing_worker_when_mode_is_separated()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-2",
            기본처리방식 = 피킹포장처리방식.피킹포장분리,
            출고라인목록 = [Line("L1", warehouseId: 10, quantity: 5)],
            작업자후보목록 =
            [
                Worker("picker", warehouseId: 10, "Picking"),
                Worker("packer", warehouseId: 10, "Packing")
            ]
        }, CancellationToken.None);

        Assert.True(result.IsComplete);
        var picking = Assert.Single(result.피킹작업목록);
        var packing = Assert.Single(result.포장작업목록);
        Assert.Equal("picker", picking.WorkerUserId);
        Assert.Equal("packer", packing.PackerUserId);
        Assert.Equal("포장 작업자에게 인계", picking.NextStep);
    }

    [Fact]
    public async Task 계획Async_allows_pick_only_without_packing_task()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-3",
            기본처리방식 = 피킹포장처리방식.피킹만,
            출고라인목록 = [Line("L1", warehouseId: 10, quantity: 5)],
            작업자후보목록 = [Worker("picker", warehouseId: 10, "Picking")]
        }, CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Single(result.피킹작업목록);
        Assert.Empty(result.포장작업목록);
    }

    [Fact]
    public async Task 계획Async_keeps_storage_rack_information_on_picking_task()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-4",
            기본처리방식 = 피킹포장처리방식.피킹만,
            출고라인목록 =
            [
                Line("L1", warehouseId: 10, quantity: 3, rackCode: "RACK-A", locationCode: "A-01-02")
            ],
            작업자후보목록 = [Worker("picker", warehouseId: 10, "Picking")]
        }, CancellationToken.None);

        var task = Assert.Single(result.피킹작업목록);
        Assert.Equal("RACK-A", task.적재대코드);
        Assert.Equal("A-01-02", task.보관위치코드);
        Assert.Equal("BAR-L1", task.상품바코드);
        Assert.Equal("BAR-L1", task.적재상품바코드);
        Assert.True(task.IsBarcodeMatched);
    }

    [Fact]
    public async Task 계획Async_does_not_assign_when_product_barcode_and_stocked_barcode_differ()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-5",
            기본처리방식 = 피킹포장처리방식.피킹만,
            출고라인목록 =
            [
                Line("L1", warehouseId: 10, quantity: 3, productBarcode: "BAR-ORDER", stockedBarcode: "BAR-RACK")
            ],
            작업자후보목록 = [Worker("picker", warehouseId: 10, "Picking")]
        }, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(result.피킹작업목록);
        var unassigned = Assert.Single(result.미배정라인목록);
        Assert.Equal("바코드", unassigned.Stage);
        Assert.Contains("일치하지 않습니다", unassigned.Reason);
    }

    [Fact]
    public async Task 계획Async_limits_assignment_to_target_warehouse()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-6",
            대상창고Id = 10,
            기본처리방식 = 피킹포장처리방식.피킹만,
            출고라인목록 =
            [
                Line("L1", warehouseId: 10, quantity: 3),
                Line("L2", warehouseId: 20, quantity: 3)
            ],
            작업자후보목록 =
            [
                Worker("picker-10", warehouseId: 10, "Picking"),
                Worker("picker-20", warehouseId: 20, "Picking")
            ]
        }, CancellationToken.None);

        Assert.False(result.IsComplete);
        var task = Assert.Single(result.피킹작업목록);
        Assert.Equal(10, task.WarehouseId);
        var unassigned = Assert.Single(result.미배정라인목록);
        Assert.Equal("창고", unassigned.Stage);
    }

    [Fact]
    public async Task 계획Async_applies_warehouse_mode_option()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-7",
            기본처리방식 = 피킹포장처리방식.피킹포장분리,
            창고옵션목록 =
            [
                new 피킹배치창고옵션
                {
                    WarehouseId = 10,
                    WarehouseName = "창고-10",
                    기본처리방식 = 피킹포장처리방식.피킹포장통합
                }
            ],
            출고라인목록 = [Line("L1", warehouseId: 10, quantity: 3)],
            작업자후보목록 = [Worker("picker-packer", warehouseId: 10, "Picking", "Packing")]
        }, CancellationToken.None);

        Assert.True(result.IsComplete);
        var picking = Assert.Single(result.피킹작업목록);
        var packing = Assert.Single(result.포장작업목록);
        Assert.Equal(피킹포장처리방식.피킹포장통합, picking.처리방식);
        Assert.Equal(picking.WorkerUserId, packing.PackerUserId);
    }

    [Fact]
    public async Task 계획Async_uses_worker_phone_last8_as_picking_bundle_barcode()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-8",
            기본처리방식 = 피킹포장처리방식.피킹포장통합,
            출고라인목록 = [Line("L1", warehouseId: 10, quantity: 3)],
            작업자후보목록 = [Worker("picker-packer", warehouseId: 10, ["Picking", "Packing"], phoneLast8: "77773333")]
        }, CancellationToken.None);

        var picking = Assert.Single(result.피킹작업목록);
        var packing = Assert.Single(result.포장작업목록);
        var bundle = Assert.Single(result.피킹묶음목록);

        Assert.Equal("77773333", picking.WorkerBundleBarcode);
        Assert.Equal("77773333", packing.PickerBundleBarcode);
        Assert.Equal("77773333", bundle.WorkerBundleBarcode);
        Assert.Equal(3, bundle.TotalQuantity);
    }

    [Fact]
    public async Task 계획Async_keeps_ssalddel_mart_urban_warehouse_as_integrated_picking_packing()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "MART-1",
            기본처리방식 = 피킹포장처리방식.피킹포장분리,
            창고옵션목록 =
            [
                new 피킹배치창고옵션
                {
                    WarehouseId = 35,
                    WarehouseName = "알뜰살뜰 마트 도심 창고",
                    살뜰마트도심창고여부 = true,
                    기본처리방식 = 피킹포장처리방식.피킹포장통합,
                    운영메모 = "협소한 도심 창고라 피킹 작업자가 포장까지 이어서 처리합니다."
                }
            ],
            출고라인목록 = [Line("L1", warehouseId: 35, quantity: 2)],
            작업자후보목록 = [Worker("mart-picker", warehouseId: 35, ["Picking", "Packing"], phoneLast8: "77773333")]
        }, CancellationToken.None);

        Assert.True(result.IsComplete);
        var picking = Assert.Single(result.피킹작업목록);
        var packing = Assert.Single(result.포장작업목록);
        Assert.Equal(피킹포장처리방식.피킹포장통합, picking.처리방식);
        Assert.Equal(picking.WorkerUserId, packing.PackerUserId);
    }

    [Fact]
    public async Task 계획Async_returns_unassigned_packaging_when_separated_packer_does_not_exist()
    {
        var engine = new 피킹배치Engine();

        var result = await engine.계획Async(new 피킹배치계획요청
        {
            출고참조번호 = "OUT-10",
            기본처리방식 = 피킹포장처리방식.피킹포장분리,
            출고라인목록 = [Line("L1", warehouseId: 10, quantity: 3)],
            작업자후보목록 = [Worker("picker", warehouseId: 10, "Picking")]
        }, CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Single(result.피킹작업목록);
        Assert.Empty(result.포장작업목록);
        var unassigned = Assert.Single(result.미배정라인목록);
        Assert.Equal("포장", unassigned.Stage);
    }

    private static 피킹배치출고라인 Line(
        string lineKey,
        long warehouseId,
        int quantity,
        string? rackCode = null,
        string? locationCode = null,
        string? productBarcode = null,
        string? stockedBarcode = null)
        => new()
        {
            출고예정Id = 900 + warehouseId,
            출고작업Key = $"OUTBOUND-{lineKey}",
            LineKey = lineKey,
            InboundProductId = 100 + warehouseId,
            WarehouseId = warehouseId,
            WarehouseName = $"창고-{warehouseId}",
            Sku = "SKU-1",
            ProductName = "테스트 상품",
            상품바코드 = productBarcode ?? $"BAR-{lineKey}",
            적재상품바코드 = stockedBarcode ?? productBarcode ?? $"BAR-{lineKey}",
            Quantity = quantity,
            적재대코드 = rackCode,
            보관위치코드 = locationCode
        };

    private static 피킹포장작업자후보 Worker(
        string userId,
        long warehouseId,
        params string[] workTypes)
        => Worker(userId, warehouseId, workTypes, phoneLast8: null);

    private static 피킹포장작업자후보 Worker(
        string userId,
        long warehouseId,
        string[] workTypes,
        string? phoneLast8)
        => new()
        {
            UserId = userId,
            DisplayName = userId,
            휴대폰뒤8자리 = phoneLast8,
            WarehouseId = warehouseId,
            WarehouseName = $"창고-{warehouseId}",
            가능작업유형코드목록 = workTypes
        };
}
