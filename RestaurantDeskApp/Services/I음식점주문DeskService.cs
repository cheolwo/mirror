using RestaurantDeskApp.Models.Restaurant;

namespace RestaurantDeskApp.Services;

public interface I음식점주문DeskService
{
    Task<IReadOnlyList<음식점주문DeskItem>> 주문목록조회Async(
        음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회,
        CancellationToken cancellationToken = default);

    Task<음식점주문DeskItem?> 주문조회Async(
        string 주문번호,
        음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회,
        CancellationToken cancellationToken = default);

    Task<음식점주문DeskItem> 주문알림수신Async(음식점주문수신Payload payload, CancellationToken cancellationToken = default);

    Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, CancellationToken cancellationToken = default);

    Task<음식점주문수락결과> 주문수락후전표준비Async(
        string 주문번호,
        int 조리예상분,
        CancellationToken cancellationToken = default);

    Task<음식점주문DeskItem?> 주문거절Async(
        string 주문번호,
        string 사유,
        CancellationToken cancellationToken = default);

    Task<음식점주문DeskItem?> 조리시작Async(
        string 주문번호, int 조리예상분, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("조리 시작을 지원하지 않는 음식점 서비스입니다.");

    Task<음식점주문DeskItem?> 조리시간변경Async(
        string 주문번호,
        int 조리예상분,
        CancellationToken cancellationToken = default);

    Task<음식점주문DeskItem?> 픽업준비완료Async(
        string 주문번호,
        CancellationToken cancellationToken = default);

    Task 전표출력완료Async(string 주문번호, CancellationToken cancellationToken = default);

    // 전표 재출력은 주문 수락/조리 명령을 다시 실행하지 않는다.
    Task<음식점주문수락결과> 전표준비Async(string 주문번호, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("전표 재출력을 지원하지 않는 음식점 서비스입니다.");

    Task 전표출력요청기록Async(string 주문번호, CancellationToken cancellationToken = default)
        => 전표출력완료Async(주문번호, cancellationToken);
}
