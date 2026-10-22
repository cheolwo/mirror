using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Driver.DispatchAction;

public interface ICargoAcceptanceRecoveryService
{
    Task<bool> ProcessRequestAsync(string requestId, string driverId, CancellationToken ct = default);
    Task<int> ProcessPendingAsync(int take = 20, CancellationToken ct = default);
    Task<bool> RetryAsync(long eventId, int expectedAttempts, int expectedRevision, CancellationToken ct = default)
        => Task.FromResult(false);
}

/// <summary>수락과 함께 저장한 의도만 재개합니다. 실제 알림 발송·송금이나 과거 수락의 역생성을 하지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalLogisticsOs, SsalddelCodeLayer.Application,
    "현재 화물 수락의 시간약속·화주 알림 의도를 기존 SQL 이벤트와 Outbox로 마감한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 75, StepKey = "application.cargo-acceptance-durable-followups",
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "최신 수락 ID/차수/담당자/현재 원의뢰를 재검증. 알림 의도만 적재하며 외부 발송·금융·운영 활성화 없음. worker 기본 OFF.")]
public sealed partial class CargoAcceptanceRecoveryService(
    SsalddelContext db, I화물연속배차UseCase continuity,
    IOptions<CargoAcceptanceRecoveryOptions> options, TimeProvider clock,
    ILogger<CargoAcceptanceRecoveryService> logger,
    IOptions<살뜰.Services.Options.화물연속배차Options>? continuityOptions = null,
    I국내화물추천재검증Service? recommendationRefresh = null) : ICargoAcceptanceRecoveryService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private IQueryable<운송이벤트> Events => db.운송이벤트.AsNoTracking()
        .Where(x => x.이벤트타입 == CargoAcceptanceRecoveryMarker.EventType);

    public async Task<bool> ProcessRequestAsync(string requestId, string driverId, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) return false;
        var row = await Events.Where(x => x.의뢰Id == requestId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return row is not null && CargoAcceptanceRecoveryMarker.Read(row.메타데이터)?.DriverId == driverId
            && await ProcessAsync(row, ct);
    }

    public async Task<int> ProcessPendingAsync(int take = 20, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) return 0;
        var query = Events.Where(x => x.메타데이터.Contains("\"state\":\"Pending\"")
            || x.메타데이터.Contains("\"state\":\"Retry\"") || x.메타데이터.Contains("\"state\":\"Processing\""));
        var wanted = Math.Clamp(take, 1, 100); var pageSize = Math.Clamp(wanted * 2, 20, 200);
        var ceiling = await query.Select(x => (long?)x.Id).MaxAsync(ct) ?? 0;
        long after = 0; var processed = 0;
        while (processed < wanted && after < ceiling)
        {
            ct.ThrowIfCancellationRequested();
            var rows = await query.Where(x => x.Id > after && x.Id <= ceiling).OrderBy(x => x.Id).Take(pageSize).ToArrayAsync(ct);
            if (rows.Length == 0) break;
            foreach (var row in rows)
            {
                after = row.Id;
                if (await ProcessAsync(row, ct)) processed++;
                if (processed >= wanted) break;
            }
        }
        return processed;
    }

    private bool Due(CargoAcceptanceRecoveryJob job)
        => (job.State is "Pending" or "Retry") && (job.NextAttemptAtUtc is null || job.NextAttemptAtUtc <= Now)
           || job.State == "Processing" && job.LeaseUntilUtc <= Now;

    private async Task<bool> ProcessAsync(운송이벤트 row, CancellationToken ct)
    {
        var job = CargoAcceptanceRecoveryMarker.Read(row.메타데이터);
        if (job is null || job.TransportId <= 0 || string.IsNullOrWhiteSpace(job.DriverId) || !Due(job)) return false;
        if (job.Attempts >= Math.Clamp(options.Value.MaximumAttempts, 1, 10))
        {
            job.State = "Failed"; job.ReasonCode = "AcceptanceRecoveryAttemptsExhausted";
            job.Revision++; job.UpdatedAtUtc = Now; job.LeaseToken = null; job.LeaseUntilUtc = null; job.NextAttemptAtUtc = null;
            return await CompareExchange(row.Id, row.메타데이터, CargoAcceptanceRecoveryMarker.Write(row.메타데이터, job), ct) == 1;
        }
        job.State = "Processing"; job.TotalAttempts = Math.Max(job.TotalAttempts, job.Attempts) + 1;
        job.Attempts++; job.Revision++; job.UpdatedAtUtc = Now; job.LeaseToken = Guid.NewGuid().ToString("N");
        job.LeaseUntilUtc = Now.AddSeconds(Math.Clamp(options.Value.LeaseSeconds, 30, 600)); job.ReasonCode = "";
        var claimed = CargoAcceptanceRecoveryMarker.Write(row.메타데이터, job);
        if (await CompareExchange(row.Id, row.메타데이터, claimed, ct) != 1) return false;
        var phase = "AcceptancePromisePreparationFailed";
        try
        {
            var attempt = 0;
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (attempt++ > 0) db.ChangeTracker.Clear();
                // 실행 전략의 롤백 뒤에는 이전 시도의 완료 flag를 재사용하지 않습니다.
                job = CargoAcceptanceRecoveryMarker.Read(claimed)!;
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                job.Revision++;
                var locked = CargoAcceptanceRecoveryMarker.Write(claimed, job);
                if (await CompareExchange(row.Id, claimed, locked, ct) != 1) throw new InvalidOperationException("AcceptanceRecoveryLeaseLost");
                var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.TransportId, ct);
                var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == row.의뢰Id, ct);
                if (!await MatchesCurrentAsync(row, job, transport, source, ct))
                { job.State = "Blocked"; job.ReasonCode = "AcceptanceGenerationChanged"; }
                else
                {
                    // 기존 서비스의 동일 의뢰 시간약속 존재 검사를 재사용합니다.
                    await continuity.시간약속잠금Async(job.DriverId, source!, ct);
                    var promiseRequired = continuityOptions?.Value.Enabled == true;
                    if (promiseRequired && !await db.화물운송시간약속.AsNoTracking()
                        .AnyAsync(x => x.의뢰Id == row.의뢰Id && x.기사Id == job.DriverId && x.잠금시각Utc >= row.이벤트시각, ct))
                        throw new InvalidOperationException("AcceptancePromiseEvidenceMissing");
                    job.PromisePrepared = true;
                    job.PromisePreparationCode = promiseRequired ? "Prepared" : "ContinuityPolicyDisabled";
                    phase = "AcceptanceRecommendationRefreshFailed";
                    if (recommendationRefresh is not null)
                    {
                        await recommendationRefresh.갱신Async(job.DriverId, ct);
                        job.RecommendationRefreshPrepared = true;
                        job.RecommendationRefreshCode = "Rechecked";
                    }
                    else job.RecommendationRefreshCode = "RefreshServiceUnavailable";
                    phase = "AcceptanceNotificationPreparationFailed";
                    db.Command알림Outbox.Add(CreateNotification(source!, job.DriverId, row.이벤트시각));
                    await db.SaveChangesAsync(ct);
                    job.NotificationPrepared = true; job.State = "Done"; job.ReasonCode = "AcceptanceFollowupsPrepared";
                }
                job.Revision++; job.UpdatedAtUtc = Now; job.LeaseToken = null; job.LeaseUntilUtc = null; job.NextAttemptAtUtc = null;
                if (await CompareExchange(row.Id, locked, CargoAcceptanceRecoveryMarker.Write(locked, job), ct) != 1)
                    throw new InvalidOperationException("AcceptanceRecoveryLeaseLost");
                await tx.CommitAsync(ct);
            });
        }
        catch (OperationCanceledException) { throw; } // lease 만료 후 같은 수락 의도로 재개합니다.
        catch (Exception)
        {
            db.ChangeTracker.Clear(); job = CargoAcceptanceRecoveryMarker.Read(claimed)!;
            job.State = job.Attempts >= Math.Clamp(options.Value.MaximumAttempts, 1, 10) ? "Failed" : "Retry";
            job.ReasonCode = phase; job.Revision++; job.UpdatedAtUtc = Now; job.LeaseToken = null; job.LeaseUntilUtc = null;
            job.NextAttemptAtUtc = job.State == "Retry" ? Now.AddSeconds(Math.Clamp(options.Value.RetrySeconds, 1, 3600)) : null;
            if (await CompareExchange(row.Id, claimed, CargoAcceptanceRecoveryMarker.Write(claimed, job), ct) == 1)
                logger.LogWarning("Cargo acceptance follow-up pending. EventId={EventId} ReasonCode={ReasonCode}", row.Id, phase);
        }
        return true;
    }

    private Task<int> CompareExchange(long id, string expected, string next, CancellationToken ct)
        => db.운송이벤트.Where(x => x.Id == id && x.이벤트타입 == CargoAcceptanceRecoveryMarker.EventType && x.메타데이터 == expected)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.메타데이터, next), ct);

    private static Command알림Outbox CreateNotification(살뜰.도메인.화주.화주운송의뢰 source, string driverId, DateTime acceptedAt)
        => 살뜰.Services.Notifications.CargoWorkflowProgressNotifications.Create(
            "cargo.accepted", source, null, driverId, acceptedAt);
}
