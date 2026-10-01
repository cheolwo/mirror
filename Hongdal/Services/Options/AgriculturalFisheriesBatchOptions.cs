namespace 홍달.Services.Options;

public sealed class AgriculturalFisheriesBatchOptions
{
    public const string SectionName = "AgriculturalFisheriesBatch";

    public bool Enabled { get; set; }

    public string TimeZoneId { get; set; } = "Asia/Seoul";

    public int ImmediateRetryCount { get; set; } = 1;

    public bool KamisDailyEnabled { get; set; } = true;

    public string KamisDailyCronExpression { get; set; } = "0 30 6 * * ?";

    public int KamisDailyDaysBehind { get; set; } = 1;

    public bool KamisMonthlyEnabled { get; set; } = true;

    public string KamisMonthlyCronExpression { get; set; } = "0 0 7 2 * ?";

    public int KamisMonthlyLookbackMonths { get; set; } = 12;

    public bool UsdaMonthlyEnabled { get; set; } = true;

    public string UsdaMonthlyCronExpression { get; set; } = "0 30 7 10 * ?";

    public int UsdaLookbackYears { get; set; } = 1;
}
