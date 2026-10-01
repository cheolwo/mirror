namespace Hongdal.Contracts.Common.Content;

public static class CommunityInformationSourceKeys
{
    public const string YouTubeChannelVideos = "youtube-channel-videos";
    public const string KamisPriceObservations = "kamis-price-observations";
    public const string UsdaNassPriceObservations = "usda-nass-price-observations";
    public const string AbsFoodPriceIndex = "abs-cpi-food-price-index";
    public const string FishCooperativeGeneralStatistics = "fish-cooperative-general-statistics";
    public const string RedditPublicPosts = "reddit-public-posts";
    public const string RedditRssPublicPosts = "reddit-rss-public-posts";
    public const string XPublicPosts = "x-public-posts";
    public const string InstagramPublicPosts = "instagram-public-posts";
    public const string FacebookPublicPosts = "facebook-public-posts";
}

public static class CommunityInformationSourceTypes
{
    public const string Video = "Video";
    public const string PublicData = "PublicData";
    public const string SocialMedia = "SocialMedia";
}

public static class CommunityInformationCollectionModes
{
    public const string ScheduledArchive = "ScheduledArchive";
    public const string OnDemandExternalResearch = "OnDemandExternalResearch";
    public const string OnDemandPublicDataQuery = "OnDemandPublicDataQuery";
}

public static class CommunityInformationReviewStates
{
    public const string Baseline = "Baseline";
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Excluded = "Excluded";
    public const string OfficialObservation = "OfficialObservation";
}

public sealed record CommunityInformationSourceDto(
    string SourceKey,
    string SourceType,
    string Provider,
    string DisplayName,
    string CollectionMode,
    string UpdateCycle,
    string PublicationPolicy,
    string DocumentationUrl,
    bool RequiresEditorialReview);

public sealed record CommunityInformationCandidateDto(
    string CandidateKey,
    string SourceKey,
    string SourceType,
    string Provider,
    string Title,
    string Summary,
    string OriginalUrl,
    string? ThumbnailUrl,
    DateTime? PublishedAtUtc,
    DateOnly? ReferenceDate,
    DateTime CollectedAtUtc,
    string CountryCode,
    string LanguageCode,
    string? CurrencyCode,
    string? Unit,
    string ReviewState,
    IReadOnlyList<string> TopicTags,
    string SourceNotice,
    string Limitations,
    decimal? NumericValue = null,
    string? MetricLabel = null,
    string? MetricSeriesKey = null,
    DateOnly? ReferencePeriodEndDate = null,
    string? MetricSeriesLabel = null);

public sealed class CommunityInformationCollectionQuery
{
    public string? SourceKey { get; init; }

    public string? CountryCode { get; init; }

    public string? ReviewState { get; init; }

    public string? SearchText { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public int Take { get; init; } = 50;
}

public sealed record CommunityInformationSourceFailureDto(
    string SourceKey,
    string Message);

public sealed record CommunityInformationCollectionResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<CommunityInformationSourceDto> Sources,
    IReadOnlyList<CommunityInformationCandidateDto> Items,
    IReadOnlyList<CommunityInformationSourceFailureDto> Failures);
