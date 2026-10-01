namespace Hongdal.Domain.Community;

public sealed class PlatformCommunityPost
{
    public long Id { get; set; }
    public string AppKey { get; set; } = "platform";
    public string Category { get; set; } = "자유";
    public string WorkflowTag { get; set; } = "국내 화물 운송";
    public string RoleTag { get; set; } = "플랫폼 구성원";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? OriginalLanguageCode { get; set; }
    public string? SharedLinkUrl { get; set; }
    public string? SalesOfferJson { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public string? AuthorUserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public bool IsAuthorDisplayCountryPublic { get; set; }
    public string? AuthorDisplayCountryCode { get; set; }
    public string? AuthorDisplayCountryName { get; set; }
    public bool IsReportBoardPost { get; set; }
    public string? ReporterDisplayName { get; set; }
    public string? ReportedDisplayName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string PublicationStatusCode { get; set; } = PlatformCommunityPostPublicationStatusCodes.Published;
    public DateTime? ScheduledPublishAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublicationNextAttemptAtUtc { get; set; }
    public DateTime? PublicationClaimedAtUtc { get; set; }
    public int PublicationAttemptCount { get; set; }
    public string? PublicationLastError { get; set; }
    public bool IsOperatorPinned { get; set; }
    public DateTime? OperatorPinnedAtUtc { get; set; }
    public bool IsCommunityMomentumPromoted { get; set; }
    public string? CommunityMomentumCode { get; set; }
    public string? CommunityMomentumMessage { get; set; }
    public int CommunityMomentumRoleParticipantCount { get; set; }
    public DateTime? CommunityMomentumUpdatedAtUtc { get; set; }
    public int RecommendationCount { get; set; }
    public int CommentCount { get; set; }
    public DateTime? LastEngagedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<PlatformCommunityPostAttachment> Attachments { get; set; } = new List<PlatformCommunityPostAttachment>();
    public ICollection<PlatformCommunityPostComment> Comments { get; set; } = new List<PlatformCommunityPostComment>();
    public ICollection<PlatformCommunityPostRecommendation> Recommendations { get; set; } = new List<PlatformCommunityPostRecommendation>();
    public ICollection<PlatformCommunityPostTranslation> Translations { get; set; } = new List<PlatformCommunityPostTranslation>();
    public PlatformCommunityPostAudio? Audio { get; set; }
    public PlatformCommunityPostKeywordScan? KeywordNotificationScan { get; set; }
}

public static class PlatformCommunityPostPublicationStatusCodes
{
    public const string Scheduled = "scheduled";
    public const string Publishing = "publishing";
    public const string Published = "published";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
}
