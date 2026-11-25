using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.도메인.공통;
using 살뜰.도메인.차량;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation;

public sealed record 화물운행중삽입결과(bool 확인됨, string 사유Code,
    운송삽입평가결과? 일정 = null, DateTime? 픽업도착시각Utc = null,
    decimal? 픽업접근거리Km = null, decimal? 픽업여유분 = null,
    decimal? 픽업까지누적거리Km = null);

/// <summary>수락 순서가 아닌 동일한 실제 경로의 약속·작업시간·구간 적재로 후보를 검토한다. 저장·배차 효과는 없다.</summary>
public sealed class 화물운행중삽입평가Service(SsalddelContext db, I배차추천경로Service routes,
    I운송일정삽입평가Service evaluations)
{
    public async Task<화물운행중삽입결과> 검토Async(string driverId, 화물후속픽업기준결과 basis,
        화주운송의뢰 candidate, 차량제원? vehicle, CancellationToken ct = default)
    {
        화물운행중삽입결과 Hold(string code) => new(false, code);
        if (basis is not { 활성업무있음: true, 확인됨: true, 기존계획: { 시작좌표: not null } plan }
            || plan.기사Id != driverId || plan.수락정차순서보류Code is not null
            || string.IsNullOrWhiteSpace(basis.RouteOption) || vehicle is null)
            return Hold("ActiveScheduleEvidenceMissing");

        var active = await db.운송원장.AsNoTracking().Where(x => (x.확정기사Id == driverId || x.기사_운송자 == driverId)
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료).ToListAsync(ct);
        var ids = active.Select(x => x.의뢰Id).ToArray();
        if (active.Count == 0 || active.Count > 10 || ids.Distinct(StringComparer.Ordinal).Count() != active.Count
            || active.Any(x => x.배차업무유형 != 상태값.배차업무유형.용달운송 || x.의뢰Id == candidate.의뢰Id
                || x.상태 is "취소" or "반환대기"
                || !string.IsNullOrWhiteSpace(x.확정기사Id) && x.확정기사Id != driverId
                || !string.IsNullOrWhiteSpace(x.기사_운송자) && x.기사_운송자 != driverId)
            || !ids.ToHashSet(StringComparer.Ordinal).SetEquals(plan.항목목록.Select(x => x.의뢰Id)))
            return Hold("ActiveScheduleSourceMismatch");
        var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        if (requests.Count != ids.Length) return Hold("ActiveScheduleSourceMismatch");
        if (requests.Values.Append(candidate).Any(x => !x.픽업_위도.HasValue || !x.픽업_경도.HasValue
                || !x.하차_위도.HasValue || !x.하차_경도.HasValue || x.픽업_시간창_시작일시 == default
                || x.픽업_시간창_종료일시 == default || !x.하차_시간창_종료일시.HasValue))
            return Hold("ActiveScheduleEvidenceMissing");
        var allIds = ids.Append(candidate.의뢰Id).ToArray();
        var cargo = await db.화물요구조건.AsNoTracking().Where(x => Enumerable.Contains(allIds, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        var jobs = active.Select(x => new 화물구간적재작업(requests[x.의뢰Id], cargo.GetValueOrDefault(x.의뢰Id),
            x.상태 is "상차완료" or "하차지도착" or "운송중")).Append(new(candidate, cargo.GetValueOrDefault(candidate.의뢰Id))).ToArray();
        if (화물구간적재검증Service.혼적차단사유(jobs).Count > 0) return Hold("ActiveScheduleCargoIncompatible");
        // 이전 수락 때 명시적으로 확인한 시간도 유지하되 현재 요청 원문과 충돌하면 보류한다.
        var handling = 국내화물배차조율입력Factory.작업시간입력결합(requests.Values.Append(candidate).ToArray(),
            plan.상하차작업시간, out var handlingConflicts);
        if (handlingConflicts.Length > 0) return Hold("ActiveScheduleHandlingChanged");
        if (!handling[candidate.의뢰Id].확인됨
            || plan.항목목록.Any(x => !handling.TryGetValue(x.의뢰Id, out var time)
                || (x.단계유형 == "pickup" ? time.상차분 : time.하차분) is not >= 0m))
            return Hold("ActiveScheduleHandlingUnverified");
        plan = plan with { 상하차작업시간 = handling, 항목목록 = plan.항목목록.Select(x => x with
            { 작업시간분 = x.단계유형 == "pickup" ? handling[x.의뢰Id].상차분 : handling[x.의뢰Id].하차분 }).ToArray() };
        var schedule = await evaluations.평가Async(plan, candidate, ct);
        if (!schedule.기존일정실제경로확인됨) return Hold("ActiveScheduleRouteUnverified");
        var capacity = new 화물구간적재검증Service(new 차량적재추천Engine());
        var heldCode = "ActiveScheduleCandidateInfeasible";
        foreach (var attempt in schedule.시도목록.Where(x => x.전체완수가능여부 && x.전체실제경로확인됨
                && x.경로옵션 == basis.RouteOption && x.도착예상목록.All(a => a.예상도착시각.HasValue && !a.시간위반여부))
            .OrderBy(x => x.총소요시간분 ?? decimal.MaxValue).ThenBy(x => x.총거리Km ?? decimal.MaxValue)
            .ThenBy(x => x.삽입인덱스))
        {
            var typed = attempt.도착예상목록.OrderBy(x => x.순서).Select(x => new 기사운송일정항목(
                x.의뢰Id, x.단계유형, "", null, null, x.시간창종료일시, x.순서, null, true, x.의뢰Id == candidate.의뢰Id)).ToArray();
            var capacityResult = capacity.평가(vehicle, jobs, typed);
            if (capacityResult is not { 확인됨: true, 적합여부: true })
            { heldCode = capacityResult.사유Code; continue; }
            // 같은 선택 시도의 상차까지를 명시 경로 옵션으로 다시 읽는다. 전체 업무 종료점을 B의 출발점으로 강제하지 않는다.
            var at = plan.기준시각;
            var point = plan.시작좌표;
            decimal cumulativeDistance = 0m;
            foreach (var stop in typed)
            {
                var source = stop.의뢰Id == candidate.의뢰Id ? candidate : requests[stop.의뢰Id];
                var pickup = stop.단계유형 == "pickup";
                var latitude = pickup ? source.픽업_위도 : source.하차_위도;
                var longitude = pickup ? source.픽업_경도 : source.하차_경도;
                var destination = latitude.HasValue && longitude.HasValue ? new 배차경로좌표(latitude.Value, longitude.Value) : null;
                var route = await routes.EstimateRouteAsync(point, destination, basis.RouteOption, ct);
                if (route is not { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
                    || route.Duration.Value < TimeSpan.Zero || route.RouteOption != basis.RouteOption)
                { heldCode = "ActiveSchedulePickupRouteUnverified"; break; }
                cumulativeDistance += route.DistanceKm.Value;
                var arrival = at.Add(route.Duration.Value);
                var starts = pickup ? source.픽업_시간창_시작일시 : source.하차_시간창_시작일시;
                if (starts > arrival) arrival = starts!.Value;
                var work = pickup ? handling[stop.의뢰Id].상차분!.Value : handling[stop.의뢰Id].하차분!.Value;
                var finished = arrival.AddMinutes((double)work);
                var deadline = pickup ? source.픽업_시간창_종료일시 : source.하차_시간창_종료일시;
                var observed = attempt.도착예상목록.Single(x => x.의뢰Id == stop.의뢰Id && x.단계유형 == stop.단계유형);
                if (!deadline.HasValue || finished > deadline.Value || arrival > observed.예상도착시각)
                { heldCode = "ActiveSchedulePickupLate"; break; }
                if (stop.의뢰Id == candidate.의뢰Id && pickup)
                {
                    var selected = schedule with { 삽입가능여부 = true, 전체완수가능여부 = true,
                        최적삽입인덱스 = attempt.삽입인덱스, 경로변경이점여부 = attempt.경로변경시도여부,
                        경로변경절감분 = attempt.단순이어가기대비절감분, 총소요시간분 = attempt.총소요시간분,
                        총거리Km = attempt.총거리Km, 총추가지연분 = attempt.총추가지연분,
                        최대시간위반분 = attempt.최대시간위반분, 위반사유 = attempt.위반사유,
                        권장경로순서 = attempt.경로순서, 도착예상목록 = attempt.도착예상목록,
                        후보포함종료좌표 = attempt.종료좌표, 후보포함실제경로확인됨 = true,
                        복귀비교경로옵션 = attempt.경로옵션 };
                    return new(true, "VerifiedInsertedSchedule", selected, arrival, route.DistanceKm.Value,
                        (decimal)(deadline.Value - finished).TotalMinutes, cumulativeDistance);
                }
                at = finished;
                point = destination;
            }
        }
        return Hold(heldCode);
    }
}
