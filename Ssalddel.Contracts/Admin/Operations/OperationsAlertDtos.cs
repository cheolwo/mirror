namespace Ssalddel.Contracts.Admin.Operations;

public sealed record OperationsAlertDto(string Id, int Episode, long Revision, string Severity,
    string Status, string Title, string Action, string SourceHref, DateTime CreatedAtUtc,
    DateTime LastVerifiedAtUtc, bool SourceCurrent, string[] DeliveryStates)
{
    /// <summary>읽기 전용 대응 안내입니다. 구판 응답의 누락은 담당·기한 확인으로 해석하지 않습니다.</summary>
    public OperationsAlertHandlingDto? Handling { get; init; }
}

/// <summary>알림 확인과 고객 응답·사건 해결을 구분합니다. 원장 재조회 시각은 운영자 처리 기한이 아닙니다.</summary>
public sealed record OperationsAlertHandlingDto(string StateCode, string ResponsibleParty, string NextAction,
    DateTime? NextOperatorReviewAtUtc, DateTime? NextRevalidationAtUtc,
    string ServiceClockCode, string? ServiceTargetSummary);

public sealed record OperationsAlertsPage(bool Enabled, bool ExternalSendEnabled,
    IReadOnlyList<OperationsAlertDto> Items, int Page, bool HasMore);
public sealed record OperationsAlertAcknowledgeRequest(Guid ClientRequestId, long ExpectedRevision);
