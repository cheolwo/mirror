using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;
using 홍달.Services.External.KieAi;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External;

public sealed class KieAiImageGenerationClientTests
{
    [Fact]
    public async Task CreateTextToImageTaskAsync_UsesMarketGptImageContract()
    {
        var handler = new RecordingHandler(request => JsonResponse(
            """
            {"code":200,"msg":"success","data":{"taskId":"task-gpt-image-1"}}
            """));
        var client = CreateClient(handler);

        var result = await client.CreateTextToImageTaskAsync(
            new KieAiCreateTaskRequest(
                "A community gathering around a shared table",
                "3:2",
                Quality: null,
                CallBackUrl: "https://example.com/api/callback"));

        Assert.Equal("task-gpt-image-1", result.TaskId);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/v1/jobs/createTask", handler.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-api-key", handler.AuthorizationParameter);

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;
        Assert.Equal("gpt-image-2-text-to-image", root.GetProperty("model").GetString());
        Assert.Equal("https://example.com/api/callback", root.GetProperty("callBackUrl").GetString());
        var input = root.GetProperty("input");
        Assert.Equal("3:2", input.GetProperty("aspect_ratio").GetString());
        Assert.Equal("A community gathering around a shared table", input.GetProperty("prompt").GetString());
        Assert.False(input.TryGetProperty("quality", out _));
        Assert.False(input.TryGetProperty("resolution", out _));
    }

    [Fact]
    public async Task CreateTextToImageTaskAsync_OmitsCallbackWhenItIsNotConfigured()
    {
        var handler = new RecordingHandler(request => JsonResponse(
            """
            {"code":200,"msg":"success","data":{"taskId":"task-without-callback"}}
            """));
        var client = CreateClient(handler);

        await client.CreateTextToImageTaskAsync(
            new KieAiCreateTaskRequest(
                "A neighborhood group preparing a shared food order",
                "1:1",
                Quality: null,
                CallBackUrl: null));

        using var document = JsonDocument.Parse(handler.Body!);
        Assert.False(document.RootElement.TryGetProperty("callBackUrl", out _));
    }

    [Fact]
    public async Task GetTaskDetailAsync_ParsesUnifiedMarketResultJson()
    {
        var handler = new RecordingHandler(request => JsonResponse(
            """
            {
              "code": 200,
              "msg": "success",
              "data": {
                "taskId": "task-gpt-image-2",
                "state": "success",
                "resultJson": "{\"resultUrls\":[\"https://cdn.example.com/result.png\"]}",
                "progress": 100,
                "creditsConsumed": 12
              }
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetTaskDetailAsync("task-gpt-image-2");

        Assert.True(result.IsTerminal);
        Assert.True(result.IsSuccess);
        Assert.Equal("https://cdn.example.com/result.png", result.ImageUrl);
        Assert.Equal(100, result.Progress);
        Assert.Equal(12m, result.CreditsConsumed);
        Assert.Equal("/api/v1/jobs/recordInfo", handler.RequestUri?.AbsolutePath);
        Assert.Equal("task-gpt-image-2", ParseQuery(handler.RequestUri!).GetValueOrDefault("taskId"));
    }

    [Fact]
    public async Task GetTaskDetailAsync_RecognizesFailedMarketTask()
    {
        var handler = new RecordingHandler(request => JsonResponse(
            """
            {
              "code": 200,
              "msg": "success",
              "data": {
                "taskId": "task-failed",
                "state": "fail",
                "failMsg": "content policy"
              }
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetTaskDetailAsync("task-failed");

        Assert.True(result.IsTerminal);
        Assert.False(result.IsSuccess);
        Assert.Equal("content policy", result.FailureMessage);
    }

    private static KieAiImageGenerationClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.kie.ai")
        };
        return new KieAiImageGenerationClient(
            httpClient,
            Options.Create(new KieAiOptions
            {
                ApiKey = "test-api-key",
                BaseUrl = "https://api.kie.ai",
                CreateTaskPath = "/api/v1/jobs/createTask",
                GetTaskPathTemplate = "/api/v1/jobs/recordInfo?taskId={taskId}",
                Model = "gpt-image-2-text-to-image"
            }));
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static Dictionary<string, string> ParseQuery(Uri uri)
        => uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Split('=', 2))
            .ToDictionary(
                value => Uri.UnescapeDataString(value[0]),
                value => value.Length > 1 ? Uri.UnescapeDataString(value[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
