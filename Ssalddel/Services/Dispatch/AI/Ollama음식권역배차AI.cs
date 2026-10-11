using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.Services.Dispatch.AI;

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Infrastructure,
    "최신 수요 집계와 검토 가능한 음식 조합의 수치만 루프백 Ollama에 보내 한 조합을 제안한다.",
    Effects = SsalddelCodeEffect.NetworkCall, FlowOrder = 48,
    StepKey = "infrastructure.food-regional-dispatch-ollama",
    DependsOnStepKeys = ["application.food-regional-dispatch-review"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "임시 조합 토큰 외 응답은 기준으로 복귀한다. 주소·좌표·메뉴·기사/주문 ID·자유 문장을 보내지 않는다. 참여 허용/순위·적재·시간·수락·정산 권위를 갖지 않는다.")]
public sealed class Ollama음식권역배차AI(HttpClient httpClient, IOptions<음식권역배차OllamaOptions> options)
    : I음식권역배차AI
{
    private const int MaxResponseBytes = 64 * 1024;
    private const string Instructions = "Select one supplied food-delivery preview plan token. "
        + "Current demand and capacity take precedence over time-of-day. Admission and driver priority are already fixed by server policy. "
        + "Never change admission, add drivers or orders, estimate missing quantities, or claim a confirmed assignment. "
        + "These are single or two-order geometric bundle previews, not verified vehicle capacity or complete promised schedules. "
        + "Compare supplied order count, distance and rule score. P1 is the server baseline. "
        + "Return only the JSON object {\"planToken\":\"P1\"}, choosing a supplied token. "
        + "No commentary, tools or additional keys.";

    public bool Enabled => options.Value.Enabled;

    public async Task<음식권역배차AI선택> 선택Async(음식권역배차AI검토 review,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        cancellationToken.ThrowIfCancellationRequested();
        var baseline = review.유효조합목록?.FirstOrDefault()?.조합키;
        음식권역배차AI선택 Fallback(string code) => new(baseline, code);
        if (!Enabled) return Fallback("Disabled");
        var settings = options.Value;
        if (!settings.Validate()) return Fallback("InvalidConfiguration");
        if (!review.수요자료최신) return Fallback("DemandUnknown");
        if (review.수요ActionCode is not ("AdditionalParticipationNeeded" or "ExistingCapacitySufficient")
            || review.예상업무량건수 is null or < 0m || review.기존기사가용처리건수 is null or < 0m
            || review.보호활동기사수 < 0 || review.추가참여인원 < 0 || review.제안허용기사수 <= 0
            || review.유효조합목록 is null || review.유효조합목록.Count > 50
            || review.유효조합목록.Any(x => x is null || !x.조합가능여부 || string.IsNullOrWhiteSpace(x.조합키)
                || x.의뢰Ids is null || x.작업목록 is null || x.배지 is null
                || x.의뢰Ids.Count is < 1 or > 2 || x.의뢰Ids.Distinct(StringComparer.Ordinal).Count() != x.의뢰Ids.Count
                || x.작업목록.Count != x.의뢰Ids.Count || x.제외사유?.Count is > 0
                || x.상차지간거리Km < 0m || x.하차지간거리Km < 0m || x.묶음내예상거리Km < 0m)
            || review.유효조합목록.Select(x => x.조합키).Distinct(StringComparer.Ordinal).Count() != review.유효조합목록.Count)
            return Fallback("InvalidPlans");
        var mapped = review.유효조합목록.Take(settings.MaxPlans)
            .Select((item, index) => new { Token = $"P{index + 1}", Item = item }).ToArray();
        if (mapped.Length == 0) return new(null, "NoPlans");
        if (mapped.Length == 1) return Fallback("SinglePlan");

        var tokens = mapped.Select(x => x.Token).ToArray();
        var schema = new
        {
            type = "object", additionalProperties = false, required = new[] { "planToken" },
            properties = new { planToken = new { type = "string", @enum = tokens } }
        };
        var context = new
        {
            baselineToken = "P1",
            demandAction = review.수요ActionCode,
            expectedWorkCount = review.예상업무량건수,
            existingCapacityCount = review.기존기사가용처리건수,
            protectedDriverCount = review.보호활동기사수,
            additionalParticipantCount = review.추가참여인원,
            allowedDriverCount = review.제안허용기사수,
            plans = mapped.Select(x => new
            {
                planToken = x.Token, orderCount = x.Item.의뢰Ids.Count,
                pickupSeparationKm = x.Item.상차지간거리Km, dropoffSeparationKm = x.Item.하차지간거리Km,
                approximateDistanceKm = x.Item.묶음내예상거리Km, ruleScore = x.Item.조합점수,
                samePickup = x.Item.배지.Contains("동일상차지", StringComparer.Ordinal),
                sameRegion = x.Item.배지.Contains("같은배달권", StringComparer.Ordinal),
                adjacentRegion = x.Item.배지.Contains("인접배달권", StringComparer.Ordinal)
            })
        };
        var body = new
        {
            model = settings.Model, stream = false, think = false, keep_alive = 0, format = schema,
            options = new { temperature = 0, seed = 50, num_ctx = 2048, num_predict = 64 },
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
                { Content = JsonContent.Create(body) };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) return Fallback("HttpFailure");
            if (response.Content.Headers.ContentLength > MaxResponseBytes) return Fallback("ResponseTooLarge");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (buffer.Length <= MaxResponseBytes)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(0,
                    Math.Min(chunk.Length, MaxResponseBytes + 1 - (int)buffer.Length)), deadline.Token);
                if (read == 0) break;
                buffer.Write(chunk, 0, read);
            }
            if (buffer.Length > MaxResponseBytes) return Fallback("ResponseTooLarge");
            using var envelope = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicateProperties(envelope.RootElement);
            var root = envelope.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.String || model.GetString() != settings.Model
                || !root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String || role.GetString() != "assistant"
                || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String
                || message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind != JsonValueKind.Null
                    && (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() != 0))
                return Fallback("InvalidResponse");
            using var selection = JsonDocument.Parse(content.GetString()!, new JsonDocumentOptions { MaxDepth = 4 });
            RejectDuplicateProperties(selection.RootElement);
            if (selection.RootElement.ValueKind != JsonValueKind.Object) return Fallback("InvalidResponse");
            var properties = selection.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "planToken" || properties[0].Value.ValueKind != JsonValueKind.String)
                return Fallback("InvalidResponse");
            var token = properties[0].Value.GetString();
            var selected = mapped.FirstOrDefault(x => x.Token == token);
            return selected is null ? Fallback("InvalidResponse") : new(selected.Item.조합키, "Applied");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Fallback("Timeout"); }
        catch (HttpRequestException) { return Fallback("NetworkFailure"); }
        catch (IOException) { return Fallback("NetworkFailure"); }
        catch (JsonException) { return Fallback("InvalidResponse"); }
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
    }
}
