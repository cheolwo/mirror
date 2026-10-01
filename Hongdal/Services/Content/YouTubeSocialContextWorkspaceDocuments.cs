using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using MongoDB.Bson.Serialization.Attributes;

namespace Hongdal.Services.Content;

internal sealed class YouTubeSocialContextWorkspaceDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public long Revision { get; set; }
    public string Status { get; set; } = YouTubeSocialContextWorkspaceStatusCodes.ResearchReady;
    public YouTubeSocialContextVideoDocument Video { get; set; } = new();
    public List<string> SearchTerms { get; set; } = [];
    public List<string> AdjacentTopics { get; set; } = [];
    public List<SocialMediaResearchTargetDocument> SourceTargets { get; set; } = [];
    public int TakePerSource { get; set; } = 10;
    public List<YouTubeSocialContextSourceGroupDocument> SocialContextSources { get; set; } = [];
    public List<YouTubeSocialContextSourceFailureDocument> Failures { get; set; } = [];
    public YouTubeSocialContextWorkspaceDraftDocument GeneratedDraft { get; set; } = new();
    public YouTubeSocialContextWorkspaceDraftDocument Draft { get; set; } = new();
    public YouTubeImportJourneyDraftDocument ImportJourney { get; set; } = new();
    public long? PublishedPostId { get; set; }
    public List<YouTubeSocialContextPublicationLinkDocument> PublicationLinks { get; set; } = [];
    public DateTime LastResearchedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string UpdatedByDisplayName { get; set; } = string.Empty;
}

internal sealed class YouTubeSocialContextVideoDocument
{
    public string VideoId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public string CountryCode { get; set; } = "ZZ";
    public string LanguageCode { get; set; } = "und";
}

internal sealed class YouTubeSocialContextSourceGroupDocument
{
    public SocialMediaResearchSourceDocument Source { get; set; } = new();
    public List<CommunityInformationCandidateDocument> Items { get; set; } = [];
}

internal sealed class SocialMediaResearchTargetDocument
{
    public string SourceKey { get; set; } = string.Empty;
    public List<string> StartUrls { get; set; } = [];
}

internal sealed class SocialMediaResearchSourceDocument
{
    public string SourceKey { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? DocumentationUrl { get; set; }
    public bool Enabled { get; set; }
    public bool SupportsKeywordSearch { get; set; }
    public bool RequiresStartUrl { get; set; }
}

internal sealed class CommunityInformationCandidateDocument
{
    public string CandidateKey { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? ReferenceDate { get; set; }
    public DateTime CollectedAtUtc { get; set; }
    public string CountryCode { get; set; } = "ZZ";
    public string LanguageCode { get; set; } = "und";
    public string? CurrencyCode { get; set; }
    public string? Unit { get; set; }
    public string ReviewState { get; set; } = string.Empty;
    public List<string> TopicTags { get; set; } = [];
    public string SourceNotice { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
}

internal sealed class YouTubeSocialContextSourceFailureDocument
{
    public string SourceKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

internal sealed class YouTubeSocialContextWorkspaceDraftDocument
{
    public string Nickname { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string WorkflowTag { get; set; } = string.Empty;
    public string RoleTag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string SharedLinkUrl { get; set; } = string.Empty;
    public YouTubeSocialContextCollectiveActionDocument CollectiveAction { get; set; } = new();
    public bool IsManuallyEdited { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class YouTubeSocialContextCollectiveActionDocument
{
    public string WorkflowTag { get; set; } = string.Empty;
    public string PrimaryIntentTypeCode { get; set; } = string.Empty;
    public List<string> IntentTypeCodes { get; set; } = [];
    public string Prompt { get; set; } = string.Empty;
    public string NonBindingNotice { get; set; } = string.Empty;
    public string ParticipationEndpointTemplate { get; set; } = string.Empty;
}

internal sealed class YouTubeImportJourneyDraftDocument
{
    public string LedgerTemplateKey { get; set; } = string.Empty;
    public List<YouTubeImportJourneyNodeDocument> Nodes { get; set; } = [];
    public List<YouTubeImportJourneyEdgeDocument> Edges { get; set; } = [];
    public List<YouTubeImportOrganizationCandidateDocument> OrganizationCandidates { get; set; } = [];
    public string OutreachReadinessCode { get; set; } = YouTubeImportOutreachReadinessCodes.Collecting;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UnixEpoch;
}

internal sealed class YouTubeImportJourneyNodeDocument
{
    public string NodeKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
}

internal sealed class YouTubeImportJourneyEdgeDocument
{
    public string FromNodeKey { get; set; } = string.Empty;
    public string ToNodeKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

internal sealed class YouTubeImportOrganizationCandidateDocument
{
    public string CandidateKey { get; set; } = string.Empty;
    public string DiagramNodeKey { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "ZZ";
    public string WebsiteUrl { get; set; } = string.Empty;
    public string PublicBusinessEmail { get; set; } = string.Empty;
    public string ContactSourceUrl { get; set; } = string.Empty;
    public bool ContactSourceReviewed { get; set; }
    public string SourceKindCode { get; set; } = DiagramOrganizationSourceKindCodes.ManualResearch;
    public string SourceReferenceKey { get; set; } = string.Empty;
    public string DirectoryStatusCode { get; set; } = string.Empty;
    public string PlatformRelationshipStatusCode { get; set; } = string.Empty;
    public string CompanySourceVerificationStatusCode { get; set; } =
        DiagramOrganizationVerificationStatusCodes.VerificationRequired;
    public string RegulatoryVerificationStatusCode { get; set; } = string.Empty;
    public bool IsPlatformPartner { get; set; }
    public bool CanBeSelectedForOperations { get; set; }
    public List<string> CapabilityCodes { get; set; } = [];
}

internal sealed class YouTubeSocialContextPublicationLinkDocument
{
    public long PostId { get; set; }
    public DateTime LinkedAtUtc { get; set; }
    public string LinkedByDisplayName { get; set; } = string.Empty;
}
