using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "출고 세션의 개정·멱등 명령·수량 보존과 저장/재생 결정성을 검증한다.",
    Boundary = "가상 세션 자동 시험이며 실제 창고·스캔·Unity 실행 증거가 아니다.")]
public sealed class SimulationFulfillmentSessionTests
{
    [Fact]
    public async Task 기존SoloRuntime의같은세션에서출고명령과재조회를직렬처리한다()
    {
        var slotPath = Path.Combine(Path.GetTempPath(), "fulfillment-slot-" + Guid.NewGuid().ToString("N"));
        using var runtime = new LocalSimulationRuntime(new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(), new FileSimulationLocalSaveSlotStore(slotPath));
        var created = await runtime.Sessions.CreateAsync(다중센터출고표본.Create(Guid.NewGuid()));
        var fulfillment = Assert.IsAssignableFrom<ISimulationFulfillmentRuntime>(runtime);
        await fulfillment.ConfirmFulfillmentAsync(created.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "command:local:configure", ActionCode = SimulationFulfillmentActionCodes.Configure,
            InitialState = 다중센터출고표본.InitialState(), ExpectedRevision = created.Revision,
        });
        var current = await runtime.Sessions.GetAsync(created.SessionStableId);
        Assert.NotNull(current.Fulfillment);
        var plan = await fulfillment.PreviewFulfillmentAsync(created.SessionStableId, 다중센터출고표본.Order());
        Assert.True(plan.IsFeasible);
        await fulfillment.ConfirmFulfillmentAsync(created.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "command:local:reserve", ActionCode = SimulationFulfillmentActionCodes.Reserve,
            Order = 다중센터출고표본.Order(), ExpectedRevision = current.Revision,
        });
        current = await runtime.Sessions.GetAsync(created.SessionStableId);
        Assert.Equal(3m, current.Fulfillment!.Stocks.Sum(stock => stock.ReservedQuantity));
        Assert.Equal(current.Fulfillment.Orders.Length,
            (await fulfillment.GetFulfillmentStateAsync(created.SessionStableId))!.Orders.Length);
        var saved = await runtime.SaveSlotAsync(created.SessionStableId,
            new SimulationLocalSaveSlotRequest { SlotStableId = "fulfillment-sample", ExpectedRevision = current.Revision });
        using var restarted = new LocalSimulationRuntime(new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(), new FileSimulationLocalSaveSlotStore(slotPath));
        var loaded = await restarted.LoadSlotAsync("fulfillment-sample");
        Assert.Equal(saved.ReplayHash, loaded.Restore.ReplayHash);
        Assert.Equal(3m, loaded.Restore.Session.Fulfillment!.Stocks.Sum(stock => stock.ReservedQuantity));
    }

    [Fact]
    public void 계획은무변경이고_센터선반직원예약부터_포장준비까지수량을보존한다()
    {
        var session = Configured();
        var before = session.Snapshot();
        var preview = session.PreviewFulfillment(다중센터출고표본.Order());
        Assert.True(preview.IsFeasible);
        Assert.Equal(다중센터출고표본.ReadyCenter, Assert.Single(preview.SelectedCenterStableIds));
        Assert.Equal(before.Revision, session.Revision);
        Assert.All(session.GetFulfillmentState()!.Stocks, stock => Assert.Equal(0m, stock.ReservedQuantity));

        var reserved = session.ConfirmFulfillment(Command(session, "command:reserve", SimulationFulfillmentActionCodes.Reserve,
            order: 다중센터출고표본.Order()));
        var allocation = Assert.Single(Assert.Single(reserved.Fulfillment!.Orders).Allocations);
        Assert.Equal(3m, reserved.Fulfillment.Stocks.Single(s => s.StockStableId == allocation.StockStableId).ReservedQuantity);
        Assert.False(string.IsNullOrWhiteSpace(allocation.WorkerStableId));
        var pick = Command(session, "command:pick", SimulationFulfillmentActionCodes.Pick, allocation: allocation);
        var picked = session.ConfirmFulfillment(pick);
        var stock = picked.Fulfillment!.Stocks.Single(s => s.StockStableId == allocation.StockStableId);
        Assert.Equal(7m, stock.OnHandQuantity);
        Assert.Equal(0m, stock.ReservedQuantity);
        var ready = session.ConfirmFulfillment(Command(session, "command:pack", SimulationFulfillmentActionCodes.PackReady));
        Assert.Equal(SimulationFulfillmentStateCodes.PackReady, Assert.Single(ready.Fulfillment!.Orders).StateCode);
        Assert.Equal(7m, ready.Fulfillment.Stocks.Single(s => s.StockStableId == allocation.StockStableId).OnHandQuantity);
        Assert.False(ready.IsOperationalState);
    }

    [Fact]
    public void 같은명령재시도는멱등이고_payload또는종류변경과오래된개정은거부한다()
    {
        var session = Configured();
        var command = Command(session, "command:reserve", SimulationFulfillmentActionCodes.Reserve, order: 다중센터출고표본.Order());
        var first = session.ConfirmFulfillment(command);
        Assert.Equal(first.Revision, session.ConfirmFulfillment(command).Revision);
        command.Order!.Lines[0].Quantity++;
        Assert.Throws<SimulationConflictException>(() => session.ConfirmFulfillment(command));
        Assert.Throws<SimulationConflictException>(() => session.Advance(new 경영SimulationTick진행Request
        { CommandId = "command:reserve", ExpectedRevision = session.Revision, TickCount = 1 }));
        var stale = Command(session, "command:stale", SimulationFulfillmentActionCodes.Cancel);
        stale.ExpectedRevision = 0;
        Assert.Throws<SimulationConflictException>(() => session.ConfirmFulfillment(stale));
        Assert.Equal(3m, session.GetFulfillmentState()!.Stocks.Sum(s => s.ReservedQuantity));
    }

    [Fact]
    public void 잘못된위치나상품확인은상태와개정을바꾸지않는다()
    {
        var session = Reserved();
        var before = session.Revision;
        var allocation = Assert.Single(Assert.Single(session.GetFulfillmentState()!.Orders).Allocations);
        var bad = Command(session, "command:bad-location", SimulationFulfillmentActionCodes.Pick, allocation: allocation);
        bad.LocationBarcode = "WRONG";
        Assert.ThrowsAny<InvalidOperationException>(() => session.ConfirmFulfillment(bad));
        bad.CommandId = "command:bad-product"; bad.LocationBarcode = allocation.LocationBarcode; bad.ProductBarcode = "WRONG";
        Assert.ThrowsAny<InvalidOperationException>(() => session.ConfirmFulfillment(bad));
        Assert.Equal(before, session.Revision);
        Assert.Equal(0m, Assert.Single(Assert.Single(session.GetFulfillmentState()!.Orders).Allocations).PickedQuantity);
    }

    [Fact]
    public void 부분피킹저장복원후_나머지만차감하고명령재시도도보존한다()
    {
        var session = Reserved();
        var allocation = Assert.Single(Assert.Single(session.GetFulfillmentState()!.Orders).Allocations);
        var firstPick = Command(session, "command:first-pick", SimulationFulfillmentActionCodes.Pick, allocation: allocation);
        firstPick.Quantity = 1m;
        session.ConfirmFulfillment(firstPick);
        var package = session.CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = "save:fulfillment:partial", ExpectedRevision = session.Revision });
        Assert.Equal(SimulationSaveSchemaVersions.V34, package.SchemaVersion);
        package = System.Text.Json.JsonSerializer.Deserialize<SimulationSessionSavePackage>(
            System.Text.Json.JsonSerializer.Serialize(package))!;
        var restored = SimulationSessionReplay.Restore(package);
        Assert.Equal(SimulationFulfillmentEngine.BuildStateKey(session.GetFulfillmentState()), SimulationFulfillmentEngine.BuildStateKey(restored.GetFulfillmentState()));
        var revision = restored.Revision;
        restored.ConfirmFulfillment(firstPick);
        Assert.Equal(revision, restored.Revision);
        var rest = Command(restored, "command:rest-pick", SimulationFulfillmentActionCodes.Pick, allocation: allocation);
        rest.Quantity = 2m;
        var result = restored.ConfirmFulfillment(rest);
        Assert.Equal(7m, result.Fulfillment!.Stocks.Single(s => s.StockStableId == allocation.StockStableId).OnHandQuantity);
        var roundtrip = restored.CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = "save:fulfillment:again", ExpectedRevision = restored.Revision });
        Assert.Equal(roundtrip.ReplayHash, SimulationSessionReplay.Restore(roundtrip).CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = roundtrip.SaveStableId, ExpectedRevision = restored.Revision }).ReplayHash);
    }

    [Fact]
    public void 저장상태나명령변조와조회결과변경은권위상태로반영하지않는다()
    {
        var session = Reserved();
        var detached = session.GetFulfillmentState()!;
        detached.Stocks[0].OnHandQuantity = 999m;
        Assert.Equal(10m, session.GetFulfillmentState()!.Stocks[0].OnHandQuantity);
        var package = session.CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = "save:fulfillment:tamper", ExpectedRevision = session.Revision });
        package.Fulfillment!.Stocks[0].OnHandQuantity = 999m;
        Assert.ThrowsAny<InvalidOperationException>(() => SimulationSessionReplay.Restore(package));
        package = session.CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = "save:fulfillment:tamper", ExpectedRevision = session.Revision });
        package.CommandLog.Last().FulfillmentCommand!.Order!.Lines[0].Quantity++;
        Assert.ThrowsAny<InvalidOperationException>(() => SimulationSessionReplay.Restore(package));
    }

    [Fact]
    public void 미사용세션의저장판본은유지한다()
    {
        var session = new 경영SimulationSessionAggregate(다중센터출고표본.Create(Guid.NewGuid()));
        var package = session.CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = "save:fulfillment:legacy", ExpectedRevision = 0 });
        Assert.NotEqual(SimulationSaveSchemaVersions.V34, package.SchemaVersion);
        Assert.Null(SimulationSessionReplay.Restore(package).Snapshot().Fulfillment);
    }

    private static 경영SimulationSessionAggregate Configured()
    {
        var session = new 경영SimulationSessionAggregate(다중센터출고표본.Create(Guid.NewGuid()));
        session.ConfirmFulfillment(new SimulationFulfillmentCommand { CommandId = "command:configure", ActionCode = SimulationFulfillmentActionCodes.Configure, InitialState = 다중센터출고표본.InitialState(), ExpectedRevision = 0 });
        return session;
    }
    private static 경영SimulationSessionAggregate Reserved()
    {
        var session = Configured();
        session.ConfirmFulfillment(Command(session, "command:reserve", SimulationFulfillmentActionCodes.Reserve, order: 다중센터출고표본.Order()));
        return session;
    }
    private static SimulationFulfillmentCommand Command(경영SimulationSessionAggregate session, string id, string action,
        SimulationFulfillmentOrderRequest? order = null, SimulationFulfillmentAllocation? allocation = null)
        => new SimulationFulfillmentCommand
        {
            CommandId = id, ExpectedRevision = session.Revision, ActionCode = action,
            Order = order, OrderStableId = "order:sample:001",
            AllocationStableId = allocation?.AllocationStableId ?? string.Empty,
            LocationBarcode = allocation?.LocationBarcode ?? string.Empty,
            ProductBarcode = allocation?.ProductBarcode ?? string.Empty,
            Quantity = allocation?.Quantity ?? 0m,
        };
}
