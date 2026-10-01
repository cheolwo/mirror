namespace HongdalApp.Services.Warehouse.Fulfillment;

public static class WarehouseOrderPickingStatusCodes
{
    public const string ReadyForPicking = "피킹대기";
    public const string PickingInProgress = "피킹중";
    public const string PickingOnHold = "피킹보류";
    public const string PickingException = "관리자확인필요";
    public const string PickingCompleted = "피킹완료";
    public const string PackingReady = "포장대기";
    public const string Cancelled = "피킹취소";
}
