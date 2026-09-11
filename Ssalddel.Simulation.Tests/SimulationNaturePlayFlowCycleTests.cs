using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.WorkflowRules;
using Xunit;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E4,
    "Nature 발산 획득과 수렴 정책·NPC 위임이 현장 보급으로 다시 연결되는 첫 플레이 순환을 검증한다.",
    WorkOrderIds = new[] { "E9-WO-NATURE-AREA-BUILDING-PROGRESSION" },
    Boundary = "자동 시험은 실제 SimulationWorldShell 배치·Play Mode·Game View 증거가 아니다.")]
public sealed class SimulationNaturePlayFlowCycleTests
{
    [Fact]
    public async System.Threading.Tasks.Task
        SoloLocalProcess는_네트워크없이NatureNpc루틴조회계약을사용한다()
    {
        using var runtime = new LocalSimulationRuntime(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(),
            new 사용하지않는NatureLocalSaveSlotStore());
        var created = await runtime.Sessions.CreateAsync(CreateRequest());
        var projection = await runtime.Sessions.GetNpcRoutineWorkAsync(
            created.SessionStableId, "Nature");

        Assert.Equal(SimulationAuthorityLocation.LocalProcess,
            runtime.Descriptor.AuthorityLocation);
        Assert.False(runtime.Descriptor.RequiresNetwork);
        Assert.Equal(SimulationNatureSurvivalCodes
            .PrepareFieldSupplyDelegatedWorldInteractionId,
            Assert.Single(projection).WorldInteractionId);
        Assert.Equal("현장 보급 제작 업무 위임",
            Assert.Single(projection).WorldInteractionName);
    }

    [Fact]
    public async System.Threading.Tasks.Task
        NatureNpc루틴조회는_Http에서도원자료나운영상태없이동일계약을노출한다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        var created = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        var projection = await client.GetFromJsonAsync<
            SimulationNpcRoutineWorkProjection[]>(
            $"/api/simulation/v1/sessions/{created.SessionStableId}"
            + "/npc-routine-work?areaCode=Nature");
        var nature = Assert.Single(projection!);
        Assert.Equal(SimulationNatureSurvivalCodes
            .PrepareFieldSupplyDelegatedWorldInteractionId,
            nature.WorldInteractionId);
        Assert.Equal("자연 탐사·생활 거점 · 현장 보급 제작 업무 위임 (WI-NATURE-17)",
            nature.WorldInteractionDisplayName);
        Assert.Equal(SimulationWorldInteractionOriginCodes.SimulationNative,
            nature.OriginCode);
        Assert.Equal(SimulationWorldInteractionControlPolicyCodes.NpcRoutine,
            nature.ControlPolicyCode);
        Assert.False(created.IsOperationalState);
    }

    [Fact]
    public void Nature현장보급은_직접제작을보존하고_정책선택뒤Npc위임으로순환한다()
    {
        var session = BuildNatureFieldSupplyReadySession();
        var opportunities = session.GetNaturePlayerOpportunities();
        var direct = opportunities.Single(value => value.WorldInteractionId ==
            SimulationNatureSurvivalCodes.PrepareFieldSupplyWorldInteractionId);
        var delegated = opportunities.Single(value => value.WorldInteractionId ==
            SimulationNatureSurvivalCodes
                .PrepareFieldSupplyDelegatedWorldInteractionId);

        Assert.Equal(Simulation플레이흐름Codes.순환연결부,
            direct.PlayerFlowCode);
        Assert.Equal(Simulation플레이흐름Codes.수렴,
            delegated.PlayerFlowCode);
        Assert.Equal(Simulation플레이흐름Codes.발산,
            delegated.NextPlayerFlowCode);
        Assert.Equal(Simulation플레이흐름인계Codes.수렴에서발산,
            delegated.CycleHandoffCode);
        Assert.Equal("현장 보급 꾸러미 제작",
            direct.WorldInteractionName);
        Assert.Equal("현장 보급 제작 업무 위임",
            delegated.WorldInteractionName);
        Assert.Equal("NatureFieldSupplyPackAdded", direct.PrimaryOutcomeCode);
        Assert.Equal("NpcFieldSupplyPolicySelected",
            delegated.PrimaryOutcomeCode);
        Assert.Equal("LegacyCompositeMigrationRequired",
            direct.SingleResponsibilityAssessmentCode);
        Assert.Equal("LegacyCompositeMigrationRequired",
            delegated.SingleResponsibilityAssessmentCode);
        Assert.True(direct.Available);
        Assert.False(delegated.Available);
        Assert.Contains("SimulationNpcAutomationDisabled",
            delegated.BlockReasonCodes);

        EnableNatureDelegation(session, "enable");
        Advance(session, 2, false, "delegated-half");

        var working = Assert.Single(session.GetNpcRoutineWork("Nature"));
        Assert.Equal(SimulationNpcActionPhaseCodes.Working,
            working.PhaseCode);
        Assert.Equal(.5m, working.ProgressRate);
        Assert.Equal(PyeongchangSimulationNpcStableIds.Nature보급담당,
            working.NpcActorStableId);
        Assert.Equal(SimulationWorldInteractionOriginCodes.SimulationNative,
            working.OriginCode);
        Assert.Equal(SimulationWorldInteractionTriggerSourceCodes.NpcDriven,
            working.TriggerSourceCode);
        Assert.Equal("자연 탐사·생활 거점 · 현장 보급 제작 업무 위임 (WI-NATURE-17)",
            working.WorldInteractionDisplayName);
        Assert.Equal("NpcFieldSupplyPolicySelected",
            working.PrimaryOutcomeCode);
        Assert.Equal("LegacyCompositeMigrationRequired",
            working.SingleResponsibilityAssessmentCode);

        var directWhileDelegated = session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.PrepareFieldSupply,
            TargetStableId =
                Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
        });
        Assert.False(directWhileDelegated.CanConfirm);
        Assert.Contains(SimulationNatureSurvivalCodes.ActionBlocked,
            directWhileDelegated.BlockReasonCodes);

        Advance(session, 2, false, "delegated-complete");
        var completed = session.Snapshot();
        Assert.Equal(1, completed.NatureSurvival.FieldSupplyPackQuantity);
        var assignment = Assert.Single(completed.NpcTaskAssignments, value =>
            value.ActionCode ==
            SimulationNpcActionCodes.NatureFieldSupplyPreparation);
        Assert.Equal(SimulationNpcActionPhaseCodes.Completed,
            assignment.PhaseCode);
        Assert.Equal(PyeongchangSimulationNpcStableIds.Nature보급담당,
            assignment.ActorStableId);
        var delegatedExecution = Assert.Single(completed.NpcRoutineExecutions,
            value =>
            value.WorldInteractionId == SimulationNatureSurvivalCodes
                .PrepareFieldSupplyDelegatedWorldInteractionId
            && value.TaskStableId == assignment.TaskStableId);
        Assert.Equal(SimulationWI사분면Codes.YinNpc,
            delegatedExecution.음양주체분류.사분면Code);
        Assert.Equal("--", delegatedExecution.음양주체분류.사분면기호);
        Assert.Contains(completed.NpcWorkRecords, value =>
            value.TaskStableId == assignment.TaskStableId
            && value.ResultCodes.Contains("NatureFieldSupplyPackAdded"));

        var afterCompletion = Assert.Single(
            session.GetNpcRoutineWork("Nature"));
        Assert.Contains(SimulationNatureSurvivalCodes
            .FieldSupplyAlreadyAvailable, afterCompletion.BlockReasonCodes);

        var saved = session.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-play-flow-cycle",
            ExpectedRevision = session.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V23, saved.SchemaVersion);
        var restored = SimulationSessionReplay.Restore(saved);
        var savedAgain = restored.CreateSavePackage(new()
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(saved.ReplayHash, savedAgain.ReplayHash);
        Assert.Equal(1,
            restored.GetNatureSurvivalState().FieldSupplyPackQuantity);
    }

    [Fact]
    public void NatureNpc위임취소는_재료를반환하고_직접제작경로를다시연다()
    {
        var session = BuildNatureFieldSupplyReadySession();
        var before = session.GetNatureSurvivalState();
        EnableNatureDelegation(session, "cancel-enable");
        Advance(session, 1, false, "cancel-start");

        Confirm(session, "command:nature-flow:cancel",
            SimulationNatureSurvivalCodes.CancelActiveWork,
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint);
        DisableNatureDelegation(session);
        Advance(session, 1, false, "after-cancel-policy-off");

        var after = session.GetNatureSurvivalState();
        Assert.Null(after.ActiveWork);
        Assert.Equal(0, after.FieldSupplyPackQuantity);
        Assert.Equal(before.TimberQuantity + before.StoredTimberQuantity,
            after.TimberQuantity + after.StoredTimberQuantity);
        Assert.Equal(before.RebuildPartQuantity,
            after.RebuildPartQuantity);
        Assert.Equal(SimulationNpcActionPhaseCodes.Cancelled,
            session.Snapshot().NpcTaskAssignments.Single(value =>
                value.ActionCode == SimulationNpcActionCodes
                    .NatureFieldSupplyPreparation).PhaseCode);

        var direct = session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.PrepareFieldSupply,
            TargetStableId =
                Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
        });
        Assert.True(direct.CanConfirm,
            string.Join(",", direct.BlockReasonCodes));
    }

    private static 경영SimulationSessionAggregate
        BuildNatureFieldSupplyReadySession()
    {
        var request = CreateRequest();
        var sessionId = "simulation-session:"
            + request.ClientRequestId.ToString("N");
        request.ScenarioSeed = Enumerable.Range(1, 10_000).First(seed =>
            NatureSurvivalRules.RollFirstDuskEncounter(seed, sessionId, 1, 16));
        var session = new 경영SimulationSessionAggregate(request);

        for (var index = 1; index <= 13; index++)
        {
            Confirm(session, $"command:nature-flow:harvest:{index}",
                SimulationNatureSurvivalCodes.BeginHarvest,
                $"resource:nature-flow-tree:{index:00}");
            Advance(session, NatureSurvivalRules.HarvestWorkSeconds, true,
                "harvest:" + index);
        }
        Confirm(session, "command:nature-flow:cabin-place",
            SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
            "facility:nature-cabin", 2, -2);
        Confirm(session, "command:nature-flow:cabin-build",
            SimulationNatureSurvivalCodes.BeginCabinBuild,
            "facility:nature-cabin");
        Advance(session, NatureSurvivalRules.CabinWorkSeconds, true, "cabin");
        Confirm(session, "command:nature-flow:cabin-enter",
            SimulationNatureSurvivalCodes.EnterCabin,
            "facility:nature-cabin");
        Confirm(session, "command:nature-flow:cabin-store",
            SimulationNatureSurvivalCodes.StoreAtCabin,
            SimulationNatureSurvivalCodes.CabinStorageContainerStableId);

        AdvanceTo(session, NatureSurvivalRules.DaylightEndsAtSecond + 1);
        var encounter = session.GetNatureSurvivalState().Encounter!;
        Confirm(session, "command:nature-flow:encounter-fight",
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, choice: SimulationNatureSurvivalCodes.Fight);
        session.ConfirmNatureSurvivalAction(new()
        {
            CommandId = "command:nature-flow:encounter-victory",
            ExpectedRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.ResolveEncounter,
            TargetStableId = encounter.EncounterStableId,
            ChoiceCode = SimulationNatureSurvivalCodes.Victory,
            AuthoritativeRewardBonusQuantity = 2,
        });

        AdvanceTo(session, NatureSurvivalRules.DuskEndsAtSecond);
        Confirm(session, "command:nature-flow:sleep",
            SimulationNatureSurvivalCodes.SleepInCabin,
            "facility:nature-cabin");
        Advance(session, 60, false, "sleep-to-dawn");
        Confirm(session, "command:nature-flow:priority",
            SimulationNatureSurvivalCodes.SelectExpansionPlan,
            choice: SimulationNatureSurvivalCodes.Workbench);
        BuildWorkbench(session);
        return session;
    }

    private static 경영SimulationSession생성Request CreateRequest()
        => new()
        {
            ClientRequestId =
                Guid.Parse("dcf1016b-46b1-43e0-a82d-e38d3302e7ce"),
            ScenarioStableId = "scenario:nature-play-flow-cycle",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 1234,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            NpcRoutineControlRevision =
                SimulationNpcRoutineControlRevisionCodes.R3,
            NpcWorkforce =
                PyeongchangSimulationNpcWorkforceFixture
                    .CreateNatureFieldSupplyFixture(),
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:solo",
                TerritoryStableId = "territory:nature",
                SettlementStableId = "settlement:nature-home",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 25, 0, 0, 0,
                    TimeSpan.Zero),
            },
            NatureSurvival = new SimulationNatureSurvivalInitialStateRequest
            {
                ProfileRevision =
                    SimulationNatureSurvivalCodes.ProfileRevisionR4,
                PlayerStableId = "player:solo",
                InventoryCapacityUnits = 64,
                BuildingProgressionCatalog =
                    Simulation영역건물발전Catalog.CreateDefault(),
                ResourceNodes = Enumerable.Range(1, 16).Select(index =>
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId =
                            $"resource:nature-flow-tree:{index:00}",
                        H2StableId =
                            SimulationNatureSurvivalCodes.HarvestH2StableId,
                        H1StableId =
                            "h1-stock:nature-exploration-buffer",
                        LocalX = -16 + index * 2,
                        LocalZ = 10,
                    }).ToArray(),
            },
        };

    private static void BuildWorkbench(
        경영SimulationSessionAggregate session)
    {
        Confirm(session, "command:nature-flow:workbench",
            SimulationNatureSurvivalCodes.BeginBuildingConstruction,
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
            10, -2);
        Advance(session, 20, true, "workbench");
    }

    private static void EnableNatureDelegation(
        경영SimulationSessionAggregate session, string suffix)
        => session.UpdateNpcPolicy(new SimulationNpcPolicyChangeRequest
        {
            CommandId = "command:nature-flow:policy:" + suffix,
            ExpectedRevision = session.Revision,
            PolicyStableId =
                PyeongchangSimulationNpcStableIds.Nature현장보급정책,
            AutomationEnabled = true,
            Priority = 100,
            PreferredActorStableId =
                PyeongchangSimulationNpcStableIds.Nature보급담당,
            AutoDelegationEnabled = true,
        });

    private static void DisableNatureDelegation(
        경영SimulationSessionAggregate session)
        => session.UpdateNpcPolicy(new SimulationNpcPolicyChangeRequest
        {
            CommandId = "command:nature-flow:policy:disable",
            ExpectedRevision = session.Revision,
            PolicyStableId =
                PyeongchangSimulationNpcStableIds.Nature현장보급정책,
            AutomationEnabled = false,
            Priority = 100,
            PreferredActorStableId =
                PyeongchangSimulationNpcStableIds.Nature보급담당,
            AutoDelegationEnabled = false,
        });

    private static void Confirm(경영SimulationSessionAggregate session,
        string commandId, string action, string target = "", double x = 0,
        double z = 0, string choice = "")
        => session.ConfirmNatureSurvivalAction(new()
        {
            CommandId = commandId,
            ExpectedRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = action,
            TargetStableId = target,
            ChoiceCode = choice,
            LocalX = x,
            LocalZ = z,
        });

    private static void Advance(경영SimulationSessionAggregate session,
        int seconds, bool workHeld, string suffix)
        => session.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:nature-flow:clock:" + suffix + ":"
                + session.Revision,
            ExpectedRevision = session.Revision,
            ElapsedRealtimeSeconds = seconds,
            WorkInputHeld = workHeld,
        });

    private static void AdvanceTo(경영SimulationSessionAggregate session,
        int targetSecond)
    {
        while (session.GetNatureSurvivalState().ElapsedSecondsInCycle <
               targetSecond)
        {
            var remaining = targetSecond
                - session.GetNatureSurvivalState().ElapsedSecondsInCycle;
            Advance(session, Math.Min(60, remaining), false,
                "to:" + targetSecond);
        }
    }

    private sealed class 사용하지않는NatureLocalSaveSlotStore
        : ISimulationLocalSaveSlotStore
    {
        public void Write(string slotStableId,
            SimulationSessionSavePackage package)
            => throw new NotSupportedException();

        public SimulationLocalSaveSlotPackage Read(string slotStableId)
            => throw new NotSupportedException();
    }
}
