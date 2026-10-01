namespace HongdalApp.Services.Warehouse.Fulfillment;

public interface IWarehousePickingPlanner
{
    WarehousePickPlan Plan(long warehouseId, string sku, int requestedQuantity);
}
