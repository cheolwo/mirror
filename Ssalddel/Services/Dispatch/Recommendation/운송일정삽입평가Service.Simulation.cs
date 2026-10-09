namespace 살뜰.Services.Dispatch.Recommendation
{
    public sealed partial class 운송일정삽입평가Service
    {
        private async Task<(bool 전체완수가능여부, decimal? 총소요시간분, decimal? 총거리Km, decimal? 최대시간위반분, string[] 위반사유, IReadOnlyList<운송일정도착예상항목> 도착예상목록, 배차경로좌표? 종료좌표, bool 전체실제경로확인됨, string? 경로옵션)> 시뮬레이션Async(
            배차경로좌표? 시작좌표,
            IReadOnlyList<기사운송일정항목> 항목목록,
            DateTime 기준시각,
            CancellationToken cancellationToken)
        {
            var nowPoint = 시작좌표;
            var nowTime = 기준시각;
            decimal totalMinutes = 0m;
            decimal totalDistance = 0m;
            decimal? maxViolation = null;
            var violations = new List<string>();
            var arrivals = new List<운송일정도착예상항목>(항목목록.Count);
            var allActualRoutes = 시작좌표 is not null;
            string? routeOption = null;

            foreach (var item in 항목목록.OrderBy(x => x.순서))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var route = await _routeService.EstimateRouteAsync(nowPoint, item.좌표);
                allActualRoutes &= route is { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
                    && route.Duration.Value >= TimeSpan.Zero
                    && !string.IsNullOrWhiteSpace(route.RouteOption);
                if (route?.RouteOption is { } option)
                {
                    if (routeOption is not null && !string.Equals(routeOption, option, StringComparison.Ordinal))
                        allActualRoutes = false;
                    routeOption ??= option;
                }
                if (route?.Duration is null)
                {
                    violations.Add($"{item.의뢰Id} {item.단계유형} 경로를 계산할 수 없습니다.");
                    arrivals.Add(new 운송일정도착예상항목(item.의뢰Id, item.단계유형, item.순서, item.주소, null, item.시간창종료일시, true, null));
                    nowPoint = item.좌표;
                    continue;
                }

                totalMinutes += (decimal)route.Duration.Value.TotalMinutes;
                totalDistance += route.DistanceKm ?? 0m;

                var rawArrival = nowTime.Add(route.Duration.Value);
                var effectiveArrival = item.기준시각.HasValue && rawArrival < item.기준시각.Value
                    ? item.기준시각.Value
                    : rawArrival;

                decimal? violationMinutes = null;
                var violates = false;
                if (item.시간창종료일시.HasValue && effectiveArrival > item.시간창종료일시.Value)
                {
                    violates = true;
                    violationMinutes = (decimal)(effectiveArrival - item.시간창종료일시.Value).TotalMinutes;
                    maxViolation = !maxViolation.HasValue
                        ? violationMinutes
                        : Math.Max(maxViolation.Value, violationMinutes.Value);
                    violations.Add($"{item.의뢰Id} {item.단계유형} 시간창을 {Math.Round(violationMinutes.Value, 0):0}분 초과합니다.");
                }

                arrivals.Add(new 운송일정도착예상항목(
                    item.의뢰Id,
                    item.단계유형,
                    item.순서,
                    item.주소,
                    effectiveArrival,
                    item.시간창종료일시,
                    violates,
                    violationMinutes.HasValue ? Math.Round(violationMinutes.Value, 2) : null));

                nowTime = effectiveArrival;
                nowPoint = item.좌표;
            }

            return (violations.Count == 0, Math.Round(totalMinutes, 2), Math.Round(totalDistance, 2), maxViolation.HasValue ? Math.Round(maxViolation.Value, 2) : null, violations.Distinct(StringComparer.Ordinal).ToArray(), arrivals, nowPoint, allActualRoutes, routeOption);
        }
    }
}
