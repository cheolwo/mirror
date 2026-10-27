using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class CargoNextPickupR64Tests
{
    [Fact]
    public async Task 수행중기사_Gps반경에없어도_전체두업무끝점으로후보탐색한다()
    {
        await using var f = await Fixture.CreateAsync();
        var review = await f.Policy().후보검토Async(f.Queue);
        var selected = Assert.IsType<배차추천후보>(review.선정후보);
        Assert.Equal("driver", selected.DriverId);
        Assert.Equal(new 배차경로좌표(38.10m, 127m), selected.화물수행근거!.기준좌표);
        Assert.NotEqual(new 배차경로좌표(37.80m, 127m), selected.화물수행근거.기준좌표);
        Assert.Equal(2, selected.화물수행근거.기존계획!.항목목록.Count);
        Assert.Equal("VerifiedActiveScheduleEnd", Assert.Single(review.후보목록).픽업기준Code);
        Assert.True(f.States.ActiveReads > 0);
        Assert.Empty(f.States.Geo);
        Assert.Equal(0, f.Queue.추천라운드);
    }

    [Fact]
    public async Task 유휴기사_현재Gps가픽업기준이다()
    {
        await using var f = await Fixture.CreateAsync(active: false);
        f.States.Geo = f.States.Active;
        var selected = await f.Policy().다음후보선정Async(f.Queue);
        Assert.NotNull(selected);
        Assert.False(selected.화물수행근거!.활성업무있음);
        Assert.Equal(new 배차경로좌표(38.09m, 127m), selected.화물수행근거.기준좌표);
        Assert.True(f.Routes.RouteCalls > 0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 공통수신Off또는자동중지는_SharedDriverWork설정과관계없이후보차단(bool sharedEnabled, bool autoPause)
    {
        await using var f = await Fixture.CreateAsync();
        f.Availability.수신On = autoPause;
        f.Availability.자동중지 = autoPause;
        var shared = new SharedDriverWorkAdmissionService(f.Db, Options.Create(new SharedDriverWorkOptions { Enabled = sharedEnabled }));
        var review = await f.Policy(shared).후보검토Async(f.Queue);
        Assert.Null(review.선정후보);
        Assert.Equal("DispatchReceivingUnavailable", Assert.Single(review.후보목록).판정Code);
        Assert.Equal(0, f.Routes.RouteCalls);
    }

    [Theory]
    [InlineData("coordinates")]
    [InlineData("deadline")]
    [InlineData("approximate")]
    [InlineData("failed-route")]
    [InlineData("late")]
    [InlineData("route-option")]
    public async Task 수행중근거가없거나픽업에늦으면_현재Gps로대체하지않는다(string missing)
    {
        await using var f = await Fixture.CreateAsync();
        var request = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "active-2");
        if (missing == "coordinates") request.하차_위도 = null;
        if (missing == "deadline") request.하차_시간창_종료일시 = null;
        if (missing == "approximate") f.Routes.Actual = false;
        if (missing == "failed-route") f.Routes.Fail = true;
        if (missing == "late") (await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "next")).픽업_시간창_종료일시 = DateTime.UtcNow.AddMinutes(1);
        if (missing == "route-option") f.Routes.ExplicitOptionMismatch = true;
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Policy().다음후보선정Async(f.Queue));
    }

    [Theory]
    [InlineData("off")]
    [InlineData("pause")]
    [InlineData("completed")]
    [InlineData("new-work")]
    public async Task 경로조회대기중수신이나기존업무변경은_후보반환전에차단한다(string change)
    {
        await using var f = await Fixture.CreateAsync();
        f.Routes.BeforeExplicitRoute = async () =>
        {
            await Task.Yield();
            if (change == "off") f.Availability.수신On = false;
            if (change == "pause") f.Availability.자동중지 = true;
            if (change == "completed") (await f.Db.운송원장.SingleAsync(x => x.의뢰Id == "active-1")).상태 = 상태값.배차상태.인수완료;
            if (change == "new-work") f.AddActive("active-3", 38.2m, 3);
            await f.Db.SaveChangesAsync();
        };
        Assert.Null(await f.Policy().다음후보선정Async(f.Queue));
        Assert.Equal(0, f.Queue.추천라운드);
    }

    [Fact]
    public async Task 직접지정추천_자동최고점과다른적격기사도선택할수있다()
    {
        await using var f = await Fixture.CreateAsync(active: false);
        var now = DateTime.UtcNow;
        f.Db.용달기사.Add(new() { 기사Id = "closer", 차량 = "fixture", 상태 = "활동중" });
        await f.Db.SaveChangesAsync();
        f.States.Geo = [State("closer", 38.11m, now), .. f.States.Active];
        var policy = f.Policy();
        Assert.Equal("closer", (await policy.다음후보선정Async(f.Queue))!.DriverId);
        Assert.Equal("driver", (await policy.대상후보조회Async(f.Queue, "driver"))!.DriverId);
    }

    [Fact]
    public async Task 기존일정근거_완료후에는현재판본으로인정하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var service = f.Anchor();
        var basis = await service.조회Async("driver", new(37m, 127m), DateTime.UtcNow);
        Assert.True(basis.확인됨);
        Assert.True(await service.현재근거확인Async("driver", basis));
        (await f.Db.운송원장.SingleAsync(x => x.의뢰Id == "active-2")).상태 = 상태값.배차상태.인수완료;
        await f.Db.SaveChangesAsync();
        Assert.False(await service.현재근거확인Async("driver", basis));
    }

    private static 국내화물운송기사상태Snapshot State(string id, decimal latitude, DateTime now)
        => new(id, 1, 상태값.기사운행상태.운행중, now.AddHours(-1), now, 0m,
            latitude, 127m, 1m, now, now, null, null, 0, null, null, null, AppKey: 기사앱식별자.CargoYongdalDriverApp);

    private sealed class Fixture(SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Routes Routes { get; } = new();
        public States States { get; } = new();
        public 운영배차시험수신UseCase Availability { get; } = new();
        public 운송원장 Queue { get; } = new()
        {
            의뢰Id = "next", 운송번호 = "next", 상태 = 상태값.배차대기상태.대기,
            배차업무유형 = 상태값.배차업무유형.용달운송, 배차큐단계 = 상태값.배차큐단계.배차추천,
            픽업_위도 = 38.11m, 픽업_경도 = 127m, 하차_위도 = 38.12m, 하차_경도 = 127m
        };
        public static async Task<Fixture> CreateAsync(bool active = true)
        {
            var f = new Fixture(new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"cargo-r64-{Guid.NewGuid():N}").Options, new Encryption()));
            f.Db.용달기사.Add(new() { 기사Id = "driver", 차량 = "fixture", 상태 = "활동중" });
            var now = DateTime.UtcNow;
            f.States.Active = [State("driver", active ? 37m : 38.09m, now)];
            f.Db.화주운송의뢰.Add(Request("next", 38.12m));
            f.Db.운송원장.Add(f.Queue);
            if (active) { f.AddActive("active-1", 37.80m, 1); f.AddActive("active-2", 38.10m, 2); }
            await f.Db.SaveChangesAsync();
            return f;
        }
        public void AddActive(string id, decimal dropoff, int order)
        {
            Db.화주운송의뢰.Add(Request(id, dropoff));
            Db.운송원장.Add(new()
            {
                의뢰Id = id, 운송번호 = id, 기사_운송자 = "driver", 확정기사Id = "driver",
                배차업무유형 = 상태값.배차업무유형.용달운송, 배차큐단계 = 상태값.배차큐단계.확정,
                상태 = "운송중", 출발_픽업 = DateTime.UtcNow.AddMinutes(order - 10),
                하차_위도 = dropoff, 하차_경도 = 127m
            });
        }
        private static 화주운송의뢰 Request(string id, decimal dropoff) => new()
        {
            의뢰Id = id, 픽업_위도 = 38.11m, 픽업_경도 = 127m, 하차_위도 = dropoff, 하차_경도 = 127m,
            화물종류 = "상자", 픽업_시간창_시작일시 = DateTime.UtcNow.AddHours(-1), 픽업_시간창_종료일시 = DateTime.UtcNow.AddHours(2),
            하차_시간창_시작일시 = DateTime.UtcNow.AddHours(-1), 하차_시간창_종료일시 = DateTime.UtcNow.AddHours(3)
        };
        public 화물후속픽업기준Service Anchor() => new(Db, new 기사운송일정구성Service(Db), new 운송일정삽입평가Service(Routes));
        public 용달운송배차업무정책 Policy(ISharedDriverWorkAdmissionService? shared = null)
            => new(Db, new Locations(), States, Options.Create(new 배차큐정책Options { 기사후보검색반경Km = 5m }),
                Routes, new Fit(), new 배차추천판정Service(), new Score(), new Rejected(), 운영배차: Availability,
                후속픽업: Anchor(), 일정평가: new 운송일정삽입평가Service(Routes), sharedAdmission: shared,
                신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class States : I국내화물운송기사상태Store
    {
        public IReadOnlyList<국내화물운송기사상태Snapshot> Geo { get; set; } = [];
        public IReadOnlyList<국내화물운송기사상태Snapshot> Active { get; set; } = [];
        public int ActiveReads { get; private set; }
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(decimal lat, decimal lng, decimal radius, int take, CancellationToken ct = default) => Task.FromResult(Geo);
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(int take, CancellationToken ct = default) { ActiveReads++; return Task.FromResult(Active); }
        public Task<국내화물운송기사상태Snapshot?> GetAsync(string id, CancellationToken ct = default) => Task.FromResult(Active.FirstOrDefault(x => x.DriverId == id));
        public Task UpsertAsync(국내화물운송기사상태Snapshot item, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Locations : IDriverLocationStore
    {
        public bool TryGetLatest(string id, out DriverLocationSnapshot item) { item = null!; return false; }
        public void Upsert(DriverLocationSnapshot item) => throw new NotSupportedException();
    }
    private sealed class Routes : I배차추천경로Service
    {
        public bool Actual { get; set; } = true;
        public bool Fail { get; set; }
        public bool ExplicitOptionMismatch { get; set; }
        public int RouteCalls { get; private set; }
        public Func<Task>? BeforeExplicitRoute { get; set; }
        public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b) => Math.Abs(a.Latitude - b.Latitude) * 100m;
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b)
        {
            RouteCalls++;
            return Task.FromResult<배차경로예상결과?>(Fail || a is null || b is null ? null
                : new(CalculateDistanceKm(a, b), TimeSpan.FromMinutes(2), 0m, "fixture", Actual) { RouteOption = "trafast" });
        }
        public async Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b, string option, CancellationToken ct)
        {
            if (BeforeExplicitRoute is { } change) { BeforeExplicitRoute = null; await change(); }
            var route = await EstimateRouteAsync(a, b);
            return route is null ? null : route with { RouteOption = ExplicitOptionMismatch ? "different-option" : option };
        }
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current, 배차추천검색조건? criteria) => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current) => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
    }
    private sealed class Fit : I차량화물적합성Service
    {
        public 차량화물적합성결과 판정(차량제원? vehicle, 화주운송의뢰 request, 화물요구조건? requirement) => new(true, [], []);
    }
    private sealed class Score : I배차추천평가Service
    {
        public 배차추천평가결과 평가(화주운송의뢰? request, 배차추천판정결과 decision, 운송삽입평가결과? schedule,
            decimal? profit, decimal? delay, decimal? distance, decimal? duration, decimal? slack, decimal? detour, bool returnBasis, string? source)
            => new(100m - distance, [], [], "fixture", null);
    }
    private sealed class Rejected : IDriverRejectedRequestStore
    {
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> IsRejectedAsync(string driver, string request, CancellationToken ct = default) => Task.FromResult(false);
        public Task RejectAsync(string driver, string request, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
