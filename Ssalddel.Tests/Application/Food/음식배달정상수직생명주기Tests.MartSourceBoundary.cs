using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Food;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Food;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task 앞열두건이소문자마트제안이어도_그뒤정상음식제안은조회된다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        const string driverId = "mart-boundary-driver";
        var now = DateTime.UtcNow;
        var aliases = new[] { 운송의뢰배차원천유형.살뜰마트포장완료주문, 운송의뢰배차원천유형.살뜰마트주문,
            운송의뢰배차원천유형.살뜰마트음식주문 };
        for (var index = 0; index < 12; index++)
        {
            var mart = CreateRecommendedTransport($"mart-before-food-{index}", order.주문번호, driverId);
            mart.원본의뢰유형 = aliases[index % aliases.Length].ToLowerInvariant();
            mart.추천만료시각 = now.AddMinutes(2);
            mart.CreatedAt = now.AddMinutes(-2).AddSeconds(index);
            database.Context.운송원장.Add(mart);
        }
        var food = CreateRecommendedTransport("normal-food-after-twelve-mart", order.주문번호, driverId);
        food.추천만료시각 = now.AddMinutes(5);
        database.Context.운송원장.Add(food);
        await database.Context.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        var offers = await CreateDriverWork(database, store, publisher, driverId).제안조회Async(driverId);

        Assert.Equal(food.의뢰Id, Assert.Single(offers).OfferId);
        Assert.Empty(publisher.Notifications);
        Assert.False(database.Context.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData(운송의뢰배차원천유형.살뜰마트포장완료주문, false)]
    [InlineData(운송의뢰배차원천유형.살뜰마트포장완료주문, true)]
    [InlineData(운송의뢰배차원천유형.살뜰마트주문, false)]
    [InlineData(운송의뢰배차원천유형.살뜰마트음식주문, true)]
    public async Task 같은번호음식주문이있어도_마트제안과수행을음식목록으로노출하지않는다(string sourceType, bool accepted)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        const string driverId = "mart-boundary-driver";
        var queue = CreateRecommendedTransport("mart-source-offer", order.주문번호, driverId);
        queue.원본의뢰유형 = sourceType;
        if (accepted)
        {
            queue.확정기사Id = driverId;
            queue.기사_운송자 = driverId;
            queue.상태 = 상태값.배차대기상태.확정;
            queue.배차큐단계 = 상태값.배차큐단계.확정;
        }
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        var offers = await CreateDriverWork(database, store, publisher, driverId).제안조회Async(driverId);

        Assert.Empty(offers);
        Assert.Empty(publisher.Notifications);
        Assert.Empty(await database.Context.음식배달시도.ToListAsync());
        Assert.False(database.Context.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    [InlineData("pickup")]
    [InlineData("deliver")]
    public async Task 음식명령으로마트원천을변경하지않으며_동일번호음식원장도보존한다(string action)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        const string driverId = "mart-boundary-driver";
        var queue = CreateRecommendedTransport("mart-source-offer", order.주문번호, driverId);
        queue.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문;
        if (action is not ("accept" or "reject"))
        {
            queue.확정기사Id = driverId;
            queue.기사_운송자 = driverId;
            queue.상태 = 상태값.배차대기상태.확정;
            queue.배차큐단계 = 상태값.배차큐단계.확정;
        }
        database.Context.운송원장.Add(queue);
        await database.Context.SaveChangesAsync();
        var beforeOrder = JsonSerializer.Serialize(store.GetOrder(order.주문번호));
        var beforeQueue = JsonSerializer.Serialize(await database.Context.운송원장.AsNoTracking().SingleAsync());
        var publisher = new RecordingPublisher();
        // 잘못된 음식 거절 명령이 재배차 Engine으로 흘러가는 것도 허용하지 않는다.
        var service = new 음식배달기사업무Service(database.Context, new InMemoryDriverLocationStore(),
            new NoOpRouteService(), new MartBoundaryForbiddenTransition(), new InMemory음식배달권실행공간Store(),
            store, new NoOpFoodLedgerOutbox(), new NoOpTransportSync(), new NoOpRestaurantNotification(),
            new NoOpSettlement(), publisher, new TestCurrentUserAccessor(driverId, "Driver"),
            NullLogger<음식배달기사업무Service>.Instance, new TestDispatchAvailability());

        var result = action switch
        {
            "accept" => await service.수락Async(driverId, queue.의뢰Id),
            "reject" => await service.거절Async(driverId, queue.의뢰Id, "DriverChoice"),
            "pickup" => await service.픽업완료Async(driverId, queue.의뢰Id),
            _ => await service.전달완료Async(driverId, queue.의뢰Id)
        };

        Assert.True(result.IsFailed);
        await using var reloaded = database.CreateContext();
        Assert.Equal(beforeOrder, JsonSerializer.Serialize(new EfSsalddelFoodOrderStore(reloaded).GetOrder(order.주문번호)));
        Assert.Equal(beforeQueue, JsonSerializer.Serialize(await reloaded.운송원장.AsNoTracking().SingleAsync()));
        Assert.Empty(await reloaded.음식배달시도.ToListAsync());
        Assert.Empty(await reloaded.음식마트원장동기화Outbox.ToListAsync());
        Assert.Empty(publisher.Notifications);
        Assert.False(database.Context.ChangeTracker.HasChanges());
    }

    private sealed class MartBoundaryForbiddenTransition : I배차대기원장전환Service
    {
        private static Task<배차대기원장전환결과> Forbidden() => throw new InvalidOperationException("마트 원천은 음식 재배차 전환을 호출할 수 없습니다.");
        public Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string id, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 추천대기처리Async(string id, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 추천시작Async(string id, string driver, int? timeoutSeconds = null, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 추천거절처리Async(string id, string driver, string? reason, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 추천만료처리Async(string id, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 공개배차로전환Async(string id, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 실행주체확정결과동기화Async(string id, DispatchConfirmationBoundaryRequest confirmation, CancellationToken ct = default) => Forbidden();
        public Task<배차대기원장전환결과> 배차수락취소처리Async(string id, string driver, string? reason = null, CancellationToken ct = default) => Forbidden();
    }
}
