using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Services.Content;
using 홍달.Services.Sales;

namespace Hongdal.Tests.Services.Sales;

public sealed class 판매페이지ServiceTests
{
    [Fact]
    public async Task 직접입력_판매페이지는_공동주문과_분리된_판매자초안으로_저장된다()
    {
        var store = new MemoryStore();
        var amazon = new StubAmazonResearchService();
        var service = new 판매페이지Service(store, amazon);

        var result = await service.초안생성Async(new 판매페이지초안생성요청
        {
            판매자유형 = 판매자유형코드.농가생산자,
            판매자표시명 = "햇살농원",
            상품명 = "당일 수확 복숭아",
            한줄소개 = "아침에 수확해 보내는 복숭아",
            판매가 = 32_000,
            통화코드 = "krw",
            개별주문허용 = true,
            공동주문허용 = true,
            공동주문최소수량 = 10
        }, "seller-1", CancellationToken.None);

        Assert.Equal(판매자유형코드.농가생산자, result.판매자유형);
        Assert.True(result.개별주문허용);
        Assert.True(result.공동주문허용);
        Assert.Equal(10, result.공동주문최소수량);
        Assert.Equal(32_000, result.판매가);
        Assert.Equal("KRW", result.통화코드);
        Assert.Null(result.외부참고자료);
        Assert.Null(result.연결된판매상품Id);
        Assert.Contains("입고상품", result.판매준비안내);
        Assert.Equal(0, amazon.CallCount);
    }

    [Fact]
    public async Task Amazon_관측가격은_Hongdal_판매가로_자동전환되지_않는다()
    {
        var store = new MemoryStore();
        var amazon = new StubAmazonResearchService(CreateAmazonReference());
        var service = new 판매페이지Service(store, amazon);

        var result = await service.초안생성Async(new 판매페이지초안생성요청
        {
            판매자유형 = 판매자유형코드.수출업자,
            판매자표시명 = "글로벌 공급자",
            Amazon상품Url = "https://www.amazon.com/dp/B0CLWNBWVT",
            개별주문허용 = true,
            공동주문허용 = false
        }, "exporter-1", CancellationToken.None);

        Assert.Equal("Amazon 참고 상품", result.상품명);
        Assert.Null(result.판매가);
        Assert.NotNull(result.외부참고자료);
        Assert.Equal(18.99m, result.외부참고자료!.관측가격);
        Assert.Equal("USD", result.외부참고자료.관측통화코드);
        Assert.Contains("자동", result.외부참고자료.안내문);
        Assert.Single(result.이미지Url목록);
        Assert.Equal(1, amazon.CallCount);
    }

    [Fact]
    public async Task 주문방식을_하나도_선택하지_않으면_초안을_만들지_않는다()
    {
        var service = new 판매페이지Service(new MemoryStore(), new StubAmazonResearchService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.초안생성Async(
            new 판매페이지초안생성요청
            {
                판매자표시명 = "판매자",
                상품명 = "상품",
                개별주문허용 = false,
                공동주문허용 = false
            },
            "seller-1",
            CancellationToken.None));

        Assert.Contains("하나 이상", exception.Message);
    }

    private static Amazon상품참고자료Dto CreateAmazonReference()
        => new(
            "amazon:us:b0clwnbwvt",
            "B0CLWNBWVT",
            "Amazon 참고 상품",
            "Sample Brand",
            "https://www.amazon.com/dp/B0CLWNBWVT",
            "US",
            new 외부상품가격스냅샷Dto(18.99m, 21.99m, null, "USD"),
            true,
            "In stock",
            4.3m,
            22,
            "Grocery",
            "https://images.example.test/product.jpg",
            ["https://images.example.test/product.jpg"],
            ["첫 번째 특징"],
            [new 외부상품속성Dto("Brand", "Sample Brand")],
            new DateTime(2026, 7, 17, 1, 2, 3, DateTimeKind.Utc),
            외부상품참고자료검수상태코드.대기,
            new Dictionary<string, string>(),
            "참고자료");

    private sealed class StubAmazonResearchService(Amazon상품참고자료Dto? result = null) : IAmazon상품참고자료Service
    {
        public int CallCount { get; private set; }

        public Task<Amazon상품참고자료Dto> 미리보기Async(
            Amazon상품참고자료조회요청Dto 요청,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result ?? throw new InvalidOperationException("Amazon service should not be called."));
        }
    }

    private sealed class MemoryStore : I판매페이지초안저장소
    {
        private readonly Dictionary<string, 판매페이지초안저장모델> _items = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<판매페이지초안저장모델>> 목록Async(
            string ownerUserId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<판매페이지초안저장모델>>(
                _items.Values.Where(x => x.소유자UserId == ownerUserId).ToArray());

        public Task<판매페이지초안저장모델?> 조회Async(
            string pageId,
            string ownerUserId,
            CancellationToken cancellationToken)
            => Task.FromResult(_items.TryGetValue(pageId, out var item) && item.소유자UserId == ownerUserId ? item : null);

        public Task<판매페이지초안저장모델> 저장Async(
            판매페이지초안저장모델 model,
            long expectedRevision,
            CancellationToken cancellationToken)
        {
            model.Revision = expectedRevision + 1;
            _items[model.페이지Id] = model;
            return Task.FromResult(model);
        }
    }
}
