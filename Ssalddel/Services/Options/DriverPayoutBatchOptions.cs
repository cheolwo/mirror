namespace 살뜰.Services.Options;

/// <summary>확정 운송별 지급 요청을 명세로 묶습니다. 실제 송금 설정이 아닙니다.</summary>
public sealed class DriverPayoutBatchOptions
{
    public const string SectionName = "DriverPayoutBatch";
    public bool Enabled { get; set; }
    public string DefaultScheduleCode { get; set; } = "Weekly";
    public string PolicyRevision { get; set; } = "transport-payout-r73";
    public DayOfWeek WeekStartDay { get; set; } = DayOfWeek.Wednesday;
    public DayOfWeek PayoutDay { get; set; } = DayOfWeek.Thursday;
    public int PayoutHourLocal { get; set; } = 9;
}
