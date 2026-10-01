namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehouseOrderPickingLine
{
    public long NotificationId { get; set; }

    public int RouteSequence { get; set; }

    public string BinCode { get; set; } = string.Empty;

    public string BinBarcode => $"BIN:{BinCode}";

    public string Sku { get; set; } = string.Empty;

    public string ProductBarcode => $"SKU:{Sku}";

    public string ProductName { get; set; } = string.Empty;

    public int PickQuantity { get; set; }

    public bool BinScanned { get; set; }

    public bool ProductScanned { get; set; }

    public DateTime? PickedAt { get; set; }

    public bool IsPicked => BinScanned && ProductScanned;
}
