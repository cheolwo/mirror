namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehousePickingPlanner : IWarehousePickingPlanner
{
    private readonly InMemoryShipperStore _store;

    public WarehousePickingPlanner(InMemoryShipperStore store)
    {
        _store = store;
    }

    public WarehousePickPlan Plan(long warehouseId, string sku, int requestedQuantity)
    {
        if (requestedQuantity <= 0)
        {
            return new WarehousePickPlan { IsComplete = true };
        }

        var remaining = requestedQuantity;
        var instructions = new List<WarehousePickInstruction>();
        var candidates = _store.GetStorageBinInventory(warehouseId, sku)
            .OrderBy(x => x.ExpirationDate ?? DateTime.MaxValue)
            .ThenBy(x => x.PickPriority)
            .ThenBy(x => CreateRouteSortKey(x.BinCode), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.OrderableQuantity);

        var routeSequence = 1;
        foreach (var candidate in candidates)
        {
            if (remaining == 0)
            {
                break;
            }

            var pickQuantity = Math.Min(candidate.OrderableQuantity, remaining);
            remaining -= pickQuantity;

            instructions.Add(new WarehousePickInstruction
            {
                BinCode = candidate.BinCode,
                Sku = candidate.Sku,
                ProductName = candidate.ProductName,
                RouteSequence = routeSequence++,
                RouteSortKey = CreateRouteSortKey(candidate.BinCode),
                RequestedQuantity = requestedQuantity,
                PickQuantity = pickQuantity,
                RemainingQuantityAfterPick = candidate.OrderableQuantity - pickQuantity,
                Reason = CreateReason(candidate)
            });
        }

        return new WarehousePickPlan
        {
            IsComplete = remaining == 0,
            RequestedQuantity = requestedQuantity,
            PlannedQuantity = requestedQuantity - remaining,
            Instructions = instructions
        };
    }

    private static string CreateRouteSortKey(string binCode)
    {
        var parts = binCode.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return binCode;
        }

        return string.Join('-', parts.Select(x => int.TryParse(x, out var number) ? number.ToString("D4") : x.ToUpperInvariant()));
    }

    private static string CreateReason(WarehouseStorageBinInventory candidate)
    {
        if (candidate.ExpirationDate.HasValue)
        {
            return $"유통기한 {candidate.ExpirationDate:yyyy-MM-dd} 우선";
        }

        return candidate.PickPriority == 0 ? "기본 피킹 우선순위" : $"피킹 우선순위 {candidate.PickPriority}";
    }
}
