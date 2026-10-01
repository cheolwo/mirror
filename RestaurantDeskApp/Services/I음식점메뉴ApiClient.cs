using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

public sealed class 메뉴저장거절Exception : Exception
{
    public 메뉴저장거절Exception() : base("서버가 메뉴 저장을 거절했습니다. 입력과 계정 권한을 확인하세요.") { }
}

public interface I음식점메뉴ApiClient
{
    Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(CancellationToken cancellationToken = default);
    Task<음식점메뉴관리응답> 등록Async(음식점메뉴등록요청 request, CancellationToken cancellationToken = default);
    Task<음식점메뉴관리응답> 수정Async(long menuId, 음식점메뉴수정요청 request, CancellationToken cancellationToken = default);
}
