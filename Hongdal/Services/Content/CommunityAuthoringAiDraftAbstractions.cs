using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

public interface ICommunityAuthoringAiEvidenceTool
{
    string ToolKey { get; }

    Task<CommunityAuthoringAiEvidenceToolResult> ExecuteAsync(
        CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken);
}

public sealed record CommunityAuthoringAiEvidenceToolResult(
    CommunityAuthoringAiToolExecutionDto Execution,
    IReadOnlyList<CommunityAuthoringAiEvidenceDto> Evidence);

public interface ICommunityAuthoringAiDraftService
{
    Task<CommunityAuthoringAiDraftResponse> GenerateAsync(
        CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken = default);
}
