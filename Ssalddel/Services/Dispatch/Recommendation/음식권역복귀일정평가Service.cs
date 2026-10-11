using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Recommendation;

public interface I음식권역복귀일정평가Service
{
    Task<음식권역복귀일정평가결과> 평가Async(string driverId, 운송원장 candidate,
        배차경로좌표 current, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<음식후속픽업기준결과> 후속픽업기준조회Async(string driverId, 운송원장 candidate,
        배차경로좌표 current, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(음식후속픽업기준결과.보류("ActiveScheduleServiceUnsupported"));
}

/// <summary>기존 업무의 완료 예상 위치다. 실제 현재 GPS나 수락·픽업 확정이 아니다.</summary>
public sealed record 음식후속픽업기준결과(bool 활성업무있음, bool 확인됨, 배차경로좌표? 기준좌표,
    DateTime? 가용예상시각Utc, string? RouteOption, string 사유Code)
{
    public static 음식후속픽업기준결과 보류(string code) => new(true, false, null, null, null, code);
}

public sealed record 음식권역복귀일정평가결과(배차경로좌표? 기존마지막좌표,
    배차경로좌표? 후보마지막좌표, bool 전체실경로확인됨, bool 적격여부, string? RouteOption, string 사유)
{
    public static 음식권역복귀일정평가결과 보류(string reason) => new(null, null, false, false, null, reason);
}

/// <summary>음식 원장을 읽어 남은 전체 작업만 평가한다. 배정·수락·원장 상태를 변경하지 않는다.</summary>
public sealed class 음식권역복귀일정평가Service(
    SsalddelContext db, I픽업하차경로최적화Service optimizer,
    IOptions<배차큐정책Options> queueOptions, IOptions<FoodDriverRoutePricingOptions> routeOptions)
    : I음식권역복귀일정평가Service
{
    public async Task<음식권역복귀일정평가결과> 평가Async(string driverId, 운송원장 candidate,
        배차경로좌표 current, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var prepared = await 준비Async(driverId, candidate, nowUtc, 기존업무만: false, cancellationToken);
        if (prepared.보류사유 is not null) return 음식권역복귀일정평가결과.보류(prepared.보류사유);
        var jobs = prepared.작업목록;
        var beforeJobs = jobs.Take(prepared.활성업무수).ToArray();
        var before = beforeJobs.Length == 0 ? null : await optimizer.최적화Async(Request(beforeJobs), cancellationToken);
        var after = await optimizer.최적화Async(Request(jobs), cancellationToken);
        var beforeVerified = beforeJobs.Length == 0 || before is { 실제경로검증여부: true, 전체완수가능여부: true };
        var verified = beforeVerified && after is { 실제경로검증여부: true, 전체완수가능여부: true };
        return new(beforeJobs.Length == 0 ? current : before?.방문순서.LastOrDefault()?.좌표,
            after.방문순서.LastOrDefault()?.좌표, verified, verified, prepared.RouteOption,
            verified ? "기존 음식 전체 일정과 추가 수행의 마지막 위치를 확인했습니다." : "음식 전체 일정의 실제 경로 또는 시간 적격을 확인하지 못했습니다.");

        픽업하차경로최적화요청 Request(IReadOnlyList<픽업하차경로작업> items)
            => 요청(nowUtc, current, items, prepared.RouteOption!);
    }

    public async Task<음식후속픽업기준결과> 후속픽업기준조회Async(string driverId, 운송원장 candidate,
        배차경로좌표 current, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var prepared = await 준비Async(driverId, candidate, nowUtc, 기존업무만: true, cancellationToken);
        if (prepared.보류사유 is not null) return 음식후속픽업기준결과.보류(prepared.사유Code);
        if (prepared.활성업무수 == 0) return new(false, true, current, nowUtc, null, "CurrentLocation");
        var before = await optimizer.최적화Async(요청(nowUtc, current, prepared.작업목록, prepared.RouteOption!), cancellationToken);
        var last = before.방문순서.OrderBy(x => x.순서).LastOrDefault();
        if (before is not { 최적화가능여부: true, 실제경로검증여부: true, 전체완수가능여부: true }
            || last is not { 단계유형: "dropoff", 좌표: not null, 예상도착시각Utc: not null }
            || last.예상도착시각Utc.Value < nowUtc)
            return 음식후속픽업기준결과.보류("ActiveScheduleRouteUnverified");
        return new(true, true, last.좌표, last.예상도착시각Utc, prepared.RouteOption, "ActiveScheduleEnd");
    }

    private 픽업하차경로최적화요청 요청(DateTime nowUtc, 배차경로좌표 current,
        IReadOnlyList<픽업하차경로작업> items, string option)
        => new(nowUtc, current, items, 음식배달기사활성업무Policy.MaxActiveDeliveries,
            좌표기반평균속도KmH: queueOptions.Value.음식배달평균이동속도KmH,
            일반시간대배달완료허용초과분: 0m, 피크시간대배달완료허용초과분: 0m, RouteOption: option);

    private async Task<음식일정준비결과> 준비Async(string driverId, 운송원장 candidate, DateTime nowUtc,
        bool 기존업무만, CancellationToken cancellationToken)
    {
        if (candidate.배차업무유형 != 상태값.배차업무유형.음식배달)
            return 음식일정준비결과.보류("음식 원장만 평가합니다.", "ActiveScheduleBusinessMismatch");
        if (await db.운송원장.AsNoTracking().AnyAsync(x => x.배차업무유형 == 상태값.배차업무유형.용달운송
            && x.상태 != 상태값.배차상태.인수완료
            && (x.기사_운송자 == driverId || x.확정기사Id == driverId), cancellationToken))
            return 음식일정준비결과.보류("진행 중인 화물과 공동 수행 일정을 확인해야 합니다.", "ActiveScheduleCargoPresent");
        var active = await db.운송원장.AsNoTracking().Where(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료
            && (x.기사_운송자 == driverId || x.확정기사Id == driverId)).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        if (!음식배달기사활성업무Policy.수락가능한가(active.Count, 1))
            return 음식일정준비결과.보류("기존 음식 수행과 새 제안이 현재 3건 상한을 넘습니다.", "ActiveScheduleCapacityExceeded");
        if (active.Any(x => x.의뢰Id == candidate.의뢰Id))
            return 음식일정준비결과.보류("이미 수행 중인 음식은 새 제안으로 중복 평가하지 않습니다.", "ActiveScheduleDuplicateOffer");
        if (기존업무만)
        {
            // 일부 실행 투영만 있어도 누락한 수락 업무의 시간을 0으로 보지 않는다.
            // 전체 최신 시도를 먼저 골라 과거 미완료 시도나 다른 기사로 재배차된 시도로 돌아가지 않는다.
            var unfinishedOrders = await db.음식배달시도.AsNoTracking().Where(x => x.기사Id == driverId
                    && x.중단시각Utc == null && x.전달완료시각Utc == null)
                .Select(x => x.주문번호).Distinct().ToArrayAsync(cancellationToken);
            var latest = (await db.음식배달시도.AsNoTracking().Where(x => Enumerable.Contains(unfinishedOrders, x.주문번호))
                    .OrderByDescending(x => x.시도순번).ThenByDescending(x => x.Id).ToListAsync(cancellationToken))
                .GroupBy(x => x.주문번호).Select(x => x.First());
            if (latest.Any(x => x.기사Id == driverId && x.중단시각Utc == null && x.전달완료시각Utc == null
                && (x.상태Code is 음식배달시도상태Code.수락 or 음식배달시도상태Code.가게도착 or 음식배달시도상태Code.픽업완료)
                && !active.Any(queue => OrderNo(queue) == x.주문번호 && queue.의뢰Id == x.제안Id && queue.추천라운드 == x.추천라운드)))
                return 음식일정준비결과.보류("수락한 음식 배달 시도와 활성 실행 원장의 연결이 없습니다.", "ActiveScheduleQueueMissing");
            if (active.Count == 0) return new([], 0, null, null, "CurrentLocation");
        }
        var driver = await db.Set<배달기사>().AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == driverId, cancellationToken);
        string? option = driver?.차량?.Trim() switch
        {
            "자동차" or "승용차" or "Car" => routeOptions.Value.ResolveBand(new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)))?.CarRouteOption
                ?? routeOptions.Value.CarRouteOption,
            "오토바이" or "이륜차" or "Motorcycle" => routeOptions.Value.MotorcycleRouteOption == "traavoidcaronly"
                ? routeOptions.Value.MotorcycleRouteOption : null,
            _ => null
        };
        if (option is not ("trafast" or "tracomfort" or "traoptimal" or "traavoidtoll" or "traavoidcaronly"))
            return 음식일정준비결과.보류("기사 차량의 음식 경로 옵션을 확인하지 못했습니다.", "ActiveScheduleVehicleUnknown");
        var all = 기존업무만 ? active.ToArray() : active.Append(candidate).ToArray();
        var orderNos = all.Select(OrderNo).Distinct().ToArray();
        var orders = await db.음식주문.AsNoTracking().Include(x => x.상태이력)
            .Where(x => Enumerable.Contains(orderNos, x.주문번호)).ToDictionaryAsync(x => x.주문번호, cancellationToken);
        // 최신 시도를 먼저 고른다. 중단된 최신 시도 대신 과거 미완료 시도로 돌아가지 않는다.
        var attempts = (await db.음식배달시도.AsNoTracking().Where(x => Enumerable.Contains(orderNos, x.주문번호))
            .OrderByDescending(x => x.시도순번).ThenByDescending(x => x.Id).ToListAsync(cancellationToken))
            .GroupBy(x => x.주문번호).ToDictionary(x => x.Key, x => x.First());
        var jobs = new List<픽업하차경로작업>();
        foreach (var queue in all)
        {
            if (!orders.TryGetValue(OrderNo(queue), out var order))
                return 음식일정준비결과.보류("음식 주문과 일정의 연결을 확인하지 못했습니다.", "ActiveScheduleOrderMissing");
            if (!음식주문상태코드.지원여부(order.상태) || 음식주문상태코드.Normalize(order.상태) is 음식주문상태코드.취소 or 음식주문상태코드.거절
                or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인)
                return 음식일정준비결과.보류("음식 주문이 현재 수행 가능한 상태가 아닙니다.", "ActiveScheduleOrderStateInvalid");
            var prep = 음식주문현재조리Policy.계산(order);
            var ready = prep.ReadyAtUtc ?? order.조리예상완료시각Utc;
            if (!ready.HasValue || (prep.RecookingRequestedAtUtc is { } recooking && ready.Value < recooking))
                return 음식일정준비결과.보류("현재 조리 회차의 준비 시각을 확인하지 못했습니다.", "ActiveSchedulePreparationUnknown");
            음식배달시도? attempt = null;
            if (!ReferenceEquals(queue, candidate))
            {
                if (!attempts.TryGetValue(order.주문번호, out attempt) || attempt.기사Id != driverId
                    || attempt.제안Id != queue.의뢰Id || attempt.중단시각Utc.HasValue || attempt.전달완료시각Utc.HasValue
                    || string.IsNullOrWhiteSpace(attempt.시도StableId) || attempt.Revision <= 0 || attempt.추천라운드 != queue.추천라운드
                    || attempt.상태Code is not (음식배달시도상태Code.수락 or 음식배달시도상태Code.가게도착 or 음식배달시도상태Code.픽업완료)
                    || (attempt.상태Code == 음식배달시도상태Code.픽업완료) != attempt.픽업완료시각Utc.HasValue
                    || attempt.유산추정여부 || (prep.RecookingRequestedAtUtc is { } roundStart && attempt.수락시각Utc < roundStart))
                    return 음식일정준비결과.보류("활성 음식의 현재 배달 시도를 확인하지 못했습니다.", "ActiveScheduleAttemptUnverified");
                if (attempt.픽업완료시각Utc.HasValue && (!prep.ReadyAtUtc.HasValue || attempt.픽업완료시각Utc < prep.ReadyAtUtc
                    || attempt.픽업완료시각Utc < attempt.수락시각Utc || attempt.픽업완료시각Utc > nowUtc))
                    return 음식일정준비결과.보류("이미 픽업한 음식의 현재 준비 회차를 확인하지 못했습니다.", "ActiveSchedulePickedRoundInvalid");
            }
            var pickup = Point(queue.픽업_위도, queue.픽업_경도);
            var dropoff = Point(queue.하차_위도, queue.하차_경도);
            if (pickup is null || dropoff is null)
                return 음식일정준비결과.보류("전체 음식 일정의 좌표가 누락됐습니다.", "ActiveScheduleCoordinatesMissing");
            // 기존 기사 안내의 준비 예상 +42분을 그대로 사용한다. 회차 근거가 없으면 위에서 보류한다.
            jobs.Add(new(queue.의뢰Id, queue.픽업_도로명주소, pickup,
                ready.Value.AddMinutes((double)queueOptions.Value.음식배달픽업지연허용분),
                queue.하차_도로명주소, dropoff, ready.Value.AddMinutes(42), ready, 42m,
                이미픽업완료: attempt?.픽업완료시각Utc.HasValue == true, 픽업완료시각Utc: attempt?.픽업완료시각Utc));
        }
        return new(jobs, active.Count, option, null, "Prepared");
    }

    private sealed record 음식일정준비결과(IReadOnlyList<픽업하차경로작업> 작업목록, int 활성업무수,
        string? RouteOption, string? 보류사유, string 사유Code)
    {
        public static 음식일정준비결과 보류(string reason, string code) => new([], 0, null, reason, code);
    }

    private static string OrderNo(운송원장 queue) => string.IsNullOrWhiteSpace(queue.원본의뢰Id) ? queue.의뢰Id : queue.원본의뢰Id;
    private static 배차경로좌표? Point(decimal? latitude, decimal? longitude)
        => latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m ? new(latitude.Value, longitude.Value) : null;
}
