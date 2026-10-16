using System.Net;
using System.Text;
using System.Text.Json;
using Ssalddel.DispatchTraining;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class DispatchTrainingShadowAITests
{
    [Theory]
    [InlineData("C1", true)]
    [InlineData("C2", false)]
    public async Task 유효선택만원래선택과비교하고로컬익명입력만보낸다(string token, bool expectedMatch)
    {
        var handler = new Handler((_, _) => Task.FromResult(Reply(token)));
        using var ai = new DispatchTrainingShadowAI(handler);
        var input = Input();

        var result = await ai.CompareAsync(input);

        Assert.Equal("Applied", result.StatusCode);
        Assert.True(result.Comparable);
        Assert.Equal(token, result.SelectedToken);
        Assert.Equal(expectedMatch, result.MatchesBaseline);
        Assert.Equal(1, ai.CallsUsed);
        Assert.Equal("http://127.0.0.1:12681/api/chat", handler.Uri?.AbsoluteUri);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("qwen3:1.7b", body.RootElement.GetProperty("model").GetString());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        var payload = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
        using var context = JsonDocument.Parse(payload!);
        Assert.Equal("C1", context.RootElement.GetProperty("baselineToken").GetString());
        Assert.Equal(new[] { "baselineToken", "candidates" }, context.RootElement.EnumerateObject().Select(x => x.Name));
        var first = context.RootElement.GetProperty("candidates")[0];
        Assert.Equal(new[] { "candidateToken", "score", "distanceKm", "agingScore", "pickupStage", "arrivalDifferenceMinutes", "demandPriority" },
            first.EnumerateObject().Select(x => x.Name));
        Assert.Equal("C1", input.BaselineToken);
        Assert.Equal("C1", input.Candidates[0].Token);
    }

    [Theory]
    [InlineData("{\"candidateToken\":\"C3\"}", "InvalidSelection")]
    [InlineData("{\"candidateToken\":\"C2\",\"reason\":\"extra\"}", "InvalidResponse")]
    [InlineData("{\"candidateToken\":\"C1\",\"candidateToken\":\"C2\"}", "InvalidResponse")]
    [InlineData("{\"candidateToken\":8}", "InvalidResponse")]
    [InlineData("[]", "InvalidResponse")]
    [InlineData("not-json", "InvalidResponse")]
    public async Task 잘못된토큰이나응답을기존선택일치로취급하지않는다(string content, string expected)
    {
        using var ai = new DispatchTrainingShadowAI(new Handler((_, _) => Task.FromResult(Envelope(content))));

        var result = await ai.CompareAsync(Input());

        Assert.Equal(expected, result.StatusCode);
        Assert.False(result.Comparable);
        Assert.Null(result.SelectedToken);
        Assert.Null(result.MatchesBaseline);
    }

    [Fact]
    public async Task 도구호출이나잘못된외부응답은비교하지않는다()
    {
        var responses = new[]
        {
            "{\"message\":{\"role\":\"assistant\",\"content\":\"{}\",\"tool_calls\":[{}]}}",
            "{\"message\":{\"role\":\"user\",\"content\":\"{}\"}}",
            "{\"message\":{},\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}",
            "{}"
        };
        foreach (var body in responses)
        {
            using var ai = new DispatchTrainingShadowAI(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body) })));
            var result = await ai.CompareAsync(Input());
            Assert.Equal("InvalidResponse", result.StatusCode);
            Assert.False(result.Comparable);
        }
    }

    [Fact]
    public async Task 복수후보가아니면모델을호출하지않는다()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("must not call"));
        using var ai = new DispatchTrainingShadowAI(handler);

        var result = await ai.CompareAsync(new("C1", [Input().Candidates[0]]));

        Assert.Equal("SingleCandidate", result.StatusCode);
        Assert.False(result.Comparable);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, ai.CallsUsed);
    }

    [Theory]
    [InlineData("private-driver-id")]
    [InlineData("C01")]
    [InlineData("C1000")]
    [InlineData("C1\n")]
    [InlineData("C1\naddress")]
    public async Task 익명토큰형식이아닌입력은전송하지않는다(string token)
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("must not call"));
        using var ai = new DispatchTrainingShadowAI(handler);

        var result = await ai.CompareAsync(new("C1", [Input().Candidates[0], new(token, 1m, 1m, 1m)]));

        Assert.Equal("InvalidInput", result.StatusCode);
        Assert.False(result.Comparable);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task 세션호출상한은동시비교에서도열다섯회다()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async (_, ct) => { await release.Task.WaitAsync(ct); return Reply("C1"); });
        using var ai = new DispatchTrainingShadowAI(handler);

        var pending = Enumerable.Range(0, 25).Select(_ => ai.CompareAsync(Input())).ToArray();
        Assert.Equal(15, handler.Calls);
        release.SetResult();
        var results = await Task.WhenAll(pending);

        Assert.Equal(15, handler.Calls);
        Assert.Equal(15, ai.CallsUsed);
        Assert.Equal(15, results.Count(x => x.Comparable));
        Assert.Equal(10, results.Count(x => x.StatusCode == "CallBudgetExhausted"));
        Assert.All(results.Where(x => !x.Comparable), x => Assert.Null(x.MatchesBaseline));
    }

    [Fact]
    public async Task 시간초과는비교불가이며다시호출하지않는다()
    {
        var handler = new Handler(async (_, ct) =>
        {
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, ct);
            return Reply("C1");
        });
        using var ai = new DispatchTrainingShadowAI(handler);

        var result = await ai.CompareAsync(Input());

        Assert.Equal("Timeout", result.StatusCode);
        Assert.False(result.Comparable);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 호출자취소는비교결과로숨기지않고전파한다()
    {
        var handler = new Handler((_, _) => Task.FromResult(Reply("C1")));
        using var ai = new DispatchTrainingShadowAI(handler);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ai.CompareAsync(Input(), cancelled.Token));

        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, ai.CallsUsed);
    }

    [Fact]
    public async Task 통신실패와HTTP실패도일치비교가아니다()
    {
        using var network = new DispatchTrainingShadowAI(new Handler((_, _) => throw new HttpRequestException("fixture")));
        using var http = new DispatchTrainingShadowAI(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var failedNetwork = await network.CompareAsync(Input());
        var failedHttp = await http.CompareAsync(Input());

        Assert.Equal("NetworkFailure", failedNetwork.StatusCode);
        Assert.Equal("HttpFailure", failedHttp.StatusCode);
        Assert.False(failedNetwork.Comparable);
        Assert.False(failedHttp.Comparable);
        Assert.Null(failedNetwork.MatchesBaseline);
        Assert.Null(failedHttp.MatchesBaseline);
    }

    [Fact]
    public async Task 과대한응답은비교하지않는다()
    {
        using var ai = new DispatchTrainingShadowAI(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(new string('x', 65537), Encoding.UTF8) })));

        var result = await ai.CompareAsync(Input());

        Assert.Equal("ResponseTooLarge", result.StatusCode);
        Assert.False(result.Comparable);
    }

    private static DispatchTrainingShadowInput Input() => new("C1",
        [new("C1", 100m, 1m, 3m, 0, 1m, 1), new("C2", 99m, 2m, 6m, 0, 2m, 1)]);

    private static HttpResponseMessage Reply(string token) => Envelope(JsonSerializer.Serialize(new { candidateToken = token }));
    private static HttpResponseMessage Envelope(string content) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { message = new { role = "assistant", content } }), Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public Uri? Uri { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            Uri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await response(request, cancellationToken);
        }
    }
}
