using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

public sealed class YouTubeSocialContextAuthoringAiEvidenceTool(
    IYouTubeSocialContextResearchService researchService)
    : ICommunityAuthoringAiEvidenceTool
{
    public string ToolKey => CommunityAuthoringAiToolKeys.YouTubeSocialContext;

    public async Task<CommunityAuthoringAiEvidenceToolResult> ExecuteAsync(
        CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (request.YouTubeSocialContext is null)
        {
            return new CommunityAuthoringAiEvidenceToolResult(
                new CommunityAuthoringAiToolExecutionDto(
                    ToolKey,
                    "YouTube·SNS 조사",
                    false,
                    0,
                    "YouTube 영상과 SNS 조사 조건이 없어 실행하지 않았습니다."),
                []);
        }

        var response = await researchService.ResearchAsync(
            request.YouTubeSocialContext,
            cancellationToken);
        var videoEvidence = new CommunityAuthoringAiEvidenceDto(
            $"youtube:{response.Video.VideoId}",
            ToolKey,
            CommunityInformationSourceKeys.YouTubeChannelVideos,
            response.Video.ChannelName,
            CommunityAuthoringAiEvidenceMapper.Truncate(response.Video.Title, 240),
            CommunityAuthoringAiEvidenceMapper.Truncate(response.Video.Summary, 500),
            response.Video.OriginalUrl,
            DateOnly.FromDateTime(response.Video.PublishedAtUtc),
            null,
            null,
            null,
            null,
            "YouTube 공개 영상 메타데이터와 운영자가 검토 대상으로 지정한 영상입니다.",
            "영상의 주장과 실제 내용은 게시 전에 원문을 직접 확인해야 합니다.");
        var itemEvidence = response.Items
            .Take(Math.Clamp(request.MaxEvidenceItems - 1, 0, 19))
            .Select(item => CommunityAuthoringAiEvidenceMapper.FromCandidate(ToolKey, item));
        var evidence = itemEvidence.Prepend(videoEvidence).ToArray();
        var failureSuffix = response.Failures.Count == 0
            ? string.Empty
            : $" 일부 SNS 원천 {response.Failures.Count:N0}곳은 조회하지 못했습니다.";
        return new CommunityAuthoringAiEvidenceToolResult(
            new CommunityAuthoringAiToolExecutionDto(
                ToolKey,
                "YouTube·SNS 조사",
                true,
                evidence.Length,
                $"영상과 공개 SNS 자료 {evidence.Length:N0}건을 확인했습니다.{failureSuffix}"),
            evidence);
    }
}
