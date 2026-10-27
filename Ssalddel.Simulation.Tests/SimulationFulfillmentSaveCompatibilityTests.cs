using System.Text.Json;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "선택적 출고와 프랜차이즈 저장 봉인의 결합 및 명령 종류 충돌을 검증한다.",
    Boundary = "가상 세션·JSON 저장/재생 시험이며 운영 재고·공급 계약·Unity 실행 증거가 아니다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3저장재생검증)]
public sealed class SimulationFulfillmentSaveCompatibilityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 공급망과출고의구성순서가달라도_v34는v33을보존하고_JSON복원후부분피킹을이어간다(
        bool franchiseFirst)
    {
        var session = 프랜차이즈공급망SimulationTests.CreateSession();
        프랜차이즈공급망CommandConfirmRequest<프랜차이즈공급망초기화PreviewRequest> initialization;
        if (franchiseFirst)
        {
            initialization = InitializeFranchise(session);
            var v33 = Save(session, "save:compat:franchise-only");
            Assert.Equal(SimulationSaveSchemaVersions.V33, v33.SchemaVersion);
            Assert.Null(v33.Fulfillment);
            var legacyRestored = SimulationSessionReplay.Restore(JsonRoundTrip(v33));
            Assert.Null(legacyRestored.GetFulfillmentState());
            Assert.Equal(v33.ReplayHash, Save(legacyRestored, v33.SaveStableId).ReplayHash);
            ConfigureFulfillment(session);
        }
        else
        {
            ConfigureFulfillment(session);
            initialization = InitializeFranchise(session);
        }

        session.ConfirmFulfillment(new SimulationFulfillmentCommand
        {
            CommandId = "command:compat:reserve",
            ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Reserve,
            Order = 다중센터출고표본.Order(),
        });
        var allocation = Assert.Single(Assert.Single(session.GetFulfillmentState()!.Orders).Allocations);
        var partialPick = PickCommand(session, "command:compat:partial-pick", allocation, 1m);
        session.ConfirmFulfillment(partialPick);
        var expectedFranchise = JsonSerializer.Serialize(session.GetFranchiseSupplyState());
        var expectedFulfillment = SimulationFulfillmentEngine.BuildStateKey(session.GetFulfillmentState());
        var package = Save(session, "save:compat:both");

        Assert.Equal(SimulationSaveSchemaVersions.V34, package.SchemaVersion);
        Assert.Equal(SimulationSaveSchemaVersions.V33, package.FulfillmentBaseSchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(package.FranchiseSupplyBaseSchemaVersion));
        Assert.NotEqual(SimulationSaveSchemaVersions.V34, package.FranchiseSupplyBaseSchemaVersion);
        Assert.NotNull(package.FranchiseSupply);
        Assert.NotNull(package.Snapshot.FranchiseSupply);
        Assert.NotNull(package.Fulfillment);
        Assert.NotNull(package.Snapshot.Fulfillment);
        Assert.Contains(package.CommandLog, entry => entry.FranchiseSupplyState is not null);
        Assert.Equal(3, package.CommandLog.Count(entry =>
            entry.CommandTypeCode == SimulationCommandTypeCodes.FulfillmentConfirm));

        var restored = SimulationSessionReplay.Restore(JsonRoundTrip(package));
        Assert.Equal(session.Revision, restored.Revision);
        Assert.Equal(expectedFranchise, JsonSerializer.Serialize(restored.GetFranchiseSupplyState()));
        Assert.Equal(expectedFulfillment, SimulationFulfillmentEngine.BuildStateKey(restored.GetFulfillmentState()));
        Assert.Equal(package.ReplayHash, Save(restored, package.SaveStableId).ReplayHash);
        var restoredAllocation = Assert.Single(Assert.Single(restored.GetFulfillmentState()!.Orders).Allocations);
        Assert.Equal(1m, restoredAllocation.PickedQuantity);
        var restoredStock = restored.GetFulfillmentState()!.Stocks.Single(stock =>
            stock.StockStableId == restoredAllocation.StockStableId);
        Assert.Equal(9m, restoredStock.OnHandQuantity);
        Assert.Equal(2m, restoredStock.ReservedQuantity);

        // 두 종류의 기존 명령을 재시도해도 새 세션 개정·수량 변경을 만들지 않는다.
        var restoredRevision = restored.Revision;
        restored.ConfirmFulfillment(partialPick);
        restored.ConfirmFranchiseSupplyInitialization(initialization);
        Assert.Equal(restoredRevision, restored.Revision);
        Assert.Equal(package.ReplayHash, Save(restored, package.SaveStableId).ReplayHash);

        var rest = restored.ConfirmFulfillment(PickCommand(restored,
            "command:compat:remaining-pick", restoredAllocation, 2m));
        var finalAllocation = Assert.Single(Assert.Single(rest.Fulfillment!.Orders).Allocations);
        Assert.Equal(3m, finalAllocation.PickedQuantity);
        var finalStock = rest.Fulfillment.Stocks.Single(stock =>
            stock.StockStableId == finalAllocation.StockStableId);
        Assert.Equal(7m, finalStock.OnHandQuantity);
        Assert.Equal(0m, finalStock.ReservedQuantity);
        Assert.Equal(expectedFranchise, JsonSerializer.Serialize(restored.GetFranchiseSupplyState()));

        var finalPackage = Save(restored, "save:compat:both-after-pick");
        var replayed = SimulationSessionReplay.Restore(JsonRoundTrip(finalPackage));
        Assert.Equal(finalPackage.ReplayHash, Save(replayed, finalPackage.SaveStableId).ReplayHash);
        Assert.Equal(SimulationFulfillmentEngine.BuildStateKey(restored.GetFulfillmentState()),
            SimulationFulfillmentEngine.BuildStateKey(replayed.GetFulfillmentState()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 일반결정과출고는_같은명령ID를_양방향과저장복원뒤에도거부한다(
        bool fulfillmentFirst)
    {
        const string sharedId = "command:compat:shared-kind";
        var session = 프랜차이즈공급망SimulationTests.CreateSession();
        ConfigureFulfillment(session);
        if (fulfillmentFirst)
            session.ConfirmFulfillment(ReserveCommand(session, sharedId));
        else
            session.ConfirmDecision(DecisionCommand(session, sharedId));

        AssertKindConflictPreservesState(session, fulfillmentFirst, sharedId);
        var package = Save(session, "save:compat:kind-conflict");
        var restored = SimulationSessionReplay.Restore(JsonRoundTrip(package));
        Assert.Equal(package.ReplayHash, Save(restored, package.SaveStableId).ReplayHash);
        AssertKindConflictPreservesState(restored, fulfillmentFirst, sharedId);
    }

    private static void AssertKindConflictPreservesState(
        경영SimulationSessionAggregate session, bool fulfillmentFirst, string sharedId)
    {
        var before = Save(session, "save:compat:unchanged-after-conflict");
        var error = fulfillmentFirst
            ? Assert.Throws<SimulationConflictException>(() =>
                session.ConfirmDecision(DecisionCommand(session, sharedId)))
            : Assert.Throws<SimulationConflictException>(() =>
                session.ConfirmFulfillment(ReserveCommand(session, sharedId)));
        Assert.Equal("SimulationCommandKindConflict", error.ErrorCode);
        Assert.Equal(before.SavedWorldRevision, session.Revision);
        Assert.Equal(before.ReplayHash, Save(session, before.SaveStableId).ReplayHash);
    }

    private static 프랜차이즈공급망CommandConfirmRequest<프랜차이즈공급망초기화PreviewRequest>
        InitializeFranchise(경영SimulationSessionAggregate session)
    {
        var request = 프랜차이즈공급망SimulationTests.Initialization(session.Revision);
        var preview = session.PreviewFranchiseSupplyInitialization(request);
        Assert.True(preview.CanConfirm);
        var command = new 프랜차이즈공급망CommandConfirmRequest<프랜차이즈공급망초기화PreviewRequest>
        {
            ClientRequestId = "command:compat:franchise-initialize",
            ExpectedPreviewHash = preview.PreviewHash,
            PreviewRequest = request,
        };
        session.ConfirmFranchiseSupplyInitialization(command);
        return command;
    }

    private static void ConfigureFulfillment(경영SimulationSessionAggregate session)
        => session.ConfirmFulfillment(new SimulationFulfillmentCommand
        {
            CommandId = "command:compat:fulfillment-configure",
            ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Configure,
            InitialState = 다중센터출고표본.InitialState(),
        });

    private static SimulationFulfillmentCommand ReserveCommand(
        경영SimulationSessionAggregate session, string commandId)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Reserve,
            Order = 다중센터출고표본.Order(),
        };

    private static SimulationFulfillmentCommand PickCommand(
        경영SimulationSessionAggregate session, string commandId,
        SimulationFulfillmentAllocation allocation, decimal quantity)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Pick,
            OrderStableId = "order:sample:001",
            AllocationStableId = allocation.AllocationStableId,
            LocationBarcode = allocation.LocationBarcode,
            ProductBarcode = allocation.ProductBarcode,
            Quantity = quantity,
        };

    private static SimulationDecisionConfirmRequest DecisionCommand(
        경영SimulationSessionAggregate session, string commandId)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = session.Revision,
            Preview = new SimulationDecisionPreviewRequest
            {
                DecisionStableId = "decision:compat:generic",
                DecisionTypeCode = "CompatibilityDecision",
                ActorStableId = "actor:compat:operator",
                TargetStableIds = new[] { "target:compat:work" },
                SourceStableIds = new[] { "fixture:compat:generic" },
                ExpectedEffects = new[]
                {
                    new SimulationValueProjection
                    {
                        ValueTypeCode = "CompatibilityEffect",
                        TargetLedgerStableId = "ledger:compat:generic",
                        BeforeValue = 0m,
                        Delta = 1m,
                        AfterValue = 1m,
                        UnitCode = "piece",
                        SourceStableIds = new[] { "fixture:compat:generic" },
                    },
                },
                Task = new SimulationTaskPlanRequest
                {
                    TaskStableId = "task:compat:generic",
                    TaskTypeCode = "CompatibilityWork",
                    FacilityStableId = "facility:compat:generic",
                    AssignedCapacity = 1m,
                    AssignedCapacityUnitCode = "piece",
                    DurationTicks = 1,
                    InputLotStableIds = new[] { "lot:compat:generic" },
                    OutputCandidateCodes = new[] { "output:compat:generic" },
                    SourceStableIds = new[] { "fixture:compat:generic" },
                },
            },
        };

    private static SimulationSessionSavePackage Save(
        경영SimulationSessionAggregate session, string saveId)
        => session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saveId,
            ExpectedRevision = session.Revision,
        });

    private static SimulationSessionSavePackage JsonRoundTrip(SimulationSessionSavePackage package)
        => Assert.IsType<SimulationSessionSavePackage>(
            JsonSerializer.Deserialize<SimulationSessionSavePackage>(JsonSerializer.Serialize(package)));
}
