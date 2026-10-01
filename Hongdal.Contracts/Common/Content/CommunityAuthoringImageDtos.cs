using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Contracts.Common.Content;

public static class CommunityAuthoringImageTaskStatusCodes
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class CommunityAuthoringImageLimits
{
    public const int DefaultPlannedImages = 4;
    public const int MaximumPlannedImages = 5;
    public const int MinimumPromptLength = 10;
    public const int MaximumPromptLength = 4_000;
    public const int MaximumArticleTitleLength = 160;
    public const int MaximumArticleBodyLength = 4_000;
}

public static class CommunityAuthoringImageAspectRatios
{
    public const string Auto = "auto";
    public const string Square = "1:1";
    public const string Landscape = "3:2";
    public const string Portrait = "2:3";

    public static IReadOnlyList<string> All { get; } =
        [Auto, Square, Landscape, Portrait];
}

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Contract,
    "문맥별 이미지 생성 요청 계약",
    Boundary = "provider 이름, API key, provider 원본 응답을 노출하지 않습니다.")]
public sealed class CommunityAuthoringImageGenerateRequest
{
    public string Prompt { get; init; } = string.Empty;

    public string AspectRatio { get; init; } = CommunityAuthoringImageAspectRatios.Landscape;
}

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Contract,
    "글 본문을 이미지 문맥으로 나누는 계획 요청 계약",
    Boundary = "계획 요청 자체는 이미지 provider를 호출하지 않습니다.")]
public sealed class CommunityAuthoringImagePromptPlanRequest
{
    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public int MaxImages { get; init; } = CommunityAuthoringImageLimits.DefaultPlannedImages;

    public string AspectRatio { get; init; } = CommunityAuthoringImageAspectRatios.Landscape;
}

public sealed record CommunityAuthoringImagePromptSegmentDto(
    string SegmentKey,
    int Sequence,
    string Title,
    string Context,
    string Prompt,
    string AspectRatio,
    bool IsSelectedByDefault);

public sealed record CommunityAuthoringImagePromptPlanResponse(
    string ArticleTitle,
    int SourceSectionCount,
    IReadOnlyList<CommunityAuthoringImagePromptSegmentDto> Segments,
    string PromptVersion,
    string Guidance);

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Contract,
    "글쓰기 화면에 반환하는 provider 독립 이미지 작업 상태 계약",
    Boundary = "AI 생성 고지와 완료 여부를 포함하고 provider 비밀 정보는 포함하지 않습니다.")]
public sealed record CommunityAuthoringImageTaskResponse(
    string JobCode,
    string StatusCode,
    string Message,
    string Prompt,
    string AspectRatio,
    string Model,
    string? ImageUrl,
    bool IsTerminal,
    bool IsSuccess,
    int? Progress,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string Disclosure);

public sealed class CommunityAuthoringGeneratedImageAttachRequest
{
    public string Password { get; init; } = string.Empty;
}
