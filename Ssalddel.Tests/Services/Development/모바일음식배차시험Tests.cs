using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Development.MobileFieldTest;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Development;

public sealed class 모바일음식배차시험Tests
{
    [Fact]
    public void 기본설정은_시험호스트와adapter를등록하지않는다()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        var before = builder.Services.Count;
        모바일음식배차시험Hosting.Add모바일음식배차시험(builder);
        Assert.Equal(before, builder.Services.Count);
        Assert.DoesNotContain(builder.Services, x => x.ImplementationType == typeof(모바일음식배차시험작업자));
    }

    [Theory]
    [InlineData("SsalddelExecution:Mode", "Operational")]
    [InlineData("DOTNET_RUNNING_IN_CONTAINER", "false")]
    [InlineData("ConnectionStrings:DefaultConnection", "server=mysql;database=ssalddel;")]
    [InlineData("ConnectionStrings:DefaultConnection", "server=production-db;database=ssalddel_food_mobile_test;")]
    [InlineData("MongoDb:Database", "ssalddel")]
    [InlineData("MongoDb:ConnectionString", "mongodb://production-db:27017")]
    [InlineData("MongoDb:ConnectionString", "mongodb://mongo:27017,other:27017")]
    [InlineData("MobileFieldTest:Enabled", "false")]
    [InlineData("IdentitySeed:DevelopmentAccounts:Enabled", "false")]
    [InlineData("IdentitySeed:DevelopmentAccounts:AllowInSimulation", "false")]
    [InlineData("IdentitySeed:BootstrapAdmin:Enabled", "true")]
    [InlineData("DatabaseInitialization:RunAtStartup", "true")]
    [InlineData("FoodObserverVerification:Enabled", "true")]
    [InlineData("VersionFeatureFlags:FoodDeliveryWorkflow", "false")]
    public void 격리호스트조건중하나라도빠지면_활성화를차단한다(string key, string value)
    {
        var options = new 모바일음식배차시험Options { Enabled = true };
        var guard = 모바일음식배차시험TestHost.CreateGuard(options,
            모바일음식배차시험TestHost.Configuration(new() { [key] = value }));
        Assert.Throws<InvalidOperationException>(guard.EnsureEnabled);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Testing")]
    public void Staging외환경에서는_시험adapter를사용하지않는다(string environment)
        => Assert.Throws<InvalidOperationException>(모바일음식배차시험TestHost.CreateGuard(
            new() { Enabled = true }, environment: environment).EnsureEnabled);

    [Fact]
    public void 명시활성화호스트는_다른HostedService를삭제하지않는다()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.Configuration.AddConfiguration(모바일음식배차시험TestHost.Configuration());
        builder.Services.AddSingleton<IHostedService, MarkerHostedService>();
        모바일음식배차시험Hosting.Add모바일음식배차시험(builder);
        Assert.Contains(builder.Services, x => x.ImplementationType == typeof(MarkerHostedService));
        Assert.Contains(builder.Services, x => x.ImplementationType == typeof(모바일음식배차시험작업자));
        Assert.Contains(builder.Services, x => x.ServiceType == typeof(IKakao좌표변환Service)
            && x.ImplementationType == typeof(모바일음식배차시험좌표Service));
    }

    [Fact]
    public async Task 시험주소만해석하고_일반주소와미등록좌표는실패한다()
    {
        var service = new 모바일음식배차시험좌표Service(모바일음식배차시험TestHost.CreateGuard(new() { Enabled = true }));
        var address = await service.주소정보조회Async(모바일음식배차시험자료.DropoffAddress);
        Assert.Equal(37.582000m, address!.위도);
        await Assert.ThrowsAsync<ArgumentException>(() => service.주소정보조회Async("실제 고객 주소"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.좌표지역정보조회Async(37m, 127m));
        var route = 모바일음식배차시험자료.ConfiguredRoute;
        Assert.True(route.IsEstimated); Assert.False(route.ActualMapRequest);
    }

    [Fact]
    public async Task 합성기사와기존메뉴를멱등준비하며_GPS관측행을만들지않는다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        await host.PrepareAsync();
        await using var db = host.CreateContext();
        Assert.Equal(3, await db.음식점공개프로필.CountAsync());
        Assert.Equal(9, await db.음식점메뉴.CountAsync());
        Assert.Single(await db.기사근무.ToListAsync());
        Assert.Single(await db.Set<살뜰.도메인.기사.배달기사>().ToListAsync());
        Assert.Equal(1, host.ReceivingState.IntentChanges);
        Assert.Empty(await db.기사위치기록.ToListAsync());
        var state = await host.States.GetAsync(모바일음식배차시험TestHost.DriverId);
        Assert.NotNull(state!.위치수신시각Utc);
        Assert.Equal("FoodDeliveryDriverApp", state.AppKey);
        Assert.Null(state.AccuracyM);
        Assert.Contains((await host.Scopes.SnapshotAsync()).SelectMany(x => x.운행중기사Ids),
            id => id == 모바일음식배차시험TestHost.DriverId);
    }

    [Fact]
    public async Task 명시수신Off와운행종료를_준비나반복갱신이다시켜지않는다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        var before = await host.States.GetAsync(모바일음식배차시험TestHost.DriverId);
        host.Options.RefreshSyntheticDriverLocation = true;
        host.ReceivingState.Current.수신의사Code = "Off";
        await host.PrepareAsync(); await host.RefreshAsync();
        Assert.Equal("Off", host.ReceivingState.Current.수신의사Code);
        Assert.Equal(1, host.ReceivingState.IntentChanges);
        Assert.Equal(before!.위치수신시각Utc, (await host.States.GetAsync(모바일음식배차시험TestHost.DriverId))!.위치수신시각Utc);
        await using (var db = host.CreateContext())
        {
            (await db.용달기사.SingleAsync()).운행상태 = 상태값.기사운행상태.대기;
            await db.SaveChangesAsync();
        }
        host.ReceivingState.Current.수신의사Code = "On";
        await host.PrepareAsync(); await host.RefreshAsync();
        await using var verify = host.CreateContext();
        Assert.Equal(상태값.기사운행상태.대기, (await verify.용달기사.SingleAsync()).운행상태);
        Assert.Equal(before.위치수신시각Utc, (await host.States.GetAsync(모바일음식배차시험TestHost.DriverId))!.위치수신시각Utc);
    }

    [Fact]
    public async Task 앱에서보낸다른좌표와정확도를_합성대기점으로덮어쓰지않는다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        host.Options.RefreshSyntheticDriverLocation = true;
        host.Locations.TryGetLatest(모바일음식배차시험TestHost.DriverId, out var initial);
        var provided = initial with { Latitude = 37.581m, AccuracyM = 5m };
        host.Locations.Upsert(provided);
        await host.RefreshAsync();
        host.Locations.TryGetLatest(모바일음식배차시험TestHost.DriverId, out var after);
        Assert.Equal(provided, after);
    }

    [Fact]
    public async Task 요금은동일운영정책과시간대로계산하고_시험근거와기상미확인을보존한다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        var queue = 모바일음식배차시험TestHost.Queue(); await host.SeedAsync(queue);
        await using var db = host.CreateContext();
        db.음식운영정책.Add(new 음식운영정책
        {
            기사기본지급액 = 1800m, 기사픽업지급액 = 800m, 기사최소지급액 = 0m,
            포함거리Meters = 0, 거리단위Meters = 100, 기사거리단위지급액 = 200m,
            기사기상할증액 = 5000m
        });
        await db.SaveChangesAsync();
        var service = new 모바일음식배차시험요금Service(db, host.Guard, host.Fixtures, new NoonClock(),
            Options.Create(new FoodDriverRoutePricingOptions { TimeBands = [new()
            { Code = "Lunch", StartMinuteKst = 660, EndMinuteKst = 780, MotorcycleSurchargeKrw = 700m }] }));
        var result = await service.산정Async(queue, 모바일음식배차시험TestHost.DriverId);
        Assert.Equal(3300m, result.요금.기사지급예정액);
        Assert.Equal(800m, result.요금.기본요금구성!.PickupFeeKrw);
        Assert.Equal(0.350m, result.산정거리Km);
        Assert.Equal(모바일음식배차시험자료.DistanceSourceCode, result.거리근거Code);
        Assert.Equal(result.거리근거Code, result.요금.기본요금구성.DistanceBasisCode);
        Assert.False(result.기상.유효한강수근거있음); Assert.Null(result.기상.관측기준시각Utc);
        Assert.Null(result.경로소요시간초);
        Assert.Contains("IsEstimated=true|ActualMapRequest=false", result.정책판본);
        Assert.True(result.정책판본.Length <= 180);
        Assert.DoesNotContain("Directions5", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("driver")]
    [InlineData("orderer")]
    [InlineData("address")]
    [InlineData("coordinate")]
    [InlineData("cargo")]
    [InlineData("cancelled")]
    [InlineData("automatic-fulfillment")]
    [InlineData("order-latitude-only")]
    [InlineData("order-longitude-only")]
    [InlineData("order-wrong-latitude")]
    [InlineData("order-wrong-longitude")]
    public async Task 비시험기사와비시험주문은_시험요금으로산정하지않는다(string mutation)
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        var queue = 모바일음식배차시험TestHost.Queue(); await host.SeedAsync(queue);
        await using var db = host.CreateContext();
        var driver = 모바일음식배차시험TestHost.DriverId;
        var order = await db.음식주문.SingleAsync();
        switch (mutation)
        {
            case "driver": driver = "ordinary-driver"; break;
            case "orderer": order.주문자UserId = "ordinary-orderer"; break;
            case "address": queue.하차_도로명주소 = "실제 고객 주소"; break;
            case "coordinate": queue.하차_위도 = null; break;
            case "cargo": queue.배차업무유형 = 상태값.배차업무유형.용달운송; break;
            case "cancelled": order.상태 = 음식주문상태코드.취소; break;
            case "automatic-fulfillment": order.모의처리처수행 = true; break;
            case "order-latitude-only": order.음식점경도 = null; break;
            case "order-longitude-only": order.음식점위도 = null; break;
            case "order-wrong-latitude": order.음식점위도 = 37.581450m; break;
            case "order-wrong-longitude": order.음식점경도 = 127.088950m; break;
        }
        await db.SaveChangesAsync();
        var service = new 모바일음식배차시험요금Service(db, host.Guard, host.Fixtures, TimeProvider.System,
            Options.Create(new FoodDriverRoutePricingOptions()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.산정Async(queue, driver));
    }

    private sealed class MarkerHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class NoonClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 3, 0, 0, TimeSpan.Zero);
    }
}
