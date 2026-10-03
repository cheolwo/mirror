using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Naver;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 음식배달기사제안요금Tests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 3, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("0.999", "2500")]
    [InlineData("1", "2500")]
    [InlineData("1.001", "2590")]
    [InlineData("1.1", "2590")]
    [InlineData("1.101", "2680")]
    public void 실제배차_거리계산은_기존요율과올림경계를유지한다(string km, string expected)
    {
        var value = 음식배달기사제안요금Policy.판정(new 음식운영정책(),
            decimal.Parse(km, System.Globalization.CultureInfo.InvariantCulture), false, false, Now, false);
        Assert.Equal(decimal.Parse(expected), value.기사지급예정액);
        Assert.Equal("LegacyUnsplit", value.기본지급구분Code);
        Assert.Equal(value.기본거리지급액, value.기본요금구성!.GrossPayoutKrw);
        Assert.Equal("RouteEstimate", value.기본요금구성.DistanceBasisCode);
    }

    [Fact]
    public void 명시한픽업배분과최소보정은_기본총액을바꾸지않고_할증은그뒤에더한다()
    {
        var policy = new 음식운영정책 { 기사기본지급액 = 1400m, 기사픽업지급액 = 700m,
            기사최소지급액 = 2500m };
        var value = 음식배달기사제안요금Policy.판정(policy, 0m, true, true, Now, false);
        Assert.Equal("PickupDropoffSplit", value.기본지급구분Code);
        Assert.Equal(700m, value.기본요금구성!.PickupFeeKrw);
        Assert.Equal(700m, value.기본요금구성.DropoffFeeKrw);
        Assert.Equal(1100m, value.기본요금구성.MinimumAdjustmentKrw);
        Assert.Equal(3500m, value.기사지급예정액);
    }

    [Fact]
    public void 기존정책의소수지급액은_검토입력의정수제한과분리해보존한다()
    {
        var value = 음식배달기사제안요금Policy.판정(new 음식운영정책
            { 기사기본지급액 = 2500.50m, 기사거리단위지급액 = 90.25m }, 1.01m, false, false, Now, false);
        Assert.Equal(2590.75m, value.기사지급예정액);
    }

    [Fact]
    public void 거리미확인이나기본액을넘는픽업배분은_배차금액으로확정하지않는다()
    {
        Assert.Throws<ArgumentException>(() => 음식배달기사제안요금Policy.판정(
            new 음식운영정책(), null, false, false, Now, false));
        Assert.Throws<ArgumentException>(() => 음식배달기사제안요금Policy.판정(
            new 음식운영정책 { 기사픽업지급액 = 3000m }, 1m, false, false, Now, false));
    }

    [Fact]
    public void 유효한_강수근거가_있을_때만_건당_고정할증을_더한다()
    {
        var policy = new 음식운영정책
        {
            포함거리Meters = 1000,
            거리단위Meters = 100,
            기사기본지급액 = 2500m,
            기사거리단위지급액 = 90m,
            기사최소지급액 = 2500m,
            기사기상할증액 = 1000m,
            기사기상할증활성화여부 = true
        };

        var rainy = 음식배달기사제안요금Policy.판정(policy, 1.15m, true, true, Now, true);
        var unavailable = 음식배달기사제안요금Policy.판정(policy, 1.15m, false, true, Now, true);

        Assert.Equal(2680m, rainy.기본거리지급액);
        Assert.Equal(1000m, rainy.기상할증액);
        Assert.Equal(3680m, rainy.기사지급예정액);
        Assert.True(rainy.기상할증적용여부);
        Assert.Equal(2680m, unavailable.기사지급예정액);
        Assert.False(unavailable.기상할증적용여부);
    }

    [Fact]
    public void 한시수요할증은_시뮬레이션의_유효시간안에서만_지급예정액에더한다()
    {
        var policy = new 음식운영정책
        {
            기사기본지급액 = 2500m,
            기사최소지급액 = 2500m,
            기사한시수요할증액 = 1000m,
            기사한시수요할증시작일시Utc = Now.UtcDateTime.AddMinutes(-1),
            기사한시수요할증종료일시Utc = Now.UtcDateTime.AddMinutes(29)
        };

        var simulation = 음식배달기사제안요금Policy.판정(policy, 0.5m, false, false, Now, true);
        var operational = 음식배달기사제안요금Policy.판정(policy, 0.5m, false, false, Now, false);
        var expired = 음식배달기사제안요금Policy.판정(policy, 0.5m, false, false, Now.AddMinutes(30), true);

        Assert.Equal(1000m, simulation.한시수요할증액);
        Assert.Equal(3500m, simulation.기사지급예정액);
        Assert.True(simulation.한시수요할증적용여부);
        Assert.Equal(2500m, operational.기사지급예정액);
        Assert.False(operational.한시수요할증적용여부);
        Assert.Equal(2500m, expired.기사지급예정액);
        Assert.False(expired.한시수요할증적용여부);
    }

    [Fact]
    public async Task 픽업지_좌표를_기상청_격자로_변환해_강수실황과_원본Hash를_보존한다()
    {
        const string payload = """
            {"response":{"header":{"resultCode":"00"},"body":{"items":{"item":[
              {"baseDate":"20260911","baseTime":"1100","category":"PTY","obsrValue":"1","nx":60,"ny":127},
              {"baseDate":"20260911","baseTime":"1100","category":"RN1","obsrValue":"2.5","nx":60,"ny":127}
            ]}}}}
            """;
        var handler = new RecordingHandler(payload);
        var client = new 기상청초단기실황Client(
            new HttpClient(handler) { BaseAddress = new Uri("https://apis.data.go.kr") },
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new PublicDataOptions { DataGoKrServiceKey = "secret-key" }),
            new FixedTimeProvider(Now),
            NullLogger<기상청초단기실황Client>.Instance);

        var result = await client.조회Async(37.5665m, 126.9780m);

        Assert.True(result.유효한강수근거있음);
        Assert.True(result.강수중);
        Assert.Equal("1", result.강수형태Code);
        Assert.Equal("2.5", result.한시간강수량표기);
        Assert.Equal(64, result.원본Hash!.Length);
        Assert.Contains("getUltraSrtNcst", handler.RequestUri!.AbsoluteUri);
        Assert.Contains("nx=60", handler.RequestUri.Query);
        Assert.Contains("ny=127", handler.RequestUri.Query);
    }

    [Fact]
    public async Task 같은_격자와_관측시각의_동시_배차는_공공데이터를_한번만_조회한다()
    {
        const string payload = """
            {"response":{"header":{"resultCode":"00"},"body":{"items":{"item":[
              {"baseDate":"20260911","baseTime":"1100","category":"PTY","obsrValue":"0","nx":60,"ny":127}
            ]}}}}
            """;
        var handler = new RecordingHandler(payload, TimeSpan.FromMilliseconds(50));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new 기상청초단기실황Client(
            new HttpClient(handler) { BaseAddress = new Uri("https://apis.data.go.kr") },
            cache,
            Options.Create(new PublicDataOptions { DataGoKrServiceKey = "secret-key" }),
            new FixedTimeProvider(Now),
            NullLogger<기상청초단기실황Client>.Instance);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => client.조회Async(37.5665m, 126.9780m)));

        Assert.All(results, result => Assert.Equal(픽업지기상자료상태Code.Available, result.자료상태Code));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task 서비스키가_없으면_외부호출이나_가짜강수판정을_하지_않는다()
    {
        var handler = new RecordingHandler("");
        var client = new 기상청초단기실황Client(
            new HttpClient(handler) { BaseAddress = new Uri("https://apis.data.go.kr") },
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new PublicDataOptions()),
            new FixedTimeProvider(Now),
            NullLogger<기상청초단기실황Client>.Instance);

        var result = await client.조회Async(37.5665m, 126.9780m);

        Assert.False(result.유효한강수근거있음);
        Assert.Equal(픽업지기상자료상태Code.MissingServiceKey, result.자료상태Code);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task 서버요금산정은_운영정책과_픽업지기상을_같은_판본으로_반환한다()
    {
        await using var db = CreateContext();
        db.음식운영정책.Add(new 음식운영정책
        {
            Id = 1,
            기사기상할증액 = 1000m,
            기사기상할증정책판본 = "weather-test.r1",
            UpdatedAtUtc = Now.UtcDateTime
        });
        await db.SaveChangesAsync();
        var service = new 음식배달기사제안요금Service(
            db,
            new FixedDistanceRouteService(1.15m),
            new RainyWeatherClient(),
            new FixedTimeProvider(Now),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions
            {
                Mode = SsalddelExecutionMode.Simulation
            })));

        var result = await service.산정Async(new 운송원장
        {
            배차업무유형 = 상태값.배차업무유형.음식배달,
            픽업_위도 = 37.5m,
            픽업_경도 = 127m,
            하차_위도 = 37.51m,
            하차_경도 = 127.01m
        });

        Assert.Equal(3680m, result.요금.기사지급예정액);
        Assert.Equal(1.15m, result.산정거리Km);
        Assert.Equal("NaverDirections5CarRouteEstimate", result.거리근거Code);
        Assert.Equal("Car", result.경로차량Code);
        Assert.Contains("weather-test.r1", result.정책판본);
        Assert.Equal(Now.UtcDateTime, result.판정시각Utc);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, -1)]
    [InlineData(true, 1001)]
    public async Task 미확인근사거리와_무효경로로는_요금을_확정하지않는다(bool actual, int distance)
    {
        await using var db = CreateContext();
        var service = new 음식배달기사제안요금Service(db, new FixedDistanceRouteService(distance, actual),
            new RainyWeatherClient(), new FixedTimeProvider(Now),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.산정Async(new 운송원장
            { 배차업무유형 = 상태값.배차업무유형.음식배달, 픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.51m, 하차_경도 = 127.01m }));
        Assert.Equal("FoodPricingRouteUnavailable", error.Message);
    }

    [Fact]
    public async Task 네이버_HTTP_미터응답이_배차요금엔진과_저장근거까지_이어진다()
    {
        await using var db = CreateContext();
        var handler = new RecordingHandler("""
            {"code":0,"route":{"trafast":[{"summary":{"distance":2345,"duration":120000}}]}}
            """);
        var options = Options.Create(new NaverCloudDirectionsOptions { ApiKeyId = "test-id", ApiKey = "test-key" });
        var naver = new NaverCloudDirectionsService(new HttpClient(handler)
            { BaseAddress = new Uri("https://maps.apigw.ntruss.com") }, options);
        var routeService = new 배차추천경로Service(db, null!, null!, naver, options,
            NullLogger<배차추천경로Service>.Instance);
        var service = new 음식배달기사제안요금Service(db, routeService, new RainyWeatherClient(),
            new FixedTimeProvider(Now), new SsalddelExecutionModePolicy(
                Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })));
        var result = await service.산정Async(new 운송원장
            { 배차업무유형 = 상태값.배차업무유형.음식배달, 픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.51m, 하차_경도 = 127.01m });
        Assert.Equal(2.345m, result.산정거리Km);
        Assert.Equal(3760m, result.요금.기본거리지급액);
        Assert.Equal("NaverDirections5CarRouteEstimate", result.거리근거Code);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("NaverDirections5CarRouteEstimate", json);
        Assert.Equal(1, handler.RequestCount);
    }


    [Fact]
    public async Task 같은주소도_기사차량별경로와_한국시간할증으로_다르게계산하고_캐시를분리한다()
    {
        await using var db = CreateContext();
        db.Set<살뜰.도메인.기사.배달기사>().AddRange(
            new() { 기사Id = "car", 차량 = "자동차" }, new() { 기사Id = "moto", 차량 = "오토바이" });
        await db.SaveChangesAsync();
        var handler = new RecordingHandler("""
            {"code":0,"route":{"tracomfort":[{"summary":{"distance":1500,"duration":120000}}],
            "traavoidcaronly":[{"summary":{"distance":2500,"duration":240000}}]}}
            """);
        var opts = Options.Create(new NaverCloudDirectionsOptions { ApiKeyId = "test-id", ApiKey = "test-key" });
        var naver = new NaverCloudDirectionsService(new HttpClient(handler) { BaseAddress = new Uri("https://maps.apigw.ntruss.com") }, opts);
        var routes = new 배차추천경로Service(db, null!, null!, naver, opts, NullLogger<배차추천경로Service>.Instance);
        var pricing = new 음식배달기사제안요금Service(db, routes, new RainyWeatherClient(), new FixedTimeProvider(Now),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Options.Create(new FoodDriverRoutePricingOptions { TimeBands = [new() { Code = "lunch",
                StartMinuteKst = 720, EndMinuteKst = 780, CarRouteOption = "tracomfort", CarSurchargeKrw = 700, MotorcycleSurchargeKrw = 1000 }] }));
        var queue = new 운송원장 { 배차업무유형 = 상태값.배차업무유형.음식배달, 픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.51m, 하차_경도 = 127.01m };
        var car = await pricing.산정Async(queue, "car");
        var moto = await pricing.산정Async(queue, "moto");
        Assert.Equal("tracomfort", car.경로옵션Code);
        Assert.Equal("traavoidcaronly", moto.경로옵션Code);
        Assert.Equal(1.5m, car.산정거리Km);
        Assert.Equal(2.5m, moto.산정거리Km);
        Assert.Equal(4650m, car.요금.기사지급예정액);
        Assert.Equal(5850m, moto.요금.기사지급예정액);
        Assert.Equal(240d, moto.경로소요시간초);
        Assert.Equal("NaverDirections5CarOnlyAvoidanceEstimate", moto.거리근거Code);
        Assert.Contains("option=traavoidcaronly", handler.RequestUri!.Query);
        await pricing.산정Async(queue, "car");
        Assert.Equal(2, handler.RequestCount);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => pricing.산정Async(queue, "missing"));
        Assert.Equal("FoodPricingDriverVehicleRequired", error.Message);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public void 한국시간_경계와자정구간_미설정및중첩할증을구분한다()
    {
        var opts = new FoodDriverRoutePricingOptions { TimeBands = [new() { Code = "night",
            StartMinuteKst = 1380, EndMinuteKst = 60, MotorcycleSurchargeKrw = 800 }] };
        Assert.Equal("night", opts.ResolveBand(DateTimeOffset.Parse("2026-10-03T14:00:00Z"))!.Code);
        Assert.Equal("night", opts.ResolveBand(DateTimeOffset.Parse("2026-10-03T15:59:59Z"))!.Code);
        Assert.Null(opts.ResolveBand(DateTimeOffset.Parse("2026-10-03T16:00:00Z")));
        opts.TimeBands.Add(new() { Code = "overlap", StartMinuteKst = 0, EndMinuteKst = 120 });
        Assert.Throws<ArgumentException>(() => opts.ResolveBand(DateTimeOffset.Parse("2026-10-03T15:30:00Z")));
        Assert.Null(new FoodDriverRoutePricingOptions().ResolveBand(Now));
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 화물원장은_음식요금서비스의_두진입점에서_차단한다(bool driverSpecific)
    {
        await using var db = CreateContext();
        var service = new 음식배달기사제안요금Service(db, new FixedDistanceRouteService(10m),
            new RainyWeatherClient(), new FixedTimeProvider(Now),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })));
        var cargo = new 운송원장 { 배차업무유형 = 상태값.배차업무유형.용달운송, 기사지급예정액 = 27000m };
        var error = await Assert.ThrowsAsync<ArgumentException>(() => driverSpecific
            ? service.산정Async(cargo, "not-a-food-driver") : service.산정Async(cargo));
        Assert.Equal("FoodPricingTaskTypeMismatch", error.Message);
        Assert.Equal(27000m, cargo.기사지급예정액);
    }

    [Fact]
    public void 동일거리라도_음식과화물은_독립된요금항목으로계산한다()
    {
        var food = 음식배달기사제안요금Policy.판정(new 음식운영정책
            { 기사기본지급액 = 2000m, 기사픽업지급액 = 1000m, 포함거리Meters = 0,
              거리단위Meters = 100, 기사거리단위지급액 = 100m, 기사최소지급액 = 0 },
            10m, false, false, Now, false, 시간대할증액: 1000m);
        var cargo = Ssalddel.Application.Shipper.Request.화주운송기준운임계산기.Calculate(
            new Ssalddel.Contracts.Shipper.Request.화주운송기준운임견적요청
                { 차량종류 = "다마스", 대기료 = 3000m, 수작업비 = 2000m, 할증 = 500m },
            new("다마스", 15000m, 1200m, 15000m, "test"), 10m);
        Assert.Equal(13000m, food.기사지급예정액);
        Assert.Equal(1000m, food.시간대할증액);
        Assert.Equal(32500m, cargo.최종운임);
        Assert.Equal(12000m, cargo.거리운임);
        Assert.Throws<ArgumentException>(() => 음식배달기사제안요금Policy.판정(
            new 음식운영정책(), 10m, false, false, Now, false, 시간대할증액: -1m));
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"food-weather-pricing-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class RainyWeatherClient : I픽업지기상관측Client
    {
        public Task<픽업지기상관측결과> 조회Async(
            decimal? latitude,
            decimal? longitude,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new 픽업지기상관측결과(
                true,
                true,
                픽업지기상자료상태Code.Available,
                "1",
                "1.0",
                Now.UtcDateTime,
                픽업지기상관측결과.공식자료출처,
                new string('a', 64)));
    }

    private sealed class FixedDistanceRouteService(decimal distance, bool actual = true) : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 살뜰.도메인.기사.용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 살뜰.도메인.기사.용달기사? driver, DriverLocationSnapshot? currentLocation) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => Task.FromResult<배차경로예상결과?>(new(distance, TimeSpan.FromMinutes(5), 0m, actual ? "Directions5" : "좌표기반도로보정", actual));
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => throw new InvalidOperationException("StraightLineMustNotPriceOffer");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingHandler(string payload, TimeSpan? delay = null) : HttpMessageHandler
    {
        private int _requestCount;

        public Uri? RequestUri { get; private set; }

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            RequestUri = request.RequestUri;
            if (delay.HasValue)
            {
                await Task.Delay(delay.Value, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
