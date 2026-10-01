namespace Hongdal.Domain.Community;

public sealed class PlatformCommunityPostTranslation
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public PlatformCommunityPost Post { get; set; } = null!;
    public string SourceLanguageCode { get; set; } = string.Empty;
    public string TargetLanguageCode { get; set; } = string.Empty;
    public string SourceContentHash { get; set; } = string.Empty;
    public string TranslatedTitle { get; set; } = string.Empty;
    public string TranslatedBody { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ProviderModelVersion { get; set; } = string.Empty;
    public bool IsHumanReviewed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
