using System.Net;
using System.Text;
using System.Text.Json;

namespace Ssalddel.DispatchTraining;

/// <summary>가짜 HTTP 봉투만 검사한다. 실제 모델·네트워크·누적 기록을 호출하거나 변경하지 않는다.</summary>
public static class DispatchTrainingShadowSelfChecks
{
    private const string Selection = "{\"candidateToken\":\"C1\"}";
    private static readonly DispatchTrainingShadowInput Input = new("C1",
        [new("C1", 10m, 1m, 5m), new("C2", 9m, 2m, 4m)]);

    public static async Task<IReadOnlyList<DispatchTrainingSelfCheckResult>> RunAsync(CancellationToken ct = default)
    {
        var results = new List<DispatchTrainingSelfCheckResult>();
        var valid = Envelope();
        await Check("shadow-completed-expected-model-compares-baseline", async () =>
        {
            using var handler = Handler(valid);
            using var shadow = new DispatchTrainingShadowAI(handler);
            var result = await shadow.CompareAsync(Input, ct);
            Require(result == new DispatchTrainingShadowResult("Applied", "C1", true, true));
            Require(handler.Calls == 1 && shadow.CallsUsed == 1);
        });
        await Check("shadow-completed-different-candidate-remains-disagreement", async () =>
        {
            using var shadow = new DispatchTrainingShadowAI(Handler(Envelope("{\"candidateToken\":\"C2\"}")));
            Require(await shadow.CompareAsync(Input, ct) == new DispatchTrainingShadowResult("Applied", "C2", true, false));
        });

        (string Name, string Body)[] invalidEnvelopes =
        [
            ("different-model", valid.Replace("qwen3:1.7b", "other-model", StringComparison.Ordinal)),
            ("missing-model", valid.Replace("\"model\":\"qwen3:1.7b\",", "", StringComparison.Ordinal)),
            ("null-model", valid.Replace("\"model\":\"qwen3:1.7b\"", "\"model\":null", StringComparison.Ordinal)),
            ("nonstring-model", valid.Replace("\"model\":\"qwen3:1.7b\"", "\"model\":12", StringComparison.Ordinal)),
            ("unfinished-response", valid.Replace("\"done\":true", "\"done\":false", StringComparison.Ordinal)),
            ("missing-done", valid.Replace("\"done\":true,", "", StringComparison.Ordinal)),
            ("null-done", valid.Replace("\"done\":true", "\"done\":null", StringComparison.Ordinal)),
            ("string-done", valid.Replace("\"done\":true", "\"done\":\"true\"", StringComparison.Ordinal)),
            ("nonassistant-role", valid.Replace("assistant", "user", StringComparison.Ordinal)),
            ("nonstring-role", valid.Replace("\"assistant\"", "42", StringComparison.Ordinal)),
            ("tool-call", valid.Replace("\"role\":", "\"tool_calls\":[{\"function\":{\"name\":\"dispatch\"}}],\"role\":", StringComparison.Ordinal)),
            ("tool-call-object", valid.Replace("\"role\":", "\"tool_calls\":{},\"role\":", StringComparison.Ordinal)),
            ("duplicate-envelope-property", valid.Replace("\"done\":true", "\"done\":false,\"done\":true", StringComparison.Ordinal)),
            ("duplicate-message-property", valid.Replace("\"role\":", "\"role\":\"user\",\"role\":", StringComparison.Ordinal)),
            ("duplicate-selection-property", Envelope("{\"candidateToken\":\"C2\",\"candidateToken\":\"C1\"}")),
            ("extra-selection-property", Envelope("{\"candidateToken\":\"C1\",\"reason\":\"invented\"}")),
            ("malformed-selection", Envelope("not-json")),
            ("null-selection", Envelope("null")),
            ("null-content", valid.Replace("\"content\":" + JsonSerializer.Serialize(Selection), "\"content\":null", StringComparison.Ordinal)),
            ("nonobject-envelope", "[]")
        ];
        foreach (var item in invalidEnvelopes)
            await Check("shadow-rejects-" + item.Name, async () =>
            {
                Require(item.Body != valid);
                using var handler = Handler(item.Body);
                using var shadow = new DispatchTrainingShadowAI(handler);
                Unavailable(await shadow.CompareAsync(Input, ct), "InvalidResponse");
                Require(handler.Calls == 1 && shadow.CallsUsed == 1);
            });

        foreach (var toolCalls in new[] { "null", "[]" })
            await Check("shadow-no-tools-" + (toolCalls == "null" ? "null" : "empty-array") + "-matches-chat-contract", async () =>
            {
                using var shadow = new DispatchTrainingShadowAI(Handler(valid.Replace("\"role\":", "\"tool_calls\":" + toolCalls + ",\"role\":", StringComparison.Ordinal)));
                Require(await shadow.CompareAsync(Input, ct) == new DispatchTrainingShadowResult("Applied", "C1", true, true));
            });
        await Check("shadow-unknown-candidate-is-not-comparable", async () =>
        {
            using var shadow = new DispatchTrainingShadowAI(Handler(Envelope("{\"candidateToken\":\"C3\"}")));
            Unavailable(await shadow.CompareAsync(Input, ct), "InvalidSelection");
        });
        await Check("shadow-declared-response-size-is-bounded", async () =>
        {
            using var shadow = new DispatchTrainingShadowAI(Handler(new string(' ', 64 * 1024 + 1)));
            Unavailable(await shadow.CompareAsync(Input, ct), "ResponseTooLarge");
        });
        await Check("shadow-stream-without-content-length-is-bounded", async () =>
        {
            using var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new UnknownLengthContent(Encoding.UTF8.GetBytes(new string(' ', 64 * 1024 + 1))) }));
            using var shadow = new DispatchTrainingShadowAI(handler);
            Unavailable(await shadow.CompareAsync(Input, ct), "ResponseTooLarge");
        });
        await Check("shadow-http-and-network-errors-are-not-comparisons", async () =>
        {
            using var httpHandler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            using var http = new DispatchTrainingShadowAI(httpHandler);
            Unavailable(await http.CompareAsync(Input, ct), "HttpFailure");
            using var networkHandler = new FakeHandler((_, _) => throw new HttpRequestException("Synthetic transport failure."));
            using var network = new DispatchTrainingShadowAI(networkHandler);
            Unavailable(await network.CompareAsync(Input, ct), "NetworkFailure");
        });
        await Check("shadow-caller-cancellation-before-send-does-not-use-budget", async () =>
        {
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            using var handler = Handler(valid);
            using var shadow = new DispatchTrainingShadowAI(handler);
            await RejectCanceled(() => shadow.CompareAsync(Input, canceled.Token));
            Require(handler.Calls == 0 && shadow.CallsUsed == 0);
        });
        await Check("shadow-caller-cancellation-during-send-propagates", async () =>
        {
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var handler = new FakeHandler((_, token) =>
            {
                caller.Cancel(); token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Linked cancellation was not propagated.");
            });
            using var shadow = new DispatchTrainingShadowAI(handler);
            await RejectCanceled(() => shadow.CompareAsync(Input, caller.Token));
            Require(handler.Calls == 1 && shadow.CallsUsed == 1);
        });
        await Check("shadow-noncaller-cancellation-is-timeout", async () =>
        {
            using var handler = new FakeHandler((_, _) => throw new TaskCanceledException("Synthetic deadline cancellation."));
            using var shadow = new DispatchTrainingShadowAI(handler);
            Unavailable(await shadow.CompareAsync(Input, ct), "Timeout");
        });
        await Check("shadow-single-or-invalid-input-does-not-call-model", async () =>
        {
            using var handler = Handler(valid);
            using var shadow = new DispatchTrainingShadowAI(handler);
            Unavailable(await shadow.CompareAsync(new("C1", [Input.Candidates[0]]), ct), "SingleCandidate");
            Unavailable(await shadow.CompareAsync(new("C1", [Input.Candidates[0], Input.Candidates[0]]), ct), "InvalidInput");
            Unavailable(await shadow.CompareAsync(new("C1", [Input.Candidates[0], new("C2", 1m, -1m, 1m)]), ct), "InvalidInput");
            Require(handler.Calls == 0 && shadow.CallsUsed == 0);
        });
        await Check("shadow-failed-response-uses-one-call-without-retry", async () =>
        {
            using var handler = Handler(valid.Replace("\"done\":true", "\"done\":false", StringComparison.Ordinal));
            using var shadow = new DispatchTrainingShadowAI(handler);
            for (var index = 0; index < DispatchTrainingShadowAI.SessionCallLimit; index++)
                Unavailable(await shadow.CompareAsync(Input, ct), "InvalidResponse");
            Unavailable(await shadow.CompareAsync(Input, ct), "CallBudgetExhausted");
            Require(handler.Calls == DispatchTrainingShadowAI.SessionCallLimit && shadow.CallsUsed == handler.Calls);
        });
        return results;

        async Task Check(string name, Func<Task> action)
        {
            ct.ThrowIfCancellationRequested();
            try { await action(); results.Add(new(name, true, null)); }
            catch (Exception error) when (error is not OutOfMemoryException && !ct.IsCancellationRequested)
            { results.Add(new(name, false, error.GetType().Name + ": " + error.Message)); }
        }
    }

    private static string Envelope(string selection = Selection) => JsonSerializer.Serialize(new
    {
        model = DispatchTrainingShadowAI.Model, done = true,
        message = new { role = "assistant", content = selection }
    });

    private static FakeHandler Handler(string body) => new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    private static void Unavailable(DispatchTrainingShadowResult result, string code) =>
        Require(result == new DispatchTrainingShadowResult(code, null, false));

    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Shadow response contract invariant failed."); }

    private static async Task RejectCanceled(Func<Task<DispatchTrainingShadowResult>> action)
    {
        try { await action(); throw new InvalidOperationException("Caller cancellation was ignored."); }
        catch (OperationCanceledException) { }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Require(request.Method == HttpMethod.Post && request.RequestUri == new Uri("http://127.0.0.1:12681/api/chat"));
            return send(request, cancellationToken);
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
