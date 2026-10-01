using Hongdal.Contracts.Common.Community;

namespace Hongdal.Contracts.Common.Content;

public static class YouTubeSocialContextWorkspaceStatusCodes
{
    public const string ResearchReady = "research-ready";
    public const string DraftEdited = "draft-edited";
    public const string Published = "published";
    public const string Archived = "archived";

    public static bool IsSupported(string? value)
        => value is ResearchReady or DraftEdited or Published or Archived;
}

public static class YouTubeImportOutreachReadinessCodes
{
    public const string Collecting = "collecting";
    public const string ContactReviewRequired = "contact-review-required";
    public const string ReadyForManualDraft = "ready-for-manual-draft";

    public static bool IsSupported(string? value)
        => value is Collecting or ContactReviewRequired or ReadyForManualDraft;
}

public sealed record YouTubeImportJourneyNodeDto(
    string NodeKey,
    string Title,
    string Description,
    string GroupLabel,
    string Kind);

public sealed record YouTubeImportJourneyEdgeDto(
    string FromNodeKey,
    string ToNodeKey,
    string Label);

public sealed record YouTubeImportOrganizationCandidateDto(
    string CandidateKey,
    string DiagramNodeKey,
    string OrganizationName,
    string RoleLabel,
    string CountryCode,
    string WebsiteUrl,
    string PublicBusinessEmail,
    string ContactSourceUrl,
    bool ContactSourceReviewed)
{
    public string SourceKindCode { get; init; } = DiagramOrganizationSourceKindCodes.ManualResearch;

    public string SourceReferenceKey { get; init; } = string.Empty;

    public string DirectoryStatusCode { get; init; } = string.Empty;

    public string PlatformRelationshipStatusCode { get; init; } = string.Empty;

    public string CompanySourceVerificationStatusCode { get; init; } =
        DiagramOrganizationVerificationStatusCodes.VerificationRequired;

    public string RegulatoryVerificationStatusCode { get; init; } = string.Empty;

    public bool IsPlatformPartner { get; init; }

    public bool CanBeSelectedForOperations { get; init; }

    public IReadOnlyList<string> CapabilityCodes { get; init; } = [];

    public DiagramOrganizationReferenceDto ToDiagramReference()
        => new()
        {
            ReferenceId = CandidateKey,
            OrganizationKey = string.IsNullOrWhiteSpace(SourceReferenceKey)
                ? CandidateKey
                : SourceReferenceKey,
            DisplayName = OrganizationName,
            RoleLabel = RoleLabel,
            CountryCode = CountryCode,
            OfficialWebsiteUrl = WebsiteUrl,
            SourceKindCode = SourceKindCode,
            SourceReferenceUrl = ContactSourceUrl,
            DirectoryStatusCode = DirectoryStatusCode,
            PlatformRelationshipStatusCode = PlatformRelationshipStatusCode,
            CompanySourceVerificationStatusCode = CompanySourceVerificationStatusCode,
            RegulatoryVerificationStatusCode = RegulatoryVerificationStatusCode,
            IsPlatformPartner = IsPlatformPartner,
            CanBeSelectedForOperations = CanBeSelectedForOperations,
            CapabilityCodes = CapabilityCodes ?? []
        };
}

public sealed record YouTubeImportJourneyDraftDto(
    string LedgerTemplateKey,
    IReadOnlyList<YouTubeImportJourneyNodeDto> Nodes,
    IReadOnlyList<YouTubeImportJourneyEdgeDto> Edges,
    IReadOnlyList<YouTubeImportOrganizationCandidateDto> OrganizationCandidates,
    string OutreachReadinessCode,
    DateTime UpdatedAtUtc)
{
    public static YouTubeImportJourneyDraftDto Empty { get; } = new(
        string.Empty,
        [],
        [],
        [],
        YouTubeImportOutreachReadinessCodes.Collecting,
        DateTime.UnixEpoch);
}

public sealed class YouTubeImportJourneyDraftUpdateRequest
{
    public string LedgerTemplateKey { get; init; } = string.Empty;

    public IReadOnlyList<YouTubeImportJourneyNodeDto> Nodes { get; init; } = [];

    public IReadOnlyList<YouTubeImportJourneyEdgeDto> Edges { get; init; } = [];

    public IReadOnlyList<YouTubeImportOrganizationCandidateDto> OrganizationCandidates { get; init; } = [];
}

public sealed record YouTubeSocialContextSourceGroupDto(
    SocialMediaResearchSourceDto Source,
    IReadOnlyList<CommunityInformationCandidateDto> Items);

public sealed record YouTubeSocialContextWorkspaceDraftDto(
    string Nickname,
    string Category,
    string WorkflowTag,
    string RoleTag,
    string Title,
    string Body,
    string SharedLinkUrl,
    YouTubeSocialContextCollectiveActionDraftDto CollectiveAction,
    bool IsManuallyEdited,
    DateTime UpdatedAtUtc);

public sealed record YouTubeSocialContextPublicationLinkDto(
    long PostId,
    DateTime LinkedAtUtc,
    string LinkedByDisplayName);

public sealed record YouTubeSocialContextWorkspaceDto(
    string WorkspaceId,
    long Revision,
    string Status,
    YouTubeSocialContextVideoDto Video,
    IReadOnlyList<string> SearchTerms,
    IReadOnlyList<string> AdjacentTopics,
    IReadOnlyList<SocialMediaResearchTargetDto> SourceTargets,
    int TakePerSource,
    IReadOnlyList<YouTubeSocialContextSourceGroupDto> SocialContextSources,
    IReadOnlyList<YouTubeSocialContextSourceFailureDto> Failures,
    YouTubeSocialContextWorkspaceDraftDto Draft,
    long? PublishedPostId,
    IReadOnlyList<YouTubeSocialContextPublicationLinkDto> PublicationLinks,
    DateTime LastResearchedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string UpdatedByDisplayName)
{
    public YouTubeImportJourneyDraftDto ImportJourney { get; init; } =
        YouTubeImportJourneyDraftDto.Empty;
}

public sealed record YouTubeSocialContextWorkspaceSummaryDto(
    string WorkspaceId,
    long Revision,
    string Status,
    string VideoId,
    string VideoTitle,
    string ChannelName,
    int SocialItemCount,
    long? PublishedPostId,
    DateTime UpdatedAtUtc)
{
    public int ImportJourneyNodeCount { get; init; }

    public int OrganizationCandidateCount { get; init; }

    public string OutreachReadinessCode { get; init; } =
        YouTubeImportOutreachReadinessCodes.Collecting;
}

public sealed class YouTubeSocialContextWorkspaceDraftUpdateRequest
{
    public long ExpectedRevision { get; init; }

    public string Nickname { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string WorkflowTag { get; init; } = string.Empty;

    public string RoleTag { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public string SharedLinkUrl { get; init; } = string.Empty;

    public YouTubeImportJourneyDraftUpdateRequest? ImportJourney { get; init; }
}

public sealed class YouTubeSocialContextPublicationLinkRequest
{
    public long ExpectedRevision { get; init; }

    public long PostId { get; init; }
}
