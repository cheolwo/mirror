using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.AI;

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Infrastructure,
    "현재 음식 후보의 임시 토큰과 최소 수치만 루프백 Ollama에 보내 제한된 후보 하나를 선택한다.",
    Effects = SsalddelCodeEffect.NetworkCall, FlowOrder = 47,
    StepKey = "infrastructure.food-dispatch-ollama-selection",
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    Boundary = "기준 후보와 같은 픽업 단계·수요 우선순위 관문을 유지한다. 주소·실제 기사/주문 ID·자유 문장을 전송하거나 저장하지 않으며 업무 원장·추천·수락·알림을 변경하지 않는다.")]
public sealed class OllamaFoodDispatchCandidateAI(HttpClient httpClient, IOptions<FoodDispatchOllamaOptions> options)
    : IFoodDispatchCandidateAI
{
    private const string Instructions = "Select one food-delivery candidate from the supplied opaque tokens. "
        + "The server has already enforced eligibility, current demand/capacity, pickup feasibility and receiving consent. "
        + "Never invent or restore candidates. Prefer a longer waiter only under comparable feasible pickup conditions; "
        + "aging score is not measured waiting minutes. Recommended score may include an optional return-area preference. "
        + "Demand/capacity takes precedence over time-of-day. No future-destination routing is implied. "
        + "C1 is the server baseline. Return only the JSON object {\"candidateToken\":\"C1\"}, choosing a supplied token. "
        + "No commentary, tools, invented facts or additional keys.";

    public bool Enabled => options.Value.Enabled;

    public async Task<FoodDispatchAISelection> SelectAsync(
        음식배달후보검토결과 trace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var baseline = trace.선정후보?.DriverId;
        FoodDispatchAISelection Fallback(string status) => new(baseline, status);
        if (!Enabled) return Fallback("Disabled");
        var settings = options.Value;
        if (!settings.Validate()) return Fallback("InvalidConfiguration");
        var candidates = FoodDispatchCandidateConstraints.Eligible(trace, settings.MaxCandidates);
        if (candidates.Count == 0) return Fallback("NoCandidates");
        if (candidates.Count == 1) return Fallback("SingleCandidate");

        var mapped = candidates.Select((item, index) => new { Token = $"C{index + 1}", Item = item }).ToArray();
        var tokens = mapped.Select(x => x.Token).ToArray();
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "candidateToken" },
            properties = new { candidateToken = new { type = "string", @enum = tokens } }
        };
        var context = new
        {
            baselineToken = "C1",
            currentDemand = trace.수요자료최신,
            demandFallback = trace.수요예외사용,
            pickupFallback = trace.픽업적시성예외사용,
            candidates = mapped.Select(x => new
            {
                candidateToken = x.Token,
                spatialStage = x.Item.공간단계,
                distanceKm = x.Item.거리Km,
                pickupStage = x.Item.픽업단계,
                arrivalDifferenceMinutes = x.Item.도착차이분,
                agingScore = x.Item.대기점수,
                recommendedScore = x.Item.추천점수,
                demandPriority = x.Item.수요우선순위
            })
        };
        var body = new
        {
            model = settings.Model,
            stream = false,
            think = false,
            keep_alive = 0,
            format = schema,
            options = new { temperature = 0, seed = 48, num_ctx = 2048, num_predict = 64 },
            messages = new[]
            {
                new { role = "system", content = Instructions },
                new { role = "user", content = JsonSerializer.Serialize(context) }
            }
        };

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), "api/chat"))
            {
                Content = JsonContent.Create(body)
            };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var chat = await OllamaChatResponseReader.ReadAsync(response, settings.Model, deadline.Token);
            if (chat.ErrorCode is { } errorCode) return Fallback(errorCode);
            using var advice = JsonDocument.Parse(chat.Content!, new JsonDocumentOptions { MaxDepth = 4 });
            OllamaChatResponseReader.RejectDuplicateProperties(advice.RootElement);
            if (advice.RootElement.ValueKind != JsonValueKind.Object) return Fallback("InvalidResponse");
            var properties = advice.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "candidateToken"
                || properties[0].Value.ValueKind != JsonValueKind.String)
                return Fallback("InvalidResponse");
            var selectedToken = properties[0].Value.GetString();
            var selected = mapped.FirstOrDefault(x => x.Token == selectedToken);
            return selected is null ? Fallback("InvalidResponse") : new(selected.Item.기사Id, "Applied");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Fallback("Timeout");
        }
        catch (HttpRequestException)
        {
            return Fallback("NetworkFailure");
        }
        catch (IOException)
        {
            return Fallback("NetworkFailure");
        }
        catch (JsonException)
        {
            return Fallback("InvalidResponse");
        }
    }

}
