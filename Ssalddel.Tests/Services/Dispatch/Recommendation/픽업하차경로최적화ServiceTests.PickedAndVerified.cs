using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed partial class 픽업하차경로최적화ServiceTests
{
    [Fact]
    public async Task 옵션미지정과미픽업기본값은기존경로결과를유지한다()
    {
        var now = DateTime.UtcNow; var point = new 배차경로좌표(37.5m, 127m);
        var job = new 픽업하차경로작업("one", "가게", point, null, "고객", new(37.51m, 127m), null);
        var route = new 직선시간경로Service(); var service = new 픽업하차경로최적화Service(route);
        var before = await service.최적화Async(new(now, point, [job]));
        var after = await service.최적화Async(new(now, point, [job with { 이미픽업완료 = false, 픽업완료시각Utc = null }], RouteOption: null));
        Assert.Equal(before.방문순서, after.방문순서); Assert.Equal(before.총소요시간분, after.총소요시간분);
        Assert.Equal(before.총거리Km, after.총거리Km); Assert.Equal(before.실제경로검증여부, after.실제경로검증여부);
        Assert.Equal(2, route.OrderedRouteCallCount);
    }

    [Fact]
    public async Task 이미픽업한음식은남은전달만계산하고품질마감을유지한다()
    {
        var now = DateTime.UtcNow; var point = new 배차경로좌표(37.5m, 127m);
        var result = await new 픽업하차경로최적화Service(new ExactVehicleRoute()).최적화Async(new(now, point,
            [new("picked", "옛가게", new(38m, 128m), null, "고객", new(37.501m, 127m), now.AddMinutes(5),
                배달완료제한분: 5, 이미픽업완료: true, 픽업완료시각Utc: now.AddMinutes(-1))], RouteOption: "traavoidcaronly"));
        Assert.True(result.전체완수가능여부); Assert.True(result.실제경로검증여부);
        Assert.Single(result.방문순서); Assert.Equal("dropoff", result.방문순서[0].단계유형);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 이미픽업작업의픽업시각또는품질마감이없으면전체실경로로인증하지않는다(bool pickupKnown)
    {
        var now = DateTime.UtcNow; var point = new 배차경로좌표(37.5m, 127m);
        var result = await new 픽업하차경로최적화Service(new ExactVehicleRoute()).최적화Async(new(now, point,
            [new("picked", "가게", point, null, "고객", new(37.501m, 127m), null,
                배달완료제한분: pickupKnown ? null : 5, 이미픽업완료: true,
                픽업완료시각Utc: pickupKnown ? now.AddMinutes(-1) : null)], RouteOption: "traavoidcaronly"));
        Assert.False(result.전체완수가능여부); Assert.False(result.실제경로검증여부);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task 옵션지정은모든구간의실제경로와시간창을검사한다(bool actual, bool slow)
    {
        var now = DateTime.UtcNow; var point = new 배차경로좌표(37.5m, 127m);
        var result = await new 픽업하차경로최적화Service(new ExactVehicleRoute(actual, slow)).최적화Async(new(now, point,
            [new("new", "가게", new(37.5001m, 127m), now.AddMinutes(3), "고객", new(37.5002m, 127m), now.AddMinutes(5))],
            RouteOption: "traavoidcaronly"));
        Assert.False(result.전체완수가능여부);
        Assert.Equal(actual, result.실제경로검증여부);
    }

    private sealed class ExactVehicleRoute(bool actual = true, bool slow = false) : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? start, 배차경로좌표? end) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? start, 배차경로좌표? end, string option, CancellationToken token)
            => Task.FromResult<배차경로예상결과?>(new(0.1m, TimeSpan.FromMinutes(slow ? 10 : 1), null, "Directions5", actual) { RouteOption = option });
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? start, IReadOnlyList<배차경로좌표> stops, CancellationToken token = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? start, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 start, 배차경로좌표 end) => 0.1m;
    }
}
