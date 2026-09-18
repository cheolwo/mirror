using Ssalddel.Contracts.Common.Warehouse;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

public sealed class 피킹배치Engine : I피킹배치Engine
{
    private const string PickingCode = "Picking";
    private const string PickingName = "피킹";
    private const string PackingCode = "Packing";
    private const string PackingName = "포장";

    public Task<피킹배치계획결과> 계획Async(피킹배치계획요청 request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lines = request.출고라인목록
            .Where(x => x.Quantity > 0 && x.WarehouseId > 0 && x.InboundProductId > 0)
            .OrderBy(x => x.WarehouseId)
            .ThenBy(x => x.적재대코드, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.보관위치코드, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.LineKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (lines.Length == 0)
        {
            return Task.FromResult(new 피킹배치계획결과
            {
                IsComplete = false,
                Message = "피킹 배치로 전환할 출고 라인이 없습니다."
            });
        }

        var pickMaxQuantity = request.작업자별권장최대피킹수량 > 0 ? request.작업자별권장최대피킹수량 : 40;
        var packMaxQuantity = request.작업자별권장최대포장수량 > 0 ? request.작업자별권장최대포장수량 : 40;
        var maxTaskCount = request.작업자별권장최대작업수 > 0 ? request.작업자별권장최대작업수 : 8;
        var warehouseOptionMap = request.창고옵션목록
            .Where(x => x.WarehouseId > 0)
            .GroupBy(x => x.WarehouseId)
            .ToDictionary(x => x.Key, x => x.First());

        var workers = request.작업자후보목록
            .Where(x => x.IsAvailable && x.WarehouseId > 0 && !string.IsNullOrWhiteSpace(x.UserId))
            .Where(x => !request.대상창고Id.HasValue || x.WarehouseId == request.대상창고Id.Value)
            .GroupBy(x => x.UserId, StringComparer.OrdinalIgnoreCase)
            .Select(x => new 작업자부하(x.First()))
            .ToList();

        var pickingTasks = new List<피킹작업배정>();
        var packingTasks = new List<포장작업배정>();
        var unassigned = new List<피킹배치미배정라인>();
        var sequence = 1;

        foreach (var line in lines)
        {
            if (request.대상창고Id.HasValue && line.WarehouseId != request.대상창고Id.Value)
            {
                unassigned.Add(미배정라인생성(line, line.Quantity, "창고", "피킹 배치 엔진의 대상 창고와 출고 라인의 창고가 다릅니다."));
                continue;
            }

            warehouseOptionMap.TryGetValue(line.WarehouseId, out var warehouseOption);
            var effectivePickMaxQuantity = warehouseOption?.작업자별권장최대피킹수량 is > 0
                ? warehouseOption.작업자별권장최대피킹수량.Value
                : pickMaxQuantity;
            var effectivePackMaxQuantity = warehouseOption?.작업자별권장최대포장수량 is > 0
                ? warehouseOption.작업자별권장최대포장수량.Value
                : packMaxQuantity;
            var effectiveMaxTaskCount = warehouseOption?.작업자별권장최대작업수 is > 0
                ? warehouseOption.작업자별권장최대작업수.Value
                : maxTaskCount;

            var barcodeValidation = 바코드검증(line, warehouseOption?.상품바코드검증필수 ?? true);
            if (!barcodeValidation.IsMatched)
            {
                unassigned.Add(미배정라인생성(line, line.Quantity, "바코드", barcodeValidation.Reason));
                continue;
            }

            var mode = line.처리방식 ?? warehouseOption?.기본처리방식 ?? request.기본처리방식;
            var remaining = line.Quantity;

            while (remaining > 0)
            {
                var picker = 작업자선택(workers, line.WarehouseId, PickingCode, effectivePickMaxQuantity, effectiveMaxTaskCount);
                if (picker is null)
                {
                    unassigned.Add(미배정라인생성(line, remaining, "피킹", "같은 창고에서 피킹 가능한 작업자가 없습니다."));
                    break;
                }

                if (mode == 피킹포장처리방식.피킹포장통합 && !작업유형가능(picker.Worker, PackingCode))
                {
                    picker = 통합작업자선택(workers, line.WarehouseId, effectivePickMaxQuantity, effectivePackMaxQuantity, effectiveMaxTaskCount);
                    if (picker is null)
                    {
                        unassigned.Add(미배정라인생성(line, remaining, "피킹/포장", "같은 창고에서 피킹과 포장을 모두 처리할 수 있는 작업자가 없습니다."));
                        break;
                    }
                }

                var pickCapacity = Math.Max(0, effectivePickMaxQuantity - picker.TotalQuantity);
                var quantity = pickCapacity > 0 ? Math.Min(remaining, pickCapacity) : remaining;
                var pickTaskKey = $"{request.출고참조번호}-PICK-{line.WarehouseId}-{sequence:000}";
                string? packTaskKey = null;
                var nextStep = "피킹 완료";

                포장작업배정? packingTask = null;
                if (mode == 피킹포장처리방식.피킹포장통합)
                {
                    packTaskKey = $"{request.출고참조번호}-PACK-{line.WarehouseId}-{sequence:000}";
                    nextStep = "같은 작업자가 포장까지 이어서 처리";
                    picker.TotalTaskCount += 1;
                    picker.TotalQuantity += quantity;
                    packingTask = 포장작업생성(line, quantity, packTaskKey, picker, picker, "피킹 작업자가 같은 물량을 포장까지 이어서 처리합니다.");
                }
                else if (mode == 피킹포장처리방식.피킹포장분리)
                {
                    var packer = 작업자선택(workers, line.WarehouseId, PackingCode, effectivePackMaxQuantity, effectiveMaxTaskCount, picker.UserId);
                    if (packer is null)
                    {
                        unassigned.Add(미배정라인생성(line, quantity, "포장", "피킹 후 넘겨받을 포장 작업자가 없습니다."));
                        nextStep = "포장 작업자 배정 필요";
                    }
                    else
                    {
                        packTaskKey = $"{request.출고참조번호}-PACK-{line.WarehouseId}-{sequence:000}";
                        nextStep = "포장 작업자에게 인계";
                        packer.TotalTaskCount += 1;
                        packer.TotalQuantity += quantity;
                        packingTask = 포장작업생성(line, quantity, packTaskKey, picker, packer, "피킹 완료 후 별도 포장 작업자에게 인계합니다.");
                    }
                }

                pickingTasks.Add(new 피킹작업배정
                {
                    출고예정Id = line.출고예정Id,
                    TaskKey = pickTaskKey,
                    WorkerUserId = picker.UserId,
                    WorkerName = picker.DisplayName,
                    WorkerBundleBarcode = picker.BundleBarcode,
                    WarehouseId = line.WarehouseId,
                    WarehouseName = line.WarehouseName,
                    LineKey = line.LineKey,
                    InboundProductId = line.InboundProductId,
                    Sku = line.Sku,
                    ProductName = line.ProductName,
                    상품바코드 = line.상품바코드,
                    적재상품바코드 = line.적재상품바코드,
                    IsBarcodeMatched = true,
                    Quantity = quantity,
                    적재대코드 = line.적재대코드,
                    보관위치코드 = line.보관위치코드,
                    LotNo = line.LotNo,
                    처리방식 = mode,
                    포장작업Key = packTaskKey,
                    NextStep = nextStep,
                    AssignmentReason = "같은 창고와 적재대 흐름에서 현재 피킹 부하가 낮은 작업자에게 배정했습니다."
                });

                picker.TotalTaskCount += 1;
                picker.TotalQuantity += quantity;

                if (packingTask is not null)
                {
                    packingTasks.Add(packingTask);
                }

                remaining -= quantity;
                sequence += 1;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        var workerSummaries = workers
            .OrderBy(x => x.WarehouseId)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new 피킹포장작업자부하
            {
                WorkerUserId = x.UserId,
                WorkerName = x.DisplayName,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.WarehouseName,
                TotalTaskCount = x.TotalTaskCount,
                TotalQuantity = x.TotalQuantity,
                IsOverloaded = x.TotalTaskCount > maxTaskCount
                    || x.TotalQuantity > Math.Max(pickMaxQuantity, packMaxQuantity)
            })
            .ToArray();

        return Task.FromResult(new 피킹배치계획결과
        {
            IsComplete = unassigned.Count == 0 && pickingTasks.Count > 0,
            Message = 결과메시지생성(pickingTasks, unassigned),
            피킹작업목록 = pickingTasks,
            포장작업목록 = packingTasks,
            피킹묶음목록 = 묶음목록생성(pickingTasks),
            미배정라인목록 = unassigned,
            작업자부하목록 = workerSummaries
        });
    }

    public static 피킹배치출고라인 출고배치에서생성(OutboundBatchAllocation allocation)
        => new()
        {
            LineKey = allocation.LineKey,
            InboundProductId = allocation.InboundProductId,
            WarehouseId = allocation.WarehouseId,
            WarehouseName = allocation.WarehouseName,
            Sku = allocation.Sku,
            ProductName = allocation.ProductName,
            상품바코드 = allocation.Sku,
            적재상품바코드 = allocation.Sku,
            Quantity = allocation.Quantity
        };

    private static 포장작업배정 포장작업생성(
        피킹배치출고라인 line,
        int quantity,
        string taskKey,
        작업자부하 picker,
        작업자부하 packer,
        string reason)
        => new()
        {
            출고예정Id = line.출고예정Id,
            TaskKey = taskKey,
            PickerUserId = picker.UserId,
            PackerUserId = packer.UserId,
            PackerName = packer.DisplayName,
            PickerBundleBarcode = picker.BundleBarcode,
            PackerBundleBarcode = packer.BundleBarcode,
            WarehouseId = line.WarehouseId,
            WarehouseName = line.WarehouseName,
            LineKey = line.LineKey,
            InboundProductId = line.InboundProductId,
            Sku = line.Sku,
            ProductName = line.ProductName,
            상품바코드 = line.상품바코드,
            적재상품바코드 = line.적재상품바코드,
            Quantity = quantity,
            AssignmentReason = reason
        };

    private static 작업자부하? 통합작업자선택(
        IReadOnlyList<작업자부하> workers,
        long warehouseId,
        int pickMaxQuantity,
        int packMaxQuantity,
        int maxTaskCount)
        => workers
            .Where(x => x.WarehouseId == warehouseId)
            .Where(x => 작업유형가능(x.Worker, PickingCode) && 작업유형가능(x.Worker, PackingCode))
            .OrderBy(x => x.TotalQuantity >= Math.Min(pickMaxQuantity, packMaxQuantity))
            .ThenBy(x => x.TotalTaskCount >= maxTaskCount)
            .ThenBy(x => x.TotalQuantity)
            .ThenBy(x => x.TotalTaskCount)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static 작업자부하? 작업자선택(
        IReadOnlyList<작업자부하> workers,
        long warehouseId,
        string workTypeCode,
        int maxQuantity,
        int maxTaskCount,
        string? excludeUserId = null)
        => workers
            .Where(x => x.WarehouseId == warehouseId)
            .Where(x => excludeUserId is null || !string.Equals(x.UserId, excludeUserId, StringComparison.OrdinalIgnoreCase))
            .Where(x => 작업유형가능(x.Worker, workTypeCode))
            .OrderBy(x => x.TotalQuantity >= maxQuantity)
            .ThenBy(x => x.TotalTaskCount >= maxTaskCount)
            .ThenBy(x => x.TotalQuantity)
            .ThenBy(x => x.TotalTaskCount)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static bool 작업유형가능(피킹포장작업자후보 worker, string workTypeCode)
    {
        if (worker.가능작업유형코드목록.Count == 0)
        {
            return true;
        }

        return worker.가능작업유형코드목록.Any(x =>
            string.Equals(x, workTypeCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, PickingName, StringComparison.OrdinalIgnoreCase) && string.Equals(workTypeCode, PickingCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, PackingName, StringComparison.OrdinalIgnoreCase) && string.Equals(workTypeCode, PackingCode, StringComparison.OrdinalIgnoreCase));
    }

    private static 피킹배치미배정라인 미배정라인생성(피킹배치출고라인 line, int quantity, string stage, string reason)
        => new()
        {
            LineKey = line.LineKey,
            InboundProductId = line.InboundProductId,
            WarehouseId = line.WarehouseId,
            WarehouseName = line.WarehouseName,
            Sku = line.Sku,
            ProductName = line.ProductName,
            상품바코드 = line.상품바코드,
            적재상품바코드 = line.적재상품바코드,
            Quantity = quantity,
            Stage = stage,
            Reason = reason
        };

    private static IReadOnlyList<피킹작업묶음> 묶음목록생성(IReadOnlyList<피킹작업배정> pickingTasks)
        => pickingTasks
            .GroupBy(x => new
            {
                x.WorkerUserId,
                x.WorkerName,
                x.WarehouseId,
                x.WarehouseName,
                x.WorkerBundleBarcode
            })
            .Select(x => new 피킹작업묶음
            {
                WorkerUserId = x.Key.WorkerUserId,
                WorkerName = x.Key.WorkerName,
                WarehouseId = x.Key.WarehouseId,
                WarehouseName = x.Key.WarehouseName,
                WorkerBundleBarcode = x.Key.WorkerBundleBarcode,
                TotalTaskCount = x.Count(),
                TotalQuantity = x.Sum(task => task.Quantity)
            })
            .OrderBy(x => x.WarehouseId)
            .ThenBy(x => x.WorkerName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static 바코드검증결과 바코드검증(피킹배치출고라인 line, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(line.상품바코드) || string.IsNullOrWhiteSpace(line.적재상품바코드))
        {
            return isRequired
                ? new(false, "출고 작업의 상품 바코드와 현재 적재 상품 바코드를 모두 확인해야 합니다.")
                : new(true, string.Empty);
        }

        if (!string.Equals(line.상품바코드.Trim(), line.적재상품바코드.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new(false, "출고 작업의 상품 바코드와 현재 적재 상품 바코드가 일치하지 않습니다.");
        }

        return new(true, string.Empty);
    }

    private static string 결과메시지생성(
        IReadOnlyList<피킹작업배정> pickingTasks,
        IReadOnlyList<피킹배치미배정라인> unassigned)
    {
        if (pickingTasks.Count == 0)
        {
            return "피킹 배치 계획을 만들 수 없습니다.";
        }

        return unassigned.Count > 0
            ? "일부 출고 라인은 피킹 또는 포장 작업자에게 배정하지 못했습니다."
            : "출고 라인을 피킹/포장 작업으로 배치했습니다.";
    }

    private sealed class 작업자부하
    {
        public 작업자부하(피킹포장작업자후보 worker)
        {
            Worker = worker;
            UserId = worker.UserId;
            DisplayName = string.IsNullOrWhiteSpace(worker.DisplayName) ? worker.UserId : worker.DisplayName;
            BundleBarcode = 작업자묶음바코드생성(worker);
            WarehouseId = worker.WarehouseId;
            WarehouseName = worker.WarehouseName;
            TotalTaskCount = Math.Max(0, worker.진행중작업수);
            TotalQuantity = Math.Max(0, worker.기배정수량);
        }

        public 피킹포장작업자후보 Worker { get; }

        public string UserId { get; }

        public string DisplayName { get; }

        public string BundleBarcode { get; }

        public long WarehouseId { get; }

        public string WarehouseName { get; }

        public int TotalTaskCount { get; set; }

        public int TotalQuantity { get; set; }
    }

    private static string 작업자묶음바코드생성(피킹포장작업자후보 worker)
    {
        if (!string.IsNullOrWhiteSpace(worker.작업자묶음바코드))
        {
            return worker.작업자묶음바코드.Trim();
        }

        var digits = new string((worker.휴대폰뒤8자리 ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length >= 8)
        {
            return digits[^8..];
        }

        return worker.UserId.Trim().ToUpperInvariant();
    }

    private sealed record 바코드검증결과(bool IsMatched, string Reason);
}
