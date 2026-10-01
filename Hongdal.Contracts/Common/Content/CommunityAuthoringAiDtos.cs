namespace Hongdal.Contracts.Common.Content;

public static class CommunityAuthoringAiToolKeys
{
    public const string InformationCollection = "community-information";
    public const string YouTubeSocialContext = "youtube-social-context";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [InformationCollection, YouTubeSocialContext],
        StringComparer.OrdinalIgnoreCase);
}

public static class CommunityAuthoringAiDraftStatusCodes
{
    public const string ReadyForReview = "ReadyForReview";
    public const string NoEvidence = "NoEvidence";
    public const string LlmBlocked = "LlmBlocked";
    public const string InvalidModelOutput = "InvalidModelOutput";
}

public sealed record CommunityAuthoringAiContextSectionDto(
    string SectionKey,
    string Title,
    string Content);

public sealed class CommunityAuthoringAiDraftRequest
{
    public string Objective { get; init; } = string.Empty;

    public string? Topic { get; init; }

    public string? SourceKey { get; init; }

    public string? CountryCode { get; init; }

    public string? SearchText { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public string LanguageCode { get; init; } = "ko";

    public int MaxEvidenceItems { get; init; } = 12;

    public IReadOnlyList<string> ToolKeys { get; init; } =
        [CommunityAuthoringAiToolKeys.InformationCollection];

    public YouTubeSocialContextResearchRequest? YouTubeSocialContext { get; init; }

    public IReadOnlyList<CommunityAuthoringAiContextSectionDto> ContextSections { get; init; } = [];
}

public sealed record CommunityAuthoringAiEvidenceDto(
    string EvidenceKey,
    string ToolKey,
    string SourceKey,
    string Provider,
    string Title,
    string Summary,
    string OriginalUrl,
    DateOnly? ReferenceDate,
    string? MetricLabel,
    decimal? NumericValue,
    string? CurrencyCode,
    string? Unit,
    string SourceNotice,
    string Limitations);

public sealed record CommunityAuthoringAiToolExecutionDto(
    string ToolKey,
    string DisplayName,
    bool Succeeded,
    int EvidenceCount,
    string Message);

public sealed record CommunityAuthoringAiPostDraftDto(
    string Title,
    string Body,
    string Category,
    string WorkflowTag,
    string RoleTag,
    string? SharedLinkUrl,
    IReadOnlyList<string> SourceUrls,
    IReadOnlyList<string> SuggestedDiagramSteps,
    IReadOnlyList<string> OpenQuestions);

public sealed record CommunityAuthoringAiDraftResponse(
    bool Success,
    string StatusCode,
    string Message,
    CommunityAuthoringAiPostDraftDto? Draft,
    IReadOnlyList<CommunityAuthoringAiEvidenceDto> Evidence,
    IReadOnlyList<CommunityAuthoringAiToolExecutionDto> ToolExecutions,
    bool RequiresHumanReview,
    bool CanPublish,
    string? Model,
    decimal ActualCostUsd,
    decimal MonthlyUsedUsd,
    decimal MonthlyBudgetUsd);
