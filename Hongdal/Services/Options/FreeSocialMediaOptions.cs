namespace 홍달.Services.Options;

public sealed class FreeSocialMediaOptions
{
    public const string SectionName = "FreeSocialMedia";

    public bool Enabled { get; set; }

    public int TimeoutSeconds { get; set; } = 20;

    public int MaxStartUrlsPerSource { get; set; } = 5;

    public int MaxItemsPerFeed { get; set; } = 10;

    public string UserAgent { get; set; } = "HongdalPublicFeedResearch/1.0";

    public RedditRssPublicContentOptions RedditRss { get; set; } = new();
}

public sealed class RedditRssPublicContentOptions
{
    public bool Enabled { get; set; }

    public string[] DefaultStartUrls { get; set; } = [];
}
