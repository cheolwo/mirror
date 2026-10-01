using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using Microsoft.Extensions.Logging.Abstractions;
using 홍달.Services.HIOPSAI;

namespace Hongdal.Tests.Services.Content;

public sealed class CommunityAuthoringAiDraftServiceTests
{
    [Fact]
    public async Task 허용된자료도구의근거로만_검토용초안을만든다()
    {
        var tool = new FakeEvidenceTool(Evidence());
        var ai = new FakeAiClient
        {
            Result = SuccessfulCompletion(
                """
                {
                  "title": "[서원] 지역 먹거리 공동구매를 함께 살펴봅니다",
                  "body": "확인된 가격 자료와 아직 합의되지 않은 조건을 나누어 살펴봅니다.",
                  "workflowTag": "공동구매 사전 검토",
                  "roleTag": "운영자 서원 기록",
                  "sourceUrls": ["https://example.com/price"],
                  "suggestedDiagramSteps": ["수요 확인", "공급 조건 확인"],
                  "openQuestions": ["참여자가 직접 확인할 최소 수량은 얼마인가요?"]
                }
                """)
        };
        var sut = CreateService(tool, ai);

        var result = await sut.GenerateAsync(new CommunityAuthoringAiDraftRequest
        {
            Objective = "공개 가격 자료를 바탕으로 공동구매 서원 글을 작성한다.",
            Topic = "지역 먹거리",
            ToolKeys = [CommunityAuthoringAiToolKeys.InformationCollection]
        });

        Assert.True(result.Success);
        Assert.True(result.RequiresHumanReview);
        Assert.False(result.CanPublish);
        Assert.NotNull(result.Draft);
        Assert.Contains("https://example.com/price", result.Draft.Body);
        Assert.Equal("https://example.com/price", result.Draft.SharedLinkUrl);
        Assert.Single(result.ToolExecutions);
        Assert.NotNull(ai.LastRequest?.OutputJsonSchema);
        var userPrompt = ai.LastRequest!.Messages.Single(message => message.Role == "user").Content;
        using var promptDocument = System.Text.Json.JsonDocument.Parse(userPrompt[userPrompt.IndexOf('{')..]);
        Assert.Contains("공동구매 서원", promptDocument.RootElement.GetProperty("objective").GetString());
        Assert.Contains("게시, 예약", ai.LastRequest.Messages.Single(message => message.Role == "developer").Content);
    }

    [Fact]
    public async Task HIOPSAI가비활성화되면_근거는남기고초안을만들지않는다()
    {
        var tool = new FakeEvidenceTool(Evidence());
        var ai = new FakeAiClient
        {
            Result = HIOPSAICompletionResult.Blocked(
                "HIOPSAI:Enabled 설정이 false입니다.",
                "fake",
                0m,
                0m,
                20m)
        };
        var sut = CreateService(tool, ai);

        var result = await sut.GenerateAsync(new CommunityAuthoringAiDraftRequest
        {
            Objective = "근거를 확인한 글을 작성한다."
        });

        Assert.False(result.Success);
        Assert.Equal(CommunityAuthoringAiDraftStatusCodes.LlmBlocked, result.StatusCode);
        Assert.Null(result.Draft);
        Assert.Single(result.Evidence);
        Assert.False(result.CanPublish);
    }

    [Fact]
    public async Task 조회하지않은URL을모델이출처로만들면_초안을거부한다()
    {
        var tool = new FakeEvidenceTool(Evidence());
        var ai = new FakeAiClient
        {
            Result = SuccessfulCompletion(
                """
                {
                  "title": "출처 확인",
                  "body": "확인되지 않은 주소를 출처로 쓰지 않습니다.",
                  "workflowTag": "자료 검토",
                  "roleTag": "운영자 정보 공유",
                  "sourceUrls": ["https://unknown.example/fabricated"],
                  "suggestedDiagramSteps": [],
                  "openQuestions": []
                }
                """)
        };
        var sut = CreateService(tool, ai);

        var result = await sut.GenerateAsync(new CommunityAuthoringAiDraftRequest
        {
            Objective = "근거를 확인한 글을 작성한다."
        });

        Assert.False(result.Success);
        Assert.Equal(CommunityAuthoringAiDraftStatusCodes.InvalidModelOutput, result.StatusCode);
        Assert.Null(result.Draft);
    }

    [Fact]
    public async Task 등록되지않은도구키는_AI호출전에거부한다()
    {
        var ai = new FakeAiClient();
        var sut = CreateService(new FakeEvidenceTool(Evidence()), ai);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GenerateAsync(
            new CommunityAuthoringAiDraftRequest
            {
                Objective = "근거를 확인한 글을 작성한다.",
                ToolKeys = ["arbitrary-http"]
            }));

        Assert.Null(ai.LastRequest);
    }

    private static CommunityAuthoringAiDraftService CreateService(
        ICommunityAuthoringAiEvidenceTool tool,
        IHIOPSAIClient ai)
        => new([tool], ai, NullLogger<CommunityAuthoringAiDraftService>.Instance);

    private static CommunityAuthoringAiEvidenceDto Evidence()
        => new(
            "price-1",
            CommunityAuthoringAiToolKeys.InformationCollection,
            CommunityInformationSourceKeys.KamisPriceObservations,
            "공공 가격 정보",
            "사과 소매가격",
            "2026년 7월 공개 관측값입니다.",
            "https://example.com/price",
            new DateOnly(2026, 7, 18),
            "가격",
            25_000m,
            "KRW",
            "10개",
            "공개 API 관측값입니다.",
            "전체 시장 평균이나 판매 권고가 아닙니다.");

    private static HIOPSAICompletionResult SuccessfulCompletion(string text)
        => new(
            true,
            text,
            null,
            "fake-model",
            0.01m,
            0.009m,
            0.1m,
            20m,
            100,
            80);

    private sealed class FakeEvidenceTool(params CommunityAuthoringAiEvidenceDto[] evidence)
        : ICommunityAuthoringAiEvidenceTool
    {
        public string ToolKey => CommunityAuthoringAiToolKeys.InformationCollection;

        public Task<CommunityAuthoringAiEvidenceToolResult> ExecuteAsync(
            CommunityAuthoringAiDraftRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(new CommunityAuthoringAiEvidenceToolResult(
                new CommunityAuthoringAiToolExecutionDto(
                    ToolKey,
                    "수집 자료",
                    true,
                    evidence.Length,
                    $"자료 {evidence.Length:N0}건"),
                evidence));
    }

    private sealed class FakeAiClient : IHIOPSAIClient
    {
        public HIOPSAICompletionRequest? LastRequest { get; private set; }

        public HIOPSAICompletionResult Result { get; init; } =
            HIOPSAICompletionResult.Blocked("not configured", "fake", 0m, 0m, 20m);

        public Task<HIOPSAICompletionResult> CompleteAsync(
            HIOPSAICompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
