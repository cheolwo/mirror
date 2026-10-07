using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;

namespace 살뜰.Infrastructure.BackgroundJobs.FoodDeliveryDispatch;

[DisallowConcurrentExecution]
public sealed class 음식배달추천만료정리Job(
    SsalddelContext db,
    I배차대기원장전환Service transition,
    ISsalddelBackgroundJobActivationPolicy activationPolicy,
    IOptions<음식배달배차배치작업Options> options,
    ILogger<음식배달추천만료정리Job> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var activation = activationPolicy.Evaluate(SsalddelBackgroundWorkloadKeys.FoodDeliveryDispatch);
        if (!activation.IsEnabled)
        {
            logger.LogDebug("Action={Action} ActivationCode={ActivationCode} FeatureKey={FeatureKey}",
                "FoodDeliveryRecommendationExpirySkipped", activation.Code, activation.FeatureKey);
            return;
        }

        var cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTime.UtcNow;
        var expiredIds = await db.운송원장.AsNoTracking()
            .Where(queue => queue.배차업무유형 == 상태값.배차업무유형.음식배달
                            && queue.상태 == 상태값.배차대기상태.대기
                            && queue.배차큐단계 == 상태값.배차큐단계.배차추천
                            && queue.배차노출상태 == 상태값.배차노출상태.추천중
                            && queue.추천만료시각.HasValue
                            && queue.추천만료시각 <= now)
            .OrderBy(queue => queue.추천만료시각)
            .ThenBy(queue => queue.Id)
            .Select(queue => queue.의뢰Id)
            .Take(Math.Max(1, options.Value.처리배치크기))
            .ToListAsync(cancellationToken);

        foreach (var requestId in expiredIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await transition.추천만료처리Async(requestId, cancellationToken);
        }

        logger.LogDebug("Action={Action} ExpiredCount={ExpiredCount} OccurredAt={OccurredAt}",
            "FoodDeliveryRecommendationExpiredCleaned", expiredIds.Count, now);
    }
}
