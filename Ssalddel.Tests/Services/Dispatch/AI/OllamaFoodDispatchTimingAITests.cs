using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.AI;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class OllamaFoodDispatchTimingAITests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("http://127.0.0.1:12681/", true)]
    [InlineData("http://[::1]:12681/", true)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("http://localhost:12681/", false)]
    [InlineData("http://10.0.0.1:12681/", false)]
    [InlineData("https://127.0.0.1:12681/", false)]
    [InlineData("http://127.0.0.1:12681/api/", false)]
    [InlineData("http://user:secret@127.0.0.1:12681/", false)]
    public void Options는루프백명시포트만허용한다(string url, bool valid)
        => Assert.Equal(valid, new FoodDispatchTimingOllamaOptions { BaseUrl = url }.Validate());

    [Fact]
    public void 정책기본OFF와수집상한및정책범위를검사한다()
    {
        Assert.False(new FoodDispatchTimingOptions().Enabled);
        Assert.False(new FoodDispatchTimingOllamaOptions().Enabled);
        Assert.Equal(2, new FoodDispatchTimingOllamaOptions().TimeoutSeconds);
        Assert.True(new FoodDispatchTimingOptions().Validate());
        Assert.False(new FoodDispatchTimingOptions { Policy = new() { MaxWaitSeconds = 0 } }.Validate());
        Assert.False(new FoodDispatchTimingOptions { Policy = new() { CollectionSeconds = 61 } }.Validate());
        Assert.False(new FoodDispatchTimingOptions { Policy = new() { DeadlineBufferSeconds = 61 } }.Validate());
        Assert.False(new FoodDispatchTimingOptions { Policy = new() { MaxSnapshotAgeSeconds = 0 } }.Validate());
        Assert.False(new FoodDispatchTimingOptions { MaximumQueueRows = 0 }.Validate());
        Assert.False(new FoodDispatchTimingOllamaOptions { Model = "other" }.Validate());
    }

    [Theory]
    [InlineData("Disabled", "Disabled")]
    [InlineData("Configuration", "InvalidConfiguration")]
    [InlineData("Required", "ServerRequired")]
    [InlineData("Expired", "DecisionExpired")]
    [InlineData("Unknown", "InvalidInput")]
    public async Task 강제처리만료미확인설정실패는호출하지않는다(string scenario, string expected)
    {
        var handler = new RecordingHandler();
        var settings = new FoodDispatchTimingOllamaOptions { Enabled = scenario != "Disabled" };
        var input = Input();
        var baseline = Baseline(input);
        if (scenario == "Configuration") settings.BaseUrl = "https://external.example/";
        if (scenario == "Required") baseline = baseline with { AllowedActions = ["DispatchNow"] };
        if (scenario == "Expired") baseline = baseline with { RecheckAtUtc = Now };
        if (scenario == "Unknown") input = input with { DriverCapacityKnown = false };
        var result = await Client(handler, settings).SelectAsync(input, baseline);
        Assert.Equal(new FoodDispatchTimingAISelection("DispatchNow", expected), result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task 현재집계와허용행동만전달하고자유문장이나식별정보를보내지않는다()
    {
        var handler = new RecordingHandler();
        var input = Input();
        var baseline = Baseline(input) with { ReasonCode = "PRIVATE-ORDER-DRIVER-ADDRESS" };
        var result = await Client(handler).SelectAsync(input, baseline);
        Assert.Equal(new FoodDispatchTimingAISelection("Collect", "Applied"), result);
        Assert.Equal("http://127.0.0.1:12681/api/chat", handler.Uri!.AbsoluteUri);
        Assert.Null(handler.Authorization);
        Assert.DoesNotContain("PRIVATE-", handler.Body!);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal(new[] { "model", "stream", "think", "keep_alive", "format", "options", "messages" },
            request.RootElement.EnumerateObject().Select(x => x.Name));
        Assert.Equal(new[] { "temperature", "seed", "num_ctx", "num_predict" },
            request.RootElement.GetProperty("options").EnumerateObject().Select(x => x.Name));
        Assert.Equal(52, request.RootElement.GetProperty("options").GetProperty("seed").GetInt32());
        Assert.False(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(request.RootElement.GetProperty("think").GetBoolean());
        Assert.False(request.RootElement.TryGetProperty("tools", out _));
        Assert.False(request.RootElement.GetProperty("format").GetProperty("additionalProperties").GetBoolean());
        using var context = JsonDocument.Parse(request.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        var keys = context.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "baselineAction", "allowedActions", "queueAgeSeconds", "remainingCollectionSeconds",
            "remainingMaximumWaitSeconds", "demandKnown", "driverCapacityKnown", "waitingOrders", "compatibleOrders",
            "availableDrivers", "expectedNewOrders", "promisingPlanAvailable", "delayRisk" }, keys);
        Assert.Equal(5d, context.RootElement.GetProperty("queueAgeSeconds").GetDouble());
        Assert.Equal(25d, context.RootElement.GetProperty("remainingCollectionSeconds").GetDouble());
        Assert.Equal(2, context.RootElement.GetProperty("availableDrivers").GetInt32());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 모델은허용된즉시처리도선택할수있다()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Envelope("{\"actionCode\":\"DispatchNow\"}")));
        Assert.Equal(new FoodDispatchTimingAISelection("DispatchNow", "Applied"), await Client(handler).SelectAsync(Input(), Baseline()));
    }

    [Theory]
    [InlineData("{\"actionCode\":\"CollectForever\"}")]
    [InlineData("{\"actionCode\":\"Collect\",\"seconds\":300}")]
    [InlineData("{\"actionCode\":\"Collect\",\"actionCode\":\"DispatchNow\"}")]
    [InlineData("{\"actionCode\":null}")]
    [InlineData("{\"actionCode\":2}")]
    [InlineData("{\"actionCode\":\"\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    public async Task 허용밖응답과기한변경중복필드를거부한다(string content)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Envelope(content)));
        Assert.Equal(new FoodDispatchTimingAISelection("DispatchNow", "InvalidResponse"), await Client(handler).SelectAsync(Input(), Baseline()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"model\":\"other\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":false,\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":true,\"message\":{\"role\":\"user\",\"content\":\"{}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":false,\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{}\",\"tool_calls\":[{}]}}")]
    public async Task 불완전봉투나도구요청을거부한다(string raw)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(raw)));
        Assert.Equal("InvalidResponse", (await Client(handler).SelectAsync(Input(), Baseline())).StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task Http실패는재시도하지않고즉시처리로복귀한다(HttpStatusCode status)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("PRIVATE-ERROR") }));
        Assert.Equal("HttpFailure", (await Client(handler).SelectAsync(Input(), Baseline())).StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 네트워크실패는자동재전송하지않는다()
    {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("PRIVATE-ERROR"));
        Assert.Equal("NetworkFailure", (await Client(handler).SelectAsync(Input(), Baseline())).StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 길이헤더유무와무관하게응답읽기상한을적용한다(bool unknownLength)
    {
        var body = new string('x', 100000);
        var handler = new RecordingHandler((_, _) => Task.FromResult(unknownLength
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(body) } : Raw(body)));
        Assert.Equal("ResponseTooLarge", (await Client(handler).SelectAsync(Input(), Baseline())).StatusCode);
    }

    [Fact]
    public async Task 모델제한시간보다수집잔여시간이짧으면먼저중단한다()
    {
        var handler = new RecordingHandler(async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return Envelope(); });
        var baseline = Baseline() with { RecheckAtUtc = Now.AddMilliseconds(50) };
        Assert.Equal("Timeout", (await Client(handler).SelectAsync(Input(), baseline)).StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 호출자취소를정상즉시처리판정으로숨기지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new RecordingHandler(async (_, token) =>
        { cancellation.Cancel(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return Envelope(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).SelectAsync(Input(), Baseline(), cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }

    private static FoodDispatchTimingInput Input() => new()
    {
        NowUtc = Now, QueuedAtUtc = Now.AddSeconds(-5), SnapshotAtUtc = Now,
        DemandKnown = true, DriverCapacityKnown = true, AvailableDrivers = 2,
        WaitingOrders = 1, CompatibleOrders = 1, ExpectedNewOrders = 1m
    };
    private static FoodDispatchTimingDecision Baseline(FoodDispatchTimingInput? input = null)
        => new FoodDispatchTimingPolicy().Evaluate(input ?? Input());
    private static OllamaFoodDispatchTimingAI Client(RecordingHandler handler, FoodDispatchTimingOllamaOptions? options = null)
        => new(new HttpClient(handler), Options.Create(options ?? new() { Enabled = true }), new Clock());
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private static HttpResponseMessage Envelope(string content = "{\"actionCode\":\"Collect\"}")
        => Raw(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true, message = new { role = "assistant", content } }));
    private static HttpResponseMessage Raw(string content)
        => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? response = null) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        public Uri? Uri { get; private set; }
        public object? Authorization { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Uri = request.RequestUri; Authorization = request.Headers.Authorization;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return response is null ? Envelope() : await response(request, cancellationToken);
        }
    }
    private sealed class UnknownLengthContent(string body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
    }
}
