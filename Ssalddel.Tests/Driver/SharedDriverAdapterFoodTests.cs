using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Services.Food;
using Ssalddel.Tests.Driver;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 음식단건과묶음수락은공통거부시원장을변경하지않는다(bool bundle)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        const string driverId = "shared-food-driver";
        var ids = new List<string>();
        for (var index = 0; index < (bundle ? 2 : 1); index++)
        {
            var order = store.AddOrder(CreateOrderRequest());
            var id = "shared-food:" + order.주문번호;
            ids.Add(id);
            database.Context.운송원장.Add(CreateRecommendedTransport(id, order.주문번호, driverId));
        }
        await database.Context.SaveChangesAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(database.Context) { RequireTransaction = true };
        var service = SharedAdmissionFoodService(database, store, driverId, admission);
        var result = bundle ? await service.묶음수락Async(driverId, ids) : await service.수락Async(driverId, ids.Single());
        Assert.True(result.IsFailed);
        Assert.Equal("SharedDriverWorkProbeDenied", result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(ids, admission.CandidateIds);
        Assert.All(await database.Context.운송원장.AsNoTracking().ToListAsync(), x => Assert.Null(x.확정기사Id));
        Assert.Empty(await database.Context.음식배달시도.ToListAsync());
        Assert.Equal("Exit", admission.Calls.Last());
    }

    [Fact]
    public async Task 음식신규참여를꺼도기존수락업무는조회한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new Ssalddel.Contracts.Food.음식점주문수락요청
            { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        const string driverId = "shared-active-food-driver";
        var id = "shared-active-food:" + order.주문번호;
        database.Context.운송원장.Add(CreateRecommendedTransport(id, order.주문번호, driverId));
        await database.Context.SaveChangesAsync();
        store.배차대기반영(order.주문번호, database.Context.운송원장.Single().Id, DateTime.UtcNow);
        var admission = new SharedDriverAdapterAdmissionProbe(database.Context) { Deny = false, RequireTransaction = true };
        var service = SharedAdmissionFoodService(database, store, driverId, admission);
        Assert.Single(await service.제안조회Async(driverId));
        Assert.True((await service.수락Async(driverId, id)).IsSuccess);
        admission.Deny = true;
        admission.Calls.Clear();
        var active = Assert.Single(await service.제안조회Async(driverId));
        Assert.NotEqual(Ssalddel.Contracts.Common.Drivers.DriverWorkOfferStatus.Recommended, active.Status);
        Assert.DoesNotContain(admission.Calls, x => x.StartsWith("Recommendation:", StringComparison.Ordinal));
        var attempt = await database.Context.음식배달시도.SingleAsync();
        Assert.True((await service.가게도착Async(driverId, id, new()
            { 클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = attempt.Revision })).IsSuccess);
    }

    [Fact]
    public async Task 음식추천후다른화물이확정되면전체약속재검사로미수락배너만숨긴다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        const string driverId = "shared-food-then-cargo-driver";
        var offerId = "shared-food-then-cargo:" + order.주문번호;
        database.Context.운송원장.Add(CreateRecommendedTransport(offerId, order.주문번호, driverId));
        await database.Context.SaveChangesAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(database.Context)
        {
            Deny = false,
            AllowsRecommendation = _ => !database.Context.운송원장.Any(x =>
                x.배차업무유형 == 살뜰.도메인.공통.상태값.배차업무유형.용달운송
                && x.확정기사Id == driverId && x.배차큐단계 == 살뜰.도메인.공통.상태값.배차큐단계.확정)
        };
        var service = SharedAdmissionFoodService(database, store, driverId, admission);
        Assert.Single(await service.제안조회Async(driverId));
        database.Context.운송원장.Add(new 살뜰.도메인.운송.운송원장
        {
            의뢰Id = "shared-new-cargo", 확정기사Id = driverId, 기사_운송자 = driverId,
            상태 = 살뜰.도메인.공통.상태값.배차대기상태.확정,
            배차큐단계 = 살뜰.도메인.공통.상태값.배차큐단계.확정
        });
        await database.Context.SaveChangesAsync();
        admission.Calls.Clear();
        Assert.Empty(await service.제안조회Async(driverId));
        Assert.Contains("Recommendation:" + offerId, admission.Calls);
        Assert.Null((await database.Context.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == offerId)).확정기사Id);
        Assert.Empty(await database.Context.음식배달시도.ToListAsync());
    }

    private static 음식배달기사업무Service SharedAdmissionFoodService(TestDatabase database,
        EfSsalddelFoodOrderStore store, string driverId, ISharedDriverWorkAdmissionService admission)
        => new(database.Context, new InMemoryDriverLocationStore(), new NoOpRouteService(), new NoOpQueueTransition(),
            new InMemory음식배달권실행공간Store(), store, new NoOpFoodLedgerOutbox(), new NoOpTransportSync(),
            new NoOpRestaurantNotification(), new NoOpSettlement(), new RecordingPublisher(),
            new TestCurrentUserAccessor(driverId, "Driver"), NullLogger<음식배달기사업무Service>.Instance,
            new TestDispatchAvailability(), sharedAdmission: admission);
}
