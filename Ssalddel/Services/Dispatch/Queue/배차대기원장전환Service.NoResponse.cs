using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Queue;

public sealed partial class 배차대기원장전환Service
{
    private sealed record 만료원장전환결과(운송원장? Queue, string? DriverId, 배차대기원장전환결과? Blocked);

    private async Task<만료원장전환결과> 추천만료원장전환Async(string requestId, CancellationToken cancellationToken)
    {
        _db.ChangeTracker.DetectChanges();
        var pending = _db.ChangeTracker.Entries<운송원장>().FirstOrDefault(x => x.Entity.의뢰Id == requestId
            && x.State is EntityState.Modified or EntityState.Added or EntityState.Deleted);
        if (pending is not null)
            return new(pending.Entity, null, 전환안됨(pending.Entity, 배차대기원장전환결과코드.추천준비안됨,
                "현재 원장에 미저장 변경이 있어 만료 처리하지 않았습니다."));

        await using var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        운송원장? queue = null;
        PropertyValues? before = null;
        운영배차활동사건? noResponse = null;
        var noResponseAdded = false;
        string? driverId = null;
        try
        {
            queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null) return new(null, null, 대상없음(requestId));
            // worker가 보관하던 추적 사본으로 이미 수락/거절된 추천을 만료시키지 않는다.
            await _db.Entry(queue).ReloadAsync(cancellationToken);
            if (_db.Entry(queue).State == EntityState.Detached) return new(null, null, 대상없음(requestId));
            if (queue.상태 != 상태값.배차대기상태.대기) return new(queue, null, 대기상태아님(queue));
            before = _db.Entry(queue).CurrentValues.Clone();
            var now = _timingClock.GetUtcNow().UtcDateTime;
            if (!queue.추천만료시각.HasValue && string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
                && queue.배차큐단계 == 상태값.배차큐단계.배차추천
                && queue.배차노출상태 is 상태값.배차노출상태.추천대기 or 상태값.배차노출상태.추천만료)
            {
                var previousDriver = await 만료된추천기사조회Async(queue, cancellationToken);
                if (previousDriver is not null) return new(queue, previousDriver, null);
            }
            if (!queue.추천만료시각.HasValue || queue.추천만료시각 > now)
                return new(queue, null, 전환안됨(queue, 배차대기원장전환결과코드.만료전,
                    "추천 만료 시각 전이라 만료 처리하지 않았습니다.", queue.현재추천대상기사Id));

            driverId = queue.현재추천대상기사Id;
            if (queue.배차큐단계 == 상태값.배차큐단계.배차추천
                && queue.배차노출상태 == 상태값.배차노출상태.추천중
                && !string.IsNullOrWhiteSpace(driverId) && queue.추천라운드 > 0 && queue.추천시작시각.HasValue)
            {
                var receipt = 운영배차활동사건Factory.제안표시확인(driverId, queue.의뢰Id, queue.추천라운드, now);
                // 외부 발송 성공만 있거나 앱 표시가 확인되지 않은 제안은 미응답으로 세지 않는다.
                if (await _db.운영배차활동사건.AnyAsync(x => x.사건StableId == receipt.사건StableId
                        && x.주체Id == driverId && x.제안Id == queue.의뢰Id
                        && x.사건유형Code == 운영배차사건유형Code.제안표시확인
                        && x.제안유효성Code == 운영배차제안유효성Code.유효
                        && x.발생시각Utc >= queue.추천시작시각.Value && x.발생시각Utc < queue.추천만료시각.Value,
                        cancellationToken))
                {
                    // 지연 처리된 이전 만료는 수동 재개 후 새 연속 횟수를 만들 수 없도록 원래 만료 시각을 보존한다.
                    noResponse = 운영배차활동사건Factory.무응답(driverId, queue.의뢰Id, queue.추천라운드, queue.추천만료시각.Value);
                    if (!await _db.운영배차활동사건.AnyAsync(x => x.사건StableId == noResponse.사건StableId, cancellationToken))
                    {
                        _db.운영배차활동사건.Add(noResponse);
                        noResponseAdded = true;
                    }
                }
            }
            queue.배차큐단계 = 상태값.배차큐단계.배차추천;
            // 후속 후보 조회가 실패하거나 commit 응답을 잃어도 기존 scanner가 다시 처리할 수 있게 남긴다.
            queue.배차노출상태 = 상태값.배차노출상태.추천대기;
            queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null;
            queue.추천만료시각 = null;
            queue.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new(queue, driverId, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await 실패정리Async();
            if (queue is null) throw;
            return new(queue, driverId, 전환안됨(queue, 배차대기원장전환결과코드.현재추천기사불일치,
                "추천 상태가 변경되어 이전 제안을 만료 처리하지 않았습니다.", driverId));
        }
        catch (DbUpdateException) when (noResponseAdded && noResponse is not null)
        {
            await 실패정리Async();
            // 두 worker가 같은 만료 사건을 INSERT한 경우만 이미 처리된 정본으로 구분한다.
            if (queue is not null && await _db.운영배차활동사건.AsNoTracking().AnyAsync(x =>
                    x.사건StableId == noResponse.사건StableId && x.주체Id == noResponse.주체Id
                    && x.제안Id == noResponse.제안Id && x.사건유형Code == noResponse.사건유형Code
                    && x.제안유효성Code == noResponse.제안유효성Code && x.책임Code == noResponse.책임Code
                    && x.상태값Code == noResponse.상태값Code && x.사유Code == noResponse.사유Code
                    && x.발생시각Utc == noResponse.발생시각Utc, cancellationToken))
                return new(queue, driverId, 전환안됨(queue, 배차대기원장전환결과코드.현재추천기사불일치,
                    "같은 추천 만료가 이미 처리되어 다시 집계하지 않았습니다.", driverId));
            throw;
        }
        catch
        {
            await 실패정리Async();
            throw;
        }

        async Task 실패정리Async()
        {
            if (transaction is not null)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                catch { /* 원래 저장/commit 실패를 rollback 실패로 바꾸지 않는다. */ }
                try { await transaction.DisposeAsync(); }
                catch { /* 원래 실패를 보존하며 재시도는 정본부터 다시 읽는다. */ }
            }
            // Save 성공 후 Commit이 실패한 경우에도 새 사건의 추적을 제거한다.
            if (noResponseAdded && noResponse is not null) _db.Entry(noResponse).State = EntityState.Detached;
            if (queue is not null)
            {
                if (before is not null) _db.Entry(queue).CurrentValues.SetValues(before);
                _db.Entry(queue).State = EntityState.Detached;
            }
        }
    }

    private async Task<string?> 만료된추천기사조회Async(운송원장 queue, CancellationToken cancellationToken)
    {
        if (queue.추천라운드 <= 0) return null;
        var events = await _db.운영배차활동사건.AsNoTracking().Where(x => x.제안Id == queue.의뢰Id
            && x.주체역할Code == 운영배차주체역할Code.기사
            && (x.사건유형Code == 운영배차사건유형Code.무응답 || x.사건유형Code == 운영배차사건유형Code.제안전달))
            .ToArrayAsync(cancellationToken);
        // 과거 라운드/다른 기사 사건을 이름이나 최신 시각만으로 연결하지 않는다.
        return events.FirstOrDefault(x => x.사건StableId == (x.사건유형Code == 운영배차사건유형Code.무응답
                ? 운영배차활동사건Factory.무응답(x.주체Id, queue.의뢰Id, queue.추천라운드, x.발생시각Utc)
                : 운영배차활동사건Factory.유효제안(x.주체Id, queue.의뢰Id, queue.추천라운드, x.발생시각Utc)).사건StableId)?.주체Id;
    }
}
