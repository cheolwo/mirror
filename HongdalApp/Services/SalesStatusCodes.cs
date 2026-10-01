namespace HongdalApp.Services;

public static class SalesStatusCodes
{
    public const string AccountConnected = "연결완료";

    public const string ProductReady = "판매준비";

    public const string ProductActive = "판매중";

    public const string ListingCompleted = "출품완료";

    public const string SyncNormal = "정상";

    public const string SyncReady = "연동준비";

    public const string SyncPending = "연동대기";

    public const string SyncManual = "수동관리";
}
