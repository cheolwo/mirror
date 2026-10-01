namespace WarehouseManagerApp.Services;

public static class WarehouseManagerRoutes
{
    public const string Home = "/";
    public const string Warehouse = "/warehouse";
    public const string ExpectedInbounds = "/warehouse/inbounds/expected";
    public const string WarehouseExceptions = "/warehouse/exceptions";
    public const string WarehouseHistory = "/warehouse/history";
    public const string WarehouseSettings = "/warehouse/settings";
    public const string GeneralInventory = "/warehouse/general/inventory";
    public const string GeneralTransportHandoff = "/warehouse/general/transport-handoff";
    public const string ImportArrival = "/warehouse/import/arrival";
    public const string ImportCustoms = "/warehouse/import/customs";
    public const string ImportRelease = "/warehouse/import/release";
    public const string ImportDomesticHandoff = "/warehouse/import/domestic-handoff";
    public const string ApartmentArrivals = "/warehouse/apartment/arrivals";
    public const string ApartmentInbound = "/warehouse/apartment/inbound";
    public const string ApartmentAllocation = "/warehouse/apartment/allocation";
    public const string ApartmentHandoff = "/warehouse/apartment/handoff";
    public const string ApartmentUnclaimed = "/warehouse/apartment/unclaimed";
    public const string InboundWorkStart = "/work/inbound";
    public const string OutboundWorkStart = "/work/outbound";
    public const string PackingWorkStart = "/work/packing";
    public const string MarketFulfillmentWorkStart = "/work/market-fulfillment";
    public const string InternationalForwardingWorkStart = "/work/international-forwarding";
    public const string DeliveryAgencyWorkStart = "/work/delivery-agency";
    public const string Scan = "/scan";
    public const string WorkBoard = "/work-board";
    public const string PickingBatch = "/work/picking-batch";
    public const string InboundProductScan = "/work/inbound/products";
    public const string InboundInspection = "/work/inbound/inspection";
    public const string MartHome = "/mart";
    public const string MartInboundWorkStart = "/mart/work/mart-inbound";
    public const string MartReplenishmentWorkStart = "/mart/work/mart-replenishment";
    public const string MartPickingWorkStart = "/mart/work/mart-picking";
    public const string MartPickingPacking = "/mart/picking";
    public const string MartDeliveryPickupWorkStart = "/mart/work/mart-delivery-pickup";
    public const string MartWorkBoard = "/mart/work-board";

    public static string WorkbenchScan(string processCode) => $"/work/{processCode}/workbench";
}
