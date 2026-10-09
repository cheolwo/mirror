namespace 살뜰.Services.Dispatch.Recommendation;

public interface I픽업하차경로최적화Service
{
    Task<픽업하차경로최적화결과> 최적화Async(픽업하차경로최적화요청 request, CancellationToken cancellationToken = default);
}

public sealed class 픽업하차경로최적화Service(I배차추천경로Service routeService) : I픽업하차경로최적화Service
{
    private const string PickupStage = "pickup";
    private const string DropoffStage = "dropoff";
    private const int DefaultMaxJobCount = 7;

    public async Task<픽업하차경로최적화결과> 최적화Async(
        픽업하차경로최적화요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var jobs = request.작업목록
            .Where(x => !string.IsNullOrWhiteSpace(x.의뢰Id))
            .ToArray();
        var dispatchBundleType = ResolveDispatchBundleType(jobs.Length);
        if (jobs.Length == 0)
        {
            return new 픽업하차경로최적화결과(true, true, 0m, 0m, 0m, [], [], [], dispatchBundleType);
        }

        var maxJobCount = request.최대작업수 <= 0 ? DefaultMaxJobCount : request.최대작업수;
        if (jobs.Length > maxJobCount)
        {
            return new 픽업하차경로최적화결과(
                false,
                false,
                null,
                null,
                null,
                [],
                [$"동시 최적화 작업 수가 너무 많습니다. 작업수={jobs.Length}, 최대={maxJobCount}"],
                [],
                dispatchBundleType);
        }

        var stops = BuildStops(
            jobs,
            request.일반시간대배달완료허용초과분,
            request.피크시간대배달완료허용초과분);
        var routeMatrix = BuildCoordinateRouteMatrix(request.시작좌표, stops, request.좌표기반도로보정계수, request.좌표기반평균속도KmH);
        var search = new SearchState(
            jobs,
            stops,
            routeMatrix,
            request.최대총거리Km,
            request.일반시간대배달완료허용초과분,
            request.피크시간대배달완료허용초과분);
        search.Visit(
            currentStopIndex: -1,
            pickedMask: jobs.Select((job, index) => job.이미픽업완료 ? 1 << index : 0).Aggregate(0, (mask, bit) => mask | bit),
            deliveredMask: 0,
            now: request.기준시각,
            totalMinutes: 0m,
            totalDistanceKm: 0m,
            maxViolationMinutes: null,
            violations: [],
            pickupCompletedAtMap: jobs.Select((job, index) => (job, index))
                .Where(x => x.job.이미픽업완료 && x.job.픽업완료시각Utc.HasValue)
                .ToDictionary(x => x.index, x => x.job.픽업완료시각Utc!.Value),
            arrivals: [],
            route: []);

        var approximateResult = search.BuildResult(dispatchBundleType, request.기사현재좌표반영여부);
        return await VerifySelectedRouteAsync(request, approximateResult, cancellationToken);
    }

    private static string ResolveDispatchBundleType(int jobCount)
        => jobCount <= 1 ? "단건배차" : "멀티배차";

    private RouteMatrix BuildCoordinateRouteMatrix(
        배차경로좌표? start,
        IReadOnlyList<픽업하차경로정류장> stops,
        decimal roadDistanceMultiplier,
        decimal averageSpeedKmH)
    {
        var startRoutes = new 배차경로예상결과?[stops.Count];
        var stopRoutes = new 배차경로예상결과?[stops.Count, stops.Count];
        var normalizedMultiplier = roadDistanceMultiplier <= 0m ? 1.25m : roadDistanceMultiplier;
        var normalizedSpeed = averageSpeedKmH <= 0m ? 35m : averageSpeedKmH;

        for (var to = 0; to < stops.Count; to++)
        {
            startRoutes[to] = EstimateCoordinateRoute(start, stops[to].좌표, normalizedMultiplier, normalizedSpeed);
        }

        for (var from = 0; from < stops.Count; from++)
        {
            for (var to = 0; to < stops.Count; to++)
            {
                if (from == to)
                {
                    continue;
                }

                stopRoutes[from, to] = EstimateCoordinateRoute(stops[from].좌표, stops[to].좌표, normalizedMultiplier, normalizedSpeed);
            }
        }

        return new RouteMatrix(startRoutes, stopRoutes);
    }

    private 배차경로예상결과? EstimateCoordinateRoute(
        배차경로좌표? origin,
        배차경로좌표? destination,
        decimal roadDistanceMultiplier,
        decimal averageSpeedKmH)
    {
        if (origin is null || destination is null)
        {
            return null;
        }

        var directDistanceKm = routeService.CalculateDistanceKm(origin, destination)
                               ?? CalculateHaversineDistanceKm(origin, destination);
        var estimatedDistanceKm = Math.Round(directDistanceKm * roadDistanceMultiplier, 2);
        var estimatedMinutes = estimatedDistanceKm == 0m
            ? 0m
            : estimatedDistanceKm / averageSpeedKmH * 60m;

        return new 배차경로예상결과(
            estimatedDistanceKm,
            TimeSpan.FromMinutes((double)estimatedMinutes),
            null,
            "좌표근사",
            false);
    }

    private async Task<픽업하차경로최적화결과> VerifySelectedRouteAsync(
        픽업하차경로최적화요청 request,
        픽업하차경로최적화결과 approximateResult,
        CancellationToken cancellationToken)
    {
        // 차량 옵션을 지정한 음식 일정은 총시간 비례 추정 대신 모든 남은 구간을 검증한다.
        // 기존 호출의 경로와 결과는 유지한다.
        if (request.RouteOption is not null)
            return await VerifyVehicleRouteAsync(request, approximateResult, cancellationToken);
        if (!approximateResult.최적화가능여부
            || request.시작좌표 is null
            || approximateResult.방문순서.Count == 0
            || approximateResult.방문순서.Any(x => x.좌표 is null))
        {
            return approximateResult;
        }

        var orderedStops = approximateResult.방문순서
            .Select(x => x.좌표!)
            .ToArray();
        var verified = await routeService.EstimateOrderedRouteAsync(request.시작좌표, orderedStops, cancellationToken);
        if (verified?.Duration is null && verified?.DistanceKm is null)
        {
            return approximateResult;
        }

        var verifiedDurationMinutes = verified.Duration.HasValue
            ? Math.Round((decimal)verified.Duration.Value.TotalMinutes, 2)
            : approximateResult.총소요시간분;
        var totalDistanceKm = verified.DistanceKm.HasValue
            ? Math.Round(verified.DistanceKm.Value, 2)
            : approximateResult.총거리Km;
        var visitOrder = ScaleVisitArrivalTimes(
            request.기준시각,
            approximateResult.방문순서,
            approximateResult.총소요시간분,
            verifiedDurationMinutes);
        var violations = approximateResult.위반사유;
        var feasible = approximateResult.최적화가능여부;
        var completed = approximateResult.전체완수가능여부;
        var verifiedConstraintViolations = ValidateDeliveryCompletionLimits(request, visitOrder);
        if (verifiedConstraintViolations.Count > 0)
        {
            violations = violations.Concat(verifiedConstraintViolations).Distinct(StringComparer.Ordinal).ToArray();
            feasible = false;
            completed = false;
        }

        if (request.최대총거리Km.HasValue
            && totalDistanceKm.HasValue
            && totalDistanceKm.Value > request.최대총거리Km.Value)
        {
            violations = violations
                .Concat([$"실제 경로 검증 거리 {totalDistanceKm.Value:0.##}km가 멀티배차 총거리 상한 {request.최대총거리Km.Value:0.##}km를 초과합니다."])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            feasible = false;
            completed = false;
        }

        return approximateResult with
        {
            최적화가능여부 = feasible,
            전체완수가능여부 = completed,
            총소요시간분 = verified.Duration.HasValue
                ? Math.Round((decimal)verified.Duration.Value.TotalMinutes, 2)
                : approximateResult.총소요시간분,
            총거리Km = totalDistanceKm,
            방문순서 = visitOrder,
            위반사유 = violations,
            비용계산방식 = verified.계산방식,
            실제경로검증여부 = verified.실제경로여부,
            좌표근사총소요시간분 = approximateResult.총소요시간분,
            좌표근사총거리Km = approximateResult.총거리Km
        };
    }

    private async Task<픽업하차경로최적화결과> VerifyVehicleRouteAsync(
        픽업하차경로최적화요청 request, 픽업하차경로최적화결과 result, CancellationToken cancellationToken)
    {
        if (!result.최적화가능여부 || request.시작좌표 is null || result.방문순서.Count == 0
            || result.방문순서.Any(x => x.좌표 is null)) return result with { 실제경로검증여부 = false };
        if (request.작업목록.Any(x => x.이미픽업완료 && (!x.픽업완료시각Utc.HasValue
            || (!x.하차시간창종료일시.HasValue && x.배달완료제한분 is not > 0m))))
            return result with { 최적화가능여부 = false, 전체완수가능여부 = false, 실제경로검증여부 = false,
                위반사유 = ["이미 픽업한 작업의 픽업 시각 또는 전달 마감을 확인하지 못했습니다."] };
        var jobs = request.작업목록.ToDictionary(x => x.의뢰Id, StringComparer.Ordinal);
        var point = request.시작좌표;
        var now = request.기준시각;
        decimal distance = 0m;
        var visits = new List<픽업하차경로방문순서>();
        var violations = new List<string>();
        foreach (var stop in result.방문순서.OrderBy(x => x.순서))
        {
            cancellationToken.ThrowIfCancellationRequested();
            배차경로예상결과? route;
            try { route = await routeService.EstimateRouteAsync(point, stop.좌표, request.RouteOption!, cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { route = null; }
            if (route is not { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
                || route.Duration.Value < TimeSpan.Zero || route.RouteOption != request.RouteOption)
                return result with { 최적화가능여부 = false, 전체완수가능여부 = false, 실제경로검증여부 = false,
                    위반사유 = ["차량별 전체 일정의 실제 경로를 확인하지 못했습니다."] };
            var job = jobs[stop.의뢰Id];
            now = now.Add(route.Duration.Value);
            if (stop.단계유형 == PickupStage && job.픽업가능시각Utc is { } ready && now < ready) now = ready;
            var deadline = stop.단계유형 == PickupStage ? job.픽업시간창종료일시
                : ResolveDropoffDeadline(job, request.일반시간대배달완료허용초과분, request.피크시간대배달완료허용초과분);
            if (deadline is { } end && now > end) violations.Add($"{stop.의뢰Id} {stop.단계유형} 실제 경로가 시간창을 초과합니다.");
            distance += route.DistanceKm.Value;
            visits.Add(stop with { 예상도착시각Utc = now });
            point = stop.좌표;
        }
        violations.AddRange(ValidateDeliveryCompletionLimits(request, visits));
        if (request.최대총거리Km is { } max && distance > max) violations.Add("실제 경로가 총거리 상한을 초과합니다.");
        return result with { 최적화가능여부 = violations.Count == 0, 전체완수가능여부 = violations.Count == 0,
            실제경로검증여부 = true, 비용계산방식 = "Directions5VehicleSegments", 방문순서 = visits,
            총거리Km = distance, 총소요시간분 = (decimal)(now - request.기준시각).TotalMinutes,
            위반사유 = violations.Distinct(StringComparer.Ordinal).ToArray(),
            좌표근사총소요시간분 = result.총소요시간분, 좌표근사총거리Km = result.총거리Km };
    }

    private static IReadOnlyList<픽업하차경로방문순서> ScaleVisitArrivalTimes(
        DateTime 기준시각,
        IReadOnlyList<픽업하차경로방문순서> visitOrder,
        decimal? approximateTotalMinutes,
        decimal? verifiedTotalMinutes)
    {
        if (!approximateTotalMinutes.HasValue
            || approximateTotalMinutes.Value <= 0m
            || !verifiedTotalMinutes.HasValue
            || verifiedTotalMinutes.Value <= 0m)
        {
            return visitOrder;
        }

        var scale = verifiedTotalMinutes.Value / approximateTotalMinutes.Value;
        return visitOrder
            .Select(x =>
            {
                if (!x.예상도착시각Utc.HasValue)
                {
                    return x;
                }

                var approximateElapsedMinutes = (decimal)(x.예상도착시각Utc.Value - 기준시각).TotalMinutes;
                return x with
                {
                    예상도착시각Utc = 기준시각.AddMinutes((double)(approximateElapsedMinutes * scale))
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ValidateDeliveryCompletionLimits(
        픽업하차경로최적화요청 request,
        IReadOnlyList<픽업하차경로방문순서> visitOrder)
    {
        var jobs = request.작업목록
            .Where(x => !string.IsNullOrWhiteSpace(x.의뢰Id))
            .GroupBy(x => x.의뢰Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var pickupCompletedAt = jobs.Values.Where(x => x.이미픽업완료 && x.픽업완료시각Utc.HasValue)
            .ToDictionary(x => x.의뢰Id, x => x.픽업완료시각Utc!.Value, StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var stop in visitOrder.OrderBy(x => x.순서))
        {
            if (!jobs.TryGetValue(stop.의뢰Id, out var job) || !stop.예상도착시각Utc.HasValue)
            {
                continue;
            }

            if (stop.단계유형 == PickupStage)
            {
                pickupCompletedAt[stop.의뢰Id] = stop.예상도착시각Utc.Value;
                continue;
            }

            if (stop.단계유형 != DropoffStage || !job.배달완료제한분.HasValue || job.배달완료제한분.Value <= 0m)
            {
                continue;
            }

            var qualityStart = job.픽업가능시각Utc;
            if (!qualityStart.HasValue && pickupCompletedAt.TryGetValue(stop.의뢰Id, out var pickupAt))
            {
                qualityStart = pickupAt;
            }

            if (!qualityStart.HasValue)
            {
                continue;
            }

            var limit = ResolveDeliveryCompletionLimit(
                qualityStart.Value,
                job.배달완료제한분.Value,
                stop.예상도착시각Utc.Value,
                request.일반시간대배달완료허용초과분,
                request.피크시간대배달완료허용초과분);
            if (stop.예상도착시각Utc.Value <= limit.마감시각Utc)
            {
                continue;
            }

            var violationMinutes = (decimal)(stop.예상도착시각Utc.Value - limit.마감시각Utc).TotalMinutes;
            violations.Add($"{stop.의뢰Id} 실제경로 하차가 안내된 배달 완료 제한을 {Math.Round(violationMinutes, 0):0}분 초과합니다. 제한={job.배달완료제한분.Value:0}분, 허용초과={limit.허용초과분:0}분");
        }

        return violations;
    }

    private static 배달완료제한판정 ResolveDeliveryCompletionLimit(
        DateTime qualityStartUtc,
        decimal deliveryLimitMinutes,
        DateTime arrivalUtc,
        decimal offPeakOverrunMinutes,
        decimal peakOverrunMinutes)
    {
        var normalizedLimit = Math.Max(0m, deliveryLimitMinutes);
        var toleranceMinutes = ResolveDeliveryCompletionToleranceMinutes(
            qualityStartUtc,
            arrivalUtc,
            offPeakOverrunMinutes,
            peakOverrunMinutes);
        return new 배달완료제한판정(
            qualityStartUtc.AddMinutes((double)(normalizedLimit + toleranceMinutes)),
            toleranceMinutes);
    }

    private static decimal ResolveDeliveryCompletionToleranceMinutes(
        DateTime qualityStartUtc,
        DateTime arrivalUtc,
        decimal offPeakOverrunMinutes,
        decimal peakOverrunMinutes)
        => IsFoodPeakTime(qualityStartUtc) || IsFoodPeakTime(arrivalUtc)
            ? Math.Max(0m, peakOverrunMinutes)
            : Math.Max(0m, offPeakOverrunMinutes);

    private static bool IsFoodPeakTime(DateTime utc)
    {
        var local = ToKoreaTime(utc).TimeOfDay;
        return (local >= TimeSpan.FromHours(11) && local < TimeSpan.FromHours(13.5))
               || (local >= TimeSpan.FromHours(17) && local < TimeSpan.FromHours(20));
    }

    private static DateTime ToKoreaTime(DateTime utc)
    {
        var normalized = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
            return TimeZoneInfo.ConvertTimeFromUtc(normalized, timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(normalized, timezone);
        }
        catch (InvalidTimeZoneException)
        {
            return normalized.AddHours(9);
        }
    }

    private static decimal CalculateHaversineDistanceKm(배차경로좌표 source, 배차경로좌표 target)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians((double)target.Latitude - (double)source.Latitude);
        var dLng = ToRadians((double)target.Longitude - (double)source.Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians((double)source.Latitude)) * Math.Cos(ToRadians((double)target.Latitude))
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (decimal)(earthRadiusKm * c);
    }

    private static double ToRadians(double angle) => angle * Math.PI / 180.0;

    private static 픽업하차경로정류장[] BuildStops(
        IReadOnlyList<픽업하차경로작업> jobs,
        decimal offPeakOverrunMinutes,
        decimal peakOverrunMinutes)
    {
        var stops = new List<픽업하차경로정류장>(jobs.Count * 2);
        for (var i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            if (!job.이미픽업완료) stops.Add(new 픽업하차경로정류장(
                i,
                PickupStage,
                job.의뢰Id,
                job.픽업주소,
                job.픽업좌표,
                job.픽업시간창종료일시));
            stops.Add(new 픽업하차경로정류장(
                i,
                DropoffStage,
                job.의뢰Id,
                job.하차주소,
                job.하차좌표,
                ResolveDropoffDeadline(job, offPeakOverrunMinutes, peakOverrunMinutes)));
        }

        return stops.ToArray();
    }

    private static DateTime? ResolveDropoffDeadline(
        픽업하차경로작업 job,
        decimal offPeakOverrunMinutes,
        decimal peakOverrunMinutes)
    {
        var deliveryLimitDeadline = job.픽업가능시각Utc.HasValue && job.배달완료제한분 is > 0m
            ? ResolveDeliveryCompletionLimit(
                    job.픽업가능시각Utc.Value,
                    job.배달완료제한분.Value,
                    job.픽업가능시각Utc.Value.AddMinutes((double)job.배달완료제한분.Value),
                    offPeakOverrunMinutes,
                    peakOverrunMinutes)
                .마감시각Utc
            : (DateTime?)null;
        if (!job.하차시간창종료일시.HasValue)
        {
            return deliveryLimitDeadline;
        }

        if (!deliveryLimitDeadline.HasValue)
        {
            return job.하차시간창종료일시;
        }

        return job.하차시간창종료일시.Value <= deliveryLimitDeadline.Value
            ? job.하차시간창종료일시
            : deliveryLimitDeadline;
    }

    private sealed class SearchState(
        IReadOnlyList<픽업하차경로작업> jobs,
        IReadOnlyList<픽업하차경로정류장> stops,
        RouteMatrix routeMatrix,
        decimal? maxTotalDistanceKm,
        decimal offPeakOverrunMinutes,
        decimal peakOverrunMinutes)
    {
        private readonly int _allDeliveredMask = (1 << jobs.Count) - 1;
        private readonly Dictionary<SearchKey, decimal> _bestMinutesByState = [];
        private CandidateResult? _bestFeasible;
        private CandidateResult? _bestFallback;

        public void Visit(
            int currentStopIndex,
            int pickedMask,
            int deliveredMask,
            DateTime now,
            decimal totalMinutes,
            decimal totalDistanceKm,
            decimal? maxViolationMinutes,
            IReadOnlyList<string> violations,
            IReadOnlyDictionary<int, DateTime> pickupCompletedAtMap,
            IReadOnlyList<DateTime> arrivals,
            IReadOnlyList<픽업하차경로정류장> route)
        {
            if (_bestFeasible?.TotalMinutes is decimal bestMinutes && totalMinutes >= bestMinutes)
            {
                return;
            }

            var key = new SearchKey(currentStopIndex, pickedMask, deliveredMask);
            if (_bestMinutesByState.TryGetValue(key, out var knownBest) && totalMinutes >= knownBest)
            {
                return;
            }

            _bestMinutesByState[key] = totalMinutes;

            if (deliveredMask == _allDeliveredMask)
            {
                var completedViolations = violations;
                if (maxTotalDistanceKm.HasValue && totalDistanceKm > maxTotalDistanceKm.Value)
                {
                    completedViolations = completedViolations
                        .Concat([$"멀티배차 총 운행거리 {Math.Round(totalDistanceKm, 2):0.##}km가 상한 {maxTotalDistanceKm.Value:0.##}km를 초과합니다."])
                        .ToArray();
                }

                var candidate = new CandidateResult(
                    route,
                    arrivals,
                    Math.Round(totalMinutes, 2),
                    Math.Round(totalDistanceKm, 2),
                    maxViolationMinutes.HasValue ? Math.Round(maxViolationMinutes.Value, 2) : null,
                    completedViolations.Distinct(StringComparer.Ordinal).ToArray());
                KeepBest(candidate);
                return;
            }

            for (var nextIndex = 0; nextIndex < stops.Count; nextIndex++)
            {
                var nextStop = stops[nextIndex];
                var jobBit = 1 << nextStop.JobIndex;
                if (nextStop.단계유형 == PickupStage)
                {
                    if ((pickedMask & jobBit) != 0)
                    {
                        continue;
                    }
                }
                else
                {
                    if ((pickedMask & jobBit) == 0 || (deliveredMask & jobBit) != 0)
                    {
                        continue;
                    }
                }

                var routeEstimate = routeMatrix.Get(currentStopIndex, nextIndex);
                if (routeEstimate?.Duration is null)
                {
                    var failed = $"{nextStop.의뢰Id} {StageName(nextStop.단계유형)} 경로를 계산할 수 없습니다.";
                    Visit(
                        nextIndex,
                        nextStop.단계유형 == PickupStage ? pickedMask | jobBit : pickedMask,
                        nextStop.단계유형 == DropoffStage ? deliveredMask | jobBit : deliveredMask,
                        now,
                        totalMinutes,
                        totalDistanceKm,
                        maxViolationMinutes,
                        violations.Concat([failed]).ToArray(),
                        pickupCompletedAtMap,
                        arrivals.Concat([now]).ToArray(),
                        route.Concat([nextStop]).ToArray());
                    continue;
                }

                var rawArrival = now.Add(routeEstimate.Duration.Value);
                var arrival = ApplyPickupReadyTime(nextStop, rawArrival);
                var travelMinutes = (decimal)(arrival - now).TotalMinutes;
                decimal? violationMinutes = null;
                var nextViolations = violations;
                var nextMaxViolation = maxViolationMinutes;
                if (nextStop.시간창종료일시.HasValue && arrival > nextStop.시간창종료일시.Value)
                {
                    violationMinutes = (decimal)(arrival - nextStop.시간창종료일시.Value).TotalMinutes;
                    nextMaxViolation = !nextMaxViolation.HasValue
                        ? violationMinutes
                        : Math.Max(nextMaxViolation.Value, violationMinutes.Value);
                    nextViolations = violations
                        .Concat([$"{nextStop.의뢰Id} {StageName(nextStop.단계유형)} 시간창을 {Math.Round(violationMinutes.Value, 0):0}분 초과합니다."])
                        .ToArray();
                }

                var nextPickupCompletedAtMap = pickupCompletedAtMap;
                if (nextStop.단계유형 == PickupStage)
                {
                    nextPickupCompletedAtMap = new Dictionary<int, DateTime>(pickupCompletedAtMap)
                    {
                        [nextStop.JobIndex] = arrival
                    };
                }
                else
                {
                    ApplyDeliveryCompletionLimit(
                        nextStop,
                        arrival,
                        pickupCompletedAtMap,
                        ref nextMaxViolation,
                        ref nextViolations);
                }

                Visit(
                    nextIndex,
                    nextStop.단계유형 == PickupStage ? pickedMask | jobBit : pickedMask,
                    nextStop.단계유형 == DropoffStage ? deliveredMask | jobBit : deliveredMask,
                    arrival,
                    totalMinutes + travelMinutes,
                    totalDistanceKm + (routeEstimate.DistanceKm ?? 0m),
                    nextMaxViolation,
                    nextViolations,
                    nextPickupCompletedAtMap,
                    arrivals.Concat([arrival]).ToArray(),
                    route.Concat([nextStop]).ToArray());
            }
        }

        public 픽업하차경로최적화결과 BuildResult(string dispatchBundleType, bool driverLocationIncluded)
        {
            var selected = _bestFeasible ?? _bestFallback;
            if (selected is null)
            {
                return new 픽업하차경로최적화결과(false, false, null, null, null, [], ["가능한 경로 후보가 없습니다."], [], dispatchBundleType, 기사현재좌표반영여부: driverLocationIncluded);
            }

            return new 픽업하차경로최적화결과(
                _bestFeasible is not null,
                selected.Violations.Count == 0,
                selected.TotalMinutes,
                selected.TotalDistanceKm,
                selected.MaxViolationMinutes,
                selected.Route.Select((x, index) => new 픽업하차경로방문순서(
                    index,
                    x.의뢰Id,
                    x.단계유형,
                    x.주소,
                    x.좌표,
                    index < selected.Arrivals.Count ? selected.Arrivals[index] : null)).ToArray(),
                selected.Violations,
                selected.Route.Select(x => $"{StageName(x.단계유형)} {x.의뢰Id}").ToArray(),
                dispatchBundleType,
                기사현재좌표반영여부: driverLocationIncluded,
                좌표근사총소요시간분: selected.TotalMinutes,
                좌표근사총거리Km: selected.TotalDistanceKm);
        }

        private void KeepBest(CandidateResult candidate)
        {
            if (candidate.Violations.Count == 0)
            {
                if (IsBetter(candidate, _bestFeasible))
                {
                    _bestFeasible = candidate;
                }

                return;
            }

            if (IsBetterFallback(candidate, _bestFallback))
            {
                _bestFallback = candidate;
            }
        }

        private static bool IsBetter(CandidateResult candidate, CandidateResult? current)
        {
            return current is null
                   || candidate.TotalMinutes < current.TotalMinutes
                   || (candidate.TotalMinutes == current.TotalMinutes && candidate.TotalDistanceKm < current.TotalDistanceKm);
        }

        private static bool IsBetterFallback(CandidateResult candidate, CandidateResult? current)
        {
            return current is null
                   || (candidate.MaxViolationMinutes ?? decimal.MaxValue) < (current.MaxViolationMinutes ?? decimal.MaxValue)
                   || (candidate.MaxViolationMinutes == current.MaxViolationMinutes && candidate.TotalMinutes < current.TotalMinutes);
        }

        private static string StageName(string stageType)
            => stageType == PickupStage ? "상차" : "하차";

        private DateTime ApplyPickupReadyTime(픽업하차경로정류장 stop, DateTime rawArrival)
        {
            if (stop.단계유형 != PickupStage)
            {
                return rawArrival;
            }

            var readyAt = jobs[stop.JobIndex].픽업가능시각Utc;
            return readyAt.HasValue && rawArrival < readyAt.Value ? readyAt.Value : rawArrival;
        }

        private void ApplyDeliveryCompletionLimit(
            픽업하차경로정류장 stop,
            DateTime arrival,
            IReadOnlyDictionary<int, DateTime> pickupCompletedAtMap,
            ref decimal? maxViolationMinutes,
            ref IReadOnlyList<string> violations)
        {
            var job = jobs[stop.JobIndex];
            if (!job.배달완료제한분.HasValue || job.배달완료제한분.Value <= 0m)
            {
                return;
            }

            var qualityStart = job.픽업가능시각Utc;
            if (!qualityStart.HasValue && pickupCompletedAtMap.TryGetValue(stop.JobIndex, out var pickupCompletedAt))
            {
                qualityStart = pickupCompletedAt;
            }

            if (!qualityStart.HasValue)
            {
                return;
            }

            var limit = ResolveDeliveryCompletionLimit(
                qualityStart.Value,
                job.배달완료제한분.Value,
                arrival,
                offPeakOverrunMinutes,
                peakOverrunMinutes);
            if (arrival <= limit.마감시각Utc)
            {
                return;
            }

            var violationMinutes = (decimal)(arrival - limit.마감시각Utc).TotalMinutes;
            maxViolationMinutes = !maxViolationMinutes.HasValue
                ? violationMinutes
                : Math.Max(maxViolationMinutes.Value, violationMinutes);
            violations = violations
                .Concat([$"{stop.의뢰Id} 하차가 안내된 배달 완료 제한을 {Math.Round(violationMinutes, 0):0}분 초과합니다. 제한={job.배달완료제한분.Value:0}분, 허용초과={limit.허용초과분:0}분"])
                .ToArray();
        }
    }

    private sealed record RouteMatrix(배차경로예상결과?[] StartRoutes, 배차경로예상결과?[,] StopRoutes)
    {
        public 배차경로예상결과? Get(int fromStopIndex, int toStopIndex)
            => fromStopIndex < 0 ? StartRoutes[toStopIndex] : StopRoutes[fromStopIndex, toStopIndex];
    }

    private sealed record SearchKey(int CurrentStopIndex, int PickedMask, int DeliveredMask);

    private sealed record CandidateResult(
        IReadOnlyList<픽업하차경로정류장> Route,
        IReadOnlyList<DateTime> Arrivals,
        decimal TotalMinutes,
        decimal TotalDistanceKm,
        decimal? MaxViolationMinutes,
        IReadOnlyList<string> Violations);
}

public sealed record 픽업하차경로최적화요청(
    DateTime 기준시각,
    배차경로좌표? 시작좌표,
    IReadOnlyList<픽업하차경로작업> 작업목록,
    int 최대작업수 = 7,
    decimal 좌표기반도로보정계수 = 1.25m,
    decimal 좌표기반평균속도KmH = 35m,
    decimal? 최대총거리Km = null,
    decimal 일반시간대배달완료허용초과분 = 0m,
    decimal 피크시간대배달완료허용초과분 = 20m,
    string? RouteOption = null)
{
    public 배차경로좌표? 기사현재좌표 => 시작좌표;

    public bool 기사현재좌표반영여부 => 기사현재좌표 is not null;
}

public sealed record 픽업하차경로작업(
    string 의뢰Id,
    string 픽업주소,
    배차경로좌표? 픽업좌표,
    DateTime? 픽업시간창종료일시,
    string 하차주소,
    배차경로좌표? 하차좌표,
    DateTime? 하차시간창종료일시,
    DateTime? 픽업가능시각Utc = null,
    decimal? 배달완료제한분 = null,
    string? 픽업배달권키 = null,
    string? 하차배달권키 = null,
    bool 이미픽업완료 = false,
    DateTime? 픽업완료시각Utc = null);

public sealed record 픽업하차경로방문순서(
    int 순서,
    string 의뢰Id,
    string 단계유형,
    string 주소,
    배차경로좌표? 좌표,
    DateTime? 예상도착시각Utc = null);

public sealed record 픽업하차경로최적화결과(
    bool 최적화가능여부,
    bool 전체완수가능여부,
    decimal? 총소요시간분,
    decimal? 총거리Km,
    decimal? 최대시간위반분,
    IReadOnlyList<픽업하차경로방문순서> 방문순서,
    IReadOnlyList<string> 위반사유,
    IReadOnlyList<string> 권장경로순서,
    string 배차묶음유형,
    bool 기사현재좌표반영여부 = false,
    string 비용계산방식 = "좌표근사",
    bool 실제경로검증여부 = false,
    decimal? 좌표근사총소요시간분 = null,
    decimal? 좌표근사총거리Km = null);

internal sealed record 픽업하차경로정류장(
    int JobIndex,
    string 단계유형,
    string 의뢰Id,
    string 주소,
    배차경로좌표? 좌표,
    DateTime? 시간창종료일시);

internal sealed record 배달완료제한판정(DateTime 마감시각Utc, decimal 허용초과분);
