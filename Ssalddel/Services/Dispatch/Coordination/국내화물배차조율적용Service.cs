using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Recommendation;

namespace 살뜰.Services.Dispatch.Coordination;

public interface I국내화물배차조율적용Service
{
    Task<국내화물배차조율적용결과> 추천잠금적용Async(
        국내화물배차조율결과 조율결과,
        int? timeoutSeconds = null,
        int 기사동시추천잠금건수 = 1,
        CancellationToken cancellationToken = default);
}

public sealed partial class 국내화물배차조율적용Service : I국내화물배차조율적용Service
{
    private readonly SsalddelContext _db;
    private readonly 배차큐정책Options _options;
    private readonly I배차추천알림Service _알림Service;
    private readonly I국내화물운송기사상태Service _기사상태Service;
    private readonly I국내화물배달권실행공간Store _배달권실행공간Store;
    private readonly ILogger<국내화물배차조율적용Service> _logger;
    private readonly I신규화물업무자격Guard? _신규업무자격;
    private readonly I화물배차상태SnapshotService? _상태근거;
    private readonly I운영배차공통UseCase? _수신;
    private readonly ISharedDriverWorkAdmissionService? _참여;
    private readonly I화물배차수락적격성Service? _적격성;
    private readonly TimeProvider _clock;

    public 국내화물배차조율적용Service(
        SsalddelContext db,
        IOptions<배차큐정책Options> options,
        I배차추천알림Service 알림Service,
        I국내화물운송기사상태Service 기사상태Service,
        I국내화물배달권실행공간Store 배달권실행공간Store,
        ILogger<국내화물배차조율적용Service> logger,
        I신규화물업무자격Guard? 신규업무자격 = null,
        I화물배차상태SnapshotService? 상태근거 = null,
        I운영배차공통UseCase? 수신 = null,
        ISharedDriverWorkAdmissionService? 참여 = null,
        I화물배차수락적격성Service? 적격성 = null,
        TimeProvider? clock = null)
    {
        _db = db;
        _options = options.Value;
        _알림Service = 알림Service;
        _기사상태Service = 기사상태Service;
        _배달권실행공간Store = 배달권실행공간Store;
        _logger = logger;
        _신규업무자격 = 신규업무자격;
        _상태근거 = 상태근거;
        _수신 = 수신;
        _참여 = 참여;
        _적격성 = 적격성;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<국내화물배차조율적용결과> 추천잠금적용Async(
        국내화물배차조율결과 조율결과,
        int? timeoutSeconds = null,
        int 기사동시추천잠금건수 = 1,
        CancellationToken cancellationToken = default)
    {
        var 잠금목록 = new List<국내화물배차추천잠금>();
        var 실패목록 = new List<국내화물배차추천잠금실패>();

        foreach (var 배차제안 in 조율결과.추천배정목록)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var plans = 조율결과.검증계획목록.Where(x => x.계획Id == 배차제안.수행계획Id).Take(2).ToArray();
                var 적용된잠금 = await 추천잠금시도Async(
                    배차제안,
                    plans.Length == 1 ? plans[0] : null,
                    조율결과.판정근거?.입력지문,
                    timeoutSeconds,
                    Math.Max(1, 기사동시추천잠금건수),
                    cancellationToken);
                if (적용된잠금 is null)
                {
                    실패목록.Add(new 국내화물배차추천잠금실패(
                        배차제안.의뢰Id,
                        배차제안.기사Id,
                        "현재 의뢰·기사 자격 또는 추천 상태가 유효하지 않아 잠금 대상에서 제외되었습니다."));
                }
                else
                {
                    잠금목록.Add(적용된잠금);
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogInformation(ex, "다량 배차 추천 잠금 중 동시성 충돌이 발생했습니다. RequestId={RequestId} DriverId={DriverId}", 배차제안.의뢰Id, 배차제안.기사Id);
                실패목록.Add(new 국내화물배차추천잠금실패(
                    배차제안.의뢰Id,
                    배차제안.기사Id,
                    "다른 배차 루프가 먼저 추천 또는 확정했습니다."));
            }
        }

        return new 국내화물배차조율적용결과(DateTime.UtcNow, 잠금목록, 실패목록);
    }

}
