using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;

namespace Ssalddel.Application.Food;

public interface I음식점음식주문조회UseCase
{
    음식점주문수신함응답 목록(음식점주문수신함조회요청 request, long 음식점Id);

    음식주문응답? 상세(string 주문번호, long 음식점Id);
}

/// <summary>
/// 로그인한 음식점 운영자의 음식점 범위에서 서버 원장을 다시 읽습니다.
/// 실시간 알림 유실이나 앱 재시작 뒤에도 이 조회가 수신함의 기준이 됩니다.
/// </summary>
public sealed class 음식점음식주문조회UseCase(
    ISsalddelFoodOrderStore orderStore) : I음식점음식주문조회UseCase
{
    public 음식점주문수신함응답 목록(음식점주문수신함조회요청 request, long 음식점Id)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (음식점Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(음식점Id));
        }

        var 처리상태 = 음식점주문수신함처리상태코드.Normalize(request.처리상태);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = orderStore.GetOrders().Items
            .Where(order => order.음식점Id == 음식점Id);

        if (request.UpdatedAfterUtc is { } updatedAfterUtc)
        {
            var normalizedUpdatedAfterUtc = updatedAfterUtc.Kind == DateTimeKind.Utc
                ? updatedAfterUtc
                : updatedAfterUtc.ToUniversalTime();
            query = query.Where(order => 최근변경시각(order) > normalizedUpdatedAfterUtc);
        }

        query = 처리상태 switch
        {
            음식점주문수신함처리상태코드.미처리 => query.Where(order =>
                음식점주문수신함처리상태코드.미처리여부(order.상태)),
            음식점주문수신함처리상태코드.완료 => query.Where(order =>
                !음식점주문수신함처리상태코드.미처리여부(order.상태)),
            _ => query
        };

        var ordered = query
            .OrderByDescending(최근변경시각)
            .ThenByDescending(order => order.CreatedAt)
            .ToArray();

        return new 음식점주문수신함응답
        {
            Items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(FoodOrderSampleData.Clone)
                .Select(음식배달가능행동Projector.음식점용)
                .ToArray(),
            TotalCount = ordered.Length,
            Page = page,
            PageSize = pageSize,
            ServerTimeUtc = DateTime.UtcNow
        };
    }

    public 음식주문응답? 상세(string 주문번호, long 음식점Id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(주문번호);
        if (음식점Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(음식점Id));
        }

        var order = orderStore.GetOrder(주문번호.Trim());
        return order?.음식점Id == 음식점Id
            ? 음식배달가능행동Projector.음식점용(FoodOrderSampleData.Clone(order))
            : null;
    }

    private static DateTime 최근변경시각(음식주문응답 order)
        => order.최근변경시각Utc
           ?? order.상태이력.OrderByDescending(history => history.전이시각Utc)
               .Select(history => (DateTime?)history.전이시각Utc)
               .FirstOrDefault()
           ?? order.CreatedAt;
}
