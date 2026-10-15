using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Tests.Driver;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task 기존추천이남아도새로운업무선택거부후에는알림을재생성하지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var queue = CreateRecommendedTransport("shared-existing-offer", "order-test", "driver-test");
        database.Context.운송원장.Add(queue); await database.Context.SaveChangesAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(database.Context);
        var notification = new PricingNotification();
        var transition = SharedAdmissionPricingTransition(database.Context, notification, admission);

        var result = await transition.추천시작Async(queue.의뢰Id, "driver-test");
        Assert.False(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.추천준비안됨, result.결과코드);
        Assert.Equal(0, notification.Count);
        Assert.Equal("driver-test", queue.현재추천대상기사Id);
        Assert.Equal(1, queue.추천라운드);
    }

    [Fact]
    public async Task 요금계산중업무선택이바뀌면새추천과요금동결과알림을확정하지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var queue = CreateRecommendedTransport("shared-pricing-changed", "order-test", "driver-test");
        queue.현재추천대상기사Id = null; queue.추천라운드 = 0;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        database.Context.운송원장.Add(queue);
        database.Context.Set<살뜰.도메인.기사.배달기사>().Add(new() { 기사Id = "driver-test", 차량 = "자동차" });
        await database.Context.SaveChangesAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(database.Context) { Deny = false };
        var notification = new PricingNotification();
        var pricing = new ChangeParticipationDuringPricing(SharedAdapterPricingService(database.Context), admission);
        var transition = SharedAdmissionPricingTransition(database.Context, notification, admission, pricing);

        var result = await transition.추천시작Async(queue.의뢰Id, "driver-test");
        Assert.False(result.전환여부);
        Assert.Equal(0, notification.Count);
        Assert.Null(queue.현재추천대상기사Id); Assert.Null(queue.기사지급예정액);
        Assert.Null(queue.기사제안요금계산근거Json); Assert.Equal(0, queue.추천라운드);
        // 이후의 정상 Save가 보류된 제안 요금을 우발적으로 저장하지 않습니다.
        await database.Context.SaveChangesAsync(); database.Context.ChangeTracker.Clear();
        var saved = await database.Context.운송원장.SingleAsync();
        Assert.Null(saved.기사지급예정액); Assert.Null(saved.현재추천대상기사Id);
    }

    [Fact]
    public async Task 공통기능OFF면미등록업무설정으로기존추천과알림을막지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var queue = CreateRecommendedTransport("shared-disabled", "order-test", "driver-test");
        queue.현재추천대상기사Id = null; queue.추천라운드 = 0;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        database.Context.운송원장.Add(queue);
        database.Context.Set<살뜰.도메인.기사.배달기사>().Add(new() { 기사Id = "driver-test", 차량 = "자동차" });
        await database.Context.SaveChangesAsync();
        var admission = new SharedDriverWorkAdmissionService(database.Context,
            Options.Create(new SharedDriverWorkOptions { Enabled = false }));
        var notification = new PricingNotification();
        var result = await SharedAdmissionPricingTransition(database.Context, notification, admission)
            .추천시작Async(queue.의뢰Id, "driver-test");
        Assert.True(result.전환여부);
        Assert.Equal(1, notification.Count);
        Assert.Equal("driver-test", queue.현재추천대상기사Id);
    }

    private static 음식배달기사제안요금Service SharedAdapterPricingService(SsalddelContext db)
        => new(db, new NoOpRouteService(), new PricingWeather(), TimeProvider.System,
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })));

    private static 배차대기원장전환Service SharedAdmissionPricingTransition(SsalddelContext db,
        PricingNotification notification, ISharedDriverWorkAdmissionService admission, I음식배달기사제안요금Service? pricing = null)
        => new(db, Options.Create(new 배차큐정책Options()), null!, notification,
            new PricingDriverState(), new 음식배달배차흐름Resolver(), pricing ?? SharedAdapterPricingService(db),
            new PricingFoodRecommendationRecord(), sharedAdmission: admission);

    private sealed class ChangeParticipationDuringPricing(I음식배달기사제안요금Service pricing,
        SharedDriverAdapterAdmissionProbe admission) : I음식배달기사제안요금Service
    {
        public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken ct = default)
            => throw new NotSupportedException();
        public async Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, string driverId, CancellationToken ct = default)
        {
            var result = await pricing.산정Async(queue, driverId, ct);
            admission.Deny = true;
            return result;
        }
    }
}
