using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.Services.Dispatch.AI;

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Infrastructure,
    "최신 주문 수집·기사 여력 집계로 서버가 허용한 수집 또는 즉시 처리만 로컬 모델에 묻는다.",
    Effects = SsalddelCodeEffect.NetworkCall, FlowOrder = 47,
    StepKey = "infrastructure.food-dispatch-timing-ollama",
    DependsOnStepKeys = ["application.food-dispatch-timing"],
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "주소·좌표·기사/주문 ID·원장 문구 없이 최소 집계만 보낸다. 모델은 원 접수 대기를 연장하거나 묶음/기사 확정·운임·수락·알림을 변경하지 않는다.")]
public sealed class OllamaFoodDispatchTimingAI(HttpClient httpClient,
    IOptions<FoodDispatchTimingOllamaOptions> options, TimeProvider clock) : IFoodDispatchTimingAI
{
    private const string Instructions = "Choose whether to briefly collect more food-delivery orders or dispatch now. "
        + "The server has already enforced the immutable original queue age, collection limit, maximum wait, freshness and driver capacity gates. "
        + "Collect only while it can improve compatible combinations within the supplied remaining window. "
        + "Prefer DispatchNow if a useful combination already exists, no eligible driver exists, arrivals are sparse, data is unknown, "
        + "or waiting risks delay. Never extend waiting or invent drivers, orders, deadlines or fee information. "
        + "Compatibility is geometric opportunity, not verified capacity, complete promises or a confirmed multi-order assignment. "
        + "Return only {\"actionCode\":\"Collect\"} or {\"actionCode\":\"DispatchNow\"}, choosing a supplied allowed action. "
        + "No commentary, tools or additional keys.";

    public bool Enabled => options.Value.Enabled;

    public async Task<FoodDispatchTimingAISelection> SelectAsync(FoodDispatchTimingInput input,
        FoodDispatchTimingDecision baseline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(baseline);
        cancellationToken.ThrowIfCancellationRequested();
        FoodDispatchTimingAISelection Fallback(string code) => new("DispatchNow", code);
        if (!Enabled) return Fallback("Disabled");
        var settings = options.Value;
        if (!settings.Validate()) return Fallback("InvalidConfiguration");
        var allowed = baseline.AllowedActions?.Where(x => x is "Collect" or "DispatchNow")
            .Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (!allowed.Contains("Collect", StringComparer.Ordinal) || !allowed.Contains("DispatchNow", StringComparer.Ordinal))
            return Fallback("ServerRequired");
        if (!input.Applicable || input.HasActiveOffer || input.IsRetry || input.DelayRisk
            || !input.DemandKnown || !input.DriverCapacityKnown || input.SnapshotAtUtc is null
            || input.SnapshotAtUtc > input.NowUtc || input.AvailableDrivers <= 0 || input.WaitingOrders < 1
            || input.CompatibleOrders < 0 || input.ExpectedNewOrders is null or <= 0m
            || input.QueuedAtUtc > input.NowUtc || baseline.RecheckAtUtc is null
            || baseline.RecheckAtUtc > baseline.HardDeadlineUtc)
            return Fallback("InvalidInput");
        var remaining = baseline.RecheckAtUtc.Value - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return Fallback("DecisionExpired");

        var schema = new
        {
            type = "object", additionalProperties = false, required = new[] { "actionCode" },
            properties = new { actionCode = new { type = "string", @enum = allowed } }
        };
        var context = new
        {
            baselineAction = baseline.ActionCode, allowedActions = allowed,
            queueAgeSeconds = (input.NowUtc - input.QueuedAtUtc).TotalSeconds,
            remainingCollectionSeconds = remaining.TotalSeconds,
            remainingMaximumWaitSeconds = Math.Max(0d, (baseline.HardDeadlineUtc - input.NowUtc).TotalSeconds),
            demandKnown = input.DemandKnown, driverCapacityKnown = input.DriverCapacityKnown,
            waitingOrders = input.WaitingOrders, compatibleOrders = input.CompatibleOrders,
            availableDrivers = input.AvailableDrivers, expectedNewOrders = input.ExpectedNewOrders,
            promisingPlanAvailable = input.PromisingPlanAvailable, delayRisk = input.DelayRisk
        };
        var body = new
        {
            model = settings.Model, stream = false, think = false, keep_alive = 0, format = schema,
            options = new { temperature = 0, seed = 52, num_ctx = 2048, num_predict = 64 },
            messages = new[]
            {
                new { role = "system", content = Instructions },
                new { role = "user", content = JsonSerializer.Serialize(context) }
            }
        };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(remaining < TimeSpan.FromSeconds(settings.TimeoutSeconds)
            ? remaining : TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), "api/chat"))
                { Content = JsonContent.Create(body) };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var chat = await OllamaChatResponseReader.ReadAsync(response, settings.Model, deadline.Token);
            if (chat.ErrorCode is { } errorCode) return Fallback(errorCode);
            using var selection = JsonDocument.Parse(chat.Content!, new JsonDocumentOptions { MaxDepth = 4 });
            OllamaChatResponseReader.RejectDuplicateProperties(selection.RootElement);
            if (selection.RootElement.ValueKind != JsonValueKind.Object) return Fallback("InvalidResponse");
            var properties = selection.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "actionCode" || properties[0].Value.ValueKind != JsonValueKind.String)
                return Fallback("InvalidResponse");
            var selected = properties[0].Value.GetString();
            return selected is not null && allowed.Contains(selected, StringComparer.Ordinal)
                ? new(selected, "Applied") : Fallback("InvalidResponse");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Fallback("Timeout"); }
        catch (HttpRequestException) { return Fallback("NetworkFailure"); }
        catch (IOException) { return Fallback("NetworkFailure"); }
        catch (JsonException) { return Fallback("InvalidResponse"); }
    }

}
