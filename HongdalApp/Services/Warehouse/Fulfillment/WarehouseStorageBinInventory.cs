namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehouseStorageBinInventory
{
    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string BinCode { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    public int PickingQuantity { get; set; }

    public int PickedQuantity { get; set; }

    public int OrderableQuantity => Math.Max(0, AvailableQuantity - ReservedQuantity - PickingQuantity - PickedQuantity);

    public DateTime? ReceivedAt { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public int PickPriority { get; set; }
}
