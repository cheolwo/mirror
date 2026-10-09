using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Theory]
    [InlineData(5, true, "return")]
    [InlineData(11, true, "near")]
    [InlineData(5, false, "near")]
    public async Task 전체일정복귀거리8에서5는가점으로선택되지만11또는미확인은기존순서를유지한다(int after, bool verified, string expected)
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("return", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("return", 37.502m, 127m));
        var returns = new ReturnPort(after);
        var plans = new PlanPort(verified);
        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, returnService: returns, foodPlan: plans).다음후보선정Async(Queue());
        Assert.Equal(expected, result?.DriverId);
        Assert.Equal(verified ? 1 : 0, returns.EvaluationCalls);
        Assert.Equal(verified ? "traavoidcaronly" : null, returns.LastOption);
    }

    [Fact]
    public async Task 복귀OFF는기존순서와가점없는점수를유지한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("return", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("return", 37.502m, 127m));
        var plans = new PlanPort();
        var baseline = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces).다음후보선정Async(Queue());
        var actual = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, returnService: new ReturnPort(5, false), foodPlan: plans).다음후보선정Async(Queue());
        Assert.Equal(baseline, actual); Assert.Equal(0, plans.Calls);
    }

    [Fact]
    public async Task 동일권후보가충분해도접근반경안의인접복귀기사를비교한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("return", 기사앱식별자.FoodDeliveryDriverApp, 37.4995m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("return", 37.4995m, 127m));
        Assert.Equal("near", (await CreatePolicy(states, new FakeRejectedRequestStore(), spaces).다음후보선정Async(Queue()))?.DriverId);
        Assert.Equal("return", (await CreatePolicy(states, new FakeRejectedRequestStore(), spaces,
            returnService: new ReturnPort(5), foodPlan: new PlanPort()).다음후보선정Async(Queue()))?.DriverId);
    }

    [Fact]
    public async Task 늦은GPS와수신OFF는복귀가점으로살리지않는다()
    {
        var now = DateTime.UtcNow;
        var late = Snapshot("return", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0) with { 위치기록시각Utc = now.AddMinutes(-20) };
        var states = new FakeDriverStateStore([Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0), late]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("return", 37.502m, 127m));
        var plans = new PlanPort();
        Assert.Equal("near", (await CreatePolicy(states, new FakeRejectedRequestStore(), spaces,
            returnService: new ReturnPort(5), foodPlan: plans).다음후보선정Async(Queue()))?.DriverId);
        Assert.Equal(0, plans.Calls);
        var receiving = new TestDispatchAvailability();
        await receiving.기사의사변경Async("return", new() { 수신의사Code = 운영배차수신의사Code.Off });
        states.Replace([late with { 위치기록시각Utc = now }, Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0)]);
        Assert.Equal("near", (await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, receiving: receiving,
            returnService: new ReturnPort(5), foodPlan: plans).다음후보선정Async(Queue()))?.DriverId);
        Assert.Equal(0, plans.Calls);
    }

    [Fact]
    public async Task 픽업지연단계는복귀일정평가를하지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("return", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("return", 37.502m, 127m));
        var plans = new PlanPort();
        var queue = Queue();
        var candidate = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, foodOrderStore: new FakeFoodOrderStore(new 음식주문응답
            { 주문번호 = queue.원본의뢰Id, 조리예상완료시각Utc = now.AddMinutes(-20) }), returnService: new ReturnPort(5), foodPlan: plans).다음후보선정Async(queue);
        Assert.NotNull(candidate);
        Assert.Contains("픽업 지연 후보에는 복귀 가점을 적용하지 않습니다.", candidate.추천사유);
        Assert.Equal(0, plans.Calls);
    }

    private sealed class PlanPort(bool verified = true) : I음식권역복귀일정평가Service
    {
        public int Calls { get; private set; }
        public Task<음식권역복귀일정평가결과> 평가Async(string driverId, 운송원장 candidate, 배차경로좌표 current, DateTime nowUtc, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new 음식권역복귀일정평가결과(new(37.5m, 127.08m), new(37.5m, 127.05m), verified, verified, "traavoidcaronly", "전체 일정 미확인")); }
    }
    private sealed class ReturnPort(decimal after, bool enabled = true) : I기사권역복귀Service
    {
        public int EvaluationCalls { get; private set; }
        public string? LastOption { get; private set; }
        public Task<기사권역복귀상태> 상태조회Async(string driverId, 배차경로좌표? current, DateTime? time, decimal? accuracy = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new 기사권역복귀상태(enabled && driverId == "return", enabled && driverId == "return" && time.HasValue && time.Value > DateTime.UtcNow.AddMinutes(-5), 8, new(37.5m, 127m), 1, "설정 상태"));
        public Task<기사권역복귀평가> 평가Async(기사권역복귀상태 state, 배차경로좌표? baselineEnd, 배차경로좌표? candidateEnd,
            bool verified, bool eligible, string? option = null, CancellationToken cancellationToken = default)
        { EvaluationCalls++; LastOption = option; Assert.Equal(127.08m, baselineEnd?.Longitude); Assert.Equal(127.05m, candidateEnd?.Longitude);
            return Task.FromResult(new 기사권역복귀평가(verified && eligible && after < 8 ? 20 : 0, verified, 8, after, $"복귀 8→{after}")); }
    }
}
