using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Services.Content;

public interface ICommunityAuthoringImagePromptPlanner
{
    CommunityAuthoringImagePromptPlanResponse Plan(CommunityAuthoringImagePromptPlanRequest request);
}

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Application,
    "글 제목과 본문을 비용 없는 문맥별 이미지 프롬프트 계획으로 변환",
    ContractType = typeof(ICommunityAuthoringImagePromptPlanner),
    FlowOrder = 40,
    Boundary = "순수 계획 단계이며 DB 저장, 네트워크 호출, 이미지 생성 비용이 없습니다.")]
public sealed class CommunityAuthoringImagePromptPlanner : ICommunityAuthoringImagePromptPlanner
{
    private const string PromptVersion = "community-context-editorial-v1";

    public CommunityAuthoringImagePromptPlanResponse Plan(CommunityAuthoringImagePromptPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = NormalizeTitle(request.Title);
        var body = NormalizeBody(request.Body);
        var maxImages = NormalizeMaxImages(request.MaxImages);
        var aspectRatio = NormalizeAspectRatio(request.AspectRatio);

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("제목이나 본문을 입력한 뒤 이미지 문맥을 나눠 주세요.", nameof(request));
        }

        var articleTitle = string.IsNullOrWhiteSpace(title) ? "함께 준비하는 다음 행동" : title;
        var contextPlan = CommunityAuthoringImageContextSegmenter.Create(articleTitle, body, maxImages);
        var segments = contextPlan.Groups
            .Select((group, index) => CommunityAuthoringImagePromptFactory.Create(
                articleTitle,
                group,
                index + 1,
                contextPlan.Groups.Count,
                aspectRatio))
            .ToArray();

        return new CommunityAuthoringImagePromptPlanResponse(
            articleTitle,
            contextPlan.SourceSectionCount,
            segments,
            PromptVersion,
            "문맥 나누기는 비용이 들지 않습니다. 선택한 문맥마다 별도의 Kie.ai 이미지 생성 작업이 한 건씩 등록되므로 프롬프트를 검토한 뒤 생성하세요.");
    }

    private static string NormalizeTitle(string? title)
    {
        var normalized = title?.Trim() ?? string.Empty;
        if (normalized.Length > CommunityAuthoringImageLimits.MaximumArticleTitleLength)
        {
            throw new ArgumentException(
                $"게시글 제목은 {CommunityAuthoringImageLimits.MaximumArticleTitleLength:N0}자 이하여야 합니다.",
                nameof(title));
        }

        return normalized;
    }

    private static string NormalizeBody(string? body)
    {
        var normalized = (body ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        if (normalized.Length > CommunityAuthoringImageLimits.MaximumArticleBodyLength)
        {
            throw new ArgumentException(
                $"게시글 본문은 {CommunityAuthoringImageLimits.MaximumArticleBodyLength:N0}자 이하여야 합니다.",
                nameof(body));
        }

        return normalized;
    }

    private static int NormalizeMaxImages(int maxImages)
    {
        if (maxImages is < 1 or > CommunityAuthoringImageLimits.MaximumPlannedImages)
        {
            throw new ArgumentException(
                $"문맥 이미지는 1개 이상 {CommunityAuthoringImageLimits.MaximumPlannedImages}개 이하로 계획할 수 있습니다.",
                nameof(maxImages));
        }

        return maxImages;
    }

    private static string NormalizeAspectRatio(string? aspectRatio)
    {
        var normalized = string.IsNullOrWhiteSpace(aspectRatio)
            ? CommunityAuthoringImageAspectRatios.Landscape
            : aspectRatio.Trim().ToLowerInvariant();
        if (!CommunityAuthoringImageAspectRatios.All.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException("지원하지 않는 이미지 비율입니다.", nameof(aspectRatio));
        }

        return normalized;
    }
}
