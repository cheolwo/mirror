using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using 홍달.Services.HIOPSAI;

namespace Hongdal.Services.Content;

public sealed class CommunityAuthoringAiDraftService : ICommunityAuthoringAiDraftService
{
    private readonly IReadOnlyDictionary<string, ICommunityAuthoringAiEvidenceTool> _tools;
    private readonly IReadOnlySet<string> _registeredToolKeys;
    private readonly IHIOPSAIClient _aiClient;
    private readonly ILogger<CommunityAuthoringAiDraftService> _logger;

    public CommunityAuthoringAiDraftService(
        IEnumerable<ICommunityAuthoringAiEvidenceTool> tools,
        IHIOPSAIClient aiClient,
        ILogger<CommunityAuthoringAiDraftService> logger)
    {
        var toolList = tools.ToArray();
        var duplicate = toolList
            .GroupBy(tool => tool.ToolKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"LLM 글쓰기 도구 키가 중복되었습니다: {duplicate.Key}");
        }

        _tools = toolList.ToDictionary(tool => tool.ToolKey, StringComparer.OrdinalIgnoreCase);
        _registeredToolKeys = _tools.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<CommunityAuthoringAiDraftResponse> GenerateAsync(
        CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = CommunityAuthoringAiDraftPolicy.NormalizeRequest(
            request,
            _registeredToolKeys);
        var toolResults = await ExecuteToolsAsync(normalized, cancellationToken);
        var executions = toolResults.Select(result => result.Execution).ToArray();
        var evidence = toolResults
            .SelectMany(result => result.Evidence)
            .Where(item => IsHttpUrl(item.OriginalUrl))
            .GroupBy(item => $"{item.SourceKey}|{item.OriginalUrl}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(normalized.MaxEvidenceItems)
            .ToArray();
        if (evidence.Length == 0)
        {
            return CreateResponse(
                false,
                CommunityAuthoringAiDraftStatusCodes.NoEvidence,
                "글의 근거로 사용할 공개 자료가 없습니다. 검색 조건이나 자료 원천을 조정해 주세요.",
                null,
                evidence,
                executions);
        }

        var completion = await _aiClient.CompleteAsync(
            new HIOPSAICompletionRequest(
                Purpose: "community-evidence-based-authoring-draft",
                Messages:
                [
                    new HIOPSAIMessage(
                        "developer",
                        CommunityAuthoringAiDraftPolicy.DeveloperInstruction),
                    new HIOPSAIMessage(
                        "user",
                        CommunityAuthoringAiDraftPolicy.BuildUserPrompt(normalized, evidence))
                ],
                MaxOutputTokens: 700,
                CorrelationId: $"community-authoring:{Guid.NewGuid():N}",
                OutputJsonSchema: new HIOPSAIJsonSchema(
                    "community_authoring_draft",
                    CommunityAuthoringAiDraftPolicy.OutputSchema)),
            cancellationToken);
        if (!completion.Success)
        {
            return CreateResponse(
                false,
                CommunityAuthoringAiDraftStatusCodes.LlmBlocked,
                completion.BlockedReason ?? "LLM 초안 생성이 실행되지 않았습니다.",
                null,
                evidence,
                executions,
                completion);
        }

        try
        {
            var draft = CommunityAuthoringAiDraftPolicy.CreateDraft(
                completion.Text,
                normalized.ContextSections,
                evidence);
            return CreateResponse(
                true,
                CommunityAuthoringAiDraftStatusCodes.ReadyForReview,
                "근거 기반 초안을 만들었습니다. 출처, 수치, 표현을 확인한 뒤 현재 글에 적용해 주세요.",
                draft,
                evidence,
                executions,
                completion);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return CreateResponse(
                false,
                CommunityAuthoringAiDraftStatusCodes.InvalidModelOutput,
                $"LLM 결과를 안전한 글 초안으로 해석하지 못했습니다: {exception.Message}",
                null,
                evidence,
                executions,
                completion);
        }
    }

    private async Task<IReadOnlyList<CommunityAuthoringAiEvidenceToolResult>> ExecuteToolsAsync(
        CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<CommunityAuthoringAiEvidenceToolResult>(request.ToolKeys.Count);
        foreach (var toolKey in request.ToolKeys)
        {
            try
            {
                results.Add(await _tools[toolKey].ExecuteAsync(request, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "LLM 글쓰기 자료 도구 실행에 실패했습니다. ToolKey={ToolKey}",
                    toolKey);
                results.Add(new CommunityAuthoringAiEvidenceToolResult(
                    new CommunityAuthoringAiToolExecutionDto(
                        toolKey,
                        ResolveToolDisplayName(toolKey),
                        false,
                        0,
                        "자료 조회 도구를 실행하지 못했습니다."),
                    []));
            }
        }

        return results;
    }

    private static CommunityAuthoringAiDraftResponse CreateResponse(
        bool success,
        string statusCode,
        string message,
        CommunityAuthoringAiPostDraftDto? draft,
        IReadOnlyList<CommunityAuthoringAiEvidenceDto> evidence,
        IReadOnlyList<CommunityAuthoringAiToolExecutionDto> executions,
        HIOPSAICompletionResult? completion = null)
        => new(
            success,
            statusCode,
            message,
            draft,
            evidence,
            executions,
            RequiresHumanReview: true,
            CanPublish: false,
            completion?.Model,
            completion?.ActualCostUsd ?? 0m,
            completion?.MonthlyUsedUsd ?? 0m,
            completion?.MonthlyBudgetUsd ?? 0m);

    private static string ResolveToolDisplayName(string toolKey)
        => toolKey switch
        {
            CommunityAuthoringAiToolKeys.InformationCollection => "수집 자료",
            CommunityAuthoringAiToolKeys.YouTubeSocialContext => "YouTube·SNS 조사",
            _ => toolKey
        };

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
