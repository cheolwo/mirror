using System.Data;
using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Driver.DispatchAction;

public sealed partial class CargoAcceptanceRecoveryService
{
    /// <summary>기존 관리자 API가 확인한 같은 판본의 실패 의도만 재예약합니다. 원 수락/송금/발송은 실행하지 않습니다.</summary>
    public async Task<bool> RetryAsync(long eventId, int expectedAttempts, int expectedRevision, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null
            || eventId <= 0 || expectedAttempts < 0 || expectedRevision < 0) return false;
        var retry = 0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (retry++ > 0) db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var row = await Events.SingleOrDefaultAsync(x => x.Id == eventId, ct);
            var job = row is null ? null : CargoAcceptanceRecoveryMarker.Read(row.메타데이터);
            if (row is null || job is null || job.Attempts != expectedAttempts || job.Revision != expectedRevision
                || job.State != "Failed") return false;
            // CAS를 같은 transaction의 정본 읽기/현재 수락 검증까지 유지합니다.
            var original = row.메타데이터;
            job.Revision++; job.UpdatedAtUtc = Now;
            var locked = CargoAcceptanceRecoveryMarker.Write(original, job);
            if (await CompareExchange(row.Id, original, locked, ct) != 1) return false;
            var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.TransportId, ct);
            var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == row.의뢰Id, ct);
            if (!await MatchesCurrentAsync(row, job, transport, source, ct)) return false;
            job.State = "Retry"; job.ManualRetries++; job.TotalAttempts = Math.Max(job.TotalAttempts, job.Attempts);
            job.Attempts = 0; job.Revision++;
            job.LeaseToken = null; job.LeaseUntilUtc = null; job.NextAttemptAtUtc = Now;
            job.PromisePrepared = false; job.PromisePreparationCode = ""; job.NotificationPrepared = false;
            job.RecommendationRefreshPrepared = false; job.RecommendationRefreshCode = "";
            job.ReasonCode = "ExplicitRetryRequested";
            if (await CompareExchange(row.Id, locked, CargoAcceptanceRecoveryMarker.Write(locked, job), ct) != 1) return false;
            await tx.CommitAsync(ct);
            return true;
        });
    }

    private async Task<bool> MatchesCurrentAsync(운송이벤트 row, CargoAcceptanceRecoveryJob job,
        운송원장? transport, 화주운송의뢰? source, CancellationToken ct)
    {
        if (transport is null || source is null || transport.의뢰Id != row.의뢰Id
            || transport.배차업무유형 != 상태값.배차업무유형.용달운송 || transport.일반회원배송전용
            || transport.확정기사Id != job.DriverId || transport.기사_운송자 != job.DriverId
            || transport.추천라운드 != job.RecommendationRound || transport.배차큐단계 != 상태값.배차큐단계.확정
            || source.상태 != 상태값.의뢰상태.생성됨
            || source.배차상태 is 상태값.배차상태.취소 or 상태값.배차상태.인수완료 or 상태값.배차상태.하차완료
            || transport.상태 is 상태값.배차상태.취소 or 상태값.배차상태.인수완료 or 상태값.배차상태.하차완료) return false;
        return await Events.Where(x => x.의뢰Id == row.의뢰Id).MaxAsync(x => x.Id, ct) == row.Id;
    }
}
