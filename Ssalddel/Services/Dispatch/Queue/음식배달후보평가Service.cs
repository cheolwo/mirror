using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using static 살뜰.Services.Dispatch.Queue.음식배달후보검토기록;

namespace 살뜰.Services.Dispatch.Queue;

/// <summary>준비된 기사의 물리·업무 관문과 경로·일정·복귀 근거를 평가한다. 추천을 확정하지 않는다.</summary>
internal sealed class 음식배달후보평가Service
{
    private readonly I배차추천경로Service _경로Service;
    private readonly 배차큐정책Options _options;
    private readonly I기사권역복귀Service? _권역복귀;
    private readonly I음식권역복귀일정평가Service? _음식일정;
    private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;

    internal 음식배달후보평가Service(I배차추천경로Service 경로Service, 배차큐정책Options options,
        I기사권역복귀Service? 권역복귀, I음식권역복귀일정평가Service? 음식일정,
        ISharedDriverWorkAdmissionService? sharedAdmission)
    {
        _경로Service = 경로Service;
        _options = options;
        _권역복귀 = 권역복귀;
        _음식일정 = 음식일정;
        _sharedAdmission = sharedAdmission;
    }

    internal async Task<IReadOnlyList<음식배달기사항목>> 평가Async(
        IReadOnlyList<국내화물운송기사상태Snapshot> candidates,
        HashSet<string>? seenDriverIds,
        배차경로좌표 pickup,
        IReadOnlySet<string> rejectedDriverIds,
        string? 제외기사Id,
        DateTime? 조리예상완료시각Utc,
        DateTime now,
        int 공간단계,
        string 공간설명,
        List<음식배달후보검토항목> 검토항목,
        IReadOnlyDictionary<string, 음식배달수요기사판정>? decisions,
        운송원장 queue,
        CancellationToken cancellationToken)
    {
        var result = new List<음식배달기사항목>();
        foreach (var candidate in candidates)
        {
            if (seenDriverIds is not null && !seenDriverIds.Add(candidate.DriverId))
            {
                continue;
            }

            var blocked = !string.Equals(candidate.AppKey, 기사앱식별자.FoodDeliveryDriverApp, StringComparison.Ordinal)
                ? "DriverAppMismatch"
                : !string.Equals(candidate.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase)
                    ? "DriverNotDriving"
                : !candidate.Latitude.HasValue || !candidate.Longitude.HasValue ? "DriverLocationMissing"
                : !기사위치신선도정책.유효한가(candidate.위치수신시각Utc, now, _options.기사위치유효시간분)
                    ? "DriverLocationStale"
                : rejectedDriverIds.Contains(candidate.DriverId) ? "DriverRejected"
                : !string.IsNullOrWhiteSpace(제외기사Id)
                    && string.Equals(candidate.DriverId, 제외기사Id, StringComparison.Ordinal) ? "DriverExcluded"
                : null;
            if (blocked is not null)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, blocked, candidate, null, decisions);
                continue;
            }

            if (_sharedAdmission is not null)
            {
                var participation = await _sharedAdmission.CheckRecommendationAsync(candidate.DriverId, queue, cancellationToken);
                if (participation.IsFailed)
                {
                    var code = participation.Errors.FirstOrDefault()?.Metadata.GetValueOrDefault("ErrorCode")?.ToString()
                        ?? "DriverWorkNotSelected";
                    검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, code, candidate, null, decisions);
                    continue;
                }
            }

            var current = new 배차경로좌표(candidate.Latitude!.Value, candidate.Longitude!.Value);
            음식후속픽업기준결과? basis = null;
            decimal? travelMinutes = null;
            string? routeBasis = null;
            decimal? distance;
            if (_options.음식배달수행종료기준사용)
            {
                basis = _음식일정 is null ? 음식후속픽업기준결과.보류("ActiveScheduleServiceUnavailable")
                    : await _음식일정.후속픽업기준조회Async(candidate.DriverId, queue, current, now, cancellationToken);
                if (!basis.확인됨 || basis.기준좌표 is null || !basis.가용예상시각Utc.HasValue
                    || basis.가용예상시각Utc.Value < now)
                {
                    검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, basis.사유Code,
                        candidate, null, decisions, basis: basis);
                    continue;
                }
                if (basis.활성업무있음)
                {
                    if (!조리예상완료시각Utc.HasValue)
                    {
                        검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "ActiveScheduleReadyTimeMissing",
                            candidate, null, decisions, basis: basis);
                        continue;
                    }
                    배차경로예상결과? route = null;
                    if (basis.RouteOption is "trafast" or "tracomfort" or "traoptimal" or "traavoidtoll" or "traavoidcaronly")
                    {
                        try { route = await _경로Service.EstimateRouteAsync(basis.기준좌표, pickup, basis.RouteOption, cancellationToken); }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch (Exception) { /* 경로 실패는 아래의 명시적 신규 후보 제외로 남긴다. */ }
                    }
                    if (route is not { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
                        || route.Duration.Value < TimeSpan.Zero || route.RouteOption != basis.RouteOption)
                    {
                        검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "ActiveSchedulePickupRouteUnverified",
                            candidate, null, decisions, basis: basis);
                        continue;
                    }
                    distance = route.DistanceKm;
                    travelMinutes = (decimal)(basis.가용예상시각Utc.Value - now).TotalMinutes + (decimal)route.Duration.Value.TotalMinutes;
                    routeBasis = "VerifiedVehicleRoute";
                }
                else
                {
                    distance = _경로Service.CalculateDistanceKm(current, pickup);
                    routeBasis = "CurrentLocationEstimate";
                }
            }
            else distance = _경로Service.CalculateDistanceKm(current, pickup);
            if (!distance.HasValue)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "DistanceUnavailable", candidate, null, decisions, basis: basis);
                continue;
            }

            var allowedRadiusKm = Math.Min(
                Math.Max(1m, _options.음식배달후보검색반경Km),
                Math.Max(1m, candidate.상차접근허용반경Km ?? _options.음식배달후보검색반경Km));
            if (distance.Value > allowedRadiusKm)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "OutsideApproachRadius", candidate, null, decisions, distance, basis, travelMinutes, routeBasis);
                continue;
            }

            var 픽업시각 = travelMinutes.HasValue
                ? Ssalddel.WorkflowRules.음식배달픽업평가Policy.예상소요시간판정(
                    distance.Value, candidate.Aging점수, 조리예상완료시각Utc, now, travelMinutes.Value,
                    _options.음식배달픽업조기도착허용분, _options.음식배달픽업지연허용분)
                : Ssalddel.WorkflowRules.음식배달픽업평가Policy.판정(
                distance.Value, candidate.Aging점수, 조리예상완료시각Utc, now,
                _options.음식배달평균이동속도KmH,
                _options.음식배달픽업조기도착허용분, _options.음식배달픽업지연허용분);
            if (basis is { 활성업무있음: true } && 픽업시각.단계 == 2)
            {
                검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "ActiveSchedulePickupLate",
                    candidate, 픽업시각, decisions, distance, basis, travelMinutes, routeBasis);
                continue;
            }
            검토기록(검토항목, candidate.DriverId, 공간단계, 공간설명, "Eligible", candidate, 픽업시각, decisions, distance, basis, travelMinutes, routeBasis);
            result.Add(new 음식배달기사항목(
                candidate,
                distance.Value,
                공간단계,
                공간설명,
                픽업시각));
        }

        return result;
    }

    internal async Task<IReadOnlyList<음식배달기사항목>> 복귀평가Async(
        IReadOnlyList<음식배달기사항목> evaluated, 운송원장 queue, DateTime now,
        List<음식배달후보검토항목> 검토항목, CancellationToken cancellationToken)
    {
        if (_권역복귀 is null || _음식일정 is null) return evaluated;
        var result = new List<음식배달기사항목>();
        foreach (var item in evaluated)
        {
            var point = new 배차경로좌표(item.기사.Latitude!.Value, item.기사.Longitude!.Value);
            var state = await _권역복귀.상태조회Async(item.기사.DriverId, point,
                item.기사.위치기록시각Utc, item.기사.AccuracyM, cancellationToken);
            if (!state.정책선택됨) { result.Add(item); continue; }
            // 픽업 지연 후보를 복귀 가점으로 살리지 않는다.
            if (!state.활성여부 || item.평가.단계 == 2)
            {
                result.Add(item with { 복귀평가 = 기사권역복귀평가.보류(state.활성여부 ? "픽업 지연 후보에는 복귀 가점을 적용하지 않습니다." : state.사유) });
                continue;
            }
            var plan = await _음식일정.평가Async(item.기사.DriverId, queue, point, now, cancellationToken);
            var returnEvaluation = plan.적격여부
                ? await _권역복귀.평가Async(state, plan.기존마지막좌표, plan.후보마지막좌표,
                    plan.전체실경로확인됨, plan.적격여부, plan.RouteOption, cancellationToken)
                : 기사권역복귀평가.보류(plan.사유);
            result.Add(item with { 복귀평가 = returnEvaluation });
        }
        foreach (var item in result)
            점수갱신(검토항목, item.기사.DriverId, item.추천점수);
        return result;
    }

}
