using Ssalddel.Contracts.Common.Metadata;

using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Work;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;

namespace 살뜰.Services.Dispatch.Queue;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "근무 식별자 기준 운행 시작 인덱스 재구성 계약을 정의한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 19,
    StepKey = "contract.work-start-followup-port",
    DependsOnStepKeys = ["contract.work-start-response"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "운행 시작 정본을 만들거나 수신 의사를 변경하는 계약이 아니다.")]
public interface I기사운행시작후속Service
{
    Task<bool> 처리Async(long shiftId, CancellationToken ct = default);
    Task<int> 대기처리Async(CancellationToken ct = default);
}

/// <summary>기존 SQL 근무만 인덱스로 재구성합니다. 배차 추천이나 수신 의사를 변경하지 않습니다.</summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Infrastructure,
    "SQL의 현재 근무를 실행 인덱스로 재구성하고 실패 재시도를 보존한다.",
    ContractType = typeof(I기사운행시작후속Service),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation,
    FlowOrder = 150,
    StepKey = "infrastructure.work-start-followup",
    DependsOnStepKeys = ["application.work-start", "application.work-transition-lock", "infrastructure.work-start-followup-mapping", "contract.work-start-followup-port"],
    ExecutionStage = SsalddelCodeExecutionStage.Projection,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "종료·새 근무 뒤의 오래된 작업은 인덱스를 되살리지 않는다. 같은 근무 GPS·Aging을 초기화하지 않으며 수신 의사·추천은 변경하지 않는다. SQL과 Redis는 별도 경계다.")]
public sealed class 기사운행시작후속Service(
    SsalddelContext db,
    IDriverWorkQueueStore queue,
    I국내화물운송기사상태Service states,
    I음식배달권실행공간Store foodScopes,
    I국내화물배달권실행공간Store cargoScopes,
    ILogger<기사운행시작후속Service> logger,
    TimeProvider? timeProvider = null,
    I국내화물운송기사상태Store? stateStore = null) : I기사운행시작후속Service
{
    private TimeProvider Clock => timeProvider ?? TimeProvider.System;

    public async Task<int> 대기처리Async(CancellationToken ct = default)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var ids = await db.Set<기사운행시작후속>().AsNoTracking()
            .Where(x => x.Status == OutboxProcessingStatuses.Pending
                && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
            .OrderBy(x => x.CreatedAtUtc).Select(x => x.ShiftId).Take(100).ToArrayAsync(ct);
        var completed = 0;
        foreach (var id in ids)
            if (await 처리Async(id, ct)) completed++;
        return completed;
    }

    public async Task<bool> 처리Async(long shiftId, CancellationToken ct = default)
    {
        var driverId = await db.Set<기사운행시작후속>().AsNoTracking()
            .Where(x => x.ShiftId == shiftId).Select(x => x.DriverId).SingleOrDefaultAsync(ct);
        if (driverId is null) return true;
        using var gate = await 기사운행전이Lock.진입Async(driverId, ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(ct) : null;
            await 기사운행전이Lock.행잠금Async(db, driverId, ct);
            // 같은 scoped Context에 남은 이전 값도 재시도 시 정본을 대신하지 않습니다.
            var trackedItem = db.ChangeTracker.Entries<기사운행시작후속>().FirstOrDefault(x => x.Entity.ShiftId == shiftId);
            if (trackedItem is not null) trackedItem.State = EntityState.Detached;
            var item = await db.Set<기사운행시작후속>().SingleOrDefaultAsync(x => x.ShiftId == shiftId, ct);
            if (item is null || item.Status == OutboxProcessingStatuses.Succeeded) return true;
            if (item.Status == OutboxProcessingStatuses.Failed || item.NextAttemptAtUtc > Clock.GetUtcNow().UtcDateTime) return false;
            var driver = await db.용달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == driverId, ct);
            var shift = await db.기사근무.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shiftId, ct);
            var latestId = await db.기사근무.AsNoTracking().Where(x => x.기사Id == driverId)
                .OrderByDescending(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync(ct);
            var now = Clock.GetUtcNow().UtcDateTime;
            if (driver is null || shift is null || driver.운행상태 != 상태값.기사운행상태.운행중
                || shift.StoppedAtUtc.HasValue || latestId != shiftId || shift.ActiveDriverId != driverId)
            {
                item.Status = OutboxProcessingStatuses.Succeeded;
                item.LastFailureCode = "WorkNoLongerCurrent";
                item.CompletedAtUtc = now;
            }
            else
            {
                item.AttemptCount++;
                try
                {
                    using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attemptTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                    var indexCt = attemptTimeout.Token;
                    await queue.UpsertAsync(new DriverWorkQueueEntry(driverId, shift.Id, shift.CreatedAt,
                        shift.시작모드, shift.시작위치, shift.오늘의복귀지주소 ?? shift.복귀지), indexCt);
                    var currentIndex = stateStore is null ? null : await stateStore.GetAsync(driverId, indexCt);
                    if (currentIndex?.ShiftId != shift.Id)
                    {
                        await states.운행시작Async(driverId, shift.Id, shift.시작시각 ?? shift.CreatedAt,
                            shift.시작모드, shift.시작위치, shift.오늘의복귀지주소 ?? shift.복귀지,
                            shift.StartReturnPreference,
                            shift.운송실행유형 == 운송실행유형코드.음식배달
                                ? 기사앱식별자.FoodDeliveryDriverApp : 기사앱식별자.CargoYongdalDriverApp, indexCt);
                    }
                    // 상태 Upsert 뒤 공간 정리에 실패했더라도 다음 시도에서 남은 정리를 끝냅니다.
                    // 같은 근무의 GPS·Aging 상태를 초기화하지 않는 판정과는 별도입니다.
                    await foodScopes.Remove기사Async(driverId, indexCt);
                    await cargoScopes.Remove기사Async(driverId, indexCt);
                    item.Status = OutboxProcessingStatuses.Succeeded;
                    item.CompletedAtUtc = now;
                    item.NextAttemptAtUtc = null;
                    item.LastFailureCode = null;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    item.Status = OutboxProcessingPolicy.CanRetry(item.AttemptCount)
                        ? OutboxProcessingStatuses.Pending : OutboxProcessingStatuses.Failed;
                    item.NextAttemptAtUtc = now + OutboxProcessingPolicy.RetryDelay;
                    item.LastFailureCode = "WorkIndexUnavailable";
                    logger.LogWarning(ex, "근무 확정 후 인덱스 재구성을 재처리합니다. ShiftId={ShiftId}", shiftId);
                }
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return item.Status == OutboxProcessingStatuses.Succeeded;
        });
    }
}
