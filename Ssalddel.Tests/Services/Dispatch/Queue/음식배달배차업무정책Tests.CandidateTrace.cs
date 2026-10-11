using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Fact]
    public async Task 후보검토는기존선정결과를반환하고원장과수신의사를변경하지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m));
        var receiving = new TestDispatchAvailability();
        var policy = CreatePolicy(states, new FakeRejectedRequestStore(), spaces, receiving: receiving);
        var queue = Queue();
        var before = JsonSerializer.Serialize(queue);
        var original = await policy.다음후보선정Async(queue);

        var review = await policy.후보검토Async(queue);

        Assert.Equal(original, review.선정후보);
        Assert.Equal(before, JsonSerializer.Serialize(queue));
        Assert.Equal(운영배차수신의사Code.On, (await receiving.수신상태조회Async("near")).수신의사Code);
        Assert.Equal(new[] { "near" }, review.최종허용기사Ids);
        Assert.Equal("CandidateAllowed", Assert.Single(review.후보목록).판정Code);
        Assert.False(review.수요자료최신);
        Assert.Null(review.중단사유Code);
    }

    [Fact]
    public async Task 같은권후보가충분하면조회하지않은인접기사를검토했다고표시하지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("unread", 기사앱식별자.FoodDeliveryDriverApp, 37.4995m, 127m, now, 999)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("unread", 37.4995m, 127m));

        var review = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces).후보검토Async(Queue());

        Assert.Equal(new[] { review.주배달권키 }, review.조회배달권키목록);
        Assert.Equal("near", review.선정후보?.DriverId);
        Assert.DoesNotContain(review.후보목록, x => x.기사Id == "unread");
    }

    [Fact]
    public async Task 동일권후보가없으면실제조회한인접셀과선정단계를기록한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("adjacent", 기사앱식별자.FoodDeliveryDriverApp, 37.4995m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("adjacent", 37.4995m, 127m));

        var review = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces).후보검토Async(Queue());

        Assert.Equal("adjacent", review.선정후보?.DriverId);
        Assert.Equal(9, review.조회배달권키목록.Count);
        Assert.Equal(1, Assert.Single(review.후보목록).공간단계);
        Assert.Equal(음식배달권정책.인접배달권키조회(review.주배달권키), review.조회배달권키목록.Skip(1));
    }

    [Theory]
    [InlineData("off", "DriverReceivingOff")]
    [InlineData("app", "DriverAppMismatch")]
    [InlineData("stopped", "DriverNotDriving")]
    [InlineData("gps_missing", "DriverLocationMissing")]
    [InlineData("gps_stale", "DriverLocationStale")]
    [InlineData("rejected", "DriverRejected")]
    [InlineData("excluded", "DriverExcluded")]
    [InlineData("radius", "OutsideApproachRadius")]
    [InlineData("state_missing", "DriverStateMissing")]
    public async Task 후보관문탈락은최초확인사유만남기고최종허용집합에포함하지않는다(string condition, string expected)
    {
        var now = DateTime.UtcNow;
        var driver = Snapshot("blocked", 기사앱식별자.FoodDeliveryDriverApp, 37.52m, 127m, now, 999,
            allowedRadiusKm: condition == "radius" ? 1m : null);
        if (condition == "app") driver = driver with { AppKey = 기사앱식별자.CargoYongdalDriverApp };
        if (condition == "stopped") driver = driver with { 운행상태 = "운행종료" };
        if (condition == "gps_missing") driver = driver with { Latitude = null };
        if (condition == "gps_stale") driver = driver with { 위치수신시각Utc = now.AddMinutes(-20) };
        var states = new FakeDriverStateStore(condition == "state_missing" ? [] : [driver]);
        var spaces = await CreateFoodSpaceStoreAsync(("blocked", 37.52m, 127m));
        var receiving = new TestDispatchAvailability();
        if (condition == "off") await receiving.기사의사변경Async("blocked", new() { 수신의사Code = 운영배차수신의사Code.Off });
        var rejected = new FakeRejectedRequestStore();
        if (condition == "rejected") await rejected.RejectAsync("blocked", Queue().의뢰Id);

        var review = await CreatePolicy(states, rejected, spaces, receiving: receiving)
            .후보검토Async(Queue(), condition == "excluded" ? "blocked" : null);

        Assert.Null(review.선정후보);
        Assert.Empty(review.최종허용기사Ids);
        Assert.Equal("NoEligibleCandidate", review.중단사유Code);
        var item = Assert.Single(review.후보목록);
        Assert.Equal(expected, item.판정Code);
        Assert.Null(item.픽업단계);
        Assert.Null(item.추천점수);
        Assert.Equal(condition == "radius" ? 2.22m : (decimal?)null, item.거리Km);
    }

    [Fact]
    public async Task 수요부적격으로상태를읽지않은기사는거리나대기점수를확인한것으로표시하지않는다()
    {
        var now = DateTime.UtcNow;
        var spaces = await CreateFoodSpaceStoreAsync(("ineligible", 37.5001m, 127m), ("allowed", 37.502m, 127m));
        var blocked = DemandDriver("ineligible", false, 1);
        blocked.상태Code = 음식배달수요참여상태Code.적격조건미충족;
        var demand = new DemandParticipationPort(DemandResult(blocked, DemandDriver("allowed", true, 2)));
        var states = new FakeDriverStateStore([Snapshot("allowed", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);

        var review = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand).후보검토Async(Queue());

        var item = Assert.Single(review.후보목록, x => x.기사Id == "ineligible");
        Assert.Equal("DemandIneligible", item.판정Code);
        Assert.Null(item.거리Km);
        Assert.Null(item.대기점수);
        Assert.Null(item.픽업단계);
        Assert.Equal(1, item.수요우선순위);
        Assert.Equal(new[] { "allowed" }, review.최종허용기사Ids);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 적시성과수요허용집합은실제최종평가목록과일치한다(bool nearAdmitted)
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("late", 기사앱식별자.FoodDeliveryDriverApp, 37.51m, 127m, now, 999)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("late", 37.51m, 127m));
        var demand = new DemandParticipationPort(DemandResult(DemandDriver("late", true, 1, true), DemandDriver("near", nearAdmitted, 2)));
        var queue = Queue();
        var orders = new FakeFoodOrderStore(new 음식주문응답 { 주문번호 = queue.원본의뢰Id, 조리예상완료시각Utc = now.AddMinutes(-4) });

        var review = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, foodOrderStore: orders, demand: demand).후보검토Async(queue);

        Assert.Equal("near", review.선정후보?.DriverId);
        Assert.Equal(new[] { "near" }, review.최종허용기사Ids);
        Assert.Equal("PickupTimelinessExcluded", Assert.Single(review.후보목록, x => x.기사Id == "late").판정Code);
        Assert.Equal(2, Assert.Single(review.후보목록, x => x.기사Id == "late").픽업단계);
        Assert.Equal(!nearAdmitted, review.수요예외사용);
        Assert.Equal(!nearAdmitted, review.픽업적시성예외사용);
    }

    [Fact]
    public async Task 동일한픽업단계에서수요허용후보가있으면대기후보는최종선택불가로기록한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("waiting", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 999),
            Snapshot("admitted", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("waiting", 37.5001m, 127m), ("admitted", 37.502m, 127m));
        var demand = new DemandParticipationPort(DemandResult(DemandDriver("waiting", false, 1), DemandDriver("admitted", true, 2)));

        var review = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand).후보검토Async(Queue());

        Assert.Equal("DemandParticipationExcluded", Assert.Single(review.후보목록, x => x.기사Id == "waiting").판정Code);
        Assert.Equal(new[] { "admitted" }, review.최종허용기사Ids);
        Assert.False(review.수요예외사용);
    }

    [Fact]
    public async Task 인접권을평가해도포함조건이없으면최종허용으로승격하지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("return", 기사앱식별자.FoodDeliveryDriverApp, 37.4995m, 127m, now, 999)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("return", 37.4995m, 127m));

        var review = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces,
            returnService: new ReturnPort(5, enabled: false), foodPlan: new PlanPort()).후보검토Async(Queue());

        Assert.Equal("near", review.선정후보?.DriverId);
        Assert.Equal("AdjacentScopeNotIncluded", Assert.Single(review.후보목록, x => x.기사Id == "return").판정Code);
        Assert.Equal(new[] { "near" }, review.최종허용기사Ids);
    }

    [Fact]
    public async Task 조회상한뒤의기사를실제확인한후보로만들지않는다()
    {
        var now = DateTime.UtcNow;
        var drivers = Enumerable.Range(0, 21).Select(i => Snapshot($"driver-{i:00}", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, i)).ToArray();
        var spaces = await CreateFoodSpaceStoreAsync(drivers.Select(x => (x.DriverId, x.Latitude!.Value, x.Longitude!.Value)).ToArray());

        var review = await CreatePolicy(new FakeDriverStateStore(drivers), new FakeRejectedRequestStore(), spaces).후보검토Async(Queue());

        Assert.Equal(20, review.후보목록.Count);
        Assert.Equal(20, review.최종허용기사Ids.Count);
        Assert.DoesNotContain(review.후보목록, x => x.기사Id == "driver-20");
    }

    [Fact]
    public async Task 좌표누락이나합의요금실패는공간이나후보를확인했다고표시하지않는다()
    {
        var states = new FakeDriverStateStore([]);
        var queue = Queue();
        queue.픽업_위도 = null;
        var missing = await CreatePolicy(states, new FakeRejectedRequestStore()).후보검토Async(queue);
        Assert.Equal("PickupCoordinatesMissing", missing.중단사유Code);
        Assert.Empty(missing.조회배달권키목록);
        Assert.Empty(missing.후보목록);

        var unavailable = await CreatePolicy(states, new FakeRejectedRequestStore(), agreed: new Agreement("driver", unavailable: true)).후보검토Async(Queue());
        Assert.Equal("AgreedRateUnavailable", unavailable.중단사유Code);
        Assert.Empty(unavailable.조회배달권키목록);
        Assert.Empty(unavailable.후보목록);
    }

    [Fact]
    public async Task 복귀가점이포함된점수는최종선정후보와같이기록한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("return", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("return", 37.502m, 127m));

        var review = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces,
            returnService: new ReturnPort(5), foodPlan: new PlanPort()).후보검토Async(Queue());

        Assert.Equal(review.선정후보?.추천점수, Assert.Single(review.후보목록).추천점수);
        Assert.Equal(117.336m, review.선정후보?.추천점수);
    }
}
