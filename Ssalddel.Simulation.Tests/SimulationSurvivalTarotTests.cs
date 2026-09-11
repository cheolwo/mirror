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
public sealed class SimulationSurvivalTarotTests
{
    private const string SafeBuilding = "building:sim:pyeongchang-farm:safe-barn";
    private const string OtherBuilding = "building:sim:pyeongchang-farm:field-shed";
    private const string Container = "container:sim:pyeongchang-farm:safe-barn:food";
    private const string FoodStack = "item-stack:sim:pyeongchang-farm:potato-food";
    private const string FoodItem = "item:sim:pyeongchang-potato-box";
    private const string PlayerA = "player:sim:survivor-a";
    private const string PlayerB = "player:sim:survivor-b";

    [Fact]
    public void 농장식량이2인일이하이면_외부탐색용결정적3장기회를만든다()
    {
        var first = CreateContext(foodQuantity: 3m);
        var second = CreateContext(foodQuantity: 3m);

        var firstState = first.Tarot.Get(first.Session.SessionStableId);
        var secondState = second.Tarot.Get(second.Session.SessionStableId);

        Assert.Equal(1.5m, firstState.CurrentFoodReservePersonDays);
        Assert.Equal(1.5m, firstState.CurrentFarmFoodReservePersonDays);
        Assert.True(firstState.RequiresExternalExpedition);
        Assert.Equal(SimulationSurvivalTarotCodes.ExternalExpeditionRequired,
            firstState.PendingOpportunity!.TriggerCode);
        Assert.True(firstState.PendingOpportunity.RequiresExternalExpedition);
        Assert.Equal(3, firstState.PendingOpportunity.Draw.Offers.Length);
        Assert.Equal(
            firstState.PendingOpportunity.Draw.Offers.Select(value => value.OfferStableId),
            secondState.PendingOpportunity!.Draw.Offers.Select(value => value.OfferStableId));
    }

    [Fact]
    public void 서버사건원장은_외부탐색기회를Unity의미키와공간기준으로내려준다()
    {
        var context = CreateContext(foodQuantity: 3m);

        var projection = context.Events.GetChanges(
            context.Session.SessionStableId, -1);
        var worldEvent = Assert.Single(projection.Events);

        Assert.Equal(0, projection.WorldRevision);
        Assert.Equal(0, projection.NextAfterWorldRevision);
        Assert.Equal(SimulationWorldEventCodes.SurvivalTarotOpportunity,
            worldEvent.EventTypeCode);
        Assert.Equal(SimulationSurvivalTarotCodes.ExternalExpeditionRequired,
            worldEvent.TriggerCode);
        Assert.Equal(SimulationWorldEventCodes.ExternalExpeditionPresentation,
            worldEvent.PresentationKey);
        Assert.Equal(SimulationWorldEventCodes.AwaitingResponse,
            worldEvent.StateCode);
        Assert.Equal(1, worldEvent.EventRevision);
        Assert.Equal(0, worldEvent.LastChangedWorldRevision);
        Assert.Equal(SafeBuilding, worldEvent.ActiveBuildingStableId);
        Assert.Equal("kr5186:l2:438:419", Assert.Single(worldEvent.TileKeys));
        Assert.Equal("region:legal-dong:5176031000",
            Assert.Single(worldEvent.RegionStableIds));
        Assert.Equal(3, worldEvent.Choices.Length);
        Assert.Equal(2, worldEvent.RequiredParticipantCount);
        Assert.True(worldEvent.CanRespond);
        Assert.True(worldEvent.SimulationOnly);
        Assert.False(worldEvent.IsOperationalState);
        Assert.True(worldEvent.PresentationOnly);
    }

    [Fact]
    public void 서버사건원장은_세계개정이후변경만내려주고_합의결과를확정한다()
    {
        var context = CreateContext(foodQuantity: 3m);
        var pending = context.Tarot.Get(context.Session.SessionStableId)
            .PendingOpportunity!;
        var offer = pending.Draw.Offers[0].OfferStableId;

        Assert.Empty(context.Events.GetChanges(
            context.Session.SessionStableId, 0).Events);

        var first = context.Tarot.ConfirmResponse(context.Session.SessionStableId,
            Response("command:world-event:response-a", 0,
                pending.OpportunityStableId, PlayerA, offer));
        var firstEvent = Assert.Single(context.Events.GetChanges(
            context.Session.SessionStableId, 0).Events);
        Assert.Equal(2, firstEvent.EventRevision);
        Assert.Equal(1, firstEvent.LastChangedWorldRevision);
        Assert.Equal(1, firstEvent.RespondedParticipantCount);
        Assert.Empty(context.Events.GetChanges(
            context.Session.SessionStableId, 1).Events);

        var second = context.Tarot.ConfirmResponse(context.Session.SessionStableId,
            Response("command:world-event:response-b", first.AppliedWorldRevision,
                pending.OpportunityStableId, PlayerB, offer));
        var resolved = context.Tarot.ConfirmResolution(context.Session.SessionStableId,
            Resolution("command:world-event:resolve", second.AppliedWorldRevision,
                pending.OpportunityStableId, PlayerA, offer));
        var resolvedEvent = Assert.Single(context.Events.GetChanges(
            context.Session.SessionStableId, second.AppliedWorldRevision).Events);

        Assert.Equal(resolved.AppliedWorldRevision,
            resolvedEvent.LastChangedWorldRevision);
        Assert.Equal(4, resolvedEvent.EventRevision);
        Assert.Equal(SimulationWorldEventCodes.Resolved, resolvedEvent.StateCode);
        Assert.Equal(offer, resolvedEvent.SelectedChoiceStableId);
        Assert.Equal(2, resolvedEvent.RespondedParticipantCount);
        Assert.False(resolvedEvent.CanRespond);
    }

    [Fact]
    public void 월드전체식량이충분해도_농장자급이어려우면외부탐색기회를만든다()
    {
        var request = CreateRequest(3m);
        request.WorldInventory!.Containers = request.WorldInventory.Containers.Concat(
        [
            new SimulationWorldContainerInitialStateRequest
            {
                ContainerStableId = "container:sim:pyeongchang-hub:external-food",
                BuildingStableId = OtherBuilding,
                InteriorSpaceStableId = "interior:sim:pyeongchang-farm:field-shed",
                CapacityUnits = 100m,
            },
        ]).ToArray();
        request.WorldInventory.ItemStacks = request.WorldInventory.ItemStacks.Concat(
        [
            new SimulationWorldItemStackInitialStateRequest
            {
                ItemStackStableId = "item-stack:sim:pyeongchang-hub:external-food",
                ContainerStableId = "container:sim:pyeongchang-hub:external-food",
                ItemCode = FoodItem,
                KoreanName = "농장 밖 보급 식량",
                Quantity = 20m,
                UnitCode = "box",
                BuildingItemRelationStableId =
                    "relation:sim:pyeongchang-hub:external-food",
            },
        ]).ToArray();

        var context = CreateContext(request);
        var state = context.Tarot.Get(context.Session.SessionStableId);

        Assert.Equal(11.5m, state.CurrentFoodReservePersonDays);
        Assert.Equal(1.5m, state.CurrentFarmFoodReservePersonDays);
        Assert.True(state.RequiresExternalExpedition);
        Assert.Equal(SimulationSurvivalTarotCodes.ExternalExpeditionRequired,
            state.PendingOpportunity!.TriggerCode);
    }

    [Fact]
    public void 농장범위를지정하지않은기존Scenario는_일반식량위기를유지한다()
    {
        var request = CreateRequest(3m);
        request.SurvivalTarot!.FarmBuildingStableIds = [];
        var context = CreateContext(request);

        var state = context.Tarot.Get(context.Session.SessionStableId);

        Assert.False(state.FarmScopeConfigured);
        Assert.Equal(SimulationSurvivalTarotCodes.FoodReserveCrisis,
            state.PendingOpportunity!.TriggerCode);
    }

    [Fact]
    public void 전원이안전건물에서같은제안을선택해야_다음Tick보정이활성화된다()
    {
        var context = CreateContext(foodQuantity: 3m);
        var pending = context.Tarot.Get(context.Session.SessionStableId)
            .PendingOpportunity!;
        var offer = pending.Draw.Offers[0].OfferStableId;

        var first = context.Tarot.ConfirmResponse(context.Session.SessionStableId,
            Response("command:survival-tarot:response-a", 0,
                pending.OpportunityStableId, PlayerA, offer));
        var second = context.Tarot.ConfirmResponse(context.Session.SessionStableId,
            Response("command:survival-tarot:response-b", first.AppliedWorldRevision,
                pending.OpportunityStableId, PlayerB, offer));
        var resolved = context.Tarot.ConfirmResolution(context.Session.SessionStableId,
            Resolution("command:survival-tarot:resolve", second.AppliedWorldRevision,
                pending.OpportunityStableId, PlayerA, offer));

        Assert.Null(resolved.State.PendingOpportunity);
        Assert.Empty(resolved.State.ActiveModifierLines);
        Assert.Equal(3m, Assert.Single(context.Inventory.Get(
            context.Session.SessionStableId).ContainerItemStacks).Quantity);

        var nextTick = context.SessionService.Advance(context.Session.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:survival-tarot:tick-1",
                ExpectedRevision = resolved.AppliedWorldRevision,
                TickCount = 1,
            });
        var active = context.Tarot.Get(context.Session.SessionStableId);

        Assert.Equal(1, nextTick.CurrentTick);
        Assert.Equal(2, active.ActiveModifierLines.Length);
        Assert.All(active.ActiveModifierLines, line =>
        {
            Assert.Equal(1, line.ActiveFromTurnNumber);
            Assert.Equal(1, line.ActiveThroughTurnNumber);
        });

        context.SessionService.Advance(context.Session.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:survival-tarot:tick-2",
                ExpectedRevision = nextTick.Revision,
                TickCount = 1,
            });
        Assert.Empty(context.Tarot.Get(context.Session.SessionStableId)
            .ActiveModifierLines);
    }

    [Fact]
    public void 서로다른제안응답은_전원합의확정을차단한다()
    {
        var context = CreateContext(foodQuantity: 3m);
        var pending = context.Tarot.Get(context.Session.SessionStableId)
            .PendingOpportunity!;
        var first = context.Tarot.ConfirmResponse(context.Session.SessionStableId,
            Response("command:survival-tarot:split-a", 0,
                pending.OpportunityStableId, PlayerA,
                pending.Draw.Offers[0].OfferStableId));
        var second = context.Tarot.ConfirmResponse(context.Session.SessionStableId,
            Response("command:survival-tarot:split-b", first.AppliedWorldRevision,
                pending.OpportunityStableId, PlayerB,
                pending.Draw.Offers[1].OfferStableId));

        var error = Assert.Throws<SimulationConflictException>(() =>
            context.Tarot.ConfirmResolution(context.Session.SessionStableId,
                Resolution("command:survival-tarot:split-resolve",
                    second.AppliedWorldRevision, pending.OpportunityStableId,
                    PlayerA, pending.Draw.Offers[0].OfferStableId)));

        Assert.Equal(SimulationSurvivalTarotCodes.UnanimousResponseRequired,
            error.ErrorCode);
    }

    [Fact]
    public void 참여자가서로다른건물에있으면_응답을차단한다()
    {
        var context = CreateContext(foodQuantity: 3m, playerBOutside: true);
        var pending = context.Tarot.Get(context.Session.SessionStableId)
            .PendingOpportunity!;

        var error = Assert.Throws<SimulationConflictException>(() =>
            context.Tarot.ConfirmResponse(context.Session.SessionStableId,
                Response("command:survival-tarot:unsafe", 0,
                    pending.OpportunityStableId, PlayerA,
                    pending.Draw.Offers[0].OfferStableId)));

        Assert.Equal(SimulationSurvivalTarotCodes.ParticipantsNotTogether,
            error.ErrorCode);
    }

    [Fact]
    public void 위기가없으면_3번째Tick에주기기회를하나만만든다()
    {
        var context = CreateContext(foodQuantity: 20m);
        Assert.Null(context.Tarot.Get(context.Session.SessionStableId).PendingOpportunity);

        context.SessionService.Advance(context.Session.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:survival-tarot:periodic-tick",
                ExpectedRevision = 0,
                TickCount = 3,
            });
        var state = context.Tarot.Get(context.Session.SessionStableId);

        Assert.Equal(SimulationSurvivalTarotCodes.Periodic,
            state.PendingOpportunity!.TriggerCode);
        Assert.Single(state.OpportunityHistory);
    }

    [Fact]
    public void 응답과합의는_SaveReplay뒤에도같은카드와보정선을복원한다()
    {
        var saveStore = new InMemorySimulationSessionSaveStore();
        var sourceStore = new InMemory경영SimulationSessionStore();
        var sourceService = new 경영SimulationSessionService(sourceStore, saveStore);
        var session = sourceService.Create(CreateRequest(3m));
        var sourceTarot = new SimulationSurvivalTarotService(sourceStore);
        var pending = sourceTarot.Get(session.SessionStableId).PendingOpportunity!;
        var offer = pending.Draw.Offers[0].OfferStableId;
        var first = sourceTarot.ConfirmResponse(session.SessionStableId,
            Response("command:survival-tarot:save-a", 0,
                pending.OpportunityStableId, PlayerA, offer));
        var second = sourceTarot.ConfirmResponse(session.SessionStableId,
            Response("command:survival-tarot:save-b", first.AppliedWorldRevision,
                pending.OpportunityStableId, PlayerB, offer));
        var resolved = sourceTarot.ConfirmResolution(session.SessionStableId,
            Resolution("command:survival-tarot:save-resolve", second.AppliedWorldRevision,
                pending.OpportunityStableId, PlayerA, offer));
        var saved = sourceService.Save(session.SessionStableId,
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:survival-tarot:consensus",
                ExpectedRevision = resolved.AppliedWorldRevision,
            });

        var targetStore = new InMemory경영SimulationSessionStore();
        var targetService = new 경영SimulationSessionService(targetStore, saveStore);
        var restored = targetService.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = saved.SaveStableId,
        });
        var restoredState = new SimulationSurvivalTarotService(targetStore)
            .Get(restored.Session.SessionStableId);
        var restoredEvent = Assert.Single(
            new SimulationWorldEventProjectionService(targetStore)
                .GetChanges(restored.Session.SessionStableId, -1).Events);

        Assert.Equal(3, restored.ReplayedCommandCount);
        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(offer, Assert.Single(restoredState.OpportunityHistory)
            .SelectedOfferStableId);
        Assert.Equal(2, Assert.Single(restoredState.OpportunityHistory)
            .ModifierLines.Length);
        Assert.Equal("world-event:" + pending.OpportunityStableId,
            restoredEvent.EventStableId);
        Assert.Equal(SimulationWorldEventCodes.Resolved, restoredEvent.StateCode);
        Assert.Equal(offer, restoredEvent.SelectedChoiceStableId);
        Assert.Equal(4, restoredEvent.EventRevision);
    }

    [Fact]
    public async Task HTTP에서도_조회응답전원합의를수직으로확정한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest(3m));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var session = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();

        var state = await client.GetFromJsonAsync<SimulationSurvivalTarotStateSnapshot>(
            "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(session!.SessionStableId) + "/survival-tarot");

        Assert.NotNull(state);
        Assert.True(state.SimulationOnly);
        Assert.False(state.IsOperationalState);
        Assert.NotNull(state.PendingOpportunity);

        var eventRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(session.SessionStableId) + "/world-events";
        var discoveredEvents = await client
            .GetFromJsonAsync<SimulationWorldEventProjectionSnapshot>(
                eventRoute + "?afterWorldRevision=-1");
        var discoveredEvent = Assert.Single(discoveredEvents!.Events);
        Assert.Equal(SimulationWorldEventCodes.ExternalExpeditionPresentation,
            discoveredEvent.PresentationKey);

        var opportunity = state.PendingOpportunity;
        var offer = opportunity.Draw.Offers[0].OfferStableId;
        var baseRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(session.SessionStableId) + "/survival-tarot";
        var firstResponse = await client.PostAsJsonAsync(baseRoute + "/responses/confirm",
            Response("command:survival-tarot:http-a", 0,
                opportunity.OpportunityStableId, PlayerA, offer));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = await firstResponse.Content
            .ReadFromJsonAsync<SimulationSurvivalTarotCommandResultSnapshot>();
        var secondResponse = await client.PostAsJsonAsync(baseRoute + "/responses/confirm",
            Response("command:survival-tarot:http-b", first!.AppliedWorldRevision,
                opportunity.OpportunityStableId, PlayerB, offer));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var second = await secondResponse.Content
            .ReadFromJsonAsync<SimulationSurvivalTarotCommandResultSnapshot>();
        var resolutionResponse = await client.PostAsJsonAsync(
            baseRoute + "/resolutions/confirm",
            Resolution("command:survival-tarot:http-resolve",
                second!.AppliedWorldRevision, opportunity.OpportunityStableId,
                PlayerA, offer));

        Assert.Equal(HttpStatusCode.OK, resolutionResponse.StatusCode);
        var resolved = await resolutionResponse.Content
            .ReadFromJsonAsync<SimulationSurvivalTarotCommandResultSnapshot>();
        Assert.Null(resolved!.State.PendingOpportunity);
        Assert.Equal(2, Assert.Single(resolved.State.OpportunityHistory)
            .ModifierLines.Length);

        var changedEvents = await client
            .GetFromJsonAsync<SimulationWorldEventProjectionSnapshot>(
                eventRoute + "?afterWorldRevision=2");
        var changedEvent = Assert.Single(changedEvents!.Events);
        Assert.Equal(SimulationWorldEventCodes.Resolved, changedEvent.StateCode);
        Assert.Equal(offer, changedEvent.SelectedChoiceStableId);
    }

    private static TestContext CreateContext(decimal foodQuantity, bool playerBOutside = false)
        => CreateContext(CreateRequest(foodQuantity, playerBOutside));

    private static TestContext CreateContext(경영SimulationSession생성Request request)
    {
        var store = new InMemory경영SimulationSessionStore();
        var sessionService = new 경영SimulationSessionService(store,
            new InMemorySimulationSessionSaveStore());
        var session = sessionService.Create(request);
        return new TestContext(sessionService,
            new SimulationSurvivalTarotService(store),
            new SimulationWorldSurvivalInventoryService(store),
            new SimulationWorldEventProjectionService(store),
            session);
    }

    private static 경영SimulationSession생성Request CreateRequest(
        decimal foodQuantity,
        bool playerBOutside = false)
        => new 경영SimulationSession생성Request
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:sim:pyeongchang-farm-hub-survival.v1",
            ScenarioDataRevision = "scenario-data:pyeongchang-farm-hub-survival.r1",
            ScenarioSeed = 20260815,
            RuleRevision = "workflow-rules.v2",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim:survivors-1",
                TerritoryStableId = "territory:sim:pyeongchang-1",
                SettlementStableId = "settlement:sim:daegwallyeong-farm-1",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 15, 0, 0, 0,
                    TimeSpan.Zero),
            },
            WorldInventory = new SimulationWorldInventoryInitialStateRequest
            {
                Buildings =
                [
                    Building(SafeBuilding, "interior:sim:pyeongchang-farm:safe-barn"),
                    Building(OtherBuilding, "interior:sim:pyeongchang-farm:field-shed"),
                ],
                Players =
                [
                    new SimulationWorldPlayerInitialStateRequest
                    {
                        PlayerStableId = PlayerA,
                        CurrentBuildingStableId = SafeBuilding,
                        InventoryCapacityUnits = 20m,
                    },
                    new SimulationWorldPlayerInitialStateRequest
                    {
                        PlayerStableId = PlayerB,
                        CurrentBuildingStableId = playerBOutside
                            ? OtherBuilding : SafeBuilding,
                        InventoryCapacityUnits = 20m,
                    },
                ],
                Containers =
                [
                    new SimulationWorldContainerInitialStateRequest
                    {
                        ContainerStableId = Container,
                        BuildingStableId = SafeBuilding,
                        InteriorSpaceStableId =
                            "interior:sim:pyeongchang-farm:safe-barn",
                        CapacityUnits = 100m,
                    },
                ],
                ItemStacks =
                [
                    new SimulationWorldItemStackInitialStateRequest
                    {
                        ItemStackStableId = FoodStack,
                        ContainerStableId = Container,
                        ItemCode = FoodItem,
                        KoreanName = "대관령 감자 식량 상자",
                        Quantity = foodQuantity,
                        UnitCode = "box",
                        BuildingItemRelationStableId =
                            "relation:sim:pyeongchang-farm:safe-barn-food",
                    },
                ],
            },
            SurvivalTarot = new SimulationSurvivalTarotInitialStateRequest
            {
                PeriodicIntervalTicks = 3,
                FoodCrisisThresholdPersonDays = 2m,
                FarmExitThresholdPersonDays = 2m,
                FoodUnitsPerPlayerDay = 1m,
                FoodItemCodes = [FoodItem],
                FarmBuildingStableIds = [SafeBuilding],
                SafeBuildingStableIds = [SafeBuilding],
                ParticipantPlayerStableIds = [PlayerA, PlayerB],
            },
        };

    private static SimulationWorldBuildingInteriorInitialStateRequest Building(
        string buildingStableId,
        string interiorSpaceStableId)
        => new SimulationWorldBuildingInteriorInitialStateRequest
        {
            BuildingStableId = buildingStableId,
            TileKey = "kr5186:l2:438:419",
            RegionStableId = "region:legal-dong:5176031000",
            BuildingEvidenceKindCode = "ObservedFixture",
            SourceRecordStableId = "fixture:vworld-building:" + buildingStableId,
            InteriorSpaceStableId = interiorSpaceStableId,
        };

    private static SimulationSurvivalTarotResponseConfirmRequest Response(
        string commandId,
        long revision,
        string opportunityStableId,
        string playerStableId,
        string offerStableId)
        => new SimulationSurvivalTarotResponseConfirmRequest
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            OpportunityStableId = opportunityStableId,
            PlayerStableId = playerStableId,
            OfferStableId = offerStableId,
        };

    private static SimulationSurvivalTarotResolutionConfirmRequest Resolution(
        string commandId,
        long revision,
        string opportunityStableId,
        string playerStableId,
        string offerStableId)
        => new SimulationSurvivalTarotResolutionConfirmRequest
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            OpportunityStableId = opportunityStableId,
            PlayerStableId = playerStableId,
            OfferStableId = offerStableId,
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

    private sealed class TestContext
    {
        public TestContext(
            경영SimulationSessionService sessionService,
            SimulationSurvivalTarotService tarot,
            SimulationWorldSurvivalInventoryService inventory,
            SimulationWorldEventProjectionService events,
            경영SimulationSessionSnapshot session)
        {
            SessionService = sessionService;
            Tarot = tarot;
            Inventory = inventory;
            Events = events;
            Session = session;
        }

        public 경영SimulationSessionService SessionService { get; }
        public SimulationSurvivalTarotService Tarot { get; }
        public SimulationWorldSurvivalInventoryService Inventory { get; }
        public SimulationWorldEventProjectionService Events { get; }
        public 경영SimulationSessionSnapshot Session { get; }
    }
}
