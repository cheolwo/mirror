using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food.Events;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;

namespace Ssalddel.Services.Community;

/// <summary>현재 음식·운송 원천으로 독립 예약된 기사 후속 관심사를 처리합니다.</summary>
internal sealed class 음식기사후속PayloadHandler(
    SsalddelContext db,
    I음식마트원장Mongo동기화Service ledgerSync,
    ISsalddelFoodOrderStore? foodOrderStore,
    I운송원장Mongo동기화Service? transportLedgerSync,
    I음식점주문실시간알림Service? restaurantNotification,
    IPublisher? publisher)
{
    public async Task 처리Async(음식마트원장동기화Outbox item,
        음식기사후속Payload payload, CancellationToken cancellationToken)
    {
        var intent = payload.Intent;
        if (intent.주문번호 != item.원천Id || intent.처리기사Id != item.변경자
            || 음식기사후속Kinds.Key(intent, payload.Kind) != item.멱등키
            || !음식기사후속Kinds.For(intent).Contains(payload.Kind))
            throw new InvalidOperationException("FoodDriverFollowupSourceMismatch");
        // 저장 당시의 고객 정보나 상태 사본으로 후속 업무를 확정하지 않습니다.
        var order = foodOrderStore?.GetOrder(item.원천Id)
            ?? throw new InvalidOperationException("FoodOutboxSourceUnavailable");
        switch (payload.Kind)
        {
            case 음식기사후속Kinds.FoodLedger:
                if (await ledgerSync.음식주문동기화Async(order, item.변경자, cancellationToken) is null)
                    throw new InvalidOperationException("FoodDriverFollowupLedgerUnavailable");
                break;
            case 음식기사후속Kinds.TransportLedger:
                var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == intent.운송Id, cancellationToken);
                if (queue is null || queue.의뢰Id != intent.배차의뢰Id || queue.원본의뢰Id != intent.주문번호
                    || queue.배차업무유형 != 상태값.배차업무유형.음식배달
                    || !운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형))
                    throw new InvalidOperationException("FoodDriverFollowupTransportMismatch");
                if (transportLedgerSync is null || await transportLedgerSync.운송실행투영동기화Async(queue, item.변경자, cancellationToken) is null)
                    throw new InvalidOperationException("FoodDriverFollowupTransportUnavailable");
                break;
            case 음식기사후속Kinds.RestaurantNotification:
                if (restaurantNotification is null) throw new InvalidOperationException("FoodDriverFollowupNotificationUnavailable");
                await restaurantNotification.주문상태변경알림발송Async(order, intent.음식점알림사유!, cancellationToken);
                break;
            case 음식기사후속Kinds.CommunityActivity:
                if (publisher is null) throw new InvalidOperationException("FoodDriverFollowupPublisherUnavailable");
                await publisher.Publish(new 음식배달인계상태변경됨Event(intent.배차의뢰Id, intent.주문번호,
                    intent.상태, intent.발생시각Utc, intent.EventId), cancellationToken);
                break;
            default:
                throw new InvalidOperationException("FoodDriverFollowupKindUnsupported");
        }
    }
}
