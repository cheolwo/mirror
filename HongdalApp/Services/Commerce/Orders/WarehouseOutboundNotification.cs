namespace HongdalApp.Services.Commerce.Orders;

using HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehouseOutboundNotification
{
    public long Id { get; set; }

    public string OrderScope { get; set; } = string.Empty;

    public string ChannelType { get; set; } = string.Empty;

    public string ChannelOrderNo { get; set; } = string.Empty;

    public long? WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string WarehouseManagerName { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public int RequestedQuantity { get; set; }

    public string RecipientName { get; set; } = string.Empty;

    public string RecipientAddress { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public WarehousePickPlan? PickPlan { get; set; }

    public DateTime CreatedAt { get; set; }
}
