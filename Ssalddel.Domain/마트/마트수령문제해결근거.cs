namespace 살뜰.도메인.마트;

/// <summary>사진 검증이나 입금 사실을 대신하지 않는 인증 당사자의 진술입니다. 보호된 수령 JSON 안에 보존합니다.</summary>
public sealed record 마트수령문제해결근거
{
    public string Kind { get; init; } = "";
    public string ProposalId { get; init; } = "";
    public long MartOrderId { get; init; }
    public string OrderReference { get; init; } = "";
    public string AttemptStableId { get; init; } = "";
    public Guid HoldRequestId { get; init; }
    public string BuyerUserId { get; init; } = "";
    public string SellerUserId { get; init; } = "";
    public string ActorUserId { get; init; } = "";
    public Guid ClientRequestId { get; init; }
    public long ExpectedRevision { get; init; }
    public long ResultRevision { get; init; }
    public string OutcomeCode { get; init; } = "";
    public long ConfirmedQuantity { get; init; }
    public string SourceFingerprint { get; init; } = "";
    public bool? Granted { get; init; }
    public string EvidenceNote { get; init; } = "";
    public DateTime RecordedAtUtc { get; init; }
    public string InputDigest { get; init; } = "";
}
