using Ssalddel.Contracts.Common.PrivacySupport;

namespace Ssalddel.Services.PrivacySupport;

public sealed class 보호지원Record
{
    public string CaseId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string RequestKind { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "received";
    public string OwnerUserId { get; set; } = string.Empty;
    public string[] PartyUserIds { get; set; } = [];
    public string? AssignedAdminUserId { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    // 접수 요약도 연락처/주소를 포함할 수 있어 저장소에서 전체 payload를 보호합니다.
    public string Summary { get; set; } = string.Empty;
    public string? PreparedProgress { get; set; }
    public string? PreparedResult { get; set; }
    public int ReviewCycle { get; set; } = 1;
    public string? LegalBasis { get; set; }
    public string? LegalReasonCode { get; set; }
    public DateTime? RetainUntilUtc { get; set; }
    public string? RightsExecutionEvidenceRef { get; set; }
    public DateTime? RightsExecutionConfirmedAtUtc { get; set; }
    public bool SourceHoldActive { get; set; }
    public DateTime? SourceHoldReviewAtUtc { get; set; }
    public string? SourceHoldLegalBasis { get; set; }
    public 보호지원보존Intent? RetentionIntent { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? PurgeAfterAtUtc { get; set; }
    public string PurgeStateCode { get; set; } = string.Empty;
    public string RetentionPolicyVersion { get; set; } = string.Empty;
    public Guid CreateRequestId { get; set; }
    public string CreateFingerprint { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ProgressDueAtUtc { get; set; }
    public DateTime? ResultDueAtUtc { get; set; }
    public bool BusinessCalendarVerified { get; set; }
    public string BusinessCalendarVersion { get; set; } = string.Empty;
    public DateTime? KnownAtUtc { get; set; }
    public DateTime? IncidentDeadlineAtUtc { get; set; }
    public 개인정보사고판정Request? IncidentAssessment { get; set; }
    public List<보호지원사고판정Evidence> IncidentAssessments { get; set; } = [];
    public List<보호지원Evidence> Evidence { get; set; } = [];
    public List<보호지원History> History { get; set; } = [];
    public List<보호지원Receipt> Receipts { get; set; } = [];
    public List<보호지원Notification> Notifications { get; set; } = [];
}

public sealed record 보호지원Evidence(string ActorUserId, string Text, DateTime RecordedAtUtc);
public sealed record 보호지원사고판정Evidence(string ActorUserId, 개인정보사고판정Request Assessment, DateTime RecordedAtUtc);
public sealed record 보호지원History(string Action, string ActorRoleCode, string ActorUserId, DateTime RecordedAtUtc);
public sealed record 보호지원Receipt(Guid RequestId, string ActorUserId, string Fingerprint);
public sealed record 보호지원Notification(string Kind, string RecipientUserId, string RecipientRoleCode,
    string EvidenceRef, DateTime DeliveredAtUtc, DateTime RecordedAtUtc, int ReviewCycle = 1,
    string ContentFingerprint = "", string AssessmentFingerprint = "");
public sealed record 보호지원보존Intent(Guid RequestId, string Fingerprint, string ActorUserId, string Action,
    string LegalBasis, DateTime? ReviewAtUtc, DateTime RequestedAtUtc, long Sequence = 0);
public sealed record 보호지원보존적용(long Sequence, bool Hold, DateTime? ReviewAtUtc);

public interface I보호지원Store
{
    Task<보호지원Record?> 조회Async(string caseId, CancellationToken cancellationToken = default);
    Task<bool> 생성Async(보호지원Record record, CancellationToken cancellationToken = default);
    Task<bool> 교체Async(보호지원Record record, long expectedRevision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<보호지원Record>> 목록Async(string? userId, string? kind, int skip, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken cancellationToken = default);
    Task<bool> 파기Async(보호지원Record claimedRecord, DateTime nowUtc, CancellationToken cancellationToken = default);
}

public sealed record 보호지원Source(string[] PartyUserIds);
public interface I보호지원SourceResolver
{
    Task<보호지원Source?> 조회Async(string sourceKind, string sourceId, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>실제 실행 원장을 확인한 어댑터만 권리 처리 완료를 반환할 수 있습니다. 문안·담당자 확인은 실행 증거가 아닙니다.</summary>
public interface I보호지원권리실행확인
{
    Task<bool> 완료확인Async(string caseId, string ownerUserId, string requestKind, string evidenceRef, CancellationToken cancellationToken = default);
}

public interface I보호지원보존연결
{
    Task 보존정지Async(string caseId, string sourceKind, string sourceId, string legalBasis, DateTime reviewAtUtc, CancellationToken cancellationToken = default);
    Task 보존정지해제Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default);
    Task<DateTime?> 보존정지조회Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default);
    Task<bool> 보존정지해제확인Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default);
    Task 보존상태적용Async(string caseId, string sourceKind, string sourceId, long sequence, bool hold, string legalBasis,
        DateTime? reviewAtUtc, CancellationToken cancellationToken = default);
    Task<보호지원보존적용?> 보존적용조회Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default);
}
