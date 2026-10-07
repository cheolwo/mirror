using FluentResults;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

public interface I생활협업UseCase
{
    Task<Result<NeighborhoodCollaborationResponse>> 신청Async(NeighborhoodCollaborationCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result<NeighborhoodCollaborationResponse>> 변경Async(string stableId, NeighborhoodCollaborationCommandRequest request, CancellationToken cancellationToken = default);
    Task<NeighborhoodCollaborationResponse?> 상세Async(string stableId, CancellationToken cancellationToken = default);
    Task<NeighborhoodCollaborationResponse?> 요청결과Async(Guid clientRequestId, string? stableId = null, CancellationToken cancellationToken = default);
    Task<NeighborhoodCollaborationListResponse> 내목록Async(string scope = "all", int page = 1, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> 참여기회Async(long sourcePostId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> 공개이력Async(long sourcePostId, CancellationToken cancellationToken = default);
}

/// <summary>서버 어댑터 전용. Controller나 공개 원장 조회로 노출하지 않습니다.</summary>
public interface I생활협업연결Query
{
    Task<생활협업연결Context?> 조회Async(string stableId, CancellationToken cancellationToken = default);
}

public sealed record 생활협업연결Context(string StableId, long Revision, long TermsRevision, string OwnerUserId,
    string RequesterUserId, string Kind, string StatusCode, bool AcceptedCurrentTerms,
    NeighborhoodCollaborationTerms Terms, string? LinkedDeliveryRequestId, long? SourcePostId, long? StorageOfferRevision = null);

public interface I생활협업공간Source
{
    Task<생활협업공간SourceSnapshot?> 조회Async(string stableId, CancellationToken cancellationToken = default);
}
public sealed record 생활협업공간SourceSnapshot(string StableId, long Revision, string OwnerUserId, string Title,
    string? PublicNeighborhoodRegionKey, bool Available);

public interface I생활협업보관상태Source
{
    Task<bool> 반환확인Async(string collaborationId, long termsRevision, CancellationToken cancellationToken = default);
    Task<생활협업보관예약Snapshot?> 예약조회Async(string collaborationId, CancellationToken cancellationToken = default);
}
public sealed record 생활협업보관예약Snapshot(long TermsRevision, string StatusCode);

/// <summary>공간 CAS 전에 협업 판본을 영속 고정합니다. 저장 불명확 시 공간의 결과 확인/쓰기 fence 후에만 해제합니다.</summary>
public interface I생활협업보관예약Guard
{
    Task<Result<생활협업예약IntentRecord>> 획득Async(string collaborationId, long termsRevision, string spaceId,
        string actorUserId, Guid requestId, long expectedSpaceRevision, string fingerprint, CancellationToken cancellationToken = default);
    Task<Result<생활협업예약IntentRecord>> 확정Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default);
    Task<Result<생활협업예약IntentRecord>> 해제Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default);
    Task<생활협업예약IntentRecord?> 조회Async(string collaborationId, Guid requestId, string actorUserId, CancellationToken cancellationToken = default);
}

public interface I생활협업배송Source
{
    Task<생활협업배송Snapshot?> 조회Async(string requestId, CancellationToken cancellationToken = default);
}
public sealed record 생활협업배송Snapshot(string RequestId, string OwnerUserId, long? SourcePostId,
    bool IsNeighborhoodDelivery, bool Completed, bool Cancelled);

/// <summary>별도 보호 컬렉션만 읽고 서버 CAS로 변경하며 generic 원장 투영을 발행하지 않습니다.</summary>
public interface I생활협업Store
{
    Task<생활협업Record?> 조회Async(string stableId, CancellationToken cancellationToken = default);
    Task<bool> 생성Async(생활협업Record record, CancellationToken cancellationToken = default);
    Task<bool> 교체Async(생활협업Record record, long expectedRevision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<생활협업Record>> 목록Async(생활협업StoreQuery query, CancellationToken cancellationToken = default);
}
public sealed record 생활협업StoreQuery(string? UserId = null, string Scope = "all", long? SourcePostId = null, int Skip = 0, int Take = 21);

public sealed class 생활협업Record
{
    public string StableId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long TermsRevision { get; set; }
    public Guid CreateRequestId { get; set; }
    public string CreateFingerprint { get; set; } = string.Empty;
    public long? SourcePostId { get; set; }
    public DateTime? SourceUpdatedAtUtc { get; set; }
    public string? SourceConditionsFingerprint { get; set; }
    public long? SpaceRevision { get; set; }
    public string? TermsAuthorRoleCode { get; set; }
    public string? ProviderRoleCode { get; set; }
    public 생활배송연결IntentRecord? DeliveryIntent { get; set; }
    public string SourceTitle { get; set; } = string.Empty;
    public string? PublicNeighborhoodRegionKey { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string RequesterUserId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string StatusCode { get; set; } = NeighborhoodCollaborationStates.Requested;
    public NeighborhoodCollaborationTerms Terms { get; set; } = new();
    public bool OwnerAgreed { get; set; }
    public bool RequesterAgreed { get; set; }
    public string? OwnerPrivacyNoticeVersion { get; set; }
    public string? RequesterPrivacyNoticeVersion { get; set; }
    public bool OwnerPublicHistoryConsented { get; set; }
    public bool RequesterPublicHistoryConsented { get; set; }
    public string? CompletionProposerUserId { get; set; }
    public string? LinkedDeliveryRequestId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public List<생활협업참여Record> Participants { get; set; } = [];
    public List<생활협업명령Receipt> Receipts { get; set; } = [];
    public List<NeighborhoodCollaborationHistoryResponse> History { get; set; } = [];
    public List<생활협업예약IntentRecord> StorageReservationIntents { get; set; } = [];
}
public sealed class 생활협업예약IntentRecord
{
    public Guid RequestId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string SpaceId { get; set; } = string.Empty;
    public long TermsRevision { get; set; }
    public long ExpectedSpaceRevision { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string StateCode { get; set; } = "pending";
    public DateTime RecordedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
public sealed class 생활협업참여Record
{
    public string UserId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "requested";
    public bool OwnerAccepted { get; set; }
    public bool RequesterAccepted { get; set; }
    public bool PublicHistoryConsented { get; set; }
}
public sealed class 생활협업명령Receipt
{
    public Guid ClientRequestId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
}
