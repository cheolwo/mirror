using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class 음식배달배차업무정책Tests
{
    [Fact]
    public async Task F드라이버_운행중_최신위치_후보만_선정한다()
    {
        var now = DateTime.UtcNow;
        var store = new FakeDriverStateStore(
        [
            Snapshot("food-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.501m, 127.001m, now, 3m),
            Snapshot("cargo-driver", 기사앱식별자.CargoYongdalDriverApp, 37.5001m, 127.0001m, now, 50m),
            Snapshot("stale-food-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127.0001m, now.AddMinutes(-11), 100m)
        ]);
        var foodSpaceStore = await CreateFoodSpaceStoreAsync(
            ("food-driver", 37.501m, 127.001m),
            ("cargo-driver", 37.5001m, 127.0001m),
            ("stale-food-driver", 37.5001m, 127.0001m));
        var policy = CreatePolicy(store, new FakeRejectedRequestStore(), foodSpaceStore);

        var candidate = await policy.다음후보선정Async(Queue());

        Assert.NotNull(candidate);
        Assert.Equal("food-driver", candidate!.DriverId);
        Assert.Contains("음식점까지", candidate.추천사유);
    }

    [Fact]
    public async Task 이미_거절한_F드라이버는_다음_후보에서_제외한다()
    {
        var now = DateTime.UtcNow;
        var store = new FakeDriverStateStore(
        [
            Snapshot("rejected-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127.0001m, now, 100m),
            Snapshot("next-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127.002m, now, 0m)
        ]);
        var rejected = new FakeRejectedRequestStore();
        await rejected.RejectAsync("rejected-driver", "food-order-1");
        var foodSpaceStore = await CreateFoodSpaceStoreAsync(
            ("rejected-driver", 37.5001m, 127.0001m),
            ("next-driver", 37.502m, 127.002m));
        var policy = CreatePolicy(store, rejected, foodSpaceStore);

        var candidate = await policy.다음후보선정Async(Queue());

        Assert.NotNull(candidate);
        Assert.Equal("next-driver", candidate!.DriverId);
    }

    [Fact]
    public async Task 기사가_설정한_상차접근반경_밖의_음식점은_추천하지_않는다()
    {
        var now = DateTime.UtcNow;
        var store = new FakeDriverStateStore(
        [
            Snapshot(
                "near-but-outside-own-radius",
                기사앱식별자.FoodDeliveryDriverApp,
                37.518m,
                127m,
                now,
                100m,
                allowedRadiusKm: 1m),
            Snapshot(
                "farther-but-inside-own-radius",
                기사앱식별자.FoodDeliveryDriverApp,
                37.527m,
                127m,
                now,
                0m,
                allowedRadiusKm: 5m)
        ]);
        var foodSpaceStore = await CreateFoodSpaceStoreAsync(
            ("near-but-outside-own-radius", 37.518m, 127m),
            ("farther-but-inside-own-radius", 37.527m, 127m));
        var policy = CreatePolicy(store, new FakeRejectedRequestStore(), foodSpaceStore);

        var candidate = await policy.다음후보선정Async(Queue());

        Assert.NotNull(candidate);
        Assert.Equal("farther-but-inside-own-radius", candidate!.DriverId);
    }

    [Fact]
    public async Task 첫_판정에_반경내_기사가_없어도_기사가_진입한_뒤_다시_실행하면_재판정한다()
    {
        var now = DateTime.UtcNow;
        var store = new FakeDriverStateStore(
        [
            Snapshot("entering-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.56m, 127m, now, 0m)
        ]);
        var foodSpaceStore = await CreateFoodSpaceStoreAsync(("entering-driver", 37.56m, 127m));
        var policy = CreatePolicy(store, new FakeRejectedRequestStore(), foodSpaceStore);

        var beforeEntry = await policy.다음후보선정Async(Queue());
        store.Replace(
        [
            Snapshot("entering-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.501m, 127m, now, 0m)
        ]);
        var pickupSpaceKey = 음식배달권정책.판정(new 배차경로좌표(37.5m, 127m), null).배달권키;
        await foodSpaceStore.Upsert기사Async(
            pickupSpaceKey,
            "entering-driver",
            음식배달권정책.인접배달권키조회(pickupSpaceKey));

        var afterEntry = await policy.다음후보선정Async(Queue());

        Assert.Null(beforeEntry);
        Assert.Equal("entering-driver", afterEntry!.DriverId);
    }

    [Fact]
    public async Task 음식배달은_더_가까운_미등록기사보다_동일_음식배달공간의_기사를_먼저_검토한다()
    {
        var now = DateTime.UtcNow;
        var stateStore = new FakeDriverStateStore(
        [
            Snapshot("unindexed-near-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 100m),
            Snapshot("same-space-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.5100m, 127m, now, 0m)
        ]);
        var foodSpaceStore = new InMemory음식배달권실행공간Store();
        var foodSpaceKey = 음식배달권정책.판정(new 배차경로좌표(37.5m, 127m), null).배달권키;
        await foodSpaceStore.Upsert기사Async(
            foodSpaceKey,
            "same-space-driver",
            음식배달권정책.인접배달권키조회(foodSpaceKey));
        var policy = CreatePolicy(
            stateStore,
            new FakeRejectedRequestStore(),
            foodSpaceStore);

        var candidate = await policy.다음후보선정Async(Queue());

        Assert.NotNull(candidate);
        Assert.Equal("same-space-driver", candidate!.DriverId);
        Assert.Contains("같은 음식배달권", candidate.추천사유);
    }

    [Fact]
    public async Task 동일_음식배달공간에_적격기사가_없으면_인접_음식배달공간을_조회한다()
    {
        var now = DateTime.UtcNow;
        var stateStore = new FakeDriverStateStore(
        [
            Snapshot("stale-same-space-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.501m, 127m, now.AddMinutes(-11), 100m),
            Snapshot("adjacent-space-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0m)
        ]);
        var foodSpaceStore = new InMemory음식배달권실행공간Store();
        var foodSpaceKey = 음식배달권정책.판정(new 배차경로좌표(37.5m, 127m), null).배달권키;
        var adjacentSpaceKey = 음식배달권정책.인접배달권키조회(foodSpaceKey).First();
        await foodSpaceStore.Upsert기사Async(
            foodSpaceKey,
            "stale-same-space-driver",
            음식배달권정책.인접배달권키조회(foodSpaceKey));
        await foodSpaceStore.Upsert기사Async(
            adjacentSpaceKey,
            "adjacent-space-driver",
            음식배달권정책.인접배달권키조회(adjacentSpaceKey));
        var policy = CreatePolicy(
            stateStore,
            new FakeRejectedRequestStore(),
            foodSpaceStore);

        var candidate = await policy.다음후보선정Async(Queue());

        Assert.NotNull(candidate);
        Assert.Equal("adjacent-space-driver", candidate!.DriverId);
        Assert.Contains("인접 음식배달권", candidate.추천사유);
    }

    [Fact]
    public async Task 조리완료_예정시각이_있으면_픽업창에_맞게_도착하는_기사를_우선한다()
    {
        var now = DateTime.UtcNow;
        var stateStore = new FakeDriverStateStore(
        [
            Snapshot("too-early-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 100m),
            Snapshot("ready-window-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.525m, 127m, now, 0m)
        ]);
        var foodSpaceStore = new InMemory음식배달권실행공간Store();
        var foodSpaceKey = 음식배달권정책.판정(new 배차경로좌표(37.5m, 127m), null).배달권키;
        await foodSpaceStore.Upsert기사Async(foodSpaceKey, "too-early-driver", []);
        await foodSpaceStore.Upsert기사Async(foodSpaceKey, "ready-window-driver", []);
        var orderStore = new FakeFoodOrderStore(new 음식주문응답
        {
            주문번호 = "food-order-1",
            조리예상완료시각Utc = now.AddMinutes(10)
        });
        var policy = CreatePolicy(
            stateStore,
            new FakeRejectedRequestStore(),
            foodSpaceStore,
            orderStore);

        var candidate = await policy.다음후보선정Async(Queue());

        Assert.NotNull(candidate);
        Assert.Equal("ready-window-driver", candidate!.DriverId);
        Assert.Contains("적정 픽업창", candidate.추천사유);
    }

    [Fact]
    public async Task 제한거리_확장도_공용_GEO가_아니라_Food_물리공간만_조회한다()
    {
        var now = DateTime.UtcNow;
        var stateStore = new FakeDriverStateStore(
        [
            Snapshot("expanded-food-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127.0501m, now, 0m)
        ]);
        var foodSpaceStore = await CreateFoodSpaceStoreAsync(
            ("expanded-food-driver", 37.5m, 127.0501m));
        var policy = CreatePolicy(
            stateStore,
            new FakeRejectedRequestStore(),
            foodSpaceStore);

        var candidate = await policy.다음후보선정Async(Queue());

        Assert.NotNull(candidate);
        Assert.Equal("expanded-food-driver", candidate!.DriverId);
        Assert.Contains("제한 음식배달공간 확장", candidate.추천사유);
        Assert.False(stateStore.위치반경조회호출됨);
    }

    private static 음식배달배차업무정책 CreatePolicy(
        I국내화물운송기사상태Store store,
        IDriverRejectedRequestStore rejected,
        I음식배달권실행공간Store? foodSpaceStore = null,
        ISsalddelFoodOrderStore? foodOrderStore = null)
    {
        var options = new 배차큐정책Options
        {
            음식배달동일권최소후보기사수 = 1,
            음식배달후보검색반경Km = 5m,
            음식배달후보최대조회수 = 20,
            음식배달평균이동속도KmH = 20m,
            음식배달픽업조기도착허용분 = 3m,
            음식배달픽업지연허용분 = 5m
        };
        return new 음식배달배차업무정책(
            store,
            rejected,
            new StraightLineRouteService(),
            foodSpaceStore ?? new InMemory음식배달권실행공간Store(),
            foodOrderStore ?? new FakeFoodOrderStore(),
            Options.Create(options));
    }

    private static 운송원장 Queue()
        => new()
        {
            의뢰Id = "food-order-1",
            원본의뢰Id = "food-order-1",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            픽업_위도 = 37.5m,
            픽업_경도 = 127m
        };

    private static async Task<InMemory음식배달권실행공간Store> CreateFoodSpaceStoreAsync(
        params (string DriverId, decimal Latitude, decimal Longitude)[] drivers)
    {
        var store = new InMemory음식배달권실행공간Store();
        foreach (var driver in drivers)
        {
            var spaceKey = 음식배달권정책.판정(
                new 배차경로좌표(driver.Latitude, driver.Longitude),
                null).배달권키;
            await store.Upsert기사Async(
                spaceKey,
                driver.DriverId,
                음식배달권정책.인접배달권키조회(spaceKey));
        }

        return store;
    }

    private static 국내화물운송기사상태Snapshot Snapshot(
        string driverId,
        string appKey,
        decimal latitude,
        decimal longitude,
        DateTime receivedAt,
        decimal aging,
        decimal? allowedRadiusKm = null)
        => new(
            driverId,
            null,
            상태값.기사운행상태.운행중,
            receivedAt.AddMinutes(-20),
            receivedAt.AddMinutes(-20),
            aging,
            latitude,
            longitude,
            5m,
            receivedAt,
            receivedAt,
            null,
            null,
            0,
            "immediate",
            "test",
            null,
            상차접근허용반경Km: allowedRadiusKm,
            AppKey: appKey);

    private sealed class FakeDriverStateStore(
        IReadOnlyList<국내화물운송기사상태Snapshot> initialSnapshots) : I국내화물운송기사상태Store
    {
        private IReadOnlyList<국내화물운송기사상태Snapshot> _snapshots = initialSnapshots;

        public bool 위치반경조회호출됨 { get; private set; }

        public void Replace(IReadOnlyList<국내화물운송기사상태Snapshot> snapshots)
            => _snapshots = snapshots;

        public Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<국내화물운송기사상태Snapshot?> GetAsync(string driverId, CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshots.FirstOrDefault(x => x.DriverId == driverId));

        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(
            decimal latitude,
            decimal longitude,
            decimal radiusKm,
            int take,
            CancellationToken cancellationToken = default)
        {
            위치반경조회호출됨 = true;
            return Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>(_snapshots.Take(take).ToArray());
        }

        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>(_snapshots.Take(take).ToArray());

        public Task RemoveAsync(string driverId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeRejectedRequestStore : IDriverRejectedRequestStore
    {
        private readonly Dictionary<string, HashSet<string>> _driversByRequest = new(StringComparer.Ordinal);

        public Task RejectAsync(string driverId, string requestId, CancellationToken cancellationToken = default)
        {
            if (!_driversByRequest.TryGetValue(requestId, out var drivers))
            {
                drivers = new HashSet<string>(StringComparer.Ordinal);
                _driversByRequest[requestId] = drivers;
            }

            drivers.Add(driverId);
            return Task.CompletedTask;
        }

        public Task<bool> IsRejectedAsync(string driverId, string requestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_driversByRequest.TryGetValue(requestId, out var drivers) && drivers.Contains(driverId));

        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string driverId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(_driversByRequest
                .Where(x => x.Value.Contains(driverId))
                .Select(x => x.Key)
                .ToHashSet(StringComparer.Ordinal));

        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string requestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(
                _driversByRequest.TryGetValue(requestId, out var drivers)
                    ? drivers
                    : new HashSet<string>(StringComparer.Ordinal));
    }

    private sealed class FakeFoodOrderStore(음식주문응답? order = null) : ISsalddelFoodOrderStore
    {
        public 음식주문목록응답 GetOrders()
            => new() { Items = order is null ? [] : [order] };

        public 음식주문응답? GetOrder(string orderNo)
            => string.Equals(order?.주문번호, orderNo, StringComparison.Ordinal) ? order : null;

        public 음식주문응답 AddOrder(음식주문등록요청 request)
            => throw new NotSupportedException();

        public 음식주문응답? 음식점수락(string orderNo, 음식점주문수락요청 request)
            => throw new NotSupportedException();

        public 음식주문응답? 배차대기반영(
            string orderNo,
            long dispatchWaitId,
            DateTime dispatchRequestedAtUtc)
            => throw new NotSupportedException();
    }

    private sealed class StraightLineRouteService : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(
            string driverId,
            용달기사? driver,
            DriverLocationSnapshot? currentLocation,
            배차추천검색조건? criteria)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(
            string driverId,
            용달기사? driver,
            DriverLocationSnapshot? currentLocation)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => Task.FromResult<배차경로예상결과?>(null);

        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(
            배차경로좌표? origin,
            IReadOnlyList<배차경로좌표> orderedStops,
            CancellationToken cancellationToken = default)
            => Task.FromResult<배차경로예상결과?>(null);

        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(
            배차경로좌표? origin,
            배차경로좌표? routeAnchor,
            배차경로좌표? pickup,
            배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);

        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
        {
            var latitudeKm = Math.Abs(source.Latitude - target.Latitude) * 111m;
            var longitudeKm = Math.Abs(source.Longitude - target.Longitude) * 88m;
            return latitudeKm + longitudeKm;
        }
    }
}
