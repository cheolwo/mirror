using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;
using Ssalddel.Hubs;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed partial class FoodDispatchWorkflowScenarioFixture
{
    private readonly InMemoryDriverWorkQueueStore _pilotWorkQueue = new();

    // Real food start/status handlers use the same SQL and test index as dispatch.
    // The cargo-only immediate recommendation port must never run in this pilot.
    private void ConfigurePilotServices(IServiceCollection services)
    {
        services.AddSingleton<IDriverWorkQueueStore>(_pilotWorkQueue);
        services.AddSingleton<I국내화물운송기사상태Service>(new 국내화물운송기사상태Service(_states));
        services.AddSingleton<I음식배달권실행공간Store>(_spaces);
        services.AddSingleton<I국내화물배달권실행공간Store>(_cargoSpaces);
        services.AddSingleton<I참여자실행권한검사, 참여자실행권한검사>();
        services.AddSingleton<ICommunityDriverAvailabilityService>(
            new CommunityDriverAvailabilityService(new SystemCommunityDriverAvailabilityClock()));
        services.AddSingleton<I배차추천Service>(new PilotUnusedCargoRecommendations());
    }

    public async Task PreparePilotDriverAsync(string driverId, bool otherDriversReceive = false)
    {
        DriverId = driverId;
        await using var db = CreateContext();
        var driver = await db.용달기사.SingleAsync(x => x.기사Id == driverId);
        driver.운행상태 = 상태값.기사운행상태.대기;
        foreach (var shift in await db.기사근무.Where(x => x.기사Id == driverId).ToArrayAsync())
        {
            shift.ActiveDriverId = null;
            shift.StoppedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        await _states.RemoveAsync(driverId);
        await _spaces.Remove기사Async(driverId);
        foreach (var id in await db.용달기사.Select(x => x.기사Id).ToArrayAsync())
            await Availability(db).기사의사변경Async(id, new()
            {
                클라이언트요청Id = Guid.NewGuid(),
                수신의사Code = id != driverId && otherDriversReceive && id != "scenario-off"
                    ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off
            });
        Actor(AccessToken);
    }

    public async Task PublishPilotPickupLocationAsync()
    {
        // A synthetic, explicitly injected fix replaces phone GPS/permission and the
        // production location endpoint. Work start and dispatch intent stay real HTTP.
        await SetDriverLocationAsync(atPickup: true);
        var key = 음식배달권정책.판정(new(PickupLatitude, PickupLongitude), null).배달권키;
        await _spaces.Upsert기사Async(key, DriverId, 음식배달권정책.인접배달권키조회(key));
    }

    public async Task RecommendPilotOrderAsync()
    {
        await using var db = CreateContext();
        var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == OfferId);
        var result = queue.배차큐단계 == 상태값.배차큐단계.계획배차
            ? await Transition(db).계획배차에서추천으로전환Async(OfferId)
            : await Transition(db).추천대기처리Async(OfferId);
        Assert.True(result.전환여부);
        await db.Entry(queue).ReloadAsync();
        Assert.NotNull(queue.현재추천대상기사Id);
    }

    public async Task CreateNextPilotOrderAsync()
    {
        var previous = OfferId;
        Actor("fixture-orderer");
        using var created = await _client.PostAsJsonAsync("api/v1/food-orders", new 음식주문등록요청
        {
            클라이언트요청Id = Guid.NewGuid(), 음식점Id = RestaurantId,
            수령인정보 = new() { 수령인명 = "합성 주문자", 연락처 = "010-0000-0000", 주소 = "합성 전달 도로", 주문자본인수령여부 = true },
            상품목록 = [new() { 메뉴Id = 49, 상품명 = "합성 다음 메뉴", 수량 = 1, 단가 = 9000m }]
        });
        await RequireSuccessAsync(created);
        OfferId = (await created.Content.ReadFromJsonAsync<음식주문응답>())!.주문번호;
        Assert.NotEqual(previous, OfferId);
        Actor("fixture-restaurant");
        using var accepted = await _client.PostAsJsonAsync($"api/v1/food-orders/{OfferId}/restaurant-acceptance",
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 음식점명 = "합성 시험 식당",
                음식점주소 = "합성 픽업 도로", 음식점위도 = PickupLatitude, 음식점경도 = PickupLongitude, 조리예상분 = 10 });
        await RequireSuccessAsync(accepted);
        await using var db = CreateContext();
        Assert.Equal(1, await DispatchRequests(db).대기항목처리Async());
        Actor(AccessToken);
    }

    public async Task<FoodDispatchWorkflowSnapshot> ReadPilotSnapshotAsync(string? offerId = null)
    {
        var id = offerId ?? OfferId;
        await using var db = CreateContext();
        var order = await db.음식주문.AsNoTracking().SingleAsync(x => x.주문번호 == id);
        var queue = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == id);
        var attempts = await db.음식배달시도.AsNoTracking().Where(x => x.주문번호 == id).OrderBy(x => x.Id).ToArrayAsync();
        var last = attempts.LastOrDefault();
        return new(order.상태, queue.상태, queue.배차큐단계, queue.배차노출상태, queue.추천라운드,
            queue.현재추천대상기사Id, queue.확정기사Id, queue.기사지급예정액, await db.음식주문.CountAsync(),
            await db.운송원장.CountAsync(), await db.음식마트원장동기화Outbox.CountAsync(),
            await db.음식마트원장동기화Outbox.OrderBy(x => x.Id).Select(x => x.처리상태).ToArrayAsync(),
            await db.배차추천알림Outbox.CountAsync(), await db.배차추천알림Outbox.OrderBy(x => x.Id).Select(x => x.발송상태).ToArrayAsync(),
            attempts.Length, last?.Revision, last?.가게도착시각Utc is not null, last?.픽업완료시각Utc is not null,
            last?.전달완료시각Utc is not null, order.상태 == 음식주문상태코드.수령확인);
    }

    public async Task MakePilotNotificationRetryDueAsync()
    {
        await using var db = CreateContext();
        foreach (var item in await db.배차추천알림Outbox.Where(x => x.의뢰Id == OfferId).ToArrayAsync())
        {
            item.마지막시도시각 = DateTime.UtcNow.AddMinutes(-1);
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-1);
        }
        await db.SaveChangesAsync();
    }

    public async Task RestartPilotHttpHostAsync()
    {
        // Same SQLite anchor and in-memory fixture stores survive this HTTP host
        // replacement. It is not a process restart or a Redis recovery claim.
        _client.Dispose();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _app.StopAsync(deadline.Token);
        await _app.DisposeAsync();
        await StartHttpAsync();
        Actor(AccessToken);
    }

    private sealed class PilotUnusedCargoRecommendations : I배차추천Service
    {
        public Task SendToDriverAsync(string id) => throw new InvalidOperationException("음식 운행 시작이 화물 즉시 추천을 호출했습니다.");
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string id) => throw new NotSupportedException();
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string id, 배차추천검색조건? criteria) => throw new NotSupportedException();
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetDrivingRecommendationsAsync(string id, 배차추천검색조건? criteria = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetIdleRecommendationsAsync(string id, 배차추천검색조건? criteria = null) => throw new NotSupportedException();
    }
}
