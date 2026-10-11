using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class Ollama음식권역배차AITests
{
    [Fact]
    public void 옵션기본값은비활성이고별도설정영역을사용한다()
    {
        Assert.False(new 음식권역배차OllamaOptions().Enabled);
        Assert.True(new 음식권역배차OllamaOptions().Validate());
        Assert.Equal("FoodRegionalDispatchOllama", 음식권역배차OllamaOptions.SectionName);
    }

    [Theory]
    [InlineData("http://127.0.0.1:12681/", true)]
    [InlineData("http://[::1]:12681/", true)]
    [InlineData("http://localhost:12681/", false)]
    [InlineData("https://127.0.0.1:12681/", false)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("http://127.0.0.1:12681/api/", false)]
    [InlineData("http://192.168.0.2:12681/", false)]
    [InlineData("http://user:password@127.0.0.1:12681/", false)]
    [InlineData("http://127.0.0.1:12681/?token=secret", false)]
    public void 명시적루프백HTTP포트검증을기존후보AI와공유한다(string endpoint, bool expected)
        => Assert.Equal(expected, new 음식권역배차OllamaOptions { BaseUrl = endpoint }.Validate());

    [Theory]
    [InlineData(0, 12, "qwen3:1.7b")]
    [InlineData(91, 12, "qwen3:1.7b")]
    [InlineData(10, 1, "qwen3:1.7b")]
    [InlineData(10, 31, "qwen3:1.7b")]
    [InlineData(10, 12, "different-model")]
    public void 모델과시간및대안상한을임의로넓히지못한다(int seconds, int plans, string model)
        => Assert.False(new 음식권역배차OllamaOptions { TimeoutSeconds = seconds, MaxPlans = plans, Model = model }.Validate());

    [Fact]
    public async Task 허용토큰을조합으로대응시키고개인정보와자유문구를전송하지않는다()
    {
        var handler = new Handler();
        var result = await Client(handler).선택Async(Review());
        Assert.Equal(new 음식권역배차AI선택("PRIVATE-PLAN-B", "Applied"), result);
        Assert.Equal("http://127.0.0.1:12681/api/chat", handler.Uri!.AbsoluteUri);
        Assert.Null(handler.Authorization);
        Assert.DoesNotContain("PRIVATE-", handler.Body!);
        Assert.DoesNotContain("37.59", handler.Body!);
        Assert.DoesNotContain("127.08", handler.Body!);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(body.RootElement.GetProperty("think").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("tools", out _));
        Assert.Equal(64, body.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.False(body.RootElement.GetProperty("format").GetProperty("additionalProperties").GetBoolean());
        using var context = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal("AdditionalParticipationNeeded", context.RootElement.GetProperty("demandAction").GetString());
        Assert.Equal(12, context.RootElement.GetProperty("expectedWorkCount").GetDecimal());
        Assert.Equal(new[] { "P1", "P2" }, context.RootElement.GetProperty("plans").EnumerateArray().Select(x => x.GetProperty("planToken").GetString()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 비활성또는잘못된설정은추론하지않는다()
    {
        var handler = new Handler();
        Assert.Equal("Disabled", (await Client(handler, new()).선택Async(Review())).StatusCode);
        Assert.Equal("InvalidConfiguration", (await Client(handler, new() { Enabled = true, BaseUrl = "http://external:1234/" }).선택Async(Review())).StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task 수요미확인또는대안부재나단일대안은추론하지않는다()
    {
        var handler = new Handler();
        var ai = Client(handler);
        Assert.Equal("DemandUnknown", (await ai.선택Async(Review() with { 수요자료최신 = false })).StatusCode);
        Assert.Equal("NoPlans", (await ai.선택Async(Review() with { 유효조합목록 = [] })).StatusCode);
        Assert.Equal("SinglePlan", (await ai.선택Async(Review() with { 유효조합목록 = [Plan("A")] })).StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("invalid-action")]
    [InlineData("negative-demand")]
    [InlineData("unknown-capacity")]
    [InlineData("no-driver")]
    [InlineData("excluded-plan")]
    [InlineData("duplicate-plan")]
    [InlineData("oversized-bundle")]
    [InlineData("too-many-plans")]
    [InlineData("negative-distance")]
    public async Task 잘못된집계나부적합또는중복조합을모델에보내지않는다(string variation)
    {
        var review = Review();
        review = variation switch
        {
            "invalid-action" => review with { 수요ActionCode = "PRIVATE-UNTRUSTED-TEXT" },
            "negative-demand" => review with { 예상업무량건수 = -1m },
            "unknown-capacity" => review with { 기존기사가용처리건수 = null },
            "no-driver" => review with { 제안허용기사수 = 0 },
            "excluded-plan" => review with { 유효조합목록 = [Plan("A") with { 조합가능여부 = false }, Plan("B")] },
            "duplicate-plan" => review with { 유효조합목록 = [Plan("A"), Plan("A")] },
            "oversized-bundle" => review with { 유효조합목록 = [Plan("A") with { 의뢰Ids = ["a", "b", "c"] }, Plan("B")] },
            "too-many-plans" => review with { 유효조합목록 = Enumerable.Range(0, 51).Select(x => Plan($"P-{x}")).ToArray() },
            _ => review with { 유효조합목록 = [Plan("A") with { 묶음내예상거리Km = -1m }, Plan("B")] }
        };
        var handler = new Handler();
        Assert.Equal("InvalidPlans", (await Client(handler).선택Async(review)).StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task 대안상한이후토큰은선택할수없고기준은첫번째로유지한다()
    {
        var handler = new Handler((_, _) => Task.FromResult(Envelope("{\"planToken\":\"P3\"}")));
        var review = Review() with { 유효조합목록 = [Plan("A"), Plan("B"), Plan("C")] };
        var result = await Client(handler, new() { Enabled = true, MaxPlans = 2 }).선택Async(review);
        Assert.Equal(new 음식권역배차AI선택("A", "InvalidResponse"), result);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal(2, request.RootElement.GetProperty("format").GetProperty("properties").GetProperty("planToken").GetProperty("enum").GetArrayLength());
    }

    [Theory]
    [InlineData("{\"planToken\":\"P99\"}")]
    [InlineData("{\"planToken\":\"PRIVATE-PLAN-B\"}")]
    [InlineData("{\"planToken\":\"P2\",\"reason\":\"safe\"}")]
    [InlineData("{\"planToken\":\"P1\",\"planToken\":\"P2\"}")]
    [InlineData("{\"planToken\":null}")]
    [InlineData("{\"planToken\":2}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    public async Task 허용되지않은응답은기준조합으로복귀한다(string response)
    {
        var handler = new Handler((_, _) => Task.FromResult(Envelope(response)));
        Assert.Equal(new 음식권역배차AI선택("PRIVATE-PLAN-A", "InvalidResponse"), await Client(handler).선택Async(Review()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"model\":\"other\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"planToken\\\":\\\"P2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":false,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"planToken\\\":\\\"P2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":false,\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"planToken\\\":\\\"P2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":true,\"message\":{\"role\":\"user\",\"content\":\"{\\\"planToken\\\":\\\"P2\\\"}\"}}")]
    [InlineData("{\"model\":\"qwen3:1.7b\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"planToken\\\":\\\"P2\\\"}\",\"tool_calls\":[{}]}}")]
    public async Task 모델불일치와미완료및중복필드도구호출을거부한다(string raw)
    {
        var handler = new Handler((_, _) => Task.FromResult(Raw(raw)));
        Assert.Equal("InvalidResponse", (await Client(handler).선택Async(Review())).StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task HTTP실패는자동재시도하지않는다(HttpStatusCode status)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        Assert.Equal("HttpFailure", (await Client(handler).선택Async(Review())).StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 네트워크실패는기준으로복귀하고자동재시도하지않는다()
    {
        var handler = new Handler((_, _) => throw new HttpRequestException("PRIVATE-ERROR"));
        Assert.Equal("NetworkFailure", (await Client(handler).선택Async(Review())).StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 응답크기는ContentLength가없어도제한한다(bool declared)
    {
        var handler = new Handler((_, _) => Task.FromResult(declared ? Raw(new string('x', 65537))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(new string('x', 100000)) }));
        Assert.Equal("ResponseTooLarge", (await Client(handler).선택Async(Review())).StatusCode);
    }

    [Fact]
    public async Task 모델기한은기준으로복귀한다()
    {
        var handler = new Handler(async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return Envelope(); });
        var settings = new 음식권역배차OllamaOptions { Enabled = true, TimeoutSeconds = 1 };
        Assert.Equal("Timeout", (await Client(handler, settings).선택Async(Review())).StatusCode);
    }

    [Fact]
    public async Task 호출자취소는모델실패로숨기지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new Handler(async (_, token) => { cancellation.Cancel(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return Envelope(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).선택Async(Review(), cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 미리취소되면모델호출은발생하지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new Handler();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).선택Async(Review(), cancellation.Token));
        Assert.Equal(0, handler.Calls);
    }

    private static Ollama음식권역배차AI Client(Handler handler, 음식권역배차OllamaOptions? settings = null)
        => new(new HttpClient(handler), Options.Create(settings ?? new() { Enabled = true }));
    private static 음식권역배차AI검토 Review() => new(true, "AdditionalParticipationNeeded", 12m, 7m, 2, 2, 4,
        [Plan("PRIVATE-PLAN-A"), Plan("PRIVATE-PLAN-B")]);
    private static 멀티배차조합후보 Plan(string key)
    {
        var job = new 픽업하차경로작업($"PRIVATE-ORDER-{key}", "PRIVATE-PICKUP-ADDRESS", new(37.59m, 127.08m), null,
            "PRIVATE-DROPOFF-ADDRESS", new(37.592m, 127.082m), null);
        return new(key, "PRIVATE-TYPE", [job], [job.의뢰Id], 0.5m, 0.7m, 2.4m, 84m,
            ["동일상차지", "PRIVATE-BADGE"], ["PRIVATE-MENU-OR-NOTE"], true, []);
    }
    private static HttpResponseMessage Envelope(string content = "{\"planToken\":\"P2\"}")
        => Raw(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true, message = new { role = "assistant", content } }));
    private static HttpResponseMessage Raw(string content)
        => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? response = null) : HttpMessageHandler
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
