using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using Hongdal.Services.External.Apify;

namespace Hongdal.Tests.Services.Content;

public sealed class Amazon상품참고자료ServiceTests
{
    [Fact]
    public async Task 미리보기Async_외부관측값을_검수대기원장참조로변환한다()
    {
        var client = new StubClient(new ApifyAmazon상품상세응답(
            "B0CLWNBWVT",
            "B0CLWNBWVT",
            "삼양 불닭 라면",
            "Samyang",
            "https://www.amazon.com/dp/B0CLWNBWVT",
            "US",
            new ApifyAmazon가격응답(35.95m, "USD"),
            new ApifyAmazon가격응답(39.95m, "USD"),
            new ApifyAmazon가격응답(0m, "USD"),
            true,
            "In Stock",
            4.5m,
            22,
            "Grocery > Noodles",
            "https://m.media-amazon.com/thumb.jpg",
            ["https://m.media-amazon.com/one.jpg"],
            ["Korean spicy noodles"],
            [new ApifyAmazon속성응답("Flavor", "Spicy")]));
        var sut = new Amazon상품참고자료Service(client);

        var result = await sut.미리보기Async(
            new Amazon상품참고자료조회요청Dto
            {
                상품Url = "https://www.amazon.com/dp/B0CLWNBWVT"
            },
            CancellationToken.None);

        Assert.Equal("amazon:us:b0clwnbwvt", result.참조키);
        Assert.Equal(외부상품참고자료검수상태코드.대기, result.검수상태);
        Assert.Equal("B0CLWNBWVT", result.원장외부참조["AmazonAsin"]);
        Assert.Equal("Apify", result.원장외부참조["SourceProvider"]);
        Assert.Equal(35.95m, result.가격.현재가격);
        Assert.Contains("자동 전환하지 말고", result.안내문, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 미리보기Async_Amazon검색Url은_외부호출전에거절한다()
    {
        var client = new StubClient(null);
        var sut = new Amazon상품참고자료Service(client);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.미리보기Async(
            new Amazon상품참고자료조회요청Dto
            {
                상품Url = "https://www.amazon.com/s?k=korean+ramen"
            },
            CancellationToken.None));

        Assert.Equal(0, client.CallCount);
    }

    private sealed class StubClient : IApifyAmazonProductClient
    {
        private readonly ApifyAmazon상품상세응답? _response;

        public StubClient(ApifyAmazon상품상세응답? response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }

        public Task<ApifyAmazon상품상세응답?> 상품상세조회Async(
            Uri 상품Url,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_response);
        }
    }
}
