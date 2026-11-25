using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Opinet;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

/// <summary>
/// 합성 서울권→구리권→남양주권 좌표의 서버 계산 회귀다. 지역명은 시험 구분이며
/// 지오코딩·실제 NAVER 경로·GPS·운영 추천·수락·휴대폰 검증을 의미하지 않는다.
/// </summary>
public sealed class CargoCrossRegionReturnR23Tests
{
    private static readonly 배차경로좌표 Home = new(37.58m, 127.09m);
    private static readonly 배차경로좌표 Guri = new(37.60m, 127.14m);
    private static readonly 배차경로좌표 Namyangju = new(37.65m, 127.21m);

    [Fact]
    public async Task 같은서비스에서왕복이동해도현위치추천이이어지고_복귀활성만전환되며기준점은고정된다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.AddAsync("home-area", new(Home.Latitude + 0.001m, Home.Longitude + 0.001m), Home,
            pickupAddress: "합성 home-area 고정 픽업");
        await f.AddAsync("guri-area", new(Guri.Latitude + 0.001m, Guri.Longitude + 0.001m), Home,
            pickupAddress: "합성 guri-area 고정 픽업");
        await f.AddAsync("namyangju-area", new(Namyangju.Latitude + 0.001m, Namyangju.Longitude + 0.001m), Home,
            pickupAddress: "합성 namyangju-area 고정 픽업");
        var service = f.Service();
        var anchor = (f.Settings.Current.설정.기준위도, f.Settings.Current.설정.기준경도);
        var revision = f.Settings.Current.설정.Revision;
        var itinerary = new (배차경로좌표 Point, string ExpectedId, bool ReturnActive)[]
        {
            (Home, "home-area", false),
            (Guri, "guri-area", false),
            (Namyangju, "namyangju-area", true),
            (Guri, "guri-area", false),
            (Home, "home-area", false)
        };
        var observedStates = new List<bool>();

        // 같은 메모리 저장소와 같은 제품 서비스 인스턴스의 상태 전환을 검사한다.
        // 후보 주소와 좌표는 고정하고 현재 위치만 이동한다. 귀환 시 같은 지역 후보를
        // 다시 조회하는 것이며 수락·운송 완료·새 추천 발행을 검증하는 흐름은 아니다.
        // 인증 HTTP 세션이나 실제 GPS/주행을 재현하는 시험은 아니다.
        foreach (var step in itinerary)
        {
            f.Move(step.Point);
            var pickup = new 배차경로좌표(step.Point.Latitude + 0.001m, step.Point.Longitude + 0.001m);

            var result = Assert.Single(await service.GetDrivingRecommendationsAsync("driver",
                new(step.Point.Latitude, step.Point.Longitude, 1m)));

            Assert.Equal(step.ExpectedId, result.의뢰Id);
            Assert.Equal($"합성 {step.ExpectedId} 고정 픽업", result.픽업지);
            Assert.Equal(pickup.Latitude, result.픽업_위도);
            Assert.Equal(pickup.Longitude, result.픽업_경도);
            Assert.True(result.직선거리Km is >= 0m and < 1m);
            Assert.True(result.차량적합여부);
            Assert.True(result.일정삽입가능여부);
            Assert.Equal(step.ReturnActive, result.권역복귀활성여부);
            Assert.Equal(step.ReturnActive, f.Settings.Current.활성여부);
            Assert.Equal(anchor.Item1, f.Settings.Current.설정.기준위도);
            Assert.Equal(anchor.Item2, f.Settings.Current.설정.기준경도);
            Assert.Equal(revision, f.Settings.Current.설정.Revision);
            observedStates.Add(f.Settings.Current.활성여부);
        }

        Assert.Equal(new[] { false, false, true, false, false }, observedStates);
    }

    [Theory]
    [InlineData("서울권", 37.58, 127.09, false)]
    [InlineData("구리권", 37.60, 127.14, false)]
    [InlineData("남양주권", 37.65, 127.21, true)]
    public async Task 이동후현위치주변의다른지역명후보도조회하며_복귀기준점은이동하지않는다(
        string region, double latitude, double longitude, bool returnActive)
    {
        await using var f = await Fixture.CreateAsync();
        var current = new 배차경로좌표((decimal)latitude, (decimal)longitude);
        f.Move(current);
        var near = new 배차경로좌표(current.Latitude + 0.001m, current.Longitude + 0.001m);
        await f.AddAsync("near", near, Home, pickupAddress: $"합성 {region} 경계를 넘은 다른 지역 픽업");
        await f.AddAsync("far", new(37.90m, 127.60m), Home);

        var result = Assert.Single(await f.Service().GetDrivingRecommendationsAsync("driver", new(current.Latitude, current.Longitude, 1m)));

        Assert.Equal("near", result.의뢰Id);
        Assert.True(result.직선거리Km is >= 0m and < 1m);
        Assert.True(result.차량적합여부);
        Assert.True(result.일정삽입가능여부);
        Assert.Equal(returnActive, result.권역복귀활성여부);
        Assert.Equal(Home.Latitude, f.Settings.Current.설정.기준위도);
        Assert.Equal(Home.Longitude, f.Settings.Current.설정.기준경도);
        Assert.Equal(1, f.Settings.Current.설정.Revision);
    }

    [Fact]
    public async Task 귀가방향의전체일정끝점은실제정책가점으로우선하고_더멀어지는후보는가점이없다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Move(Namyangju);
        var pickup = new 배차경로좌표(37.651m, 127.211m);
        await f.AddAsync("away", pickup, new(37.70m, 127.30m));
        await f.AddAsync("toward-home", pickup, Guri);

        var results = await f.Service().GetDrivingRecommendationsAsync("driver");
        var toward = Assert.Single(results, x => x.의뢰Id == "toward-home");
        var away = Assert.Single(results, x => x.의뢰Id == "away");

        Assert.Equal("toward-home", results[0].의뢰Id);
        Assert.True(toward.권역복귀확인됨);
        Assert.Equal(20m, toward.권역복귀가점);
        Assert.True(toward.전체일정후보복귀거리Km < toward.전체일정기존복귀거리Km);
        Assert.True(away.권역복귀확인됨);
        Assert.Equal(0m, away.권역복귀가점);
        Assert.True(away.전체일정후보복귀거리Km > away.전체일정기존복귀거리Km);
        Assert.Contains("권역 복귀 방향", toward.배지);
        Assert.DoesNotContain("권역 복귀 방향", away.배지);
        Assert.All(f.Roads.RoadCalls, x => Assert.Equal("SyntheticRoadProvider", x.Provider));
    }

    [Fact]
    public async Task 오래된GPS의대기후보조회는남아도_복귀가점과확인배지를인증하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Move(Namyangju, age: TimeSpan.FromMinutes(6));
        await f.AddAsync("toward-home", Namyangju, Guri);

        var result = Assert.Single(await f.Service().GetDrivingRecommendationsAsync("driver"));

        Assert.True(result.권역복귀정책선택됨);
        Assert.False(result.권역복귀활성여부);
        Assert.False(result.권역복귀확인됨);
        Assert.Equal(0m, result.권역복귀가점);
        Assert.Null(result.전체일정후보복귀거리Km);
        Assert.DoesNotContain("권역 복귀 방향", result.배지);
    }

    [Fact]
    public async Task 도로응답이근사fallback이면_가까워지는끝점도복귀확인이나가점으로승격하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Move(Namyangju);
        f.Roads.VerifiedSyntheticRoad = false;
        await f.AddAsync("toward-home", Namyangju, Guri);

        var result = Assert.Single(await f.Service().GetDrivingRecommendationsAsync("driver"));

        Assert.True(result.권역복귀활성여부);
        Assert.False(result.권역복귀확인됨);
        Assert.Equal(0m, result.권역복귀가점);
        Assert.Null(result.전체일정후보복귀거리Km);
    }

    [Fact]
    public async Task 늦은픽업은대기조회에일정불가로남으며_귀가가점으로살리지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Move(Namyangju);
        await f.AddAsync("late", Guri, Home, pickupEnd: DateTime.UtcNow.AddMinutes(-1));

        var result = Assert.Single(await f.Service().GetDrivingRecommendationsAsync("driver"));

        Assert.False(result.일정삽입가능여부);
        Assert.False(result.전체일정완수가능여부);
        Assert.Equal(0m, result.권역복귀가점);
        Assert.False(result.권역복귀확인됨);
        Assert.NotEmpty(result.일정위반사유);
    }

    [Fact]
    public async Task 복귀가점이있어도_다른기사전용과거절과현위치검색반경밖후보를되살리지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Move(Namyangju);
        await f.AddAsync("visible", Namyangju, Home);
        await f.AddAsync("other-driver", Namyangju, Home, owner: "other");
        await f.AddAsync("rejected", Namyangju, Home);
        await f.AddAsync("outside-search", Guri, Home);
        f.Rejected.Ids.Add("rejected");

        var result = Assert.Single(await f.Service().GetDrivingRecommendationsAsync("driver", new(Namyangju.Latitude, Namyangju.Longitude, 1m)));

        Assert.Equal("visible", result.의뢰Id);
        Assert.Equal(20m, result.권역복귀가점);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(SsalddelContext db)
        {
            Db = db;
            ReturnArea = new(Settings, Roads, Options.Create(new 기사권역복귀정책Options()), TimeProvider.System);
        }

        public SsalddelContext Db { get; }
        public SyntheticRoadProvider Roads { get; } = new();
        public SettingsStore Settings { get; } = new();
        public LocationStore Location { get; } = new();
        public RejectedStore Rejected { get; } = new();
        private 기사권역복귀Service ReturnArea { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"cargo-cross-region-r23-{Guid.NewGuid():N}").Options, new PlainEncryption());
            db.용달기사.Add(new()
            {
                기사Id = "driver", 차량 = "r23-vehicle", 상태 = "활동중", 운행상태 = 상태값.기사운행상태.운행중,
                기본복귀지주소 = "합성 비공개 기준점", 기본복귀지위도 = Home.Latitude, 기본복귀지경도 = Home.Longitude
            });
            db.차량제원.Add(new()
            {
                차량코드 = "r23-vehicle", 차량명 = "합성 1t", 최대적재중량Kg = 1000,
                적재함길이Mm = 2000, 적재함폭Mm = 2000, 적재함높이Mm = 2000, 권장최대CBM = 8m, 팔레트적재개수 = 2
            });
            await db.SaveChangesAsync();
            var fixture = new Fixture(db);
            fixture.Move(Home);
            return fixture;
        }

        public void Move(배차경로좌표 current, TimeSpan? age = null)
        {
            var now = DateTime.UtcNow;
            var recorded = now - (age ?? TimeSpan.Zero);
            Location.Current = new("driver", current.Latitude, current.Longitude, 5m,
                상태값.기사운행상태.운행중, recorded, recorded);
        }

        public async Task AddAsync(string id, 배차경로좌표 pickup, 배차경로좌표 dropoff,
            string owner = "driver", string pickupAddress = "합성 지역 픽업", DateTime? pickupEnd = null)
        {
            var now = DateTime.UtcNow;
            var request = new 화주운송의뢰
            {
                의뢰Id = id, 화물종류 = "상자", 운송방식 = "혼적", 화물온도조건 = "상온", 최종운임 = 20_000m,
                결제상태 = 상태값.결제상태.결제완료, 정산상태 = "결제완료",
                화물중량Kg = 100m, 화물수량 = 1, 화물길이Mm = 500, 화물폭Mm = 500, 화물높이Mm = 500,
                픽업_도로명주소 = pickupAddress, 픽업_위도 = pickup.Latitude, 픽업_경도 = pickup.Longitude,
                하차_도로명주소 = "합성 지역 하차", 하차_위도 = dropoff.Latitude, 하차_경도 = dropoff.Longitude,
                픽업_시간창_시작일시 = now.AddHours(-1), 픽업_시간창_종료일시 = pickupEnd ?? now.AddHours(12),
                하차_시간창_시작일시 = now.AddHours(-1), 하차_시간창_종료일시 = now.AddHours(12)
            };
            Db.화주운송의뢰.Add(request);
            Db.운송원장.Add(new()
            {
                의뢰Id = id, 운송번호 = id, 배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
                배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
                현재추천대상기사Id = owner, 추천시작시각 = now, 추천만료시각 = now.AddHours(1),
                픽업_도로명주소 = request.픽업_도로명주소, 픽업_위도 = pickup.Latitude, 픽업_경도 = pickup.Longitude,
                하차_도로명주소 = request.하차_도로명주소, 하차_위도 = dropoff.Latitude, 하차_경도 = dropoff.Longitude
            });
            await Db.SaveChangesAsync();
        }

        public 화물배차추천Service Service() => new(Db, Location, Rejected, null!, null!, null!, Roads,
            new 배차추천판정Service(), new 배차추천평가Service(), new 차량화물적합성Service(new 차량적재추천Engine()),
            new 기사운송일정구성Service(Db), new 운송일정삽입평가Service(Roads), new Fuel(), null!, null!, ReturnArea,
            운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용,
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class SyntheticRoadProvider : I배차추천경로Service
    {
        // 명시적 모의 도로 응답. 실제 도로 또는 NAVER 응답으로 기록하지 않는다.
        public bool VerifiedSyntheticRoad { get; set; } = true;
        public List<(배차경로좌표 From, 배차경로좌표 To, string Provider)> RoadCalls { get; } = [];
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria)
            => Task.FromResult(Point(location));
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location)
            => Task.FromResult(Point(location));
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => EstimateRouteAsync(origin, destination, "traoptimal", CancellationToken.None);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string routeOption, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (origin is null || destination is null) return Task.FromResult<배차경로예상결과?>(null);
            RoadCalls.Add((origin, destination, "SyntheticRoadProvider"));
            var distance = CalculateDistanceKm(origin, destination)!.Value * 1.2m;
            return Task.FromResult<배차경로예상결과?>(new(distance, TimeSpan.FromMinutes((double)distance * 2d), 0m,
                VerifiedSyntheticRoad ? "SyntheticRoadProvider" : "SyntheticApproximationFallback", VerifiedSyntheticRoad) { RouteOption = routeOption });
        }
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("이 회귀는 단독 후보 일정만 검증합니다.");
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
        {
            const double radians = Math.PI / 180d;
            var lat1 = (double)source.Latitude * radians;
            var lat2 = (double)target.Latitude * radians;
            var dLat = lat2 - lat1;
            var dLon = (double)(target.Longitude - source.Longitude) * radians;
            var a = Math.Pow(Math.Sin(dLat / 2d), 2d) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2d), 2d);
            return (decimal)(6371.0088d * 2d * Math.Atan2(Math.Sqrt(Math.Clamp(a, 0d, 1d)), Math.Sqrt(1d - Math.Clamp(a, 0d, 1d))));
        }
        private static 배차경로좌표? Point(DriverLocationSnapshot? location)
            => location is null ? null : new(location.Latitude, location.Longitude);
    }

    private sealed class SettingsStore : I기사권역복귀설정Store
    {
        public 기사권역복귀설정Snapshot Current { get; private set; } = new(new 기사권역복귀설정
        { Revision = 1, Mode = "automatic", 기준위치명 = "합성 고정 기준점", 기준위도 = Home.Latitude, 기준경도 = Home.Longitude });
        public Task<기사권역복귀설정Snapshot> 조회Async(string driverId, CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task<기사권역복귀설정Snapshot> 저장Async(string driverId, 기사권역복귀설정 settings, long expectedRevision, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> 활성상태갱신Async(string driverId, long revision, bool active, DateTime recorded, CancellationToken cancellationToken = default)
        {
            if (revision != Current.설정.Revision || Current.위치판정시각Utc > recorded) return Task.FromResult(false);
            Current = Current with { 활성여부 = active, 위치판정시각Utc = recorded };
            return Task.FromResult(true);
        }
    }
    private sealed class LocationStore : IDriverLocationStore
    {
        public DriverLocationSnapshot Current { get; set; } = null!;
        public void Upsert(DriverLocationSnapshot snapshot) => Current = snapshot;
        public bool TryGetLatest(string id, out DriverLocationSnapshot snapshot) { snapshot = Current; return id == Current.DriverId; }
    }
    private sealed class RejectedStore : IDriverRejectedRequestStore
    {
        public HashSet<string> Ids { get; } = [];
        public Task RejectAsync(string id, string request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsRejectedAsync(string id, string request, CancellationToken cancellationToken = default) => Task.FromResult(Ids.Contains(request));
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(Ids);
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
    private sealed class Fuel : IOpinetAveragePriceService
    {
        public Task<IReadOnlyList<OpinetAveragePriceItem>> GetAveragePricesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OpinetAveragePriceItem>>([]);
    }
    private sealed class PlainEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
