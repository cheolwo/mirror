using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 기사권역복귀ServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc);
    private static readonly 배차경로좌표 Home = new(37.5m, 127m);
    private static readonly 배차경로좌표 Current = new(37.6m, 127m);
    private static readonly 배차경로좌표 Before = new(37.7m, 127m);
    private static readonly 배차경로좌표 After = new(37.8m, 127m);

    [Fact]
    public async Task 기본꺼짐은기존복귀정책을선택해제하며도로API를호출하지않는다()
    {
        var (service, store, route) = Create();
        store.Current.설정.Mode = "off";
        var state = await service.상태조회Async("driver", Current, Now, 0m);
        Assert.False(state.정책선택됨);
        Assert.Null(state.기준점);
        Assert.Empty(route.Requests);
        Assert.Equal(0, store.ActivationWrites);
    }

    [Theory]
    [InlineData(6.99, false)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    public async Task 반경진입은현재GPS직선거리로판정한다(double km, bool expected)
    {
        var (service, store, route) = Create();
        route.RadiusKm = (decimal)km;
        var state = await service.상태조회Async("driver", Current, Now, 0m);
        Assert.True(state.정책선택됨);
        Assert.Equal(expected, state.활성여부);
        Assert.Equal(expected, store.Current.활성여부);
        Assert.Empty(route.Requests);
    }

    [Theory]
    [InlineData(6.5, true)]
    [InlineData(6, false)]
    public async Task 이미진입한상태는해제반경까지유지한다(double km, bool expected)
    {
        var (service, store, route) = Create();
        store.Current = store.Current with { 활성여부 = true, 위치판정시각Utc = Now.AddSeconds(-10) };
        route.RadiusKm = (decimal)km;
        var state = await service.상태조회Async("driver", Current, Now, 0m);
        Assert.Equal(expected, state.활성여부);
    }

    [Fact]
    public async Task 위치오차가경계에겹치면우대를섣불리전환하지않는다()
    {
        var (service, store, route) = Create();
        route.RadiusKm = 7.01m;
        Assert.False((await service.상태조회Async("driver", Current, Now, 50m)).활성여부);
        store.Current = store.Current with { 활성여부 = true, 위치판정시각Utc = Now.AddSeconds(-10) };
        route.RadiusKm = 5.99m;
        Assert.True((await service.상태조회Async("driver", Current, Now, 50m)).활성여부);
    }

    [Fact]
    public async Task 오래된위치미확인은레거시점수로우회하지않는다()
    {
        var (service, store, route) = Create();
        var state = await service.상태조회Async("driver", Current, Now.AddMinutes(-6), 0m);
        Assert.True(state.정책선택됨);
        Assert.False(state.활성여부);
        Assert.Null(state.기준점직선거리Km);
        Assert.Empty(route.Requests);
        Assert.Equal(0, store.ActivationWrites);
    }

    [Fact]
    public async Task 늦게온GPS가활성상태를되돌리지않는다()
    {
        var (service, store, _) = Create();
        store.Current = store.Current with { 활성여부 = true, 위치판정시각Utc = Now };
        var state = await service.상태조회Async("driver", Current, Now.AddSeconds(-1), 0m);
        Assert.False(state.활성여부);
        Assert.True(store.Current.활성여부);
        Assert.Equal(0, store.ActivationWrites);
    }

    [Fact]
    public async Task 수동우대도유효한현재위치는필요하다()
    {
        var (service, store, route) = Create();
        store.Current.설정.Mode = "on";
        route.RadiusKm = 1m;
        Assert.True((await service.상태조회Async("driver", Current, Now, 0m)).활성여부);
        Assert.False((await service.상태조회Async("driver", Current, null)).활성여부);
    }

    [Fact]
    public async Task 설정동시변경뒤예전기준점으로판정하지않는다()
    {
        var (service, store, _) = Create();
        store.FailNextUpdateAndDisable = true;
        var state = await service.상태조회Async("driver", Current, Now, 0m);
        Assert.False(state.정책선택됨);
        Assert.Equal(2, state.설정Revision);
    }

    [Theory]
    [InlineData(8, 5, 20)]
    [InlineData(8, 6.5, 10)]
    [InlineData(8, 11, 0)]
    [InlineData(4, 6, 0)]
    public async Task 기존전체업무끝점과후보포함끝점의실경로개선에만가점(double before, double after, double score)
    {
        var (service, _, route) = CreateForEvaluation();
        route.BeforeKm = (decimal)before;
        route.AfterKm = (decimal)after;
        var evaluation = await service.평가Async(Active(), Before, After, true, true, "traavoidcaronly");
        Assert.True(evaluation.확인됨);
        Assert.Equal((decimal)score, evaluation.가점);
        Assert.Equal((decimal)before, evaluation.기존복귀거리Km);
        Assert.Equal((decimal)after, evaluation.후보복귀거리Km);
        Assert.All(route.Requests, r => Assert.Equal("traavoidcaronly", r.Option));
        Assert.DoesNotContain(Current, route.Requests.Select(r => r.From));
    }

    [Fact]
    public async Task 도로실경로가없으면근사거리를가점근거로쓰지않는다()
    {
        var (service, _, route) = CreateForEvaluation();
        route.Actual = false;
        var result = await service.평가Async(Active(), Before, After, true, true);
        Assert.False(result.확인됨);
        Assert.Equal(0m, result.가점);
        Assert.Null(result.후보복귀거리Km);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 불완전일정또는부적격후보를가점으로살리지않는다(bool verified, bool eligible)
    {
        var (service, _, route) = CreateForEvaluation();
        var result = await service.평가Async(Active(), Before, After, verified, eligible);
        Assert.Equal(0m, result.가점);
        Assert.False(result.확인됨);
        Assert.Empty(route.Requests);
    }

    [Fact]
    public async Task 기준점과끝점동일한실제0거리는API없이확인한다()
    {
        var (service, _, route) = CreateForEvaluation();
        var result = await service.평가Async(Active(), Before, Home, true, true);
        Assert.True(result.확인됨);
        Assert.Equal(0m, result.후보복귀거리Km);
        Assert.Equal(20m, result.가점);
        Assert.Single(route.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1d)]
    [InlineData(101d)]
    public async Task UnknownOrInvalidAccuracyDoesNotActivatePreference(double? accuracy)
    {
        var (service, store, _) = CreateForEvaluation();
        var state = await service.상태조회Async("driver", Current, Now, accuracy.HasValue ? (decimal)accuracy.Value : null);
        Assert.True(state.정책선택됨);
        Assert.False(state.활성여부);
        Assert.Equal(0, store.ActivationWrites);
    }

    [Fact]
    public async Task VehicleOptionMismatchDoesNotAwardReturnBonus()
    {
        var (service, _, route) = CreateForEvaluation();
        route.ReportedOption = "trafast";
        var result = await service.평가Async(Active(), Before, After, true, true, "traavoidcaronly");
        Assert.False(result.확인됨);
        Assert.Equal(0m, result.가점);
    }

    private static 기사권역복귀상태 Active() => new(true, true, 8m, Home, 1, "적용 중") { DriverId = "driver" };
    private static (기사권역복귀Service, Store, Routes) CreateForEvaluation()
    {
        var context = Create();
        context.Item2.Current = context.Item2.Current with { 활성여부 = true, 위치판정시각Utc = Now };
        return context;
    }

    [Theory]
    [InlineData("off")]
    [InlineData("anchor")]
    [InlineData("gps")]
    public async Task SettingsOrGpsChangedDuringRoadLookupDoesNotKeepEarlierBonus(string change)
    {
        var (service, store, route) = CreateForEvaluation();
        route.AfterRequest = () =>
        {
            if (change == "gps") store.Current = store.Current with { 활성여부 = false };
            else
            {
                store.Current.설정.Revision++;
                if (change == "off") store.Current.설정.Mode = "off";
                else store.Current.설정.기준위도 = 38m;
            }
        };
        var result = await service.평가Async(Active(), Before, After, true, true, "traavoidcaronly");
        Assert.False(result.확인됨);
        Assert.Equal(0m, result.가점);
        Assert.Null(result.기존복귀거리Km);
    }

    private static (기사권역복귀Service, Store, Routes) Create()
    {
        var store = new Store();
        var route = new Routes();
        return (new 기사권역복귀Service(store, route, Options.Create(new 기사권역복귀정책Options()), new Clock()), store, route);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Store : I기사권역복귀설정Store
    {
        public 기사권역복귀설정Snapshot Current = new(new 기사권역복귀설정
        { Revision = 1, Mode = "automatic", 기준위치명 = "활동 중심", 기준위도 = Home.Latitude, 기준경도 = Home.Longitude });
        public int ActivationWrites;
        public bool FailNextUpdateAndDisable;
        public Task<기사권역복귀설정Snapshot> 조회Async(string driverId, CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task<기사권역복귀설정Snapshot> 저장Async(string driverId, 기사권역복귀설정 settings, long expectedRevision, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> 활성상태갱신Async(string driverId, long revision, bool active, DateTime positionRecordedUtc, CancellationToken cancellationToken = default)
        {
            ActivationWrites++;
            if (FailNextUpdateAndDisable)
            {
                FailNextUpdateAndDisable = false;
                Current = new(new 기사권역복귀설정 { Mode = "off", Revision = 2 });
                return Task.FromResult(false);
            }
            if (revision != Current.설정.Revision || Current.위치판정시각Utc > positionRecordedUtc) return Task.FromResult(false);
            Current = Current with { 활성여부 = active, 위치판정시각Utc = positionRecordedUtc };
            return Task.FromResult(true);
        }
    }

    private sealed class Routes : I배차추천경로Service
    {
        public decimal RadiusKm = 8m, BeforeKm = 8m, AfterKm = 5m;
        public bool Actual = true;
        public string? ReportedOption;
        public Action? AfterRequest;
        public List<(배차경로좌표? From, string? Option)> Requests { get; } = [];
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => RadiusKm;
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => EstimateRouteAsync(origin, destination, "default", CancellationToken.None);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string option, CancellationToken cancellationToken)
        {
            Requests.Add((origin, option));
            AfterRequest?.Invoke();
            return Task.FromResult<배차경로예상결과?>(new(origin == Before ? BeforeKm : AfterKm,
                TimeSpan.FromMinutes(10), 0m, "TestDirections", Actual) { RouteOption = ReportedOption ?? option });
        }
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
    }
}
