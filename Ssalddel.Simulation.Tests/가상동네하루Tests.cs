using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;
using Ssalddel.Unity.Observation;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "가상 동네 하루의 업무·휴식·귀환 결정성과 재고 보존을 검증한다.",
    Boundary = "Simulation 자동 시험이며 실제 운영과 Unity Game View 증거가 아니다.")]
public sealed class 가상동네하루Tests
{
    [Fact]
    public void 하루동안_업무휴식귀가식사수면이_같은주체로이어지고_마감후신규배정은없다()
    {
        var core = Create();
        var ids = core.Snapshot().WaitingFleet!.NeighborhoodLife!.Actors.Select(x => x.ActorId).ToArray();
        var stages = new HashSet<string>();
        var meal = false;
        var latestCount = 0;
        for (var i = 0; i < 1800; i++)
        {
            var prior = core.Snapshot(); var state = Tick(core,i);
            var fleet = state.WaitingFleet!; var life = fleet.NeighborhoodLife!;
            Assert.Equal(ids,life.Actors.Select(x => x.ActorId));
            foreach (var actor in life.Actors.Where(Target)) stages.Add(actor.Stage);
            foreach (var actor in life.Actors.Where(x => x.Role == "Resident" && x.Stage == "Eating"))
            { meal = true; Assert.Contains(state.FoodDeliveries,x => x.OrdererStableId == actor.ActorId && x.ReceivedTick.HasValue); }
            // 기존 유한 물품은 집에서 식사 표시를 하더라도 새로 생기거나 차감되지 않는다.
            for (var item = 0; item < 2; item++)
                Assert.Equal(42,life.Depot.Incoming[item] + life.Depot.Stock[item] + life.Depot.Shipments.Sum(x => x.Cargo[item])
                    + fleet.Mart!.Stock[item] + fleet.Mart.Orders.Count(x => x.PickedUnits > item));
            var count = state.FoodDeliveries.Length + fleet.Mart!.Orders.Length;
            if (state.CurrentTick >= 1050)
            {
                if (state.CurrentTick == 1050) latestCount = count;
                Assert.Equal(latestCount,count);
                Assert.DoesNotContain(fleet.Drivers,x => x.AssignedTick == state.CurrentTick);
            }
            foreach (var actor in life.Actors.Where(x => Target(x) && x.Stage is "GoingHome" or "Eating" or "HomeRest" or "Sleeping"))
            {
                var before = prior.WaitingFleet!.NeighborhoodLife!.Actors.Single(x => x.ActorId == actor.ActorId);
                Assert.True(Math.Sqrt(Math.Pow(actor.X-before.X,2)+Math.Pow(actor.Z-before.Z,2)) <= 1.00001, actor.ActorId+":"+actor.Stage);
                if (actor.Role == "Courier") {
                    var oldDriver = prior.WaitingFleet.Drivers.Single(x => x.Courier.ActorStableId == actor.ActorId).Courier;
                    var driver = fleet.Drivers.Single(x => x.Courier.ActorStableId == actor.ActorId).Courier;
                    Assert.Equal(oldDriver.VehicleX,driver.VehicleX); Assert.Equal(oldDriver.VehicleZ,driver.VehicleZ);
                }
            }
        }
        Assert.True(meal);
        foreach (var stage in new[] { "Working", "Resting", "Finishing", "GoingHome", "Eating", "HomeRest", "Sleeping" }) Assert.Contains(stage,stages);
        var final = core.Snapshot();
        Assert.All(final.WaitingFleet!.NeighborhoodLife!.Actors.Where(Target),actor => {
            Assert.True(actor.Stage is "Sleeping" or "Finishing",actor.ActorId+":"+actor.Stage+":"+actor.Reason);
            if (actor.Stage == "Sleeping") { Assert.Equal(actor.HomeX,actor.X); Assert.Equal(actor.HomeZ,actor.Z); }
        });
        var model = 동네관찰Presenter.생성(final);
        Assert.StartsWith("밤",model.Time);
        Assert.Contains(model.Actors,x => x.Body.Contains("오늘 기록:") && x.Body.Contains("수면"));
    }

    [Theory]
    [InlineData(280)]
    [InlineData(1070)]
    [InlineData(1530)]
    public void 하루중저장재생은_주거이동기록과해시를보존한다(int at)
    {
        var core = Create();
        for(var i=0;i<at;i++) Tick(core,i);
        var package = Save(core);
        Assert.True(package.SessionCreateRequest.NeighborhoodDayEnabled);
        var restored = SimulationSessionReplay.Restore(package);
        for(var i=at;i<at+12;i++) Assert.Equal(Json(Tick(core,i)),Json(Tick(restored,i)));
        Assert.Equal(Save(core).ReplayHash,Save(restored).ReplayHash);
        var copy = core.Snapshot().WaitingFleet!.NeighborhoodLife!;
        var before = Json(core.Snapshot()); copy.Actors[0].DayHistory[0] = "잘못된 외부 변경";
        Assert.Equal(before,Json(core.Snapshot()));
    }

    [Fact]
    public void 주거는안정배정되고_기존저장의누락옵션은종전근무를유지한다()
    {
        var initial = Create().Snapshot().WaitingFleet!.NeighborhoodLife!;
        var second = Create().Snapshot().WaitingFleet!.NeighborhoodLife!;
        Assert.Equal(initial.Actors.Select(x => x.HomeFacilityId),second.Actors.Select(x => x.HomeFacilityId));
        Assert.Equal("facility:synthetic:a",initial.Actors.Single(x => x.ActorId == "participant:synthetic:a").HomeFacilityId);
        var old = new 경영SimulationSessionAggregate(가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid()));
        Tick(old,0); var package = Save(old);
        var text = Json(package).Replace("\"NeighborhoodDayEnabled\":false,","");
        var restored = SimulationSessionReplay.Restore(System.Text.Json.JsonSerializer.Deserialize<SimulationSessionSavePackage>(text)!);
        Assert.False(restored.Snapshot().WaitingFleet!.NeighborhoodLife!.DayEnabled);
        Assert.Equal(package.ReplayHash,Save(restored).ReplayHash);
        Assert.All(restored.Snapshot().WaitingFleet!.NeighborhoodLife!.Actors,x=>Assert.Empty(x.HomeFacilityId));
    }

    [Fact]
    public void 다른프로필에는하루옵션을적용하지않는다()
    {
        var request = 가상배달관찰표본.CreateOrderFlow(Guid.NewGuid()); request.NeighborhoodDayEnabled = true;
        Assert.Throws<SimulationContractException>(()=>new 경영SimulationSessionAggregate(request));
    }

    [Fact]
    public void 점유된출입구에서는귀가를기다리고_해제후같은위치에서재개한다()
    {
        var core=Create();
        core.UpdateNpcPolicy(new SimulationNpcPolicyChangeRequest {CommandId="day:disable-orders",ExpectedRevision=core.Snapshot().Revision,
            PolicyStableId="policy:restaurant:cooking",AutomationEnabled=false});
        for(var i=0;i<1050;i++) Tick(core,i);
        // 외부 점유를 만드는 시험용 준비. 제품 API로 권위 상태를 노출하지 않는다.
        var fleet=(가상배달대기Snapshot)typeof(경영SimulationSessionAggregate).GetField("waitingFleet",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(core)!;
        fleet.NeighborhoodLife!.ResourceOwners["entrance:restaurant"]="actor:test:occupied";
        for(var i=1050;i<1060;i++) Tick(core,i);
        var before=core.Snapshot().WaitingFleet!.NeighborhoodLife!.Actors.Single(x=>x.ActorId==가상동네생활기준.RestaurantActorId);
        Assert.Equal("GoingHome",before.Stage); Assert.Contains("점유",before.Reason);
        for(var i=1060;i<1070;i++) Tick(core,i);
        var blocked=core.Snapshot().WaitingFleet!.NeighborhoodLife!.Actors.Single(x=>x.ActorId==before.ActorId);
        Assert.Equal((before.X,before.Z),(blocked.X,blocked.Z));
        fleet.NeighborhoodLife.ResourceOwners.Remove("entrance:restaurant");
        var resumed=Tick(core,1070).WaitingFleet!.NeighborhoodLife!.Actors.Single(x=>x.ActorId==before.ActorId);
        Assert.NotEqual((blocked.X,blocked.Z),(resumed.X,resumed.Z));
        for(var i=1071;i<1500;i++) Tick(core,i);
        var after=core.Snapshot().WaitingFleet!.NeighborhoodLife!.Actors.Single(x=>x.ActorId==before.ActorId);
        Assert.True(after.Stage is "HomeRest" or "Sleeping",after.Stage+":"+after.Reason+":"+after.HomeRouteStep+":"+Json(fleet.NeighborhoodLife.ResourceOwners));
        Assert.Equal((after.HomeX,after.HomeZ),(after.X,after.Z));
    }

    private static bool Target(가상생활NpcSnapshot x) => x.Role == "Courier" || x.ActorId == 가상동네생활기준.MartWorkerId || x.ActorId == 가상동네생활기준.RestaurantActorId;
    private static 경영SimulationSessionAggregate Create() => new(가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid(),true));
    private static 경영SimulationSessionSnapshot Tick(경영SimulationSessionAggregate core,int i) => core.Advance(new 경영SimulationTick진행Request {
        CommandId="day:tick:"+i,ExpectedRevision=core.Snapshot().Revision,TickCount=1 });
    private static SimulationSessionSavePackage Save(경영SimulationSessionAggregate core) => core.CreateSavePackage(new SimulationSessionSaveRequest {
        SaveStableId="save:day-test",ExpectedRevision=core.Snapshot().Revision });
    private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value);
}
