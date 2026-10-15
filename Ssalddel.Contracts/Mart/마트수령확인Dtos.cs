namespace Ssalddel.Contracts.Mart;

/// <summary>실제 마트 주문자의 전량 수령 확인입니다. 결제·보수 확정·송금 요청이 아닙니다.</summary>
public sealed class 마트수령확인요청
{
    public Guid ClientRequestId { get; set; }
    public string ExpectedAttemptStableId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public bool FullReceiptConfirmed { get; set; }
}

public sealed class 마트수령문제신고요청
{
    public Guid ClientRequestId { get; set; }
    public string ExpectedAttemptStableId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public string ReasonCode { get; set; } = "";
}

/// <summary>수령과 보류의 현재 정본입니다. 고객·주소·전표 원문·금액·보호 JSON은 반환하지 않습니다.</summary>
public sealed class 마트수령확인Dto
{
    public long MartOrderId { get; set; }
    public string OrderReference { get; set; } = "";
    public string AttemptStableId { get; set; } = "";
    public long Revision { get; set; }
    public bool CanConfirmReceipt { get; set; }
    public bool CanReportIssue { get; set; }
    public bool ReceiptConfirmed { get; set; }
    public bool ReceiptRecorded { get; set; }
    public bool HoldRecorded { get; set; }
    public bool PaymentReleaseReady { get; set; }
    public string? ReceiptId { get; set; }
    public long? ReceiptRevision { get; set; }
    public long? ConfirmedQuantity { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? HoldReasonCode { get; set; }
    public DateTime? HeldAtUtc { get; set; }
    public string WaitingReasonCode { get; set; } = "";
    public bool IsReplay { get; set; }
}
