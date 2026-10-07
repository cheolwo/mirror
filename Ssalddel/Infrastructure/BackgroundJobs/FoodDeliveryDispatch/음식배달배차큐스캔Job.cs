using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;

namespace 살뜰.Infrastructure.BackgroundJobs.FoodDeliveryDispatch;

/// <summary>음식 큐만 기존 서버 전환 Port로 전달하며 공개 화물 배차를 수행하지 않습니다.</summary>
[DisallowConcurrentExecution]
public sealed class 음식배달배차큐스캔Job(
    SsalddelContext db,
    I배차대기원장전환Service transition,
    ISsalddelBackgroundJobActivationPolicy activationPolicy,
    IOptions<음식배달배차배치작업Options> options,
    ILogger<음식배달배차큐스캔Job> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var activation = activationPolicy.Evaluate(SsalddelBackgroundWorkloadKeys.FoodDeliveryDispatch);
        if (!activation.IsEnabled)
        {
            logger.LogDebug("Action={Action} ActivationCode={ActivationCode} FeatureKey={FeatureKey}",
                "FoodDeliveryDispatchQueueScanSkipped", activation.Code, activation.FeatureKey);
            return;
        }

        var cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        var batchSize = Math.Max(1, options.Value.처리배치크기);
        var retryBefore = DateTime.UtcNow.AddSeconds(-Math.Max(1, options.Value.후보재탐색간격초));

        // 피킹·포장 전 마트 주문을 계획 큐에서 자동으로 꺼내지 않습니다.
        var plannedIds = await db.운송원장.AsNoTracking()
            .Where(queue => queue.배차업무유형 == 상태값.배차업무유형.음식배달
                            && queue.상태 == 상태값.배차대기상태.대기
                            && queue.배차큐단계 == 상태값.배차큐단계.계획배차
                            && queue.배차노출상태 == 상태값.배차노출상태.계획대기
                            && queue.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트주문
                            && queue.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트음식주문)
            .OrderBy(queue => queue.CreatedAt)
            .ThenBy(queue => queue.Id)
            .Select(queue => queue.의뢰Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var requestId in plannedIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await transition.계획배차에서추천으로전환Async(requestId, cancellationToken);
        }

        var waitingIds = await db.운송원장.AsNoTracking()
            .Where(queue => queue.배차업무유형 == 상태값.배차업무유형.음식배달
                            && queue.상태 == 상태값.배차대기상태.대기
                            && queue.배차큐단계 == 상태값.배차큐단계.배차추천
                            && queue.현재추천대상기사Id == null
                            && ((queue.배차노출상태 == 상태값.배차노출상태.추천대기
                                 && queue.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트주문
                                 && queue.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트음식주문)
                                || (queue.배차노출상태 == 상태값.배차노출상태.추천후보없음
                                    && queue.UpdatedAt <= retryBefore)))
            .OrderBy(queue => queue.UpdatedAt)
            .ThenBy(queue => queue.Id)
            .Select(queue => queue.의뢰Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var requestId in waitingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await transition.추천대기처리Async(requestId, cancellationToken);
        }

        logger.LogDebug("Action={Action} PlannedCount={PlannedCount} WaitingCount={WaitingCount} OccurredAt={OccurredAt}",
            "FoodDeliveryDispatchQueueScanned", plannedIds.Count, waitingIds.Count, DateTime.UtcNow);
    }
}
