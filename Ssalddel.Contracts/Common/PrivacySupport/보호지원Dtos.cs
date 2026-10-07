namespace Ssalddel.Contracts.Common.PrivacySupport;

public static class 보호지원종류Codes
{
    public const string 분쟁 = "transaction-dispute";
    public const string 권리 = "privacy-rights";
    public const string 사고 = "privacy-incident";
}

public static class 보호지원출처Codes
{
    public const string 음식주문 = "food-order";
    public const string 생활배송 = "neighborhood-delivery";
    public const string 생활협업 = "neighborhood-collaboration";
    public const string 화물의뢰 = "cargo-request";
}

public sealed class 거래분쟁접수Request
{
    public Guid ClientRequestId { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PrivateEvidence { get; set; } = string.Empty;
}

public sealed class 개인정보권리접수Request
{
    public Guid ClientRequestId { get; set; }
    public string RequestKind { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class 개인정보사고접수Request
{
    public Guid ClientRequestId { get; set; }
    public DateTime KnownAtUtc { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string PrivateEvidence { get; set; } = string.Empty;
}

public sealed class 보호지원CommandRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PrivateEvidence { get; set; } = string.Empty;
    public string? LegalBasis { get; set; }
    public string? LegalReasonCode { get; set; }
    public DateTime? RetainUntilUtc { get; set; }
    public string? DeliveryEvidenceRef { get; set; }
    public string? RecipientUserId { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public DateTime? HoldReviewAtUtc { get; set; }
    public 개인정보사고판정Request? IncidentAssessment { get; set; }
}

/// <summary>가능성·실제 유출·기관 신고 판단은 서로 독립적으로 기록합니다. 법정 요건을 검토한 담당자가 입력합니다.</summary>
public sealed class 개인정보사고판정Request
{
    public string OccurrenceCode { get; set; } = "unknown";
    public int? AffectedPersonCount { get; set; }
    public bool SensitiveOrUniqueIdentifierAffected { get; set; }
    public bool ExternalIllegalAccess { get; set; }
    public bool? IndividualNoticeRequired { get; set; }
    public bool? AuthorityReportRequired { get; set; }
    public string LegalBasis { get; set; } = string.Empty;
}

public sealed class 보호지원CaseResponse
{
    public bool RetentionPending { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string RequestKind { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? PreparedProgress { get; set; }
    public string? PreparedResult { get; set; }
    public string? LegalBasis { get; set; }
    public string? LegalReasonCode { get; set; }
    public DateTime? RetainUntilUtc { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public DateTime? ProgressDueAtUtc { get; set; }
    public DateTime? ResultDueAtUtc { get; set; }
    public DateTime? ProgressNotifiedAtUtc { get; set; }
    public DateTime? ResultNotifiedAtUtc { get; set; }
    public bool BusinessCalendarVerified { get; set; }
    public string BusinessCalendarVersion { get; set; } = string.Empty;
    public bool ProgressOverdue { get; set; }
    public bool ResultOverdue { get; set; }
    public DateTime? KnownAtUtc { get; set; }
    public DateTime? IncidentDeadlineAtUtc { get; set; }
    public bool IncidentOverdue { get; set; }
    public bool IncidentNoticeCoverageVerified { get; set; }
    public 개인정보사고판정Request? IncidentAssessment { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<보호지원HistoryResponse> History { get; set; } = [];
    public IReadOnlyList<보호지원통지Response> Notifications { get; set; } = [];
    public bool Replay { get; set; }
}

public sealed class 보호지원HistoryResponse
{
    public string Action { get; set; } = string.Empty;
    public string ActorRoleCode { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}

public sealed class 보호지원통지Response
{
    public string Kind { get; set; } = string.Empty;
    public string RecipientRoleCode { get; set; } = string.Empty;
    public DateTime? RecordedDeliveredAtUtc { get; set; }
}

public sealed class 보호지원ListResponse
{
    public IReadOnlyList<보호지원CaseResponse> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public bool HasMore { get; set; }
}

public sealed class 보호지원증거Response
{
    public string CaseId { get; set; } = string.Empty;
    public IReadOnlyList<보호지원증거Item> Items { get; set; } = [];
    public IReadOnlyList<string> AuthorizedRecipientIds { get; set; } = [];
}

public sealed class 보호지원증거Item
{
    public string Text { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}
