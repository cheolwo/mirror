namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehousePickPlan
{
    public bool IsComplete { get; set; }

    public int RequestedQuantity { get; set; }

    public int PlannedQuantity { get; set; }

    public int ShortageQuantity => Math.Max(0, RequestedQuantity - PlannedQuantity);

    public IReadOnlyList<WarehousePickInstruction> Instructions { get; set; } = [];
}
