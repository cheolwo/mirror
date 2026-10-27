namespace Ssalddel.Contracts.Food;

/// <summary>수락 후 취소의 비용 조건 제안. 제안만으로 동의·취소·환불을 만들지 않습니다.</summary>
public sealed class 음식주문취소합의제안요청
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedOrderRevision { get; set; }
    public decimal? OrdererCostKrw { get; set; }
    public decimal? RestaurantCostKrw { get; set; }
    public string CostBasisReference { get; set; } = string.Empty;
}

public sealed class 음식주문취소합의변경요청
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedAgreementRevision { get; set; }
    public long ExpectedOrderRevision { get; set; }
    public bool Granted { get; set; }
}

/// <summary>고객 개인정보와 다른 당사자의 사용자 ID는 포함하지 않습니다.</summary>
public sealed class 음식주문취소합의응답
{
    public string AgreementId { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long FrozenOrderRevision { get; set; }
    public long CurrentOrderRevision { get; set; }
    public string OrderStatusCode { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public decimal OrdererCostKrw { get; set; }
    public decimal RestaurantCostKrw { get; set; }
    public string CostBasisReference { get; set; } = string.Empty;
    public bool OrdererAgreed { get; set; }
    public bool RestaurantAgreed { get; set; }
    public string CurrentPartyCode { get; set; } = string.Empty;
    public bool CanConsent { get; set; }
    public bool CanApply { get; set; }
    public string BlockReasonCode { get; set; } = string.Empty;
    public string FinanceStatusCode { get; set; } = "PendingExternalSettlement";
    public DateTime? AppliedAtUtc { get; set; }
    public Guid? ConfirmedClientRequestId { get; set; }
    public bool IsIdempotentReplay { get; set; }
}
