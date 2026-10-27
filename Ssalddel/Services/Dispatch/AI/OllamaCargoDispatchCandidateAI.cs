using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.AI;

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Infrastructure,
    "검증된 화물 후보의 익명 토큰과 수치만 기존 루프백 모델에 보내 제한된 선택을 받는다.",
    StepKey = "infrastructure.cargo-dispatch-ollama-selection", FlowOrder = 47,
    ExecutionStage = SsalddelCodeExecutionStage.Query, Effects = SsalddelCodeEffect.NetworkCall,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    Boundary = "차량·약속·대기 공정성 관문을 낮추지 않는다. 모델은 원장·추천·수락을 저장하지 않으며 주소·실제 ID·연락처를 받지 않는다.")]
public sealed class OllamaCargoDispatchCandidateAI(HttpClient httpClient, IOptions<CargoDispatchOllamaOptions> options,
    CargoDispatchAIInferenceGate gate) : ICargoDispatchCandidateAI
{
    private const string Instructions = "Choose one anonymous cargo dispatch candidate already verified by the server. "
        + "All supplied candidates satisfy vehicle, load, promised pickup and delivery, and participation constraints. "
        + "Never invent or restore a candidate. Prefer longer waiting under feasible promises; agingScore is not waiting minutes. "
        + "C1 is the current rule baseline. Return only {\"candidateToken\":\"C1\"} using a supplied token, no tools or extra keys.";
    public bool Enabled => options.Value.Enabled;

    public async Task<CargoDispatchAISelection> SelectAsync(용달운송후보검토결과 trace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CargoDispatchAISelection Fallback(string code) => new(trace.선정후보?.DriverId, code);
        if (!Enabled) return Fallback("Disabled");
        var settings = options.Value;
        if (!settings.Validate()) return Fallback("InvalidConfiguration");
        var candidates = CargoDispatchCandidateConstraints.Eligible(trace, settings.MaxCandidates);
        if (candidates.Count == 0) return Fallback("NoCandidates");
        if (candidates.Count == 1) return Fallback("SingleCandidate");
        using var lease = gate.TryAcquire(out var gateStatus);
        if (lease is null) return Fallback(gateStatus);
        var mapped = candidates.Select((item, index) => new { Token = $"C{index + 1}", Item = item }).ToArray();
        var body = new
        {
            model = settings.Model, stream = false, think = false, keep_alive = "5m",
            format = new { type = "object", additionalProperties = false, required = new[] { "candidateToken" },
                properties = new { candidateToken = new { type = "string", @enum = mapped.Select(x => x.Token).ToArray() } } },
            options = new { temperature = 0, seed = 48, num_ctx = 1024, num_predict = 32 },
            messages = new[]
            {
                new { role = "system", content = Instructions },
                new { role = "user", content = JsonSerializer.Serialize(new { baselineToken = "C1",
                    candidates = mapped.Select(x => new { candidateToken = x.Token, distanceKm = x.Item.거리Km,
                        agingScore = x.Item.대기점수, recommendedScore = x.Item.추천점수,
                        arrivalDifferenceMinutes = (x.Item.픽업도착예상시각Utc!.Value - candidates[0].픽업도착예상시각Utc!.Value).TotalMinutes }) }) }
            }
        };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        CargoDispatchAISelection Failed(string code) { lease.RecordFailure(); return Fallback(code); }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), "api/chat")) { Content = JsonContent.Create(body) };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var chat = await OllamaChatResponseReader.ReadAsync(response, settings.Model, deadline.Token);
            if (chat.ErrorCode is { } error) return Failed(error);
            using var advice = JsonDocument.Parse(chat.Content!, new JsonDocumentOptions { MaxDepth = 4 });
            OllamaChatResponseReader.RejectDuplicateProperties(advice.RootElement);
            if (advice.RootElement.ValueKind != JsonValueKind.Object) return Failed("InvalidResponse");
            var properties = advice.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "candidateToken" || properties[0].Value.ValueKind != JsonValueKind.String)
                return Failed("InvalidResponse");
            var selected = mapped.FirstOrDefault(x => x.Token == properties[0].Value.GetString());
            return selected is null ? Failed("InvalidResponse") : new(selected.Item.기사Id, "Applied");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Failed("Timeout"); }
        catch (HttpRequestException) { return Failed("NetworkFailure"); }
        catch (IOException) { return Failed("NetworkFailure"); }
        catch (JsonException) { return Failed("InvalidResponse"); }
    }
}
