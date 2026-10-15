using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Pricing;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class StraightLineDeliveryPricingR72Tests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = DateOnly.FromDateTime(Now.UtcDateTime);

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.001", 1)]
    [InlineData("0.0005", 1)]
    [InlineData("0.0004999", 0)]
    [InlineData("1.2345", 1235)]
    [InlineData("1.2344999", 1234)]
    public void 포함거리없이원래거리비만최종원단위반올림한다(string raw, int fee)
        => Assert.Equal(fee, StraightLineDeliveryPricingPolicy.CalculateDistanceFee(decimal.Parse(raw, CultureInfo.InvariantCulture)));

    [Fact]
    public void 직선거리는좌표에서계산하고대칭및동일좌표를보존한다()
    {
        var forward = StraightLineDeliveryPricingPolicy.CalculateDistanceKm(0, 0, 0, .01m);
        Assert.InRange(forward, 1.11195080m, 1.11195081m);
        Assert.Equal(forward, StraightLineDeliveryPricingPolicy.CalculateDistanceKm(0, .01m, 0, 0));
        Assert.Equal(0, StraightLineDeliveryPricingPolicy.CalculateDistanceKm(37.5m, 127m, 37.5m, 127m));
        Assert.InRange(StraightLineDeliveryPricingPolicy.CalculateDistanceKm(0, 179.99m, 0, -179.99m), 2.22390160m, 2.22390161m);
        Assert.InRange(StraightLineDeliveryPricingPolicy.CalculateDistanceKm(0, 0, 0, 180), 20015m, 20016m);
    }

    [Theory]
    [InlineData(91, 0, 0, 0)]
    [InlineData(0, -181, 0, 0)]
    [InlineData(0, 0, -91, 0)]
    [InlineData(0, 0, 0, 181)]
    public void 잘못된좌표를영거리로보정하지않는다(int lat1, int lon1, int lat2, int lon2)
        => Assert.Throws<ArgumentException>(() => StraightLineDeliveryPricingPolicy.CalculateDistanceKm(lat1, lon1, lat2, lon2));

    [Fact]
    public void 음수거리와음수단가는거부한다()
    {
        Assert.Throws<ArgumentException>(() => StraightLineDeliveryPricingPolicy.CalculateDistanceFee(-1));
        Assert.Throws<ArgumentException>(() => StraightLineDeliveryPricingPolicy.CalculateDistanceFee(1, -1));
    }

    [Theory]
    [InlineData("Floor", 1200)]
    [InlineData("Ceiling", 1300)]
    [InlineData("ProportionalAwayFromZero", 1235)]
    public void 기존거리단위반올림과새비례산식을명시적으로구분한다(string rounding, int expected)
    {
        var policy = new FoodDeliveryPayoutPricingPolicy("review.r1", Day, Day, 0, 0, 100, 100, rounding);
        var review = 음식배달지급검토Policy.건별계산(new(policy, [new("delivery", Day, 1.2345m,
            rounding == "ProportionalAwayFromZero" ? "StraightLine" : "RouteEstimate", "None", 0, 0, 0)]));
        Assert.Equal(expected, Assert.Single(review.Lines).DistanceFeeKrw);
    }

    [Fact]
    public void 거리비외기본액과최소보전의기존소수금액은보존한다()
    {
        var policy = new FoodDeliveryPayoutPricingPolicy("review.r1", Day, Day, 1000.25m, 1000.5m, 1000, 1000,
            "ProportionalAwayFromZero", 0, 4000.75m);
        var line = 음식배달지급검토Policy.구성계산(policy, new("delivery", Day, 1.2345m, "StraightLine", "None", 0, 0, 0), policy.PickupFeeKrw);
        Assert.Equal(1235m, line.DistanceFeeKrw);
        Assert.Equal(4000.75m, line.GrossPayoutKrw);
        Assert.Equal(765m, line.MinimumAdjustmentKrw);
    }

    [Fact]
    public async Task 새정책은주행거리대신원래직선거리로계산하고ETA를따로보존한다()
    {
        await using var db = Db(); var routes = new Routes(); var weather = new Weather(rain: true);
        db.음식운영정책.Add(new() { 기사기본지급액 = 2000, 기사픽업지급액 = 700, 기사최소지급액 = 3900,
            포함거리Meters = 1000, 기사거리단위지급액 = 90, 기사기상할증액 = 1000,
            기사한시수요할증액 = 300, 기사한시수요할증시작일시Utc = Now.UtcDateTime.AddMinutes(-1),
            기사한시수요할증종료일시Utc = Now.UtcDateTime.AddMinutes(1) });
        await db.SaveChangesAsync();
        var bands = new FoodDriverRoutePricingOptions { TimeBands = [new() { Code = "Day", StartMinuteKst = 0,
            EndMinuteKst = 1439, CarSurchargeKrw = 700, MotorcycleSurchargeKrw = 800 }] };
        var service = Service(db, routes, weather, true, bands);
        var result = await service.산정Async(Queue());
        Assert.InRange(result.산정거리Km!.Value, 1.11195080m, 1.11195081m);
        Assert.Equal(10m, result.경로거리Km); Assert.Equal(600d, result.경로소요시간초);
        Assert.Equal("StraightLine", result.거리근거Code);
        Assert.Equal(1112m, result.요금.기본요금구성!.DistanceFeeKrw);
        Assert.Equal(5900m, result.요금.기사지급예정액);
        Assert.Equal(700m, result.요금.시간대할증액); Assert.Equal(1000m, result.요금.기상할증액);
        Assert.Equal(300m, result.요금.한시수요할증액);
        Assert.NotNull(result.Pricing); Assert.Equal(0, result.Pricing.IncludedDistanceMeters);
        Assert.Equal(1000m, result.Pricing.RatePerKmKrw); Assert.Equal(1112m, result.Pricing.DistanceFeeKrw);
        Assert.Equal(2000m, result.Pricing.BaseFeeKrw); Assert.Equal(3900m, result.Pricing.MinimumPayoutKrw);
        Assert.Equal(2000m, result.Pricing.SurchargeKrw); Assert.Equal(5900m, result.Pricing.TotalPayoutKrw);
        Assert.Equal(result.산정거리Km * 1000m, result.Pricing.RawDistanceMeters);
        Assert.Contains("eta-route:naver-directions5-vehicle", result.정책판본);
        Assert.Equal(1, routes.Calls);
    }

    [Fact]
    public async Task 기본OFF는기존경로산식과판본을유지한다()
    {
        await using var db = Db(); var result = await Service(db, new Routes(), new Weather(), false).산정Async(Queue());
        Assert.Equal(10m, result.산정거리Km); Assert.Equal(10600m, result.요금.기사지급예정액);
        Assert.Equal("NaverDirections5CarRouteEstimate", result.거리근거Code);
        Assert.Null(result.Pricing); Assert.DoesNotContain("straight-line", result.정책판본);
    }

    [Fact]
    public async Task 선택한합의요금은새직선정책보다우선하고기존경로거리를사용한다()
    {
        await using var db = Db(); db.배달기사.Add(new() { 기사Id = "food-driver", 차량 = "자동차" }); await db.SaveChangesAsync();
        var agreed = new Agreed();
        var result = await Service(db, new Routes(), new Weather(), true, agreement: agreed).산정Async(Queue(), "food-driver");
        Assert.Same(agreed.Snapshot, result.합의요금표);
        Assert.Same(agreed.Snapshot.FoodPricing, result.요금.기본계산정책);
        Assert.Equal(10m, result.산정거리Km); Assert.Equal(5000m, result.요금.기본요금구성!.DistanceFeeKrw);
        Assert.Equal(7000m, result.요금.기사지급예정액); Assert.Null(result.Pricing);
        Assert.Contains("agreement:agreement", result.정책판본); Assert.DoesNotContain("straight-line", result.정책판본);
    }

    [Fact]
    public async Task 직선요금계산이가능해도실제경로부족을우회하지않는다()
    {
        await using var db = Db(); var weather = new Weather(); var routes = new Routes(actual: false);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(db, routes, weather, true).산정Async(Queue()));
        Assert.Equal("FoodPricingRouteUnavailable", error.Message); Assert.Equal(0, weather.Calls);
    }

    [Fact]
    public async Task 좌표누락은영거리로채우지않는다()
    {
        await using var db = Db(); var routes = new Routes(); var queue = Queue(); queue.하차_위도 = null;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(db, routes, new Weather(), true).산정Async(queue));
        Assert.Equal("FoodPricingCoordinatesRequired", error.Message); Assert.Equal(0, routes.Calls);
    }

    [Fact]
    public async Task 산정이기존원장의동결금액과합의근거를직접수정하지않는다()
    {
        await using var db = Db(); var queue = Queue(); queue.기사지급예정액 = 7777; queue.기사제안요금계산근거Json = "existing";
        await Service(db, new Routes(), new Weather(), true).산정Async(queue);
        Assert.Equal(7777m, queue.기사지급예정액); Assert.Equal("existing", queue.기사제안요금계산근거Json);
    }

    private static 음식배달기사제안요금Service Service(SsalddelContext db, Routes routes, Weather weather, bool enabled,
        FoodDriverRoutePricingOptions? bands = null, IAgreedRateTableUseCase? agreement = null)
        => new(db, routes, weather, new Clock(), new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions
        { Mode = SsalddelExecutionMode.Simulation })), Options.Create(bands ?? new()), agreement,
            Options.Create(new StraightLineDriverPricingOptions { Enabled = enabled }));
    private static 운송원장 Queue() => new() { 의뢰Id = "offer", 원본의뢰Id = "food-order", 배차업무유형 = 상태값.배차업무유형.음식배달,
        픽업_위도 = 0, 픽업_경도 = 0, 하차_위도 = 0, 하차_경도 = .01m };
    private static SsalddelContext Db() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("straight-line-r72-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? v) => v; public string? Unprotect(string? v) => v; }
    private sealed class Weather(bool rain = false) : I픽업지기상관측Client
    {
        public int Calls { get; private set; }
        public Task<픽업지기상관측결과> 조회Async(decimal? latitude, decimal? longitude, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new 픽업지기상관측결과(true, rain, 픽업지기상자료상태Code.Available,
                rain ? "1" : "0", "0", Now.UtcDateTime, 픽업지기상관측결과.공식자료출처, new string('a', 64)));
        }
    }
    private sealed class Routes(bool actual = true) : I배차추천경로Service
    {
        public int Calls { get; private set; }
        private Task<배차경로예상결과?> Route(string? option = null)
        { Calls++; return Task.FromResult<배차경로예상결과?>(new(10, TimeSpan.FromMinutes(10), 0, "Directions5", actual) { RouteOption = option }); }
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b) => Route();
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b, string option, CancellationToken ct) => Route(option);
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? d, DriverLocationSnapshot? s, 배차추천검색조건? c) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? d, DriverLocationSnapshot? s) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? a, IReadOnlyList<배차경로좌표> b, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? a, 배차경로좌표? b, 배차경로좌표? c, 배차경로좌표? d) => throw new NotSupportedException();
        public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b) => throw new InvalidOperationException("The pricing policy owns unrounded straight-line distance.");
    }
    private sealed class Agreed : IAgreedRateTableUseCase
    {
        public AgreedRateTableSnapshotDto Snapshot { get; } = new("agreement", 7, AgreedRateBusinessKinds.FoodDelivery, "Car",
            new(AgreedRatePartyKinds.Restaurant, "1"), new(AgreedRatePartyKinds.FoodDriver, "food-driver"),
            new("agreement.r7", Day, Day, 700, 1300, 1000, 500, "Ceiling"), null,
            Now.UtcDateTime.AddDays(-1), Now.UtcDateTime.AddDays(1), Now.UtcDateTime.AddDays(-1), "fixture-digest");
        public Task<AgreedRateTableSnapshotDto?> SelectedAsync(string kind, string work, string driver, string vehicle, CancellationToken ct)
            => Task.FromResult<AgreedRateTableSnapshotDto?>(Snapshot);
        public Task<string?> SelectedDriverAsync(string kind, string work, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AgreedRateTableDto>> ListAsync(AgreedRatePartyDto actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableDto> GetAsync(string id, AgreedRatePartyDto actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableDto> ProposeAsync(AgreedRatePartyDto actor, AgreedRateTableProposalRequest r, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableDto> ActAsync(string id, AgreedRatePartyDto actor, AgreedRateTableActionRequest r, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTablePreviewDto> PreviewAsync(string id, AgreedRatePartyDto actor, AgreedRateTablePreviewRequest r, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableSelectionDto> SelectAsync(string id, AgreedRatePartyDto actor, AgreedRateTableSelectionRequest r, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableSelectionDto> GetSelectionAsync(string id, string work, AgreedRatePartyDto actor, CancellationToken ct) => throw new NotSupportedException();
    }
}
