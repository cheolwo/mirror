using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.도메인.공통;
using 살뜰.Services.Dispatch.Notification;

namespace 살뜰.Infrastructure.BackgroundJobs.DispatchQueue
{
    [DisallowConcurrentExecution]
    public sealed class 배차추천알림발송Job : IJob
    {
        private readonly I배차추천알림Service _notificationService;
        private readonly ISsalddelBackgroundJobActivationPolicy _activationPolicy;
        private readonly 배차큐배치작업Options _options;
        private readonly ILogger<배차추천알림발송Job> _logger;

        public 배차추천알림발송Job(
            I배차추천알림Service notificationService,
            ISsalddelBackgroundJobActivationPolicy activationPolicy,
            Microsoft.Extensions.Options.IOptions<배차큐배치작업Options> options,
            ILogger<배차추천알림발송Job> logger)
        {
            _notificationService = notificationService;
            _activationPolicy = activationPolicy;
            _options = options.Value;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var activation = _activationPolicy.Evaluate(
                SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch);
            if (!activation.IsEnabled)
            {
                _logger.LogDebug(
                    "Action={Action} ActivationCode={ActivationCode} FeatureKey={FeatureKey}",
                    "DispatchRecommendationPushSkipped",
                    activation.Code,
                    activation.FeatureKey);
                return;
            }

            var processed = await _notificationService.업무유형별대기알림발송Async(
                상태값.배차업무유형.용달운송, _options.처리배치크기, context.CancellationToken);
            _logger.LogDebug("Action={Action} ProcessedCount={ProcessedCount} OccurredAt={OccurredAt}",
                "DispatchRecommendationPushSent",
                processed,
                DateTime.UtcNow);
        }
    }
}
