namespace 홍달.Services.Options;

public sealed class ApifySocialMediaOptions
{
    public const string SectionName = "ApifySocialMedia";

    /// <summary>
    /// 외부 비용이 발생하는 SNS 공개 자료 조사를 명시적으로 허용합니다.
    /// </summary>
    public bool Enabled { get; set; }

    public int MaxSearchTerms { get; set; } = 6;

    public int MaxStartUrlsPerSource { get; set; } = 5;

    public int MaxDraftItemsPerSource { get; set; } = 3;

    public ApifySocialMediaProviderOptions Reddit { get; set; } = new()
    {
        ActorId = "trudax~reddit-scraper-lite"
    };

    public ApifySocialMediaProviderOptions X { get; set; } = new()
    {
        ActorId = "apidojo~twitter-scraper-lite"
    };

    public ApifySocialMediaProviderOptions Instagram { get; set; } = new()
    {
        ActorId = "apify~instagram-scraper"
    };

    public ApifySocialMediaProviderOptions Facebook { get; set; } = new()
    {
        ActorId = "apify~facebook-posts-scraper"
    };
}

public sealed class ApifySocialMediaProviderOptions
{
    public bool Enabled { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public int ActorTimeoutSeconds { get; set; } = 120;

    public int MemoryMegabytes { get; set; } = 512;

    public int MaxDatasetItems { get; set; } = 10;

    public decimal MaxTotalChargeUsd { get; set; } = 0.25m;

    public string[] DefaultStartUrls { get; set; } = [];
}
