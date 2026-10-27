using Microsoft.EntityFrameworkCore;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Queue;

public sealed partial class 용달운송배차업무정책
{
    // 검색 반경과 직선 근사는 후보 탐색 조건이다. 약속 이행 근거는 수락 때와 같은
    // 실제 경로·픽업/하차 시간창·상하차 작업시간을 포함한 전체 일정에서 확인한다.
    private async Task<대기기사약속검토결과> 대기기사약속검토Async(
        string driverId, 배차경로좌표 origin, 배차경로좌표 pickupPoint,
        화주운송의뢰 request, DateTime nowUtc, CancellationToken ct)
    {
        if (!request.픽업_위도.HasValue || !request.픽업_경도.HasValue
            || !request.하차_위도.HasValue || !request.하차_경도.HasValue
            || request.픽업_시간창_종료일시 == default || !request.하차_시간창_종료일시.HasValue)
            return new("IdleScheduleEvidenceMissing");
        if (pickupPoint != new 배차경로좌표(request.픽업_위도.Value, request.픽업_경도.Value))
            return new("IdleScheduleRequestChanged");
        if (request.픽업_시간창_종료일시 <= nowUtc)
            return new("IdleSchedulePickupLate");

        배차경로예상결과? route;
        try { route = await _routeService.EstimateRouteAsync(origin, pickupPoint, ct).WaitAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return new("IdleSchedulePickupRouteUnverified"); }
        if (route is not { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
            || route.Duration.Value < TimeSpan.Zero || string.IsNullOrWhiteSpace(route.RouteOption)
            || route.Duration.Value > DateTime.MaxValue - nowUtc)
            return new("IdleSchedulePickupRouteUnverified");

        // 경로 응답을 기다린 시간도 약속의 여유로 소비된다.
        nowUtc = DateTime.UtcNow;
        if (route.Duration.Value > DateTime.MaxValue - nowUtc)
            return new("IdleSchedulePickupRouteUnverified");
        var arrival = nowUtc.Add(route.Duration.Value);
        if (arrival < request.픽업_시간창_시작일시) arrival = request.픽업_시간창_시작일시;
        if (arrival > request.픽업_시간창_종료일시)
            return new("IdleSchedulePickupLate", route.DistanceKm);

        운송삽입평가결과 schedule;
        Dictionary<string, 화물상하차작업시간> handling;
        try
        {
            // 끝점 기준 옵션이 꺼진 기존 경로에서도 실제로 남아 있는 일정을 지우지 않는다.
            var plan = await new 기사운송일정구성Service(_db).구성Async(driverId, origin, ct);
            handling = plan.상하차작업시간?.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, 화물상하차작업시간>(StringComparer.Ordinal);
            handling[request.의뢰Id] = new(request.픽업_작업시간분, request.하차_작업시간분);
            plan = plan with { 기준시각 = nowUtc, 상하차작업시간 = handling };
            if (plan.항목목록.Any(x => x.좌표 is null || !x.시간창종료일시.HasValue))
                return new("IdleScheduleEvidenceMissing", route.DistanceKm);
            schedule = await _일정평가.평가Async(plan, request, ct).WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return new("IdleScheduleCandidateUnverified", route.DistanceKm); }
        if (!schedule.삽입가능여부 || !schedule.전체완수가능여부 || !schedule.후보포함실제경로확인됨
            || !schedule.시도목록.Any(x => x.전체완수가능여부 && x.전체실제경로확인됨))
            return new("IdleScheduleCandidateInfeasible", route.DistanceKm);

        var latest = await _db.화주운송의뢰.AsNoTracking().FirstOrDefaultAsync(x => x.의뢰Id == request.의뢰Id, ct);
        if (latest is null || !Equals(약속입력(latest), 약속입력(request)))
            return new("IdleScheduleRequestChanged", route.DistanceKm);
        var elapsed = DateTime.UtcNow - nowUtc;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        // 일정 계산 도중에 지난 시각으로 약속을 통과시키지 않는다. 보수적으로 지연을
        // 더해도 전체 시간창을 지킬 수 있는 동일 실제경로 시도가 있어야 한다.
        var attempt = schedule.시도목록.FirstOrDefault(x => x.전체완수가능여부 && x.전체실제경로확인됨
            && string.Equals(x.경로옵션, route.RouteOption, StringComparison.Ordinal)
            && x.도착예상목록.Count > 0 && x.도착예상목록.All(a => a.예상도착시각.HasValue
                && a.시간창종료일시.HasValue && a.예상도착시각.Value <= DateTime.MaxValue - elapsed
                && (decimal)(a.시간창종료일시.Value - a.예상도착시각.Value.Add(elapsed)).TotalMinutes
                    >= Math.Max(0m, a.단계유형 == "pickup" ? handling.GetValueOrDefault(a.의뢰Id)?.상차분 ?? 0m
                        : handling.GetValueOrDefault(a.의뢰Id)?.하차분 ?? 0m)));
        var pickup = attempt?.도착예상목록.FirstOrDefault(x => x.의뢰Id == request.의뢰Id && x.단계유형 == "pickup")?.예상도착시각;
        if (!pickup.HasValue) return new("IdleScheduleCandidateInfeasible", route.DistanceKm);
        arrival = pickup.Value.Add(elapsed);
        if (arrival > request.픽업_시간창_종료일시)
            return new("IdleSchedulePickupLate", route.DistanceKm);

        return new(null, route.DistanceKm, arrival, (decimal)(arrival - nowUtc).TotalMinutes,
            (decimal)(request.픽업_시간창_종료일시 - arrival).TotalMinutes, schedule);
    }

    private static object 약속입력(화주운송의뢰 request) => new
    {
        request.UpdatedAt, request.픽업_위도, request.픽업_경도, request.하차_위도, request.하차_경도,
        request.픽업_시간창_시작일시, request.픽업_시간창_종료일시, request.하차_시간창_시작일시,
        request.하차_시간창_종료일시, request.픽업_작업시간분, request.하차_작업시간분
    };

    private sealed record 대기기사약속검토결과(string? 제외Code, decimal? 거리Km = null,
        DateTime? 픽업도착시각Utc = null, decimal? 픽업소요분 = null,
        decimal? 픽업여유분 = null, 운송삽입평가결과? 일정 = null);
}
