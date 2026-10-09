namespace Ssalddel.Domain.Community;

/// <summary>개인 활동점은 보호 payload에만 저장한다. 실제 현재 GPS를 저장하거나 대체하지 않는다.</summary>
public sealed class NeighborhoodMemberParticipation
{
    public string UserId { get; set; } = "";
    public long Revision { get; set; }
    public long WorkLockRevision { get; set; }
    public bool ReceiveOffers { get; set; }
    public string PrivatePayload { get; set; } = "";
    public string? ActiveRequestId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>기존 운송 의뢰·실행 투영에 연결한 일반 회원의 한 건 수행. 기사 ID를 만들지 않는다.</summary>
public sealed class NeighborhoodMemberDelivery
{
    public string RequestId { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public long Revision { get; set; } = 1;
    public string StateCode { get; set; } = "Pending";
    public string? AssignedMemberUserId { get; set; }
    public Guid ConsentEvidenceId { get; set; }
    public string NoticeVersion { get; set; } = "";
    public decimal FareKrw { get; set; }
    public decimal? DistanceKm { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? PickupEvidenceId { get; set; }
    public string? DeliveryEvidenceId { get; set; }
    public string? IssueCode { get; set; }
    public string? SupportCaseId { get; set; }
    public string? PrivateIssuePayload { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>수신함과 재처리 가능한 푸시 Outbox를 같은 제안에 결속한다. 주소·좌표는 포함하지 않는다.</summary>
public sealed class NeighborhoodMemberOffer
{
    public string OfferId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string MemberUserId { get; set; } = "";
    public long ParticipationRevision { get; set; }
    public long DeliveryRevision { get; set; }
    public long Revision { get; set; } = 1;
    public string StateCode { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string PushStateCode { get; set; } = "Pending";
    public int PushAttempts { get; set; }
    public long PushClaimRevision { get; set; }
    public DateTime? PushClaimUntilUtc { get; set; }
    public DateTime? PushLastAttemptUtc { get; set; }
}

public sealed class NeighborhoodMemberReceipt
{
    public string ActorUserId { get; set; } = "";
    public Guid ClientRequestId { get; set; }
    public string Kind { get; set; } = "";
    public string AggregateId { get; set; } = "";
    public string InputDigest { get; set; } = "";
    public DateTime RecordedAtUtc { get; set; }
}

public sealed class NeighborhoodMemberPhoto
{
    public string EvidenceId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public Guid UploadRequestId { get; set; }
    public long WorkRevision { get; set; }
    public string StageCode { get; set; } = "";
    public string InputDigest { get; set; } = "";
    public string StoredSha256 { get; set; } = "";
    public string PrivatePayload { get; set; } = "";
    public string StateCode { get; set; } = "Uploaded";
    public DateTime CreatedAtUtc { get; set; }
}
