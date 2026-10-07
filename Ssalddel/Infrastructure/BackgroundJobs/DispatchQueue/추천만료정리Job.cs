using Microsoft.EntityFrameworkCore;
using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Infrastructure.BackgroundJobs.DispatchQueue
{
    [DisallowConcurrentExecution]
    public sealed class 추천만료정리Job : IJob
    {
        private readonly SsalddelContext _db;
        private readonly I배차대기원장전환Service _원장전환Service;
        private readonly ISsalddelBackgroundJobActivationPolicy _activationPolicy;
        private readonly 배차큐배치작업Options _options;
        private readonly ILogger<추천만료정리Job> _logger;

        public 추천만료정리Job(
            SsalddelContext db,
            I배차대기원장전환Service 원장전환Service,
            ISsalddelBackgroundJobActivationPolicy activationPolicy,
            Microsoft.Extensions.Options.IOptions<배차큐배치작업Options> options,
            ILogger<추천만료정리Job> logger)
        {
            _db = db;
            _원장전환Service = 원장전환Service;
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
                    "DispatchRecommendationExpirySkipped",
                    activation.Code,
                    activation.FeatureKey);
                return;
            }

            var cancellationToken = context.CancellationToken;
            var now = DateTime.UtcNow;

            var expiredRequestIds = await _db.운송원장.AsNoTracking()
                .Where(x => x.상태 == 상태값.배차대기상태.대기
                            && x.배차업무유형 == 상태값.배차업무유형.용달운송
                            && x.배차큐단계 == 상태값.배차큐단계.배차추천
                            && x.배차노출상태 == 상태값.배차노출상태.추천중
                            && x.추천만료시각.HasValue
                            && x.추천만료시각 <= now)
                .OrderBy(x => x.추천만료시각)
                .Select(x => x.의뢰Id)
                .Take(_options.처리배치크기)
                .ToListAsync(cancellationToken);

            foreach (var requestId in expiredRequestIds)
            {
                await _원장전환Service.추천만료처리Async(requestId, cancellationToken);
            }

            _logger.LogDebug("Action={Action} ExpiredCount={ExpiredCount} OccurredAt={OccurredAt}",
                "DispatchRecommendationExpiredCleaned",
                expiredRequestIds.Count,
                now);
        }
    }
}
