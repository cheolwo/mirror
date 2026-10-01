using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

public sealed class CommunityInformationAuthoringAiEvidenceTool(
    ICommunityInformationCollectionService informationCollectionService)
    : ICommunityAuthoringAiEvidenceTool
{
    public string ToolKey => CommunityAuthoringAiToolKeys.InformationCollection;

    public async Task<CommunityAuthoringAiEvidenceToolResult> ExecuteAsync(
        CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken)
    {
        var response = await informationCollectionService.ReadAsync(
            new CommunityInformationCollectionQuery
            {
                SourceKey = NormalizeOptional(request.SourceKey),
                CountryCode = NormalizeOptional(request.CountryCode),
                SearchText = NormalizeOptional(request.SearchText) ?? NormalizeOptional(request.Topic),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Take = Math.Clamp(request.MaxEvidenceItems, 1, 20)
            },
            cancellationToken);
        var evidence = response.Items
            .Take(Math.Clamp(request.MaxEvidenceItems, 1, 20))
            .Select(item => CommunityAuthoringAiEvidenceMapper.FromCandidate(ToolKey, item))
            .ToArray();
        var failureSuffix = response.Failures.Count == 0
            ? string.Empty
            : $" 일부 원천 {response.Failures.Count:N0}곳은 조회하지 못했습니다.";
        return new CommunityAuthoringAiEvidenceToolResult(
            new CommunityAuthoringAiToolExecutionDto(
                ToolKey,
                "수집 자료",
                true,
                evidence.Length,
                $"조건에 맞는 자료 {evidence.Length:N0}건을 확인했습니다.{failureSuffix}"),
            evidence);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
