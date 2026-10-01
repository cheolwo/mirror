using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp;

// 개발 화면 검토 전용. 운영 Client를 대체하거나 서버 장애 시 fallback으로 사용하지 않는다.
public sealed class 메뉴미리보기Client : I음식점메뉴ApiClient
{
    private readonly List<음식점메뉴관리응답> items =
    [new() { Id = 1, 메뉴명 = "따뜻한 제육 덮밥", 설명 = "한 그릇 든든하게, 매콤한 제육과 채소", 판매가 = 9500, 공개여부 = true, Revision = 1 },
     new() { Id = 2, 메뉴명 = "구수한 된장찌개", 설명 = "두부와 채소를 넣고 끓인 집밥 메뉴", 판매가 = 8000, 공개여부 = true, 품절여부 = true, Revision = 1 }];
    private readonly Dictionary<Guid, 음식점메뉴관리응답> requests = [];
    public Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<음식점메뉴관리응답>>(items.ToArray());
    public Task<음식점메뉴관리응답> 등록Async(음식점메뉴등록요청 request, CancellationToken cancellationToken = default)
    {
        if (requests.TryGetValue(request.클라이언트요청Id, out var previous)) return Task.FromResult(previous);
        var item = new 음식점메뉴관리응답 { Id = items.Count + 1, 메뉴명 = request.메뉴명, 설명 = request.설명, 판매가 = request.판매가, 대표이미지Url = request.대표이미지Url, 공개여부 = request.공개여부, 품절여부 = request.품절여부, Revision = 1 };
        items.Add(item); requests.Add(request.클라이언트요청Id, item); return Task.FromResult(item);
    }
    public Task<음식점메뉴관리응답> 수정Async(long menuId, 음식점메뉴수정요청 request, CancellationToken cancellationToken = default)
    {
        var item = items.Single(x => x.Id == menuId);
        if (item.Revision != request.예상Revision) throw new InvalidOperationException("판본 충돌");
        item.메뉴명 = request.메뉴명; item.설명 = request.설명; item.판매가 = request.판매가;
        item.대표이미지Url = request.대표이미지Url; item.공개여부 = request.공개여부; item.품절여부 = request.품절여부; item.Revision++;
        return Task.FromResult(item);
    }
}
