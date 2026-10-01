using System.Net;
using System.Text;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class KamisJsonClientTests
{
    [Fact]
    public async Task GetDocumentAsync_성공응답을Json문서로반환한다()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"error_code":"000","item":[{"price":"1,000"}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var sut = new KamisJsonClient(client, NullLogger<KamisJsonClient>.Instance);

        using var document = await sut.GetDocumentAsync("price/list?p_returntype=json");

        Assert.Equal("000", document.RootElement.GetProperty("error_code").GetString());
        Assert.Equal("/price/list?p_returntype=json", handler.RequestUri?.PathAndQuery);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetDocumentAsync_재시도대상이아닌Http오류를즉시반환한다()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var sut = new KamisJsonClient(client, NullLogger<KamisJsonClient>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetDocumentAsync("price/list"));

        Assert.Contains("상태 코드=400", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
