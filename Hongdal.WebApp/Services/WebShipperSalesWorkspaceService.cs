using Hongdal.Contracts.Common.Sales;

namespace Hongdal.WebApp.Services;

public sealed class WebShipperSalesWorkspaceService
{
    private readonly List<판매채널계정항목응답> _accounts =
    [
        new()
        {
            Id = 1,
            채널종류 = CommerceChannelKeys.SmartStore,
            상점명 = "살뜰 샘플 스토어",
            연결상태 = "연결됨",
            마지막동기화일시 = DateTime.Now.AddMinutes(-42)
        },
        new()
        {
            Id = 2,
            채널종류 = CommerceChannelKeys.Coupang,
            상점명 = "살뜰 풀필먼트",
            연결상태 = "인증확인필요",
            마지막동기화일시 = DateTime.Now.AddHours(-5)
        }
    ];

    private readonly List<판매상품항목응답> _products =
    [
        new()
        {
            Id = 10,
            입고상품Id = 5001,
            대표상품명 = "냉장 간편식 세트",
            판매SKU = "FOOD-SET-001",
            판매가 = 32900,
            상태 = "판매준비",
            샘플데이터여부 = true,
            샘플데이터코드 = "web-food-set"
        },
        new()
        {
            Id = 11,
            입고상품Id = 5002,
            대표상품명 = "접이식 캠핑 테이블",
            판매SKU = "CAMP-TABLE-240",
            판매가 = 59000,
            상태 = "출품중",
            샘플데이터여부 = true,
            샘플데이터코드 = "web-camp-table"
        }
    ];

    private readonly List<채널출품항목응답> _listings =
    [
        new()
        {
            Id = 100,
            판매상품Id = 11,
            판매채널계정Id = 1,
            채널상품번호 = "ST-240-001",
            출품상태 = "출품중",
            동기화상태 = "동기화완료"
        }
    ];

    private long _nextAccountId = 3;
    private long _nextProductId = 12;
    private long _nextListingId = 101;

    public IReadOnlyList<(string Key, string Name, string Status)> SupportedChannels { get; } =
    [
        (CommerceChannelKeys.SmartStore, "네이버 스마트스토어", "API 연동 준비"),
        (CommerceChannelKeys.Coupang, "쿠팡 Wing", "API 연동 준비"),
        (CommerceChannelKeys.Shopify, "Shopify", "수출 채널 후보"),
        (CommerceChannelKeys.Amazon, "Amazon", "수출 채널 후보")
    ];

    public Task<판매채널계정목록응답> GetAccountsAsync()
        => Task.FromResult(new 판매채널계정목록응답 { Items = _accounts.ToArray() });

    public Task<판매채널계정항목응답> CreateAccountAsync(판매채널계정저장요청 request)
    {
        var account = new 판매채널계정항목응답
        {
            Id = _nextAccountId++,
            채널종류 = string.IsNullOrWhiteSpace(request.채널종류) ? CommerceChannelKeys.SmartStore : request.채널종류,
            상점명 = string.IsNullOrWhiteSpace(request.상점명) ? "신규 판매채널" : request.상점명.Trim(),
            연결상태 = string.IsNullOrWhiteSpace(request.인증메모) ? "인증대기" : "연결됨",
            마지막동기화일시 = DateTime.Now
        };

        _accounts.Add(account);
        return Task.FromResult(account);
    }

    public Task<판매상품목록응답> GetProductsAsync()
        => Task.FromResult(new 판매상품목록응답 { Items = _products.ToArray() });

    public Task<판매상품항목응답> CreateProductAsync(판매상품저장요청 request)
    {
        var product = new 판매상품항목응답
        {
            Id = _nextProductId++,
            입고상품Id = request.입고상품Id,
            대표상품명 = string.IsNullOrWhiteSpace(request.대표상품명) ? "신규 판매상품" : request.대표상품명.Trim(),
            판매SKU = string.IsNullOrWhiteSpace(request.판매SKU) ? $"SKU-{DateTime.Now:HHmmss}" : request.판매SKU.Trim(),
            판매가 = Math.Max(0, request.판매가),
            상태 = "판매준비",
            샘플데이터여부 = request.샘플데이터여부,
            샘플데이터코드 = request.샘플데이터코드
        };

        _products.Add(product);
        return Task.FromResult(product);
    }

    public Task<채널출품목록응답> GetListingsAsync()
        => Task.FromResult(new 채널출품목록응답 { Items = _listings.ToArray() });

    public Task<채널출품항목응답> CreateListingAsync(채널출품저장요청 request)
    {
        var listing = new 채널출품항목응답
        {
            Id = _nextListingId++,
            판매상품Id = request.판매상품Id,
            판매채널계정Id = request.판매채널계정Id,
            채널상품번호 = $"WEB-{DateTime.Now:HHmmss}",
            출품상태 = "출품준비",
            동기화상태 = "수동동기화대기"
        };

        _listings.Add(listing);
        return Task.FromResult(listing);
    }
}
