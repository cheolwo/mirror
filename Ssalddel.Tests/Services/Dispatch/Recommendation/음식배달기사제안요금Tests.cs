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
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 음식배달기사제안요금Tests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 3, 30, 0, TimeSpan.Zero);

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

        var rainy = 음식배달기사제안요금Policy.판정(policy, 1.15m, true, true);
        var unavailable = 음식배달기사제안요금Policy.판정(policy, 1.15m, false, true);

        Assert.Equal(2680m, rainy.기본거리지급액);
        Assert.Equal(1000m, rainy.기상할증액);
        Assert.Equal(3680m, rainy.기사지급예정액);
        Assert.True(rainy.기상할증적용여부);
        Assert.Equal(2680m, unavailable.기사지급예정액);
        Assert.False(unavailable.기상할증적용여부);
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
            new FixedTimeProvider(Now));

        var result = await service.산정Async(new 운송원장
        {
            픽업_위도 = 37.5m,
            픽업_경도 = 127m,
            하차_위도 = 37.51m,
            하차_경도 = 127.01m
        });

        Assert.Equal(3680m, result.요금.기사지급예정액);
        Assert.Contains("weather-test.r1", result.정책판본);
        Assert.Equal(Now.UtcDateTime, result.판정시각Utc);
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

    private sealed class FixedDistanceRouteService(decimal distance) : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 살뜰.도메인.기사.용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 살뜰.도메인.기사.용달기사? driver, DriverLocationSnapshot? currentLocation) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => distance;
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
