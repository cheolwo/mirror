using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.WorkflowRules;
using Xunit;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E3,
    "영역 건물 카탈로그·Nature 건설·학습·저장·HTTP의 결정적 회귀를 검증한다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3저장재생검증,
    WorkOrderIds = new[] { "E9-WO-NATURE-AREA-BUILDING-PROGRESSION" },
    Boundary = "자동 Fixture는 실제 SimulationWorldShell 배치·Play Mode·Game View 증거가 아니다.")]
public sealed class SimulationAreaBuildingProgressionTests
{
    [Fact]
    public void 기본대장은_다섯영역의_독립누적테크트리와_결정적hash를가진다()
    {
        var first = Simulation영역건물발전Catalog.CreateDefault();
        var second = Simulation영역건물발전Catalog.CreateDefault();

        Assert.Equal(first.HashSha256, second.HashSha256);
        Assert.Equal(new[] { "City", "Farm", "Hub", "Nature", "Town" },
            first.Blueprints.Select(value => value.AreaCode).Distinct()
                .OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(first.Blueprints.Length,
            first.Blueprints.Select(value => value.BlueprintStableId)
                .Distinct(StringComparer.Ordinal).Count());
        Assert.All(first.Blueprints.GroupBy(value => value.AreaCode), group =>
            Assert.Contains(group, value => value.StageCode ==
                Simulation영역건물발전Codes.Landmark));
        Assert.All(first.ApprovedTeachingMaterials,
            value => Assert.True(value.AdminApproved));
        Assert.Contains(first.ApprovedTeachingMaterials,
            value => value.TopicCode == "Reflection"
                && value.ViewpointAndLimitations.Contains("우열",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void 대장은_hash불일치와_순환선행조건을거부한다()
    {
        var badHash = Simulation영역건물발전Catalog.CreateDefault();
        badHash.HashSha256 = new string('0', 64);
        var hashError = Assert.Throws<SimulationContractException>(() =>
            Simulation영역건물발전Catalog.Validate(badHash));
        Assert.Equal(Simulation영역건물발전Codes.CatalogHashMismatch,
            hashError.ErrorCode);

        var cycle = Simulation영역건물발전Catalog.CreateDefault();
        var workbench = cycle.Blueprints.Single(value =>
            value.BlueprintStableId ==
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint);
        var lodge = cycle.Blueprints.Single(value =>
            value.BlueprintStableId ==
            Simulation영역건물발전Codes.NatureLearningLodgeBlueprint);
        workbench.RequiredOperationalBlueprintStableIds =
            new[] { lodge.BlueprintStableId };
        cycle.HashSha256 = Simulation영역건물발전Catalog.CalculateHash(cycle);
        var cycleError = Assert.Throws<SimulationContractException>(() =>
            Simulation영역건물발전Catalog.Validate(cycle));
        Assert.Equal(Simulation영역건물발전Codes.CatalogInvalid,
            cycleError.ErrorCode);
    }

    [Fact]
    public void Nature는_우선순위와무관하게_작업대뒤배움터를누적건설하고_Npc학습을기록한다()
    {
        var session = BuildDay2ReadySession();
        var initial = session.GetAreaBuildingProgression("Nature");
        Assert.Equal(Simulation영역건물발전Codes.CatalogRevision,
            initial.CatalogRevision);
        Assert.True(initial.Nodes.Single(value => value.BlueprintStableId ==
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint)
            .IsDay2Priority);
        Assert.Equal(Simulation영역건물발전Codes.Locked,
            initial.Nodes.Single(value => value.BlueprintStableId ==
                Simulation영역건물발전Codes.NatureLearningLodgeBlueprint)
                .StateCode);

        Build(session, Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
            10, -2, 20, "workbench");
        var afterWorkbench = session.GetAreaBuildingProgression("Nature");
        Assert.Equal(Simulation영역건물발전Codes.Available,
            afterWorkbench.Nodes.Single(value => value.BlueprintStableId ==
                Simulation영역건물발전Codes.NatureLearningLodgeBlueprint)
                .StateCode);

        Build(session, Simulation영역건물발전Codes.NatureLearningLodgeBlueprint,
            -8, -2, 60, "learning-lodge");
        Advance(session, 10, false, "enter-next-daylight");
        Assert.Equal("Learning",
            session.GetNatureSurvivalState().LearningVisit!.StateCode);
        Advance(session, 30, false, "complete-learning-visit");
        var completed = session.GetNatureSurvivalState();
        Assert.Equal("Completed", completed.LearningVisit!.StateCode);
        Assert.Equal(1, completed.BuildingProgression!.Nodes.Single(value =>
            value.BlueprintStableId ==
            Simulation영역건물발전Codes.NatureLearningLodgeBlueprint)
            .CompletedLearningVisitCount);

        var package = session.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-area-building-progression",
            ExpectedRevision = session.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V19, package.SchemaVersion);

        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new()
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal("Completed",
            restored.GetNatureSurvivalState().LearningVisit!.StateCode);
    }

    [Fact]
    public void Nature건설취소는_예약재료를반환하고_다른가지를잠그지않는다()
    {
        var session = BuildDay2ReadySession();
        var before = session.GetNatureSurvivalState();
        var preview = session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginBuildingConstruction,
            TargetStableId = Simulation영역건물발전Codes.NatureStorageRackBlueprint,
            LocalX = 10,
            LocalZ = -2,
        });
        Assert.True(preview.CanConfirm);
        Confirm(session, "command:building:cancel:start",
            SimulationNatureSurvivalCodes.BeginBuildingConstruction,
            Simulation영역건물발전Codes.NatureStorageRackBlueprint, 10, -2);

        Confirm(session, "command:building:cancel:confirm",
            SimulationNatureSurvivalCodes.CancelActiveWork,
            Simulation영역건물발전Codes.NatureStorageRackBlueprint);
        var after = session.GetNatureSurvivalState();
        Assert.Equal(before.TimberQuantity + before.StoredTimberQuantity,
            after.TimberQuantity + after.StoredTimberQuantity);
        Assert.Equal(before.RebuildPartQuantity, after.RebuildPartQuantity);
        Assert.Equal(Simulation영역건물발전Codes.Available,
            after.BuildingProgression!.Nodes.Single(value =>
                value.BlueprintStableId ==
                Simulation영역건물발전Codes.NaturePalisadeBlueprint).StateCode);
    }

    [Fact]
    public void Nature현장보급은_작업대제작취소원정선택패배보호와_v20재생을닫는다()
    {
        var session = BuildDay2ReadySession(
            SimulationNatureSurvivalCodes.ProfileRevisionR4);
        var blocked = session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.PrepareFieldSupply,
            TargetStableId =
                Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
        });
        Assert.False(blocked.CanConfirm);
        Assert.Contains(SimulationNatureSurvivalCodes.WorkbenchRequired,
            blocked.BlockReasonCodes);

        Build(session, Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
            10, -2, 20, "field-supply-workbench");
        var beforePreview = session.GetNatureSurvivalState();
        var revisionBeforePreview = session.Revision;
        var preview = session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.PrepareFieldSupply,
            TargetStableId =
                Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
        });
        Assert.True(preview.CanConfirm, string.Join(",", preview.BlockReasonCodes));
        Assert.Equal(Simulation플레이어활동경로Codes.AreaManufacturing,
            preview.PlayerActivityTrackCode);
        Assert.Equal(2, preview.RequiredTimberQuantity);
        Assert.Equal(1, preview.RequiredRebuildPartQuantity);
        Assert.Equal(4, preview.RequiredWorkSeconds);
        Assert.Equal(revisionBeforePreview, session.Revision);
        Assert.Equal(beforePreview.TimberQuantity,
            session.GetNatureSurvivalState().TimberQuantity);

        var craft = new SimulationNatureSurvivalCommandRequest
        {
            CommandId = "command:r4:field-supply:cancelled-start",
            ExpectedRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.PrepareFieldSupply,
            TargetStableId =
                Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
        };
        var started = session.ConfirmNatureSurvivalAction(craft);
        var duplicate = session.ConfirmNatureSurvivalAction(craft);
        Assert.Equal(started.Revision, duplicate.Revision);
        Assert.Equal(SimulationNatureSurvivalCodes.FieldSupplyCraft,
            started.NatureSurvival.ActiveWork!.WorkKindCode);

        Confirm(session, "command:r4:field-supply:cancel",
            SimulationNatureSurvivalCodes.CancelActiveWork,
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint);
        var afterCancel = session.GetNatureSurvivalState();
        Assert.Equal(beforePreview.TimberQuantity + beforePreview.StoredTimberQuantity,
            afterCancel.TimberQuantity + afterCancel.StoredTimberQuantity);
        Assert.Equal(beforePreview.RebuildPartQuantity,
            afterCancel.RebuildPartQuantity);

        Confirm(session, "command:r4:field-supply:start",
            SimulationNatureSurvivalCodes.PrepareFieldSupply,
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint);
        Advance(session, SimulationNatureSurvivalCodes.FieldSupplyCraftSeconds,
            true, "field-supply-complete");
        Assert.Equal(1, session.GetNatureSurvivalState().FieldSupplyPackQuantity);

        var opportunity = session.GetNaturePlayerOpportunities();
        Assert.Contains(opportunity, value => value.WorldInteractionId ==
            SimulationNatureSurvivalCodes.PrepareFieldSupplyWorldInteractionId
            && value.PlayerActivityTrackCode ==
            Simulation플레이어활동경로Codes.AreaManufacturing);
        Assert.All(session.GetNatureAreaNeeds(), value =>
            Assert.Equal(SimulationNatureSurvivalCodes.AreaSetStableId,
                value.AreaSetStableId));

        var tree = session.GetNatureSurvivalState().ResourceNodes
            .First(value => value.StateCode == SimulationNatureSurvivalCodes.Standing);
        var expeditionPreview = session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = tree.ResourceNodeStableId,
            ChoiceCode = SimulationNatureSurvivalCodes.UseFieldSupplyPack,
        });
        Assert.True(expeditionPreview.CanConfirm,
            string.Join(",", expeditionPreview.BlockReasonCodes));
        Assert.Equal(Simulation플레이어활동경로Codes.FieldExpedition,
            expeditionPreview.PlayerActivityTrackCode);
        Confirm(session, "command:r4:prepared-expedition:start",
            SimulationNatureSurvivalCodes.BeginHarvest, tree.ResourceNodeStableId,
            choice: SimulationNatureSurvivalCodes.UseFieldSupplyPack);
        Assert.True(session.GetNatureSurvivalState().ExpeditionPrepared);
        Assert.Equal(0, session.GetNatureSurvivalState().FieldSupplyPackQuantity);
        Advance(session, NatureSurvivalRules.HarvestWorkSeconds, true,
            "prepared-expedition-harvest");

        var preparedPackage = session.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-field-supply-prepared",
            ExpectedRevision = session.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V20,
            preparedPackage.SchemaVersion);
        var restored = SimulationSessionReplay.Restore(preparedPackage);
        Assert.True(restored.GetNatureSurvivalState().ExpeditionPrepared);
        Assert.Equal(preparedPackage.ReplayHash,
            restored.CreateSavePackage(new()
            {
                SaveStableId = preparedPackage.SaveStableId,
                ExpectedRevision = restored.Revision,
            }).ReplayHash);

        AdvanceToCycleSecond(session, 1,
            NatureSurvivalRules.DaylightEndsAtSecond + 1);
        var encounter = session.GetNatureSurvivalState().Encounter!;
        Assert.Equal(SimulationNatureSurvivalCodes.Pending, encounter.StateCode);
        Confirm(session, "command:r4:prepared-expedition:fight",
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, choice: SimulationNatureSurvivalCodes.Fight);
        var beforeDefeat = session.GetNatureSurvivalState();
        Confirm(session, "command:r4:prepared-expedition:defeat",
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, choice: SimulationNatureSurvivalCodes.Defeat);
        var defeated = session.GetNatureSurvivalState();
        Assert.False(defeated.ExpeditionPrepared);
        Assert.Equal(SimulationNatureSurvivalCodes.RebuildPartItemCode,
            defeated.LastProtectedMaterialItemCode);
        Assert.Equal(beforeDefeat.RebuildPartQuantity,
            defeated.RebuildPartQuantity);
        Assert.Equal(beforeDefeat.TimberQuantity
            - (beforeDefeat.TimberQuantity / 2), defeated.TimberQuantity);
    }

    [Fact]
    public async System.Threading.Tasks.Task HTTP통합조회는_세션에동결된_영역건물대장만반환한다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var request = CreateR3Request(
            SimulationNatureSurvivalCodes.ProfileRevisionR4);
        request.ClientRequestId = Guid.NewGuid();
        var create = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var session = await create.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>();

        var response = await client.GetAsync(
            $"/api/simulation/v1/sessions/{session!.SessionStableId}"
            + "/nature-survival/building-progression/Town");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var progression = await response.Content.ReadFromJsonAsync<
            Simulation영역건물발전Snapshot>();
        Assert.Equal("Town", progression!.AreaCode);
        Assert.Equal(request.NatureSurvival!.BuildingProgressionCatalog!.HashSha256,
            progression.CatalogHashSha256);
        Assert.Empty(progression.ApprovedTeachingMaterials);

        var opportunities = await client.GetFromJsonAsync<
            Simulation플레이어기회Snapshot[]>(
            $"/api/simulation/v1/sessions/{session.SessionStableId}"
            + "/nature-survival/player-opportunities");
        Assert.Contains(opportunities!, value => value.WorldInteractionId ==
            SimulationNatureSurvivalCodes.PrepareFieldSupplyWorldInteractionId
            && value.PlayerActivityTrackCode ==
            Simulation플레이어활동경로Codes.AreaManufacturing);
        var needs = await client.GetFromJsonAsync<Simulation영역수요Snapshot[]>(
            $"/api/simulation/v1/sessions/{session.SessionStableId}"
            + "/nature-survival/area-needs");
        Assert.Equal(2, needs!.Length);
    }

    private static 경영SimulationSessionAggregate BuildDay2ReadySession(
        string profileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR3)
    {
        var request = CreateR3Request(profileRevision);
        if (profileRevision == SimulationNatureSurvivalCodes.ProfileRevisionR4)
        {
            var sessionId = "simulation-session:"
                + request.ClientRequestId.ToString("N");
            request.ScenarioSeed = Enumerable.Range(1, 10_000).First(seed =>
                NatureSurvivalRules.RollFirstDuskEncounter(seed, sessionId, 1, 16));
        }
        var session = new 경영SimulationSessionAggregate(request);
        for (var index = 1; index <= 13; index++)
        {
            Confirm(session, $"command:r3:harvest:{index}",
                SimulationNatureSurvivalCodes.BeginHarvest,
                $"resource:nature-r3-tree:{index:00}");
            Advance(session, NatureSurvivalRules.HarvestWorkSeconds, true,
                $"harvest:{index}");
        }
        Confirm(session, "command:r3:cabin:place",
            SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
            "facility:nature-cabin", 2, -2);
        Confirm(session, "command:r3:cabin:build",
            SimulationNatureSurvivalCodes.BeginCabinBuild,
            "facility:nature-cabin");
        Advance(session, NatureSurvivalRules.CabinWorkSeconds, true, "cabin");
        Confirm(session, "command:r3:cabin:enter",
            SimulationNatureSurvivalCodes.EnterCabin,
            "facility:nature-cabin");
        Confirm(session, "command:r3:cabin:store",
            SimulationNatureSurvivalCodes.StoreAtCabin,
            SimulationNatureSurvivalCodes.CabinStorageContainerStableId);

        AdvanceTo(session, NatureSurvivalRules.DaylightEndsAtSecond + 1);
        var encounter = session.GetNatureSurvivalState().Encounter!;
        Confirm(session, "command:r3:encounter:fight",
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, 0, 0, SimulationNatureSurvivalCodes.Fight);
        session.ConfirmNatureSurvivalAction(new()
        {
            CommandId = "command:r3:encounter:victory",
            ExpectedRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.ResolveEncounter,
            TargetStableId = encounter.EncounterStableId,
            ChoiceCode = SimulationNatureSurvivalCodes.Victory,
            AuthoritativeRewardBonusQuantity = 2,
        });

        AdvanceTo(session, NatureSurvivalRules.DuskEndsAtSecond);
        Confirm(session, "command:r3:sleep",
            SimulationNatureSurvivalCodes.SleepInCabin,
            "facility:nature-cabin");
        Advance(session, 60, false, "sleep-to-dawn");
        Assert.Equal("Dawn", session.GetNatureSurvivalState().ClockPhaseCode);
        Confirm(session, "command:r3:priority",
            SimulationNatureSurvivalCodes.SelectExpansionPlan,
            string.Empty, 0, 0, SimulationNatureSurvivalCodes.Workbench);
        return session;
    }

    private static 경영SimulationSession생성Request CreateR3Request(
        string profileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR3)
        => new()
        {
            ClientRequestId = Guid.Parse("b4c97b79-e488-46dc-b9f0-d3d5b0950f66"),
            ScenarioStableId = "scenario:nature-area-building-progression",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 1234,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
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
                ProfileRevision = profileRevision,
                PlayerStableId = "player:solo",
                InventoryCapacityUnits = 64,
                BuildingProgressionCatalog =
                    Simulation영역건물발전Catalog.CreateDefault(),
                ResourceNodes = Enumerable.Range(1, 16).Select(index => new
                    SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId =
                            $"resource:nature-r3-tree:{index:00}",
                        H2StableId = SimulationNatureSurvivalCodes.HarvestH2StableId,
                        H1StableId = "h1-stock:nature-exploration-buffer",
                        LocalX = -16 + index * 2,
                        LocalZ = 10,
                    }).ToArray(),
            },
        };

    private static void Build(경영SimulationSessionAggregate session,
        string blueprint, double x, double z, int seconds, string suffix)
    {
        var preview = session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = session.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginBuildingConstruction,
            TargetStableId = blueprint,
            LocalX = x,
            LocalZ = z,
        });
        Assert.True(preview.CanConfirm, string.Join(",", preview.BlockReasonCodes));
        Confirm(session, $"command:r3:building:{suffix}",
            SimulationNatureSurvivalCodes.BeginBuildingConstruction,
            blueprint, x, z);
        Advance(session, seconds, true, "building:" + suffix);
        Assert.Equal(Simulation영역건물발전Codes.Operational,
            session.GetNatureSurvivalState().BuildingProgression!.Nodes
                .Single(value => value.BlueprintStableId == blueprint).StateCode);
    }

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
            CommandId = "command:r3:clock:" + suffix + ":" + session.Revision,
            ExpectedRevision = session.Revision,
            ElapsedRealtimeSeconds = seconds,
            WorkInputHeld = workHeld,
        });

    private static void AdvanceTo(경영SimulationSessionAggregate session,
        int targetSecond)
    {
        while (session.GetNatureSurvivalState().ElapsedSecondsInCycle < targetSecond)
        {
            var remaining = targetSecond
                - session.GetNatureSurvivalState().ElapsedSecondsInCycle;
            Advance(session, Math.Min(60, remaining), false,
                "to:" + targetSecond);
        }
    }

    private static void AdvanceToCycleSecond(
        경영SimulationSessionAggregate session, int targetCycleIndex,
        int targetSecond)
    {
        while (session.GetNatureSurvivalState().CycleIndex < targetCycleIndex
               || session.GetNatureSurvivalState().ElapsedSecondsInCycle < targetSecond)
        {
            var state = session.GetNatureSurvivalState();
            var remaining = state.CycleIndex < targetCycleIndex
                ? NatureSurvivalRules.CycleSeconds
                    - state.ElapsedSecondsInCycle
                : targetSecond - state.ElapsedSecondsInCycle;
            Advance(session, Math.Min(60, remaining), false,
                "cycle:" + targetCycleIndex + ":to:" + targetSecond);
        }
    }
}
