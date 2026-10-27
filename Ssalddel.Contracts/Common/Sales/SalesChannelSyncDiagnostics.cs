namespace Ssalddel.Contracts.Common.Sales;

public static class SalesChannelSyncIssueCodes
{
    public const string ProviderUnavailable = "ProviderUnavailable";
    public const string FeedReadFailed = "FeedReadFailed";
    public const string OrderInputInvalid = "OrderInputInvalid";
    public const string ProductMappingMissing = "ProductMappingMissing";
    public const string ContractNotSellable = "ContractNotSellable";
    public const string AllocationIncomplete = "AllocationIncomplete";
    public const string ReservationConflict = "ReservationConflict";
    public const string HistoryLimitReached = "HistoryLimitReached";
    public static string Message(string code) => code switch
    {
        ProviderUnavailable => "주문 수집 연결이 아직 준비되지 않았습니다.",
        FeedReadFailed => "채널 주문을 읽지 못했습니다. 연결 상태를 확인해 주세요.",
        OrderInputInvalid => "채널 주문의 품목·수량·식별값을 확인해야 합니다.",
        ProductMappingMissing => "연결되지 않은 품목이 있어 주문 전체의 출고를 보류했습니다.",
        ContractNotSellable => "판매가 허용되지 않은 재고가 연결돼 출고를 보류했습니다.",
        AllocationIncomplete => "재고·출고 창고 배정이 끝나지 않아 주문 전체를 보류했습니다.",
        ReservationConflict => "계획 이후 재고가 바뀌어 출고 예약을 보류했습니다.",
        HistoryLimitReached => "진단 보관 한도에 도달했습니다. 수집 커서를 유지하며 운영자 확인이 필요합니다.",
        _ => "주문 수집 상태를 확인해야 합니다."
    };
}

public sealed record 판매채널주문보류진단(long AccountId, string ChannelType, string OrderKey,
    string IssueCode, DateTime FirstObservedAtUtc, DateTime LastObservedAtUtc,
    string RetryStateCode, IReadOnlyList<string> ItemKeys);
