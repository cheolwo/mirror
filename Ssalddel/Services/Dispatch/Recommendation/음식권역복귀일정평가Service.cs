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
        if (candidate.배차업무유형 != 상태값.배차업무유형.음식배달)
            return 음식권역복귀일정평가결과.보류("음식 원장만 평가합니다.");
        if (await db.운송원장.AsNoTracking().AnyAsync(x => x.배차업무유형 == 상태값.배차업무유형.용달운송
            && x.상태 != 상태값.배차상태.인수완료
            && (x.기사_운송자 == driverId || x.확정기사Id == driverId), cancellationToken))
            return 음식권역복귀일정평가결과.보류("진행 중인 화물과 공동 수행 일정을 확인해야 합니다.");
        var active = await db.운송원장.AsNoTracking().Where(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료
            && (x.기사_운송자 == driverId || x.확정기사Id == driverId)).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        if (!음식배달기사활성업무Policy.수락가능한가(active.Count, 1))
            return 음식권역복귀일정평가결과.보류("기존 음식 수행과 새 제안이 현재 3건 상한을 넘습니다.");
        if (active.Any(x => x.의뢰Id == candidate.의뢰Id))
            return 음식권역복귀일정평가결과.보류("이미 수행 중인 음식은 새 제안으로 중복 평가하지 않습니다.");
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
            return 음식권역복귀일정평가결과.보류("기사 차량의 음식 경로 옵션을 확인하지 못했습니다.");
        var all = active.Append(candidate).ToArray();
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
                return 음식권역복귀일정평가결과.보류("음식 주문과 일정의 연결을 확인하지 못했습니다.");
            if (!음식주문상태코드.지원여부(order.상태) || 음식주문상태코드.Normalize(order.상태) is 음식주문상태코드.취소 or 음식주문상태코드.거절
                or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인)
                return 음식권역복귀일정평가결과.보류("음식 주문이 현재 수행 가능한 상태가 아닙니다.");
            var prep = 음식주문현재조리Policy.계산(order);
            var ready = prep.ReadyAtUtc ?? order.조리예상완료시각Utc;
            if (!ready.HasValue || (prep.RecookingRequestedAtUtc is { } recooking && ready.Value < recooking))
                return 음식권역복귀일정평가결과.보류("현재 조리 회차의 준비 시각을 확인하지 못했습니다.");
            음식배달시도? attempt = null;
            if (!ReferenceEquals(queue, candidate))
            {
                if (!attempts.TryGetValue(order.주문번호, out attempt) || attempt.기사Id != driverId
                    || attempt.제안Id != queue.의뢰Id || attempt.중단시각Utc.HasValue || attempt.전달완료시각Utc.HasValue
                    || string.IsNullOrWhiteSpace(attempt.시도StableId) || attempt.Revision <= 0 || attempt.추천라운드 != queue.추천라운드
                    || attempt.상태Code is not (음식배달시도상태Code.수락 or 음식배달시도상태Code.가게도착 or 음식배달시도상태Code.픽업완료)
                    || (attempt.상태Code == 음식배달시도상태Code.픽업완료) != attempt.픽업완료시각Utc.HasValue
                    || attempt.유산추정여부 || (prep.RecookingRequestedAtUtc is { } roundStart && attempt.수락시각Utc < roundStart))
                    return 음식권역복귀일정평가결과.보류("활성 음식의 현재 배달 시도를 확인하지 못했습니다.");
                if (attempt.픽업완료시각Utc.HasValue && (!prep.ReadyAtUtc.HasValue || attempt.픽업완료시각Utc < prep.ReadyAtUtc
                    || attempt.픽업완료시각Utc < attempt.수락시각Utc || attempt.픽업완료시각Utc > nowUtc))
                    return 음식권역복귀일정평가결과.보류("이미 픽업한 음식의 현재 준비 회차를 확인하지 못했습니다.");
            }
            var pickup = Point(queue.픽업_위도, queue.픽업_경도);
            var dropoff = Point(queue.하차_위도, queue.하차_경도);
            if (pickup is null || dropoff is null)
                return 음식권역복귀일정평가결과.보류("전체 음식 일정의 좌표가 누락됐습니다.");
            // 기존 기사 안내의 준비 예상 +42분을 그대로 사용한다. 회차 근거가 없으면 위에서 보류한다.
            jobs.Add(new(queue.의뢰Id, queue.픽업_도로명주소, pickup,
                ready.Value.AddMinutes((double)queueOptions.Value.음식배달픽업지연허용분),
                queue.하차_도로명주소, dropoff, ready.Value.AddMinutes(42), ready, 42m,
                이미픽업완료: attempt?.픽업완료시각Utc.HasValue == true, 픽업완료시각Utc: attempt?.픽업완료시각Utc));
        }
        var beforeJobs = jobs.Take(active.Count).ToArray();
        var before = beforeJobs.Length == 0 ? null : await optimizer.최적화Async(Request(beforeJobs), cancellationToken);
        var after = await optimizer.최적화Async(Request(jobs), cancellationToken);
        var beforeVerified = beforeJobs.Length == 0 || before is { 실제경로검증여부: true, 전체완수가능여부: true };
        var verified = beforeVerified && after is { 실제경로검증여부: true, 전체완수가능여부: true };
        return new(beforeJobs.Length == 0 ? current : before?.방문순서.LastOrDefault()?.좌표,
            after.방문순서.LastOrDefault()?.좌표, verified, verified, option,
            verified ? "기존 음식 전체 일정과 추가 수행의 마지막 위치를 확인했습니다." : "음식 전체 일정의 실제 경로 또는 시간 적격을 확인하지 못했습니다.");

        픽업하차경로최적화요청 Request(IReadOnlyList<픽업하차경로작업> items)
            => new(nowUtc, current, items, 음식배달기사활성업무Policy.MaxActiveDeliveries,
                좌표기반평균속도KmH: queueOptions.Value.음식배달평균이동속도KmH,
                일반시간대배달완료허용초과분: 0m, 피크시간대배달완료허용초과분: 0m, RouteOption: option);
    }

    private static string OrderNo(운송원장 queue) => string.IsNullOrWhiteSpace(queue.원본의뢰Id) ? queue.의뢰Id : queue.원본의뢰Id;
    private static 배차경로좌표? Point(decimal? latitude, decimal? longitude)
        => latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m ? new(latitude.Value, longitude.Value) : null;
}
