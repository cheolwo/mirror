using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 음식권역복귀일정평가ServiceTests
{
    private static readonly 배차경로좌표 Current = new(37.5m, 127m);

    [Fact]
    public async Task 저장한전체활성음식의마지막위치를비교하고이미픽업한장소를다시넣지않는다()
    {
        await using var db = Context(); var now = DateTime.UtcNow;
        var candidate = await SeedAsync(db, now);
        var routes = new RoutePort(); var optimizer = new CapturingOptimizer(routes);
        var result = await Service(db, optimizer).평가Async("driver", candidate, Current, now);
        Assert.True(result.전체실경로확인됨); Assert.True(result.적격여부);
        Assert.Equal(new 배차경로좌표(37.5m, 127.10m), result.기존마지막좌표);
        // 새 주문만 보면 가까워 보이지만, 기존의 동쪽 전달을 모두 마치면 끝점은 그대로다.
        Assert.Equal(result.기존마지막좌표, result.후보마지막좌표);
        Assert.NotEqual(new 배차경로좌표(candidate.하차_위도!.Value, candidate.하차_경도!.Value), result.후보마지막좌표);
        Assert.True(optimizer.Requests[0].작업목록.Single().이미픽업완료);
        Assert.Single(optimizer.Results[0].방문순서); Assert.Equal("dropoff", optimizer.Results[0].방문순서[0].단계유형);
        Assert.All(routes.Options, option => Assert.Equal("traavoidcaronly", option));
        Assert.Equal(2, optimizer.Requests[1].작업목록.Count);
    }

    [Theory]
    [InlineData("Cargo")]
    [InlineData("MissingPreparation")]
    [InlineData("InterruptedLatest")]
    [InlineData("RecookingOldAttempt")]
    [InlineData("UnknownVehicle")]
    [InlineData("ActiveLimit")]
    public async Task 교차화물누락회차과거시도차량상한은복귀가점을위한적격으로추정하지않는다(string condition)
    {
        await using var db = Context(); var now = DateTime.UtcNow;
        var candidate = await SeedAsync(db, now);
        if (condition == "Cargo") db.운송원장.Add(new 운송원장 { 의뢰Id = "cargo", 기사_운송자 = "driver", 배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = "운송중" });
        if (condition == "MissingPreparation")
        {
            var order = await db.음식주문.Include(x => x.상태이력).SingleAsync(x => x.주문번호 == "active");
            Assert.NotEmpty(order.상태이력);
            db.RemoveRange(order.상태이력);
            order.상태이력.Clear();
            order.조리예상완료시각Utc = null;
        }
        if (condition == "InterruptedLatest") db.음식배달시도.Add(new 음식배달시도 { 주문번호 = "active", 제안Id = "active", 기사Id = "driver", 시도순번 = 2, 중단시각Utc = now, 수락시각Utc = now });
        if (condition == "RecookingOldAttempt") (await db.음식주문.SingleAsync(x => x.주문번호 == "active")).상태이력.Add(new 음식주문상태이력 { 다음상태 = 음식주문상태코드.조리중, 사유 = 음식주문현재조리Policy.재조리요청이력사유, 전이시각Utc = now });
        if (condition == "UnknownVehicle") (await db.Set<배달기사>().SingleAsync()).차량 = "미확인";
        if (condition == "ActiveLimit")
            for (var i = 0; i < 2; i++) db.운송원장.Add(new 운송원장 { 의뢰Id = $"extra-{i}", 기사_운송자 = "driver", 배차업무유형 = 상태값.배차업무유형.음식배달, 상태 = "확정", 배차큐단계 = 상태값.배차큐단계.확정 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        if (condition == "MissingPreparation")
        {
            var persisted = await db.음식주문.AsNoTracking().Include(x => x.상태이력).SingleAsync(x => x.주문번호 == "active");
            Assert.Empty(persisted.상태이력);
            Assert.Null(persisted.조리예상완료시각Utc);
            Assert.Null(음식주문현재조리Policy.계산(persisted).ReadyAtUtc);
        }
        var optimizer = new CapturingOptimizer(new RoutePort());
        var result = await Service(db, optimizer).평가Async("driver", candidate, Current, now);
        Assert.False(result.적격여부); Assert.False(result.전체실경로확인됨); Assert.Empty(optimizer.Requests);
    }

    [Theory]
    [InlineData(false, "traavoidcaronly")]
    [InlineData(true, "trafast")]
    public async Task 근사경로나다른차량옵션은실경로전체확인으로승격하지않는다(bool actual, string returnedOption)
    {
        await using var db = Context(); var now = DateTime.UtcNow;
        var candidate = await SeedAsync(db, now);
        var result = await Service(db, new CapturingOptimizer(new RoutePort(actual, returnedOption))).평가Async("driver", candidate, Current, now);
        Assert.False(result.전체실경로확인됨); Assert.False(result.적격여부);
    }

    private static 음식권역복귀일정평가Service Service(SsalddelContext db, I픽업하차경로최적화Service optimizer)
        => new(db, optimizer, Options.Create(new 배차큐정책Options()), Options.Create(new FoodDriverRoutePricingOptions()));

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase($"food-return-{Guid.NewGuid():N}").Options, new EncryptionPort());

    private static async Task<운송원장> SeedAsync(SsalddelContext db, DateTime now)
    {
        db.Set<배달기사>().Add(new 배달기사 { 기사Id = "driver", 차량 = "오토바이" });
        var active = new 운송원장 { 의뢰Id = "active", 원본의뢰Id = "active", 기사_운송자 = "driver", 확정기사Id = "driver",
            배차업무유형 = 상태값.배차업무유형.음식배달, 배차큐단계 = 상태값.배차큐단계.확정, 상태 = "배송중",
            픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.5m, 하차_경도 = 127.10m };
        db.운송원장.Add(active);
        db.음식주문.Add(new 음식주문 { 주문번호 = "active", 상태 = 음식주문상태코드.픽업완료, 조리예상완료시각Utc = now.AddMinutes(-1),
            상태이력 = [new 음식주문상태이력 { 다음상태 = 음식주문상태코드.픽업대기, 사유 = "음식점 픽업 준비 완료", 전이시각Utc = now.AddMinutes(-1) }] });
        db.음식배달시도.Add(new 음식배달시도 { 주문번호 = "active", 제안Id = "active", 기사Id = "driver", 시도순번 = 1,
            시도StableId = "attempt-active", Revision = 1, 상태Code = 음식배달시도상태Code.픽업완료,
            수락시각Utc = now.AddMinutes(-2), 픽업완료시각Utc = now.AddSeconds(-30) });
        var candidate = new 운송원장 { 의뢰Id = "new", 원본의뢰Id = "new", 배차업무유형 = 상태값.배차업무유형.음식배달,
            픽업_위도 = 37.5m, 픽업_경도 = 127.001m, 하차_위도 = 37.5m, 하차_경도 = 127.005m };
        db.음식주문.Add(new 음식주문 { 주문번호 = "new", 상태 = 음식주문상태코드.기사배정, 조리예상완료시각Utc = now.AddMinutes(1) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return candidate;
    }

    private sealed class CapturingOptimizer(I배차추천경로Service routes) : I픽업하차경로최적화Service
    {
        public List<픽업하차경로최적화요청> Requests { get; } = [];
        public List<픽업하차경로최적화결과> Results { get; } = [];
        public async Task<픽업하차경로최적화결과> 최적화Async(픽업하차경로최적화요청 request, CancellationToken cancellationToken = default)
        { Requests.Add(request); var result = await new 픽업하차경로최적화Service(routes).최적화Async(request, cancellationToken); Results.Add(result); return result; }
    }
    private sealed class EncryptionPort : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class RoutePort(bool actual = true, string option = "traavoidcaronly") : I배차추천경로Service
    {
        public List<string> Options { get; } = [];
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string requestedOption, CancellationToken cancellationToken)
        { Options.Add(requestedOption); var distance = origin is null || destination is null ? 0m : CalculateDistanceKm(origin, destination)!.Value;
            return Task.FromResult<배차경로예상결과?>(new(distance, TimeSpan.FromMinutes((double)(distance * 2m)), null, "Directions5", actual) { RouteOption = option }); }
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => (Math.Abs(source.Latitude - target.Latitude) + Math.Abs(source.Longitude - target.Longitude)) * 100m;
    }
}
