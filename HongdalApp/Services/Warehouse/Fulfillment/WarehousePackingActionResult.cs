namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehousePackingActionResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public WarehousePackingTask? Task { get; set; }
}
