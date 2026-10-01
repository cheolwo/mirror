namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehousePickInstruction
{
    public string BinCode { get; set; } = string.Empty;

    public string BinBarcode => $"BIN:{BinCode}";

    public string Sku { get; set; } = string.Empty;

    public string ProductBarcode => $"SKU:{Sku}";

    public string ProductName { get; set; } = string.Empty;

    public int RouteSequence { get; set; }

    public string RouteSortKey { get; set; } = string.Empty;

    public int RequestedQuantity { get; set; }

    public int PickQuantity { get; set; }

    public int RemainingQuantityAfterPick { get; set; }

    public string Reason { get; set; } = string.Empty;
}
