using Microsoft.EntityFrameworkCore;
using Quartz;
using Ssalddel.Infrastructure.BackgroundJobs;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Infrastructure.BackgroundJobs.DispatchQueue
{
    [DisallowConcurrentExecution]
    public sealed class 배차큐스캔Job : IJob
    {
        private readonly SsalddelContext _db;
        private readonly I배차대기원장전환Service _원장전환Service;
        private readonly ISsalddelBackgroundJobActivationPolicy _activationPolicy;
        private readonly 배차큐배치작업Options _options;
        private readonly 배차큐정책Options _queuePolicyOptions;
        private readonly ILogger<배차큐스캔Job> _logger;

        public 배차큐스캔Job(
            SsalddelContext db,
            I배차대기원장전환Service 원장전환Service,
            ISsalddelBackgroundJobActivationPolicy activationPolicy,
            Microsoft.Extensions.Options.IOptions<배차큐배치작업Options> options,
            Microsoft.Extensions.Options.IOptions<배차큐정책Options> queuePolicyOptions,
            ILogger<배차큐스캔Job> logger)
        {
            _db = db;
            _원장전환Service = 원장전환Service;
            _activationPolicy = activationPolicy;
            _options = options.Value;
            _queuePolicyOptions = queuePolicyOptions.Value;
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
                    "DispatchQueueScanSkipped",
                    activation.Code,
                    activation.FeatureKey);
                return;
            }

            var cancellationToken = context.CancellationToken;
            var 자동공개전환Count = await 공개전환기한초과처리Async(cancellationToken);

            var plannedIds = await _db.운송원장.AsNoTracking()
                .Where(x => x.상태 == 상태값.배차대기상태.대기
                            && x.배차업무유형 == 상태값.배차업무유형.용달운송
                            && x.생활배송수락준비완료
                            && x.배차큐단계 == 상태값.배차큐단계.계획배차
                            && x.배차노출상태 == 상태값.배차노출상태.계획대기
                            && x.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트주문
                            && x.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트음식주문)
                .OrderBy(x => x.CreatedAt)
                .Select(x => x.의뢰Id)
                .Take(_options.처리배치크기)
                .ToListAsync(cancellationToken);

            foreach (var requestId in plannedIds)
            {
                await _원장전환Service.계획배차에서추천으로전환Async(requestId, cancellationToken);
            }

            var retryBefore = DateTime.UtcNow.AddSeconds(-Math.Max(1, _queuePolicyOptions.음식배달후보재탐색간격초));
            var recommendWaitingIds = await _db.운송원장.AsNoTracking()
                .Where(x => x.상태 == 상태값.배차대기상태.대기
                            && x.배차업무유형 == 상태값.배차업무유형.용달운송
                            && x.배차큐단계 == 상태값.배차큐단계.배차추천
                            && x.현재추천대상기사Id == null
                            && (x.배차노출상태 == 상태값.배차노출상태.추천대기
                                || ((x.생활배송배차방식 == Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Automatic
                                        || x.생활배송배차방식 == Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Hybrid)
                                    && x.배차노출상태 == 상태값.배차노출상태.추천후보없음 && x.UpdatedAt <= retryBefore))
                            && x.생활배송수락준비완료
                            && x.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트주문
                            && x.원본의뢰유형 != 운송의뢰배차원천유형.살뜰마트음식주문)
                .OrderBy(x => x.UpdatedAt)
                .Select(x => x.의뢰Id)
                .Take(_options.처리배치크기)
                .ToListAsync(cancellationToken);

            foreach (var requestId in recommendWaitingIds)
            {
                await _원장전환Service.추천대기처리Async(requestId, cancellationToken);
            }

            var cargoWaitingCount = await _db.운송원장.AsNoTracking()
                .CountAsync(x => x.상태 == 상태값.배차대기상태.대기
                                 && x.배차업무유형 == 상태값.배차업무유형.용달운송
                                 && x.배차큐단계 == 상태값.배차큐단계.배차추천
                                 && x.배차노출상태 == 상태값.배차노출상태.추천대기,
                    cancellationToken);

            _logger.LogDebug("Action={Action} AutoPublicCount={AutoPublicCount} PlannedCount={PlannedCount} RecommendWaitingCount={RecommendWaitingCount} CargoWaitingCount={CargoWaitingCount} OccurredAt={OccurredAt}",
                "DispatchQueueScanned",
                자동공개전환Count,
                plannedIds.Count,
                recommendWaitingIds.Count,
                cargoWaitingCount,
                DateTime.UtcNow);
        }

        private async Task<int> 공개전환기한초과처리Async(CancellationToken cancellationToken)
        {
            if (_queuePolicyOptions.당일미배정공개전환분 <= 0)
            {
                return 0;
            }

            var now = DateTime.UtcNow;
            var immediateDeadline = now.AddMinutes(-_queuePolicyOptions.당일미배정공개전환분);
            var scanLimit = Math.Max(_options.처리배치크기, _options.처리배치크기 * 5);

            var candidates = await (
                from queue in _db.운송원장.AsNoTracking()
                join request in _db.화주운송의뢰.AsNoTracking()
                    on queue.의뢰Id equals request.의뢰Id into requestGroup
                from request in requestGroup.DefaultIfEmpty()
                where queue.상태 == 상태값.배차대기상태.대기
                      && queue.배차업무유형 == 상태값.배차업무유형.용달운송
                      && queue.배차큐단계 != 상태값.배차큐단계.공개배차
                      && queue.배차큐단계 != 상태값.배차큐단계.확정
                      && queue.배차큐단계 != 상태값.배차큐단계.종료
                      && queue.배차노출상태 != 상태값.배차노출상태.공개중
                      && queue.생활배송수락준비완료
                      && queue.생활배송배차방식 == null
                      && queue.확정기사Id == null
                      && queue.CreatedAt <= immediateDeadline
                orderby queue.CreatedAt
                select new
                {
                    queue.의뢰Id,
                    queue.CreatedAt,
                    상차시간창시작Utc = request == null ? (DateTime?)null : request.픽업_시간창_시작일시
                })
                .Take(scanLimit)
                .ToListAsync(cancellationToken);

            var overdueRequestIds = candidates
                .Where(x => 배차공개전환시점정책.공개전환대상(
                    now,
                    x.CreatedAt,
                    x.상차시간창시작Utc,
                    _queuePolicyOptions.당일미배정공개전환분,
                    _queuePolicyOptions.예약상차전공개전환시간,
                    _queuePolicyOptions.예약최소추천유지분))
                .Select(x => x.의뢰Id)
                .Take(_options.처리배치크기)
                .ToArray();

            foreach (var requestId in overdueRequestIds)
            {
                await _원장전환Service.공개배차로전환Async(requestId, cancellationToken);
            }

            return overdueRequestIds.Length;
        }
    }
}
