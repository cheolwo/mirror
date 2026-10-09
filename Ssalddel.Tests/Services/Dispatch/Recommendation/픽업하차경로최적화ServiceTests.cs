using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed partial class 픽업하차경로최적화ServiceTests
{
    [Fact]
    public async Task 최적화는_각_의뢰의_상차전_하차를_금지하면서_최소경로를_선택한다()
    {
        var routeService = new 직선시간경로Service();
        var service = new 픽업하차경로최적화Service(routeService);
        var now = new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc);

        var result = await service.최적화Async(new 픽업하차경로최적화요청(
            now,
            new 배차경로좌표(0m, 0m),
            [
                new 픽업하차경로작업(
                    "1",
                    "상차지 1",
                    new 배차경로좌표(1m, 0m),
                    null,
                    "하차지 1",
                    new 배차경로좌표(10m, 0m),
                    null),
                new 픽업하차경로작업(
                    "2",
                    "상차지 2",
                    new 배차경로좌표(2m, 0m),
                    null,
                    "하차지 2",
                    new 배차경로좌표(3m, 0m),
                    null)
            ]));

        Assert.True(result.최적화가능여부);
        Assert.True(result.전체완수가능여부);
        Assert.True(result.실제경로검증여부);
        Assert.Equal("Directions5", result.비용계산방식);
        Assert.Equal(100m, result.총소요시간분);
        Assert.Equal(
            ["상차 1", "상차 2", "하차 2", "하차 1"],
            result.권장경로순서);
        Assert.Equal(0, routeService.PointToPointCallCount);
        Assert.Equal(1, routeService.OrderedRouteCallCount);
        AssertPrecedence(result);
    }

    [Fact]
    public async Task 최적화는_두번째_상차를_먼저_가는_경로도_후보로_평가한다()
    {
        var routeService = new 직선시간경로Service();
        var service = new 픽업하차경로최적화Service(routeService);
        var now = new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc);

        var result = await service.최적화Async(new 픽업하차경로최적화요청(
            now,
            new 배차경로좌표(0m, 0m),
            [
                new 픽업하차경로작업(
                    "1",
                    "상차지 1",
                    new 배차경로좌표(10m, 0m),
                    null,
                    "하차지 1",
                    new 배차경로좌표(11m, 0m),
                    null),
                new 픽업하차경로작업(
                    "2",
                    "상차지 2",
                    new 배차경로좌표(1m, 0m),
                    null,
                    "하차지 2",
                    new 배차경로좌표(2m, 0m),
                    null)
            ]));

        Assert.True(result.최적화가능여부);
        Assert.Equal(
            ["상차 2", "하차 2", "상차 1", "하차 1"],
            result.권장경로순서);
        Assert.Equal(0, routeService.PointToPointCallCount);
        Assert.Equal(1, routeService.OrderedRouteCallCount);
        AssertPrecedence(result);
    }

    [Fact]
    public async Task 기사현재위치는_첫_상차지_선택에_반영된다()
    {
        var routeService = new 직선시간경로Service();
        var service = new 픽업하차경로최적화Service(routeService);
        var now = new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc);

        var result = await service.최적화Async(new 픽업하차경로최적화요청(
            now,
            new 배차경로좌표(9m, 0m),
            [
                new 픽업하차경로작업(
                    "1",
                    "상차지 1",
                    new 배차경로좌표(1m, 0m),
                    null,
                    "하차지 1",
                    new 배차경로좌표(2m, 0m),
                    null),
                new 픽업하차경로작업(
                    "2",
                    "상차지 2",
                    new 배차경로좌표(10m, 0m),
                    null,
                    "하차지 2",
                    new 배차경로좌표(11m, 0m),
                    null)
            ]));

        Assert.True(result.기사현재좌표반영여부);
        Assert.Equal("멀티배차", result.배차묶음유형);
        Assert.Equal(
            ["상차 2", "하차 2", "상차 1", "하차 1"],
            result.권장경로순서);
        AssertPrecedence(result);
    }

    [Fact]
    public async Task 작업이_한건이면_단건배차로_분류한다()
    {
        var service = new 픽업하차경로최적화Service(new 직선시간경로Service());
        var now = new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc);

        var result = await service.최적화Async(new 픽업하차경로최적화요청(
            now,
            new 배차경로좌표(0m, 0m),
            [
                new 픽업하차경로작업(
                    "1",
                    "상차지 1",
                    new 배차경로좌표(1m, 0m),
                    null,
                    "하차지 1",
                    new 배차경로좌표(2m, 0m),
                    null)
            ]));

        Assert.Equal("단건배차", result.배차묶음유형);
        Assert.Equal(["상차 1", "하차 1"], result.권장경로순서);
    }

    [Fact]
    public async Task 최적화는_조리완료후_안내된_최대배달시간을_초과하는_경로를_제외한다()
    {
        var service = new 픽업하차경로최적화Service(new 직선시간경로Service());
        var now = new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc);
        var job = new 픽업하차경로작업(
            "1",
            "음식점",
            new 배차경로좌표(1m, 0m),
            null,
            "고객 주소",
            new 배차경로좌표(3m, 0m),
            null,
            now,
            15m);

        var result = await service.최적화Async(new 픽업하차경로최적화요청(
            now,
            new 배차경로좌표(0m, 0m),
            [job]));

        Assert.False(result.최적화가능여부);
        Assert.False(result.전체완수가능여부);
        Assert.Contains(result.위반사유, x => x.Contains("배달 완료 제한", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 최적화는_피크타임에는_배달완료_허용초과분을_반영한다()
    {
        var service = new 픽업하차경로최적화Service(new 직선시간경로Service());
        var lunchPeak = new DateTime(2026, 7, 10, 3, 0, 0, DateTimeKind.Utc);
        var job = new 픽업하차경로작업(
            "1",
            "음식점",
            new 배차경로좌표(1m, 0m),
            null,
            "고객 주소",
            new 배차경로좌표(3m, 0m),
            null,
            lunchPeak,
            15m);

        var result = await service.최적화Async(new 픽업하차경로최적화요청(
            lunchPeak,
            new 배차경로좌표(0m, 0m),
            [job],
            피크시간대배달완료허용초과분: 20m));

        Assert.True(result.최적화가능여부);
        Assert.True(result.전체완수가능여부);
        Assert.DoesNotContain(result.위반사유, x => x.Contains("배달 완료 제한", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 최적화는_멀티배차_총운행거리_상한을_검증한다()
    {
        var service = new 픽업하차경로최적화Service(new 직선시간경로Service());
        var now = new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc);

        var result = await service.최적화Async(new 픽업하차경로최적화요청(
            now,
            new 배차경로좌표(0m, 0m),
            [
                new 픽업하차경로작업(
                    "1",
                    "상차지 1",
                    new 배차경로좌표(1m, 0m),
                    null,
                    "하차지 1",
                    new 배차경로좌표(2m, 0m),
                    null)
            ],
            최대총거리Km: 1m));

        Assert.False(result.최적화가능여부);
        Assert.Contains(result.위반사유, x => x.Contains("총 운행거리", StringComparison.Ordinal));
    }

    private static void AssertPrecedence(픽업하차경로최적화결과 result)
    {
        var pickups = result.방문순서
            .Where(x => x.단계유형 == "pickup")
            .ToDictionary(x => x.의뢰Id, x => x.순서, StringComparer.Ordinal);
        var dropoffs = result.방문순서
            .Where(x => x.단계유형 == "dropoff")
            .ToDictionary(x => x.의뢰Id, x => x.순서, StringComparer.Ordinal);

        foreach (var id in pickups.Keys)
        {
            Assert.True(pickups[id] < dropoffs[id], $"{id} 하차가 상차보다 먼저 배치되었습니다.");
        }
    }

    private sealed class 직선시간경로Service : I배차추천경로Service
    {
        public int PointToPointCallCount { get; private set; }

        public int OrderedRouteCallCount { get; private set; }

        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
        {
            PointToPointCallCount++;
            if (origin is null || destination is null)
            {
                return Task.FromResult<배차경로예상결과?>(null);
            }

            var distance = Math.Abs(destination.Latitude - origin.Latitude) + Math.Abs(destination.Longitude - origin.Longitude);
            var duration = TimeSpan.FromMinutes((double)(distance * 10m));
            return Task.FromResult<배차경로예상결과?>(new 배차경로예상결과(distance, duration, null, "Directions5", true));
        }

        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(
            배차경로좌표? origin,
            IReadOnlyList<배차경로좌표> orderedStops,
            CancellationToken cancellationToken = default)
        {
            OrderedRouteCallCount++;
            if (origin is null || orderedStops.Count == 0)
            {
                return Task.FromResult<배차경로예상결과?>(null);
            }

            var current = origin;
            decimal distance = 0m;
            foreach (var stop in orderedStops)
            {
                distance += CalculateDistanceKm(current, stop) ?? 0m;
                current = stop;
            }

            var duration = TimeSpan.FromMinutes((double)(distance * 10m));
            return Task.FromResult<배차경로예상결과?>(new 배차경로예상결과(distance, duration, null, "Directions5", true));
        }

        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);

        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
            => Math.Abs(target.Latitude - source.Latitude) + Math.Abs(target.Longitude - source.Longitude);
    }
}
