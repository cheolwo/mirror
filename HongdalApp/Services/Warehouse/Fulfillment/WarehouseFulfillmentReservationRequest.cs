namespace HongdalApp.Services.Warehouse.Fulfillment;

public sealed class WarehouseFulfillmentReservationRequest
{
    public long InboundProductId { get; set; }

    public int Quantity { get; set; }

    public WarehousePickPlan PickPlan { get; set; } = new();
}
