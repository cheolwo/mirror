namespace Ssalddel.Contracts.Common.Settlement;

/// <summary>Existing specialist obligations and activity entries, projected for the authenticated worker. No new debt or transfer.</summary>
public sealed class WorkerWeeklyStatementDto
{
    public string WorkerUserId { get; set; } = "";
    public DateOnly CompletionWeekStartKst { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string PeriodBasisCode { get; set; } = "WorkCompletedAt";
    public IReadOnlyList<WorkerWeeklyStatementLineDto> Lines { get; set; } = [];
    public decimal KnownAccruedAmountKrw { get; set; }
    public decimal? AccruedAmountKrw { get; set; }
    public decimal KnownPaymentConditionReadyAmountKrw { get; set; }
    public decimal KnownHeldAmountKrw { get; set; }
    public decimal ReportedPaymentAmountKrw { get; set; }
    public decimal RecipientConfirmedAmountKrw { get; set; }
    public int UnknownAmountCount { get; set; }
    public int UnassignedCompletionTimeCount { get; set; }
    public int ExcludedDeliveryReferenceCount { get; set; }
    public decimal? ActualBankVerifiedAmountKrw { get; set; }
    public bool IsActualBankVerified { get; set; }
    public DateTime ObservedAtUtc { get; set; }
}

public sealed class WorkerWeeklyStatementLineDto
{
    public string StableId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string ActivityKind { get; set; } = "";
    public DateTime CompletedAtUtc { get; set; }
    public decimal? AccruedAmountKrw { get; set; }
    public decimal? NetAmountKrw { get; set; }
    public bool PaymentConditionReady { get; set; }
    public string ReadinessCode { get; set; } = "Unknown";
    public string WaitingReason { get; set; } = "";
    public string PaymentStatusCode { get; set; } = "Unknown";
    public decimal ReportedPaymentAmountKrw { get; set; }
    public decimal RecipientConfirmedAmountKrw { get; set; }
    public DateTime? PayoutScheduledAtUtc { get; set; }
    public string? AgreementId { get; set; }
    public bool IsActualBankVerified { get; set; }
}
