using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationWorldSurvivalInventoryTests
{
    private const string Building = PyeongchangWorldExplorationFixtureIds.Barn;
    private const string Interior = "interior:sim:pyeongchang-farm:barn-a";
    private const string Container = "container:sim:pyeongchang-farm:barn-a:pallet-1";
    private const string Stack = "item-stack:sim:pyeongchang-farm:potato-boxes-1";
    private const string PlayerA = "player:sim:survival-a";
    private const string PlayerB = "player:sim:survival-b";

    [Fact]
    public void 출처가있는건물근거와_Scenario내부재고를분리해초기화한다()
    {
        var context = CreateContext();
        var inventory = context.Inventory.Get(context.Session.SessionStableId);

        var building = Assert.Single(inventory.Buildings);
        Assert.Equal("ObservedFixture", building.BuildingEvidenceKindCode);
        Assert.Equal(SimulationWorldSurvivalInventoryCodes.SimulationScenario,
            building.InteriorEvidenceKindCode);
        Assert.Equal("fixture:vworld-building:51760:sample-warehouse-1",
            building.SourceRecordStableId);
        Assert.Equal(3m, Assert.Single(inventory.ContainerItemStacks).Quantity);
        Assert.False(inventory.IsOperationalState);
        Assert.True(inventory.SimulationOnly);
    }

    [Fact]
    public void Preview는_획득가능수량을계산하지만_재고와개정을바꾸지않는다()
    {
        var context = CreateContext();
        var preview = context.Inventory.PreviewAcquisition(context.Session.SessionStableId,
            Preview(PlayerA, context.Session.Revision));
        var after = context.Inventory.Get(context.Session.SessionStableId);

        Assert.True(preview.CanConfirm);
        Assert.Equal(3m, preview.ContainerQuantityBefore);
        Assert.Equal(2m, preview.ContainerQuantityAfter);
        Assert.Equal(0m, preview.PlayerQuantityBefore);
        Assert.Equal(1m, preview.PlayerQuantityAfter);
        Assert.False(preview.StateChanged);
        Assert.Equal(0, after.WorldRevision);
        Assert.Equal(3m, Assert.Single(after.ContainerItemStacks).Quantity);
        Assert.Empty(Assert.Single(after.Players, value => value.PlayerStableId == PlayerA).Items);
    }

    [Fact]
    public void 한플레이어가획득하면_다른플레이어의재조회에도같은재고가보인다()
    {
        var context = CreateContext();
        var result = context.Inventory.ConfirmAcquisition(context.Session.SessionStableId,
            Confirm("command:world-item-acquire:1", PlayerA, context.Session.Revision));
        var observedByPlayerB = context.Inventory.Get(context.Session.SessionStableId);
        var canonicalSession = context.SessionService.Get(context.Session.SessionStableId);

        Assert.Equal(1, result.AppliedWorldRevision);
        Assert.Equal(2m, Assert.Single(observedByPlayerB.ContainerItemStacks).Quantity);
        Assert.Equal(1m, Assert.Single(
            Assert.Single(observedByPlayerB.Players,
                value => value.PlayerStableId == PlayerA).Items).Quantity);
        Assert.Empty(Assert.Single(observedByPlayerB.Players,
            value => value.PlayerStableId == PlayerB).Items);
        Assert.Single(observedByPlayerB.Transfers);
        Assert.Equal(observedByPlayerB.WorldRevision, canonicalSession.Revision);
        Assert.Equal(observedByPlayerB.WorldRevision,
            canonicalSession.WorldContext.WorldRevision);
    }

    [Fact]
    public void 같은명령재시도는_재고를두번감소시키지않는다()
    {
        var context = CreateContext();
        var request = Confirm("command:world-item-acquire:idempotent", PlayerA,
            context.Session.Revision);

        var first = context.Inventory.ConfirmAcquisition(context.Session.SessionStableId, request);
        var repeated = context.Inventory.ConfirmAcquisition(context.Session.SessionStableId, request);

        Assert.Equal(first.Transfer.TransferStableId, repeated.Transfer.TransferStableId);
        Assert.Equal(1, repeated.AppliedWorldRevision);
        Assert.Equal(2m, Assert.Single(repeated.Inventory.ContainerItemStacks).Quantity);
        Assert.Single(repeated.Inventory.Transfers);
    }

    [Fact]
    public void 두플레이어가같은개정으로획득하면_먼저확정된한명만성공한다()
    {
        var context = CreateContext();
        var observedRevision = context.Session.Revision;
        context.Inventory.ConfirmAcquisition(context.Session.SessionStableId,
            Confirm("command:world-item-acquire:first", PlayerA, observedRevision));

        var error = Assert.Throws<SimulationConflictException>(() =>
            context.Inventory.ConfirmAcquisition(context.Session.SessionStableId,
                Confirm("command:world-item-acquire:stale", PlayerB, observedRevision)));
        var latest = context.Inventory.Get(context.Session.SessionStableId);

        Assert.Equal(SimulationWorldSurvivalInventoryCodes.ExpectedRevisionMismatch,
            error.ErrorCode);
        Assert.Equal(2m, Assert.Single(latest.ContainerItemStacks).Quantity);
        Assert.Empty(Assert.Single(latest.Players,
            value => value.PlayerStableId == PlayerB).Items);
    }

    [Fact]
    public void 관리전용컨테이너는_관리관계가없는플레이어의획득을차단한다()
    {
        var context = CreateContext(SimulationWorldSurvivalInventoryCodes.ManagerOnly);
        var preview = context.Inventory.PreviewAcquisition(context.Session.SessionStableId,
            Preview(PlayerB, context.Session.Revision));

        Assert.False(preview.CanConfirm);
        Assert.Contains(SimulationWorldSurvivalInventoryCodes.ContainerAccessDenied,
            preview.BlockReasonCodes);
    }

    [Fact]
    public void 운영재고라고표시한초기상태는_SimulationSession에들어올수없다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var service = new 경영SimulationSessionService(store,
            new InMemorySimulationSessionSaveStore());
        var request = CreateRequest();
        request.WorldInventory!.IsOperationalInventory = true;

        var error = Assert.Throws<SimulationContractException>(() => service.Create(request));

        Assert.Equal(SimulationWorldSurvivalInventoryCodes.OperationalInventoryForbidden,
            error.ErrorCode);
    }

    [Fact]
    public void 획득상태도_SaveReplay에서같은재고로복원한다()
    {
        var context = CreateContext();
        var acquired = context.Inventory.ConfirmAcquisition(context.Session.SessionStableId,
            Confirm("command:world-item-acquire:save-guard", PlayerA,
                context.Session.Revision));

        var saved = context.SessionService.Save(context.Session.SessionStableId,
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:world-item-acquire:replayable",
                ExpectedRevision = acquired.AppliedWorldRevision,
            });
        var restored = SimulationSessionReplay.Restore(saved);
        var restoredInventory = restored.GetWorldInventory();

        Assert.Equal(2m, Assert.Single(restoredInventory.ContainerItemStacks).Quantity);
        Assert.Equal(1m, Assert.Single(Assert.Single(restoredInventory.Players,
            value => value.PlayerStableId == PlayerA).Items).Quantity);
        Assert.Single(restoredInventory.Transfers);
    }

    [Fact]
    public async Task HTTP에서도_Preview와Confirm뒤최신공유상태를다시조회할수있다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateRequest();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var session = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.NotNull(session);

        var sessionId = Uri.EscapeDataString(session.SessionStableId);
        var confirmResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/" + sessionId
            + "/world-inventory/item-acquisitions/confirm",
            Confirm("command:world-item-acquire:http", PlayerA, session.Revision));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var inventory = await client.GetFromJsonAsync<SimulationWorldInventorySnapshot>(
            "/api/simulation/v1/sessions/" + sessionId + "/world-inventory");
        Assert.NotNull(inventory);
        Assert.Equal(1, inventory.WorldRevision);
        Assert.Equal(2m, Assert.Single(inventory.ContainerItemStacks).Quantity);
    }

    private static TestContext CreateContext(
        string accessPolicy = SimulationWorldSurvivalInventoryCodes.PublicAcquisition)
    {
        var store = new InMemory경영SimulationSessionStore();
        var sessionService = new 경영SimulationSessionService(store,
            new InMemorySimulationSessionSaveStore());
        var session = sessionService.Create(CreateRequest(accessPolicy));
        return new TestContext(sessionService,
            new SimulationWorldSurvivalInventoryService(store), session);
    }

    private static 경영SimulationSession생성Request CreateRequest(
        string accessPolicy = SimulationWorldSurvivalInventoryCodes.PublicAcquisition)
        => new 경영SimulationSession생성Request
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:sim:pyeongchang-survival-management.v1",
            ScenarioDataRevision = "scenario-data:pyeongchang-survival-management.r1",
            ScenarioSeed = 20260814,
            RuleRevision = "workflow-rules.v2",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim:survivors-1",
                TerritoryStableId = "territory:sim:pyeongchang-1",
                SettlementStableId = "settlement:sim:daegwallyeong-farm-1",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 14, 0, 0, 0,
                    TimeSpan.Zero),
            },
            WorldInventory = new SimulationWorldInventoryInitialStateRequest
            {
                Buildings =
                [
                    new SimulationWorldBuildingInteriorInitialStateRequest
                    {
                        BuildingStableId = Building,
                        TileKey = PyeongchangWorldExplorationFixtureIds.DaegwallyeongFarmCenterTile,
                        RegionStableId = SimulationWorldExplorationCodes.DaegwallyeongRegion,
                        BuildingEvidenceKindCode = "ObservedFixture",
                        SourceRecordStableId =
                            "fixture:vworld-building:51760:sample-warehouse-1",
                        InteriorSpaceStableId = Interior,
                        InteriorEvidenceKindCode =
                            SimulationWorldSurvivalInventoryCodes.SimulationScenario,
                    },
                ],
                Players =
                [
                    new SimulationWorldPlayerInitialStateRequest
                    {
                        PlayerStableId = PlayerA,
                        CurrentBuildingStableId = Building,
                        InventoryCapacityUnits = 10m,
                        ManagedContainerStableIds = [Container],
                    },
                    new SimulationWorldPlayerInitialStateRequest
                    {
                        PlayerStableId = PlayerB,
                        CurrentBuildingStableId = Building,
                        InventoryCapacityUnits = 10m,
                    },
                ],
                Containers =
                [
                    new SimulationWorldContainerInitialStateRequest
                    {
                        ContainerStableId = Container,
                        BuildingStableId = Building,
                        InteriorSpaceStableId = Interior,
                        AccessPolicyCode = accessPolicy,
                        CapacityUnits = 10m,
                        ManagerPlayerStableIds = [PlayerA],
                    },
                ],
                ItemStacks =
                [
                    new SimulationWorldItemStackInitialStateRequest
                    {
                        ItemStackStableId = Stack,
                        ContainerStableId = Container,
                        ItemCode = PyeongchangWorldExplorationFixtureIds.PotatoSample,
                        KoreanName = "대관령 감자 상자",
                        Quantity = 3m,
                        UnitCode = "box",
                        BuildingItemRelationStableId =
                            "relation:sim:pyeongchang-farm:barn-potato-sample",
                    },
                ],
                IsOperationalInventory = false,
            },
        };

    private static SimulationWorldItemAcquisitionPreviewRequest Preview(
        string playerStableId, long revision)
        => new SimulationWorldItemAcquisitionPreviewRequest
        {
            ObservedWorldRevision = revision,
            PlayerStableId = playerStableId,
            BuildingStableId = Building,
            ContainerStableId = Container,
            ItemStackStableId = Stack,
            Quantity = 1m,
        };

    private static SimulationWorldItemAcquisitionConfirmRequest Confirm(
        string commandId, string playerStableId, long revision)
        => new SimulationWorldItemAcquisitionConfirmRequest
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            PlayerStableId = playerStableId,
            BuildingStableId = Building,
            ContainerStableId = Container,
            ItemStackStableId = Stack,
            Quantity = 1m,
        };

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SsalddelExecution:Mode"] = "Operational",
                        ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                        ["SimulationSharedPublicData:Enabled"] = "false",
                    });
                });
            });

    private sealed record TestContext(
        경영SimulationSessionService SessionService,
        SimulationWorldSurvivalInventoryService Inventory,
        경영SimulationSessionSnapshot Session);
}
