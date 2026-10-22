using Microsoft.EntityFrameworkCore;
using 살뜰.Services.Notifications;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Food;

public sealed partial class EfSsalddelFoodOrderStore
{
    // 조회나 후속 Event가 아니라 전문 상태를 저장하는 호출자에만 결속합니다.
    private List<Command알림Outbox> 진행알림예약(음식주문 order, string? stageCode, Guid requestId)
    {
        var reserved = new List<Command알림Outbox>();
        if (stageCode is null) return reserved;
        var occurredAt = order.상태이력.LastOrDefault()?.전이시각Utc ?? order.UpdatedAt;
        var occurrenceKey = requestId != Guid.Empty
            ? requestId.ToString("N")
            : $"revision:{order.상태이력.Count}:{occurredAt.Ticks}";
        try
        {
            Reserve(order.주문자UserId, $"/workspace/orderer?selected={Uri.EscapeDataString(order.주문번호)}");
            // 준비 완료 때 다음 행동이 있는 현재 확정 기사만 대상으로 합니다.
            // 음식점 ID를 실제 사용자 ID로 추정하지 않고 기존 음식점 SignalR은 유지합니다.
            if (stageCode == "food.ready" && order.배차대기Id is > 0 && 유효한배차확정(order))
            {
                var queue = _db.운송원장.AsNoTracking().SingleOrDefault(x => x.Id == order.배차대기Id);
                if (queue is not null && queue.배차업무유형 == 상태값.배차업무유형.음식배달
                    && 운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형)
                    && (string.IsNullOrWhiteSpace(queue.원본의뢰Id) ? queue.의뢰Id : queue.원본의뢰Id) == order.주문번호
                    && !string.IsNullOrWhiteSpace(queue.확정기사Id))
                    Reserve(queue.확정기사Id, $"/workspace/food-driver?selected={Uri.EscapeDataString(queue.의뢰Id)}");
            }
            return reserved;
        }
        catch
        {
            진행알림예약해제(reserved);
            throw;
        }

        void Reserve(string recipientId, string route)
        {
            var item = 업무진행알림Recorder.Reserve(_db, stageCode, order.주문번호,
                occurrenceKey, recipientId, route, occurredAt);
            if (item is not null) reserved.Add(item);
        }
    }

    private void 진행알림예약해제(IEnumerable<Command알림Outbox> items)
    {
        foreach (var item in items)
            if (_db.Entry(item).State != EntityState.Detached)
                _db.Entry(item).State = EntityState.Detached;
    }
}
