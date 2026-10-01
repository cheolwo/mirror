namespace 홍달.Services.Options;

public sealed class HongikHakdangCardOptions
{
    public const string SectionName = "HongikHakdangCards";

    public bool Enabled { get; set; }

    public string SourcePageUrl { get; set; } = "https://hihd.imweb.me/card";

    public string OriginalImageBaseUrl { get; set; } = "https://cdn.imweb.me/upload/";

    public bool DownloadImages { get; set; } = true;

    public string StorageFolder { get; set; } = "App_Data/hongik-hakdang-cards";

    public int TimeoutSeconds { get; set; } = 60;

    public int SyncIntervalHours { get; set; } = 24;

    public int MaxImageBytes { get; set; } = 20 * 1024 * 1024;

    public int MaxConcurrentDownloads { get; set; } = 4;

    public bool DeliveryEnabled { get; set; }

    public bool PrepareVariantsOnSync { get; set; } = true;

    public string PublicBaseUrl { get; set; } = string.Empty;

    public string DefaultTimeZoneId { get; set; } = "Asia/Seoul";

    public int MediaUrlLifetimeMinutes { get; set; } = 2880;

    public int DeliveryPollingSeconds { get; set; } = 60;

    public int DeliveryBatchSize { get; set; } = 100;

    public int MaxDeliveryAttempts { get; set; } = 5;

    public int NotificationImageMaxBytes { get; set; } = 900_000;

    public int LockScreenImageMaxBytes { get; set; } = 2_500_000;
}
