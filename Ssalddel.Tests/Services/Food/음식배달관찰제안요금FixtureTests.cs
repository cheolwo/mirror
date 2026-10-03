using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Development.FoodObserver;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Food;

public sealed class 음식배달관찰제안요금FixtureTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 표본거리도_운영요율과시간대정책으로계산하며_관측미확정과합성출처를보존한다()
    {
        await using var db = CreateContext();
        await AddDriverAsync(db);
        db.음식운영정책.Add(new 음식운영정책
        {
            기사기본지급액 = 1800m, 기사픽업지급액 = 800m, 기사최소지급액 = 0m,
            포함거리Meters = 0, 거리단위Meters = 100, 기사거리단위지급액 = 200m,
            기사기상할증액 = 5000m, 기사기상할증활성화여부 = true,
            UpdatedAtUtc = Now.UtcDateTime
        });
        await db.SaveChangesAsync();
        var result = await Service(db, timeOptions: new FoodDriverRoutePricingOptions
        {
            TimeBands = [new FoodDriverTimeBand
            {
                Code = "Lunch", StartMinuteKst = 660, EndMinuteKst = 780,
                MotorcycleSurchargeKrw = 700m, CarSurchargeKrw = 900m
            }]
        }).산정Async(Queue(), 검증표본기사제안요금Service.NearDriverId);

        Assert.Equal(3100m, result.요금.기사지급예정액); // 기본 1800 + 3×200 + 오토바이 시간대 700
        Assert.Equal(800m, result.요금.기본요금구성!.PickupFeeKrw);
        Assert.Equal(1000m, result.요금.기본요금구성.DropoffFeeKrw);
        Assert.Equal(0m, result.요금.기상할증액);
        Assert.False(result.기상.유효한강수근거있음);
        Assert.Equal("FoodObserverNotObserved", result.기상.자료상태Code);
        Assert.Null(result.기상.관측기준시각Utc);
        Assert.Equal("Motorcycle", result.경로차량Code);
        Assert.Null(result.경로소요시간초);
        Assert.Equal(0.250m, result.산정거리Km);
        Assert.Equal(검증표본기사제안요금Service.DistanceSourceCode, result.거리근거Code);
        Assert.Equal(result.거리근거Code, result.요금.기본요금구성.DistanceBasisCode);
        Assert.Equal(검증표본기사제안요금Service.FixtureId, result.경로옵션Code);
        Assert.True(result.정책판본.Length <= 180);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(검증표본기사제안요금Service.DistanceSourceCode,
            json.RootElement.GetProperty("거리근거Code").GetString());
        Assert.DoesNotContain("naver", result.정책판본, StringComparison.OrdinalIgnoreCase);

        var fixture = 검증표본기사제안요금Service.ConfiguredFixture;
        Assert.True(fixture.IsEstimated);
        Assert.False(fixture.ActualMapRequest);
        Assert.Equal("Simulation", fixture.ExecutionModeCode);
        Assert.Equal(검증표본좌표Service.RestaurantAddress, fixture.PickupAddress);
        Assert.Equal(검증표본좌표Service.CustomerAddress, fixture.DropoffAddress);
    }

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation)]
    [InlineData(true, SsalddelExecutionMode.Operational)]
    public async Task 옵트인없는호스트와운영모드에는_표본요금을적용하지않는다(bool enabled, SsalddelExecutionMode mode)
    {
        await using var db = CreateContext();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, enabled, mode)
            .산정Async(Queue(), 검증표본기사제안요금Service.NearDriverId));
        Assert.Equal("FoodObserverPricingRequiresSimulation", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 표본밖의좌표와좌표누락은_실패로남고표본으로보충하지않는다(bool missing)
    {
        await using var db = CreateContext();
        var queue = Queue();
        queue.하차_경도 = missing ? null : 127.0871m;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(db)
            .산정Async(queue, 검증표본기사제안요금Service.NearDriverId));
        Assert.Equal("FoodObserverRouteFixtureNotFound", error.Message);
    }

    [Fact]
    public async Task 일반기사는_좌표가같아도_검증표본에포함하지않는다()
    {
        await using var db = CreateContext();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(db).산정Async(Queue(), "ordinary-driver"));
        Assert.Equal("FoodObserverPricingDriverOutsideFixture", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("검증 오토바이")]
    [InlineData("자동차")]
    public async Task 정상음식배달오토바이프로필이_없으면_요금을산정하지않는다(string? vehicle)
    {
        await using var db = CreateContext();
        if (vehicle is not null) await AddDriverAsync(db, vehicle);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(db)
            .산정Async(Queue(), 검증표본기사제안요금Service.NearDriverId));
        Assert.Equal("FoodPricingDriverVehicleRequired", error.Message);
    }

    [Fact]
    public async Task 기사없는호출과화물원장에는_음식배달표본을적용하지않는다()
    {
        await using var db = CreateContext();
        Assert.Equal("FoodObserverPricingDriverRequired", (await Assert.ThrowsAsync<ArgumentException>(() => Service(db)
            .산정Async(Queue()))).Message);
        var cargo = new 운송원장();
        Assert.Equal("FoodPricingTaskTypeMismatch", (await Assert.ThrowsAsync<ArgumentException>(() => Service(db)
            .산정Async(cargo, 검증표본기사제안요금Service.NearDriverId))).Message);
    }

    private static async Task AddDriverAsync(SsalddelContext db, string vehicle = "오토바이")
    {
        db.Set<배달기사>().Add(new 배달기사
        {
            기사Id = 검증표본기사제안요금Service.NearDriverId, 기사명 = "합성 기사", 차량 = vehicle, 상태 = "활동중"
        });
        await db.SaveChangesAsync();
    }

    private static 운송원장 Queue() => new()
    {
        의뢰Id = "observer-fixture-order", 배차업무유형 = 상태값.배차업무유형.음식배달,
        픽업_위도 = 37.5880m, 픽업_경도 = 127.0850m,
        하차_위도 = 37.5880m, 하차_경도 = 127.0870m
    };

    private static 검증표본기사제안요금Service Service(SsalddelContext db, bool enabled = true,
        SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation, FoodDriverRoutePricingOptions? timeOptions = null)
        => new(db, new 음식배달관찰검증Options { Enabled = enabled },
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })),
            new FixedTimeProvider(), Options.Create(timeOptions ?? new FoodDriverRoutePricingOptions()));

    private static SsalddelContext CreateContext() => new(
        new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase("food-observer-price-" + Guid.NewGuid()).Options,
        new DummyPersonalDataEncryptionService());

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
