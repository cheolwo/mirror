using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "음식점 관찰 입력과 Presenter를 실제 로컬 Core·저장 경계에 연결해 검증한다.",
    Boundary = ".NET 시험이며 실제 Unity Scene·입력·Game View 증거가 아니다.")]
public sealed class 음식점관찰표본Tests
{
    [Fact]
    public void 사가정첫주문폐루프는_실제사업장과분리된_세가상음식점을고정한다()
    {
        var profiles = 사가정가상음식점Catalog.목록();

        Assert.Equal(new[]
        {
            "가상 사가정 큰길식당",
            "가상 면목 생활길분식",
            "가상 골목안 도시락",
        }, profiles.Select(value => value.DisplayName));
        Assert.Equal(3, profiles.Select(value => value.ProfileStableId).Distinct().Count());
        Assert.Equal(3, profiles.Select(value => value.FacilityStableId).Distinct().Count());
        Assert.Equal(3, profiles.Select(value => value.DisplayRouteStableId).Distinct().Count());
        Assert.All(profiles, value =>
        {
            Assert.Equal(사가정가상음식점Policy.SourceKindCode, value.SourceKindCode);
            Assert.Empty(value.PublicBusinessObservationStableId);
            Assert.Empty(value.ClaimStableId);
            Assert.False(value.DistributionApproved);
            Assert.False(value.RouteApplied);
            Assert.False(value.TraversalReady);
        });
    }

    [Fact]
    public void 사가정세가상음식점은_주문부터수령과복귀까지닫히고_저장재생결정성을보존한다()
    {
        var session = new 경영SimulationSessionAggregate(
            가상배달관찰표본.CreateSagajeongRestaurantOrderFlow(
                Guid.Parse("0f907217-d4b7-41ca-85fc-28c8df4ac975")));

        while (session.CurrentTick < session.DurationTicks)
        {
            session.Advance(new 경영SimulationTick진행Request
            {
                CommandId = "command:sagajeong-three-restaurants:tick:" + session.CurrentTick,
                ExpectedRevision = session.Revision,
                TickCount = 1,
            });
        }

        var completed = session.Snapshot();
        var profiles = 사가정가상음식점Catalog.목록();
        Assert.True(completed.IsCompleted);
        Assert.NotEmpty(completed.FoodDeliveries);
        Assert.Equal(profiles.Select(value => value.FacilityStableId).OrderBy(value => value),
            completed.FoodDeliveries.Select(value => value.RestaurantFacilityStableId)
                .Distinct().OrderBy(value => value));
        Assert.All(completed.FoodDeliveries, order =>
        {
            var profile = profiles.Single(value =>
                value.FacilityStableId == order.RestaurantFacilityStableId);
            Assert.Equal("수령확인", order.StateCode);
            Assert.StartsWith("food-order:synthetic:sagajeong:r1:", order.FoodOrderStableId);
            Assert.NotNull(order.ReceivedTick);
            Assert.Contains(profile.ProfileStableId, order.SourceStableIds);
            Assert.Contains(profile.DisplayRouteStableId, order.SourceStableIds);
            Assert.DoesNotContain(order.SourceStableIds, value =>
                value.Contains("public-business", StringComparison.OrdinalIgnoreCase)
                || value.Contains("claim", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(new[] { "조리중", "픽업대기", "기사배정", "픽업완료", "전달완료", "수령확인" },
                order.StateHistory.Select(value => value.ToStateCode));
        });
        Assert.NotNull(completed.WaitingFleet);
        Assert.Null(completed.WaitingFleet!.Mart);
        Assert.All(completed.WaitingFleet!.Drivers,
            driver => Assert.Equal("Idle", driver.Courier.Stage));

        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:synthetic:sagajeong-three-restaurants:r1",
            ExpectedRevision = session.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var replayed = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(saved.ReplayHash, replayed.ReplayHash);
        Assert.Equal(completed.FoodDeliveries.Select(value =>
                (value.FoodOrderStableId, value.RestaurantFacilityStableId, value.StateCode)),
            restored.Snapshot().FoodDeliveries.Select(value =>
                (value.FoodOrderStableId, value.RestaurantFacilityStableId, value.StateCode)));
    }

    [Fact]
    public void 사가정세음식점은_이동중간저장뒤에도_무중단실행과같은결과로끝난다()
    {
        var request = 가상배달관찰표본.CreateSagajeongRestaurantOrderFlow(
            Guid.Parse("55cb7e1b-e0de-41d5-858b-489f6c2eb69d"));
        var uninterrupted = new 경영SimulationSessionAggregate(request);
        while (uninterrupted.CurrentTick < 100
               && !uninterrupted.Snapshot().WaitingFleet!.Drivers.Any(driver =>
                   driver.Courier.Stage is "DriveRestaurant" or "WaitFood"
                   || driver.PendingOrderId.Length > 0))
        {
            Advance(uninterrupted);
        }

        Assert.Contains(uninterrupted.Snapshot().WaitingFleet!.Drivers, driver =>
            driver.Courier.Stage is "DriveRestaurant" or "WaitFood"
            || driver.PendingOrderId.Length > 0);
        var midSave = uninterrupted.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:synthetic:sagajeong-three-restaurants:mid-route",
            ExpectedRevision = uninterrupted.Revision,
        });
        var restored = SimulationSessionReplay.Restore(midSave);

        while (uninterrupted.CurrentTick < uninterrupted.DurationTicks)
        {
            Advance(uninterrupted);
            Advance(restored);
        }

        var sourceFinal = uninterrupted.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:synthetic:sagajeong-three-restaurants:final",
            ExpectedRevision = uninterrupted.Revision,
        });
        var restoredFinal = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = sourceFinal.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(sourceFinal.ReplayHash, restoredFinal.ReplayHash);

        static void Advance(경영SimulationSessionAggregate aggregate)
            => aggregate.Advance(new 경영SimulationTick진행Request
            {
                CommandId = "command:sagajeong-three-restaurants:continuation:" + aggregate.CurrentTick,
                ExpectedRevision = aggregate.Revision,
                TickCount = 1,
            });
    }

    [Fact]
    public async Task 명시주문과수동Tick만_접수조리상태를바꾸고_저장뒤자동진행하지않는다()
    {
        var path = Path.Combine(Path.GetTempPath(), "restaurant-observer-tests", Guid.NewGuid().ToString("N"));
        using var runtime = Create(path);
        var session = await runtime.Sessions.CreateAsync(음식점관찰표본.Create(Guid.NewGuid()));
        var id = session.SessionStableId;
        var card = new 음식점정책카드Presenter(runtime, id, 음식점관찰표본.FacilityId);
        await card.조회Async();
        Assert.Empty(card.주문목록);
        Assert.Equal(0, card.현재Tick);
        Assert.Equal(1, card.조리자리수);
        for (var i = 0; i < 3; i++)
        {
            await runtime.ConfirmFoodDeliveryAsync(id, 음식점관찰표본.주문("order:" + i, card.판본, "food-order:" + i));
            await card.조회Async();
        }
        Assert.All(card.주문목록, row => Assert.Equal("접수 대기", row.표시상태));
        await Tick();
        Assert.Equal(2, card.주문목록.Count(row => row.표시상태 == "조리 대기"));
        Assert.Single(card.주문목록, row => row.표시상태 == "조리 배정·시작 대기");
        await Tick();
        Assert.Single(card.주문목록, row => row.표시상태 == "조리중");
        await card.적용Async("pause", card.판본, 1, 5, false);
        await Tick();
        Assert.Single(card.주문목록, row => row.표시상태 == "픽업 준비");
        Assert.Equal(2, card.주문목록.Count(row => row.표시상태 == "조리 대기"));
        var saved = await runtime.Sessions.SaveSlotAsync(id, new SimulationLocalSaveSlotRequest
        { SlotStableId = 음식점관찰표본.SlotId, ExpectedRevision = card.판본 });
        using var restored = Create(path);
        var loaded = await restored.Sessions.LoadSlotAsync(음식점관찰표본.SlotId);
        Assert.Equal(saved.SavedWorldTick, loaded.Restore.Session.CurrentTick);
        Assert.Equal(3, loaded.Restore.Session.FoodDeliveries.Length);
        var after = new 음식점정책카드Presenter(restored, loaded.Restore.Session.SessionStableId, 음식점관찰표본.FacilityId);
        await after.조회Async(); await after.조회Async();
        Assert.Equal(card.판본, after.판본);
        Assert.Equal(card.현재Tick, after.현재Tick);
        Assert.Equal(card.주문목록.Select(x => x.표시상태), after.주문목록.Select(x => x.표시상태));
        await after.적용Async("resume", after.판본, 2, 2, true);
        await restored.Sessions.AdvanceWorldTickAsync(loaded.Restore.Session.SessionStableId,
            new 경영SimulationTick진행Request { CommandId = "resume-tick", ExpectedRevision = after.판본, TickCount = 1 });
        await after.조회Async();
        Assert.Equal(2, after.주문목록.Count(x => x.표시상태 == "조리 배정·시작 대기"));
        async Task Tick()
        {
            await runtime.Sessions.AdvanceWorldTickAsync(id, new 경영SimulationTick진행Request
            { CommandId = "tick:" + card.현재Tick, ExpectedRevision = card.판본, TickCount = 1 });
            await card.조회Async();
        }
    }
    private static LocalSimulationRuntime Create(string path) => new(
        new InMemory경영SimulationSessionStore(), new InMemorySimulationSessionSaveStore(), new FileSimulationLocalSaveSlotStore(path));
}
