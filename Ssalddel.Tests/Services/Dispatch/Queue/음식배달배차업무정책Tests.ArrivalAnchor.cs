using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Theory]
    [InlineData("valid", "CandidateAllowed")]
    [InlineData("late", "ActiveSchedulePickupLate")]
    [InlineData("missingproof", "ActiveScheduleAttemptUnverified")]
    public async Task 수행중후속픽업은기존전달완료예상과실경로근거를이용하며지각이나미확인을제외한다(string scenario, string expectedCode)
    {
        var result = await RunArrivalAnchorLocalScenarioAsync(37.576m, 127.086m, scenario);
        Assert.True(result.ExpectedPassed);
        Assert.Equal(expectedCode, result.Review.후보목록.Single().판정Code);
        Assert.False(result.ActualMapApiCalled);
        if (scenario != "missingproof")
        {
            var item = result.Review.후보목록.Single();
            Assert.Equal("ActiveScheduleEnd", item.위치기준Code);
            Assert.NotNull(item.기존수행완료예상시각Utc);
            Assert.InRange(item.픽업예상소요분!.Value, 1.5m, 1.7m);
            Assert.Equal("VerifiedVehicleRoute", item.경로근거Code);
            Assert.True(result.Basis.가용예상시각Utc < result.EvaluatedAtUtc.AddMinutes((double)item.픽업예상소요분.Value));
        }
        else
        {
            Assert.Equal("UnverifiedActiveSchedule", result.Review.후보목록.Single().위치기준Code);
            Assert.Null(result.Review.후보목록.Single().거리Km);
            Assert.Null(result.Review.후보목록.Single().픽업예상소요분);
        }
    }

    [Theory]
    [InlineData("missing-ready", "ActiveScheduleReadyTimeMissing")]
    [InlineData("approximate", "ActiveSchedulePickupRouteUnverified")]
    [InlineData("wrong-option", "ActiveSchedulePickupRouteUnverified")]
    public async Task 수행중새픽업은준비시각근사경로다른차량경로를명시적으로제외한다(string scenario, string expectedCode)
    {
        var now = DateTime.UtcNow;
        var state = Snapshot("active", 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127m, now, 99m);
        var spaces = await CreateFoodSpaceStoreAsync(("active", 37.5m, 127m));
        var basis = new 음식후속픽업기준결과(true, true, new(37.5001m, 127m), now.AddMinutes(1), "traavoidcaronly", "ActiveScheduleEnd");
        var routes = new AnchorRoutePort(scenario != "approximate", scenario == "wrong-option" ? "trafast" : "traavoidcaronly");
        var ready = scenario == "missing-ready" ? (DateTime?)null : now.AddMinutes(5);
        var result = await AnchorPolicy(new FakeDriverStateStore([state]), spaces, routes,
            new AnchorPlanPort(basis), ready).후보검토Async(Queue());
        Assert.Null(result.선정후보); Assert.Empty(result.최종허용기사Ids);
        Assert.Equal(expectedCode, result.후보목록.Single().판정Code);
    }

    [Fact]
    public async Task 기본OFF는일정조회없이기존현재위치선정을보존한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("active", 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127m, now, 0m)]);
        var spaces = await CreateFoodSpaceStoreAsync(("active", 37.5m, 127m));
        var plan = new AnchorPlanPort(음식후속픽업기준결과.보류("Missing"));
        var actual = await AnchorPolicy(states, spaces, new AnchorRoutePort(), plan, now.AddMinutes(5), enabled: false).후보검토Async(Queue());
        Assert.Equal("active", actual.선정후보?.DriverId); Assert.Equal(0, plan.Calls);
        Assert.Null(actual.후보목록.Single().위치기준Code);
        Assert.False(new 배차큐정책Options().음식배달수행종료기준사용);
    }

    [Fact]
    public async Task 활성업무없음은현재위치기준으로표시하고늦은GPS나수신OFF는일정으로살리지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("idle", 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127m, now, 0m)]);
        var spaces = await CreateFoodSpaceStoreAsync(("idle", 37.5m, 127m));
        var plan = new AnchorPlanPort(new(false, true, new(37.5m, 127m), now.AddMinutes(1), null, "CurrentLocation"));
        var result = await AnchorPolicy(states, spaces, new AnchorRoutePort(), plan, now.AddMinutes(5)).후보검토Async(Queue());
        Assert.Equal("idle", result.선정후보?.DriverId); Assert.Equal("CurrentLocation", result.후보목록.Single().위치기준Code);
        Assert.Null(result.후보목록.Single().기존수행완료예상시각Utc);
        states.Replace([Snapshot("idle", 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127m, now.AddHours(-1), 999m)]);
        plan.Calls = 0;
        var stale = await AnchorPolicy(states, spaces, new AnchorRoutePort(), plan, now.AddMinutes(5)).후보검토Async(Queue());
        Assert.Null(stale.선정후보); Assert.Equal(0, plan.Calls);
        var receiving = new TestDispatchAvailability();
        await receiving.기사의사변경Async("idle", new() { 수신의사Code = Ssalddel.Contracts.Common.Dispatch.운영배차수신의사Code.Off });
        states.Replace([Snapshot("idle", 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127m, now, 999m)]);
        var off = await AnchorPolicy(states, spaces, new AnchorRoutePort(), plan, now.AddMinutes(5), receiving: receiving).후보검토Async(Queue());
        Assert.Null(off.선정후보); Assert.Equal(0, plan.Calls);
    }

    [Fact]
    public async Task 서비스미연결과기본인터페이스미지원은현재위치대체없이제외한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("active", 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127m, now, 999m)]);
        var spaces = await CreateFoodSpaceStoreAsync(("active", 37.5m, 127m));
        var missing = await AnchorPolicy(states, spaces, new AnchorRoutePort(), null, now.AddMinutes(5)).후보검토Async(Queue());
        Assert.Null(missing.선정후보); Assert.Equal("ActiveScheduleServiceUnavailable", missing.후보목록.Single().판정Code);
        var unsupported = await AnchorPolicy(states, spaces, new AnchorRoutePort(), new PlanPort(), now.AddMinutes(5)).후보검토Async(Queue());
        Assert.Null(unsupported.선정후보); Assert.Equal("ActiveScheduleServiceUnsupported", unsupported.후보목록.Single().판정Code);
    }

    [Fact]
    public async Task 미래전달지가가까워도현재GPS공간조회밖의기사를전역탐색한것으로표시하지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("outside", 기사앱식별자.FoodDeliveryDriverApp, 37.9m, 127m, now, 1000m)]);
        var spaces = await CreateFoodSpaceStoreAsync(("outside", 37.9m, 127m));
        var plan = new AnchorPlanPort(new(true, true, new(37.5m, 127m), now.AddMinutes(1), "traavoidcaronly", "ActiveScheduleEnd"));
        var result = await AnchorPolicy(states, spaces, new AnchorRoutePort(), plan, now.AddMinutes(5)).후보검토Async(Queue());
        Assert.Null(result.선정후보); Assert.Empty(result.후보목록); Assert.Equal(0, plan.Calls);
        Assert.Equal("NoEligibleCandidate", result.중단사유Code);
    }

    /// <summary>공개 매장 좌표 주위 합성 업무만 사용한다. 실제 지도 API·운영 DB·모델을 호출하지 않는다.</summary>
    public static async Task<ArrivalAnchorScenarioEvidence> RunArrivalAnchorLocalScenarioAsync(decimal pickupLat, decimal pickupLon, string caseCode)
    {
        if (caseCode is not ("valid" or "late" or "missingproof")) throw new ArgumentException("ArrivalAnchorScenarioUnknown", nameof(caseCode));
        var now = DateTime.UtcNow;
        var current = new 배차경로좌표(pickupLat + 0.004m, pickupLon + 0.004m);
        var end = new 배차경로좌표(pickupLat + 0.001m, pickupLon + 0.001m);
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"arrival-anchor-{Guid.NewGuid():N}").Options, new AnchorEncryptionPort());
        db.Set<배달기사>().Add(new 배달기사 { 기사Id = "anchor-driver", 차량 = "오토바이" });
        db.운송원장.Add(new 운송원장
        {
            의뢰Id = "synthetic-active", 원본의뢰Id = "synthetic-active", 기사_운송자 = "anchor-driver", 확정기사Id = "anchor-driver",
            배차업무유형 = 상태값.배차업무유형.음식배달, 배차큐단계 = 상태값.배차큐단계.확정, 상태 = "배송중",
            픽업_위도 = current.Latitude, 픽업_경도 = current.Longitude, 하차_위도 = end.Latitude, 하차_경도 = end.Longitude
        });
        db.음식주문.Add(new 음식주문
        {
            주문번호 = "synthetic-active", 상태 = 음식주문상태코드.픽업완료, 조리예상완료시각Utc = now.AddMinutes(-1),
            상태이력 = [new 음식주문상태이력 { 다음상태 = 음식주문상태코드.픽업대기, 사유 = "음식점 픽업 준비 완료", 전이시각Utc = now.AddMinutes(-1) }]
        });
        if (caseCode != "missingproof") db.음식배달시도.Add(new 음식배달시도
        {
            주문번호 = "synthetic-active", 제안Id = "synthetic-active", 기사Id = "anchor-driver", 시도순번 = 1,
            시도StableId = "synthetic-active-attempt", Revision = 1, 상태Code = 음식배달시도상태Code.픽업완료,
            수락시각Utc = now.AddMinutes(-2), 픽업완료시각Utc = now.AddSeconds(-30)
        });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var queue = Queue(); queue.픽업_위도 = pickupLat; queue.픽업_경도 = pickupLon;
        var routes = new AnchorRoutePort();
        var options = AnchorOptions(true);
        var plan = new 음식권역복귀일정평가Service(db, new 픽업하차경로최적화Service(routes),
            Options.Create(options), Options.Create(new FoodDriverRoutePricingOptions()));
        var basis = await plan.후속픽업기준조회Async("anchor-driver", queue, current, now);
        var states = new FakeDriverStateStore([Snapshot("anchor-driver", 기사앱식별자.FoodDeliveryDriverApp, current.Latitude, current.Longitude, now, 10m)]);
        var spaces = await CreateFoodSpaceStoreAsync(("anchor-driver", current.Latitude, current.Longitude));
        var ready = now.AddMinutes(caseCode == "late" ? -10 : 5);
        var review = await AnchorPolicy(states, spaces, routes, plan, ready).후보검토Async(queue);
        var expected = caseCode switch { "valid" => "CandidateAllowed", "late" => "ActiveSchedulePickupLate", _ => "ActiveScheduleAttemptUnverified" };
        return new(caseCode, now, basis, review,
            review.후보목록.SingleOrDefault()?.판정Code == expected
                && (caseCode == "valid" ? review.선정후보?.DriverId == "anchor-driver" : review.선정후보 is null),
            "IsolatedEfInMemory", "SyntheticVehicleRoutePort", false);
    }

    private static 배차큐정책Options AnchorOptions(bool enabled) => new()
    {
        음식배달수행종료기준사용 = enabled, 음식배달동일권최소후보기사수 = 1,
        음식배달후보검색반경Km = 5m, 음식배달후보최대조회수 = 20
    };

    private static 음식배달배차업무정책 AnchorPolicy(I국내화물운송기사상태Store states,
        I음식배달권실행공간Store spaces, I배차추천경로Service routes, I음식권역복귀일정평가Service? plans,
        DateTime? ready, bool enabled = true, 살뜰.Services.Dispatch.Common.I운영배차공통UseCase? receiving = null)
        => new(states, new FakeRejectedRequestStore(), routes, spaces,
            new FakeFoodOrderStore(new 음식주문응답 { 주문번호 = "food-order-1", 조리예상완료시각Utc = ready }),
            Options.Create(AnchorOptions(enabled)), receiving ?? new TestDispatchAvailability(), 음식일정: plans);

    private sealed class AnchorPlanPort(음식후속픽업기준결과 result) : I음식권역복귀일정평가Service
    {
        public int Calls { get; set; }
        public Task<음식권역복귀일정평가결과> 평가Async(string id, 운송원장 candidate, 배차경로좌표 current, DateTime now, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<음식후속픽업기준결과> 후속픽업기준조회Async(string id, 운송원장 candidate, 배차경로좌표 current, DateTime now, CancellationToken ct = default)
        { Calls++; return Task.FromResult(result); }
    }

    private sealed class AnchorEncryptionPort : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }

    private sealed class AnchorRoutePort(bool actual = true, string option = "traavoidcaronly") : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? from, 배차경로좌표? to) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? from, 배차경로좌표? to, string requested, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var distance = from is null || to is null ? 0m : CalculateDistanceKm(from, to)!.Value;
            return Task.FromResult<배차경로예상결과?>(new(distance, TimeSpan.FromMinutes((double)(distance * 2m)), null, "SyntheticDirections5", actual) { RouteOption = option });
        }
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
            => (Math.Abs(source.Latitude - target.Latitude) + Math.Abs(source.Longitude - target.Longitude)) * 100m;
    }
}

public sealed record ArrivalAnchorScenarioEvidence(string ScenarioCode, DateTime EvaluatedAtUtc,
    음식후속픽업기준결과 Basis, 음식배달후보검토결과 Review, bool ExpectedPassed,
    string DatabaseEvidence, string RouteEvidence, bool ActualMapApiCalled);
