namespace Ssalddel.Domain.Notifications;

/// <summary>원문·주소·연락처·사진은 보관하지 않는 운영 관찰. 원장의 상태 변경 권위가 없다.</summary>
public sealed class OperationsAlert
{
    public string Id { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string ProblemCode { get; set; } = "";
    public string Severity { get; set; } = "Warning";
    public string Status { get; set; } = "Open";
    public int Episode { get; set; } = 1;
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastVerifiedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedBy { get; set; }
    public bool ReminderQueued { get; set; }
}

/// <summary>최초·재알림·요약의 채널별 at-least-once 발송. 외부 응답 유실 시 중복 가능.</summary>
public sealed class OperationsAlertOutbox
{
    public string Id { get; set; } = "";
    public string? AlertId { get; set; }
    public int Episode { get; set; }
    public string Kind { get; set; } = "Initial";
    public string Channel { get; set; } = "Push";
    public string RecipientUserId { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class OperationsAlertCheckpoint
{
    public string Id { get; set; } = "";
    public DateTime CursorUtc { get; set; }
    public string CursorId { get; set; } = "";
    public long Revision { get; set; } = 1;
    public DateTime? StartedAtUtc { get; set; }
}

public sealed class OperationsAlertReceipt
{
    public string Id { get; set; } = "";
    public string AlertId { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public Guid RequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public int Episode { get; set; }
}
