namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehousePackingTask
{
    public long Id { get; set; }

    public long PickingTaskId { get; set; }

    public string ChannelType { get; set; } = string.Empty;

    public string ChannelOrderNo { get; set; } = string.Empty;

    public long? WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string RecipientAddress { get; set; } = string.Empty;

    public int LineCount { get; set; }

    public string Status { get; set; } = WarehousePackingStatusCodes.ReadyForPacking;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
