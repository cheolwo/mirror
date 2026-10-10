using System.Data.Common;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task 현재업무조회는정산을읽지않고_기존workspace는동일현재업무와정산을유지한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new Ssalddel.Services.Food.EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        database.Context.운송원장.Add(CreateRecommendedTransport("current-offer", order.주문번호, "current-driver"));
        await database.Context.SaveChangesAsync();
        var sql = new CurrentWorkSqlRecorder();
        await using var watched = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(database.Context.Database.GetDbConnection()).AddInterceptors(sql).Options, new PassThroughEncryptionService());
        var monthly = new CurrentWorkMonthly();
        var driver = CreateDriverWork(watched, new(watched), new(), "current-driver");
        var useCase = new FoodDeliveryDriverWorkspaceUseCase(watched, driver, monthly,
            new CurrentWorkExecution(), new RecookingFeatureFlags(), new CurrentWorkClock());
        var current = await useCase.GetCurrentWorkAsync("current-driver", default);
        var currentQueries = sql.Commands.ToArray();
        Assert.Equal(0, monthly.Reads);
        Assert.DoesNotContain(currentQueries, IsSettlementQuery);
        Assert.Single(current.Recommendations);
        Assert.Empty(current.ActiveDeliveries);
        Assert.DoesNotContain("settlement", JsonSerializer.Serialize(current, new JsonSerializerOptions(JsonSerializerDefaults.Web)), StringComparison.OrdinalIgnoreCase);
        sql.Commands.Clear();
        var legacy = await useCase.GetAsync("current-driver", default);
        Assert.Equal(1, monthly.Reads);
        Assert.Contains(sql.Commands, IsSettlementQuery);
        Assert.Equal(JsonSerializer.Serialize(current.Recommendations), JsonSerializer.Serialize(legacy.Recommendations));
        Assert.Equal(current.UpdatedAtUtc, legacy.UpdatedAtUtc);
        Assert.Equal(current.DispatchAutomationEnabled, legacy.DispatchAutomationEnabled);
        Assert.True(currentQueries.Length < sql.Commands.Count);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(current).Length < JsonSerializer.SerializeToUtf8Bytes(legacy).Length);
        Console.WriteLine($"CurrentWorkQueryMeasurement currentQueries={currentQueries.Length}; legacyQueries={sql.Commands.Count}; currentBytes={JsonSerializer.SerializeToUtf8Bytes(current).Length}; legacyBytes={JsonSerializer.SerializeToUtf8Bytes(legacy).Length}; currentMonthlyReads=0; currentSettlementQueries=0");
        Assert.Empty(legacy.OrderSettlements);
        Assert.Equal("current-driver", legacy.Settlement.기사Id);
    }

    [Fact]
    public async Task 정산조회실패는0원응답으로바꾸지않고_현재업무조회는영향받지않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monthly = new CurrentWorkMonthly { Unavailable = true };
        var driver = CreateDriverWork(database.Context, new(database.Context), new(), "current-driver");
        var useCase = new FoodDeliveryDriverWorkspaceUseCase(database.Context, driver, monthly,
            new CurrentWorkExecution(), new RecookingFeatureFlags());
        Assert.Empty((await useCase.GetCurrentWorkAsync("current-driver", default)).ActiveDeliveries);
        Assert.Equal(0, monthly.Reads);
        await Assert.ThrowsAsync<FoodDeliverySettlementUnavailableException>(
            () => useCase.GetAsync("current-driver", default));
        Assert.Equal(1, monthly.Reads);
    }

    [Fact]
    public async Task 현재업무조회는배정기사의현재시도와준비상태를유지한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new Ssalddel.Services.Food.EfSsalddelFoodOrderStore(database.Context);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 조리예상분 = 10 }, "restaurant-41");
        database.Context.운송원장.Add(CreateRecommendedTransport("active-current", order.주문번호, "current-driver"));
        await database.Context.SaveChangesAsync();
        var driver = CreateDriverWork(database, store, new(), "current-driver");
        Assert.True((await driver.수락Async("current-driver", "active-current")).IsSuccess);
        var monthly = new CurrentWorkMonthly { FailIfRead = true };
        var useCase = new FoodDeliveryDriverWorkspaceUseCase(database.Context, driver, monthly,
            new CurrentWorkExecution(), new RecookingFeatureFlags());
        var current = await useCase.GetCurrentWorkAsync("current-driver", default);
        var active = Assert.Single(current.ActiveDeliveries);
        var attempt = await database.Context.음식배달시도.SingleAsync();
        Assert.Equal(order.주문번호, active.OrderNo);
        Assert.Equal(attempt.시도StableId, active.DeliveryAttemptId);
        Assert.Equal(attempt.Revision, active.AttemptRevision);
        Assert.Equal(1, active.CurrentPreparationRound);
        Assert.Empty(current.Recommendations);
        Assert.Equal(0, monthly.Reads);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(12, true)]
    [InlineData(13, false)]
    public void 순수묶음표시는기존12분경계와동결금액을유지한다(int gapMinutes, bool included)
    {
        var ready = new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc);
        Ssalddel.Contracts.Driver.Food.FoodDeliveryDriverOfferDto Make(string id, DateTime at, decimal payout) => new()
        {
            OfferId = id, RestaurantName = id, DriverPayout = payout,
            Pickup = new() { Latitude = 37.58m, Longitude = 127.08m, TargetAtUtc = at },
            Dropoff = new() { Latitude = 37.581m, Longitude = 127.081m }
        };
        var offers = new[] { Make("first", ready, 3000), Make("second", ready.AddMinutes(gapMinutes), 4000) };
        var before = JsonSerializer.Serialize(offers);
        var candidates = FoodDeliveryCurrentBundlePolicy.Build(offers);
        Assert.Equal(included ? 1 : 0, candidates.Count);
        if (included) { Assert.Equal(7000, candidates[0].TotalPayout); Assert.Equal(new[] { "first", "second" }, candidates[0].OfferIds); }
        Assert.Equal(before, JsonSerializer.Serialize(offers));
    }

    [Fact]
    public void 순수묶음표시는미확인좌표를추정하지않는다()
    {
        var offers = new[]
        {
            new Ssalddel.Contracts.Driver.Food.FoodDeliveryDriverOfferDto { OfferId = "unknown" },
            new Ssalddel.Contracts.Driver.Food.FoodDeliveryDriverOfferDto { OfferId = "also-unknown" }
        };
        Assert.Empty(FoodDeliveryCurrentBundlePolicy.Build(offers));
        Assert.Null(offers[0].Pickup.Latitude);
    }

    private static bool IsSettlementQuery(string command)
        => command.Contains("음식주문기사정산", StringComparison.Ordinal)
            || command.Contains("음식주문기사지급검증", StringComparison.Ordinal)
            || command.Contains("기사월정산", StringComparison.Ordinal);

    private sealed class CurrentWorkSqlRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Commands.Add(command.CommandText); return result; }
    }

    private sealed class CurrentWorkMonthly : I배달기사월정산UseCase
    {
        public int Reads { get; private set; }
        public bool FailIfRead { get; init; }
        public bool Unavailable { get; init; }
        public Task<Result<배달기사월정산응답>> 당월조회Async(string driverId, string? currentUserId, CancellationToken cancellationToken)
        {
            Reads++;
            if (FailIfRead) throw new InvalidOperationException("현재 업무에서 정산을 조회했습니다.");
            if (Unavailable) return Task.FromResult(Result.Fail<배달기사월정산응답>("storage unavailable"));
            return Task.FromResult(Result.Ok(new 배달기사월정산응답 { 기사Id = driverId }));
        }
        public Task<Result<배달기사월정산결제완료응답>> 결제완료처리Async(string driverId, int year, int month, string? currentUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
    private sealed class CurrentWorkExecution : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation;
        public bool IsSimulation => true;
        public bool IsOperational => false;
    }
    private sealed class CurrentWorkClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero); }
}
