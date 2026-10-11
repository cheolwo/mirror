using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Food;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Fact]
    public async Task 수요참여허용후보안에서는기존활동우선권을거리와대기점수보다먼저적용한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near-new", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 1000),
            Snapshot("incumbent", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near-new", 37.5001m, 127m), ("incumbent", 37.502m, 127m));
        var demand = new DemandParticipationPort(DemandResult(
            DemandDriver("incumbent", allowed: true, priority: 1, existing: true),
            DemandDriver("near-new", allowed: true, priority: 2)));

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand)
            .다음후보선정Async(Queue());

        Assert.Equal("incumbent", result?.DriverId);
        Assert.Equal(1, demand.Calls);
    }

    [Fact]
    public async Task 적격참여후보가있으면더가까운수요대기기사는새제안에서제외한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("waiting", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 1000),
            Snapshot("admitted", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("waiting", 37.5001m, 127m), ("admitted", 37.502m, 127m));
        var demand = new DemandParticipationPort(DemandResult(
            DemandDriver("waiting", allowed: false, priority: 1),
            DemandDriver("admitted", allowed: true, priority: 2)));

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand)
            .다음후보선정Async(Queue());

        Assert.Equal("admitted", result?.DriverId);
    }

    [Fact]
    public async Task 같은권역의대기후보가충분해도인접권역의참여허용기사를버리고신규기사로우회하지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("waiting-1", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 1000),
            Snapshot("waiting-2", 기사앱식별자.FoodDeliveryDriverApp, 37.5002m, 127m, now, 1000),
            Snapshot("waiting-3", 기사앱식별자.FoodDeliveryDriverApp, 37.5003m, 127m, now, 1000),
            Snapshot("adjacent-incumbent", 기사앱식별자.FoodDeliveryDriverApp, 37.4995m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("waiting-1", 37.5001m, 127m), ("waiting-2", 37.5002m, 127m),
            ("waiting-3", 37.5003m, 127m), ("adjacent-incumbent", 37.4995m, 127m));
        var demand = new DemandParticipationPort(DemandResult(
            DemandDriver("adjacent-incumbent", true, 1, true),
            DemandDriver("waiting-1", false, 2), DemandDriver("waiting-2", false, 3), DemandDriver("waiting-3", false, 4)));

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand)
            .다음후보선정Async(Queue());

        Assert.NotNull(result);
        Assert.Equal("adjacent-incumbent", result.DriverId);
        Assert.Equal(1, demand.Calls);
        Assert.DoesNotContain("수요 참여 예외", result.추천사유);
    }

    [Fact]
    public async Task 참여허용기사가없어도실제대기주문은기존적격후보로다시탐색한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("waiting", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("waiting", 37.5001m, 127m));
        var demand = new DemandParticipationPort(DemandResult(DemandDriver("waiting", allowed: false, priority: 1)));
        var queue = Queue();

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand)
            .다음후보선정Async(queue);

        Assert.Equal("waiting", result?.DriverId);
        Assert.Contains("수요 참여 예외", result!.추천사유);
        Assert.Null(queue.현재추천대상기사Id);
        Assert.Null(queue.확정기사Id);
    }

    [Theory]
    [InlineData("Off")]
    [InlineData("StaleGps")]
    [InlineData("CargoApp")]
    public async Task 수요참여우선권과긴급재탐색은기존수신GPS앱관문을우회하지않는다(string condition)
    {
        var now = DateTime.UtcNow;
        var protectedDriver = Snapshot("protected", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 1000);
        if (condition == "StaleGps") protectedDriver = protectedDriver with { 위치수신시각Utc = now.AddMinutes(-20) };
        if (condition == "CargoApp") protectedDriver = protectedDriver with { AppKey = 기사앱식별자.CargoYongdalDriverApp };
        var states = new FakeDriverStateStore([
            protectedDriver,
            Snapshot("waiting", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("protected", 37.5001m, 127m), ("waiting", 37.502m, 127m));
        var receiving = new TestDispatchAvailability();
        if (condition == "Off")
            await receiving.기사의사변경Async("protected", new() { 수신의사Code = 운영배차수신의사Code.Off });
        var demand = new DemandParticipationPort(DemandResult(
            DemandDriver("protected", allowed: true, priority: 1, existing: true),
            DemandDriver("waiting", allowed: false, priority: 2)));

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, receiving: receiving, demand: demand)
            .다음후보선정Async(Queue());

        Assert.Equal("waiting", result?.DriverId);
    }

    [Fact]
    public async Task 최대조회수뒤쪽에있는기존기사도참여우선순위를정렬한뒤조회한다()
    {
        var now = DateTime.UtcNow;
        var states = Enumerable.Range(0, 21)
            .Select(i => Snapshot($"a-new-{i:00}", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 1000))
            .Append(Snapshot("z-incumbent", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)).ToArray();
        var spaces = await CreateFoodSpaceStoreAsync(states.Select(x => (x.DriverId, x.Latitude!.Value, x.Longitude!.Value)).ToArray());
        var decisions = states.Select((x, i) => DemandDriver(x.DriverId, allowed: true,
            priority: x.DriverId == "z-incumbent" ? 1 : i + 2, existing: x.DriverId == "z-incumbent")).ToArray();

        var result = await CreatePolicy(new FakeDriverStateStore(states), new FakeRejectedRequestStore(), spaces,
            demand: new DemandParticipationPort(DemandResult(decisions))).다음후보선정Async(Queue());

        Assert.Equal("z-incumbent", result?.DriverId);
    }

    [Fact]
    public async Task 수요참여옵션이꺼져결과가없으면기존후보와점수를그대로유지한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("far", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("far", 37.502m, 127m));
        var baseline = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces).다음후보선정Async(Queue());
        var demand = new DemandParticipationPort(null);

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand)
            .다음후보선정Async(Queue());

        Assert.Equal(baseline, result);
        Assert.Equal(1, demand.Calls);
    }

    [Theory]
    [InlineData(음식배달수요자료상태Code.미확인)]
    [InlineData(음식배달수요자료상태Code.만료)]
    public async Task 미확인이나만료된참여순서는사용하지않고기존적격판정을유지한다(string dataStatus)
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 1000),
            Snapshot("far", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("far", 37.502m, 127m));
        var baseline = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces).다음후보선정Async(Queue());
        var stale = DemandResult(DemandDriver("far", true, 1, true), DemandDriver("near", false, 2));
        stale.데이터상태Code = dataStatus;

        var actual = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces,
            demand: new DemandParticipationPort(stale)).다음후보선정Async(Queue());

        Assert.Equal(baseline, actual);
    }

    [Fact]
    public async Task 현재근무나수행상한으로미적격인기사는수요대기예외재탐색으로살리지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("ineligible", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 1000),
            Snapshot("waiting", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("ineligible", 37.5001m, 127m), ("waiting", 37.502m, 127m));
        var blocked = DemandDriver("ineligible", false, 1, true);
        blocked.상태Code = 음식배달수요참여상태Code.적격조건미충족;
        var demand = new DemandParticipationPort(DemandResult(blocked, DemandDriver("waiting", false, 2)));

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, demand: demand)
            .다음후보선정Async(Queue());

        Assert.NotNull(result);
        Assert.Equal("waiting", result.DriverId);
        Assert.Contains("수요 참여 예외", result.추천사유);
    }

    [Fact]
    public async Task 수요참여평가는권고만반환하며추천확정요금이나공통수신의사를변경하지않는다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([Snapshot("incumbent", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("incumbent", 37.5001m, 127m));
        var receiving = new TestDispatchAvailability();
        var beforeReceiving = await receiving.수신상태조회Async("incumbent");
        var queue = Queue();
        var beforeQueue = System.Text.Json.JsonSerializer.Serialize(queue);
        var demand = new DemandParticipationPort(DemandResult(DemandDriver("incumbent", true, 1, true)));

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, receiving: receiving, demand: demand)
            .다음후보선정Async(queue);

        Assert.NotNull(result);
        Assert.Equal(beforeQueue, System.Text.Json.JsonSerializer.Serialize(queue));
        Assert.Equal(beforeReceiving.수신의사Code, (await receiving.수신상태조회Async("incumbent")).수신의사Code);
        Assert.Same(queue, demand.LastQueue);
        Assert.Equal(1, demand.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 수요우선권은적정픽업창후보보다픽업지연후보를앞세우지않는다(bool allowedNear)
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("near", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("late-incumbent", 기사앱식별자.FoodDeliveryDriverApp, 37.51m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("near", 37.5001m, 127m), ("late-incumbent", 37.51m, 127m));
        var queue = Queue();
        var orderStore = new FakeFoodOrderStore(new 음식주문응답
        {
            주문번호 = queue.원본의뢰Id,
            조리예상완료시각Utc = now.AddMinutes(-4)
        });
        var demand = new DemandParticipationPort(DemandResult(
            DemandDriver("late-incumbent", true, 1, true), DemandDriver("near", allowedNear, 2)));

        var result = await CreatePolicy(states, new FakeRejectedRequestStore(), spaces, foodOrderStore: orderStore, demand: demand)
            .다음후보선정Async(queue);

        Assert.Equal("near", result?.DriverId);
        if (allowedNear) Assert.DoesNotContain("수요 참여 예외", result!.추천사유);
        else Assert.Contains("수요 참여 예외: 픽업 적시성 우선", result!.추천사유);
    }

    private static 음식배달수요참여결과 DemandResult(params 음식배달수요기사판정[] decisions)
        => new()
        {
            정책판본 = "food-demand-participation/v1",
            권역Key = "food-cell:v1:1500:5080",
            판정시각Utc = DateTimeOffset.UtcNow,
            데이터상태Code = 음식배달수요자료상태Code.최신,
            목표활동인원 = decisions.Count(x => x.신규제안허용),
            기사판정목록 = decisions
        };

    private static 음식배달수요기사판정 DemandDriver(string driverId, bool allowed, int priority, bool existing = false)
        => new()
        {
            기사Id = driverId,
            신규제안허용 = allowed,
            기존활동여부 = existing,
            우선순위 = priority,
            상태Code = !allowed ? 음식배달수요참여상태Code.수요대기
                : existing ? 음식배달수요참여상태Code.기존활동보호 : 음식배달수요참여상태Code.추가참여권고,
            사유Code = !allowed ? "DemandWaiting" : existing ? "ExistingActivityProtected" : "AdmissionRecommended"
        };

    private sealed class DemandParticipationPort(음식배달수요참여결과? result) : I음식배달수요참여Service
    {
        public int Calls { get; private set; }
        public 운송원장? LastQueue { get; private set; }
        public Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken token = default)
        {
            Calls++;
            LastQueue = queue;
            return Task.FromResult(result);
        }
    }
}
