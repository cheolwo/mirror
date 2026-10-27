using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Community;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Queue;

public sealed partial class 배차대기원장전환Service
{
    public async Task<배차대기원장전환결과> 배차수락취소처리Async(string requestId, string driverId,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var (queue, blocked) = await 수락취소원장전환Async(requestId, driverId, cancellationToken);
        if (blocked is not null) return blocked;
        if (queue!.생활배송배차방식 == NeighborhoodDispatchModes.PublicCall)
            return 전환됨(queue, 배차대기원장전환결과코드.공개배차전환됨, "기사 수락 취소 뒤 공개 콜 선택으로 다시 대기합니다.", driverId);
        // 재추천 실패는 이미 저장된 취소를 실패로 바꾸지 않는다. 기존 scanner가 추천 대기를 이어간다.
        try { await 추천거절후다음후보로진행Async(queue, driverId, cancellationToken); }
        catch (Exception ex)
        { _timingLogger?.LogWarning(ex, "수락 취소 저장 후 재추천을 이어가지 못했습니다. RequestId={RequestId}", requestId); }
        return 전환됨(queue, 배차대기원장전환결과코드.수락취소됨, "수락 취소를 저장했습니다. 다음 기사 추천은 별도로 진행됩니다.", driverId);
    }

    private async Task<(운송원장? Queue, 배차대기원장전환결과? Blocked)> 수락취소원장전환Async(
        string requestId, string driverId, CancellationToken ct)
    {
        _db.ChangeTracker.DetectChanges();
        var pending = _db.ChangeTracker.Entries().FirstOrDefault(x => x.State is EntityState.Modified or EntityState.Added or EntityState.Deleted
            && (x.Entity is 운송원장 q && q.의뢰Id == requestId
                || x.Entity is 살뜰.도메인.화주.화주운송의뢰 s && s.의뢰Id == requestId));
        if (pending is not null)
            return (null, 배차대기원장전환결과.전환안됨(requestId, 배차응답가능정책.취소상태변경Code,
                "현재 운송에 미저장 변경이 있습니다. 최신 상태를 확인한 뒤 다시 진행해 주세요.", driverId));

        await using var tx = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        if (queue is null) return (null, 대상없음(requestId, driverId));
        await _db.Entry(queue).ReloadAsync(ct);
        if (_db.Entry(queue).State == EntityState.Detached) return (null, 대상없음(requestId, driverId));
        if (!배차응답가능정책.수락취소가능(queue, driverId))
            return (queue, 전환안됨(queue, 배차응답가능정책.물품인수이후인가(queue)
                ? 배차응답가능정책.물품보유복구필요Code : 배차응답가능정책.취소상태변경Code,
                배차응답가능정책.물품인수이후인가(queue) ? 배차응답가능정책.물품보유복구안내
                    : "현재 운송 상태와 담당 기사를 다시 확인해 주세요. 일반 수락 취소를 진행하지 않았습니다.", driverId));

        var source = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        if (source is not null)
        {
            await _db.Entry(source).ReloadAsync(ct);
            if (배차응답가능정책.인수이후상태인가(source.배차상태))
                return (queue, 전환안됨(queue, 배차응답가능정책.물품보유복구필요Code, 배차응답가능정책.물품보유복구안내, driverId));
        }
        if (await _db.비정상운송사건.AsNoTracking().AnyAsync(x => (x.운송Id == queue.Id || x.운송의뢰Id == requestId)
            && (x.정산보류적용여부 || x.상태Code == 비정상운송사건상태Codes.운영검토대기
                || x.업무통제상태Code == 비정상운송업무통제상태Codes.전체보류
                || x.업무통제상태Code == 비정상운송업무통제상태Codes.일부보류), ct))
            return (queue, 전환안됨(queue, 배차응답가능정책.물품보유복구필요Code,
                "현재 운송의 문제 검토 또는 보류가 남아 있습니다. 해당 문제 처리에서 다음 처리를 확인해 주세요.", driverId));

        var before = _db.Entry(queue).CurrentValues.Clone();
        PropertyValues? sourceBefore = source is null ? null : _db.Entry(source).CurrentValues.Clone();
        try
        {
            var now = _timingClock.GetUtcNow().UtcDateTime;
            if (source is not null) { source.배차상태 = 상태값.배차상태.매칭중; source.UpdatedAt = now; }
            배차재추천상태Policy.적용(queue, driverId, now);
            if (queue.생활배송배차방식 is not null)
            { queue.생활배송선택판본++; Ssalddel.Services.Community.생활배송배차Policy.대기적용(queue); }
            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return (queue, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (tx is not null) await tx.RollbackAsync(CancellationToken.None);
            _db.Entry(queue).CurrentValues.SetValues(before); _db.Entry(queue).State = EntityState.Unchanged;
            if (source is not null && sourceBefore is not null)
            { _db.Entry(source).CurrentValues.SetValues(sourceBefore); _db.Entry(source).State = EntityState.Unchanged; }
            await _db.Entry(queue).ReloadAsync(CancellationToken.None);
            if (source is not null) await _db.Entry(source).ReloadAsync(CancellationToken.None);
            return (queue, 전환안됨(queue, 배차응답가능정책.취소상태변경Code,
                "운송 상태가 먼저 변경되었습니다. 물품 보유와 현재 운송 상태를 다시 확인해 주세요.", driverId));
        }
    }
}
