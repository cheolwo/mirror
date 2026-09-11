using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationBattleInstanceTests
{
    private const string Commander = "actor:sim:commander";
    private const string Manager = "actor:sim:manager";
    private const string NpcA = "actor:sim:defender-a";
    private const string NpcB = "actor:sim:defender-b";
    private const string SupplyStack = "item-stack:sim:battle-supply";
    private const string Team = "team:sim:parallel-battle";

    [Fact]
    public void 전투_processLocal_저장소_구현은_Infrastructure에_위치한다()
        => Assert.Equal(
            "Ssalddel.Simulation.Infrastructure",
            typeof(InMemorySimulationBattleInstanceStore).Namespace);

    [Fact]
    public void 전역개정이달라도_지역전투문맥과파생입력이같으면_확정한다()
    {
        var context = CreateContext();
        var ready = AdvanceToEncounter(context.SessionStore.Find(context.SessionId)!);
        var encounter = Encounter(ready);
        var battles = new SimulationBattleInstanceService(context.SessionStore,
            new FixedPolicyStore(Policy(context.SessionId)), context.BattleStore,
            new FixedBattlefieldDerivationService());
        var preview = battles.PreviewCreate(context.SessionId,
            new SimulationBattleCreatePreviewRequest
            {
                ExpectedWorldRevision = ready.Revision - 1,
                EncounterStableId = encounter.EncounterStableId,
                RequestingActorStableId = Commander,
            });

        var created = battles.ConfirmCreate(context.SessionId,
            new SimulationBattleCreateConfirmRequest
            {
                CommandId = "command:battle:local-hash-confirm",
                ExpectedWorldRevision = ready.Revision - 1,
                ExpectedBattleWorldContextHashSha256 = preview
                    .BattlefieldDerivation.WorldContext.ContextHashSha256,
                ExpectedBattlefieldDerivationInputHashSha256 = preview
                    .BattlefieldDerivation.BattlefieldDerivationInputHashSha256,
                EncounterStableId = encounter.EncounterStableId,
                RequestingActorStableId = Commander,
            });

        Assert.True(preview.CanConfirm);
        Assert.Equal("context-hash:local", created.BattlefieldDerivation
            .WorldContext.ContextHashSha256);
        Assert.Throws<SimulationConflictException>(() => battles.ConfirmCreate(
            context.SessionId, new SimulationBattleCreateConfirmRequest
            {
                CommandId = "command:battle:changed-local-input",
                ExpectedWorldRevision = ready.Revision,
                ExpectedBattleWorldContextHashSha256 = "context-hash:changed",
                ExpectedBattlefieldDerivationInputHashSha256 =
                    "derivation-hash:local",
                EncounterStableId = encounter.EncounterStableId,
                RequestingActorStableId = Commander,
            }));
    }

    [Fact]
    public void 전장사본은_부대예약_의미효과_저장재생을한폐루프로보존한다()
    {
        var state = new SimulationBattleInstanceState(SpatialCreation());
        var created = state.Snapshot();
        Assert.Contains(created.ParticipationReservations,
            value => value.ActorStableId == NpcA
                && value.StateCode ==
                    SimulationBattlefieldDerivationCodes.CommittedToBattle);
        Assert.Contains(created.WorldTargetReservations,
            value => value.WorldEffectTargetStableId == "defense:farm:fence");

        var active = state.ConfirmDeployment(new SimulationBattleDeploymentConfirmRequest
        {
            CommandId = "command:spatial:deploy",
            ExpectedBattleRevision = created.BattleRevision,
            ActorStableId = Commander,
            DeploymentCode = SimulationBattleInstanceCodes.Defensive,
        });
        var commanded = state.ConfirmTacticalCommand(
            new SimulationBattleTacticalCommandConfirmRequest
            {
                CommandId = "command:spatial:hold",
                ExpectedBattleRevision = active.BattleRevision,
                RequestingActorStableId = Commander,
                UnitStableId = "battle-unit:allied:000",
                CommandCode = SimulationBattlefieldDerivationCodes.Hold,
            });
        var completed = state.Advance(new SimulationBattleAdvanceRequest
        {
            CommandId = "command:spatial:finish",
            ExpectedBattleRevision = commanded.BattleRevision,
            CombatTickCount = SimulationBattleInstanceCodes.MaximumCombatTick,
        }, 5, 0);

        Assert.NotEmpty(completed.SemanticEffects);
        Assert.All(completed.SemanticEffects, value => Assert.Equal(
            SimulationBattlefieldDerivationCodes.Pending,
            value.ReconciliationStateCode));
        Assert.All(completed.SemanticEffects, value => Assert.StartsWith(
            "battle:spatial|", value.WorldEffectApplicationKey));

        var reconciled = state.Reconcile(6, 9);
        Assert.All(reconciled.ParticipationReservations, value => Assert.Equal(
            SimulationBattlefieldDerivationCodes.Released, value.StateCode));
        Assert.All(reconciled.SemanticEffects, value => Assert.Equal(
            SimulationBattlefieldDerivationCodes.Applied,
            value.ReconciliationStateCode));
        var restored = SimulationBattleInstanceState.Restore(state.CreateSaveRecord())
            .Snapshot();
        Assert.Equal(reconciled.BattlefieldDerivation.BattlefieldPlan
            .BattlefieldPlanHashSha256, restored.BattlefieldDerivation
            .BattlefieldPlan.BattlefieldPlanHashSha256);
        Assert.Equal(reconciled.UnitRoster.CombatSeedHashSha256,
            restored.UnitRoster.CombatSeedHashSha256);
        Assert.Equal(reconciled.SemanticEffects.Select(value =>
                value.WorldEffectApplicationKey),
            restored.SemanticEffects.Select(value => value.WorldEffectApplicationKey));
    }

    [Fact]
    public void 전투와WorldTick은_서로다른개정으로병렬진행되고_다음Tick에결과가합류한다()
    {
        var context = CreateContext();
        var ready = AdvanceToEncounter(context.SessionStore.Find(context.SessionId)!);
        var encounter = Encounter(ready);
        var battle = CreateBattle(context, ready.Revision, encounter);
        var active = context.Battles.ConfirmDeployment(context.SessionId,
            battle.BattleStableId, Deployment(battle.BattleRevision));

        var completed = context.Battles.Advance(context.SessionId,
            battle.BattleStableId, new SimulationBattleAdvanceRequest
            {
                CommandId = "command:battle:finish",
                ExpectedBattleRevision = active.BattleRevision,
                CombatTickCount = SimulationBattleInstanceCodes.MaximumCombatTick,
            });
        var worldBefore = context.SessionStore.Find(context.SessionId)!.Snapshot();
        Assert.Equal(SimulationBattleInstanceCodes.Completed, completed.PhaseCode);
        Assert.Equal(5, worldBefore.CurrentTick);

        var sessionService = new 경영SimulationSessionService(context.SessionStore,
            new InMemorySimulationSessionSaveStore(), context.Battles);
        var advanced = sessionService.Advance(context.SessionId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:world:while-battle-result-pending",
                ExpectedRevision = worldBefore.Revision,
                TickCount = 1,
            });
        var reconciled = context.Battles.Get(context.SessionId,
            battle.BattleStableId, Commander);

        Assert.Equal(6, advanced.CurrentTick);
        Assert.Equal(SimulationBattleInstanceCodes.Reconciled, reconciled.PhaseCode);
        Assert.Equal(6, reconciled.Outcome!.AppliedWorldTick);
        Assert.Equal(SimulationBattleInstanceCodes.Applied,
            reconciled.Outcome.ReconciliationStateCode);
        Assert.All(reconciled.ResourceReservations, value =>
            Assert.Equal(SimulationBattleInstanceCodes.Released, value.StateCode));
    }

    [Fact]
    public void 경영팀원은_전투중보급을보내고_관전자와위임분대는권한을구분한다()
    {
        var context = CreateContext();
        var ready = AdvanceToEncounter(context.SessionStore.Find(context.SessionId)!);
        var battle = CreateBattle(context, ready.Revision, Encounter(ready));
        var spectator = context.Battles.ConfirmParticipation(context.SessionId,
            battle.BattleStableId, new SimulationBattleParticipationConfirmRequest
            {
                CommandId = "command:battle:spectate",
                ExpectedBattleRevision = battle.BattleRevision,
                ExpectedTeamPolicyRevision = 7,
                ActorStableId = Manager,
                ParticipationRoleCode = SimulationBattleInstanceCodes.Spectator,
            });
        var preview = context.Battles.PreviewSupport(context.SessionId,
            battle.BattleStableId, new SimulationBattleSupportPreviewRequest
            {
                ExpectedWorldRevision = ready.Revision,
                ExpectedBattleRevision = spectator.BattleRevision,
                RequestingActorStableId = Manager,
                SupportCode = SimulationBattleInstanceCodes.SupplyCrate,
                SourceResourceStableId = SupplyStack,
            });
        var supported = context.Battles.ConfirmSupport(context.SessionId,
            battle.BattleStableId, new SimulationBattleSupportConfirmRequest
            {
                CommandId = "command:battle:supply",
                ExpectedWorldRevision = ready.Revision,
                ExpectedBattleRevision = spectator.BattleRevision,
                RequestingActorStableId = Manager,
                SupportCode = SimulationBattleInstanceCodes.SupplyCrate,
                SourceResourceStableId = SupplyStack,
            });

        Assert.True(preview.CanConfirm);
        Assert.Equal(8, preview.ProjectedStrengthBonus);
        Assert.True(Assert.Single(spectator.Participants,
            value => value.ActorStableId == Manager).PresentationOnly);
        Assert.False(Assert.Single(spectator.Participants,
            value => value.ActorStableId == Manager).CanControlWorldState);
        Assert.Contains(supported.ResourceReservations, value =>
            value.ResourceStableId == SupplyStack
            && value.StateCode == SimulationBattleInstanceCodes.Reserved);
        var inventory = new SimulationWorldSurvivalInventoryService(
            context.SessionStore, context.BattleStore);
        var locked = Assert.Throws<SimulationConflictException>(() =>
            inventory.ConfirmAcquisition(context.SessionId,
                new SimulationWorldItemAcquisitionConfirmRequest
                {
                    CommandId = "command:inventory:take-locked-support",
                    ExpectedRevision = ready.Revision,
                    PlayerStableId = Manager,
                    BuildingStableId = "building:daegwallyeong-warehouse",
                    ContainerStableId = "container:battle-support",
                    ItemStackStableId = SupplyStack,
                    Quantity = 1,
                }));
        Assert.Equal("BattleResourceLocked", locked.ErrorCode);

        var duplicate = context.Battles.PreviewSupport(context.SessionId,
            battle.BattleStableId, new SimulationBattleSupportPreviewRequest
            {
                ExpectedWorldRevision = ready.Revision,
                ExpectedBattleRevision = supported.BattleRevision,
                RequestingActorStableId = Manager,
                SupportCode = SimulationBattleInstanceCodes.SupplyCrate,
                SourceResourceStableId = SupplyStack,
            });
        Assert.False(duplicate.CanConfirm);
        Assert.Contains("BattleResourceLocked", duplicate.BlockingReasonCodes);
    }

    [Fact]
    public void 증원분대로예약한Npc는_같은시간농장노동에중복배치할수없다()
    {
        var context = CreateContext();
        var ready = AdvanceToEncounter(context.SessionStore.Find(context.SessionId)!);
        var battle = CreateBattle(context, ready.Revision, Encounter(ready));
        var supported = context.Battles.ConfirmSupport(context.SessionId,
            battle.BattleStableId, new SimulationBattleSupportConfirmRequest
            {
                CommandId = "command:battle:reinforcement",
                ExpectedWorldRevision = ready.Revision,
                ExpectedBattleRevision = battle.BattleRevision,
                RequestingActorStableId = Manager,
                SupportCode = SimulationBattleInstanceCodes.ReinforcementSquad,
                SourceResourceStableId = NpcB,
            });
        var farm = new SimulationFarmSurvivalService(
            context.SessionStore, context.BattleStore);
        var preview = farm.PreviewWork(context.SessionId,
            new SimulationFarmWorkPreviewRequest
            {
                ExpectedRevision = ready.Revision,
                ActorStableId = NpcB,
                TargetStableId = "soil-tile:0:0",
                ActionCode = SimulationFarmSurvivalCodes.Tilling,
                AssignmentKindCode = SimulationFarmSurvivalCodes.NpcDelegated,
            });
        var locked = Assert.Throws<SimulationConflictException>(() =>
            farm.ConfirmWork(context.SessionId,
                new SimulationFarmWorkConfirmRequest
                {
                    CommandId = "command:farm:duplicate-reinforcement",
                    ExpectedRevision = ready.Revision,
                    ActorStableId = NpcB,
                    TargetStableId = "soil-tile:0:0",
                    ActionCode = SimulationFarmSurvivalCodes.Tilling,
                    AssignmentKindCode = SimulationFarmSurvivalCodes.NpcDelegated,
                }));

        Assert.Contains(supported.ResourceReservations, value =>
            value.ResourceStableId == NpcB
            && value.StateCode == SimulationBattleInstanceCodes.Reserved);
        Assert.Contains(supported.Supports, value =>
            value.SourceResourceStableId == NpcB
            && value.SupportCode == SimulationBattleInstanceCodes.ReinforcementSquad);
        Assert.False(preview.CanConfirm);
        Assert.Contains("BattleResourceLocked", preview.BlockingReasonCodes);
        Assert.Equal("BattleResourceLocked", locked.ErrorCode);
    }

    [Fact]
    public void 같은영역과자원은_두전투가동시에예약할수없다()
    {
        var store = new InMemorySimulationBattleInstanceStore();
        var first = Creation("battle:one", "command:battle:one");
        store.CreateOrGet(first);
        var second = Creation("battle:two", "command:battle:two");

        var error = Assert.Throws<SimulationConflictException>(() =>
            store.CreateOrGet(second));
        Assert.Equal("BattleResourceLocked", error.ErrorCode);
    }

    [Fact]
    public void 같은Seed와명령열은_같은자동전투결과와ReplayHash를만든다()
    {
        var first = new SimulationBattleInstanceState(
            Creation("battle:deterministic", "command:battle:create"));
        var second = new SimulationBattleInstanceState(
            Creation("battle:deterministic", "command:battle:create"));

        var firstActive = first.ConfirmDeployment(Deployment(0));
        var secondActive = second.ConfirmDeployment(Deployment(0));
        var firstResult = first.Advance(new SimulationBattleAdvanceRequest
        {
            CommandId = "command:battle:auto",
            ExpectedBattleRevision = firstActive.BattleRevision,
            CombatTickCount = SimulationBattleInstanceCodes.MaximumCombatTick,
        }, 5, 0);
        var secondResult = second.Advance(new SimulationBattleAdvanceRequest
        {
            CommandId = "command:battle:auto",
            ExpectedBattleRevision = secondActive.BattleRevision,
            CombatTickCount = SimulationBattleInstanceCodes.MaximumCombatTick,
        }, 5, 0);

        Assert.Equal(firstResult.ReplayHashSha256, secondResult.ReplayHashSha256);
        Assert.Equal(firstResult.Outcome!.ResultCode, secondResult.Outcome!.ResultCode);
        Assert.True(firstResult.Outcome.UsedDeterministicAutoCommand);
    }

    [Fact]
    public void SaveReplay는_진행중전투와자원예약과멱등명령을동일하게복원한다()
    {
        var context = CreateContext();
        var ready = AdvanceToEncounter(context.SessionStore.Find(context.SessionId)!);
        var created = CreateBattle(context, ready.Revision, Encounter(ready));
        var supported = context.Battles.ConfirmSupport(context.SessionId,
            created.BattleStableId, new SimulationBattleSupportConfirmRequest
            {
                CommandId = "command:battle:save-support",
                ExpectedWorldRevision = ready.Revision,
                ExpectedBattleRevision = created.BattleRevision,
                RequestingActorStableId = Manager,
                SupportCode = SimulationBattleInstanceCodes.SupplyCrate,
                SourceResourceStableId = SupplyStack,
            });
        var deployed = context.Battles.ConfirmDeployment(context.SessionId,
            created.BattleStableId, Deployment(supported.BattleRevision));
        var advanceRequest = new SimulationBattleAdvanceRequest
        {
            CommandId = "command:battle:save-partial-advance",
            ExpectedBattleRevision = deployed.BattleRevision,
            CombatTickCount = 1200,
        };
        var advanced = context.Battles.Advance(context.SessionId,
            created.BattleStableId, advanceRequest);
        var sourceSessions = new 경영SimulationSessionService(context.SessionStore,
            context.SaveStore, context.Battles);
        var package = sourceSessions.Save(context.SessionId,
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:parallel-battle:active",
                ExpectedRevision = ready.Revision,
            });

        var targetSessionStore = new InMemory경영SimulationSessionStore();
        var targetPolicies = new InMemorySimulationTeamObservationPolicyStore();
        targetPolicies.Replace(Policy(context.SessionId));
        var targetBattleStore = new InMemorySimulationBattleInstanceStore();
        var targetBattles = new SimulationBattleInstanceService(targetSessionStore,
            targetPolicies, targetBattleStore);
        var targetSessions = new 경영SimulationSessionService(targetSessionStore,
            context.SaveStore, targetBattles);
        var restore = targetSessions.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = package.SaveStableId,
        });
        var restored = targetBattles.Get(context.SessionId,
            created.BattleStableId, Commander);
        var retried = targetBattles.Advance(context.SessionId,
            created.BattleStableId, advanceRequest);

        Assert.Single(package.Battles);
        Assert.Equal(3, package.Battles[0].AppliedCommands.Length);
        Assert.Equal(1, restore.RestoredBattleCount);
        Assert.Equal(advanced.ReplayHashSha256, restored.ReplayHashSha256);
        Assert.Equal(advanced.BattleRevision, restored.BattleRevision);
        Assert.Equal(advanced.CombatTick, restored.CombatTick);
        Assert.Contains(restored.ResourceReservations, value =>
            value.ResourceStableId == SupplyStack
            && value.StateCode == SimulationBattleInstanceCodes.Reserved);
        Assert.Equal(restored.ReplayHashSha256, retried.ReplayHashSha256);
        Assert.Equal(restored.BattleRevision, retried.BattleRevision);

        var tampered = SimulationSaveReplayCloner.ClonePackage(package);
        tampered.Battles[0].State.CombatTick++;
        var integrity = Assert.Throws<SimulationConflictException>(() =>
            SimulationSessionReplay.Restore(tampered));
        Assert.Equal("SimulationBattleSaveIntegrityMismatch", integrity.ErrorCode);
    }

    [Fact]
    public async Task Api는_전투Preview부터배치와자동판정까지제공한다()
    {
        using var factory = CreateFactory();
        var policies = factory.Services.GetRequiredService<
            InMemorySimulationTeamObservationPolicyStore>();
        var sessions = factory.Services.GetRequiredService<경영SimulationSessionService>();
        var created = sessions.Create(SessionRequest());
        policies.Replace(Policy(created.SessionStableId));
        var ready = sessions.Advance(created.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:to-encounter",
                ExpectedRevision = 0,
                TickCount = 5,
            });
        var encounter = Encounter(ready);
        using var client = factory.CreateClient();
        var path = "/api/simulation/v1/sessions/" + Uri.EscapeDataString(created.SessionStableId)
            + "/battles";

        using var previewResponse = await client.PostAsJsonAsync(path + "/previews",
            new SimulationBattleCreatePreviewRequest
            {
                ExpectedWorldRevision = ready.Revision,
                EncounterStableId = encounter.EncounterStableId,
                RequestingActorStableId = Commander,
            });
        var preview = await previewResponse.Content.ReadFromJsonAsync<
            SimulationBattleCreatePreviewSnapshot>();
        using var createResponse = await client.PostAsJsonAsync(path + "/confirm",
            new SimulationBattleCreateConfirmRequest
            {
                CommandId = "command:http:battle:create",
                ExpectedWorldRevision = ready.Revision,
                EncounterStableId = encounter.EncounterStableId,
                RequestingActorStableId = Commander,
            });
        var battle = await createResponse.Content.ReadFromJsonAsync<
            SimulationBattleInstanceSnapshot>();
        using var deployResponse = await client.PostAsJsonAsync(path + "/"
            + Uri.EscapeDataString(battle!.BattleStableId) + "/deployments/confirm",
            Deployment(battle.BattleRevision));

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview!.CanConfirm);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deployResponse.StatusCode);
    }

    private static TestContext CreateContext()
    {
        var sessionStore = new InMemory경영SimulationSessionStore();
        var saveStore = new InMemorySimulationSessionSaveStore();
        var sessions = new 경영SimulationSessionService(sessionStore, saveStore);
        var created = sessions.Create(SessionRequest());
        var policies = new InMemorySimulationTeamObservationPolicyStore();
        policies.Replace(Policy(created.SessionStableId));
        var battleStore = new InMemorySimulationBattleInstanceStore();
        var battles = new SimulationBattleInstanceService(sessionStore, policies,
            battleStore);
        return new TestContext(created.SessionStableId, sessions, sessionStore, battles,
            battleStore, saveStore);
    }

    private static 경영SimulationSessionSnapshot AdvanceToEncounter(
        경영SimulationSessionAggregate session) => session.Advance(
        new 경영SimulationTick진행Request
        {
            CommandId = "command:world:to-interactive-combat",
            ExpectedRevision = 0,
            TickCount = 5,
        });

    private static SimulationThreatEncounterSnapshot Encounter(
        경영SimulationSessionSnapshot snapshot) => snapshot.FarmSurvival!.Encounters.Single(value =>
        value.ThreatTypeCode == SimulationFarmSurvivalCodes.ZombiePressure);

    private static SimulationBattleInstanceSnapshot CreateBattle(TestContext context,
        long revision, SimulationThreatEncounterSnapshot encounter)
        => context.Battles.ConfirmCreate(context.SessionId,
            new SimulationBattleCreateConfirmRequest
            {
                CommandId = "command:battle:create",
                ExpectedWorldRevision = revision,
                EncounterStableId = encounter.EncounterStableId,
                RequestingActorStableId = Commander,
            });

    private static SimulationBattleDeploymentConfirmRequest Deployment(long revision) => new()
    {
        CommandId = "command:battle:deployment",
        ExpectedBattleRevision = revision,
        ActorStableId = Commander,
        DeploymentCode = SimulationBattleInstanceCodes.Defensive,
    };

    private static SimulationBattleCreationContext Creation(string battleId, string commandId) => new()
    {
        BattleStableId = battleId,
        SessionStableId = "simulation-session:parallel-test",
        EncounterStableId = "encounter:night-raid",
        AreaStableId = "area:daegwallyeong-farm",
        CommanderActorStableId = Commander,
        StartedWorldTick = 5,
        StartedWorldRevision = 1,
        ScenarioSeed = 20260815,
        AlliedStrength = 12,
        HostileStrength = 14,
        InitialResourceStableIds = ["building:farm", "battle-squad:initial"],
        ReinforcementCandidateStableIds = [NpcB],
        CreateCommandId = commandId,
    };

    private static SimulationBattleCreationContext SpatialCreation() => new()
    {
        BattleStableId = "battle:spatial",
        SessionStableId = "simulation-session:spatial",
        EncounterStableId = "encounter:spatial",
        AreaStableId = "area:daegwallyeong-farm",
        CommanderActorStableId = Commander,
        StartedWorldTick = 5,
        StartedWorldRevision = 8,
        ScenarioSeed = 20260820,
        AlliedStrength = 12,
        HostileStrength = 10,
        InitialResourceStableIds = ["defense:farm:fence", NpcA],
        BattlefieldDerivation = FixedDerivation(),
        UnitRoster = new SimulationBattleUnitRosterSnapshot
        {
            BattleUnitRosterHashSha256 = "roster-hash:spatial",
            CardModifierHashSha256 = "card-hash:spatial",
            CombatSeedHashSha256 = "combat-seed-hash:spatial",
            CombatSeed = 42,
            Units =
            [
                new SimulationBattleUnitSnapshot
                {
                    UnitStableId = "battle-unit:allied:000",
                    SideCode = SimulationFarmTacticalCombatCodes.Allied,
                    MemberActorStableIds = [NpcA],
                    MemberCount = 1,
                    CombatStrength = 8,
                },
                new SimulationBattleUnitSnapshot
                {
                    UnitStableId = "battle-unit:hostile:000",
                    SideCode = SimulationFarmTacticalCombatCodes.Hostile,
                    ThreatTypeCode = SimulationFarmSurvivalCodes.ZombiePressure,
                    MemberCount = 10,
                    CombatStrength = 10,
                },
            ],
        },
        CreateCommandId = "command:spatial:create",
    };

    private static SimulationBattlefieldDerivationSnapshot FixedDerivation() => new()
    {
        CanConfirm = true,
        BattlefieldDerivationInputHashSha256 = "derivation-hash:local",
        TacticalTerrainInputHashSha256 = "terrain-hash:local",
        WorldContext = new SimulationBattleWorldContextSnapshot
        {
            ContextHashSha256 = "context-hash:local",
            AnchorSetHashSha256 = "anchor-hash:local",
            Anchors =
            [
                new SimulationBattlefieldAnchorSnapshot
                {
                    BattlefieldAnchorStableId = "battle-anchor:fence",
                    SourceStableId = "defense:farm:fence",
                    WorldEffectTargetStableId = "defense:farm:fence",
                    PreservationPolicyCode =
                        SimulationBattlefieldDerivationCodes.Required,
                    AnchorTypeCodes =
                    [
                        SimulationBattlefieldDerivationCodes.Physical,
                        SimulationBattlefieldDerivationCodes.Objective,
                    ],
                },
            ],
        },
        BattlefieldPlan = new SimulationBattlefieldPlanSnapshot
        {
            BattlefieldPlanHashSha256 = "plan-hash:local",
            BattlefieldDerivationInputHashSha256 = "derivation-hash:local",
            BattlefieldSeedHashSha256 = "battlefield-seed-hash:local",
            BattlefieldPlanStableId = "battlefield-plan:local",
            ValidationCodes = [],
        },
    };

    private sealed class FixedBattlefieldDerivationService
        : ISimulationBattlefieldDerivationService
    {
        public SimulationBattlefieldDerivationSnapshot Derive(string sessionStableId,
            string encounterStableId, string areaStableId,
            long capturedWorldRevision, bool natureEncounter)
        {
            var value = FixedDerivation();
            value.SpatialOrigin.CapturedWorldRevision = capturedWorldRevision;
            return value;
        }
    }

    private sealed class FixedPolicyStore(SimulationTeamObservationPolicySnapshot value)
        : ISimulationTeamObservationPolicyStore
    {
        public SimulationTeamObservationPolicySnapshot? FindForObserver(
            string sessionStableId, string observerActorStableId) => value;
    }

    private static SimulationTeamObservationPolicySnapshot Policy(string sessionId) => new()
    {
        SessionStableId = sessionId,
        TeamStableId = Team,
        Revision = 7,
        MembersCanObserve = true,
        MemberActorStableIds = [Commander, Manager],
        AllowedViewModeCodes = [SimulationTeamObservationViewModeCodes.FirstPerson],
        ShowObserverIndicator = true,
        SimulationOnly = true,
    };

    private static 경영SimulationSession생성Request SessionRequest() => new()
    {
        ClientRequestId = Guid.Parse("bd6786c0-94ef-42c1-a852-dd07f746d7aa"),
        ScenarioStableId = "scenario:pyeongchang-parallel-battle",
        ScenarioDataRevision = "scenario-data:parallel-battle.r1",
        ScenarioSeed = 20260815,
        RuleRevision = "simulation:parallel-battle.r1",
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:pyeongchang-survivors",
            TerritoryStableId = "territory:pyeongchang",
            SettlementStableId = "settlement:daegwallyeong-farm",
            GameDateStartsOn = DateTimeOffset.Parse("2026-04-01T00:00:00Z"),
        },
        WorldInventory = new SimulationWorldInventoryInitialStateRequest
        {
            Buildings = [new SimulationWorldBuildingInteriorInitialStateRequest
            {
                BuildingStableId = "building:daegwallyeong-warehouse",
                TileKey = "kr5186:l2:438:419",
                RegionStableId = "region:legal-dong:5176031000",
                BuildingEvidenceKindCode = "ObservedFixture",
                SourceRecordStableId = "fixture:warehouse:1",
                InteriorSpaceStableId = "interior:warehouse:1",
            }],
            Players = [new SimulationWorldPlayerInitialStateRequest
            {
                PlayerStableId = Manager,
                CurrentBuildingStableId = "building:daegwallyeong-warehouse",
                InventoryCapacityUnits = 20,
                ManagedContainerStableIds = ["container:battle-support"],
            }],
            Containers = [new SimulationWorldContainerInitialStateRequest
            {
                ContainerStableId = "container:battle-support",
                BuildingStableId = "building:daegwallyeong-warehouse",
                InteriorSpaceStableId = "interior:warehouse:1",
                CapacityUnits = 20,
                ManagerPlayerStableIds = [Manager],
            }],
            ItemStacks = [new SimulationWorldItemStackInitialStateRequest
            {
                ItemStackStableId = SupplyStack,
                ContainerStableId = "container:battle-support",
                ItemCode = "battle-supply-crate",
                KoreanName = "전투 보급 상자",
                Quantity = 3,
                UnitCode = "box",
                BuildingItemRelationStableId = "building-item:warehouse:battle-supply",
            }],
        },
        FarmSurvival = new SimulationFarmSurvivalInitialStateRequest
        {
            RuleRevision = SimulationFarmSurvivalCodes.HeroTacticalCombatRuleRevision,
            RegionStableId = "region:legal-dong:5176031000",
            AreaStableId = "area:daegwallyeong-farm",
            TileKey = "kr5186:l2:438:419",
            FarmBuildingStableId = "building:daegwallyeong-farm",
            SupplyUnits = 8,
            RepairMaterialUnits = 4,
            Actors =
            [
                new SimulationFarmActorInitialStateRequest
                { ActorStableId = Commander, ActorKindCode = SimulationFarmSurvivalCodes.Player,
                    KoreanName = "전투 지휘자" },
                new SimulationFarmActorInitialStateRequest
                { ActorStableId = NpcA, ActorKindCode = SimulationFarmSurvivalCodes.Npc,
                    KoreanName = "농장 수비대 A" },
                new SimulationFarmActorInitialStateRequest
                { ActorStableId = NpcB, ActorKindCode = SimulationFarmSurvivalCodes.Npc,
                    KoreanName = "농장 수비대 B" },
            ],
            SoilTiles = [new SimulationFarmSoilTileInitialStateRequest
            { SoilTileStableId = "soil-tile:0:0", GridX = 0, GridY = 0 }],
            Defenses = [new SimulationFarmDefenseInitialStateRequest
            { DefenseStableId = "defense:farm:fence", DefenseKindCode = SimulationFarmSurvivalCodes.Fence,
                Durability = 80, Prepared = true }],
        },
    };

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SsalddelExecution:Mode"] = "Operational",
                    ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                    ["SimulationSharedPublicData:Enabled"] = "false",
                }));
        });

    private sealed record TestContext(string SessionId,
        경영SimulationSessionService Sessions,
        InMemory경영SimulationSessionStore SessionStore,
        SimulationBattleInstanceService Battles,
        InMemorySimulationBattleInstanceStore BattleStore,
        InMemorySimulationSessionSaveStore SaveStore);
}
