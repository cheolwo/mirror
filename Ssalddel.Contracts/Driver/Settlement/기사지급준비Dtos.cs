namespace Ssalddel.Contracts.Driver.Settlement;

public sealed class 기사지급준비목록응답
{
    public string DriverId { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public bool HasSettlementAccount { get; set; }

    public string SettlementAccountVerificationStatus { get; set; } = string.Empty;

    public decimal ExpectedPayoutTotal { get; set; }

    public decimal ReadyForPayoutPreparationTotal { get; set; }

    public decimal OnSiteCollectionConfirmedTotal { get; set; }

    public IReadOnlyList<기사지급준비항목응답> Items { get; set; } = [];
}

public sealed class 기사지급준비항목응답
{
    public long TransportId { get; set; }

    public string TransportNumber { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;

    public DateTime CompletedAtUtc { get; set; }

    public decimal? ExpectedPayoutAmount { get; set; }

    public string CurrencyCode { get; set; } = "KRW";

    public string AmountSource { get; set; } = string.Empty;

    public string SettlementTiming { get; set; } = string.Empty;

    public string ShipperPaymentStatus { get; set; } = string.Empty;

    public string FreightSettlementStatus { get; set; } = string.Empty;

    public string ReadinessCode { get; set; } = string.Empty;

    public string ReadinessMessage { get; set; } = string.Empty;

    public bool IsReadyForPayoutPreparation { get; set; }
}

public static class 기사지급준비상태코드
{
    public const string 원천의뢰없음 = "SourceRequestMissing";
    public const string 지급예정운임없음 = "ExpectedPayoutMissing";
    public const string 현장수금대기 = "OnSiteCollectionPending";
    public const string 현장수금확인 = "OnSiteCollectionConfirmed";
    public const string 화주수납대기 = "ShipperCollectionPending";
    public const string 비정상운송검토보류 = "AbnormalTransportReviewHeld";
    public const string 정산계좌없음 = "SettlementAccountMissing";
    public const string 정산계좌미확인 = "SettlementAccountUnverified";
    public const string 지급준비가능 = "ReadyForPayoutPreparation";
}
