using Microsoft.Extensions.Options;
using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.도메인.공통;

namespace 살뜰.Infrastructure.BackgroundJobs.FoodDeliveryDispatch;

[DisallowConcurrentExecution]
public sealed class 음식배달추천알림발송Job(
    I배차추천알림Service notifications,
    ISsalddelBackgroundJobActivationPolicy activationPolicy,
    IOptions<음식배달배차배치작업Options> options,
    ILogger<음식배달추천알림발송Job> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var activation = activationPolicy.Evaluate(SsalddelBackgroundWorkloadKeys.FoodDeliveryDispatch);
        if (!activation.IsEnabled)
        {
            logger.LogDebug("Action={Action} ActivationCode={ActivationCode} FeatureKey={FeatureKey}",
                "FoodDeliveryRecommendationPushSkipped", activation.Code, activation.FeatureKey);
            return;
        }

        var cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        var processed = await notifications.업무유형별대기알림발송Async(
            상태값.배차업무유형.음식배달,
            Math.Max(1, options.Value.처리배치크기),
            cancellationToken);
        logger.LogDebug("Action={Action} ProcessedCount={ProcessedCount} OccurredAt={OccurredAt}",
            "FoodDeliveryRecommendationPushSent", processed, DateTime.UtcNow);
    }
}
