namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehouseOrderPickingTask
{
    public long Id { get; set; }

    public string ChannelType { get; set; } = string.Empty;

    public string ChannelOrderNo { get; set; } = string.Empty;

    public long? WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string RecipientAddress { get; set; } = string.Empty;

    public string Status { get; set; } = WarehouseOrderPickingStatusCodes.ReadyForPicking;

    public string? ExceptionReason { get; set; }

    public IReadOnlyList<WarehouseOrderPickingLine> Lines { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public int PickedLineCount => Lines.Count(x => x.IsPicked);

    public int TotalLineCount => Lines.Count;

    public WarehouseOrderPickingLine? NextLine => Lines
        .OrderBy(x => x.RouteSequence)
        .FirstOrDefault(x => !x.IsPicked);
}
