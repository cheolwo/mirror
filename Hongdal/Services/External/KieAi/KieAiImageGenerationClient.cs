using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hongdal.Contracts.Common.Metadata;
using Microsoft.Extensions.Options;

namespace 홍달.Services.External.KieAi;

public interface IKieAiImageGenerationClient
{
    Task<KieAiCreateTaskResult> CreateTextToImageTaskAsync(KieAiCreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<KieAiTaskDetailResult> GetTaskDetailAsync(string taskId, CancellationToken cancellationToken = default);
    Task<Stream> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default);
}

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.ExternalAdapter,
    "Kie.ai GPT Image 작업 생성·상태 조회·결과 다운로드 HTTP adapter",
    ContractType = typeof(IKieAiImageGenerationClient),
    FlowOrder = 60,
    Effects = HongdalCodeEffect.NetworkCall
              | HongdalCodeEffect.ThirdPartyApiCall
              | HongdalCodeEffect.MayIncurExternalCost,
    Boundary = "API key는 서버 설정에서만 읽으며 작업 생성 호출은 외부 비용을 발생시킬 수 있습니다.")]
public sealed class KieAiImageGenerationClient : IKieAiImageGenerationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly KieAiOptions _options;

    public KieAiImageGenerationClient(HttpClient httpClient, IOptions<KieAiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<KieAiCreateTaskResult> CreateTextToImageTaskAsync(KieAiCreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new ArgumentException("prompt is required.", nameof(request));
        }

        EnsureApiKey();
        using var message = new HttpRequestMessage(HttpMethod.Post, _options.CreateTaskPath)
        {
            Content = JsonContent.Create(new KieAiCreateTaskHttpRequest(
                _options.Model,
                request.CallBackUrl,
                new KieAiTextToImageInput(
                    request.Prompt.Trim(),
                    NormalizeAspectRatio(request.AspectRatio),
                    NormalizeQuality(request.Quality))))
        };
        ApplyAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<KieAiCreateTaskResponse>(rawJson, JsonOptions);

        if (!response.IsSuccessStatusCode || payload?.Code != 200 || string.IsNullOrWhiteSpace(payload.Data?.TaskId))
        {
            throw new InvalidOperationException(
                $"Kie.AI task creation failed. Status={(int)response.StatusCode}, Body={Truncate(rawJson)}");
        }

        return new KieAiCreateTaskResult(payload.Data.TaskId.Trim(), rawJson);
    }

    public async Task<KieAiTaskDetailResult> GetTaskDetailAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("taskId is required.", nameof(taskId));
        }

        EnsureApiKey();

        var path = _options.GetTaskPathTemplate.Replace("{taskId}", Uri.EscapeDataString(taskId), StringComparison.Ordinal);
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuthorization(message);
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Kie.AI task detail request failed. Status={(int)response.StatusCode}, Body={Truncate(rawJson)}");
        }

        var payload = JsonSerializer.Deserialize<KieAiTaskDetailResponse>(rawJson, JsonOptions);
        if (payload?.Code != 200 || payload.Data is null)
        {
            throw new InvalidOperationException($"Kie.AI task detail response was invalid. Body={Truncate(rawJson)}");
        }

        return KieAiTaskDetailResult.From(payload, rawJson);
    }

    public async Task<Stream> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("imageUrl is required.", nameof(imageUrl));
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("imageUrl must be an absolute HTTP(S) URL.", nameof(imageUrl));
        }

        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var memory = new MemoryStream();
        await response.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }

    private void EnsureApiKey()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("KieAi:ApiKey configuration is required.");
        }
    }

    private void ApplyAuthorization(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    private static string NormalizeAspectRatio(string? value)
        => string.IsNullOrWhiteSpace(value) ? "auto" : value.Trim();

    private static string? NormalizeQuality(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string Truncate(string value)
        => value.Length <= 1_000 ? value : value[..1_000];
}

public sealed record KieAiCreateTaskRequest(
    string Prompt,
    string AspectRatio,
    string? Quality,
    string? CallBackUrl);
public sealed record KieAiCreateTaskResult(string TaskId, string RawJson);

public sealed class KieAiTaskDetailResult
{
    public string? TaskId { get; init; }
    public string? Status { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyList<string> ImageUrls { get; init; } = [];
    public string? FailureMessage { get; init; }
    public int? Progress { get; init; }
    public decimal? CreditsConsumed { get; init; }
    public string RawJson { get; init; } = string.Empty;
    public bool IsTerminal => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Status, "succeeded", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Status, "fail", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Status, "failed", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Status, "error", StringComparison.OrdinalIgnoreCase);
    public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(Status, "succeeded", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);

    public static KieAiTaskDetailResult From(KieAiTaskDetailResponse? response, string rawJson)
    {
        var data = response?.Data;
        var imageUrls = ExtractResultUrls(data)
            .Concat(data?.Result?.Select(item => item.Url) ?? [])
            .Concat(data?.Images?.Select(item => item.Url) ?? [])
            .Append(data?.ImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new KieAiTaskDetailResult
        {
            TaskId = data?.TaskId,
            Status = data?.State ?? data?.Status ?? response?.Msg,
            ImageUrl = imageUrls.FirstOrDefault(),
            ImageUrls = imageUrls,
            FailureMessage = data?.FailMsg,
            Progress = data?.Progress,
            CreditsConsumed = data?.CreditsConsumed,
            RawJson = rawJson
        };
    }

    private static IEnumerable<string?> ExtractResultUrls(KieAiTaskDetailResponseData? data)
    {
        if (data?.ResultJson is not JsonElement resultJson)
        {
            return [];
        }

        try
        {
            JsonElement root;
            if (resultJson.ValueKind == JsonValueKind.String)
            {
                using var document = JsonDocument.Parse(resultJson.GetString() ?? "{}");
                root = document.RootElement.Clone();
            }
            else
            {
                root = resultJson;
            }

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("resultUrls", out var urls)
                || urls.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return urls.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed record KieAiCreateTaskHttpRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("callBackUrl")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CallBackUrl,
    [property: JsonPropertyName("input")] KieAiTextToImageInput Input);

public sealed record KieAiTextToImageInput(
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("aspect_ratio")] string AspectRatio,
    [property: JsonPropertyName("quality")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Quality);

public sealed class KieAiCreateTaskResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public KieAiCreateTaskResponseData? Data { get; set; }
}

public sealed class KieAiCreateTaskResponseData
{
    [JsonPropertyName("taskId")]
    public string? TaskId { get; set; }
}

public sealed class KieAiTaskDetailResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public KieAiTaskDetailResponseData? Data { get; set; }
}

public sealed class KieAiTaskDetailResponseData
{
    [JsonPropertyName("taskId")]
    public string? TaskId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("resultJson")]
    public JsonElement? ResultJson { get; set; }

    [JsonPropertyName("failMsg")]
    public string? FailMsg { get; set; }

    [JsonPropertyName("progress")]
    public int? Progress { get; set; }

    [JsonPropertyName("creditsConsumed")]
    public decimal? CreditsConsumed { get; set; }

    [JsonPropertyName("result")]
    public List<KieAiTaskImageItem>? Result { get; set; }

    [JsonPropertyName("images")]
    public List<KieAiTaskImageItem>? Images { get; set; }
}

public sealed class KieAiTaskImageItem
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
