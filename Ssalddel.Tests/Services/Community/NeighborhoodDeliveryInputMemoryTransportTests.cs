using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Middleware;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodDeliveryInputMemoryTransportTests
{
    [Fact]
    public async Task 이중Base64크기의입력은메모리에서만재조회된다()
    {
        // No actual contact/address data. Comparable size to the largest protected workbook envelope.
        var context = Context("{\"padding\":\"" + new string('A', 3_800_000) + "\"}");
        var called = false;
        var middleware = new IsmsPEncryptedTransportMiddleware(async ctx =>
        {
            called = true;
            var buffer = Assert.IsType<FileBufferingReadStream>(ctx.Request.Body);
            Assert.True(buffer.InMemory);
            Assert.Null(buffer.TempFileName);
            Assert.Equal(0, buffer.Position);
            using var reader = new StreamReader(buffer, leaveOpen: true);
            Assert.Equal(context.Request.ContentLength, Encoding.UTF8.GetByteCount(await reader.ReadToEndAsync()));
        }, NullLogger<IsmsPEncryptedTransportMiddleware>.Instance);
        await middleware.InvokeAsync(context, null!, null!);
        Assert.True(called);
        await context.Request.Body.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 알려진크기와스트리밍과대입력은파일없이413으로거절된다(bool knownLength)
    {
        var context = Context(new string('A', 4 * 1024 * 1024 + 1));
        if (!knownLength) context.Request.ContentLength = null;
        var middleware = new IsmsPEncryptedTransportMiddleware(_ => throw new InvalidOperationException("Must not proceed"),
            NullLogger<IsmsPEncryptedTransportMiddleware>.Instance);
        await middleware.InvokeAsync(context, null!, null!);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        if (context.Request.Body is FileBufferingReadStream buffered)
        {
            Assert.True(buffered.InMemory);
            Assert.Null(buffered.TempFileName);
        }
        await context.Request.Body.DisposeAsync();
    }

    private static DefaultHttpContext Context(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/" + NeighborhoodDeliveryRoutes.Api + "/input-preview";
        context.Request.ContentType = "application/json";
        // Non-seekable request exercises the real buffering path, rather than MemoryStream's bypass.
        context.Request.Body = new NonSeekableInput(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class NonSeekableInput(byte[] bytes) : Stream
    {
        private readonly MemoryStream _source = new(bytes, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _source.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
    }
}
