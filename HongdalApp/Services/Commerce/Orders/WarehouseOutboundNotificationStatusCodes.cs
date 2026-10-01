namespace HongdalApp.Services.Commerce.Orders;

public static class WarehouseOutboundNotificationStatusCodes
{
    public const string Ready = "출고대기";
    public const string Picking = "피킹중";
    public const string Picked = "피킹완료";
    public const string PackingReady = "포장대기";
    public const string Packing = "포장중";
    public const string Packed = "포장완료";
    public const string Blocked = "재고확인필요";
}
