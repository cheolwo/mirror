namespace Ssalddel.Contracts.Admin.Settlement;

public sealed class 배송료정산배치준비요청
{
    public string DriverId { get; set; } = string.Empty;
    public string? ScheduleCode { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public IReadOnlyList<long> PayoutRequestIds { get; set; } = [];
    /// <summary>화면에서 확인한 합계입니다. 정본 지급요청의 합계와 대조할 뿐 서버 금액을 바꾸지 않습니다.</summary>
    public decimal ConfirmedTotalAmount { get; set; }
    public string PolicyRevision { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class 배송료정산배치재검증요청
{
    public long ExpectedRevision { get; set; }
}

public sealed class 배송료정산배치응답
{
    public long BatchId { get; set; }
    public long Revision { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "KRW";
    public string ExecutionModeCode { get; set; } = string.Empty;
    public string PolicyRevision { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string PeriodBasisCode { get; set; } = "WorkCompletedAt";
    public DateTime? PayoutScheduledAtUtc { get; set; }
    public string PayoutTimeZoneId { get; set; } = "Asia/Seoul";
    /// <summary>은행 휴일 달력은 아직 연결되지 않았습니다.</summary>
    public bool BankCalendarApplied { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public decimal ApprovedTotalAmount { get; set; }
    public decimal ReadyTotalAmount { get; set; }
    public int ReadyCount { get; set; }
    public int HeldCount { get; set; }
    public DateTime LastValidatedAtUtc { get; set; }
    public bool IsIdempotentReplay { get; set; }
    public bool IsActualTransferCompleted { get; set; }
    public IReadOnlyList<배송료정산배치행응답> Lines { get; set; } = [];
}

public sealed class 배송료정산배치행응답
{
    public long PayoutRequestId { get; set; }
    public long TransportId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public decimal FrozenAmount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string HoldReasonCode { get; set; } = string.Empty;
    public string HoldMessage { get; set; } = string.Empty;
}
