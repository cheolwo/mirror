using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.설정;

namespace Ssalddel.Services.Operations;

public interface I마트기사후속OutboxService
{
    Task<int> 처리Async(int maxCount = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// commit 이후 최신 마트·운송 정본을 기존 Adapter로 전달합니다. 재처리 시 과거 단계 payload로 되감지 않습니다.
/// 업무 확정과 외부 사본의 성공을 구분하며 lease/CAS와 기존 소비자의 멱등 키를 함께 사용합니다.
/// </summary>
public sealed class 마트기사후속OutboxService(
    SsalddelContext db,
    I운송원장Mongo동기화Service transportLedger,
    I음식마트원장동기화OutboxService martLedger,
    I음식배달권실행공간Store scope,
    I배차대기원장전환Service transition,
    TimeProvider? timeProvider = null) : I마트기사후속OutboxService
{
    private DateTime Now => (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<int> 처리Async(int maxCount = 50, CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsRelational()) return 0;
        var now = Now;
        var keys = await db.Set<마트기사후속Outbox>().AsNoTracking()
            .Where(x => (x.상태Code == "Pending" && (!x.NextAttemptAtUtc.HasValue || x.NextAttemptAtUtc <= now))
                || (x.상태Code == "Processing" && x.LeaseUntilUtc < now))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.멱등키).Select(x => x.멱등키)
            .Take(Math.Clamp(maxCount, 1, 200)).ToArrayAsync(cancellationToken);
        var completed = 0;
        foreach (var key in keys)
        {
            var owner = Guid.NewGuid().ToString("N");
            var claimed = await db.Set<마트기사후속Outbox>()
                .Where(x => x.멱등키 == key && ((x.상태Code == "Pending" && (!x.NextAttemptAtUtc.HasValue || x.NextAttemptAtUtc <= now))
                    || (x.상태Code == "Processing" && x.LeaseUntilUtc < now)))
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.상태Code, "Processing").SetProperty(y => y.LeaseOwner, owner)
                    .SetProperty(y => y.LeaseUntilUtc, now.AddMinutes(2)).SetProperty(y => y.ClaimRevision, y => y.ClaimRevision + 1)
                    .SetProperty(y => y.시도횟수, y => y.시도횟수 + 1).SetProperty(y => y.UpdatedAtUtc, now), cancellationToken);
            if (claimed != 1) continue;
            var item = await db.Set<마트기사후속Outbox>().AsNoTracking().SingleAsync(x => x.멱등키 == key, cancellationToken);
            var success = false;
            try { success = await ConsumeAsync(item, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { /* 원문 오류·주소·보호 코드를 Outbox나 로그로 확산하지 않습니다. */ }
            var count = await db.Set<마트기사후속Outbox>()
                .Where(x => x.멱등키 == key && x.상태Code == "Processing" && x.LeaseOwner == owner && x.ClaimRevision == item.ClaimRevision)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.상태Code, success ? "Succeeded" : "Pending")
                    .SetProperty(y => y.마지막실패Code, success ? null : "MartDriverFollowupUnavailable")
                    .SetProperty(y => y.NextAttemptAtUtc, success ? (DateTime?)null : Now.AddSeconds(30))
                    .SetProperty(y => y.LeaseOwner, (string?)null).SetProperty(y => y.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(y => y.UpdatedAtUtc, Now), cancellationToken);
            if (success && count == 1) completed++;
        }
        return completed;
    }

    private async Task<bool> ConsumeAsync(마트기사후속Outbox item, CancellationToken ct)
    {
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == item.배차의뢰Id, ct);
        var order = await db.마트주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문참조번호 == item.주문참조번호, ct);
        if (queue is null || order is null || queue.원본의뢰유형 != 마트기사업무Service.SourceType
            || queue.원본의뢰Id != order.주문참조번호 || queue.화주Id != order.판매자UserId
            || queue.배차업무유형 != 상태값.배차업무유형.음식배달) return false;
        var attempt = await db.Set<마트배송시도>().AsNoTracking().SingleOrDefaultAsync(x => x.배차의뢰Id == queue.의뢰Id, ct);
        switch (item.종류Code)
        {
            case "TransportLedger":
                return await transportLedger.운송실행투영동기화Async(queue, item.기사Id, ct) is not null;
            case "DispatchScope":
                if (!string.IsNullOrWhiteSpace(queue.확정기사Id) || queue.배차큐단계 == 상태값.배차큐단계.종료)
                    await scope.Remove운송의뢰Async(queue.의뢰Id, ct);
                return true;
            case "AdvanceRecommendation":
                // 오래된 거절 예약은 새 추천이나 이미 수락한 업무에 간섭하지 않습니다.
                if (queue.추천라운드 != item.업무Revision || queue.마지막거절기사Id != item.기사Id
                    || queue.확정기사Id is not null || queue.현재추천대상기사Id is not null
                    || queue.배차노출상태 != 상태값.배차노출상태.추천대기) return true;
                var next = await transition.추천대기처리Async(queue.의뢰Id, ct);
                return next.결과코드 != 배차대기원장전환결과코드.배차구성오류
                    && next.결과코드 != 배차대기원장전환결과코드.후보선정입력오류;
            case "MartLedger":
                if (attempt is null || attempt.Revision < item.업무Revision) return false;
                var outbounds = await db.출고예정.AsNoTracking().Where(x => x.주문참조번호 == item.주문참조번호).ToListAsync(ct);
                if (outbounds.Count == 0 || outbounds.Any(x => x.운송의뢰Id != queue.의뢰Id
                    || x.판매자UserId != order.판매자UserId || x.주문자UserId != order.주문자UserId)) return false;
                var inboundIds = outbounds.Where(x => x.입고요청Id.HasValue).Select(x => x.입고요청Id!.Value).Distinct().ToArray();
                var inbounds = await db.입고요청.AsNoTracking().Where(x => Enumerable.Contains(inboundIds, x.Id)).ToListAsync(ct);
                // 같은 최신 시도 판본의 후속은 기존 Outbox 멱등 키 하나로 수렴합니다.
                var childKey = $"mart-driver-ledger:{attempt.시도StableId}:{attempt.Revision}";
                await martLedger.출고원장예약후즉시처리Async(outbounds, inbounds, item.기사Id, childKey,
                    currentStageKey: order.현재단계, ledgerTemplateKey: CommunityLedgerTemplateKeys.SsalddelMart, cancellationToken: ct);
                // 기존 adapter는 실패를 자체 Pending으로 보존하고 반환하므로 void 반환만으로 성공하지 않습니다.
                return await db.Set<음식마트원장동기화Outbox>().AsNoTracking()
                    .AnyAsync(x => x.멱등키 == childKey && x.처리상태 == "Succeeded", ct);
            default: return false;
        }
    }
}
