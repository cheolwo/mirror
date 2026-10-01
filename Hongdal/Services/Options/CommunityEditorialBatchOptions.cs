namespace 홍달.Services.Options;

public sealed class CommunityEditorialBatchOptions
{
    public const string SectionName = "CommunityEditorialBatch";

    public bool Enabled { get; set; }

    public string TimeZoneId { get; set; } = "Asia/Seoul";

    public int ImmediateRetryCount { get; set; } = 1;

    public bool KamisPriceBriefEnabled { get; set; } = true;

    public string KamisPriceBriefCronExpression { get; set; } = "0 50 6 * * ?";

    public int KamisPriceBriefMaxItems { get; set; } = 5;

    public bool ReflectionEnabled { get; set; } = true;

    public string ReflectionCronExpression { get; set; } = "0 0 9 ? * MON,THU";

    public bool ActivityDigestEnabled { get; set; } = true;

    public string ActivityDigestCronExpression { get; set; } = "0 30 8 * * ?";

    public bool PrajnaPublicationEnabled { get; set; }

    public string PrajnaPublicationCronExpression { get; set; } = "0 15 9 * * ?";

}
