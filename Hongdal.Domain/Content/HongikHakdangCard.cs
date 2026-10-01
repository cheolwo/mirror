namespace Hongdal.Domain.Content;

public sealed class HongikHakdangCardCollection
{
    public long Id { get; set; }

    public string SourceKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAdminEnabled { get; set; } = true;

    public DateTime LastSeenAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<HongikHakdangCardCollectionItem> Items { get; set; } =
        new List<HongikHakdangCardCollectionItem>();
}

public sealed class HongikHakdangCard
{
    public const string ImagePendingStatus = "Pending";
    public const string ImageDownloadedStatus = "Downloaded";
    public const string ImageFailedStatus = "Failed";

    public long Id { get; set; }

    public string SourceKey { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string OriginalImageUrl { get; set; } = string.Empty;

    public string? ThumbnailImageUrl { get; set; }

    public string? RelatedUrl { get; set; }

    public string? LocalImagePath { get; set; }

    public string? ImageContentType { get; set; }

    public long? ImageSizeBytes { get; set; }

    public string? ImageSha256 { get; set; }

    public string ImageDownloadStatus { get; set; } = ImagePendingStatus;

    public string? ImageDownloadError { get; set; }

    public DateTime? ImageDownloadedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAdminEnabled { get; set; } = true;

    public bool IsCommunityPublicationApproved { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<HongikHakdangCardCollectionItem> Collections { get; set; } =
        new List<HongikHakdangCardCollectionItem>();

    public ICollection<HongikHakdangCardImageVariant> ImageVariants { get; set; } =
        new List<HongikHakdangCardImageVariant>();
}

public sealed class HongikHakdangCardCollectionItem
{
    public long CollectionId { get; set; }

    public HongikHakdangCardCollection Collection { get; set; } = null!;

    public long CardId { get; set; }

    public HongikHakdangCard Card { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime LastSeenAtUtc { get; set; }
}

public static class HongikHakdangCardImageVariantKinds
{
    public const string Notification = "Notification";
    public const string LockScreenPortrait = "LockScreenPortrait";
}

public sealed class HongikHakdangCardImageVariant
{
    public long Id { get; set; }

    public long CardId { get; set; }

    public HongikHakdangCard Card { get; set; } = null!;

    public string VariantKind { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public string LocalImagePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = "image/jpeg";

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string SourceImageSha256 { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class HongikHakdangCardDeliveryModes
{
    public const string EveryLock = "EveryLock";
    public const string Daily = "Daily";
    public const string Manual = "Manual";

    public static bool IsSupported(string? value)
        => value is EveryLock or Daily or Manual;
}

public sealed class HongikHakdangCardDeliveryPreference
{
    public string UserId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string DeliveryMode { get; set; } = HongikHakdangCardDeliveryModes.EveryLock;

    public bool PushEnabled { get; set; }

    public int LocalDeliveryMinute { get; set; } = 8 * 60;

    public string TimeZoneId { get; set; } = "Asia/Seoul";

    public bool ShuffleWithoutRepeats { get; set; } = true;

    public string? PreferredCollectionKey { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class HongikHakdangDailyCardSelection
{
    public long Id { get; set; }

    public DateOnly SelectionDate { get; set; }

    public string TimeZoneId { get; set; } = "Asia/Seoul";

    public long CardId { get; set; }

    public HongikHakdangCard Card { get; set; } = null!;

    public DateTime SelectedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class HongikHakdangCardDeliveryOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

public sealed class HongikHakdangCardDeliveryOutbox
{
    public long Id { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public long InstallationId { get; set; }

    public Hongdal.Domain.Notifications.HongdalMobilePushInstallation Installation { get; set; } = null!;

    public long CardId { get; set; }

    public HongikHakdangCard Card { get; set; } = null!;

    public DateOnly SelectionDate { get; set; }

    public string Status { get; set; } = HongikHakdangCardDeliveryOutboxStatuses.Pending;

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;

    public string? LastError { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
