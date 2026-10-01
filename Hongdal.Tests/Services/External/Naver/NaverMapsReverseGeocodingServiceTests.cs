using System.Net;
using System.Text;
using Hongdal.Services.External.Naver;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.Naver;

public sealed class NaverMapsReverseGeocodingServiceTests
{
    [Fact]
    public async Task ResolveDistrictAsync_ReturnsOnlySidoAndSigungu()
    {
        var handler = new RecordingHandler("""
            {
              "status": { "code": 0, "name": "ok", "message": "done" },
              "results": [
                {
                  "name": "admcode",
                  "region": {
                    "area1": { "name": "서울특별시" },
                    "area2": { "name": "중랑구" },
                    "area3": { "name": "면목동" },
                    "area4": { "name": "" }
                  }
                }
              ]
            }
            """);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://maps.apigw.ntruss.com") };
        var service = new NaverMapsReverseGeocodingService(
            httpClient,
            Options.Create(new NaverMapsOptions
            {
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret"
            }));

        var result = await service.ResolveDistrictAsync(37.588m, 127.087m);

        Assert.NotNull(result);
        Assert.Equal("서울특별시", result.SidoName);
        Assert.Equal("중랑구", result.SigunguName);
        Assert.DoesNotContain("면목동", $"{result.SidoName} {result.SigunguName}");
        Assert.Contains("coords=127.087%2C37.588", handler.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders=admcode", handler.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.True(handler.SentApiKeyIdHeader);
        Assert.True(handler.SentApiKeyHeader);
    }

    [Fact]
    public async Task ResolveDistrictAsync_WithoutCredentials_DoesNotSendRequest()
    {
        var handler = new RecordingHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://maps.apigw.ntruss.com") };
        var service = new NaverMapsReverseGeocodingService(
            httpClient,
            Options.Create(new NaverMapsOptions()));

        var result = await service.ResolveDistrictAsync(37.588m, 127.087m);

        Assert.Null(result);
        Assert.Equal(0, handler.SendCount);
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public int SendCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public bool SentApiKeyIdHeader { get; private set; }
        public bool SentApiKeyHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            RequestUri = request.RequestUri;
            SentApiKeyIdHeader = request.Headers.Contains("x-ncp-apigw-api-key-id");
            SentApiKeyHeader = request.Headers.Contains("x-ncp-apigw-api-key");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
