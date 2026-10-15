namespace Ssalddel.Contracts.Mart;

public static class MartReceiptResolutionCodes
{
    public const string FullReceiptReconfirmed = "FullReceiptReconfirmed";
    public const string OperationalReview = "OperationalReview";
}
public sealed class 마트수령해결제안요청
{
    public Guid ClientRequestId { get; set; }
    public string ExpectedAttemptStableId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public string OutcomeCode { get; set; } = "";
    public long ConfirmedQuantity { get; set; }
    public string EvidenceNote { get; set; } = "";
}
public sealed class 마트수령해결동의요청
{
    public Guid ClientRequestId { get; set; }
    public string ExpectedAttemptStableId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public string ProposalId { get; set; } = "";
    public bool Granted { get; set; }
    public long ConfirmedQuantity { get; set; }
    public string EvidenceNote { get; set; } = "";
}
/// <summary>구매자·판매자 본인의 해결 절차입니다. 타인 식별자·근거 원문·금융 상태를 노출하지 않습니다.</summary>
public sealed class 마트수령문제해결Dto
{
    public long MartOrderId { get; set; }
    public string AttemptStableId { get; set; } = "";
    public long Revision { get; set; }
    public string MyRole { get; set; } = "";
    public long TotalOrderQuantity { get; set; }
    public bool HoldRecorded { get; set; }
    public string? HoldReasonCode { get; set; }
    public string ResolutionStatus { get; set; } = "";
    public string NextActor { get; set; } = "";
    public string WaitingReasonCode { get; set; } = "";
    public string? ProposalId { get; set; }
    public string? OutcomeCode { get; set; }
    public long? ProposedQuantity { get; set; }
    public bool BuyerConsented { get; set; }
    public bool SellerConsented { get; set; }
    public bool CanProposeResolution { get; set; }
    public bool CanConsentResolution { get; set; }
    public bool PaymentReleaseReady { get; set; }
    public bool IsReplay { get; set; }
    public Guid? ResponseRequestId { get; set; }
}
