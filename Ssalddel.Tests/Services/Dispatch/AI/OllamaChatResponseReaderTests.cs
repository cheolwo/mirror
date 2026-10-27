using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using 살뜰.Services.Dispatch.AI;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class OllamaChatResponseReaderTests
{
    private const string Model = "qwen3:1.7b";

    [Theory]
    [InlineData("")]
    [InlineData(",\"tool_calls\":null")]
    [InlineData(",\"tool_calls\":[]")]
    public async Task 완료된Assistant내용을그대로반환하고_모델본문은해석하지않는다(string tools)
    {
        const string content = "후보🙂 · JSON이 아닌 본문도 각 어댑터가 검사";
        using var response = Raw(Envelope(content, tools));
        var result = await OllamaChatResponseReader.ReadAsync(response, Model, default);
        Assert.Null(result.ErrorCode);
        Assert.Equal(content, result.Content);
    }

    [Theory]
    [InlineData("root-array")]
    [InlineData("model-missing")]
    [InlineData("model-number")]
    [InlineData("model-other")]
    [InlineData("done-false")]
    [InlineData("done-string")]
    [InlineData("message-array")]
    [InlineData("role-user")]
    [InlineData("role-missing")]
    [InlineData("content-number")]
    [InlineData("content-missing")]
    [InlineData("tools-nonempty")]
    [InlineData("tools-object")]
    public async Task 잘못된봉투는내용없이InvalidResponse를반환한다(string mutation)
    {
        JsonNode node = JsonNode.Parse(Envelope("body"))!;
        switch (mutation)
        {
            case "root-array": node = new JsonArray(); break;
            case "model-missing": node.AsObject().Remove("model"); break;
            case "model-number": node["model"] = 1; break;
            case "model-other": node["model"] = "other"; break;
            case "done-false": node["done"] = false; break;
            case "done-string": node["done"] = "true"; break;
            case "message-array": node["message"] = new JsonArray(); break;
            case "role-user": node["message"]!["role"] = "user"; break;
            case "role-missing": node["message"]!.AsObject().Remove("role"); break;
            case "content-number": node["message"]!["content"] = 2; break;
            case "content-missing": node["message"]!.AsObject().Remove("content"); break;
            case "tools-nonempty": node["message"]!["tool_calls"] = new JsonArray(new JsonObject()); break;
            case "tools-object": node["message"]!["tool_calls"] = new JsonObject(); break;
        }
        using var response = Raw(node.ToJsonString());
        var result = await OllamaChatResponseReader.ReadAsync(response, Model, default);
        Assert.Equal("InvalidResponse", result.ErrorCode);
        Assert.Null(result.Content);
    }

    [Theory]
    [InlineData("""{"model":"qwen3:1.7b","done":false,"done":true,"message":{"role":"assistant","content":"body"}}""")]
    [InlineData("""{"model":"qwen3:1.7b","done":true,"message":{"role":"user","role":"assistant","content":"body"}}""")]
    [InlineData("""{"model":"qwen3:1.7b","done":true,"message":{"role":"assistant","content":"body"},"other":[{"x":1,"x":2}]}""")]
    public async Task 봉투와중첩배열의중복키는Json오류로전달한다(string raw)
    {
        using var response = Raw(raw);
        await Assert.ThrowsAsync<JsonException>(() => OllamaChatResponseReader.ReadAsync(response, Model, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 잘못된문법과깊이16초과는Json오류로전달한다(bool tooDeep)
    {
        var raw = tooDeep ? new string('[', 17) + "0" + new string(']', 17) : "{incomplete-json";
        using var response = Raw(raw);
        await Assert.ThrowsAnyAsync<JsonException>(() => OllamaChatResponseReader.ReadAsync(response, Model, default));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task HTTP실패는본문과길이검사보다먼저반환한다(HttpStatusCode status)
    {
        var stream = new TrackingStream([1, 2, 3]);
        using var response = new HttpResponseMessage(status) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentLength = OllamaChatResponseReader.MaxResponseBytes + 1;
        var result = await OllamaChatResponseReader.ReadAsync(response, Model, default);
        Assert.Equal("HttpFailure", result.ErrorCode);
        Assert.Null(result.Content);
        Assert.Equal(0, stream.ReadCalls);
    }

    [Fact]
    public async Task 초과길이헤더는본문스트림을읽지않는다()
    {
        var stream = new TrackingStream([1, 2, 3]);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentLength = OllamaChatResponseReader.MaxResponseBytes + 1;
        var result = await OllamaChatResponseReader.ReadAsync(response, Model, default);
        Assert.Equal("ResponseTooLarge", result.ErrorCode);
        Assert.Null(result.Content);
        Assert.Equal(0, stream.ReadCalls);
    }

    [Theory]
    [InlineData(65536, null)]
    [InlineData(65537, "ResponseTooLarge")]
    [InlineData(100000, "ResponseTooLarge")]
    public async Task 길이없는부분스트림은_정확히64KiB를허용하고_판정용한바이트까지만읽는다(int length, string? error)
    {
        var body = Enumerable.Repeat((byte)' ', length).ToArray();
        Encoding.UTF8.GetBytes(Envelope("body")).CopyTo(body, 0);
        var stream = new TrackingStream(body, 137);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        Assert.Null(response.Content.Headers.ContentLength);
        var result = await OllamaChatResponseReader.ReadAsync(response, Model, default);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(error is null ? "body" : null, result.Content);
        Assert.Equal(Math.Min(length, 65537), stream.BytesRead);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task 실제보다작은길이헤더도_스트림읽기상한을우회하지못한다()
    {
        var stream = new TrackingStream(new byte[100000]);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentLength = 1;
        var result = await OllamaChatResponseReader.ReadAsync(response, Model, default);
        Assert.Equal("ResponseTooLarge", result.ErrorCode);
        Assert.Equal(65537, stream.BytesRead);
    }

    [Fact]
    public async Task 스트림읽기취소를잡아서응답코드로바꾸지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new TrackingStream([1]) { BeforeRead = cancellation.Cancel };
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => OllamaChatResponseReader.ReadAsync(response, Model, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, stream.ReadCalls);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task 스트림IO오류는caller의NetworkFailure처리에전달한다()
    {
        var failure = new IOException("synthetic read failure");
        var stream = new TrackingStream([1]) { ReadFailure = failure };
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        var error = await Assert.ThrowsAsync<IOException>(() => OllamaChatResponseReader.ReadAsync(response, Model, default));
        Assert.Same(failure, error);
        Assert.Equal(1, stream.ReadCalls);
        Assert.True(stream.Disposed);
    }

    private static string Envelope(string content, string tools = "")
        => "{\"model\":\"" + Model + "\",\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":"
            + JsonSerializer.Serialize(content) + tools + "}}";
    private static HttpResponseMessage Raw(string raw)
        => new(HttpStatusCode.OK) { Content = new StringContent(raw, Encoding.UTF8, "application/json") };

    private sealed class TrackingStream(byte[] body, int maximumChunk = 8192) : Stream
    {
        private int position;
        public int ReadCalls { get; private set; }
        public int BytesRead => position;
        public bool Disposed { get; private set; }
        public Action? BeforeRead { get; init; }
        public Exception? ReadFailure { get; init; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            BeforeRead?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadFailure is not null) return ValueTask.FromException<int>(ReadFailure);
            var count = Math.Min(buffer.Length, Math.Min(maximumChunk, body.Length - position));
            body.AsMemory(position, count).CopyTo(buffer);
            position += count;
            return ValueTask.FromResult(count);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
