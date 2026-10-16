using System.Data;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Common;

public interface I운영배차제안표시Service
{
    Task<bool> 표시확인Async(string 기사Id, string 의뢰Id, int 추천라운드, CancellationToken cancellationToken = default);
}

/// <summary>인증한 기사 앱의 활성 제안 표시 수신만 기록한다. FCM 성공이나 사람의 읽음을 대신 기록하지 않는다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "본인의 현재 추천 라운드가 앱에 표시된 수신 확인을 영속 활동 원장에 기록한다.",
    StepKey = "application.operational-dispatch-offer-display", FlowOrder = 61,
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "앱 표시 ACK만 기록하며 사람의 읽음·알림음 청취·수락을 인증하지 않는다.")]
public sealed class 운영배차제안표시Service(
    SsalddelContext db,
    I운영배차공통UseCase availability,
    TimeProvider? timeProvider = null) : I운영배차제안표시Service
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task<bool> 표시확인Async(string 기사Id, string 의뢰Id, int 추천라운드,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(기사Id) || string.IsNullOrWhiteSpace(의뢰Id) || 추천라운드 <= 0)
            return false;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            운영배차활동사건? receipt = null;
            var receiptAdded = false;
            DateTime? startedAt = null;
            DateTime? expiresAt = null;
            try
            {
                var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == 의뢰Id, cancellationToken);
                var now = _clock.GetUtcNow().UtcDateTime;
                if (queue is null || queue.상태 != 상태값.배차대기상태.대기
                    || queue.배차큐단계 != 상태값.배차큐단계.배차추천
                    || queue.배차노출상태 != 상태값.배차노출상태.추천중
                    || queue.현재추천대상기사Id != 기사Id || queue.추천라운드 != 추천라운드
                    || !queue.추천시작시각.HasValue || queue.추천시작시각 > now
                    || !queue.추천만료시각.HasValue || queue.추천만료시각 <= now
                    || !운영배차수신상태Policy.신규배차수신허용(await availability.수신상태조회Async(기사Id, cancellationToken)))
                    return false;

                startedAt = queue.추천시작시각;
                expiresAt = queue.추천만료시각;
                receipt = 운영배차활동사건Factory.제안표시확인(기사Id, 의뢰Id, 추천라운드, now);
                if (!await db.운영배차활동사건.AnyAsync(x => x.사건StableId == receipt.사건StableId, cancellationToken))
                {
                    db.운영배차활동사건.Add(receipt);
                    receiptAdded = true;
                    await db.SaveChangesAsync(cancellationToken);
                }
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException) when (receiptAdded && receipt is not null)
            {
                await 실패정리Async();
                // rollback 이후 같은 기사·제안·라운드·유효시간의 정본만 멱등 성공으로 인정한다.
                if (await db.운영배차활동사건.AsNoTracking().AnyAsync(x => x.사건StableId == receipt.사건StableId
                    && x.주체Id == 기사Id && x.제안Id == 의뢰Id && x.상태값Code == receipt.상태값Code
                    && x.사건유형Code == 운영배차사건유형Code.제안표시확인
                    && x.사유Code == 운영배차미응답Code.앱표시확인 && x.제안유효성Code == 운영배차제안유효성Code.유효
                    && x.발생시각Utc >= startedAt!.Value && x.발생시각Utc < expiresAt!.Value, cancellationToken)) return true;
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
                    catch { /* 원래 저장/commit 실패를 보존한다. */ }
                    try { await transaction.DisposeAsync(); }
                    catch { /* 재시도는 현재 추천과 정본을 다시 확인한다. */ }
                }
                if (receiptAdded && receipt is not null) db.Entry(receipt).State = EntityState.Detached;
            }
        });
    }
}
