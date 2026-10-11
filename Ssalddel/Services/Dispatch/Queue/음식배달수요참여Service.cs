using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Transport;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Queue;

public sealed class 음식배달수요참여Options
{
    public bool Enabled { get; set; }
    public int 관측시간창분 { get; set; } = 15;
    public int 수행관측시간창분 { get; set; } = 60;
    public int 최대조회건수 { get; set; } = 5000;
    public int 최대권역기사수 { get; set; } = 500;
    public decimal 주문지연주의분 { get; set; } = 10m;
    public 음식배달수요참여설정 정책 { get; set; } = new();

    public bool 유효한가() => 관측시간창분 is > 0 and <= 60
        && 수행관측시간창분 is > 0 and <= 240 && 최대조회건수 is > 0 and <= 20000
        && 최대권역기사수 is > 0 and <= 2000 && 주문지연주의분 > 0m
        && 정책 is not null && 정책.시간당목표처리건수 is > 0m and <= 20m
        && 정책.계획시간창분 is > 0m and <= 120m
        && 정책.최대수요관측경과분 is > 0m and <= 30m
        && 정책.최대기사관측경과분 is > 0m and <= 30m
        && 정책.장기대기기회분 > 0m && 정책.장기대기추가인원 is >= 0 and <= 20
        && 정책.주문지연추가인원 is >= 0 and <= 20;
}

public interface I음식배달수요참여Service
{
    Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken cancellationToken = default);
}

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "음식 권역의 정본 주문·근무·수락을 읽어 신규 추천의 참여 순서를 계산한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall, FlowOrder = 45,
    StepKey = "application.food-demand-participation",
    DependsOnStepKeys = ["domain.food-demand-participation-policy"],
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "수신 상태 조회가 정본 기반 캐시를 복구할 수 있다. 업무 원장은 읽기만 하며 영속 정원·운행 종료·전역 수신 OFF·유효 제안 취소·수락·지급 또는 LLM 호출은 수행하지 않는다.")]
public sealed class 음식배달수요참여Service(
    SsalddelContext db, I음식배달권실행공간Store spaces,
    I국내화물운송기사상태Store driverStates, I운영배차공통UseCase availability,
    IOptions<음식배달수요참여Options> options, 음식배달수요참여Policy policy,
    ILogger<음식배달수요참여Service> logger, TimeProvider timeProvider) : I음식배달수요참여Service
{
    public async Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || queue.배차업무유형 != 상태값.배차업무유형.음식배달) return null;
        if (!settings.유효한가()) throw new InvalidOperationException("FoodDemandParticipationConfigurationInvalid");
        var now = timeProvider.GetUtcNow();
        var region = queue.픽업_위도.HasValue && queue.픽업_경도.HasValue
            ? 음식배달권정책.판정(new(queue.픽업_위도.Value, queue.픽업_경도.Value), queue.픽업_도로명주소).배달권키
            : "unknown";
        if (region == "unknown") return Unknown(region, now, "FoodDemandRegionUnknown");
        try
        {
            var keys = 음식배달권정책.인접배달권키조회(region).Append(region).ToHashSet(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var space = await spaces.GetAsync(key, cancellationToken);
                if (space is not null) ids.UnionWith(space.운행중기사Ids);
            }
            if (ids.Count > settings.최대권역기사수)
                return Unknown(region, now, "FoodDemandDriverSnapshotTruncated");

            var observedFrom = now.UtcDateTime.AddMinutes(-settings.관측시간창분);
            // 공간 인덱스의 갱신 시각은 수요의 신선도가 아니다. 새 DB 조회의 실행 투영을 사용한다.
            var rows = await db.운송원장.AsNoTracking()
                .Where(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                    && (x.CreatedAt >= observedFrom || x.상태 == 상태값.배차대기상태.대기))
                .OrderBy(x => x.Id).Take(settings.최대조회건수 + 1).ToListAsync(cancellationToken);
            if (rows.Count > settings.최대조회건수)
                return Unknown(region, now, "FoodDemandOrderSnapshotTruncated");
            var regional = rows.Where(x => x.픽업_위도.HasValue && x.픽업_경도.HasValue
                && keys.Contains(음식배달권정책.판정(new(x.픽업_위도!.Value, x.픽업_경도!.Value), x.픽업_도로명주소).배달권키))
                .GroupBy(x => string.IsNullOrWhiteSpace(x.원본의뢰Id) ? x.의뢰Id : x.원본의뢰Id, StringComparer.Ordinal)
                .Select(x => x.OrderByDescending(y => y.Id).First()).ToArray();
            if (!regional.Any(x => x.의뢰Id == queue.의뢰Id))
                return Unknown(region, now, "FoodDemandCurrentQueueNotObserved");
            var pending = regional.Where(x => 배차실행인덱스재구성정책.미처리운송의뢰인가(x, now.UtcDateTime)).ToArray();

            var idArray = ids.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var shifts = await db.Set<기사근무>().AsNoTracking()
                .Where(x => Enumerable.Contains(idArray, x.기사Id) && x.ActiveDriverId == x.기사Id && x.StoppedAtUtc == null
                    && x.운송실행유형 == 운송실행유형코드.음식배달)
                .ToListAsync(cancellationToken);
            var activityFrom = now.UtcDateTime.AddMinutes(-settings.수행관측시간창분);
            var attempts = await db.음식배달시도.AsNoTracking()
                .Where(x => Enumerable.Contains(idArray, x.기사Id) && x.수락시각Utc <= now.UtcDateTime
                    && (x.수락시각Utc >= activityFrom || (x.중단시각Utc == null && x.전달완료시각Utc == null)))
                .OrderBy(x => x.Id).Take(settings.최대조회건수 + 1).ToListAsync(cancellationToken);
            if (attempts.Count > settings.최대조회건수)
                return Unknown(region, now, "FoodDemandActivitySnapshotTruncated");

            var inputs = new List<음식배달수요기사입력>();
            foreach (var id in idArray)
            {
                var state = await driverStates.GetAsync(id, cancellationToken);
                if (state is null || state.AppKey != 기사앱식별자.FoodDeliveryDriverApp) continue;
                var shift = shifts.SingleOrDefault(x => x.Id == state.ShiftId && x.기사Id == id);
                var intent = await availability.수신상태조회Async(id, cancellationToken);
                var accepted = attempts.Where(x => x.기사Id == id && !x.유산추정여부)
                    .GroupBy(x => x.주문번호, StringComparer.Ordinal)
                    .Select(x => x.OrderByDescending(y => y.시도순번).ThenByDescending(y => y.Id).First()).ToArray();
                var active = accepted.Where(x => x.중단시각Utc == null && x.전달완료시각Utc == null).ToArray();
                var balanced = accepted.Where(x => x.수락시각Utc >= activityFrom
                    && (x.중단시각Utc == null || x.책임Code == 운영배차책임Code.기사)).ToArray();
                var held = regional.Count(x => x.현재추천대상기사Id == id
                    && 배차실행인덱스재구성정책.유효한추천중잠금인가(x, now.UtcDateTime));
                var lastAcceptance = balanced.Select(x => (DateTime?)x.수락시각Utc).Max();
                var lastCompletion = accepted.Select(x => x.전달완료시각Utc).Where(x => x.HasValue).DefaultIfEmpty().Max();
                var waitStart = new[] { shift?.시작시각, lastAcceptance, lastCompletion }.Where(x => x.HasValue).Max();
                // 서비스 시간은 설정 처리량의 보수적 근사다. 실제 경로 예측이나 LLM 확정값이 아니다.
                var remaining = active.Sum(x => 60m / settings.정책.시간당목표처리건수
                    * (x.픽업완료시각Utc.HasValue ? 0.5m : 1m)) + held * 60m / settings.정책.시간당목표처리건수;
                inputs.Add(new()
                {
                    기사Id = id, 기존활동여부 = active.Length > 0 || balanced.Length > 0 || held > 0,
                    운행시작시각Utc = Utc(shift?.시작시각), 대기시작시각Utc = Utc(waitStart),
                    마지막관측시각Utc = Utc(state.위치수신시각Utc), 남은수행시간분 = remaining,
                    최근시간창배차수 = balanced.Length,
                    적격여부 = shift is not null && shift.시작시각.HasValue && shift.시작시각 <= now.UtcDateTime
                        && state.운행상태 == 상태값.기사운행상태.운행중 && state.Latitude.HasValue && state.Longitude.HasValue
                        && 기사위치신선도정책.유효한가(state.위치수신시각Utc, now.UtcDateTime, (int)settings.정책.최대기사관측경과분)
                        && 운영배차수신상태Policy.신규배차수신허용(intent)
                        && 음식배달기사활성업무Policy.수락가능한가(active.Length, 1)
                });
            }
            return policy.평가(new()
            {
                권역Key = region, 판정시각Utc = timeProvider.GetUtcNow(), 수요관측시각Utc = now,
                대기주문수 = pending.Length,
                최근시간창신규주문수 = regional.Count(x => x.CreatedAt >= observedFrom && x.CreatedAt <= now.UtcDateTime
                    && x.상태 != 상태값.배차상태.취소),
                관측시간창분 = settings.관측시간창분,
                지연위험주문존재 = pending.Any(x => x.CreatedAt <= now.UtcDateTime.AddMinutes(-(double)settings.주문지연주의분)),
                적격활동기사부재 = !inputs.Any(x => x.적격여부 && x.기존활동여부), 기사목록 = inputs
            }, settings.정책);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is System.Data.Common.DbException or TimeoutException or InvalidOperationException
            or StackExchange.Redis.RedisException or OperationCanceledException)
        {
            logger.LogWarning("Food demand snapshot unavailable; existing eligibility gates remain. FailureType={FailureType}", ex.GetType().Name);
            return Unknown(region, now, "FoodDemandSnapshotReadFailed");
        }
    }

    private static DateTimeOffset? Utc(DateTime? value) => value.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;

    private static 음식배달수요참여결과 Unknown(string region, DateTimeOffset now, string reason)
        => new() { 권역Key = region, 판정시각Utc = now, 정책판본 = 음식배달수요참여Policy.현재정책판본,
            데이터상태Code = 음식배달수요자료상태Code.미확인, 사유Code = reason };
}
