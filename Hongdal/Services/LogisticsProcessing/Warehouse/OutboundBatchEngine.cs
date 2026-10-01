using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace Hongdal.Services.LogisticsProcessing.Warehouse;

public sealed class OutboundBatchEngine : IOutboundBatchEngine
{
    private readonly HongdalContext _db;
    private readonly IWarehouseServiceAreaPolicy _serviceAreaPolicy;
    private readonly IWarehouseDistanceCostEstimator _distanceCostEstimator;

    public OutboundBatchEngine(
        HongdalContext db,
        IWarehouseServiceAreaPolicy serviceAreaPolicy,
        IWarehouseDistanceCostEstimator distanceCostEstimator)
    {
        _db = db;
        _serviceAreaPolicy = serviceAreaPolicy;
        _distanceCostEstimator = distanceCostEstimator;
    }

    public async Task<OutboundBatchPlanResult> PlanAsync(OutboundBatchPlanRequest request, CancellationToken cancellationToken)
    {
        var validLines = request.Lines
            .Where(x => x.Quantity > 0 && !string.IsNullOrWhiteSpace(x.Sku))
            .ToArray();

        if (validLines.Length == 0)
        {
            return new OutboundBatchPlanResult
            {
                IsComplete = false,
                Message = "출고 요청 라인이 없습니다."
            };
        }

        var candidateMap = new Dictionary<string, IReadOnlyList<OutboundStockCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in validLines)
        {
            candidateMap[line.LineKey] = await 후보검색Async(request, line, cancellationToken);
        }

        return CreatePlan(validLines, candidateMap);
    }

    internal static OutboundBatchPlanResult CreatePlan(
        IReadOnlyList<OutboundBatchPlanLineRequest> lines,
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        var allocations = TryCreateSingleWarehousePlan(lines, candidateMap);
        var unallocated = new List<OutboundBatchUnallocatedLine>();

        if (allocations.Count == 0)
        {
            var remainingStock = CreateRemainingStock(candidateMap);
            foreach (var line in lines)
            {
                allocations.AddRange(CreateLineAllocations(
                    line,
                    candidateMap[line.LineKey],
                    remainingStock,
                    unallocated));
            }
        }

        foreach (var line in lines)
        {
            var plannedQuantity = allocations
                .Where(x => string.Equals(x.LineKey, line.LineKey, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Quantity);

            if (plannedQuantity < line.Quantity && unallocated.All(x => !string.Equals(x.LineKey, line.LineKey, StringComparison.OrdinalIgnoreCase)))
            {
                unallocated.Add(new OutboundBatchUnallocatedLine
                {
                    LineKey = line.LineKey,
                    Sku = line.Sku,
                    ProductName = line.ProductName,
                    RequestedQuantity = line.Quantity,
                    PlannedQuantity = plannedQuantity,
                    Reason = "출고 가능한 창고 재고가 부족합니다."
                });
            }
        }

        var warehouseCount = allocations.Select(x => x.WarehouseId).Distinct().Count();
        return new OutboundBatchPlanResult
        {
            IsComplete = unallocated.Count == 0 && allocations.Count > 0,
            RequiresSplitShipment = warehouseCount > 1,
            Message = CreateMessage(allocations, unallocated),
            Allocations = allocations,
            UnallocatedLines = unallocated
        };
    }

    private async Task<IReadOnlyList<OutboundStockCandidate>> 후보검색Async(
        OutboundBatchPlanRequest request,
        OutboundBatchPlanLineRequest line,
        CancellationToken cancellationToken)
    {
        var normalizedSku = line.Sku.Trim();
        var rows = await (
                from item in _db.입고상품.AsNoTracking()
                join warehouse in _db.창고.AsNoTracking() on item.창고Id equals warehouse.Id
                where item.판매자UserId == request.SellerUserId
                      && item.SKU == normalizedSku
                      && warehouse.IsActive
                      && item.가용수량 > 0
                select new
                {
                    Item = item,
                    Warehouse = warehouse
                })
            .ToArrayAsync(cancellationToken);

        return rows
            .Where(x => 입고계약유형코드.CanSellToMarket(x.Item.계약유형))
            .Select(x => CreateCandidate(request, line, x.Item, x.Warehouse, GetAllocatableQuantity(x.Item)))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.AvailableQuantity)
            .ToArray();
    }

    internal static int GetAllocatableQuantity(홍달.도메인.창고.입고상품 item)
    {
        // 예약 처리 시 WarehouseOperationService가 가용수량을 감소시키므로 예약수량을 다시 차감하지 않는다.
        return Math.Max(0, item.가용수량);
    }

    private OutboundStockCandidate CreateCandidate(
        OutboundBatchPlanRequest request,
        OutboundBatchPlanLineRequest line,
        홍달.도메인.창고.입고상품 item,
        홍달.도메인.창고.창고 warehouse,
        int availableQuantity)
    {
        var isServiceAreaMatched = _serviceAreaPolicy.IsInServiceArea(warehouse.주소, request.DestinationAddress);
        var estimate = _distanceCostEstimator.Estimate(
            warehouse.위도,
            warehouse.경도,
            request.DestinationLatitude,
            request.DestinationLongitude);
        var canFulfillLine = availableQuantity >= line.Quantity;
        var isPreferredInbound = line.PreferredInboundProductId.HasValue && line.PreferredInboundProductId.Value == item.Id;
        var score = CalculateScore(warehouse, isServiceAreaMatched, estimate, canFulfillLine, isPreferredInbound);

        return new OutboundStockCandidate(
            line.LineKey,
            line.SalesProductId,
            item.Id,
            warehouse.Id,
            warehouse.창고명,
            warehouse.주소,
            item.SKU,
            string.IsNullOrWhiteSpace(line.ProductName) ? item.상품명 : line.ProductName,
            availableQuantity,
            isServiceAreaMatched,
            estimate.DistanceKm,
            estimate.EstimatedTransportCost,
            score,
            CreateSelectionReason(isServiceAreaMatched, estimate, warehouse.기본창고여부, isPreferredInbound));
    }

    private static decimal CalculateScore(
        홍달.도메인.창고.창고 warehouse,
        bool isServiceAreaMatched,
        WarehouseDistanceCostEstimate estimate,
        bool canFulfillLine,
        bool isPreferredInbound)
    {
        var score = 1000m;
        if (canFulfillLine)
        {
            score += 300m;
        }

        if (isServiceAreaMatched)
        {
            score += 250m;
        }

        if (warehouse.기본창고여부)
        {
            score += 80m;
        }

        if (isPreferredInbound)
        {
            score += 50m;
        }

        if (estimate.DistanceKm.HasValue)
        {
            score -= Math.Min(300m, estimate.DistanceKm.Value * 5m);
        }

        if (estimate.EstimatedTransportCost.HasValue)
        {
            score -= Math.Min(200m, estimate.EstimatedTransportCost.Value / 1000m);
        }

        return Math.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    private static List<OutboundBatchAllocation> TryCreateSingleWarehousePlan(
        IReadOnlyList<OutboundBatchPlanLineRequest> lines,
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        if (lines.Count <= 1)
        {
            return [];
        }

        var warehouseIds = candidateMap.Values
            .SelectMany(x => x.Select(candidate => candidate.WarehouseId))
            .Distinct()
            .ToArray();

        var bestPlan = warehouseIds
            .Select(warehouseId => TryCreateSingleWarehousePlanForWarehouse(
                warehouseId,
                lines,
                candidateMap))
            .OfType<SingleWarehousePlan>()
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestPlan is null)
        {
            return [];
        }

        return bestPlan.Allocations;
    }

    private static SingleWarehousePlan? TryCreateSingleWarehousePlanForWarehouse(
        long warehouseId,
        IReadOnlyList<OutboundBatchPlanLineRequest> lines,
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        var remainingStock = CreateRemainingStock(candidateMap);
        var allocations = new List<OutboundBatchAllocation>();
        var selectedCandidates = new List<OutboundStockCandidate>();

        foreach (var line in lines)
        {
            var candidate = candidateMap[line.LineKey]
                .Where(x => x.WarehouseId == warehouseId
                            && remainingStock.GetValueOrDefault(x.InboundProductId) >= line.Quantity)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (candidate is null)
            {
                return null;
            }

            remainingStock[candidate.InboundProductId] -= line.Quantity;
            selectedCandidates.Add(candidate);
            allocations.Add(CreateAllocation(line, candidate, line.Quantity));
        }

        var score = selectedCandidates.Sum(candidate => candidate.Score)
                    + (selectedCandidates.Any(candidate => candidate.IsServiceAreaMatched) ? 100m : 0m);
        return new SingleWarehousePlan(allocations, score);
    }

    private static Dictionary<long, int> CreateRemainingStock(
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        return candidateMap.Values
            .SelectMany(x => x)
            .GroupBy(x => x.InboundProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Min(candidate => candidate.AvailableQuantity));
    }

    private static List<OutboundBatchAllocation> CreateLineAllocations(
        OutboundBatchPlanLineRequest line,
        IReadOnlyList<OutboundStockCandidate> candidates,
        Dictionary<long, int> remainingStock,
        List<OutboundBatchUnallocatedLine> unallocated)
    {
        var remaining = line.Quantity;
        var allocations = new List<OutboundBatchAllocation>();

        foreach (var candidate in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var availableQuantity = remainingStock.GetValueOrDefault(candidate.InboundProductId);
            var quantity = Math.Min(availableQuantity, remaining);
            if (quantity <= 0)
            {
                continue;
            }

            allocations.Add(CreateAllocation(line, candidate, quantity));
            remainingStock[candidate.InboundProductId] = availableQuantity - quantity;
            remaining -= quantity;
        }

        if (remaining > 0)
        {
            unallocated.Add(new OutboundBatchUnallocatedLine
            {
                LineKey = line.LineKey,
                Sku = line.Sku,
                ProductName = line.ProductName,
                RequestedQuantity = line.Quantity,
                PlannedQuantity = line.Quantity - remaining,
                Reason = candidates.Count == 0
                    ? "출고 가능한 창고 후보가 없습니다."
                    : "후보 창고 재고 합계가 요청 수량보다 부족합니다."
            });
        }

        return allocations;
    }

    private static OutboundBatchAllocation CreateAllocation(
        OutboundBatchPlanLineRequest line,
        OutboundStockCandidate candidate,
        int quantity)
    {
        return new OutboundBatchAllocation
        {
            LineKey = line.LineKey,
            SalesProductId = line.SalesProductId,
            InboundProductId = candidate.InboundProductId,
            WarehouseId = candidate.WarehouseId,
            WarehouseName = candidate.WarehouseName,
            Sku = candidate.Sku,
            ProductName = candidate.ProductName,
            Quantity = quantity,
            IsServiceAreaMatched = candidate.IsServiceAreaMatched,
            EstimatedDistanceKm = candidate.EstimatedDistanceKm,
            EstimatedTransportCost = candidate.EstimatedTransportCost,
            SelectionScore = candidate.Score,
            SelectionReason = candidate.SelectionReason
        };
    }

    private static string CreateSelectionReason(
        bool isServiceAreaMatched,
        WarehouseDistanceCostEstimate estimate,
        bool isDefaultWarehouse,
        bool isPreferredInbound)
    {
        var reasons = new List<string>();
        if (isServiceAreaMatched)
        {
            reasons.Add("배송권 일치");
        }

        if (estimate.DistanceKm.HasValue)
        {
            reasons.Add($"예상거리 {estimate.DistanceKm:0.##}km");
        }

        if (isDefaultWarehouse)
        {
            reasons.Add("기본 창고");
        }

        if (isPreferredInbound)
        {
            reasons.Add("원 판매상품 입고 재고");
        }

        return reasons.Count == 0 ? "재고 충족 후보" : string.Join(", ", reasons);
    }

    private static string CreateMessage(
        IReadOnlyList<OutboundBatchAllocation> allocations,
        IReadOnlyList<OutboundBatchUnallocatedLine> unallocated)
    {
        if (allocations.Count == 0)
        {
            return "출고 배치 계획을 만들 수 없습니다.";
        }

        if (unallocated.Count > 0)
        {
            return "일부 상품은 출고 배치 계획을 만들지 못했습니다.";
        }

        var warehouseCount = allocations.Select(x => x.WarehouseId).Distinct().Count();
        return warehouseCount > 1
            ? "복수 창고 분할 출고 계획이 생성되었습니다."
            : "단일 창고 출고 배치 계획이 생성되었습니다.";
    }

    private sealed record SingleWarehousePlan(
        List<OutboundBatchAllocation> Allocations,
        decimal Score);

    internal sealed record OutboundStockCandidate(
        string LineKey,
        long? SalesProductId,
        long InboundProductId,
        long WarehouseId,
        string WarehouseName,
        string WarehouseAddress,
        string Sku,
        string ProductName,
        int AvailableQuantity,
        bool IsServiceAreaMatched,
        decimal? EstimatedDistanceKm,
        decimal? EstimatedTransportCost,
        decimal Score,
        string SelectionReason);
}
