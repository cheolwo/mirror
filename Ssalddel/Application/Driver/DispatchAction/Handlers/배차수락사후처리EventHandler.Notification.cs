using Microsoft.EntityFrameworkCore;

namespace Ssalddel.Application.Driver.DispatchAction;

public sealed partial class 배차수락사후처리EventHandler
{
    private async Task 화주수락알림의도적재Async(배차수락됨Event notification, CancellationToken cancellationToken)
    {
        // 신규 SQL 수락은 원자 저장된 recovery 의도가 알림 Outbox를 소유합니다.
        // Event 재발행·부분 실패가 같은 수락의 알림을 다시 만들지 않게 구형 경로와 분리합니다.
        if (await _db.운송이벤트.AsNoTracking().AnyAsync(x => x.의뢰Id == notification.의뢰Id
            && x.이벤트타입 == CargoAcceptanceRecoveryMarker.EventType, cancellationToken)) return;
        var dispatchRequest = await _db.화주운송의뢰
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.의뢰Id == notification.의뢰Id, cancellationToken);
        if (dispatchRequest is null)
        {
            return;
        }

        var reserved = await 살뜰.Services.Notifications.CargoWorkflowProgressNotifications.ReserveAcceptedAsync(
            _db, dispatchRequest, notification.기사Id, notification.발생시각Utc, notification.TraceId, cancellationToken);
        if (reserved is null) return;
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch
        {
            살뜰.Services.Notifications.CargoWorkflowProgressNotifications.DiscardUncommitted(_db, reserved);
            throw;
        }
    }
}
