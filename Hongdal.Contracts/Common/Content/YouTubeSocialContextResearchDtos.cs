namespace Hongdal.Contracts.Common.Content;

public sealed record SocialMediaResearchTargetDto(
    string SourceKey,
    IReadOnlyList<string> StartUrls);

public sealed class YouTubeSocialContextResearchRequest
{
    public string VideoId { get; init; } = string.Empty;

    public IReadOnlyList<string> SourceKeys { get; init; } = [];

    public IReadOnlyList<string> SearchTerms { get; init; } = [];

    public IReadOnlyList<string> AdjacentTopics { get; init; } = [];

    public IReadOnlyList<SocialMediaResearchTargetDto> SourceTargets { get; init; } = [];

    public int TakePerSource { get; init; } = 10;

    public string? CountryCode { get; init; }

    public string? LanguageCode { get; init; }

}

public sealed record SocialMediaResearchSourceDto(
    string SourceKey,
    string Provider,
    string DisplayName,
    string DocumentationUrl,
    bool Enabled,
    bool SupportsKeywordSearch,
    bool RequiresStartUrl);

public sealed record YouTubeSocialContextVideoDto(
    string VideoId,
    string ChannelName,
    string Title,
    string Summary,
    string OriginalUrl,
    string? ThumbnailUrl,
    DateTime PublishedAtUtc,
    string CountryCode,
    string LanguageCode);

public sealed record AmazonAssociateLinkDraftDto(
    string ProductLabel,
    string CanonicalProductUrl,
    string AffiliateUrl,
    string LinkDisclosure,
    string AssociateIdentification);

public sealed record YouTubeSocialContextSourceFailureDto(
    string SourceKey,
    string Message);

public sealed record YouTubeSocialContextCollectiveActionDraftDto(
    string WorkflowTag,
    string PrimaryIntentTypeCode,
    IReadOnlyList<string> IntentTypeCodes,
    string Prompt,
    string NonBindingNotice,
    string ParticipationEndpointTemplate);

public sealed record YouTubeSocialContextPostDraftDto(
    string Title,
    string Body,
    YouTubeSocialContextCollectiveActionDraftDto CollectiveAction);

public sealed record YouTubeSocialContextResearchResponse(
    DateTime GeneratedAtUtc,
    YouTubeSocialContextVideoDto Video,
    IReadOnlyList<string> SearchTerms,
    IReadOnlyList<string> AdjacentTopics,
    IReadOnlyList<SocialMediaResearchSourceDto> Sources,
    IReadOnlyList<CommunityInformationCandidateDto> Items,
    IReadOnlyList<YouTubeSocialContextSourceFailureDto> Failures,
    YouTubeSocialContextPostDraftDto Draft)
{
    public string WorkspaceId { get; init; } = string.Empty;

    public long WorkspaceRevision { get; init; }

    public string WorkspaceStatus { get; init; } = string.Empty;
}
