using System.Net;
using System.Text;
using System.Text.Json;
using Hongdal.Services.External.Apify;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class ApifyAmazonProductClientTests
{
    [Fact]
    public async Task 상품상세조회Async_상품한건만요청하고_상세응답을정규화한다()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                [
                  {
                    "title": "Samyang Buldak Ramen Carbonara",
                    "url": "https://www.amazon.com/dp/B0CLWNBWVT",
                    "asin": "B0CLWNBWVT",
                    "originalAsin": "B0CLWNBWVT",
                    "price": { "value": 35.95, "currency": "USD" },
                    "listPrice": { "value": 39.95, "currency": "USD" },
                    "shippingPrice": { "value": 0, "currency": "USD" },
                    "inStock": true,
                    "inStockText": "In Stock",
                    "brand": "Samyang",
                    "stars": 4.5,
                    "reviewsCount": 22,
                    "breadCrumbs": "Grocery > Noodles",
                    "thumbnailImage": "https://m.media-amazon.com/thumb.jpg",
                    "highResolutionImages": [
                      "https://m.media-amazon.com/one.jpg",
                      "https://m.media-amazon.com/two.jpg"
                    ],
                    "features": ["Korean spicy noodles", "Pack of 10"],
                    "productOverview": [
                      { "key": "Flavor", "value": "Spicy" }
                    ],
                    "attributes": [
                      { "key": "ASIN", "value": "B0CLWNBWVT" }
                    ],
                    "loadedCountryCode": "US"
                  }
                ]
                """);
        });
        var sut = CreateClient(handler);

        var result = await sut.상품상세조회Async(
            new Uri("https://www.amazon.com/dp/B0CLWNBWVT"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("B0CLWNBWVT", result.Asin);
        Assert.Equal("Samyang", result.브랜드명);
        Assert.Equal(35.95m, result.현재가격.금액);
        Assert.Equal("USD", result.현재가격.통화코드);
        Assert.True(result.재고여부);
        Assert.Equal(3, result.이미지Url목록.Count);
        Assert.Equal(2, result.속성목록.Count);

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Contains(
            "actors/junglee~amazon-crawler/run-sync-get-dataset-items",
            capturedRequest.RequestUri!.AbsoluteUri,
            StringComparison.Ordinal);
        Assert.Contains("maxItems=1", capturedRequest.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("maxTotalChargeUsd=1", capturedRequest.RequestUri.Query, StringComparison.Ordinal);

        using var body = JsonDocument.Parse(capturedBody!);
        Assert.Equal(1, body.RootElement.GetProperty("maxItemsPerStartUrl").GetInt32());
        Assert.Equal(0, body.RootElement.GetProperty("maxOffers").GetInt32());
        Assert.False(body.RootElement.GetProperty("scrapeSellers").GetBoolean());
        Assert.Equal(
            "https://www.amazon.com/dp/B0CLWNBWVT",
            body.RootElement
                .GetProperty("categoryOrProductUrls")[0]
                .GetProperty("url")
                .GetString());
    }

    [Fact]
    public async Task 상품상세조회Async_가격과재고가없어도_상세참고자료를반환한다()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(JsonResponse(
            """
            [
              {
                "title": "가격 미관측 상품",
                "url": "https://www.amazon.com/dp/B0CLWNBWVT",
                "asin": "B0CLWNBWVT",
                "price": null,
                "listPrice": null,
                "inStock": false,
                "inStockText": "Currently unavailable.",
                "stars": 4.3,
                "reviewsCount": 22,
                "features": [],
                "attributes": []
              }
            ]
            """)));
        var sut = CreateClient(handler);

        var result = await sut.상품상세조회Async(
            new Uri("https://www.amazon.com/dp/B0CLWNBWVT"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.현재가격.금액);
        Assert.False(result.재고여부);
        Assert.Equal("Currently unavailable.", result.재고표시문구);
    }

    private static ApifyAmazonProductClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.apify.com/v2/")
        };
        var gateway = new ApifyActorGateway(httpClient, Options.Create(new ApifyOptions
        {
            Enabled = true,
            ApiToken = "test-token",
            AllowedActorIds = ["junglee~amazon-crawler"]
        }));
        return new ApifyAmazonProductClient(gateway, Options.Create(new ApifyAmazonOptions
        {
            Enabled = true
        }));
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _responseFactory(request);
    }
}
