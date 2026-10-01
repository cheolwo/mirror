using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace 홍달.Services.HIOPSAI;

public interface IHIOPSAIClient
{
    Task<HIOPSAICompletionResult> CompleteAsync(HIOPSAICompletionRequest request, CancellationToken cancellationToken = default);
}

public sealed class HIOPSAIClient : IHIOPSAIClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly HIOPSAIOptions _options;
    private readonly IHIOPSAIUsageBudgetStore _budgetStore;
    private readonly ILogger<HIOPSAIClient> _logger;

    public HIOPSAIClient(
        HttpClient httpClient,
        IOptions<HIOPSAIOptions> options,
        IHIOPSAIUsageBudgetStore budgetStore,
        ILogger<HIOPSAIClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _budgetStore = budgetStore;
        _logger = logger;
    }

    public async Task<HIOPSAICompletionResult> CompleteAsync(
        HIOPSAICompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HIOPSAICompletionResult.Blocked("HIOPSAI:Enabled 설정이 false입니다.", _options.DefaultModel, 0m, 0m, _options.MonthlyBudgetUsd);
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return HIOPSAICompletionResult.Blocked("HIOPSAI:ApiKey 설정이 필요합니다.", _options.DefaultModel, 0m, 0m, _options.MonthlyBudgetUsd);
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? _options.DefaultModel : request.Model;
        var inputTokens = request.EstimatedInputTokens ?? EstimateTokens(request.Messages);
        var outputTokens = request.MaxOutputTokens ?? _options.MaxOutputTokens;
        inputTokens = Math.Min(Math.Max(inputTokens, 0), Math.Max(1, _options.MaxInputTokens));
        outputTokens = Math.Min(Math.Max(outputTokens, 1), Math.Max(1, _options.MaxOutputTokens));

        var estimatedCostUsd = EstimateCost(inputTokens, outputTokens);
        var reservation = await _budgetStore.TryReserveAsync(estimatedCostUsd, cancellationToken);
        if (!reservation.Allowed)
        {
            return HIOPSAICompletionResult.Blocked(
                reservation.BlockedReason ?? "HIOPS AI 예산 제한에 도달했습니다.",
                model,
                estimatedCostUsd,
                reservation.MonthlySpentUsd + reservation.MonthlyReservedUsd,
                reservation.MonthlyBudgetUsd);
        }

        try
        {
            ApplyAuthorization();
            using var response = await _httpClient.PostAsJsonAsync(
                _options.ResponsesPath,
                OpenAIResponsesRequest.From(model, request, outputTokens),
                JsonOptions,
                cancellationToken);

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await _budgetStore.ReleaseAsync(reservation.ReservationId, cancellationToken);
                _logger.LogWarning(
                    "HIOPS AI call failed. Status={StatusCode}, Body={Body}",
                    (int)response.StatusCode,
                    rawJson);
                return HIOPSAICompletionResult.Blocked(
                    $"OpenAI 호출 실패: HTTP {(int)response.StatusCode}",
                    model,
                    estimatedCostUsd,
                    reservation.MonthlySpentUsd + reservation.MonthlyReservedUsd,
                    reservation.MonthlyBudgetUsd,
                    rawJson);
            }

            var parsed = ParseResponse(rawJson);
            var actualInputTokens = parsed.InputTokens ?? inputTokens;
            var actualOutputTokens = parsed.OutputTokens ?? outputTokens;
            var actualCostUsd = EstimateCost(actualInputTokens, actualOutputTokens);
            var usage = await _budgetStore.CompleteAsync(reservation.ReservationId, actualCostUsd, cancellationToken);

            return new HIOPSAICompletionResult(
                Success: true,
                Text: parsed.Text,
                BlockedReason: null,
                Model: model,
                EstimatedCostUsd: estimatedCostUsd,
                ActualCostUsd: actualCostUsd,
                MonthlyUsedUsd: usage.MonthlySpentUsd + usage.MonthlyReservedUsd,
                MonthlyBudgetUsd: usage.MonthlyBudgetUsd,
                InputTokens: actualInputTokens,
                OutputTokens: actualOutputTokens,
                RawJson: rawJson);
        }
        catch (Exception ex)
        {
            await _budgetStore.ReleaseAsync(reservation.ReservationId, cancellationToken);
            _logger.LogError(ex, "HIOPS AI call failed.");
            return HIOPSAICompletionResult.Blocked(
                "OpenAI 호출 중 오류가 발생했습니다.",
                model,
                estimatedCostUsd,
                reservation.MonthlySpentUsd + reservation.MonthlyReservedUsd,
                reservation.MonthlyBudgetUsd);
        }
    }

    private void ApplyAuthorization()
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    private decimal EstimateCost(int inputTokens, int outputTokens)
    {
        var inputCost = inputTokens / 1_000_000m * _options.InputUsdPerMillionTokens;
        var outputCost = outputTokens / 1_000_000m * _options.OutputUsdPerMillionTokens;
        return decimal.Round(inputCost + outputCost, 6, MidpointRounding.AwayFromZero);
    }

    private static int EstimateTokens(IReadOnlyList<HIOPSAIMessage> messages)
    {
        var chars = messages.Sum(message => (message.Content?.Length ?? 0) + (message.Role?.Length ?? 0));
        var imageTokens = messages.Sum(message => (message.Images?.Count ?? 0) * 1024);
        return Math.Max(1, (int)Math.Ceiling(chars / 4m) + imageTokens);
    }

    private static ParsedOpenAIResponse ParseResponse(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;

        var text = ExtractOutputText(root);
        int? inputTokens = null;
        int? outputTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var input))
            {
                inputTokens = input.GetInt32();
            }

            if (usage.TryGetProperty("output_tokens", out var output))
            {
                outputTokens = output.GetInt32();
            }
        }

        return new ParsedOpenAIResponse(text, inputTokens, outputTokens);
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }
        }

        return builder.ToString();
    }

    private sealed record ParsedOpenAIResponse(string Text, int? InputTokens, int? OutputTokens);
}

public sealed record HIOPSAICompletionRequest(
    string Purpose,
    IReadOnlyList<HIOPSAIMessage> Messages,
    string? Model = null,
    int? EstimatedInputTokens = null,
    int? MaxOutputTokens = null,
    string? CorrelationId = null,
    HIOPSAIJsonSchema? OutputJsonSchema = null);

public sealed record HIOPSAIMessage(
    string Role,
    string Content,
    IReadOnlyList<HIOPSAIImageInput>? Images = null);

public sealed record HIOPSAIImageInput(
    string DataUrl,
    string Detail = "low",
    string? Label = null);

public sealed record HIOPSAIJsonSchema(
    string Name,
    JsonElement Schema,
    bool Strict = true);

public sealed record HIOPSAICompletionResult(
    bool Success,
    string Text,
    string? BlockedReason,
    string Model,
    decimal EstimatedCostUsd,
    decimal ActualCostUsd,
    decimal MonthlyUsedUsd,
    decimal MonthlyBudgetUsd,
    int InputTokens,
    int OutputTokens,
    string? RawJson = null)
{
    public static HIOPSAICompletionResult Blocked(
        string reason,
        string model,
        decimal estimatedCostUsd,
        decimal monthlyUsedUsd,
        decimal monthlyBudgetUsd,
        string? rawJson = null)
        => new(
            Success: false,
            Text: string.Empty,
            BlockedReason: reason,
            Model: model,
            EstimatedCostUsd: estimatedCostUsd,
            ActualCostUsd: 0m,
            MonthlyUsedUsd: monthlyUsedUsd,
            MonthlyBudgetUsd: monthlyBudgetUsd,
            InputTokens: 0,
            OutputTokens: 0,
            RawJson: rawJson);
}

internal sealed record OpenAIResponsesRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] IReadOnlyList<OpenAIInputMessage> Input,
    [property: JsonPropertyName("max_output_tokens")] int MaxOutputTokens,
    [property: JsonPropertyName("store")] bool Store,
    [property: JsonPropertyName("text")] OpenAITextConfiguration? Text)
{
    public static OpenAIResponsesRequest From(string model, HIOPSAICompletionRequest request, int maxOutputTokens)
        => new(
            model,
            request.Messages.Select(message => new OpenAIInputMessage(
                message.Role,
                BuildContent(message))).ToArray(),
            maxOutputTokens,
            Store: false,
            request.OutputJsonSchema is null
                ? null
                : new OpenAITextConfiguration(new OpenAITextFormat(
                    "json_schema",
                    request.OutputJsonSchema.Name,
                    request.OutputJsonSchema.Schema,
                    request.OutputJsonSchema.Strict)));

    private static IReadOnlyList<OpenAIInputContent> BuildContent(HIOPSAIMessage message)
    {
        var content = new List<OpenAIInputContent>
        {
            OpenAIInputContent.CreateText(message.Content)
        };
        foreach (var image in message.Images ?? [])
        {
            if (!string.IsNullOrWhiteSpace(image.Label))
            {
                content.Add(OpenAIInputContent.CreateText(image.Label));
            }

            content.Add(OpenAIInputContent.CreateImage(image.DataUrl, image.Detail));
        }

        return content;
    }
}

internal sealed record OpenAIInputMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] IReadOnlyList<OpenAIInputContent> Content);

internal sealed record OpenAIInputContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("detail")] string? Detail)
{
    public static OpenAIInputContent CreateText(string value)
        => new("input_text", value, null, null);

    public static OpenAIInputContent CreateImage(string dataUrl, string detail)
        => new("input_image", null, dataUrl, detail);
}

internal sealed record OpenAITextConfiguration(
    [property: JsonPropertyName("format")] OpenAITextFormat Format);

internal sealed record OpenAITextFormat(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("schema")] JsonElement Schema,
    [property: JsonPropertyName("strict")] bool Strict);
