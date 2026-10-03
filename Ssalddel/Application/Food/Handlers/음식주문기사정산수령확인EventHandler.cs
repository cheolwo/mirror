using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food.Events;
using 살뜰.Data;

namespace Ssalddel.Application.Food.Handlers;

public sealed class 음식주문기사정산수령확인EventHandler(SsalddelContext db)
    : INotificationHandler<주문자음식주문수령확인됨Event>
{
    public async Task Handle(주문자음식주문수령확인됨Event notification, CancellationToken cancellationToken)
    {
        // 이벤트 내용만으로 정산을 승격하지 않고 저장된 주문·수령 확인을 재검증합니다.
        var order = await db.음식주문.Include(x => x.상태이력)
            .SingleOrDefaultAsync(x => x.주문번호 == notification.주문.주문번호, cancellationToken);
        var settlement = await db.음식주문기사정산
            .SingleOrDefaultAsync(x => x.주문번호 == notification.주문.주문번호, cancellationToken);
        if (order is null || settlement is null || settlement.수령확인시각Utc.HasValue) return;
        음식주문기사정산Recorder.상태반영(settlement, order);
        if (settlement.수령확인시각Utc.HasValue)
        {
            settlement.Revision++;
            settlement.UpdatedAtUtc = notification.발생시각Utc;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
