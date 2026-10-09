namespace Ssalddel.Domain.Community;

/// <summary>견적은 개인정보 사본 대신 원 의뢰의 지문과 최소 물품 조건을 보관합니다.</summary>
public sealed class NeighborhoodFulfillmentQuoteRecord
{
    public string SourceKind { get; set; } = "NeighborhoodDelivery";
    public long? ConsentRevision { get; set; }
    public long SourceRevision { get; set; }
    public string Leg { get; set; } = "initial";
    public string QuoteId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string InputDigest { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public decimal CostKrw { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal DistanceKm { get; set; }
    public DateTime? DesiredCompletionUtc { get; set; }
    public DateTime EstimatedCompletionUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsSimulation { get; set; }
}

/// <summary>의뢰별 유일한 현재 선택. Revision은 서버 간 동시 변경을 막는 SQL concurrency token입니다.</summary>
public sealed class NeighborhoodFulfillmentRecord
{
    public string SourceKind { get; set; } = "NeighborhoodDelivery";
    public bool IsAutomatic { get; set; }
    public string StateCode { get; set; } = "AwaitingRouting";
    public string? ReviewCode { get; set; }
    public string SelectionReason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string AttemptId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>전송 전 영속화하는 수행 시도. 결과 불명은 새 처리처 접수가 아닌 원 시도의 재확인을 요구합니다.</summary>
public sealed class NeighborhoodFulfillmentAttemptRecord
{
    public string SourceKind { get; set; } = "NeighborhoodDelivery";
    public long? ConsentRevision { get; set; }
    public long SourceRevision { get; set; }
    public string Leg { get; set; } = "initial";
    public decimal? ConfirmedIncurredCostKrw { get; set; }
    public string? ResultOperationId { get; set; }
    public string AttemptId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    public string SelectionDigest { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string? ExternalRequestId { get; set; }
    public decimal CostKrw { get; set; }
    public bool IsSimulation { get; set; }
    public bool SharingAgreed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DesiredCompletionUtc { get; set; }
    public DateTime EstimatedCompletionUtc { get; set; }
    public long Revision { get; set; }
    public Guid? CancelClientRequestId { get; set; }
    public string HistoryJson { get; set; } = "[]";
}

/// <summary>한 원 주문의 사전 조건과 최초 지원액을 보존합니다. 동의 갱신은 지원액을 다시 지급하지 않습니다.</summary>
public sealed class AutoFulfillmentConsentRecord
{
    public string SourceKind { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    public string InputDigest { get; set; } = string.Empty;
    public long Revision { get; set; }
    public decimal MaxUserChargeKrw { get; set; }
    public DateTime? DesiredCompletionUtc { get; set; }
    public string AllowedProvidersJson { get; set; } = "[]";
    public string InformationFieldsJson { get; set; } = "[]";
    public string NoticeVersion { get; set; } = string.Empty;
    public string ReceiptsJson { get; set; } = "[]";
    public bool SharingAgreed { get; set; }
    public bool AutomaticSubmissionAgreed { get; set; }
    public bool DirectPaymentAgreed { get; set; }
    public bool Revoked { get; set; }
    public decimal FrozenRestaurantSupportKrw { get; set; }
    public string? FrozenCostSharingPolicyJson { get; set; }
    public long? RestaurantSupportRevision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class RestaurantFulfillmentSupportRecord
{
    public long RestaurantId { get; set; }
    public long Revision { get; set; }
    public decimal FixedContributionKrw { get; set; }
    public string? CostSharingPolicyJson { get; set; }
    public bool Enabled { get; set; }
    public Guid ClientRequestId { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>개인정보 payload 없이 동일 원 요청의 수행을 재개하는 영속 작업입니다.</summary>
public sealed class AutoFulfillmentOutboxRecord
{
    public string Id { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public string StateCode { get; set; } = "Pending";
    public int ClaimRevision { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>모의 접수와 배정·픽업·전달을 개별 증거로 저장하며 시간 경과로 진행시키지 않습니다.</summary>
public sealed class FulfillmentMockJobRecord
{
    public string AttemptId { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ExternalRequestId { get; set; } = string.Empty;
    public string StateCode { get; set; } = "Accepted";
    public string Leg { get; set; } = "initial";
    public long Revision { get; set; }
    public long AssignmentRevision { get; set; }
    public string? ExternalDriverReference { get; set; }
    public Guid? LastClientRequestId { get; set; }
    public string? LastRequestDigest { get; set; }
    public decimal? ConfirmedIncurredCostKrw { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
