using 살뜰.Services.External.Naver;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public sealed partial class 배차추천경로Service
    {
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => EstimateRouteAsync(origin, destination, CancellationToken.None);

        public async Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, CancellationToken cancellationToken)
            => await EstimateRouteAsync(origin, destination, _routeOptions.DefaultOption, cancellationToken);

        public async Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string routeOption, CancellationToken cancellationToken)
        {
            if (routeOption is not ("trafast" or "tracomfort" or "traoptimal" or "traavoidtoll" or "traavoidcaronly"))
                throw new ArgumentException("FoodPricingRouteOptionInvalid");
            cancellationToken.ThrowIfCancellationRequested();
            if (origin is null || destination is null)
            {
                return null;
            }

            var cacheKey = 배차경로CacheKey.Create(origin, destination, routeOption);
            if (_routeEstimateCache.TryGetValue(cacheKey, out var cachedTask))
            {
                return await cachedTask.WaitAsync(cancellationToken);
            }

            var task = EstimateRouteCoreAsync(origin, destination, routeOption, cancellationToken);
            _routeEstimateCache[cacheKey] = task;

            try
            {
                return await task;
            }
            catch
            {
                _routeEstimateCache.Remove(cacheKey);
                throw;
            }
        }

        private async Task<배차경로예상결과?> EstimateRouteCoreAsync(배차경로좌표 origin, 배차경로좌표 destination, string routeOption, CancellationToken cancellationToken)
        {
            try
            {
                var route = await _routeService.GetDrivingRouteAsync(origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude, option: routeOption, cancellationToken: cancellationToken);
                if (route?.Duration is not null || route?.DistanceKm is not null)
                {
                    return new 배차경로예상결과(route.DistanceKm, route.Duration, route.TollFare, "Directions5", true) { RouteOption = route.RouteType };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "네이버 경로 API 호출에 실패해 fallback 경로 추정을 사용합니다. Origin={OriginLat},{OriginLng} Destination={DestinationLat},{DestinationLng}",
                    origin.Latitude,
                    origin.Longitude,
                    destination.Latitude,
                    destination.Longitude);
            }

            return EstimateFallbackRoute(origin, destination);
        }

        public async Task<배차경로예상결과?> EstimateOrderedRouteAsync(
            배차경로좌표? origin,
            IReadOnlyList<배차경로좌표> orderedStops,
            CancellationToken cancellationToken = default)
        {
            if (origin is null || orderedStops.Count == 0)
            {
                return null;
            }

            if (orderedStops.Count == 1)
            {
                return await EstimateRouteAsync(origin, orderedStops[0], cancellationToken);
            }

            var goal = orderedStops[^1];
            var waypointCount = orderedStops.Count - 1;
            if (waypointCount <= 5)
            {
                try
                {
                    var waypoints = orderedStops
                        .Take(waypointCount)
                        .Select(x => new NaverCloudRouteWaypoint(x.Latitude, x.Longitude))
                        .ToArray();
                    var route = await _routeService.GetDrivingRouteAsync(
                        origin.Latitude,
                        origin.Longitude,
                        goal.Latitude,
                        goal.Longitude,
                        waypoints,
                        cancellationToken: cancellationToken);
                    if (route?.Duration is not null || route?.DistanceKm is not null)
                    {
                        return new 배차경로예상결과(route.DistanceKm, route.Duration, route.TollFare, "Directions5", true) { RouteOption = route.RouteType };
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "네이버 경유 경로 API 호출에 실패해 구간별 경로 추정으로 대체합니다. StopCount={StopCount}",
                        orderedStops.Count);
                }
            }

            return await EstimateSegmentSumAsync(origin, orderedStops);
        }

        private async Task<배차경로예상결과?> EstimateSegmentSumAsync(배차경로좌표 origin, IReadOnlyList<배차경로좌표> orderedStops)
        {
            var current = origin;
            decimal totalDistance = 0m;
            TimeSpan totalDuration = TimeSpan.Zero;
            decimal? totalTollFare = null;
            var hasAnyDistance = false;
            var hasAnyDuration = false;
            var allActualRoutes = true;
            var anySegment = false;

            foreach (var stop in orderedStops)
            {
                var segment = await EstimateRouteAsync(current, stop);
                if (segment is not null)
                {
                    anySegment = true;
                    allActualRoutes &= segment.실제경로여부;
                }

                if (segment?.DistanceKm is decimal distance)
                {
                    totalDistance += distance;
                    hasAnyDistance = true;
                }

                if (segment?.Duration is TimeSpan duration)
                {
                    totalDuration += duration;
                    hasAnyDuration = true;
                }

                if (segment?.TollFare is decimal tollFare)
                {
                    totalTollFare = (totalTollFare ?? 0m) + tollFare;
                }

                current = stop;
            }

            if (!hasAnyDistance && !hasAnyDuration)
            {
                return null;
            }

            return new 배차경로예상결과(
                hasAnyDistance ? Math.Round(totalDistance, 2) : null,
                hasAnyDuration ? totalDuration : null,
                totalTollFare,
                anySegment && allActualRoutes ? "Directions5-구간합산" : "구간합산",
                anySegment && allActualRoutes);
        }

        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
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

        private 배차경로예상결과? EstimateFallbackRoute(배차경로좌표 origin, 배차경로좌표 destination)
        {
            if (!_routeOptions.EnableFallbackRouteEstimate)
            {
                return null;
            }

            var directDistanceKm = CalculateDistanceKm(origin, destination);
            if (!directDistanceKm.HasValue)
            {
                return null;
            }

            var multiplier = _routeOptions.FallbackDistanceMultiplier <= 0m
                ? 1.25m
                : _routeOptions.FallbackDistanceMultiplier;
            var averageSpeedKmH = _routeOptions.FallbackAverageSpeedKmH <= 0m
                ? 45m
                : _routeOptions.FallbackAverageSpeedKmH;
            var estimatedDistanceKm = Math.Round(directDistanceKm.Value * multiplier, 2);
            var estimatedMinutes = estimatedDistanceKm == 0m
                ? 0m
                : estimatedDistanceKm / averageSpeedKmH * 60m;

            return new 배차경로예상결과(
                estimatedDistanceKm,
                TimeSpan.FromMinutes((double)estimatedMinutes),
                null,
                "좌표기반도로보정",
                false);
        }

        private static double ToRadians(double angle) => angle * Math.PI / 180.0;

        private readonly record struct 배차경로CacheKey(
            decimal OriginLatitude,
            decimal OriginLongitude,
            decimal DestinationLatitude,
            decimal DestinationLongitude, string RouteOption)
        {
            public static 배차경로CacheKey Create(배차경로좌표 origin, 배차경로좌표 destination, string option)
                => new(origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude, option);
        }
    }
}
