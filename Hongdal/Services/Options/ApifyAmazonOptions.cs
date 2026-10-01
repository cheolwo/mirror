namespace 홍달.Services.Options;

public sealed class ApifyAmazonOptions
{
    public const string SectionName = "ApifyAmazon";

    /// <summary>
    /// 외부 비용과 Amazon 이용 조건 확인을 위해 명시적으로 활성화해야 합니다.
    /// </summary>
    public bool Enabled { get; set; }

    public string ApiToken { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.apify.com/v2/";

    public string ActorId { get; set; } = "junglee~amazon-crawler";

    public int TimeoutSeconds { get; set; } = 150;

    public int ActorTimeoutSeconds { get; set; } = 120;

    public int MemoryMegabytes { get; set; } = 1024;

    public int MaxDatasetItems { get; set; } = 1;

    public decimal MaxTotalChargeUsd { get; set; } = 1m;

    public int MaxFeatureCount { get; set; } = 8;

    public int MaxAttributeCount { get; set; } = 30;

    public int MaxImageCount { get; set; } = 8;
}
