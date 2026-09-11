using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationFarmSurvivalTests
{
    private const string Player = "actor:sim:player-survivor";
    private const string Npc = "actor:sim:farm-worker";
    private const string SoilA = "soil-tile:sim:daegwallyeong:0:0";
    private const string SoilB = "soil-tile:sim:daegwallyeong:0:1";

    [Fact]
    public void 플레이어직접노동과Npc위임은_같은농장원장에서다른비용으로진행된다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());

        var playerPreview = session.PreviewFarmWork(Preview(
            0, Player, SoilA, SimulationFarmSurvivalCodes.PlayerDirect));
        Assert.True(playerPreview.CanConfirm);
        Assert.Equal(0m, playerPreview.RequiredLabor);
        Assert.Equal(1, playerPreview.DurationTicks);

        var playerState = session.ConfirmFarmWork(Confirm(
            "command:farm-work:player-till", 0, Player, SoilA,
            SimulationFarmSurvivalCodes.PlayerDirect));
        var npcPreview = session.PreviewFarmWork(Preview(
            playerState.WorldRevision, Npc, SoilB,
            SimulationFarmSurvivalCodes.NpcDelegated));
        Assert.True(npcPreview.CanConfirm);
        Assert.Equal(3m, npcPreview.RequiredLabor);
        Assert.Equal(2, npcPreview.DurationTicks);

        var npcState = session.ConfirmFarmWork(Confirm(
            "command:farm-work:npc-till", playerState.WorldRevision,
            Npc, SoilB, SimulationFarmSurvivalCodes.NpcDelegated));
        Assert.Equal(3m, session.Snapshot().Settlement!.LaborReserved);
        Assert.All(npcState.WorkOrders,
            value => Assert.Equal(SimulationFarmSurvivalCodes.InProgress,
                value.StatusCode));

        var tickOne = session.Advance(Tick("command:tick:day-2",
            npcState.WorldRevision));
        Assert.Equal(SimulationFarmSurvivalCodes.Tilled,
            tickOne.FarmSurvival!.SoilTiles.Single(value =>
                value.SoilTileStableId == SoilA).StateCode);
        Assert.Equal(SimulationFarmSurvivalCodes.Untilled,
            tickOne.FarmSurvival.SoilTiles.Single(value =>
                value.SoilTileStableId == SoilB).StateCode);

        var tickTwo = session.Advance(Tick("command:tick:day-3", tickOne.Revision));
        Assert.Equal(SimulationFarmSurvivalCodes.Tilled,
            tickTwo.FarmSurvival!.SoilTiles.Single(value =>
                value.SoilTileStableId == SoilB).StateCode);
        Assert.Equal(0m, tickTwo.Settlement!.LaborReserved);
        Assert.Equal(85m, tickTwo.FarmSurvival.Actors.Single(value =>
            value.ActorStableId == Player).Stamina);
    }

    [Fact]
    public void 오늘작업계획은_여러작업을한개개정으로원자확정하고_SaveReplay한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var items = new[]
        {
            PlanItem("plan-item:today:player", 10, Player, SoilA,
                SimulationFarmSurvivalCodes.PlayerDirect),
            PlanItem("plan-item:today:npc", 20, Npc, SoilB,
                SimulationFarmSurvivalCodes.NpcDelegated),
        };

        var preview = session.PreviewFarmWorkPlan(new SimulationFarmWorkPlanPreviewRequest
        {
            ExpectedRevision = 0,
            Items = items,
        });

        Assert.True(preview.CanConfirm);
        Assert.Equal(3m, preview.TotalRequiredLabor);
        Assert.Equal(2, preview.EstimatedCompletionWorldTick);
        Assert.Equal(0, session.Revision);

        var request = new SimulationFarmWorkPlanConfirmRequest
        {
            CommandId = "command:farm-work-plan:today",
            ExpectedRevision = 0,
            Items = items,
        };
        var confirmed = session.ConfirmFarmWorkPlan(request);
        Assert.Equal(1, confirmed.WorldRevision);
        Assert.Equal(2, confirmed.WorkOrders.Length);
        var confirmedSession = session.Snapshot();
        Assert.Equal(3m, confirmedSession.Settlement!.LaborReserved);

        var retried = session.ConfirmFarmWorkPlan(request);
        Assert.Equal(confirmed.WorldRevision, retried.WorldRevision);
        Assert.Equal(2, retried.WorkOrders.Length);

        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:farm-work-plan:today",
            ExpectedRevision = confirmed.WorldRevision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var replayed = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(saved.ReplayHash, replayed.ReplayHash);
        Assert.Equal(2, restored.GetFarmSurvivalState().WorkOrders.Length);
    }

    [Fact]
    public void 오늘작업계획은_한항목이라도막히면_예약과개정을전혀변경하지않는다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var items = new[]
        {
            PlanItem("plan-item:today:valid", 10, Player, SoilA,
                SimulationFarmSurvivalCodes.PlayerDirect),
            PlanItem("plan-item:today:missing", 20, Npc,
                "soil-tile:sim:missing", SimulationFarmSurvivalCodes.NpcDelegated),
        };
        var preview = session.PreviewFarmWorkPlan(new SimulationFarmWorkPlanPreviewRequest
        {
            ExpectedRevision = 0,
            Items = items,
        });
        Assert.False(preview.CanConfirm);
        Assert.Contains("SimulationFarmSoilTileNotFound", preview.BlockingReasonCodes);

        var error = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmFarmWorkPlan(new SimulationFarmWorkPlanConfirmRequest
            {
                CommandId = "command:farm-work-plan:blocked",
                ExpectedRevision = 0,
                Items = items,
            }));
        Assert.Equal("SimulationFarmSoilTileNotFound", error.Message);
        var state = session.Snapshot();
        Assert.Equal(0, state.Revision);
        Assert.Empty(state.FarmSurvival!.WorkOrders);
        Assert.Equal(0m, state.Settlement!.LaborReserved);
    }

    [Fact]
    public void 오늘작업계획은_같은행위자의중복배정을확정전에차단한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var preview = session.PreviewFarmWorkPlan(new SimulationFarmWorkPlanPreviewRequest
        {
            ExpectedRevision = 0,
            Items =
            [
                PlanItem("plan-item:today:first", 10, Player, SoilA,
                    SimulationFarmSurvivalCodes.PlayerDirect),
                PlanItem("plan-item:today:second", 20, Player, SoilB,
                    SimulationFarmSurvivalCodes.PlayerDirect),
            ],
        });

        Assert.False(preview.CanConfirm);
        Assert.Contains("SimulationFarmWorkPlanActorConflict",
            preview.BlockingReasonCodes);
        Assert.Empty(session.GetFarmSurvivalState().WorkOrders);
    }

    [Fact]
    public void 경관중심규칙은_이십삼일까지평온하고_이십사일에계절방어를예고한다()
    {
        Assert.Equal(SimulationFarmSurvivalCodes.ScenicSeasonRuleRevision,
            new SimulationFarmSurvivalInitialStateRequest().RuleRevision);
        var session = new 경영SimulationSessionAggregate(CreateScenicRequest());

        var dayTwentyThree = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:scenic:day-23",
            ExpectedRevision = 0,
            TickCount = 22,
        });
        Assert.Empty(dayTwentyThree.FarmSurvival!.Encounters);
        Assert.Equal("ScenicExploration",
            dayTwentyThree.FarmSurvival.DayGoalCode);

        var dayTwentyFour = session.Advance(Tick(
            "command:tick:scenic:day-24", dayTwentyThree.Revision));
        var warning = Assert.Single(dayTwentyFour.FarmSurvival!.Encounters);
        Assert.Equal(SimulationFarmSurvivalCodes.Warning, warning.StateCode);
        Assert.Equal(27, warning.DecisionDeadlineWorldTick);
        Assert.Empty(warning.AvailableChoiceStableIds);
        Assert.Equal(
            SimulationFarmSurvivalCodes.SeasonalDefenseWarningPresentation,
            warning.PresentationKey);
        var worldEvent = Assert.Single(session.GetWorldEvents(0).Events);
        Assert.False(worldEvent.CanRespond);
        Assert.Empty(worldEvent.Choices);
    }

    [Fact]
    public void 계절방어는_이십칠일에선택을열고_미선택이면이십팔일자동판정한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateScenicRequest());
        var dayTwentySeven = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:scenic:day-27",
            ExpectedRevision = 0,
            TickCount = 26,
        });

        var choice = Assert.Single(dayTwentySeven.FarmSurvival!.Encounters);
        Assert.Equal(SimulationFarmSurvivalCodes.AwaitingDefenseChoice,
            choice.StateCode);
        Assert.Equal(new[]
        {
            SimulationFarmSurvivalCodes.AutomaticDefense,
            SimulationFarmSurvivalCodes.DirectCombat,
        }, choice.AvailableChoiceStableIds);
        Assert.DoesNotContain(dayTwentySeven.FarmSurvival.Encounters, value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.RaiderFaction);
        var choiceEvent = Assert.Single(session.GetWorldEvents(0).Events);
        Assert.True(choiceEvent.CanRespond);
        Assert.Equal(2, choiceEvent.Choices.Length);

        var dayTwentyEight = session.Advance(Tick(
            "command:tick:scenic:day-28", dayTwentySeven.Revision));
        var resolved = Assert.Single(dayTwentyEight.FarmSurvival!.Encounters);
        Assert.Equal(SimulationFarmSurvivalCodes.Resolved, resolved.StateCode);
        Assert.Equal(SimulationFarmSurvivalCodes.AutomaticDefense,
            resolved.SelectedChoiceStableId);
        Assert.Equal(28, Assert.Single(dayTwentyEight.FarmSurvival.DayReports).DayNumber);
    }

    [Fact]
    public void 직접전투를선택해야만_경관중심규칙에서교전대기상태가열린다()
    {
        var session = new 경영SimulationSessionAggregate(CreateScenicRequest());
        var dayTwentySeven = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:scenic:direct:day-27",
            ExpectedRevision = 0,
            TickCount = 26,
        }).FarmSurvival!;
        var encounter = Assert.Single(dayTwentySeven.Encounters);

        var selected = session.ConfirmThreatResponse(
            new SimulationThreatResponseConfirmRequest
            {
                CommandId = "command:threat:scenic:direct",
                ExpectedRevision = dayTwentySeven.WorldRevision,
                EncounterStableId = encounter.EncounterStableId,
                ActorStableId = Player,
                ChoiceStableId = SimulationFarmSurvivalCodes.DirectCombat,
            });

        Assert.Equal(SimulationFarmSurvivalCodes.AwaitingCombat,
            Assert.Single(selected.Encounters).StateCode);
        Assert.Equal(SimulationFarmSurvivalCodes.DirectCombat,
            Assert.Single(selected.Encounters).SelectedChoiceStableId);

        var perspective = session.ConfirmCombatPerspective(
            CombatPerspective(selected.WorldRevision,
                SimulationFarmCombatCodes.FirstPersonPrecision));
        var started = session.StartCombatBeat(new SimulationCombatBeatStartRequest
        {
            CommandId = "command:combat:scenic:beat",
            ExpectedRevision = perspective.WorldRevision,
            EncounterStableId = encounter.EncounterStableId,
            ActorStableId = Player,
        });
        var beat = Assert.Single(started.Combat.Beats);
        var resolved = session.ConfirmCombatReaction(
            new SimulationCombatReactionConfirmRequest
            {
                CommandId = "command:combat:scenic:counter",
                ExpectedRevision = started.WorldRevision,
                BeatStableId = beat.BeatStableId,
                ActorStableId = Player,
                ReactionActionCode = SimulationFarmCombatCodes.Counter,
                ReactionOffsetMs = SimulationFarmCombatCodes.ImpactOffsetMs,
            });
        Assert.Equal(SimulationFarmSurvivalCodes.Resolved,
            Assert.Single(resolved.Encounters).StateCode);
        Assert.Equal(SimulationFarmSurvivalCodes.DefenseSucceeded,
            Assert.Single(resolved.Encounters).OutcomeCode);
    }

    [Fact]
    public void 자동방어선택과계절결과는_SaveReplay후에도같은Hash를만든다()
    {
        var session = new 경영SimulationSessionAggregate(CreateScenicRequest());
        var dayTwentySeven = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:scenic:save:day-27",
            ExpectedRevision = 0,
            TickCount = 26,
        }).FarmSurvival!;
        var encounter = Assert.Single(dayTwentySeven.Encounters);
        var selected = session.ConfirmThreatResponse(
            new SimulationThreatResponseConfirmRequest
            {
                CommandId = "command:threat:scenic:auto",
                ExpectedRevision = dayTwentySeven.WorldRevision,
                EncounterStableId = encounter.EncounterStableId,
                ActorStableId = Player,
                ChoiceStableId = SimulationFarmSurvivalCodes.AutomaticDefense,
            });
        Assert.Equal(SimulationFarmSurvivalCodes.AwaitingAutoResolution,
            Assert.Single(selected.Encounters).StateCode);
        session.Advance(Tick("command:tick:scenic:save:day-28",
            selected.WorldRevision));

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:farm-survival:scenic-season",
            ExpectedRevision = session.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = package.SaveStableId,
                ExpectedRevision = restored.Revision,
            });

        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(SimulationFarmSurvivalCodes.AutomaticDefense,
            Assert.Single(restored.GetFarmSurvivalState().Encounters)
                .SelectedChoiceStableId);
    }

    [Fact]
    public void 긴Session도_각이십팔일장마다계절방어를하나씩만기록한다()
    {
        var request = CreateScenicRequest();
        request.DurationTicks = 56;
        var session = new 경영SimulationSessionAggregate(request);

        var secondSeasonStart = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:scenic:first-season",
            ExpectedRevision = 0,
            TickCount = 28,
        });
        var dayFiftySix = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:scenic:second-season",
            ExpectedRevision = secondSeasonStart.Revision,
            TickCount = 27,
        }).FarmSurvival!;

        Assert.Equal(2, dayFiftySix.Encounters.Length);
        Assert.Equal(2, dayFiftySix.Encounters.Select(value =>
            value.EncounterStableId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(dayFiftySix.Encounters, value =>
            Assert.Equal(SimulationFarmSurvivalCodes.Resolved, value.StateCode));
        Assert.Equal(new[] { 28, 56 }, dayFiftySix.DayReports.Select(value =>
            value.DayNumber).ToArray());
    }

    [Fact]
    public void 다섯째날위협은경고로먼저보이고_방어부족결과는복구가능하게남는다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());

        var dayFive = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:to-day-5",
            ExpectedRevision = 0,
            TickCount = 4,
        });
        var warning = Assert.Single(dayFive.FarmSurvival!.Encounters);
        Assert.Equal(SimulationFarmSurvivalCodes.Warning, warning.StateCode);
        Assert.Equal(SimulationFarmSurvivalCodes.ZombieWarningPresentation,
            warning.PresentationKey);

        var warningEvent = Assert.Single(session.GetWorldEvents(0).Events);
        Assert.Equal(SimulationWorldEventCodes.FarmThreatEncounter,
            warningEvent.EventTypeCode);
        Assert.Equal("kr5186:l2:438:419", Assert.Single(warningEvent.TileKeys));
        Assert.True(warningEvent.SimulationOnly);
        Assert.False(warningEvent.IsOperationalState);

        var daySix = session.Advance(Tick("command:tick:day-6", dayFive.Revision));
        var zombie = daySix.FarmSurvival!.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var raider = daySix.FarmSurvival.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.RaiderFaction);
        Assert.Equal(SimulationFarmSurvivalCodes.Resolved, zombie.StateCode);
        Assert.True(zombie.Recoverable);
        Assert.True(daySix.FarmSurvival.RecoverableDamageUnits > 0m);
        Assert.Equal(SimulationFarmSurvivalCodes.AwaitingResponse, raider.StateCode);

        var changes = session.GetWorldEvents(dayFive.Revision).Events;
        Assert.Equal(2, changes.Length);
        Assert.Contains(changes, value => value.EventStableId.EndsWith(
            raider.EncounterStableId, StringComparison.Ordinal)
            && value.Choices.Length == 3 && value.CanRespond);
    }

    [Fact]
    public void 약탈자대응은선택Id만받고_수치결과는서버Seed와방어상태가정한다()
    {
        var first = AdvanceToRaider(new 경영SimulationSessionAggregate(CreateRequest()));
        var second = AdvanceToRaider(new 경영SimulationSessionAggregate(CreateRequest()));
        var firstEncounter = first.Session.GetFarmSurvivalState().Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.RaiderFaction);
        var secondEncounter = second.Session.GetFarmSurvivalState().Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.RaiderFaction);

        var firstResult = first.Session.ConfirmThreatResponse(new SimulationThreatResponseConfirmRequest
        {
            CommandId = "command:threat:deception",
            ExpectedRevision = first.Revision,
            EncounterStableId = firstEncounter.EncounterStableId,
            ActorStableId = Player,
            ChoiceStableId = SimulationFarmSurvivalCodes.Deception,
        });
        var secondResult = second.Session.ConfirmThreatResponse(new SimulationThreatResponseConfirmRequest
        {
            CommandId = "command:threat:deception",
            ExpectedRevision = second.Revision,
            EncounterStableId = secondEncounter.EncounterStableId,
            ActorStableId = Player,
            ChoiceStableId = SimulationFarmSurvivalCodes.Deception,
        });

        var firstResolved = firstResult.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.RaiderFaction);
        var secondResolved = secondResult.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.RaiderFaction);
        Assert.Equal(firstResolved.OutcomeCode, secondResolved.OutcomeCode);
        Assert.Equal(firstResolved.SupplyLossUnits, secondResolved.SupplyLossUnits);
        Assert.Equal(firstResolved.DamageUnits, secondResolved.DamageUnits);
        Assert.True(firstResolved.Recoverable);
    }

    [Fact]
    public void 위협부상은_의료휴식작업으로회복할수있다()
    {
        var advanced = AdvanceToRaider(
            new 경영SimulationSessionAggregate(CreateRequest()));
        var injured = advanced.Session.GetFarmSurvivalState().Actors.Single(value =>
            value.Injured);
        Assert.True(injured.Injured);
        var assignment = injured.ActorKindCode == SimulationFarmSurvivalCodes.Player
            ? SimulationFarmSurvivalCodes.PlayerDirect
            : SimulationFarmSurvivalCodes.NpcDelegated;

        var resting = advanced.Session.ConfirmFarmWork(
            new SimulationFarmWorkConfirmRequest
            {
                CommandId = "command:farm-work:medical-rest",
                ExpectedRevision = advanced.Revision,
                ActorStableId = injured.ActorStableId,
                TargetStableId = injured.ActorStableId,
                ActionCode = SimulationFarmSurvivalCodes.MedicalRest,
                AssignmentKindCode = assignment,
            });
        var recovered = advanced.Session.Advance(
            Tick("command:tick:medical-rest", resting.WorldRevision));

        var actor = recovered.FarmSurvival!.Actors.Single(value =>
            value.ActorStableId == injured.ActorStableId);
        Assert.False(actor.Injured);
        Assert.Equal(100m, actor.Health);
        Assert.Equal(3m, recovered.FarmSurvival.RepairMaterialUnits);
    }

    [Fact]
    public void 농장노동과위협대응은SaveReplay후에도같은상태Hash를만든다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var work = session.ConfirmFarmWork(Confirm(
            "command:farm-work:save-replay", 0, Player, SoilA,
            SimulationFarmSurvivalCodes.PlayerDirect));
        session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:save-replay-to-day-6",
            ExpectedRevision = work.WorldRevision,
            TickCount = 5,
        });
        var before = session.GetFarmSurvivalState();
        var raider = before.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.RaiderFaction);
        session.ConfirmThreatResponse(new SimulationThreatResponseConfirmRequest
        {
            CommandId = "command:threat:save-replay",
            ExpectedRevision = before.WorldRevision,
            EncounterStableId = raider.EncounterStableId,
            ActorStableId = Player,
            ChoiceStableId = SimulationFarmSurvivalCodes.Trade,
        });

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:farm-survival:week-one",
            ExpectedRevision = session.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);

        var originalState = session.GetFarmSurvivalState();
        var restoredState = restored.GetFarmSurvivalState();
        Assert.Equal(originalState.WorldRevision, restoredState.WorldRevision);
        Assert.Equal(originalState.SupplyUnits, restoredState.SupplyUnits);
        Assert.Equal(originalState.RecoverableDamageUnits,
            restoredState.RecoverableDamageUnits);
        Assert.Equal(
            originalState.WorkOrders.Select(value =>
                (value.WorkOrderStableId, value.StatusCode)),
            restoredState.WorkOrders.Select(value =>
                (value.WorkOrderStableId, value.StatusCode)));
        Assert.Equal(
            originalState.Encounters.Select(value =>
                (value.EncounterStableId, value.StateCode, value.OutcomeCode)),
            restoredState.Encounters.Select(value =>
                (value.EncounterStableId, value.StateCode, value.OutcomeCode)));
        Assert.Equal(package.ReplayHash, restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = package.SaveStableId,
                ExpectedRevision = restored.Revision,
            }).ReplayHash);
    }

    [Fact]
    public async Task HTTP에서도_농장노동Preview와Confirm을수직으로처리한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var session = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var route = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(session!.SessionStableId)
            + "/farm-survival";

        var state = await client.GetFromJsonAsync<SimulationFarmSurvivalStateSnapshot>(
            route);
        Assert.NotNull(state);
        Assert.True(state.SimulationOnly);

        var previewResponse = await client.PostAsJsonAsync(route + "/work/preview",
            Preview(state.WorldRevision, Player, SoilA,
                SimulationFarmSurvivalCodes.PlayerDirect));
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationFarmWorkPreviewSnapshot>();
        Assert.True(preview!.CanConfirm);

        var confirmResponse = await client.PostAsJsonAsync(route + "/work/confirm",
            Confirm("command:farm-work:http", state.WorldRevision,
                Player, SoilA, SimulationFarmSurvivalCodes.PlayerDirect));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        Assert.Equal(SimulationFarmSurvivalCodes.InProgress,
            Assert.Single(confirmed!.WorkOrders).StatusCode);
    }

    [Fact]
    public async Task HTTP에서도_오늘작업계획을한번에Preview하고원자확정한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var session = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var route = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(session!.SessionStableId)
            + "/farm-survival/work-plans";
        var items = new[]
        {
            PlanItem("plan-item:http:player", 10, Player, SoilA,
                SimulationFarmSurvivalCodes.PlayerDirect),
            PlanItem("plan-item:http:npc", 20, Npc, SoilB,
                SimulationFarmSurvivalCodes.NpcDelegated),
        };

        var previewResponse = await client.PostAsJsonAsync(route + "/preview",
            new SimulationFarmWorkPlanPreviewRequest
            {
                ExpectedRevision = session.Revision,
                Items = items,
            });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationFarmWorkPlanPreviewSnapshot>();
        Assert.True(preview!.CanConfirm);
        Assert.Equal(2, preview.Items.Length);

        var confirmResponse = await client.PostAsJsonAsync(route + "/confirm",
            new SimulationFarmWorkPlanConfirmRequest
            {
                CommandId = "command:farm-work-plan:http",
                ExpectedRevision = session.Revision,
                Items = items,
            });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        Assert.Equal(session.Revision + 1, confirmed!.WorldRevision);
        Assert.Equal(2, confirmed.WorkOrders.Length);
    }

    [Fact]
    public void 일인칭은_삼인칭보다넓은일반방어창을가지지만_완벽방어창은같다()
    {
        var firstPerson = ResolveGuardAt(
            SimulationFarmCombatCodes.FirstPersonPrecision, 750);
        var thirdPerson = ResolveGuardAt(
            SimulationFarmCombatCodes.ThirdPersonAwareness, 750);
        var firstPerfect = ResolveGuardAt(
            SimulationFarmCombatCodes.FirstPersonPrecision, 950);
        var thirdPerfect = ResolveGuardAt(
            SimulationFarmCombatCodes.ThirdPersonAwareness, 950);

        Assert.Equal(SimulationFarmCombatCodes.OnTime,
            Assert.Single(firstPerson.Combat.Reactions).GradeCode);
        Assert.Equal(SimulationFarmCombatCodes.Early,
            Assert.Single(thirdPerson.Combat.Reactions).GradeCode);
        Assert.Equal(SimulationFarmCombatCodes.Perfect,
            Assert.Single(firstPerfect.Combat.Reactions).GradeCode);
        Assert.Equal(SimulationFarmCombatCodes.Perfect,
            Assert.Single(thirdPerfect.Combat.Reactions).GradeCode);
    }

    [Fact]
    public void 완벽카운터는_서버가피해와점수와경직을결정하고_좀비전을끝낸다()
    {
        var session = CreateInteractiveCombatSession();
        var ready = AdvanceToInteractiveCombat(session);
        var perspective = session.ConfirmCombatPerspective(
            CombatPerspective(ready.WorldRevision,
                SimulationFarmCombatCodes.FirstPersonPrecision));
        var encounter = perspective.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var started = session.StartCombatBeat(new SimulationCombatBeatStartRequest
        {
            CommandId = "command:combat:beat:start",
            ExpectedRevision = perspective.WorldRevision,
            EncounterStableId = encounter.EncounterStableId,
            ActorStableId = Player,
        });
        var beat = Assert.Single(started.Combat.Beats);

        var resolved = session.ConfirmCombatReaction(
            new SimulationCombatReactionConfirmRequest
            {
                CommandId = "command:combat:reaction:counter",
                ExpectedRevision = started.WorldRevision,
                BeatStableId = beat.BeatStableId,
                ActorStableId = Player,
                ReactionActionCode = SimulationFarmCombatCodes.Counter,
                ReactionOffsetMs = SimulationFarmCombatCodes.ImpactOffsetMs,
            });

        var reaction = Assert.Single(resolved.Combat.Reactions);
        Assert.Equal(SimulationFarmCombatCodes.Perfect, reaction.GradeCode);
        Assert.Equal(0m, reaction.ActorDamageUnits);
        Assert.Equal(2, reaction.DefenseResponseScore);
        Assert.True(reaction.ThreatStaggered);
        Assert.Equal(SimulationFarmSurvivalCodes.DefenseSucceeded,
            resolved.Encounters.Single(value => value.EncounterStableId ==
                encounter.EncounterStableId).OutcomeCode);
    }

    [Fact]
    public void 활성전투박자는_시점변경과두번째반응을거부하고_명령재시도는멱등하다()
    {
        var session = CreateInteractiveCombatSession();
        var ready = AdvanceToInteractiveCombat(session);
        var perspectiveRequest = CombatPerspective(ready.WorldRevision,
            SimulationFarmCombatCodes.FirstPersonPrecision);
        var perspective = session.ConfirmCombatPerspective(perspectiveRequest);
        var retriedPerspective = session.ConfirmCombatPerspective(perspectiveRequest);
        Assert.Equal(perspective.WorldRevision, retriedPerspective.WorldRevision);

        var encounter = perspective.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var started = session.StartCombatBeat(new SimulationCombatBeatStartRequest
        {
            CommandId = "command:combat:beat:lock-test",
            ExpectedRevision = perspective.WorldRevision,
            EncounterStableId = encounter.EncounterStableId,
            ActorStableId = Player,
        });
        var locked = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmCombatPerspective(CombatPerspective(started.WorldRevision,
                SimulationFarmCombatCodes.ThirdPersonAwareness)));
        Assert.Equal("SimulationCombatPerspectiveLocked", locked.ErrorCode);

        var beat = Assert.Single(started.Combat.Beats);
        var reactionRequest = new SimulationCombatReactionConfirmRequest
        {
            CommandId = "command:combat:reaction:idempotent",
            ExpectedRevision = started.WorldRevision,
            BeatStableId = beat.BeatStableId,
            ActorStableId = Player,
            ReactionActionCode = SimulationFarmCombatCodes.Guard,
            ReactionOffsetMs = 1000,
        };
        var resolved = session.ConfirmCombatReaction(reactionRequest);
        var retried = session.ConfirmCombatReaction(reactionRequest);
        Assert.Equal(resolved.WorldRevision, retried.WorldRevision);

        var second = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmCombatReaction(new SimulationCombatReactionConfirmRequest
            {
                CommandId = "command:combat:reaction:second",
                ExpectedRevision = resolved.WorldRevision,
                BeatStableId = beat.BeatStableId,
                ActorStableId = Player,
                ReactionActionCode = SimulationFarmCombatCodes.Counter,
                ReactionOffsetMs = 1000,
            }));
        Assert.Equal("SimulationCombatBeatAlreadyResolved", second.ErrorCode);
    }

    [Fact]
    public void 반응하지않은전투박자는_다음WorldTick에서결정적으로만료된다()
    {
        var session = CreateInteractiveCombatSession();
        var ready = AdvanceToInteractiveCombat(session);
        var perspective = session.ConfirmCombatPerspective(
            CombatPerspective(ready.WorldRevision,
                SimulationFarmCombatCodes.FirstPersonPrecision));
        var encounter = perspective.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var started = session.StartCombatBeat(new SimulationCombatBeatStartRequest
        {
            CommandId = "command:combat:beat:expire",
            ExpectedRevision = perspective.WorldRevision,
            EncounterStableId = encounter.EncounterStableId,
            ActorStableId = Player,
        });

        var advanced = session.Advance(Tick("command:tick:combat-expire",
            started.WorldRevision));
        var reaction = Assert.Single(advanced.FarmSurvival!.Combat.Reactions);
        Assert.Equal(SimulationFarmCombatCodes.Expired, reaction.GradeCode);
        Assert.Equal(10m, reaction.ActorDamageUnits);
        Assert.Equal(SimulationFarmCombatCodes.Resolved,
            Assert.Single(advanced.FarmSurvival.Combat.Beats).StateCode);
    }

    [Fact]
    public void 전투명령세개는_저장재생후동일한상태와hash를만든다()
    {
        var session = CreateInteractiveCombatSession();
        var ready = AdvanceToInteractiveCombat(session);
        var perspective = session.ConfirmCombatPerspective(
            CombatPerspective(ready.WorldRevision,
                SimulationFarmCombatCodes.FirstPersonPrecision));
        var encounter = perspective.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var started = session.StartCombatBeat(new SimulationCombatBeatStartRequest
        {
            CommandId = "command:combat:beat:save",
            ExpectedRevision = perspective.WorldRevision,
            EncounterStableId = encounter.EncounterStableId,
            ActorStableId = Player,
        });
        var beat = Assert.Single(started.Combat.Beats);
        session.ConfirmCombatReaction(new SimulationCombatReactionConfirmRequest
        {
            CommandId = "command:combat:reaction:save",
            ExpectedRevision = started.WorldRevision,
            BeatStableId = beat.BeatStableId,
            ActorStableId = Player,
            ReactionActionCode = SimulationFarmCombatCodes.Counter,
            ReactionOffsetMs = 1000,
        });

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:farm-combat:one-beat",
            ExpectedRevision = session.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = package.SaveStableId,
                ExpectedRevision = restored.Revision,
            });

        Assert.Contains(package.CommandLog, value => value.CommandTypeCode ==
            SimulationCommandTypeCodes.CombatPerspectiveConfirm);
        Assert.Contains(package.CommandLog, value => value.CommandTypeCode ==
            SimulationCommandTypeCodes.CombatBeatStart);
        Assert.Contains(package.CommandLog, value => value.CommandTypeCode ==
            SimulationCommandTypeCodes.CombatReactionConfirm);
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(Assert.Single(session.GetFarmSurvivalState().Combat.Reactions).GradeCode,
            Assert.Single(restored.GetFarmSurvivalState().Combat.Reactions).GradeCode);
    }

    [Fact]
    public void 완벽카운터는_r3에서돌파기회를열고_전진공격은다음Tick에전선을전진시킨다()
    {
        var session = CreateHeroTacticalCombatSession();
        var reacted = ReactHeroTacticalCombat(session,
            SimulationFarmCombatCodes.Counter,
            SimulationFarmCombatCodes.ImpactOffsetMs);

        var encounter = reacted.Encounters.Single(value => value.ThreatTypeCode ==
            SimulationFarmSurvivalCodes.ZombiePressure);
        Assert.Equal(string.Empty, encounter.OutcomeCode);
        var opportunity = Assert.Single(reacted.Combat.Tactical.Opportunities);
        Assert.Equal(SimulationFarmTacticalCombatCodes.Breakthrough,
            opportunity.OpportunityKindCode);
        Assert.Equal(2, opportunity.Quality);
        var window = Assert.Single(reacted.Combat.Tactical.OrderWindows);
        var front = Assert.Single(reacted.Combat.Tactical.Fronts);

        var preview = session.PreviewTacticalOrder(new SimulationTacticalOrderPreviewRequest
        {
            ExpectedRevision = reacted.WorldRevision,
            OrderWindowStableId = window.OrderWindowStableId,
            FrontStableId = front.FrontStableId,
            ActorStableId = Player,
            OrderCode = SimulationFarmTacticalCombatCodes.AdvanceAndAttack,
            OpportunityStableId = opportunity.OpportunityStableId,
        });
        Assert.True(preview.CanConfirm);
        Assert.Equal(2, preview.OpportunityBonusScore);
        Assert.True(preview.ProjectedDefenseSucceeded);

        var confirmed = session.ConfirmTacticalOrder(
            new SimulationTacticalOrderConfirmRequest
            {
                CommandId = "command:tactical:advance",
                ExpectedRevision = reacted.WorldRevision,
                OrderWindowStableId = window.OrderWindowStableId,
                FrontStableId = front.FrontStableId,
                ActorStableId = Player,
                OrderCode = SimulationFarmTacticalCombatCodes.AdvanceAndAttack,
                OpportunityStableId = opportunity.OpportunityStableId,
            });
        Assert.Equal(SimulationFarmTacticalCombatCodes.Reserved,
            Assert.Single(confirmed.Combat.Tactical.Opportunities).StateCode);

        var advanced = session.Advance(Tick("command:tick:tactical-advance",
            confirmed.WorldRevision)).FarmSurvival!;
        var resolution = Assert.Single(advanced.Combat.Tactical.Resolutions);
        Assert.True(resolution.DefenseSucceeded);
        Assert.Equal(SimulationFarmTacticalCombatCodes.Forward,
            resolution.FrontPositionCode);
        Assert.Equal(SimulationFarmTacticalCombatCodes.Consumed,
            Assert.Single(advanced.Combat.Tactical.Opportunities).StateCode);
        Assert.Equal(SimulationFarmSurvivalCodes.DefenseSucceeded,
            advanced.Encounters.Single(value => value.EncounterStableId ==
                encounter.EncounterStableId).OutcomeCode);
    }

    [Fact]
    public void 전술기회는행동과영웅이맞아야하며_확정명령은멱등하다()
    {
        var request = CreateRequest();
        request.FarmSurvival!.RuleRevision =
            SimulationFarmSurvivalCodes.HeroTacticalCombatRuleRevision;
        request.FarmSurvival.Actors = request.FarmSurvival.Actors.Concat(
        [
            new SimulationFarmActorInitialStateRequest
            {
                ActorStableId = "actor:sim:teammate",
                ActorKindCode = SimulationFarmSurvivalCodes.Player,
                KoreanName = "팀원 영웅",
            },
        ]).ToArray();
        var session = new 경영SimulationSessionAggregate(request);
        var reacted = ReactHeroTacticalCombat(session,
            SimulationFarmCombatCodes.Guard,
            SimulationFarmCombatCodes.ImpactOffsetMs);
        var opportunity = Assert.Single(reacted.Combat.Tactical.Opportunities);
        var window = Assert.Single(reacted.Combat.Tactical.OrderWindows);
        var front = Assert.Single(reacted.Combat.Tactical.Fronts);

        var wrongOrder = session.PreviewTacticalOrder(
            new SimulationTacticalOrderPreviewRequest
            {
                ExpectedRevision = reacted.WorldRevision,
                OrderWindowStableId = window.OrderWindowStableId,
                FrontStableId = front.FrontStableId,
                ActorStableId = Player,
                OrderCode = SimulationFarmTacticalCombatCodes.AdvanceAndAttack,
                OpportunityStableId = opportunity.OpportunityStableId,
            });
        Assert.False(wrongOrder.CanConfirm);
        Assert.Contains("SimulationTacticalOpportunityOrderMismatch",
            wrongOrder.BlockingReasonCodes);

        var otherHero = session.PreviewTacticalOrder(
            new SimulationTacticalOrderPreviewRequest
            {
                ExpectedRevision = reacted.WorldRevision,
                OrderWindowStableId = window.OrderWindowStableId,
                FrontStableId = front.FrontStableId,
                ActorStableId = "actor:sim:teammate",
                OrderCode = SimulationFarmTacticalCombatCodes.HoldFormation,
                OpportunityStableId = opportunity.OpportunityStableId,
            });
        Assert.False(otherHero.CanConfirm);
        Assert.Contains("SimulationTacticalOrderActorMismatch",
            otherHero.BlockingReasonCodes);

        var command = new SimulationTacticalOrderConfirmRequest
        {
            CommandId = "command:tactical:hold:idempotent",
            ExpectedRevision = reacted.WorldRevision,
            OrderWindowStableId = window.OrderWindowStableId,
            FrontStableId = front.FrontStableId,
            ActorStableId = Player,
            OrderCode = SimulationFarmTacticalCombatCodes.HoldFormation,
            OpportunityStableId = opportunity.OpportunityStableId,
        };
        var confirmed = session.ConfirmTacticalOrder(command);
        var retried = session.ConfirmTacticalOrder(command);
        Assert.Equal(confirmed.WorldRevision, retried.WorldRevision);
    }

    [Fact]
    public void 명령하지않으면_다음Tick에기회가만료되고_보너스없는대형사수가적용된다()
    {
        var session = CreateHeroTacticalCombatSession();
        var reacted = ReactHeroTacticalCombat(session,
            SimulationFarmCombatCodes.Counter,
            SimulationFarmCombatCodes.ImpactOffsetMs);

        var advanced = session.Advance(Tick("command:tick:tactical-timeout",
            reacted.WorldRevision)).FarmSurvival!;
        var order = Assert.Single(advanced.Combat.Tactical.Orders);
        Assert.True(order.AutomaticallySelected);
        Assert.Equal(SimulationFarmTacticalCombatCodes.HoldFormation,
            order.OrderCode);
        Assert.Equal(SimulationFarmTacticalCombatCodes.Expired,
            Assert.Single(advanced.Combat.Tactical.Opportunities).StateCode);
        var allied = advanced.Combat.Tactical.Squads.Single(value =>
            value.SideCode == SimulationFarmTacticalCombatCodes.Allied);
        Assert.Equal(0, allied.CombatStrength);
        Assert.Equal(1, allied.RecoverableInjuryCount);
        Assert.True(advanced.Actors.Single(value => value.ActorStableId == Npc).Injured);
        Assert.Equal(20m, advanced.RecoverableDamageUnits);
    }

    [Fact]
    public void 전술명령과Tick판정은_저장재생후동일한hash를만든다()
    {
        var session = CreateHeroTacticalCombatSession();
        var reacted = ReactHeroTacticalCombat(session,
            SimulationFarmCombatCodes.Counter,
            SimulationFarmCombatCodes.ImpactOffsetMs);
        var opportunity = Assert.Single(reacted.Combat.Tactical.Opportunities);
        var window = Assert.Single(reacted.Combat.Tactical.OrderWindows);
        var front = Assert.Single(reacted.Combat.Tactical.Fronts);
        var confirmed = session.ConfirmTacticalOrder(
            new SimulationTacticalOrderConfirmRequest
            {
                CommandId = "command:tactical:save",
                ExpectedRevision = reacted.WorldRevision,
                OrderWindowStableId = window.OrderWindowStableId,
                FrontStableId = front.FrontStableId,
                ActorStableId = Player,
                OrderCode = SimulationFarmTacticalCombatCodes.AdvanceAndAttack,
                OpportunityStableId = opportunity.OpportunityStableId,
            });
        session.Advance(Tick("command:tick:tactical-save",
            confirmed.WorldRevision));

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:farm-tactical:one-order",
            ExpectedRevision = session.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = package.SaveStableId,
                ExpectedRevision = restored.Revision,
            });

        Assert.Contains(package.CommandLog, value => value.CommandTypeCode ==
            SimulationCommandTypeCodes.TacticalOrderConfirm);
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(Assert.Single(session.GetFarmSurvivalState().Combat.Tactical
                .Resolutions).OutcomeCode,
            Assert.Single(restored.GetFarmSurvivalState().Combat.Tactical
                .Resolutions).OutcomeCode);
    }

    [Fact]
    public async Task HTTP전투경계는_경로의박자Id와본문Id를일치시키고_서버결과를반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateRequest();
        request.ClientRequestId = Guid.Parse("5ff6319f-babd-43a4-a595-25cb891b0951");
        request.FarmSurvival!.RuleRevision =
            SimulationFarmSurvivalCodes.InteractiveCombatRuleRevision;
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", request);
        var session = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(session!.SessionStableId);
        var tickResponse = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:combat:to-day-6",
                ExpectedRevision = 0,
                TickCount = 5,
            });
        var ready = await tickResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var farmRoute = sessionRoute + "/farm-survival";
        var perspectiveResponse = await client.PostAsJsonAsync(
            farmRoute + "/combat/perspective/confirm",
            new SimulationCombatPerspectiveConfirmRequest
            {
                CommandId = "command:http:combat:perspective",
                ExpectedRevision = ready!.Revision,
                ActorStableId = Player,
                PerspectiveCode = SimulationFarmCombatCodes.FirstPersonPrecision,
            });
        var perspective = await perspectiveResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        var encounter = perspective!.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var beatResponse = await client.PostAsJsonAsync(
            farmRoute + "/combat/beats/start",
            new SimulationCombatBeatStartRequest
            {
                CommandId = "command:http:combat:beat",
                ExpectedRevision = perspective.WorldRevision,
                EncounterStableId = encounter.EncounterStableId,
                ActorStableId = Player,
            });
        var started = await beatResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        var beat = Assert.Single(started!.Combat.Beats);
        var reaction = new SimulationCombatReactionConfirmRequest
        {
            CommandId = "command:http:combat:reaction",
            ExpectedRevision = started.WorldRevision,
            BeatStableId = beat.BeatStableId,
            ActorStableId = Player,
            ReactionActionCode = SimulationFarmCombatCodes.Counter,
            ReactionOffsetMs = 1000,
        };

        var mismatch = await client.PostAsJsonAsync(
            farmRoute + "/combat/beats/another-beat/react", reaction);
        var resolved = await client.PostAsJsonAsync(farmRoute + "/combat/beats/"
            + Uri.EscapeDataString(beat.BeatStableId) + "/react", reaction);
        var state = await resolved.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();

        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(SimulationFarmCombatCodes.Perfect,
            Assert.Single(state!.Combat.Reactions).GradeCode);
    }

    [Fact]
    public async Task HTTP전술경계는_Preview와명령창Id확인을거쳐Confirm한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateRequest();
        request.ClientRequestId = Guid.Parse("60d40d0b-7cf6-45e8-a9b7-6fe390c50326");
        request.FarmSurvival!.RuleRevision =
            SimulationFarmSurvivalCodes.HeroTacticalCombatRuleRevision;
        var created = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", request);
        var session = await created.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(session!.SessionStableId);
        var tick = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:tactical:to-combat",
                ExpectedRevision = 0,
                TickCount = 5,
            });
        var ready = await tick.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var farmRoute = sessionRoute + "/farm-survival";
        var perspectiveResponse = await client.PostAsJsonAsync(
            farmRoute + "/combat/perspective/confirm",
            new SimulationCombatPerspectiveConfirmRequest
            {
                CommandId = "command:http:tactical:perspective",
                ExpectedRevision = ready!.Revision,
                ActorStableId = Player,
                PerspectiveCode = SimulationFarmCombatCodes.FirstPersonPrecision,
            });
        var perspective = await perspectiveResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        var encounter = perspective!.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var beatResponse = await client.PostAsJsonAsync(
            farmRoute + "/combat/beats/start",
            new SimulationCombatBeatStartRequest
            {
                CommandId = "command:http:tactical:beat",
                ExpectedRevision = perspective.WorldRevision,
                EncounterStableId = encounter.EncounterStableId,
                ActorStableId = Player,
            });
        var started = await beatResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        var beat = Assert.Single(started!.Combat.Beats);
        var reactionResponse = await client.PostAsJsonAsync(
            farmRoute + "/combat/beats/"
                + Uri.EscapeDataString(beat.BeatStableId) + "/react",
            new SimulationCombatReactionConfirmRequest
            {
                CommandId = "command:http:tactical:reaction",
                ExpectedRevision = started.WorldRevision,
                BeatStableId = beat.BeatStableId,
                ActorStableId = Player,
                ReactionActionCode = SimulationFarmCombatCodes.Counter,
                ReactionOffsetMs = SimulationFarmCombatCodes.ImpactOffsetMs,
            });
        var reacted = await reactionResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        var window = Assert.Single(reacted!.Combat.Tactical.OrderWindows);
        var front = Assert.Single(reacted.Combat.Tactical.Fronts);
        var opportunity = Assert.Single(reacted.Combat.Tactical.Opportunities);
        var previewRequest = new SimulationTacticalOrderPreviewRequest
        {
            ExpectedRevision = reacted.WorldRevision,
            OrderWindowStableId = window.OrderWindowStableId,
            FrontStableId = front.FrontStableId,
            ActorStableId = Player,
            OrderCode = SimulationFarmTacticalCombatCodes.AdvanceAndAttack,
            OpportunityStableId = opportunity.OpportunityStableId,
        };
        var previewResponse = await client.PostAsJsonAsync(
            farmRoute + "/combat/tactical-orders/preview", previewRequest);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationTacticalOrderPreviewSnapshot>();
        Assert.True(preview!.CanConfirm);

        var confirm = new SimulationTacticalOrderConfirmRequest
        {
            CommandId = "command:http:tactical:confirm",
            ExpectedRevision = reacted.WorldRevision,
            OrderWindowStableId = window.OrderWindowStableId,
            FrontStableId = front.FrontStableId,
            ActorStableId = Player,
            OrderCode = SimulationFarmTacticalCombatCodes.AdvanceAndAttack,
            OpportunityStableId = opportunity.OpportunityStableId,
        };
        var mismatch = await client.PostAsJsonAsync(
            farmRoute + "/combat/tactical-orders/another-window/confirm", confirm);
        var confirmed = await client.PostAsJsonAsync(
            farmRoute + "/combat/tactical-orders/"
                + Uri.EscapeDataString(window.OrderWindowStableId) + "/confirm",
            confirm);

        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    private static SimulationFarmSurvivalStateSnapshot ResolveGuardAt(
        string perspectiveCode,
        int reactionOffsetMs)
    {
        var session = CreateInteractiveCombatSession();
        var ready = AdvanceToInteractiveCombat(session);
        var perspective = session.ConfirmCombatPerspective(
            CombatPerspective(ready.WorldRevision, perspectiveCode));
        var encounter = perspective.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var started = session.StartCombatBeat(new SimulationCombatBeatStartRequest
        {
            CommandId = "command:combat:beat:" + perspectiveCode,
            ExpectedRevision = perspective.WorldRevision,
            EncounterStableId = encounter.EncounterStableId,
            ActorStableId = Player,
        });
        var beat = Assert.Single(started.Combat.Beats);
        return session.ConfirmCombatReaction(new SimulationCombatReactionConfirmRequest
        {
            CommandId = "command:combat:reaction:" + perspectiveCode,
            ExpectedRevision = started.WorldRevision,
            BeatStableId = beat.BeatStableId,
            ActorStableId = Player,
            ReactionActionCode = SimulationFarmCombatCodes.Guard,
            ReactionOffsetMs = reactionOffsetMs,
        });
    }

    private static 경영SimulationSessionAggregate CreateInteractiveCombatSession()
    {
        var request = CreateRequest();
        request.FarmSurvival!.RuleRevision =
            SimulationFarmSurvivalCodes.InteractiveCombatRuleRevision;
        return new 경영SimulationSessionAggregate(request);
    }

    private static 경영SimulationSessionAggregate CreateHeroTacticalCombatSession()
    {
        var request = CreateRequest();
        request.FarmSurvival!.RuleRevision =
            SimulationFarmSurvivalCodes.HeroTacticalCombatRuleRevision;
        return new 경영SimulationSessionAggregate(request);
    }

    private static SimulationFarmSurvivalStateSnapshot ReactHeroTacticalCombat(
        경영SimulationSessionAggregate session,
        string actionCode,
        int reactionOffsetMs)
    {
        var ready = AdvanceToInteractiveCombat(session);
        var perspective = session.ConfirmCombatPerspective(
            CombatPerspective(ready.WorldRevision,
                SimulationFarmCombatCodes.FirstPersonPrecision));
        var encounter = perspective.Encounters.Single(value =>
            value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);
        var started = session.StartCombatBeat(new SimulationCombatBeatStartRequest
        {
            CommandId = "command:combat:beat:tactical",
            ExpectedRevision = perspective.WorldRevision,
            EncounterStableId = encounter.EncounterStableId,
            ActorStableId = Player,
        });
        var beat = Assert.Single(started.Combat.Beats);
        return session.ConfirmCombatReaction(
            new SimulationCombatReactionConfirmRequest
            {
                CommandId = "command:combat:reaction:tactical",
                ExpectedRevision = started.WorldRevision,
                BeatStableId = beat.BeatStableId,
                ActorStableId = Player,
                ReactionActionCode = actionCode,
                ReactionOffsetMs = reactionOffsetMs,
            });
    }

    private static SimulationFarmSurvivalStateSnapshot AdvanceToInteractiveCombat(
        경영SimulationSessionAggregate session)
        => session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:to-interactive-combat",
            ExpectedRevision = 0,
            TickCount = 5,
        }).FarmSurvival!;

    private static SimulationCombatPerspectiveConfirmRequest CombatPerspective(
        long revision,
        string perspectiveCode)
        => new()
        {
            CommandId = "command:combat:perspective:" + perspectiveCode,
            ExpectedRevision = revision,
            ActorStableId = Player,
            PerspectiveCode = perspectiveCode,
        };

    private static (경영SimulationSessionAggregate Session, long Revision)
        AdvanceToRaider(경영SimulationSessionAggregate session)
    {
        var result = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:tick:to-raider",
            ExpectedRevision = 0,
            TickCount = 5,
        });
        return (session, result.Revision);
    }

    private static SimulationFarmWorkPreviewRequest Preview(
        long revision,
        string actor,
        string target,
        string assignment)
        => new()
        {
            ExpectedRevision = revision,
            ActorStableId = actor,
            TargetStableId = target,
            ActionCode = SimulationFarmSurvivalCodes.Tilling,
            AssignmentKindCode = assignment,
        };

    private static SimulationFarmWorkConfirmRequest Confirm(
        string commandId,
        long revision,
        string actor,
        string target,
        string assignment)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            ActorStableId = actor,
            TargetStableId = target,
            ActionCode = SimulationFarmSurvivalCodes.Tilling,
            AssignmentKindCode = assignment,
        };

    private static SimulationFarmWorkPlanItemRequest PlanItem(
        string planItemStableId,
        int priority,
        string actor,
        string target,
        string assignment)
        => new()
        {
            PlanItemStableId = planItemStableId,
            Priority = priority,
            ActorStableId = actor,
            TargetStableId = target,
            ActionCode = SimulationFarmSurvivalCodes.Tilling,
            AssignmentKindCode = assignment,
        };

    private static 경영SimulationTick진행Request Tick(
        string commandId,
        long revision)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            TickCount = 1,
        };

    private static 경영SimulationSession생성Request CreateRequest()
        => new()
        {
            ClientRequestId = Guid.Parse("5c5ed8c4-a504-4771-a5d8-c5e5a79054a0"),
            ScenarioStableId = "scenario:sim.daegwallyeong-spring-survival",
            ScenarioDataRevision = "scenario-data:2026-08-15",
            ScenarioSeed = 20260815,
            RuleRevision = "rule:survival-season-r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim.survivors",
                TerritoryStableId = "territory:sim.pyeongchang",
                SettlementStableId = "settlement:sim.daegwallyeong-farm",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            },
            Settlement = new SimulationSettlementInitialStateRequest
            {
                TreasuryBalance = 100m,
                CurrencyCode = "SIM",
                LaborCapacityTotal = 10m,
                LaborReserved = 0m,
                StorageCapacity = 20m,
                StorageOccupied = 5m,
                StorageUnitCode = "unit",
                PopulationCount = 2,
                PopulationFoodDemandPerTick = 2m,
                GarrisonCount = 0,
                GarrisonFoodDemandPerTick = 0m,
                FoodEquivalentUnitCode = "person-day",
                FoodEquivalentRuleRevision = "food-equivalent:sim-r1",
                Districts =
                [
                    new SimulationSettlementDistrictRequest
                    {
                        DistrictStableId = "district:sim.daegwallyeong-farm",
                        DistrictTypeCode = "FarmDistrict",
                        SourceStableIds = ["source:scenario-farm-survival"],
                    },
                ],
                Facilities =
                [
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = "facility:sim.farm-storage",
                        FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Storage,
                        DistrictStableId = "district:sim.daegwallyeong-farm",
                        SourceStableIds = ["source:scenario-farm-survival"],
                    },
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = "facility:sim.farm-market",
                        FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Market,
                        DistrictStableId = "district:sim.daegwallyeong-farm",
                        SourceStableIds = ["source:scenario-farm-survival"],
                    },
                ],
                SourceStableIds = ["source:scenario-farm-survival"],
            },
            FarmSurvival = new SimulationFarmSurvivalInitialStateRequest
            {
                RuleRevision = SimulationFarmSurvivalCodes.RuleRevision,
                RegionStableId = "region:legal-dong:5176031000",
                AreaStableId = "area:sim.daegwallyeong-farm",
                TileKey = "kr5186:l2:438:419",
                FarmBuildingStableId = "building:sim.daegwallyeong-farmhouse",
                SupplyUnits = 8m,
                RepairMaterialUnits = 4m,
                Actors =
                [
                    new SimulationFarmActorInitialStateRequest
                    {
                        ActorStableId = Player,
                        ActorKindCode = SimulationFarmSurvivalCodes.Player,
                        KoreanName = "플레이어 생존자",
                    },
                    new SimulationFarmActorInitialStateRequest
                    {
                        ActorStableId = Npc,
                        ActorKindCode = SimulationFarmSurvivalCodes.Npc,
                        KoreanName = "농장 일꾼",
                    },
                ],
                SoilTiles =
                [
                    new SimulationFarmSoilTileInitialStateRequest
                    {
                        SoilTileStableId = SoilA,
                        GridX = 0,
                        GridY = 0,
                    },
                    new SimulationFarmSoilTileInitialStateRequest
                    {
                        SoilTileStableId = SoilB,
                        GridX = 0,
                        GridY = 1,
                    },
                ],
                Defenses =
                [
                    new SimulationFarmDefenseInitialStateRequest
                    {
                        DefenseStableId = "defense:sim:fence",
                        DefenseKindCode = SimulationFarmSurvivalCodes.Fence,
                        Durability = 60m,
                    },
                    new SimulationFarmDefenseInitialStateRequest
                    {
                        DefenseStableId = "defense:sim:storage-lock",
                        DefenseKindCode = SimulationFarmSurvivalCodes.StorageLock,
                        Durability = 80m,
                    },
                    new SimulationFarmDefenseInitialStateRequest
                    {
                        DefenseStableId = "defense:sim:lighting",
                        DefenseKindCode = SimulationFarmSurvivalCodes.Lighting,
                        Durability = 100m,
                    },
                    new SimulationFarmDefenseInitialStateRequest
                    {
                        DefenseStableId = "defense:sim:guard-post",
                        DefenseKindCode = SimulationFarmSurvivalCodes.GuardPost,
                        Durability = 100m,
                    },
                ],
            },
        };

    private static 경영SimulationSession생성Request CreateScenicRequest()
    {
        var request = CreateRequest();
        request.FarmSurvival!.RuleRevision =
            SimulationFarmSurvivalCodes.ScenicSeasonRuleRevision;
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SsalddelExecution:Mode"] = "Operational",
                            ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                            ["SimulationSharedPublicData:Enabled"] = "false",
                        });
                });
            });
}
