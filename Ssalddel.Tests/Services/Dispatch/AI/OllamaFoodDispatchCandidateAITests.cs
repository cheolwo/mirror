using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class OllamaFoodDispatchCandidateAITests
{
    [Theory]
    [InlineData("http://127.0.0.1:12681/", true)]
    [InlineData("http://127.0.0.1:12681", true)]
    [InlineData("http://127.0.0.1:80/", true)]
    [InlineData("http://[::1]:12681/", true)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("http://[::1]/", false)]
    [InlineData("http://localhost:12681/", false)]
    [InlineData("https://127.0.0.1:12681/", false)]
    [InlineData("http://192.168.0.2:12681/", false)]
    [InlineData("http://127.0.0.1:12681/api/", false)]
    [InlineData("http://user:password@127.0.0.1:12681/", false)]
    [InlineData("http://127.0.0.1:12681/?key=secret", false)]
    [InlineData("http://127.0.0.1:12681/#fragment", false)]
    [InlineData("http://127.0.0.1:0/", false)]
    public void Options_명시적루프백Http포트만허용한다(string endpoint, bool expected)
        => Assert.Equal(expected, new FoodDispatchOllamaOptions { BaseUrl = endpoint }.Validate());

    [Theory]
    [InlineData(0, 12, "qwen3:1.7b")]
    [InlineData(91, 12, "qwen3:1.7b")]
    [InlineData(10, 1, "qwen3:1.7b")]
    [InlineData(10, 31, "qwen3:1.7b")]
    [InlineData(10, 12, "other-model")]
    [InlineData(10, 12, "qwen3:1.7b-cloud")]
    public void Options_범위밖설정이나다른모델을거부한다(int seconds, int maximum, string model)
        => Assert.False(new FoodDispatchOllamaOptions { TimeoutSeconds = seconds, MaxCandidates = maximum, Model = model }.Validate());

    [Fact]
    public void Constraints_최종허용집합과동일픽업단계및수요우선순위를보존한다()
    {
        var trace = Trace(rows:
        [
            Row("excluded", verdict: "DriverReceivingOff"),
            Row("unlisted"),
            Row("late", pickup: 2),
            Row("lower-priority", priority: 3),
            Row("unknown-priority", priority: null),
            Row("allowed-b", priority: 1),
            Row("allowed-a")
        ], allowed: ["excluded", "late", "lower-priority", "unknown-priority", "allowed-b", "allowed-a"]);

        Assert.Equal(new[] { "allowed-a", "allowed-b" }, FoodDispatchCandidateConstraints.Eligible(trace).Select(x => x.기사Id));
    }

    [Fact]
    public void Constraints_자료비최신에서는수요우선순위를추정하지않는다()
    {
        var trace = Trace(rows: [Row("allowed-a", priority: 1), Row("allowed-b", priority: null)], current: false);
        Assert.Equal(2, FoodDispatchCandidateConstraints.Eligible(trace).Count);
    }

    [Fact]
    public void Constraints_최신자료의기준우선순위가미확인이면기준만남긴다()
    {
        var trace = Trace(rows: [Row("allowed-a", priority: null), Row("allowed-b", priority: 1)]);
        Assert.Equal("allowed-a", Assert.Single(FoodDispatchCandidateConstraints.Eligible(trace)).기사Id);
    }

    [Fact]
    public void Constraints_상한에서도기준후보를보존한다()
    {
        var trace = Trace(rows: [Row("b"), Row("c"), Row("allowed-a")], allowed: ["b", "c", "allowed-a"]);
        Assert.Equal(new[] { "allowed-a", "b" }, FoodDispatchCandidateConstraints.Eligible(trace, 2).Select(x => x.기사Id));
    }

    [Fact]
    public void Constraints_중복또는기준후보누락을허용하지않는다()
    {
        Assert.Empty(FoodDispatchCandidateConstraints.Eligible(Trace(rows: [Row("allowed-a"), Row("allowed-a")])));
        Assert.Empty(FoodDispatchCandidateConstraints.Eligible(Trace(rows: [Row("allowed-b")])));
        Assert.Empty(FoodDispatchCandidateConstraints.Eligible(Trace(allowed: ["allowed-a", "allowed-a"])));
    }

    [Fact]
    public async Task SelectAsync_꺼짐이면호출없이기준을유지한다()
    {
        var handler = new RecordingHandler();
        var result = await Client(handler, new FoodDispatchOllamaOptions()).SelectAsync(Trace());
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "Disabled"), result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_잘못된설정이면호출하지않는다()
    {
        var handler = new RecordingHandler();
        var settings = Settings();
        settings.BaseUrl = "https://external.example/";
        var result = await Client(handler, settings).SelectAsync(Trace());
        Assert.Equal("InvalidConfiguration", result.StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_후보가없거나하나면호출하지않는다()
    {
        var handler = new RecordingHandler();
        var none = await Client(handler).SelectAsync(Trace() with { 선정후보 = null, 최종허용기사Ids = [] });
        var single = await Client(handler).SelectAsync(Trace(allowed: ["allowed-a"]));
        Assert.Equal(new FoodDispatchAISelection(null, "NoCandidates"), none);
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "SingleCandidate"), single);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_임시토큰선택을실제후보에대응시키고개인정보를보내지않는다()
    {
        var handler = new RecordingHandler();
        const string privateDriver = "PRIVATE-DRIVER-NEVER-SEND";
        var trace = Trace(rows: [Row(privateDriver), Row("allowed-b")], allowed: [privateDriver, "allowed-b"])
            with { 선정후보 = new 배차추천후보(privateDriver, 17m, "SECRET-ORDER-ADDRESS-NEVER-SEND") };
        var result = await Client(handler).SelectAsync(trace);

        Assert.Equal(new FoodDispatchAISelection("allowed-b", "Applied"), result);
        Assert.Equal("http://127.0.0.1:12681/api/chat", handler.Uri!.AbsoluteUri);
        Assert.Null(handler.Authorization);
        Assert.DoesNotContain(privateDriver, handler.Body!);
        Assert.DoesNotContain("SECRET-", handler.Body!);
        Assert.DoesNotContain("PRIVATE-AREA", handler.Body!);
        Assert.DoesNotContain("PRIVATE-SOURCE-REASON", handler.Body!);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal("qwen3:1.7b", request.RootElement.GetProperty("model").GetString());
        Assert.False(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(request.RootElement.GetProperty("think").GetBoolean());
        Assert.False(request.RootElement.TryGetProperty("tools", out _));
        Assert.Equal(64, request.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.False(request.RootElement.GetProperty("format").GetProperty("additionalProperties").GetBoolean());
        using var context = JsonDocument.Parse(request.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal("C1", context.RootElement.GetProperty("baselineToken").GetString());
        Assert.Equal(new[] { "C1", "C2" }, context.RootElement.GetProperty("candidates").EnumerateArray()
            .Select(x => x.GetProperty("candidateToken").GetString()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_탈락후보와늦은후보를모델의선택목록에서뺀다()
    {
        var handler = new RecordingHandler();
        var trace = Trace(rows: [Row("allowed-a"), Row("allowed-b"), Row("excluded", verdict: "DriverReceivingOff"), Row("late", pickup: 2)],
            allowed: ["allowed-a", "allowed-b", "excluded", "late"]);
        await Client(handler).SelectAsync(trace);
        using var request = JsonDocument.Parse(handler.Body!);
        var choices = request.RootElement.GetProperty("format").GetProperty("properties").GetProperty("candidateToken").GetProperty("enum");
        Assert.Equal(2, choices.GetArrayLength());
        Assert.DoesNotContain("excluded", handler.Body!);
        Assert.DoesNotContain("late", handler.Body!);
    }

    [Theory]
    [InlineData("{\"candidateToken\":\"C99\"}")]
    [InlineData("{\"candidateToken\":\"allowed-b\"}")]
    [InlineData("{\"candidateToken\":\"C2\",\"reason\":\"approved\"}")]
    [InlineData("{\"candidateToken\":\"C1\",\"candidateToken\":\"C2\"}")]
    [InlineData("{\"candidateToken\":null}")]
    [InlineData("{\"candidateToken\":2}")]
    [InlineData("{\"candidateToken\":\"\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("{\"candidateToken\":\"C2\",}")]
    public async Task SelectAsync_모델의허용되지않는내용은기준으로복귀한다(string content)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Envelope(content)));
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "InvalidResponse"), await Client(handler).SelectAsync(Trace()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"model\":\"other-model\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"candidateToken\\\":\\\"C2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":false,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"candidateToken\\\":\\\"C2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":1,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"candidateToken\\\":\\\"C2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":false,\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"candidateToken\\\":\\\"C2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"candidateToken\\\":\\\"C2\\\"}\",\"tool_calls\":[{}]}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"candidateToken\\\":\\\"C2\\\"}\",\"tool_calls\":{}}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":{}}}")]
    public async Task SelectAsync_불완전응답중복필드도구요청을거부한다(string raw)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(raw)));
        Assert.Equal("InvalidResponse", (await Client(handler).SelectAsync(Trace())).StatusCode);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("system")]
    [InlineData("tool")]
    [InlineData("")]
    [InlineData(null)]
    public async Task SelectAsync_선택내용이정상이어도Assistant역할이아니면거부한다(string? role)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(JsonSerializer.Serialize(new
        {
            model = "qwen3:1.7b", done = true,
            message = new { role, content = "{\"candidateToken\":\"C2\"}" }
        }))));
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "InvalidResponse"), await Client(handler).SelectAsync(Trace()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_역할없는선택응답을거부한다()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(JsonSerializer.Serialize(new
        {
            model = "qwen3:1.7b", done = true,
            message = new { content = "{\"candidateToken\":\"C2\"}" }
        }))));
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "InvalidResponse"), await Client(handler).SelectAsync(Trace()));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task SelectAsync_Http실패는내용을이용하지않고기준으로복귀한다(HttpStatusCode status)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent("SECRET-ERROR-BODY") }));
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "HttpFailure"), await Client(handler).SelectAsync(Trace()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_연결실패는자동재시도없이기준으로복귀한다()
    {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("SECRET-NETWORK-BODY"));
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "NetworkFailure"), await Client(handler).SelectAsync(Trace()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_초과응답은내용을파싱하지않는다()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Raw(new string('x', 65537))));
        Assert.Equal("ResponseTooLarge", (await Client(handler).SelectAsync(Trace())).StatusCode);
    }

    [Fact]
    public async Task SelectAsync_ContentLength가없어도응답읽기상한을지킨다()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new UnknownLengthContent(new string('x', 100000)) }));
        Assert.Equal("ResponseTooLarge", (await Client(handler).SelectAsync(Trace())).StatusCode);
    }

    [Fact]
    public async Task SelectAsync_자체기한이되면기준으로복귀한다()
    {
        var handler = new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Envelope();
        });
        var settings = Settings();
        settings.TimeoutSeconds = 1;
        Assert.Equal(new FoodDispatchAISelection("allowed-a", "Timeout"), await Client(handler, settings).SelectAsync(Trace()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_호출자취소는실패결과로숨기지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new RecordingHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Envelope();
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).SelectAsync(Trace(), cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SelectAsync_미리취소된호출은모델에전달하지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new RecordingHandler();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).SelectAsync(Trace(), cancellation.Token));
        Assert.Equal(0, handler.Calls);
    }

    private static FoodDispatchOllamaOptions Settings() => new() { Enabled = true };
    private static OllamaFoodDispatchCandidateAI Client(RecordingHandler handler, FoodDispatchOllamaOptions? settings = null)
        => new(new HttpClient(handler), Options.Create(settings ?? Settings()));

    private static 음식배달후보검토결과 Trace(IReadOnlyList<음식배달후보검토항목>? rows = null,
        IReadOnlyList<string>? allowed = null, bool current = true)
        => new(new 배차추천후보("allowed-a", 17m, "SECRET-ORDER-ADDRESS-NEVER-SEND"),
            "PRIVATE-AREA-KEY", ["PRIVATE-AREA-KEY"], rows ?? [Row("allowed-a"), Row("allowed-b")],
            allowed ?? ["allowed-a", "allowed-b"], current, false, false, null);

    private static 음식배달후보검토항목 Row(string id, string verdict = "CandidateAllowed", int pickup = 1, int? priority = 2)
        => new(id, 0, "PRIVATE-AREA-NAME", verdict, 0.8m, pickup, 2m, 17m, 10m, priority, "PRIVATE-SOURCE-REASON");

    private static HttpResponseMessage Envelope(string content = "{\"candidateToken\":\"C2\"}")
        => Raw(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true, message = new { role = "assistant", content } }));
    private static HttpResponseMessage Raw(string content)
        => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? response = null)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        public Uri? Uri { get; private set; }
        public object? Authorization { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization;
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
