using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class FoodCargoDispatchBoundaryTests
{
    [Fact]
    public async Task 음식추천은_화물상태Port가실패해도_음식Aging과요금및알림을보존한다()
    {
        await using var db = CreateDb();
        var queue = CreateQueue(상태값.배차업무유형.음식배달);
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var original = Snapshot("food-driver", 기사앱식별자.FoodDeliveryDriverApp);
        var store = new StateStore(original);
        var cargo = new CargoStatePort { ThrowOnRecommendation = true };
        var notification = new NotificationPort();
        var pricing = new FoodPricingPort();
        var service = CreateService(db, cargo, notification, pricing,
            new 음식배달기사추천기록Service(store));

        var result = await service.추천시작Async(queue.의뢰Id, original.DriverId);

        Assert.True(result.전환여부);
        Assert.Equal(0, cargo.RecommendationCalls);
        Assert.Equal(1, pricing.Calls);
        Assert.Equal(1, notification.CreateCalls);
        Assert.Equal((queue.Id, queue.의뢰Id, original.DriverId, 1), notification.LastRequest);
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        Assert.Equal(queue.Id, saved.Id);
        Assert.Equal(queue.의뢰Id, saved.의뢰Id);
        Assert.Equal(상태값.배차업무유형.음식배달, saved.배차업무유형);
        Assert.Equal(상태값.배차노출상태.추천중, saved.배차노출상태);
        Assert.Equal(4000m, saved.기사지급예정액);
        Assert.NotNull(saved.기사제안요금계산근거Json);
        Assert.Null(saved.공개전환시각);
        Assert.Single(await db.운영배차활동사건.ToListAsync());
        Assert.Equal(1, store.UpsertCalls);
        Assert.Equal(original with
        {
            Aging기준시각Utc = saved.추천시작시각!.Value,
            Aging점수 = 0m,
            마지막추천시각Utc = saved.추천시작시각.Value,
            마지막후보없음시각Utc = null,
            후보없음횟수 = 0
        }, store.Current);
    }

    [Fact]
    public async Task 화물추천은_기존화물Port만호출하고_음식Port와음식요금을호출하지않는다()
    {
        await using var db = CreateDb();
        var queue = CreateQueue(상태값.배차업무유형.용달운송);
        queue.기사지급예정액 = 27000m;
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var cargo = new CargoStatePort();
        var notification = new NotificationPort();
        var pricing = new FoodPricingPort();
        var food = new FoodRecordPort { ThrowOnRecommendation = true };
        var service = CreateService(db, cargo, notification, pricing, food);

        var result = await service.추천시작Async(queue.의뢰Id, "cargo-driver");

        Assert.True(result.전환여부);
        Assert.Equal(1, cargo.RecommendationCalls);
        Assert.Equal("cargo-driver", cargo.LastDriverId);
        Assert.Equal(queue.추천시작시각, cargo.LastRecommendedAt);
        Assert.Equal(0, food.Calls);
        Assert.Equal(0, pricing.Calls);
        Assert.Equal(1, notification.CreateCalls);
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        Assert.Equal(27000m, saved.기사지급예정액);
        Assert.Null(saved.기사제안요금계산근거Json);
    }

    [Fact]
    public async Task 음식추천Port미구성은_제안원장과알림및요금을변경하지않고차단한다()
    {
        await using var db = CreateDb();
        var queue = CreateQueue(상태값.배차업무유형.음식배달);
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var cargo = new CargoStatePort { ThrowOnRecommendation = true };
        var notification = new NotificationPort();
        var pricing = new FoodPricingPort();
        var service = CreateService(db, cargo, notification, pricing, food: null);

        var result = await service.추천시작Async(queue.의뢰Id, "food-driver");

        Assert.False(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.배차구성오류, result.결과코드);
        Assert.Equal(0, cargo.RecommendationCalls);
        Assert.Equal(0, pricing.Calls);
        Assert.Equal(0, notification.CreateCalls);
        Assert.Empty(await db.운영배차활동사건.ToListAsync());
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        Assert.Equal(상태값.배차노출상태.추천대기, saved.배차노출상태);
        Assert.Equal(0, saved.추천라운드);
        Assert.Null(saved.현재추천대상기사Id);
        Assert.Null(saved.추천시작시각);
        Assert.Null(saved.기사제안요금계산근거Json);
    }

    [Fact]
    public async Task 음식추천만료후_다음음식기사를재탐색하고_화물Port로넘기지않는다()
    {
        await using var db = CreateDb();
        var queue = CreateQueue(상태값.배차업무유형.음식배달);
        queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = "expired-food-driver";
        queue.추천라운드 = 2;
        queue.추천시작시각 = DateTime.UtcNow.AddMinutes(-2);
        queue.추천만료시각 = DateTime.UtcNow.AddMinutes(-1);
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var selection = new CandidatePort(배차추천후보선정결과.선정됨(
            new 배차추천후보("next-food-driver", 95m, "다음 음식 기사")));
        var store = new StateStore(Snapshot("next-food-driver", 기사앱식별자.FoodDeliveryDriverApp));
        var cargo = new CargoStatePort { ThrowOnRecommendation = true };
        var notification = new NotificationPort();
        var service = CreateService(db, cargo, notification, new FoodPricingPort(),
            new 음식배달기사추천기록Service(store), selection);

        var result = await service.추천만료처리Async(queue.의뢰Id);

        Assert.True(result.전환여부);
        Assert.Equal(1, selection.Calls);
        Assert.Equal("expired-food-driver", selection.LastExcludedDriverId);
        Assert.Equal(0, cargo.RecommendationCalls);
        Assert.Equal(1, store.UpsertCalls);
        Assert.Equal(1, notification.CreateCalls);
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        Assert.Equal("next-food-driver", saved.현재추천대상기사Id);
        Assert.Equal(3, saved.추천라운드);
        Assert.Equal(상태값.배차노출상태.추천중, saved.배차노출상태);
        Assert.Null(saved.공개전환시각);
    }

    [Theory]
    [InlineData(상태값.배차업무유형.음식배달, false)]
    [InlineData(상태값.배차업무유형.용달운송, true)]
    public async Task 직접공개배차전환은_화물에서만허용한다(int businessType, bool shouldChange)
    {
        await using var db = CreateDb();
        var queue = CreateQueue(businessType);
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var cargo = new CargoStatePort { ThrowOnRecommendation = true };
        var food = new FoodRecordPort { ThrowOnRecommendation = true };
        var service = CreateService(db, cargo, new NotificationPort(), new FoodPricingPort(), food);

        var result = await service.공개배차로전환Async(queue.의뢰Id);

        Assert.Equal(shouldChange, result.전환여부);
        Assert.Equal(0, cargo.RecommendationCalls);
        Assert.Equal(0, food.Calls);
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        if (shouldChange)
        {
            Assert.Equal(배차대기원장전환결과코드.공개배차전환됨, result.결과코드);
            Assert.Equal(상태값.배차큐단계.공개배차, saved.배차큐단계);
            Assert.Equal(상태값.배차노출상태.공개중, saved.배차노출상태);
            Assert.NotNull(saved.공개전환시각);
        }
        else
        {
            Assert.Equal(배차대기원장전환결과코드.단계불일치, result.결과코드);
            Assert.Equal(상태값.배차큐단계.배차추천, saved.배차큐단계);
            Assert.Equal(상태값.배차노출상태.추천대기, saved.배차노출상태);
            Assert.Null(saved.공개전환시각);
        }
    }

    [Fact]
    public async Task 음식최대추천라운드후에도_후보없음은_화물공개배차대신재탐색대기로남는다()
    {
        await using var db = CreateDb();
        var queue = CreateQueue(상태값.배차업무유형.음식배달);
        queue.추천라운드 = 5;
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var selection = new CandidatePort(배차추천후보선정결과.적격후보없음("음식 후보 없음"));
        var cargo = new CargoStatePort { ThrowOnRecommendation = true };
        var food = new FoodRecordPort { ThrowOnRecommendation = true };
        var service = CreateService(db, cargo, new NotificationPort(), new FoodPricingPort(), food, selection);

        var result = await service.추천대기처리Async(queue.의뢰Id);

        Assert.True(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.음식배달후보재탐색대기, result.결과코드);
        Assert.Equal(1, selection.Calls);
        Assert.Equal(0, cargo.RecommendationCalls);
        Assert.Equal(0, food.Calls);
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        Assert.Equal(상태값.배차큐단계.배차추천, saved.배차큐단계);
        Assert.Equal(상태값.배차노출상태.추천후보없음, saved.배차노출상태);
        Assert.Null(saved.공개전환시각);
    }

    [Theory]
    [InlineData(기사앱식별자.CargoYongdalDriverApp)]
    [InlineData(null)]
    [InlineData("UnknownDriverApp")]
    public async Task 음식추천기록Adapter는_음식AppKey가아닌상태를변경하지않는다(string? appKey)
    {
        var original = Snapshot("driver", appKey);
        var store = new StateStore(original);
        var service = new 음식배달기사추천기록Service(store);

        await service.추천기록Async(original.DriverId, DateTime.UtcNow);

        Assert.Equal(1, store.GetCalls);
        Assert.Equal(0, store.UpsertCalls);
        Assert.Equal(original, store.Current);
    }

    [Fact]
    public async Task 음식추천기록Adapter는_없는기사상태를화물상태로생성하지않는다()
    {
        var store = new StateStore(null);
        var service = new 음식배달기사추천기록Service(store);

        await service.추천기록Async("missing-driver", DateTime.UtcNow);

        Assert.Equal(1, store.GetCalls);
        Assert.Equal(0, store.UpsertCalls);
        Assert.Null(store.Current);
    }

    [Fact]
    public async Task 음식추천기록실패는_성공으로숨기거나화물Port로대체하지않는다()
    {
        await using var db = CreateDb();
        var queue = CreateQueue(상태값.배차업무유형.음식배달);
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var store = new StateStore(Snapshot("food-driver", 기사앱식별자.FoodDeliveryDriverApp))
        {
            ThrowOnUpsert = true
        };
        var cargo = new CargoStatePort { ThrowOnRecommendation = true };
        var service = CreateService(db, cargo, new NotificationPort(), new FoodPricingPort(),
            new 음식배달기사추천기록Service(store));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.추천시작Async(queue.의뢰Id, "food-driver"));

        Assert.Equal("FoodIndexWriteFailed", error.Message);
        Assert.Equal(0, cargo.RecommendationCalls);
        // 기존 저장 후 알림/인덱스 순서를 유지한다. 전체 작업의 원자성을 새로 주장하지 않는다.
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleAsync();
        Assert.Equal(상태값.배차노출상태.추천중, saved.배차노출상태);
        Assert.Equal("food-driver", saved.현재추천대상기사Id);
    }

    private static SsalddelContext CreateDb()
        => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"food-cargo-dispatch-{Guid.NewGuid():N}").Options,
            new DummyPersonalDataEncryptionService());

    private static 운송원장 CreateQueue(int businessType)
        => new()
        {
            Id = 10,
            의뢰Id = "REQUEST-BOUNDARY-1",
            상태 = 상태값.배차대기상태.대기,
            배차업무유형 = businessType,
            원본의뢰유형 = businessType == 상태값.배차업무유형.음식배달
                ? 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.음식점주문
                : 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.화주운송의뢰,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천대기,
            픽업_위도 = 37.5m,
            픽업_경도 = 127m
        };

    private static 국내화물운송기사상태Snapshot Snapshot(string driverId, string? appKey)
        => new(driverId, 11, 상태값.기사운행상태.운행중,
            DateTime.UnixEpoch, DateTime.UnixEpoch, 100m,
            37.5m, 127m, 5m, DateTime.UnixEpoch, DateTime.UnixEpoch,
            DateTime.UnixEpoch, DateTime.UnixEpoch, 3,
            "immediate", "test", "return-test", 12m, "test-preference", appKey);

    private static 배차대기원장전환Service CreateService(
        SsalddelContext db,
        CargoStatePort cargo,
        NotificationPort notification,
        FoodPricingPort pricing,
        I음식배달기사추천기록Service? food,
        CandidatePort? selection = null)
        => new(db, Options.Create(new 배차큐정책Options()), selection!, notification,
            cargo, new 음식배달배차흐름Resolver(), pricing, food);

    private sealed class CandidatePort(배차추천후보선정결과 result) : I배차추천후보선정Service
    {
        public int Calls { get; private set; }
        public string? LastExcludedDriverId { get; private set; }

        public Task<배차추천후보선정결과> 다음후보선정Async(
            string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastExcludedDriverId = 제외기사Id;
            return Task.FromResult(result);
        }
    }

    private sealed class NotificationPort : I배차추천알림Service
    {
        public int CreateCalls { get; private set; }
        public (long, string, string, int)? LastRequest { get; private set; }

        public Task 추천알림요청생성Async(long 배차대기Id, string 의뢰Id, string 기사Id, int 추천라운드,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastRequest = (배차대기Id, 의뢰Id, 기사Id, 추천라운드);
            return Task.CompletedTask;
        }

        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FoodRecordPort : I음식배달기사추천기록Service
    {
        public int Calls { get; private set; }
        public bool ThrowOnRecommendation { get; init; }

        public Task 추천기록Async(string driverId, DateTime 추천시각Utc, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (ThrowOnRecommendation) throw new InvalidOperationException("FoodPortMustNotBeCalled");
            return Task.CompletedTask;
        }
    }

    private sealed class StateStore(국내화물운송기사상태Snapshot? initial) : I국내화물운송기사상태Store
    {
        public 국내화물운송기사상태Snapshot? Current { get; private set; } = initial;
        public int GetCalls { get; private set; }
        public int UpsertCalls { get; private set; }
        public bool ThrowOnUpsert { get; init; }

        public Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            if (ThrowOnUpsert) throw new InvalidOperationException("FoodIndexWriteFailed");
            Current = snapshot;
            return Task.CompletedTask;
        }

        public Task<국내화물운송기사상태Snapshot?> GetAsync(string driverId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(Current?.DriverId == driverId ? Current : null);
        }

        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(
            decimal latitude, decimal longitude, decimal radiusKm, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(
            int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RemoveAsync(string driverId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CargoStatePort : I국내화물운송기사상태Service
    {
        public int RecommendationCalls { get; private set; }
        public string? LastDriverId { get; private set; }
        public DateTime? LastRecommendedAt { get; private set; }
        public bool ThrowOnRecommendation { get; init; }

        public Task<국내화물운송기사상태Snapshot?> 추천기록Async(
            string driverId, DateTime 추천시각Utc, CancellationToken cancellationToken = default)
        {
            RecommendationCalls++;
            LastDriverId = driverId;
            LastRecommendedAt = 추천시각Utc;
            if (ThrowOnRecommendation) throw new InvalidOperationException("CargoPortMustNotBeCalled");
            return Task.FromResult<국내화물운송기사상태Snapshot?>(null);
        }

        public Task<국내화물운송기사상태Snapshot> 운행시작Async(string driverId, long shiftId,
            DateTime startedAtUtc, string startMode, string startLocation, string? returnDestination,
            string? 복귀콜선호 = null, string? appKey = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<국내화물운송기사상태Snapshot> 위치갱신Async(DriverLocationSnapshot location,
            long? shiftId = null, decimal? 상차접근허용반경Km = null, string? appKey = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<국내화물운송기사상태Snapshot?> 후보없음기록Async(
            string driverId, DateTime 기준시각Utc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task 운행종료Async(string driverId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class FoodPricingPort : I음식배달기사제안요금Service
    {
        public int Calls { get; private set; }

        public Task<음식배달기사제안요금산정결과> 산정Async(
            운송원장 queue, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new 음식배달기사제안요금산정결과(
                new 음식배달기사제안요금판정(2500m, 1000m, 500m, 4000m, true, true),
                new 픽업지기상관측결과(true, true, 픽업지기상자료상태Code.Available,
                    "1", "1.0", DateTime.UnixEpoch, 픽업지기상관측결과.공식자료출처, new string('a', 64)),
                "food-boundary-test.r1", DateTime.UtcNow));
        }
    }
}
