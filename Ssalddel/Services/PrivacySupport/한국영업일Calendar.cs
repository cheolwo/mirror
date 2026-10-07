using Microsoft.Extensions.Options;

namespace Ssalddel.Services.PrivacySupport;

public sealed class 보호지원Options
{
    public const string SectionName = "CommercePrivacySupport";
    public string CalendarVersion { get; set; } = string.Empty;
    public string[] Holidays { get; set; } = [];
    public DateOnly? CalendarVerifiedFrom { get; set; }
    public DateOnly? CalendarVerifiedThrough { get; set; }
    // 권리 요청의 내부 응답 목표입니다. 권리 종류별 법정 기한 판단을 대신하지 않습니다.
    public int RightsResponseCalendarDays { get; set; } = 10;
    public bool ClosedDisputePurgeEnabled { get; set; }
    public bool ClosedDisputeRetentionPolicyConfirmed { get; set; }
    public string ClosedDisputeRetentionPolicyVersion { get; set; } = string.Empty;
}

public sealed class 한국영업일Calendar(IOptions<보호지원Options> options)
{
    private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

    public (DateTime DueAtUtc, bool Verified, string Version) 기한(DateTime receivedAtUtc, int businessDays)
    {
        if (receivedAtUtc.Kind != DateTimeKind.Utc || businessDays < 1) throw new ArgumentException("UTC 접수 시각과 양수 영업일이 필요합니다.");
        var configured = options.Value;
        var holidays = new HashSet<DateOnly>();
        foreach (var text in configured.Holidays)
        {
            if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var holiday))
                throw new InvalidOperationException("영업일 휴일 설정은 yyyy-MM-dd여야 합니다.");
            holidays.Add(holiday);
        }
        var first = DateOnly.FromDateTime(receivedAtUtc + KoreaOffset);
        var date = first;
        for (var count = 0; count < businessDays;)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(date)) count++;
        }
        // 접수일 다음 영업일부터 계산하고 마지막 날 한국 시간 끝을 기한으로 둡니다.
        var due = DateTime.SpecifyKind(date.AddDays(1).ToDateTime(TimeOnly.MinValue) - KoreaOffset, DateTimeKind.Utc).AddTicks(-1);
        var verified = !string.IsNullOrWhiteSpace(configured.CalendarVersion)
                       && configured.CalendarVerifiedFrom is { } from && first >= from
                       && configured.CalendarVerifiedThrough is { } through && date <= through;
        return (due, verified, configured.CalendarVersion);
    }
}
