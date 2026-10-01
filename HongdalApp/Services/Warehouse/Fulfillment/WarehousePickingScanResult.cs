namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehousePickingScanResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public WarehouseOrderPickingTask? Task { get; set; }
}
