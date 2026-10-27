using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class OllamaCargoDispatchCandidateAITests
{
    private const string DriverA = "private-driver-a-8e27";
    private const string DriverB = "private-driver-b-91ef";
    private const string PrivateReason = "서울 가상로 123 701호 · 010-9876-1234 · private@example.invalid";
    private static readonly DateTime Arrival = new(2026, 10, 21, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 유효한C2응답은검증된두번째기사로매핑하고실제식별자와주소를전송하지않는다()
    {
        using var handler = new Handler(_ => Raw(Envelope("""{"candidateToken":"C2"}""")));
        using var http = new HttpClient(handler);
        var settings = Settings();
        var ai = Client(http, settings);
        var result = await ai.SelectAsync(Trace(Row(DriverA), Row(DriverB, aging: 8m, distance: 2m, minutes: 3)));

        Assert.Equal(DriverB, result.DriverId);
        Assert.Equal("Applied", result.StatusCode);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("http://127.0.0.1:12681/api/chat", handler.Uri?.AbsoluteUri);
        Assert.DoesNotContain(DriverA, handler.Body!);
        Assert.DoesNotContain(DriverB, handler.Body!);
        Assert.DoesNotContain(PrivateReason, handler.Body!);
        Assert.DoesNotContain("010-9876", handler.Body!);
        Assert.DoesNotContain("37.581", handler.Body!);
        using var request = JsonDocument.Parse(handler.Body!);
        var body = request.RootElement;
        Assert.Equal(CargoDispatchOllamaOptions.AllowedModel, body.GetProperty("model").GetString());
        Assert.False(body.GetProperty("stream").GetBoolean());
        Assert.False(body.GetProperty("think").GetBoolean());
        var schema = body.GetProperty("format");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "C1", "C2" }, schema.GetProperty("properties").GetProperty("candidateToken")
            .GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        using var facts = JsonDocument.Parse(body.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal("C1", facts.RootElement.GetProperty("baselineToken").GetString());
        var candidates = facts.RootElement.GetProperty("candidates");
        Assert.Equal(2, candidates.GetArrayLength());
        Assert.Equal(0d, candidates[0].GetProperty("arrivalDifferenceMinutes").GetDouble());
        Assert.Equal(3d, candidates[1].GetProperty("arrivalDifferenceMinutes").GetDouble());
        Assert.Equal(8m, candidates[1].GetProperty("agingScore").GetDecimal());
        Assert.Equal(2m, candidates[1].GetProperty("distanceKm").GetDecimal());
    }

    [Theory]
    [InlineData("""{"candidateToken":"C1"}""", "Applied")]
    [InlineData("""{"candidateToken":"C99"}""", "InvalidResponse")]
    [InlineData("""{"candidateToken":"c2"}""", "InvalidResponse")]
    [InlineData("""{"candidateToken":"C2 "}""", "InvalidResponse")]
    [InlineData("""{"candidateToken":"private-driver-b-91ef"}""", "InvalidResponse")]
    [InlineData("""{"candidateToken":2}""", "InvalidResponse")]
    [InlineData("""{"candidateToken":"C2","reason":"invented"}""", "InvalidResponse")]
    [InlineData("""{"candidateToken":"C1","candidateToken":"C2"}""", "InvalidResponse")]
    [InlineData("""{"CandidateToken":"C2"}""", "InvalidResponse")]
    [InlineData("[]", "InvalidResponse")]
    [InlineData("null", "InvalidResponse")]
    [InlineData("{broken", "InvalidResponse")]
    public async Task 내용은정확한단일토큰스키마만수락하고실패하면기준기사를유지한다(string content, string expected)
    {
        using var handler = new Handler(_ => Raw(Envelope(content)));
        using var http = new HttpClient(handler);
        var result = await Client(http).SelectAsync(Trace(Row(DriverA), Row(DriverB)));
        Assert.Equal(expected, result.StatusCode);
        Assert.Equal(DriverA, result.DriverId);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("model-other")]
    [InlineData("model-missing")]
    [InlineData("done-false")]
    [InlineData("done-missing")]
    [InlineData("done-string")]
    [InlineData("role-user")]
    [InlineData("tools-nonempty")]
    [InlineData("tools-object")]
    [InlineData("duplicate-root")]
    public async Task 다른모델과미완료도구호출중복봉투를성공으로기록하지않는다(string mutation)
    {
        var envelope = JsonNode.Parse(Envelope("""{"candidateToken":"C2"}"""))!;
        switch (mutation)
        {
            case "model-other": envelope["model"] = "another-model"; break;
            case "model-missing": envelope.AsObject().Remove("model"); break;
            case "done-false": envelope["done"] = false; break;
            case "done-missing": envelope.AsObject().Remove("done"); break;
            case "done-string": envelope["done"] = "true"; break;
            case "role-user": envelope["message"]!["role"] = "user"; break;
            case "tools-nonempty": envelope["message"]!["tool_calls"] = new JsonArray(new JsonObject()); break;
            case "tools-object": envelope["message"]!["tool_calls"] = new JsonObject(); break;
        }
        var raw = mutation == "duplicate-root" ? envelope.ToJsonString().Insert(1, "\"done\":false,") : envelope.ToJsonString();
        using var handler = new Handler(_ => Raw(raw));
        using var http = new HttpClient(handler);
        var result = await Client(http).SelectAsync(Trace(Row(DriverA), Row(DriverB)));
        Assert.Equal("InvalidResponse", result.StatusCode);
        Assert.Equal(DriverA, result.DriverId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task HTTP실패에서는성공처럼보이는본문도선택하지않는다(HttpStatusCode status)
    {
        using var handler = new Handler(_ => new HttpResponseMessage(status)
            { Content = new StringContent(Envelope("""{"candidateToken":"C2"}""")) });
        using var http = new HttpClient(handler);
        var result = await Client(http).SelectAsync(Trace(Row(DriverA), Row(DriverB)));
        Assert.Equal("HttpFailure", result.StatusCode);
        Assert.Equal(DriverA, result.DriverId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 헤더나실제스트림이64KiB를넘으면본문선택을거부한다(bool lengthHeader)
    {
        var bytes = Encoding.UTF8.GetBytes(Envelope("""{"candidateToken":"C2"}""") + new string(' ', 65537));
        using var handler = new Handler(_ =>
        {
            HttpContent content = lengthHeader ? new ByteArrayContent(bytes) : new StreamContent(new NonSeekableStream(bytes));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var http = new HttpClient(handler);
        var result = await Client(http).SelectAsync(Trace(Row(DriverA), Row(DriverB)));
        Assert.Equal("ResponseTooLarge", result.StatusCode);
        Assert.Equal(DriverA, result.DriverId);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("io")]
    [InlineData("timeout")]
    public async Task 전송과읽기실패는한번호출후규칙으로복귀한다(string kind)
    {
        using var handler = new Handler((_, _) => Task.FromException<HttpResponseMessage>(kind switch
        {
            "http" => new HttpRequestException("Synthetic network failure."),
            "io" => new IOException("Synthetic stream failure."),
            _ => new OperationCanceledException("Synthetic deadline cancellation.")
        }));
        using var http = new HttpClient(handler);
        var result = await Client(http).SelectAsync(Trace(Row(DriverA), Row(DriverB)));
        Assert.Equal(kind == "timeout" ? "Timeout" : "NetworkFailure", result.StatusCode);
        Assert.Equal(DriverA, result.DriverId);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("disabled", "Disabled")]
    [InlineData("single", "SingleCandidate")]
    [InlineData("none", "NoCandidates")]
    [InlineData("invalid", "InvalidConfiguration")]
    public async Task 비활성단일후보후보없음잘못된설정은HTTP를호출하지않는다(string kind, string expected)
    {
        var settings = Settings();
        if (kind == "disabled") settings.Enabled = false;
        if (kind == "invalid") settings.BaseUrl = "https://example.invalid/";
        var trace = kind == "single" ? Trace(Row(DriverA)) : Trace(Row(DriverA), Row(DriverB));
        if (kind == "none") trace = trace with { 선정후보 = null };
        using var handler = new Handler(_ => throw new InvalidOperationException("No network call expected."));
        using var http = new HttpClient(handler);
        var result = await Client(http, settings).SelectAsync(trace);
        Assert.Equal(expected, result.StatusCode);
        Assert.Equal(trace.선정후보?.DriverId, result.DriverId);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task 서로다른어댑터도공유관문에서동시호출을기다리지않고Busy로복귀한다()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler((_, _) => { started.TrySetResult(); return reply.Task; });
        using var http = new HttpClient(handler);
        var settings = Settings();
        var gate = new CargoDispatchAIInferenceGate(Options.Create(settings));
        var first = Client(http, settings, gate).SelectAsync(Trace(Row(DriverA), Row(DriverB)));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = await Client(http, settings, gate).SelectAsync(Trace(Row(DriverA), Row(DriverB)));
        Assert.Equal("Busy", second.StatusCode);
        Assert.Equal(DriverA, second.DriverId);
        Assert.Equal(1, handler.Calls);
        reply.SetResult(Raw(Envelope("""{"candidateToken":"C2"}""")));
        Assert.Equal("Applied", (await first).StatusCode);
    }

    [Fact]
    public async Task 호출자취소는전파하고실패휴지기는만들지않지만시도예산은소모한다()
    {
        var settings = Settings(); settings.MaxCallsPerWindow = 2;
        var gate = new CargoDispatchAIInferenceGate(Options.Create(settings));
        using var cancellation = new CancellationTokenSource();
        using var handler = new Handler((_, token) =>
        {
            cancellation.Cancel(); token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Expected caller cancellation.");
        });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http, settings, gate)
            .SelectAsync(Trace(Row(DriverA), Row(DriverB)), cancellation.Token));
        using (var next = gate.TryAcquire(out var status))
        { Assert.NotNull(next); Assert.Equal("Acquired", status); }
        Assert.Null(gate.TryAcquire(out var exhausted));
        Assert.Equal("BudgetExhausted", exhausted);
    }

    [Fact]
    public async Task 이미취소된요청은HTTP와공유예산을사용하지않는다()
    {
        var settings = Settings(); settings.MaxCallsPerWindow = 1;
        var gate = new CargoDispatchAIInferenceGate(Options.Create(settings));
        using var handler = new Handler(_ => throw new InvalidOperationException("No call expected."));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http, settings, gate).SelectAsync(
            Trace(Row(DriverA), Row(DriverB)), new CancellationToken(true)));
        using var lease = gate.TryAcquire(out var status);
        Assert.NotNull(lease); Assert.Equal("Acquired", status); Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void 후보상한에서도기준을먼저두고기존목록순서를유지한다()
    {
        var a = Row(DriverA);
        var trace = Trace(Row("b"), Row("c"), a) with { 선정후보 = a.서버후보 };
        Assert.Equal(new[] { DriverA, "b" }, CargoDispatchCandidateConstraints.Eligible(trace, 2).Select(x => x.기사Id));
        Assert.Equal(3, CargoDispatchCandidateConstraints.Eligible(trace, 100).Count);
    }

    [Theory]
    [InlineData("excluded")]
    [InlineData("unlisted")]
    [InlineData("missing-server")]
    [InlineData("server-id")]
    [InlineData("score")]
    [InlineData("missing-score")]
    [InlineData("missing-distance")]
    [InlineData("negative-distance")]
    [InlineData("missing-aging")]
    [InlineData("negative-aging")]
    [InlineData("lower-aging")]
    [InlineData("missing-arrival")]
    public void 탈락하거나근거가빠진후보와기준보다대기점수가낮은후보를모델에제공하지않는다(string mutation)
    {
        var b = Row(DriverB);
        b = mutation switch
        {
            "excluded" => b with { 판정Code = "DriverReceivingOff" },
            "missing-server" => b with { 서버후보 = null },
            "server-id" => b with { 서버후보 = b.서버후보! with { DriverId = "other" } },
            "score" => b with { 추천점수 = 999m },
            "missing-score" => b with { 추천점수 = null },
            "missing-distance" => b with { 거리Km = null },
            "negative-distance" => b with { 거리Km = -1m },
            "missing-aging" => b with { 대기점수 = null },
            "negative-aging" => b with { 대기점수 = -1m },
            "lower-aging" => b with { 대기점수 = 4m },
            "missing-arrival" => b with { 픽업도착예상시각Utc = null },
            _ => b
        };
        var trace = Trace(Row(DriverA), b);
        if (mutation == "unlisted") trace = trace with { 최종허용기사Ids = [DriverA] };
        Assert.Equal(DriverA, Assert.Single(CargoDispatchCandidateConstraints.Eligible(trace)).기사Id);
    }

    [Theory]
    [InlineData("duplicate-row")]
    [InlineData("duplicate-allowed")]
    [InlineData("empty-id")]
    [InlineData("missing-baseline-row")]
    [InlineData("unverified-baseline")]
    public void 중복이나잘못된기준집합은전체를보류한다(string mutation)
    {
        var trace = Trace(Row(DriverA), Row(DriverB));
        trace = mutation switch
        {
            "duplicate-row" => Trace(Row(DriverA), Row(DriverA)),
            "duplicate-allowed" => trace with { 최종허용기사Ids = [DriverA, DriverA, DriverB] },
            "empty-id" => Trace(Row(DriverA), Row(" ")),
            "missing-baseline-row" => trace with { 후보목록 = [Row(DriverB)] },
            "unverified-baseline" => trace with { 후보목록 = [Row(DriverA) with { 픽업도착예상시각Utc = null }, Row(DriverB)] },
            _ => trace
        };
        Assert.Empty(CargoDispatchCandidateConstraints.Eligible(trace));
    }

    private static CargoDispatchOllamaOptions Settings() => new() { Enabled = true };
    private static OllamaCargoDispatchCandidateAI Client(HttpClient http, CargoDispatchOllamaOptions? settings = null, CargoDispatchAIInferenceGate? gate = null)
    { settings ??= Settings(); return new(http, Options.Create(settings), gate ?? new(Options.Create(settings))); }
    private static 용달운송후보검토결과 Trace(params 용달운송후보검토항목[] rows)
        => new(rows.FirstOrDefault()?.서버후보, rows, rows.Select(x => x.기사Id).ToArray(), null);
    private static 용달운송후보검토항목 Row(string id, decimal aging = 5m, decimal distance = 1m, int minutes = 0)
        => new(id, "CandidateAllowed", distance, 5m, aging, 10m, PrivateReason)
        { 픽업도착예상시각Utc = Arrival.AddMinutes(minutes), 서버후보 = new(id, 10m, PrivateReason)
            { 화물수행근거 = new(false, true, new(37.581m, 127.081m), Arrival.AddMinutes(-5), null, "SyntheticFixture") } };
    private static string Envelope(string content)
        => JsonSerializer.Serialize(new { model = CargoDispatchOllamaOptions.AllowedModel, done = true,
            message = new { role = "assistant", content } });
    private static HttpResponseMessage Raw(string raw)
        => new(HttpStatusCode.OK) { Content = new StringContent(raw, Encoding.UTF8, "application/json") };
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply;
        public Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : this((request, _) => Task.FromResult(reply(request))) { }
        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) => this.reply = reply;
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        public Uri? Uri { get; private set; }
        public HttpMethod? Method { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; Uri = request.RequestUri; Method = request.Method; Body = await request.Content!.ReadAsStringAsync(token); return await reply(request, token); }
    }
    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private int position;
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); var count = Math.Min(buffer.Length, bytes.Length - position); bytes.AsMemory(position, count).CopyTo(buffer); position += count; return ValueTask.FromResult(count); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
