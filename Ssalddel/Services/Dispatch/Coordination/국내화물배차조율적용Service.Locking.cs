using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using Ssalddel.Services.Dispatch.Compliance;
using System.Text.Json;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Recommendation;

namespace 살뜰.Services.Dispatch.Coordination;

public sealed partial class 국내화물배차조율적용Service
{
    private async Task<국내화물배차추천잠금?> 추천잠금시도Async(
        국내화물배차제안 배차제안,
        국내화물검증수행계획? plan,
        string? batchInputHash,
        int? timeoutSeconds,
        int 최대동시추천잠금건수,
        CancellationToken cancellationToken)
    {
        // 근거 없는 구형 제안을 현재 확인된 추천으로 자동 승격하지 않는다.
        if (배차제안.판정근거 is null || _상태근거 is null || _적격성 is null
            || plan is not { 확인됨: true, 가능여부: true } || string.IsNullOrWhiteSpace(batchInputHash)
            || plan.계획Id != 배차제안.수행계획Id || plan.입력지문 != batchInputHash
            || plan.기사Id != 배차제안.기사Id || !plan.의뢰Ids.Contains(배차제안.의뢰Id, StringComparer.Ordinal)) return null;
        using var lease = _참여 is null ? null : await _참여.EnterAsync(배차제안.기사Id, cancellationToken);
        // 호출자의 transaction에는 SQL·Outbox만 참여한다. commit 여부를 모르는 상태에서
        // 별도 저장소의 기사 순위나 실행 공간을 먼저 바꾸지 않는다.
        if (_db.Database.CurrentTransaction is not null)
            return await 추천잠금저장Async(배차제안, plan, batchInputHash, timeoutSeconds,
                최대동시추천잠금건수, null, null, cancellationToken);

        저장한추천? prepared = null;
        var applied = await _db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            var tracked = 추적상태보관();
            try
            {
                return await 추천잠금저장Async(배차제안, plan, batchInputHash, timeoutSeconds,
                    최대동시추천잠금건수, prepared, value => prepared = value with
                    {
                        호출자저장상태 = tracked.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                            .Select(x => 추적상태보관(x.Entry)).ToArray()
                    }, token);
            }
            catch
            {
                // SQL rollback 뒤 SaveChanges가 받아들인 값·Outbox를 다음 시도에 재사용하지 않는다.
                추적상태복원(tracked);
                throw;
            }
        }, cancellationToken);
        if (applied is null) return null;

        // 외부 효과는 commit 확인 뒤, SQL 재시도 밖에서 한 번만 수행한다.
        await _기사상태Service.추천기록Async(배차제안.기사Id, applied.추천시작시각Utc, cancellationToken);
        await _배달권실행공간Store.Remove운송의뢰Async(applied.의뢰Id, cancellationToken);
        return applied;
    }

    private async Task<국내화물배차추천잠금?> 추천잠금저장Async(
        국내화물배차제안 배차제안, 국내화물검증수행계획 plan, string batchInputHash,
        int? timeoutSeconds, int 최대동시추천잠금건수,
        저장한추천? prepared, Action<저장한추천>? onPrepared, CancellationToken cancellationToken)
    {
        if (_상태근거 is null || _적격성 is null) return null;
        await using var tx = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
        if (_참여 is not null) await _참여.LockAsync(배차제안.기사Id, cancellationToken);
        var now = _clock.GetUtcNow().UtcDateTime;

        // 추천 잠금은 메모리 큐가 아니라 DB 원장인 배차대기에 기록한다.
        // 서버가 재시작되면 만료되지 않은 추천중 의뢰는 이 상태를 보고 중복 추천을 막는다.
        var 배차대기 = await _db.운송원장
            .FirstOrDefaultAsync(x => x.의뢰Id == 배차제안.의뢰Id, cancellationToken);
        if (배차대기 is not null) await _db.Entry(배차대기).ReloadAsync(cancellationToken);
        // commit 응답만 끊긴 경우에는 같은 저장 판본을 정본으로 확인한다.
        // 새 라운드나 다른 제안은 이 확인으로 덮어쓰지 않는다.
        if (배차대기 is not null && prepared?.일치(배차대기) == true)
        {
            // rollback용 복원이 되돌린 Added 임시키·수정 Original 값도 같은 commit의
            // 마지막 저장 상태로 수렴시킨다. 이후 미저장 변경을 무작정 AcceptAllChanges하지 않는다.
            추적상태복원(prepared.호출자저장상태, 새추적해제: false);
            return prepared.잠금;
        }
        if (배차대기 is null || !국내화물배차후보금지정책.추천잠금가능(배차대기, now))
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            return null;
        }

        var 현재추천잠금건수 = await _db.운송원장
            .AsNoTracking()
            .CountAsync(
                x => x.현재추천대상기사Id == 배차제안.기사Id
                     && x.상태 == 상태값.배차대기상태.대기
                     && x.배차큐단계 == 상태값.배차큐단계.배차추천
                     && x.배차노출상태 == 상태값.배차노출상태.추천중
                     && (!x.추천만료시각.HasValue || x.추천만료시각 > now),
                cancellationToken);
        // 이 값은 기사에게 동시에 노출할 추천 잠금 수만 제한한다.
        // 이미 수락한 운송 건수는 업무 상한이 아니며, 추가 수락 가능 여부는
        // 수락 관문에서 차량·구간 적재량·전체 시간창으로 다시 판정한다.
        if (현재추천잠금건수 >= 최대동시추천잠금건수)
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            return null;
        }

        // AI 입력 생성 뒤 변경된 자격·화물도 실제 추천 저장·알림 전에 정본으로 다시 확인한다.
        var qualification = await 신규화물업무자격검사.평가Async(_신규업무자격, 배차제안.기사Id, cancellationToken);
        var request = await _db.화주운송의뢰.AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == 배차제안.의뢰Id, cancellationToken);
        if (!qualification.CanReceiveNewWork || request is null || !신규화물업무자격검사.화물정보완료(_신규업무자격, request))
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            return null;
        }

        if (!await 운영배차신규수신Guard.허용Async(_수신, 배차제안.기사Id, cancellationToken)
            || (_참여 is not null && (await _참여.CheckRecommendationAsync(배차제안.기사Id, 배차대기, cancellationToken)).IsFailed)
            || !await _상태근거.현재근거확인Async(배차제안.판정근거, cancellationToken))
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            return null;
        }
        var eligibility = await _적격성.평가Async(배차제안.기사Id, request, [], plan.상하차작업시간, cancellationToken);
        // 경고 확인은 기사가 수락할 때 한다. 추천 준비가 동의를 대신하지 않는다.
        if ((!eligibility.수락가능 && eligibility.오류코드 != 화물배차수락오류코드.경고확인필요)
            || !await _상태근거.현재근거확인Async(배차제안.판정근거, cancellationToken))
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            return null;
        }
        var fare = await _db.운임구성.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.운임구성Id
            && x.의뢰Id == request.의뢰Id, cancellationToken);
        if (fare?.기사지급예정운임 is not > 0) return null;

        var 기준시각Utc = _clock.GetUtcNow().UtcDateTime;
        배차대기.기사지급예정액 = fare.기사지급예정운임;
        배차대기.추천판정근거Json = JsonSerializer.Serialize(new 국내화물추천저장근거
        {
            판정근거 = 배차제안.판정근거, 수행계획 = plan, 조율입력지문 = batchInputHash
        });
        배차대기.배차큐단계 = 상태값.배차큐단계.배차추천;
        배차대기.배차노출상태 = 상태값.배차노출상태.추천중;
        배차대기.현재추천대상기사Id = 배차제안.기사Id;
        배차대기.추천라운드 += 1;
        배차대기.추천시작시각 = 기준시각Utc;
        배차대기.추천만료시각 = 기준시각Utc.AddSeconds(timeoutSeconds ?? _options.추천유지시간초);
        배차대기.UpdatedAt = 기준시각Utc;

        await _db.SaveChangesAsync(cancellationToken);

        await _알림Service.추천알림요청생성Async(
            배차대기.Id,
            배차대기.의뢰Id,
            배차제안.기사Id,
            배차대기.추천라운드,
            cancellationToken);

        var applied = new 국내화물배차추천잠금(
            배차대기.의뢰Id,
            배차제안.기사Id,
            배차대기.추천라운드,
            기준시각Utc,
            배차대기.추천만료시각!.Value);
        onPrepared?.Invoke(new 저장한추천(applied, 배차대기.추천판정근거Json!, 배차대기.기사지급예정액));
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return applied;
    }
}
