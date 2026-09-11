using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
public sealed class SimulationNatureSurvivalTests
{
    [Theory]
    [InlineData(SimulationNatureSurvivalCodes.AcquireAxe,
        SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.BeginHarvest,
        SimulationNatureSurvivalCodes.BeginHarvestWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
        SimulationNatureSurvivalCodes.PlaceCabinBlueprintWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.BeginCabinBuild,
        SimulationNatureSurvivalCodes.BeginCabinBuildWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.EnterCabin,
        SimulationNatureSurvivalCodes.EnterCabinWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.LeaveCabin,
        SimulationNatureSurvivalCodes.LeaveCabinWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.ResolveEncounter,
        SimulationNatureSurvivalCodes.ResolveEncounterWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.StoreAtCabin,
        SimulationNatureSurvivalCodes.StoreAtCabinWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.SleepInCabin,
        SimulationNatureSurvivalCodes.SleepInCabinWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.SelectExpansionPlan,
        SimulationNatureSurvivalCodes.SelectExpansionPlanWorldInteractionId)]
    [InlineData(SimulationNatureSurvivalCodes.CollectDroppedTimber,
        SimulationNatureSurvivalCodes.CollectDroppedTimberWorldInteractionId)]
    public void 플레이어가확정하는_Nature생존행위는_정식WI식별자를갖는다(
        string actionCode, string expectedWorldInteractionId)
    {
        Assert.Equal(expectedWorldInteractionId,
            SimulationNatureSurvivalCodes.WorldInteractionIdForAction(actionCode));
    }

    [Fact]
    public void 시간경과는_독립WI로잘못분류하지않는다()
    {
        Assert.Empty(SimulationNatureSurvivalCodes.WorldInteractionIdForAction(
            nameof(ISimulationNatureSurvivalRuntime.AdvanceRealtimeAsync)));
    }

    [Fact]
    public void 실시간규칙은_20분주기와_고정단계를_공유한다()
    {
        Assert.Equal(1_200, NatureSurvivalRules.CycleSeconds);
        Assert.Equal(NatureSurvivalClockPhaseCodes.Daylight,
            NatureSurvivalRules.PhaseAt(599));
        Assert.Equal(NatureSurvivalClockPhaseCodes.Dusk,
            NatureSurvivalRules.PhaseAt(600));
        Assert.Equal(NatureSurvivalClockPhaseCodes.Night,
            NatureSurvivalRules.PhaseAt(750));
        Assert.Equal(NatureSurvivalClockPhaseCodes.Dawn,
            NatureSurvivalRules.PhaseAt(1_110));

        var projected = NatureSurvivalRules.AdvanceClock(2, 1_190, 20);
        Assert.Equal(3, projected.CycleIndex);
        Assert.Equal(10, projected.ElapsedSecondsInCycle);
        Assert.Equal(1, projected.CompletedCycleCount);
    }

    [Theory]
    [InlineData(0, false, 0, 0)]
    [InlineData(1, false, 1, 1)]
    [InlineData(2, true, 0, 1)]
    [InlineData(4, false, 2, 2)]
    [InlineData(5, true, 2, 2)]
    public void r2소음과오두막방어는_결정적인위협과적수를만든다(
        int noise, bool cabin, int expectedEffectiveTier, int expectedHostiles)
    {
        Assert.Equal(expectedEffectiveTier,
            NatureSurvivalRules.EffectiveThreatTier(noise, cabin));
        Assert.Equal(expectedHostiles,
            NatureSurvivalRules.EncounterHostileCount(noise, cabin));
        Assert.Equal(noise > 0,
            NatureSurvivalRules.ShouldTriggerDuskEncounter(1, "session:first-day",
                0, noise));
    }

    [Fact]
    public void r2두번째주기부터는_같은입력에_동일한65퍼센트판정을사용한다()
    {
        Assert.Equal(650, NatureSurvivalRules.FirstDuskEncounterChancePermille);
        var first = NatureSurvivalRules.ShouldTriggerDuskEncounter(
            1701, "session:later-cycle", 1, 3);
        var repeated = NatureSurvivalRules.ShouldTriggerDuskEncounter(
            1701, "session:later-cycle", 1, 3);

        Assert.Equal(first, repeated);
        Assert.Equal(NatureSurvivalRules.RollFirstDuskEncounter(
            1701, "session:later-cycle", 1, 3), first);
    }

    [Fact]
    public void 구형세션은_Nature실시간Profile을_자동활성화하지않는다()
    {
        var request = CreateRequest(includeNature: false);
        var aggregate = new 경영SimulationSessionAggregate(request);

        Assert.False(aggregate.Snapshot().NatureSurvival.IsEnabled);
        aggregate.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:legacy-tick",
            ExpectedRevision = 0,
            TickCount = 1,
        });

        Assert.Equal(1, aggregate.Snapshot().CurrentTick);
        Assert.False(aggregate.Snapshot().NatureSurvival.IsEnabled);
    }

    [Fact]
    public void 도끼벌목은_누르고있는시간을_누적해_기존소지품원장에_통나무를넣는다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateRequest());
        var started = Confirm(aggregate, "command:harvest-start", 0,
            SimulationNatureSurvivalCodes.BeginHarvest, "resource:nature-tree:01");

        Assert.Equal(SimulationNatureSurvivalCodes.Harvest,
            started.NatureSurvival.ActiveWork?.WorkKindCode);
        var completed = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:harvest-hold",
            ExpectedRevision = started.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });

        Assert.Null(completed.NatureSurvival.ActiveWork);
        Assert.Equal(NatureSurvivalRules.HarvestTimberQuantity,
            completed.NatureSurvival.TimberQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.Stump,
            completed.NatureSurvival.ResourceNodes.Single(value =>
                value.ResourceNodeStableId == "resource:nature-tree:01").StateCode);
        Assert.Contains(aggregate.GetWorldInventory().Players.Single().Items,
            value => value.ItemCode == SimulationNatureSurvivalCodes.TimberItemCode
                && value.Quantity == NatureSurvivalRules.HarvestTimberQuantity);

        var duplicate = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:harvest-hold",
            ExpectedRevision = started.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });
        Assert.Equal(completed.Revision, duplicate.Revision);
        Assert.Equal(completed.NatureSurvival.TimberQuantity,
            duplicate.NatureSurvival.TimberQuantity);
    }

    [Fact]
    public void 같은자리자원재생은_Session_revision_행위기록_SaveReplay에_함께남는다()
    {
        var request = CreateRequest();
        request.ScenarioSeed = FindNonTriggeringSeed(
            request.ClientRequestId, NatureSurvivalRules.TreeRegrowthCycleCount);
        var aggregate = new 경영SimulationSessionAggregate(request);
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var service = new SimulationNatureSurvivalService(sessions);
        var started = service.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:regrowth:harvest-start",
            ExpectedRevision = 0,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = "resource:nature-tree:01",
        });
        var harvested = service.AdvanceClock(aggregate.SessionStableId, new()
        {
            CommandId = "command:regrowth:harvest-hold",
            ExpectedRevision = started.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });
        var stump = harvested.NatureSurvival.ResourceNodes.Single(value =>
            value.ResourceNodeStableId == "resource:nature-tree:01");
        Assert.Equal(SimulationNatureSurvivalCodes.Stump, stump.StateCode);
        Assert.Equal(NatureSurvivalRules.TreeRegrowthCycleCount,
            stump.RegrowsAtCycleIndex);

        SimulationNatureSurvivalClockAdvanceRequest? regenerationRequest = null;
        경영SimulationSessionSnapshot? regenerated = null;
        var clockIndex = 0;
        while (aggregate.GetNatureSurvivalState().CycleIndex <
               NatureSurvivalRules.TreeRegrowthCycleCount)
        {
            var state = aggregate.GetNatureSurvivalState();
            var elapsed = Math.Min(60,
                NatureSurvivalRules.CycleSeconds - state.ElapsedSecondsInCycle);
            var candidate = new SimulationNatureSurvivalClockAdvanceRequest
            {
                CommandId = $"command:regrowth:clock:{clockIndex++}",
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = elapsed,
            };
            var result = service.AdvanceClock(aggregate.SessionStableId, candidate);
            var node = result.NatureSurvival.ResourceNodes.Single(value =>
                value.ResourceNodeStableId == "resource:nature-tree:01");
            if (node.StateCode == SimulationNatureSurvivalCodes.Standing)
            {
                regenerationRequest = candidate;
                regenerated = result;
            }
        }

        Assert.NotNull(regenerationRequest);
        Assert.NotNull(regenerated);
        var regenerationRecord = Assert.Single(aggregate
            .QueryActionManifestations(new Simulation행위기록Query
            {
                WorldStableId = aggregate.TerritoryStableId,
                WorldInteractionIds = new[]
                {
                    Simulation세계자원재생Codes.WorldInteractionId,
                },
            }).Records);
        Assert.Equal(regenerationRequest!.CommandId,
            regenerationRecord.CommandId);
        Assert.Equal(regenerationRequest.ExpectedRevision,
            regenerationRecord.BeforeWorldRevision);
        Assert.Equal(regenerated!.Revision,
            regenerationRecord.AfterWorldRevision);
        Assert.Equal(regenerated.CurrentTick,
            regenerationRecord.AppliedWorldTick);
        Assert.Equal(SimulationWorldInteractionTriggerSourceCodes.WorldDerived,
            regenerationRecord.TriggerSourceCode);
        Assert.Equal("world-system:nature-resource-regeneration",
            regenerationRecord.ActorStableId);
        Assert.Equal(new[] { "resource:nature-tree:01" },
            regenerationRecord.TargetStableIds);
        Assert.Contains(
            Simulation세계자원재생Codes.ResourceAvailabilityChanged,
            regenerationRecord.변화의미Codes);
        Assert.Contains(
            "player-progression:not-applicable:" +
            Simulation세계자원재생Codes.PlayerProgressionNotApplicableReason,
            regenerationRecord.SourceReferenceIds);
        var binding = Simulation기본플레이어분야Catalog.Create().Wi결속들
            .Single(value => value.WorldInteractionId ==
                Simulation세계자원재생Codes.WorldInteractionId);
        Assert.Equal(Simulation분야기여방식Codes.None, binding.기여방식Code);
        Assert.Equal(
            Simulation세계자원재생Codes.PlayerProgressionNotApplicableReason,
            binding.NoPlayerProgressReason);
        var profileRevisionBeforeDuplicate = aggregate
            .GetPlayerDomainProfile("player:solo")!.Revision;

        var duplicate = service.AdvanceClock(
            aggregate.SessionStableId, regenerationRequest);
        Assert.Equal(regenerated.Revision, duplicate.Revision);
        Assert.Equal(profileRevisionBeforeDuplicate,
            aggregate.GetPlayerDomainProfile("player:solo")!.Revision);
        Assert.Single(aggregate.QueryActionManifestations(
            new Simulation행위기록Query
            {
                WorldStableId = aggregate.TerritoryStableId,
                WorldInteractionIds = new[]
                {
                    Simulation세계자원재생Codes.WorldInteractionId,
                },
            }).Records);

        var saved = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-resource-regeneration",
            ExpectedRevision = aggregate.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var savedAgain = restored.CreateSavePackage(new()
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        var restoredRecord = Assert.Single(restored.QueryActionManifestations(
            new Simulation행위기록Query
            {
                WorldStableId = restored.TerritoryStableId,
                WorldInteractionIds = new[]
                {
                    Simulation세계자원재생Codes.WorldInteractionId,
                },
            }).Records);
        Assert.Equal(saved.ReplayHash, savedAgain.ReplayHash);
        Assert.Equal(regenerationRecord.기록HashSha256,
            restoredRecord.기록HashSha256);
        Assert.Equal(SimulationNatureSurvivalCodes.Standing,
            restored.GetNatureSurvivalState().ResourceNodes.Single(value =>
                value.ResourceNodeStableId == "resource:nature-tree:01")
                .StateCode);
    }

    [Fact]
    public async Task 같은자리자원재생은_LocalProcess와_RemoteHost에서_같은SaveReplay를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-resource-regeneration-host-parity-" +
            Guid.NewGuid().ToString("N"));
        var request = CreateRequest();
        request.ClientRequestId = Guid.Parse(
            "fc3227f9-4a02-4934-a456-5679f8342554");
        request.ScenarioSeed = FindNonTriggeringSeed(
            request.ClientRequestId, NatureSurvivalRules.TreeRegrowthCycleCount);
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var local = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localSession = await local.Sessions.CreateAsync(request);
            localSession = await local.Nature.ConfirmAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:regrowth-parity:harvest",
                    ExpectedRevision = localSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            localSession = await local.Nature.AdvanceRealtimeAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = "command:regrowth-parity:harvest-clock",
                    ExpectedRevision = localSession.Revision,
                    ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteSession = (await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var remoteRoute = "/api/simulation/v1/sessions/" +
                Uri.EscapeDataString(remoteSession.SessionStableId);
            var remoteHarvest = await client.PostAsJsonAsync(
                remoteRoute + "/nature-survival/commands",
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:regrowth-parity:harvest",
                    ExpectedRevision = remoteSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            Assert.Equal(HttpStatusCode.OK, remoteHarvest.StatusCode);
            remoteSession = (await remoteHarvest.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var remoteHarvestClock = await client.PostAsJsonAsync(
                remoteRoute + "/nature-survival/clock/advance",
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = "command:regrowth-parity:harvest-clock",
                    ExpectedRevision = remoteSession.Revision,
                    ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });
            Assert.Equal(HttpStatusCode.OK, remoteHarvestClock.StatusCode);
            remoteSession = (await remoteHarvestClock.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;

            var clockIndex = 0;
            while (localSession.NatureSurvival.CycleIndex <
                   NatureSurvivalRules.TreeRegrowthCycleCount)
            {
                var elapsed = Math.Min(60, NatureSurvivalRules.CycleSeconds -
                    localSession.NatureSurvival.ElapsedSecondsInCycle);
                var commandId = $"command:regrowth-parity:clock:{clockIndex++}";
                localSession = await local.Nature.AdvanceRealtimeAsync(
                    localSession.SessionStableId,
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = commandId,
                        ExpectedRevision = localSession.Revision,
                        ElapsedRealtimeSeconds = elapsed,
                    });
                var response = await client.PostAsJsonAsync(
                    remoteRoute + "/nature-survival/clock/advance",
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = commandId,
                        ExpectedRevision = remoteSession.Revision,
                        ElapsedRealtimeSeconds = elapsed,
                    });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                remoteSession = (await response.Content.ReadFromJsonAsync<
                    경영SimulationSessionSnapshot>())!;
            }

            var localState = await local.Nature.GetAsync(
                localSession.SessionStableId);
            var remoteState = await client.GetFromJsonAsync<
                SimulationNatureSurvivalStateSnapshot>(
                remoteRoute + "/nature-survival");
            Assert.NotNull(remoteState);
            Assert.Equal(localSession.Revision, remoteSession.Revision);
            Assert.Equal(SimulationNatureSurvivalCodes.Standing,
                localState.ResourceNodes.Single(value =>
                    value.ResourceNodeStableId == "resource:nature-tree:01")
                    .StateCode);
            Assert.Equal(localState.ResourceNodes.Select(value => new
                {
                    value.ResourceNodeStableId,
                    value.StateCode,
                    value.RegrowsAtCycleIndex,
                }), remoteState!.ResourceNodes.Select(value => new
                {
                    value.ResourceNodeStableId,
                    value.StateCode,
                    value.RegrowsAtCycleIndex,
                }));
            var localOpportunity = (await local.Nature
                .GetPlayerOpportunitiesAsync(localSession.SessionStableId))
                .Single(value => value.ActionCode ==
                    SimulationNatureSurvivalCodes.BeginHarvest);
            var remoteOpportunities = await client.GetFromJsonAsync<
                Simulation플레이어기회Snapshot[]>(remoteRoute +
                    "/nature-survival/player-opportunities");
            var remoteOpportunity = remoteOpportunities!.Single(value =>
                value.ActionCode == SimulationNatureSurvivalCodes.BeginHarvest);
            Assert.Equal("resource:nature-tree:01",
                localOpportunity.TargetStableId);
            Assert.Equal(localOpportunity.OpportunityStableId,
                remoteOpportunity.OpportunityStableId);
            Assert.Equal(localOpportunity.Available,
                remoteOpportunity.Available);
            Assert.Equal(localOpportunity.NextPlayerFlowCode,
                remoteOpportunity.NextPlayerFlowCode);

            await local.Sessions.SaveSlotAsync(localSession.SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-resource-regeneration-parity",
                    ExpectedRevision = localSession.Revision,
                });
            var localPackage = slotStore.Read(
                "slot-resource-regeneration-parity").Package;
            var remoteSave = await client.PostAsJsonAsync(
                remoteRoute + "/saves",
                new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteSession.Revision,
                });
            Assert.Equal(HttpStatusCode.OK, remoteSave.StatusCode);
            var remotePackage = (await remoteSave.Content.ReadFromJsonAsync<
                SimulationSessionSavePackage>())!;
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
            var localRegeneration = Assert.Single(
                localPackage.ActionManifestationLedger!.TailRecords,
                value => value.WorldInteractionId ==
                    Simulation세계자원재생Codes.WorldInteractionId);
            var remoteRegeneration = Assert.Single(
                remotePackage.ActionManifestationLedger!.TailRecords,
                value => value.WorldInteractionId ==
                    Simulation세계자원재생Codes.WorldInteractionId);
            Assert.Equal(localRegeneration.기록HashSha256,
                remoteRegeneration.기록HashSha256);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Fact]
    public void r5벌목은_지면통나무를만들고_별도줍기WI가_인벤토리로옮긴다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateR5Request());
        var started = Confirm(aggregate, "command:r5:harvest-start", 0,
            SimulationNatureSurvivalCodes.BeginHarvest,
            "resource:nature-tree:01");
        var harvested = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:r5:harvest-hold",
            ExpectedRevision = started.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });

        Assert.Equal(0, harvested.NatureSurvival.TimberQuantity);
        var dropped = Assert.Single(harvested.NatureSurvival.DroppedTimber);
        Assert.Equal(SimulationNatureSurvivalCodes.DroppedTimberAvailable,
            dropped.StateCode);
        Assert.Equal(NatureSurvivalRules.HarvestTimberQuantity,
            dropped.Quantity);
        Assert.Equal("resource:nature-tree:01",
            dropped.SourceResourceNodeStableId);
        Assert.Equal(harvested.Revision, dropped.CreatedWorldRevision);

        var preview = aggregate.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = harvested.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.CollectDroppedTimber,
            TargetStableId = dropped.DroppedTimberStableId,
        });
        Assert.True(preview.CanConfirm);
        Assert.Equal(SimulationNatureSurvivalCodes
            .CollectDroppedTimberWorldInteractionId, preview.WorldInteractionId);
        Assert.Equal(dropped.Quantity, preview.TargetDroppedTimberQuantity);

        var collected = Confirm(aggregate, "command:r5:collect-timber",
            harvested.Revision,
            SimulationNatureSurvivalCodes.CollectDroppedTimber,
            dropped.DroppedTimberStableId);
        Assert.Equal(NatureSurvivalRules.HarvestTimberQuantity,
            collected.NatureSurvival.TimberQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.DroppedTimberCollected,
            Assert.Single(collected.NatureSurvival.DroppedTimber).StateCode);
        Assert.Equal(collected.Revision,
            Assert.Single(collected.NatureSurvival.DroppedTimber)
                .CollectedWorldRevision);

        var duplicate = Confirm(aggregate, "command:r5:collect-timber",
            harvested.Revision,
            SimulationNatureSurvivalCodes.CollectDroppedTimber,
            dropped.DroppedTimberStableId);
        Assert.Equal(collected.Revision, duplicate.Revision);
        Assert.Equal(collected.NatureSurvival.TimberQuantity,
            duplicate.NatureSurvival.TimberQuantity);

        var unavailable = aggregate.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = collected.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.CollectDroppedTimber,
            TargetStableId = dropped.DroppedTimberStableId,
        });
        Assert.False(unavailable.CanConfirm);
        Assert.Contains(SimulationNatureSurvivalCodes.DroppedTimberUnavailable,
            unavailable.BlockReasonCodes);
    }

    [Fact]
    public void r5통나무줍기는_묶음전체용량이부족하면_상태를바꾸지않는다()
    {
        var request = CreateR5Request();
        request.NatureSurvival!.InventoryCapacityUnits = 2m;
        var aggregate = new 경영SimulationSessionAggregate(request);
        var started = Confirm(aggregate, "command:r5:capacity-harvest", 0,
            SimulationNatureSurvivalCodes.BeginHarvest,
            "resource:nature-tree:01");
        var harvested = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:r5:capacity-hold",
            ExpectedRevision = started.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });
        var dropped = Assert.Single(harvested.NatureSurvival.DroppedTimber);

        var preview = aggregate.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = harvested.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.CollectDroppedTimber,
            TargetStableId = dropped.DroppedTimberStableId,
        });

        Assert.False(preview.CanConfirm);
        Assert.Equal(1m, preview.RemainingInventoryCapacityUnits);
        Assert.Contains(SimulationWorldSurvivalInventoryCodes
            .PlayerCapacityExceeded, preview.BlockReasonCodes);
        Assert.Equal(SimulationNatureSurvivalCodes.DroppedTimberAvailable,
            Assert.Single(aggregate.GetNatureSurvivalState().DroppedTimber)
                .StateCode);
    }

    [Fact]
    public void r5지면통나무는_v24저장자료와_재생hash에포함된다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateR5Request());
        var started = Confirm(aggregate, "command:r5:save-harvest", 0,
            SimulationNatureSurvivalCodes.BeginHarvest,
            "resource:nature-tree:01");
        aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:r5:save-hold",
            ExpectedRevision = started.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });
        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-r5-dropped-timber",
            ExpectedRevision = aggregate.Revision,
        });

        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new()
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V24, package.SchemaVersion);
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(
            Assert.Single(package.Snapshot.NatureSurvival.DroppedTimber)
                .DroppedTimberStableId,
            Assert.Single(restored.Snapshot().NatureSurvival.DroppedTimber)
                .DroppedTimberStableId);
    }

    [Fact]
    public void 세그루벌목후_오두막도면과_30초건설이_회복보관방어거점을완성한다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateRequest());
        for (var index = 1; index <= 3; index++)
        {
            var target = $"resource:nature-tree:{index:00}";
            Confirm(aggregate, $"command:harvest-start:{index}", aggregate.Revision,
                SimulationNatureSurvivalCodes.BeginHarvest, target);
            aggregate.AdvanceNatureSurvivalClock(new()
            {
                CommandId = $"command:harvest-hold:{index}",
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                WorkInputHeld = true,
            });
        }
        Assert.Equal(NatureSurvivalRules.CabinTimberCost,
            aggregate.GetNatureSurvivalState().TimberQuantity);

        Confirm(aggregate, "command:cabin-place", aggregate.Revision,
            SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
            "facility:nature-cabin", localX: 3, localZ: -2, yaw: 90);
        Confirm(aggregate, "command:cabin-build", aggregate.Revision,
            SimulationNatureSurvivalCodes.BeginCabinBuild,
            "facility:nature-cabin");
        var completed = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:cabin-hold",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.CabinWorkSeconds,
            WorkInputHeld = true,
        });

        Assert.Equal(0, completed.NatureSurvival.TimberQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.Completed,
            completed.NatureSurvival.Cabin.StateCode);
        Assert.True(completed.NatureSurvival.Cabin.RecoveryAvailable);
        Assert.True(completed.NatureSurvival.Cabin.DefenseAvailable);
        Assert.Equal(NatureSurvivalRules.CabinStorageCapacity,
            completed.NatureSurvival.Cabin.StorageCapacity);

        var entered = Confirm(aggregate, "command:cabin-enter", aggregate.Revision,
            SimulationNatureSurvivalCodes.EnterCabin, "facility:nature-cabin");
        Assert.True(entered.NatureSurvival.PlayerInsideCabin);
        var duplicateEnter = aggregate.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.EnterCabin,
            TargetStableId = "facility:nature-cabin",
        });
        Assert.Equal(SimulationNatureSurvivalCodes.EnterCabinWorldInteractionId,
            duplicateEnter.WorldInteractionId);
        Assert.False(duplicateEnter.CanConfirm);

        var left = Confirm(aggregate, "command:cabin-leave", aggregate.Revision,
            SimulationNatureSurvivalCodes.LeaveCabin, "facility:nature-cabin");
        Assert.False(left.NatureSurvival.PlayerInsideCabin);
    }

    [Fact]
    public void 작업취소는_벌목점유를해제하고_오두막예약통나무를반환한다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateRequest());
        var harvesting = Confirm(aggregate, "command:cancel:harvest-start", 0,
            SimulationNatureSurvivalCodes.BeginHarvest, "resource:nature-tree:01");
        var progressed = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:cancel:harvest-progress",
            ExpectedRevision = harvesting.Revision,
            ElapsedRealtimeSeconds = 2,
            WorkInputHeld = true,
        });

        var harvestCancelled = Confirm(aggregate, "command:cancel:harvest",
            progressed.Revision, SimulationNatureSurvivalCodes.CancelActiveWork,
            "resource:nature-tree:01");

        Assert.Null(harvestCancelled.NatureSurvival.ActiveWork);
        Assert.Equal(0, harvestCancelled.NatureSurvival.TimberQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.Standing,
            harvestCancelled.NatureSurvival.ResourceNodes.Single(value =>
                value.ResourceNodeStableId == "resource:nature-tree:01").StateCode);

        for (var index = 1; index <= 3; index++)
        {
            var target = $"resource:nature-tree:{index:00}";
            Confirm(aggregate, $"command:cancel:tree:{index}", aggregate.Revision,
                SimulationNatureSurvivalCodes.BeginHarvest, target);
            aggregate.AdvanceNatureSurvivalClock(new()
            {
                CommandId = $"command:cancel:tree-hold:{index}",
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                WorkInputHeld = true,
            });
        }
        Confirm(aggregate, "command:cancel:cabin-place", aggregate.Revision,
            SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
            "facility:nature-cabin", localX: 2, localZ: -1);
        Confirm(aggregate, "command:cancel:cabin-start", aggregate.Revision,
            SimulationNatureSurvivalCodes.BeginCabinBuild,
            "facility:nature-cabin");
        aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:cancel:cabin-progress",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = 10,
            WorkInputHeld = true,
        });

        var cabinCancelled = Confirm(aggregate, "command:cancel:cabin",
            aggregate.Revision, SimulationNatureSurvivalCodes.CancelActiveWork,
            "facility:nature-cabin");

        Assert.Null(cabinCancelled.NatureSurvival.ActiveWork);
        Assert.Equal(NatureSurvivalRules.CabinTimberCost,
            cabinCancelled.NatureSurvival.TimberQuantity);
        Assert.Equal(0, cabinCancelled.NatureSurvival.Cabin.ReservedTimberQuantity);
        Assert.Equal(0, cabinCancelled.NatureSurvival.Cabin.CompletedWorkSeconds);
        Assert.Equal(SimulationNatureSurvivalCodes.Building,
            cabinCancelled.NatureSurvival.Cabin.StateCode);

        var missing = aggregate.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.CancelActiveWork,
        });
        Assert.Equal(SimulationNatureSurvivalCodes.CancelActiveWorkWorldInteractionId,
            missing.WorldInteractionId);
        Assert.False(missing.CanConfirm);
        Assert.Contains(SimulationNatureSurvivalCodes.ActiveWorkRequired,
            missing.BlockReasonCodes);
    }

    [Fact]
    public void Solo메뉴정지는_시계를멈추고_주기경계만_WorldTick을진행한다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateRequest());
        var paused = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:pause",
            ExpectedRevision = 0,
            ElapsedRealtimeSeconds = 60,
            PauseReasonCode = SimulationNatureSurvivalCodes.Menu,
        });
        Assert.True(paused.NatureSurvival.ClockPaused);
        Assert.Equal(0, paused.NatureSurvival.ElapsedSecondsInCycle);
        Assert.Equal(0, paused.CurrentTick);

        경영SimulationSessionSnapshot current = paused;
        for (var index = 0; index < 20; index++)
        {
            current = aggregate.AdvanceNatureSurvivalClock(new()
            {
                CommandId = $"command:clock:{index}",
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = 60,
            });
        }
        Assert.Equal(1, current.NatureSurvival.CycleIndex);
        Assert.Equal(0, current.NatureSurvival.ElapsedSecondsInCycle);
        Assert.Equal(1, current.CurrentTick);
    }

    [Fact]
    public void 소음후_첫황혼조우는_seed로결정되고_Skeleton표현은placeholder로표시된다()
    {
        var request = CreateRequest();
        request.ScenarioSeed = FindTriggeringSeed(request.ClientRequestId);
        var aggregate = new 경영SimulationSessionAggregate(request);
        Confirm(aggregate, "command:noise-start", 0,
            SimulationNatureSurvivalCodes.BeginHarvest, "resource:nature-tree:01");
        aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:noise-hold",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = 4,
            WorkInputHeld = true,
        });

        for (var index = 0; index < 10; index++)
        {
            aggregate.AdvanceNatureSurvivalClock(new()
            {
                CommandId = $"command:dusk:{index}",
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = index == 9 ? 56 : 60,
            });
        }
        var encounter = aggregate.GetNatureSurvivalState().Encounter;
        Assert.NotNull(encounter);
        Assert.Equal(SimulationNatureSurvivalCodes.Pending, encounter!.StateCode);
        Assert.Equal(SimulationNatureSurvivalCodes.SkeletonPlaceholderCode,
            encounter.ThreatPresentationCode);
    }

    [Fact]
    public void Nature상태와명령은_v13저장자료에서_동일hash로재생된다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateRequest());
        Confirm(aggregate, "command:save-harvest", 0,
            SimulationNatureSurvivalCodes.BeginHarvest, "resource:nature-tree:01");
        aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:save-hold",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = 4,
            WorkInputHeld = true,
        });
        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-survival",
            ExpectedRevision = aggregate.Revision,
        });

        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-survival",
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V13, package.SchemaVersion);
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(package.Snapshot.NatureSurvival.TimberQuantity,
            restored.Snapshot().NatureSurvival.TimberQuantity);
    }

    [Fact]
    public void r2오두막보관은_Preview를바꾸지않고_Confirm에서가능한통나무를모두옮긴다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 2);
        Confirm(aggregate, "command:r2:enter-storage", aggregate.Revision,
            SimulationNatureSurvivalCodes.EnterCabin, "facility:nature-cabin");
        var before = aggregate.GetNatureSurvivalState();
        var preview = aggregate.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.StoreAtCabin,
            TargetStableId = SimulationNatureSurvivalCodes.CabinStorageContainerStableId,
        });

        Assert.True(preview.CanConfirm);
        Assert.Equal(4, preview.TransferableTimberQuantity);
        Assert.Equal(before.TimberQuantity,
            aggregate.GetNatureSurvivalState().TimberQuantity);

        var stored = Confirm(aggregate, "command:r2:store", aggregate.Revision,
            SimulationNatureSurvivalCodes.StoreAtCabin,
            SimulationNatureSurvivalCodes.CabinStorageContainerStableId);
        Assert.Equal(0, stored.NatureSurvival.TimberQuantity);
        Assert.Equal(4, stored.NatureSurvival.StoredTimberQuantity);
        Assert.Contains(aggregate.GetWorldInventory().Containers, value =>
            value.ContainerStableId ==
                SimulationNatureSurvivalCodes.CabinStorageContainerStableId
            && value.CapacityUnits == NatureSurvivalRules.CabinStorageCapacity);

        var duplicate = Confirm(aggregate, "command:r2:store",
            preview.WorldRevision, SimulationNatureSurvivalCodes.StoreAtCabin,
            SimulationNatureSurvivalCodes.CabinStorageContainerStableId);
        Assert.Equal(stored.Revision, duplicate.Revision);
        Assert.Equal(4, duplicate.NatureSurvival.StoredTimberQuantity);
    }

    [Fact]
    public void Nature보관WI는_같은명령으로_Preview_Confirm_권위_귀환추적을남긴다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 2);
        Confirm(aggregate, "command:r2:trace:enter", aggregate.Revision,
            SimulationNatureSurvivalCodes.EnterCabin, "facility:nature-cabin");
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var sink = new InMemorySimulationPlayableLoopEngineTraceSink();
        var service = new SimulationNatureSurvivalService(sessions,
            new 세계상호작용실행Pipeline(sink), "LocalProcess");
        var command = new SimulationNatureSurvivalCommandRequest
        {
            CommandId = "command:r2:trace:store",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.StoreAtCabin,
            TargetStableId =
                SimulationNatureSurvivalCodes.CabinStorageContainerStableId,
        };

        service.Confirm(aggregate.SessionStableId, command);
        var trace = sink.Snapshot(
            SimulationNatureFirstDayEngineValidationProfiles
                .PlayableLoopStableId,
            SimulationNatureSurvivalCodes.StoreAtCabinWorldInteractionId,
            command.CommandId);

        Assert.Equal(new[]
        {
            SimulationEngineInteractionPhaseCodes.Preview,
            SimulationEngineInteractionPhaseCodes.Confirm,
            SimulationEngineInteractionPhaseCodes.FocusEvidenceCollect,
            SimulationEngineInteractionPhaseCodes.AuthorityCommit,
            SimulationEngineInteractionPhaseCodes.ActionRecordAppend,
            SimulationEngineInteractionPhaseCodes.PlayerProgressionApply,
            SimulationEngineInteractionPhaseCodes.MeditationProgressionApply,
            SimulationEngineInteractionPhaseCodes.ReturnProjection,
        }, trace.Select(value => value.PhaseCode));
        Assert.All(trace, value => Assert.Equal("LocalProcess",
            value.AuthorityLocationCode));
        var focusEvidence = trace.Single(value => value.PhaseCode ==
            SimulationEngineInteractionPhaseCodes.FocusEvidenceCollect);
        Assert.Equal(SimulationEngineInteractionStatusCodes.NotApplicable,
            focusEvidence.StatusCode);
        Assert.Equal("FocusProfileNotConfigured", focusEvidence.ReasonCode);
        var meditationProgress = trace.Single(value => value.PhaseCode ==
            SimulationEngineInteractionPhaseCodes.MeditationProgressionApply);
        Assert.Equal(SimulationEngineInteractionStatusCodes.NotApplicable,
            meditationProgress.StatusCode);
        Assert.Equal("FocusEvidenceNotProvided",
            meditationProgress.ReasonCode);
        Assert.Equal(aggregate.Revision,
            trace.Last().AfterAuthorityRevision);
        var action = Assert.Single(aggregate.GetActionManifestationLedger()!
            .TailRecords);
        Assert.Equal(command.CommandId, action.CommandId);
        Assert.Contains(Simulation행위변화의미Codes.플레이어진척변경,
            action.변화의미Codes);
        var profile = aggregate.GetPlayerDomainProfile();
        Assert.NotNull(profile);
        service.Confirm(aggregate.SessionStableId, command);
        Assert.Single(aggregate.GetActionManifestationLedger()!.TailRecords);
        Assert.Equal(profile!.Revision,
            aggregate.GetPlayerDomainProfile()!.Revision);
    }

    [Fact]
    public void r2첫날조우는확정되고_Fight가전투를연동안한동안Nature시계를멈춘다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 0);
        AdvanceNatureTo(aggregate, NatureSurvivalRules.DaylightEndsAtSecond);
        var encounter = aggregate.GetNatureSurvivalState().Encounter;
        Assert.NotNull(encounter);
        Assert.Equal(SimulationNatureSurvivalCodes.Pending, encounter!.StateCode);
        Assert.True(encounter.HostileCount is >= 1 and <= 3);

        var fighting = Confirm(aggregate, "command:r2:fight", aggregate.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, SimulationNatureSurvivalCodes.Fight);
        Assert.Equal(SimulationNatureSurvivalCodes.CombatActive,
            fighting.NatureSurvival.Encounter!.StateCode);
        Assert.NotEmpty(fighting.NatureSurvival.LinkedCombatStableId);

        var frozenAt = fighting.NatureSurvival.ElapsedSecondsInCycle;
        var frozen = aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:r2:combat-clock",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = 60,
        });
        Assert.True(frozen.NatureSurvival.ClockPaused);
        Assert.Equal(frozenAt, frozen.NatureSurvival.ElapsedSecondsInCycle);

        var won = Confirm(aggregate, "command:r2:victory", aggregate.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, SimulationNatureSurvivalCodes.Victory);
        Assert.Equal(encounter.HostileCount,
            won.NatureSurvival.RebuildPartQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.Victory,
            won.NatureSurvival.LastCombatResultCode);
        Assert.Equal(SimulationNatureSurvivalCodes.Resolved,
            won.NatureSurvival.Encounter!.StateCode);
    }

    [Fact]
    public void r2패배는_도끼건물보관을유지하고_소지재료의절반내림만잃는다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 3);
        Assert.Equal(6, aggregate.GetNatureSurvivalState().TimberQuantity);
        AdvanceNatureTo(aggregate, NatureSurvivalRules.DaylightEndsAtSecond);
        var encounter = aggregate.GetNatureSurvivalState().Encounter!;
        Confirm(aggregate, "command:r2:defeat-fight", aggregate.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, SimulationNatureSurvivalCodes.Fight);
        var defeated = Confirm(aggregate, "command:r2:defeat-result",
            aggregate.Revision, SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, SimulationNatureSurvivalCodes.Defeat);

        Assert.True(defeated.NatureSurvival.HasAxe);
        Assert.Equal(SimulationNatureSurvivalCodes.Completed,
            defeated.NatureSurvival.Cabin.StateCode);
        Assert.Equal(3, defeated.NatureSurvival.TimberQuantity);
        Assert.True(defeated.NatureSurvival.PlayerInsideCabin);
    }

    [Fact]
    public void r2황혼조우는_지역사건을발명하지않고_WI01관찰에서_WI11로인계된다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 0,
            includeThreatObservationSpatial: true);
        AdvanceNatureTo(aggregate, NatureSurvivalRules.DaylightEndsAtSecond);
        var encounter = aggregate.GetNatureSurvivalState().Encounter!;
        Assert.Equal(SimulationNatureSurvivalCodes.Pending, encounter.StateCode);
        var revisionBefore = aggregate.Revision;

        var preview = aggregate.PreviewNatureThreatObservation(new()
        {
            ExpectedRevision = revisionBefore,
            DecisionStableId = "decision:nature-twilight-observation",
            TaskStableId = "task:nature-twilight-observation",
            ActorStableId = "player:solo",
            NatureRouteCode = SimulationNatureInteractionCodes.NatureHomeTwilightRoute,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature위협관찰공간,
        });

        Assert.True(preview.CanConfirm);
        Assert.Equal(encounter.EffectiveThreatTier, preview.EffectivePressure);
        Assert.Contains(encounter.EncounterStableId, preview.SourceIncidentStableIds);
        Assert.Equal(new[] { "WI-NATURE-11", "WI-NATURE-02" },
            preview.NextWorldInteractionIds);
        Assert.Empty(aggregate.Snapshot().RegionalIncidents);
        Assert.Equal(revisionBefore, aggregate.Revision);
    }

    [Fact]
    public void r2후퇴수면새벽계획은_v17로동일hash재생된다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 2);
        Confirm(aggregate, "command:r2:loop-enter", aggregate.Revision,
            SimulationNatureSurvivalCodes.EnterCabin, "facility:nature-cabin");
        Confirm(aggregate, "command:r2:loop-store", aggregate.Revision,
            SimulationNatureSurvivalCodes.StoreAtCabin,
            SimulationNatureSurvivalCodes.CabinStorageContainerStableId);
        AdvanceNatureTo(aggregate, NatureSurvivalRules.DaylightEndsAtSecond);
        var encounter = aggregate.GetNatureSurvivalState().Encounter!;
        Confirm(aggregate, "command:r2:loop-retreat", aggregate.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, SimulationNatureSurvivalCodes.Retreat);
        Assert.Equal(SimulationNatureSurvivalCodes.Retreat,
            aggregate.GetNatureSurvivalState().LastCombatResultCode);
        AdvanceNatureTo(aggregate, NatureSurvivalRules.DuskEndsAtSecond);
        Confirm(aggregate, "command:r2:loop-sleep", aggregate.Revision,
            SimulationNatureSurvivalCodes.SleepInCabin, "facility:nature-cabin");
        aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:r2:loop-sleep-clock",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = 60,
        });
        Assert.Equal(NatureSurvivalRules.NightEndsAtSecond,
            aggregate.GetNatureSurvivalState().ElapsedSecondsInCycle);
        Assert.False(aggregate.GetNatureSurvivalState().Sleeping);
        Confirm(aggregate, "command:r2:loop-plan", aggregate.Revision,
            SimulationNatureSurvivalCodes.SelectExpansionPlan,
            "plan:nature:day2", SimulationNatureSurvivalCodes.Workbench);

        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-first-day-r2",
            ExpectedRevision = aggregate.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new()
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V18, package.SchemaVersion);
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(4, restored.Snapshot().NatureSurvival.StoredTimberQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.Workbench,
            restored.Snapshot().NatureSurvival.SelectedExpansionPlanCode);
        Assert.True(restored.Snapshot().NatureSurvival.Day2Ready);
    }

    [Fact]
    public void r2Fight는_파생공간공급자없이도_WorldLocal전투를열고_후퇴를한번인계한다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 0);
        AdvanceNatureTo(aggregate, NatureSurvivalRules.DaylightEndsAtSecond + 1);
        var encounter = aggregate.GetNatureSurvivalState().Encounter!;
        Confirm(aggregate, "command:r2:battle-link", aggregate.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, SimulationNatureSurvivalCodes.Fight);

        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var policies = new InMemorySimulationTeamObservationPolicyStore();
        policies.Replace(new SimulationTeamObservationPolicySnapshot
        {
            SessionStableId = aggregate.SessionStableId,
            TeamStableId = "team:nature:first-day",
            Revision = 1,
            MembersCanObserve = true,
            MemberActorStableIds = ["player:solo"],
            AllowedViewModeCodes =
                [SimulationTeamObservationViewModeCodes.FirstPerson],
            SimulationOnly = true,
        });
        var battles = new SimulationBattleInstanceService(sessions, policies,
            new InMemorySimulationBattleInstanceStore());
        var preview = battles.PreviewCreate(aggregate.SessionStableId, new()
        {
            ExpectedWorldRevision = aggregate.Revision,
            EncounterStableId = encounter.EncounterStableId,
            RequestingActorStableId = "player:solo",
        });

        Assert.True(preview.CanConfirm);
        Assert.Equal(SimulationLocalCombatCodes.WorldLocal,
            preview.ScaleDecision.CombatSpaceCode);
        Assert.Equal(encounter.HostileCount, preview.UnitRoster.Units
            .Where(value => value.SideCode ==
                SimulationFarmTacticalCombatCodes.Hostile)
            .Sum(value => value.MemberCount));

        var battle = battles.ConfirmCreate(aggregate.SessionStableId, new()
        {
            CommandId = "command:r2:battle:create",
            ExpectedWorldRevision = aggregate.Revision,
            ExpectedBattleWorldContextHashSha256 = preview.LocalWorldContext
                .ContextHashSha256,
            EncounterStableId = encounter.EncounterStableId,
            RequestingActorStableId = "player:solo",
        });
        var retreated = battles.ConfirmLocalAction(aggregate.SessionStableId,
            battle.BattleStableId, new()
            {
                CommandId = "command:r2:battle:retreat",
                ExpectedBattleRevision = battle.BattleRevision,
                RequestingActorStableId = "player:solo",
                ActionCode = SimulationLocalCombatCodes.Retreat,
            });

        Assert.Equal(SimulationBattleInstanceCodes.Completed,
            retreated.PhaseCode);
        var nature = aggregate.GetNatureSurvivalState();
        Assert.Equal(SimulationNatureSurvivalCodes.Resolved,
            nature.Encounter!.StateCode);
        Assert.Equal(SimulationNatureSurvivalCodes.Retreat,
            nature.LastCombatResultCode);

        var duplicate = battles.ConfirmLocalAction(aggregate.SessionStableId,
            battle.BattleStableId, new()
            {
                CommandId = "command:r2:battle:retreat",
                ExpectedBattleRevision = battle.BattleRevision,
                RequestingActorStableId = "player:solo",
                ActionCode = SimulationLocalCombatCodes.Retreat,
            });
        Assert.Equal(retreated.ReplayHashSha256, duplicate.ReplayHashSha256);
        Assert.Equal(SimulationNatureSurvivalCodes.Retreat,
            aggregate.GetNatureSurvivalState().LastCombatResultCode);
    }

    [Fact]
    public void 기존_v17_Nature_r2_WI근거는_v18도입후에도_그대로복원한다()
    {
        var request = CreateRequest();
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR2;
        request.NatureSurvival.StartsWithAxe = false;
        var aggregate = new 경영SimulationSessionAggregate(request);
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var service = new SimulationNatureSurvivalService(sessions);
        service.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:r2:v17:axe",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        });
        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-r2-v17-compatibility",
            ExpectedRevision = aggregate.Revision,
        });
        package.SchemaVersion = SimulationSaveSchemaVersions.V17;
        package.ReplayHash = SimulationReplayHasher.Calculate(package);

        var restored = SimulationSessionReplay.Restore(package);

        Assert.True(restored.GetNatureSurvivalState().HasAxe);
        Assert.Equal(SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
            package.CommandLog.Single().WorldInteractionInvocation!.WorldInteractionId);
    }

    [Fact]
    public void 직접개입_추가보상은_전투권위_내부인계만_허용한다()
    {
        var aggregate = BuildR2Cabin(extraHarvestCount: 0);
        AdvanceNatureTo(aggregate, NatureSurvivalRules.DaylightEndsAtSecond + 1);
        var encounter = aggregate.GetNatureSurvivalState().Encounter!;
        Confirm(aggregate, "command:r2:bonus:link", aggregate.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter,
            encounter.EncounterStableId, SimulationNatureSurvivalCodes.Fight);
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var publicService = new SimulationNatureSurvivalService(sessions);

        var error = Assert.Throws<SimulationContractException>(() =>
            publicService.Confirm(aggregate.SessionStableId, new()
            {
                CommandId = "command:r2:bonus:client-injection",
                ExpectedRevision = aggregate.Revision,
                PlayerStableId = "player:solo",
                ActionCode = SimulationNatureSurvivalCodes.ResolveEncounter,
                TargetStableId = encounter.EncounterStableId,
                ChoiceCode = SimulationNatureSurvivalCodes.Victory,
                AuthoritativeRewardBonusQuantity = 2,
            }));
        Assert.Equal("SimulationNatureCombatRewardBonusServerOnly", error.ErrorCode);
        Assert.Equal(SimulationNatureSurvivalCodes.CombatActive,
            aggregate.GetNatureSurvivalState().Encounter!.StateCode);

        aggregate.ConfirmNatureSurvivalAction(new()
        {
            CommandId = "command:r2:bonus:authority-handoff",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.ResolveEncounter,
            TargetStableId = encounter.EncounterStableId,
            ChoiceCode = SimulationNatureSurvivalCodes.Victory,
            AuthoritativeRewardBonusQuantity = 2,
        });
        Assert.Equal(encounter.HostileCount + 2,
            aggregate.GetNatureSurvivalState().RebuildPartQuantity);
    }

    [Fact]
    public void 도끼확보WI는_E4에서_플레이어도구지점공간문맥을결속한다()
    {
        var maturity = new SimulationWorldInteractionMaturityService();

        var result = maturity.ReviewE4(
            new SimulationWorldInteractionE4ContextReviewRequest
            {
                Definition = new SimulationWorldInteractionDefinitionContext
                {
                    WorldInteractionId =
                        SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
                    AllowedTriggerSourceCodes = new[]
                    {
                        SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
                    },
                    RequiredContextCodes = new[]
                    {
                        SimulationWorldInteractionContextCodes.Initiator,
                        SimulationWorldInteractionContextCodes.Actor,
                        SimulationWorldInteractionContextCodes.Target,
                        SimulationWorldInteractionContextCodes.DataResource,
                        SimulationWorldInteractionContextCodes.Time,
                        SimulationWorldInteractionContextCodes.Spatial,
                    },
                    SpatialApplicabilityCode =
                        SimulationWorldInteractionSpatialEvidenceCodes.Required,
                },
                BoundTriggerSourceCodes = new[]
                {
                    SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
                },
                BoundContextCodes = new[]
                {
                    SimulationWorldInteractionContextCodes.Initiator,
                    SimulationWorldInteractionContextCodes.Actor,
                    SimulationWorldInteractionContextCodes.Target,
                    SimulationWorldInteractionContextCodes.DataResource,
                    SimulationWorldInteractionContextCodes.Time,
                    SimulationWorldInteractionContextCodes.Spatial,
                },
                SpatialEvidenceStateCode =
                    SimulationWorldInteractionSpatialEvidenceCodes.Bound,
            });

        Assert.Equal(SimulationWorldInteractionMaturityStateCodes.ContextBound,
            result.StateCode);
        Assert.Equal("WI-NATURE-05", result.WorldInteractionId);
        Assert.NotNull(result.실행우선순위);
        Assert.Equal("E7Closed", result.실행우선순위!.개발작업상태Code);
        Assert.Equal("E7", result.실행우선순위.목표EvidenceStage);
        Assert.Empty(result.MissingContextCodes);
    }

    [Fact]
    public void 도끼확보WI는_E5에서_Preview확정멱등저장재생을닫는다()
    {
        var request = CreateRequest();
        request.NatureSurvival!.StartsWithAxe = false;
        var aggregate = new 경영SimulationSessionAggregate(request);
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var service = new SimulationNatureSurvivalService(sessions);
        var maturity = new SimulationWorldInteractionMaturityService();
        var beforeRevision = aggregate.Revision;
        var preview = service.Preview(aggregate.SessionStableId, new()
        {
            ObservedWorldRevision = beforeRevision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        });

        Assert.True(preview.CanConfirm);
        Assert.Equal(beforeRevision, aggregate.Revision);
        Assert.Equal(SimulationWorldInteractionSpatialEvidenceCodes.Bound,
            preview.SpatialEvidenceStateCode);
        Assert.NotEmpty(preview.SpatialEvidenceReferenceIds);

        var command = new SimulationNatureSurvivalCommandRequest
        {
            CommandId = "command:single-wi:acquire-axe",
            ExpectedRevision = beforeRevision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        };
        var confirmed = service.Confirm(aggregate.SessionStableId, command);
        var duplicate = service.Confirm(aggregate.SessionStableId, command);

        Assert.True(confirmed.NatureSurvival.HasAxe);
        Assert.Equal(confirmed.Revision, duplicate.Revision);
        Assert.Equal(confirmed.Revision, aggregate.Revision);

        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:single-wi:nature-acquire-axe",
            ExpectedRevision = aggregate.Revision,
        });
        var invocation = Assert.Single(package.CommandLog,
            value => value.WorldInteractionInvocation?.CommandId == command.CommandId)
            .WorldInteractionInvocation!;
        var manifestation = Assert.Single(package.WorldInteractionManifestations,
            value => value.OriginCommandId == command.CommandId);
        var e5 = maturity.ReviewE5(
            new SimulationWorldInteractionE5ManifestationReviewRequest
            {
                Definition = new SimulationWorldInteractionDefinitionContext
                {
                    WorldInteractionId =
                        SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
                    AllowedTriggerSourceCodes = new[]
                    {
                        SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
                    },
                    RequiredContextCodes = Array.Empty<string>(),
                    SpatialApplicabilityCode =
                        SimulationWorldInteractionSpatialEvidenceCodes.Required,
                },
                E4StateCode =
                    SimulationWorldInteractionMaturityStateCodes.ContextBound,
                Invocation = invocation,
                AuthorityTransitionRecorded = manifestation.AfterWorldRevision
                    > manifestation.BeforeWorldRevision,
                TaskOrEffectRecorded =
                    manifestation.TaskOrEffectReferenceIds.Length > 0,
                ResultStateRecorded = manifestation.ResultStateCodes.Length > 0,
                SuccessorOrReturnPathRecorded =
                    manifestation.SuccessorOrReturnCodes.Length > 0,
                SpatialEvidenceStateCode =
                    manifestation.SpatialEvidenceStateCode,
            });

        Assert.Equal(SimulationWorldInteractionMaturityStateCodes.Manifested,
            manifestation.StateCode);
        Assert.Equal(SimulationWorldInteractionMaturityStateCodes.Manifested,
            e5.StateCode);
        Assert.Equal("E7Closed", e5.실행우선순위!.개발작업상태Code);

        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new()
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.True(restored.GetNatureSurvivalState().HasAxe);
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
    }

    [Fact]
    public async Task 도끼확보WI는_LocalProcess와_HostedHTTP에서_같은ReplayHash를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-wi-nature-05-host-parity-" + Guid.NewGuid().ToString("N"));
        var request = CreateRequest();
        request.ClientRequestId = Guid.Parse("b47692b4-e117-4862-9f7f-9420a8067b12");
        request.NatureSurvival!.StartsWithAxe = false;
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var local = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localCreated = await local.Sessions.CreateAsync(request);
            var localPreview = await local.Nature.PreviewAsync(
                localCreated.SessionStableId,
                new SimulationNatureSurvivalActionPreviewRequest
                {
                    ObservedWorldRevision = localCreated.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
                    TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
                });
            var command = new SimulationNatureSurvivalCommandRequest
            {
                CommandId = "command:host-parity:acquire-axe",
                ExpectedRevision = localCreated.Revision,
                PlayerStableId = "player:solo",
                ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
                TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            };
            var localConfirmed = await local.Nature.ConfirmAsync(
                localCreated.SessionStableId, command);
            var localState = await local.Nature.GetAsync(
                localCreated.SessionStableId);
            await local.Sessions.SaveSlotAsync(localCreated.SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-host-parity",
                    ExpectedRevision = localConfirmed.Revision,
                });
            var localPackage = slotStore.Read("slot-host-parity").Package;

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>();
            Assert.NotNull(created);
            Assert.Equal(localCreated.SessionStableId, created!.SessionStableId);

            var remotePreviewResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{created.SessionStableId}/nature-survival/previews",
                new SimulationNatureSurvivalActionPreviewRequest
                {
                    ObservedWorldRevision = created.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
                    TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
                });
            Assert.Equal(HttpStatusCode.OK, remotePreviewResponse.StatusCode);
            var remotePreview = await remotePreviewResponse.Content.ReadFromJsonAsync<
                SimulationNatureSurvivalActionPreviewSnapshot>();
            Assert.NotNull(remotePreview);
            Assert.Equal(localPreview.WorldInteractionId,
                remotePreview!.WorldInteractionId);
            Assert.Equal(localPreview.CanConfirm, remotePreview.CanConfirm);
            Assert.Equal(localPreview.PrimaryOutcomeCode,
                remotePreview.PrimaryOutcomeCode);

            command.ExpectedRevision = created.Revision;
            var confirmResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{created.SessionStableId}/nature-survival/commands",
                command);
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
            var remoteConfirmed = await confirmResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>();
            Assert.NotNull(remoteConfirmed);
            var remoteState = await client.GetFromJsonAsync<
                SimulationNatureSurvivalStateSnapshot>(
                $"/api/simulation/v1/sessions/{created.SessionStableId}/nature-survival");
            Assert.NotNull(remoteState);
            Assert.Equal(localConfirmed.Revision, remoteConfirmed!.Revision);
            Assert.Equal(localState.HasAxe, remoteState.HasAxe);
            Assert.Equal(localState.ProfileRevision, remoteState.ProfileRevision);

            var saveResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{created.SessionStableId}/saves",
                new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteConfirmed.Revision,
                });
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            var remotePackage = await saveResponse.Content.ReadFromJsonAsync<
                SimulationSessionSavePackage>();
            Assert.NotNull(remotePackage);
            Assert.Equal(localPackage.SchemaVersion, remotePackage!.SchemaVersion);
            Assert.Equal(localPackage.SavedWorldRevision,
                remotePackage.SavedWorldRevision);
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Fact]
    public async Task 벌목통나무줍기WI는_LocalProcess와_RemoteHost에서_v28ReplayHash가같다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-wi-nature-18-host-parity-" + Guid.NewGuid().ToString("N"));
        var request = CreateR5Request();
        request.ClientRequestId = Guid.Parse("d708dc89-0ee5-44ba-b304-69a305600518");
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var local = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localSession = await local.Sessions.CreateAsync(request);
            localSession = await local.Nature.ConfirmAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:wi18-parity:harvest",
                    ExpectedRevision = localSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            localSession = await local.Nature.AdvanceRealtimeAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = "command:wi18-parity:harvest-clock",
                    ExpectedRevision = localSession.Revision,
                    ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });
            var localHarvested = await local.Nature.GetAsync(
                localSession.SessionStableId);
            var localDrop = Assert.Single(localHarvested.DroppedTimber);
            var localPreview = await local.Nature.PreviewAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalActionPreviewRequest
                {
                    ObservedWorldRevision = localSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes
                        .CollectDroppedTimber,
                    TargetStableId = localDrop.DroppedTimberStableId,
                });
            Assert.True(localPreview.CanConfirm,
                string.Join(",", localPreview.BlockReasonCodes));
            localSession = await local.Nature.ConfirmAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:wi18-parity:collect",
                    ExpectedRevision = localSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes
                        .CollectDroppedTimber,
                    TargetStableId = localDrop.DroppedTimberStableId,
                });
            var localCollected = await local.Nature.GetAsync(
                localSession.SessionStableId);
            await local.Sessions.SaveSlotAsync(localSession.SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-wi18-parity",
                    ExpectedRevision = localSession.Revision,
                });
            var localPackage = slotStore.Read("slot-wi18-parity").Package;
            var localManifestation = Assert.Single(
                localPackage.WorldInteractionManifestations,
                value => value.OriginCommandId ==
                    "command:wi18-parity:collect");
            Assert.Equal(SimulationWorldInteractionMaturityStateCodes.Manifested,
                localManifestation.StateCode);

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteSession = (await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var sessionRoute = "/api/simulation/v1/sessions/"
                + Uri.EscapeDataString(remoteSession.SessionStableId);
            var harvestResponse = await client.PostAsJsonAsync(
                sessionRoute + "/nature-survival/commands",
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:wi18-parity:harvest",
                    ExpectedRevision = remoteSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            Assert.Equal(HttpStatusCode.OK, harvestResponse.StatusCode);
            remoteSession = (await harvestResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var clockResponse = await client.PostAsJsonAsync(
                sessionRoute + "/nature-survival/clock/advance",
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = "command:wi18-parity:harvest-clock",
                    ExpectedRevision = remoteSession.Revision,
                    ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });
            Assert.Equal(HttpStatusCode.OK, clockResponse.StatusCode);
            remoteSession = (await clockResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var remoteHarvested = await client.GetFromJsonAsync<
                SimulationNatureSurvivalStateSnapshot>(
                sessionRoute + "/nature-survival");
            var remoteDrop = Assert.Single(remoteHarvested!.DroppedTimber);
            Assert.Equal(localDrop.DroppedTimberStableId,
                remoteDrop.DroppedTimberStableId);
            Assert.Equal(localDrop.Quantity, remoteDrop.Quantity);

            var previewResponse = await client.PostAsJsonAsync(
                sessionRoute + "/nature-survival/previews",
                new SimulationNatureSurvivalActionPreviewRequest
                {
                    ObservedWorldRevision = remoteSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes
                        .CollectDroppedTimber,
                    TargetStableId = remoteDrop.DroppedTimberStableId,
                });
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            var remotePreview = (await previewResponse.Content.ReadFromJsonAsync<
                SimulationNatureSurvivalActionPreviewSnapshot>())!;
            Assert.Equal(localPreview.WorldInteractionId,
                remotePreview.WorldInteractionId);
            Assert.Equal(localPreview.TargetDroppedTimberQuantity,
                remotePreview.TargetDroppedTimberQuantity);
            Assert.Equal(localPreview.CanConfirm, remotePreview.CanConfirm);

            var collectResponse = await client.PostAsJsonAsync(
                sessionRoute + "/nature-survival/commands",
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:wi18-parity:collect",
                    ExpectedRevision = remoteSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes
                        .CollectDroppedTimber,
                    TargetStableId = remoteDrop.DroppedTimberStableId,
                });
            Assert.Equal(HttpStatusCode.OK, collectResponse.StatusCode);
            remoteSession = (await collectResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var remoteCollected = await client.GetFromJsonAsync<
                SimulationNatureSurvivalStateSnapshot>(
                sessionRoute + "/nature-survival");
            Assert.NotNull(remoteCollected);
            Assert.Equal(localSession.Revision, remoteSession.Revision);
            Assert.Equal(localCollected.TimberQuantity,
                remoteCollected!.TimberQuantity);
            Assert.Equal(localCollected.DroppedTimber.Single().StateCode,
                remoteCollected.DroppedTimber.Single().StateCode);

            var saveResponse = await client.PostAsJsonAsync(
                sessionRoute + "/saves", new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteSession.Revision,
                });
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            var remotePackage = (await saveResponse.Content.ReadFromJsonAsync<
                SimulationSessionSavePackage>())!;
            Assert.Equal(SimulationSaveSchemaVersions.V28,
                remotePackage.SchemaVersion);
            Assert.Equal(localPackage.SavedWorldRevision,
                remotePackage.SavedWorldRevision);
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Fact]
    public async Task 황혼관찰WI는_LocalProcess와_RemoteHost에서_같은Revision과ReplayHash를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-wi-nature-01-host-parity-"
            + Guid.NewGuid().ToString("N"));
        var request = CreateRequest();
        request.ClientRequestId = Guid.Parse(
            "a16e18b8-544b-48e9-a801-59b706351701");
        request.ScenarioSeed = FindTriggeringSeed(request.ClientRequestId);
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR2;
        request.NatureSurvival.StartsWithAxe = true;
        request.SpatialWorld!.Definitions = request.SpatialWorld.Definitions
            .Concat(PyeongchangSimulation공간상호작용Fixture
                .CreateNatureTwilightActualE5Observation().Definitions)
            .ToArray();
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var local = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localSession = await local.Sessions.CreateAsync(request);
            localSession = await local.Nature.ConfirmAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:wi01-parity:harvest",
                    ExpectedRevision = localSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            localSession = await local.Nature.AdvanceRealtimeAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = "command:wi01-parity:harvest-clock",
                    ExpectedRevision = localSession.Revision,
                    ElapsedRealtimeSeconds =
                        NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });
            var clockSequence = 0;
            var remaining = NatureSurvivalRules.DaylightEndsAtSecond
                - NatureSurvivalRules.HarvestWorkSeconds;
            while (remaining > 0)
            {
                var elapsed = Math.Min(60, remaining);
                localSession = await local.Nature.AdvanceRealtimeAsync(
                    localSession.SessionStableId,
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = "command:wi01-parity:dusk-clock:"
                            + ++clockSequence,
                        ExpectedRevision = localSession.Revision,
                        ElapsedRealtimeSeconds = elapsed,
                    });
                remaining -= elapsed;
            }
            var localNature = await local.Nature.GetAsync(
                localSession.SessionStableId);
            Assert.Equal(SimulationNatureSurvivalCodes.Pending,
                localNature.Encounter!.StateCode);
            var observation = new SimulationNatureThreatObservationPreviewRequest
            {
                ExpectedRevision = localSession.Revision,
                DecisionStableId = "decision:wi01-parity:twilight-observation",
                TaskStableId = "task:wi01-parity:twilight-observation",
                ActorStableId = "player:solo",
                NatureRouteCode =
                    SimulationNatureInteractionCodes.NatureHomeTwilightRoute,
                PreferredSpatialStableId = SimulationNatureSurvivalCodes
                    .ActualE5SpatialStableId("WI-NATURE-01"),
            };
            var localPreview = await local.PreviewNatureThreatObservationAsync(
                localSession.SessionStableId, observation);
            Assert.True(localPreview.CanConfirm,
                string.Join(",", localPreview.BlockingReasonCodes));
            localSession = await local.ConfirmNatureThreatObservationAsync(
                localSession.SessionStableId,
                new SimulationNatureThreatObservationConfirmRequest
                {
                    CommandId = "command:wi01-parity:observe",
                    ExpectedRevision = localSession.Revision,
                    Preview = observation,
                });
            await local.Sessions.SaveSlotAsync(localSession.SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-wi01-host-parity",
                    ExpectedRevision = localSession.Revision,
                });
            var localPackage = slotStore.Read("slot-wi01-host-parity").Package;

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteSession = (await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var sessionRoute = "/api/simulation/v1/sessions/"
                + Uri.EscapeDataString(remoteSession.SessionStableId);
            var harvestResponse = await client.PostAsJsonAsync(
                sessionRoute + "/nature-survival/commands",
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = "command:wi01-parity:harvest",
                    ExpectedRevision = remoteSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            Assert.Equal(HttpStatusCode.OK, harvestResponse.StatusCode);
            remoteSession = (await harvestResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var harvestClockResponse = await client.PostAsJsonAsync(
                sessionRoute + "/nature-survival/clock/advance",
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = "command:wi01-parity:harvest-clock",
                    ExpectedRevision = remoteSession.Revision,
                    ElapsedRealtimeSeconds =
                        NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });
            Assert.Equal(HttpStatusCode.OK, harvestClockResponse.StatusCode);
            remoteSession = (await harvestClockResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            clockSequence = 0;
            remaining = NatureSurvivalRules.DaylightEndsAtSecond
                - NatureSurvivalRules.HarvestWorkSeconds;
            while (remaining > 0)
            {
                var elapsed = Math.Min(60, remaining);
                var clockResponse = await client.PostAsJsonAsync(
                    sessionRoute + "/nature-survival/clock/advance",
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = "command:wi01-parity:dusk-clock:"
                            + ++clockSequence,
                        ExpectedRevision = remoteSession.Revision,
                        ElapsedRealtimeSeconds = elapsed,
                    });
                Assert.Equal(HttpStatusCode.OK, clockResponse.StatusCode);
                remoteSession = (await clockResponse.Content.ReadFromJsonAsync<
                    경영SimulationSessionSnapshot>())!;
                remaining -= elapsed;
            }
            observation.ExpectedRevision = remoteSession.Revision;
            var remotePreviewResponse = await client.PostAsJsonAsync(
                sessionRoute
                + "/world-events/nature-threat/observation-previews",
                observation);
            Assert.Equal(HttpStatusCode.OK, remotePreviewResponse.StatusCode);
            var remotePreview = (await remotePreviewResponse.Content
                .ReadFromJsonAsync<SimulationNatureThreatObservationPreviewSnapshot>())!;
            Assert.Equal(localPreview.EffectivePressure,
                remotePreview.EffectivePressure);
            Assert.Equal(localPreview.PressureLevelCode,
                remotePreview.PressureLevelCode);
            Assert.Equal(localPreview.NextWorldInteractionIds,
                remotePreview.NextWorldInteractionIds);
            Assert.NotNull(localPreview.DecisionPreview.SpatialInteraction);
            Assert.NotNull(remotePreview.DecisionPreview.SpatialInteraction);
            Assert.Equal(localPreview.DecisionPreview.SpatialInteraction!
                    .SelectedSpatialStableId,
                remotePreview.DecisionPreview.SpatialInteraction!
                    .SelectedSpatialStableId);
            var observationResponse = await client.PostAsJsonAsync(
                sessionRoute + "/world-events/nature-threat/observations/confirm",
                new SimulationNatureThreatObservationConfirmRequest
                {
                    CommandId = "command:wi01-parity:observe",
                    ExpectedRevision = remoteSession.Revision,
                    Preview = observation,
                });
            Assert.Equal(HttpStatusCode.OK, observationResponse.StatusCode);
            remoteSession = (await observationResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            Assert.Equal(localSession.Revision, remoteSession.Revision);

            var saveResponse = await client.PostAsJsonAsync(
                sessionRoute + "/saves", new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteSession.Revision,
                });
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            var remotePackage = (await saveResponse.Content.ReadFromJsonAsync<
                SimulationSessionSavePackage>())!;
            Assert.Equal(localPackage.SavedWorldRevision,
                remotePackage.SavedWorldRevision);
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
            var restored = SimulationSessionReplay.Restore(remotePackage);
            var restoredPackage = restored.CreateSavePackage(new()
            {
                SaveStableId = remotePackage.SaveStableId,
                ExpectedRevision = restored.Revision,
            });
            Assert.Equal(remotePackage.ReplayHash,
                restoredPackage.ReplayHash);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Theory]
    [InlineData(SimulationLocalCombatCodes.ObserverOperation)]
    [InlineData(SimulationLocalCombatCodes.DirectAction)]
    public async Task 황혼대응WI는_참여방식별로_LocalProcess와_RemoteHost가_같은결과와ReplayHash를만든다(
        string controlModeCode)
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var suffix = controlModeCode == SimulationLocalCombatCodes.DirectAction
            ? "direct" : "observer";
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-wi-nature-11-host-parity-" + suffix + "-"
            + Guid.NewGuid().ToString("N"));
        var request = CreateRequest();
        request.ClientRequestId = controlModeCode ==
            SimulationLocalCombatCodes.DirectAction
                ? Guid.Parse("cb609835-c3eb-4ae4-844f-0d44777a465f")
                : Guid.Parse("7134804d-2748-4c5c-92ae-509970b23168");
        request.ScenarioSeed = FindTriggeringSeed(request.ClientRequestId);
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR2;
        request.NatureSurvival.StartsWithAxe = true;
        request.SpatialWorld!.Definitions = request.SpatialWorld.Definitions
            .Concat(PyeongchangSimulation공간상호작용Fixture
                .CreateNatureTwilightActualE5Observation().Definitions)
            .ToArray();
        var prefix = "command:wi11-parity:" + suffix + ":";

        async Task<T> PostAsync<T>(string route, object body)
        {
            var response = await client.PostAsJsonAsync(route, body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }

        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var local = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localSession = await local.Sessions.CreateAsync(request);
            localSession = await local.Nature.ConfirmAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = prefix + "harvest",
                    ExpectedRevision = localSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            localSession = await local.Nature.AdvanceRealtimeAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = prefix + "harvest-clock",
                    ExpectedRevision = localSession.Revision,
                    ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });
            var clockSequence = 0;
            var remaining = NatureSurvivalRules.DaylightEndsAtSecond
                - NatureSurvivalRules.HarvestWorkSeconds;
            while (remaining > 0)
            {
                var elapsed = Math.Min(60, remaining);
                localSession = await local.Nature.AdvanceRealtimeAsync(
                    localSession.SessionStableId,
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = prefix + "dusk-clock:" + ++clockSequence,
                        ExpectedRevision = localSession.Revision,
                        ElapsedRealtimeSeconds = elapsed,
                    });
                remaining -= elapsed;
            }
            var localNature = await local.Nature.GetAsync(
                localSession.SessionStableId);
            var encounterStableId = localNature.Encounter!.EncounterStableId;
            var observation = new SimulationNatureThreatObservationPreviewRequest
            {
                ExpectedRevision = localSession.Revision,
                DecisionStableId = "decision:wi11-parity:" + suffix,
                TaskStableId = "task:wi11-parity:" + suffix,
                ActorStableId = "player:solo",
                NatureRouteCode =
                    SimulationNatureInteractionCodes.NatureHomeTwilightRoute,
                PreferredSpatialStableId = SimulationNatureSurvivalCodes
                    .ActualE5SpatialStableId("WI-NATURE-01"),
            };
            var localObservation = await local.PreviewNatureThreatObservationAsync(
                localSession.SessionStableId, observation);
            Assert.True(localObservation.CanConfirm,
                string.Join(",", localObservation.BlockingReasonCodes));
            localSession = await local.ConfirmNatureThreatObservationAsync(
                localSession.SessionStableId,
                new SimulationNatureThreatObservationConfirmRequest
                {
                    CommandId = prefix + "observe",
                    ExpectedRevision = localSession.Revision,
                    Preview = observation,
                });
            localSession = await local.Nature.ConfirmAsync(
                localSession.SessionStableId,
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = prefix + "fight",
                    ExpectedRevision = localSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.ResolveEncounter,
                    TargetStableId = encounterStableId,
                    ChoiceCode = SimulationNatureSurvivalCodes.Fight,
                });
            var localBattlePreview = await local.Battles.PreviewBattleAsync(
                localSession.SessionStableId,
                new SimulationBattleCreatePreviewRequest
                {
                    ExpectedWorldRevision = localSession.Revision,
                    EncounterStableId = encounterStableId,
                    RequestingActorStableId = "player:solo",
                });
            Assert.True(localBattlePreview.CanConfirm,
                string.Join(",", localBattlePreview.BlockingReasonCodes));
            var localBattle = await local.Battles.ConfirmBattleAsync(
                localSession.SessionStableId,
                new SimulationBattleCreateConfirmRequest
                {
                    CommandId = prefix + "battle-create",
                    ExpectedWorldRevision = localSession.Revision,
                    EncounterStableId = encounterStableId,
                    RequestingActorStableId = "player:solo",
                    ExpectedBattleWorldContextHashSha256 = localBattlePreview
                        .LocalWorldContext.ContextHashSha256,
                });
            localBattle = await local.Battles.ConfirmBattleControlModeAsync(
                localSession.SessionStableId, localBattle.BattleStableId,
                new SimulationLocalCombatControlModeConfirmRequest
                {
                    CommandId = prefix + "control-mode",
                    ExpectedBattleRevision = localBattle.BattleRevision,
                    RequestingActorStableId = "player:solo",
                    ControlModeCode = controlModeCode,
                    ExpectedCardLoadoutHashSha256 = localBattle.LocalCombat
                        .FrozenCardLoadoutHashSha256,
                });
            var battleSequence = 0;
            while (localBattle.PhaseCode == SimulationBattleInstanceCodes.Active
                   && battleSequence < 100)
            {
                if (controlModeCode == SimulationLocalCombatCodes.DirectAction)
                {
                    var target = localBattle.LocalCombat.Actors.First(value =>
                        value.SideCode == SimulationLocalCombatCodes.Hostile
                        && value.StateCode == SimulationLocalCombatCodes.Active);
                    localBattle = await local.Battles.ConfirmBattleActionAsync(
                        localSession.SessionStableId, localBattle.BattleStableId,
                        new SimulationLocalCombatActionConfirmRequest
                        {
                            CommandId = prefix + "action:" + battleSequence,
                            ExpectedBattleRevision = localBattle.BattleRevision,
                            RequestingActorStableId = "player:solo",
                            TargetActorStableId = target.ActorStableId,
                            ActionCode = SimulationLocalCombatCodes.BasicAttack,
                        });
                    if (localBattle.PhaseCode !=
                        SimulationBattleInstanceCodes.Active) break;
                }
                localBattle = await local.Battles.AdvanceBattleAsync(
                    localSession.SessionStableId, localBattle.BattleStableId,
                    new SimulationBattleAdvanceRequest
                    {
                        CommandId = prefix + "battle-tick:" + battleSequence++,
                        ExpectedBattleRevision = localBattle.BattleRevision,
                        CombatTickCount = 5,
                    });
            }
            Assert.NotEqual(SimulationBattleInstanceCodes.Active,
                localBattle.PhaseCode);
            var localFinalSession = await local.Sessions.GetAsync(
                localSession.SessionStableId);
            var localFinalNature = await local.Nature.GetAsync(
                localSession.SessionStableId);
            await local.Sessions.SaveSlotAsync(localSession.SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-wi11-parity-" + suffix,
                    ExpectedRevision = localFinalSession.Revision,
                });
            var localPackage = slotStore.Read(
                "slot-wi11-parity-" + suffix).Package;

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteSession = (await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var sessionRoute = "/api/simulation/v1/sessions/"
                + Uri.EscapeDataString(remoteSession.SessionStableId);
            remoteSession = await PostAsync<경영SimulationSessionSnapshot>(
                sessionRoute + "/nature-survival/commands",
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = prefix + "harvest",
                    ExpectedRevision = remoteSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                    TargetStableId = "resource:nature-tree:01",
                });
            remoteSession = await PostAsync<경영SimulationSessionSnapshot>(
                sessionRoute + "/nature-survival/clock/advance",
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = prefix + "harvest-clock",
                    ExpectedRevision = remoteSession.Revision,
                    ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                    WorkInputHeld = true,
                });
            clockSequence = 0;
            remaining = NatureSurvivalRules.DaylightEndsAtSecond
                - NatureSurvivalRules.HarvestWorkSeconds;
            while (remaining > 0)
            {
                var elapsed = Math.Min(60, remaining);
                remoteSession = await PostAsync<경영SimulationSessionSnapshot>(
                    sessionRoute + "/nature-survival/clock/advance",
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = prefix + "dusk-clock:" + ++clockSequence,
                        ExpectedRevision = remoteSession.Revision,
                        ElapsedRealtimeSeconds = elapsed,
                    });
                remaining -= elapsed;
            }
            observation.ExpectedRevision = remoteSession.Revision;
            var remoteObservation = await PostAsync<
                SimulationNatureThreatObservationPreviewSnapshot>(
                sessionRoute
                + "/world-events/nature-threat/observation-previews",
                observation);
            Assert.Equal(localObservation.EffectivePressure,
                remoteObservation.EffectivePressure);
            remoteSession = await PostAsync<경영SimulationSessionSnapshot>(
                sessionRoute + "/world-events/nature-threat/observations/confirm",
                new SimulationNatureThreatObservationConfirmRequest
                {
                    CommandId = prefix + "observe",
                    ExpectedRevision = remoteSession.Revision,
                    Preview = observation,
                });
            remoteSession = await PostAsync<경영SimulationSessionSnapshot>(
                sessionRoute + "/nature-survival/commands",
                new SimulationNatureSurvivalCommandRequest
                {
                    CommandId = prefix + "fight",
                    ExpectedRevision = remoteSession.Revision,
                    PlayerStableId = "player:solo",
                    ActionCode = SimulationNatureSurvivalCodes.ResolveEncounter,
                    TargetStableId = encounterStableId,
                    ChoiceCode = SimulationNatureSurvivalCodes.Fight,
                });
            factory.Services.GetRequiredService<
                    InMemorySimulationTeamObservationPolicyStore>()
                .Replace(new SimulationTeamObservationPolicySnapshot
                {
                    SessionStableId = remoteSession.SessionStableId,
                    TeamStableId = "team:local-player:player:solo",
                    Revision = remoteSession.Revision,
                    MembersCanObserve = true,
                    MemberActorStableIds = ["player:solo"],
                    AllowedViewModeCodes =
                        ["FirstPerson", "TacticalThirdPerson", "ObserverOperation"],
                    SimulationOnly = true,
                    IsOperationalState = false,
                });
            var battleRoute = sessionRoute + "/battles";
            var remoteBattlePreview = await PostAsync<
                SimulationBattleCreatePreviewSnapshot>(battleRoute + "/previews",
                new SimulationBattleCreatePreviewRequest
                {
                    ExpectedWorldRevision = remoteSession.Revision,
                    EncounterStableId = encounterStableId,
                    RequestingActorStableId = "player:solo",
                });
            Assert.Equal(localBattlePreview.LocalWorldContext.ContextHashSha256,
                remoteBattlePreview.LocalWorldContext.ContextHashSha256);
            var remoteBattle = await PostAsync<SimulationBattleInstanceSnapshot>(
                battleRoute + "/confirm",
                new SimulationBattleCreateConfirmRequest
                {
                    CommandId = prefix + "battle-create",
                    ExpectedWorldRevision = remoteSession.Revision,
                    EncounterStableId = encounterStableId,
                    RequestingActorStableId = "player:solo",
                    ExpectedBattleWorldContextHashSha256 = remoteBattlePreview
                        .LocalWorldContext.ContextHashSha256,
                });
            var battleInstanceRoute = battleRoute + "/"
                + Uri.EscapeDataString(remoteBattle.BattleStableId);
            remoteBattle = await PostAsync<SimulationBattleInstanceSnapshot>(
                battleInstanceRoute + "/local-control-mode/confirm",
                new SimulationLocalCombatControlModeConfirmRequest
                {
                    CommandId = prefix + "control-mode",
                    ExpectedBattleRevision = remoteBattle.BattleRevision,
                    RequestingActorStableId = "player:solo",
                    ControlModeCode = controlModeCode,
                    ExpectedCardLoadoutHashSha256 = remoteBattle.LocalCombat
                        .FrozenCardLoadoutHashSha256,
                });
            battleSequence = 0;
            while (remoteBattle.PhaseCode == SimulationBattleInstanceCodes.Active
                   && battleSequence < 100)
            {
                if (controlModeCode == SimulationLocalCombatCodes.DirectAction)
                {
                    var target = remoteBattle.LocalCombat.Actors.First(value =>
                        value.SideCode == SimulationLocalCombatCodes.Hostile
                        && value.StateCode == SimulationLocalCombatCodes.Active);
                    remoteBattle = await PostAsync<SimulationBattleInstanceSnapshot>(
                        battleInstanceRoute + "/local-actions/confirm",
                        new SimulationLocalCombatActionConfirmRequest
                        {
                            CommandId = prefix + "action:" + battleSequence,
                            ExpectedBattleRevision = remoteBattle.BattleRevision,
                            RequestingActorStableId = "player:solo",
                            TargetActorStableId = target.ActorStableId,
                            ActionCode = SimulationLocalCombatCodes.BasicAttack,
                        });
                    if (remoteBattle.PhaseCode !=
                        SimulationBattleInstanceCodes.Active) break;
                }
                remoteBattle = await PostAsync<SimulationBattleInstanceSnapshot>(
                    battleInstanceRoute + "/ticks",
                    new SimulationBattleAdvanceRequest
                    {
                        CommandId = prefix + "battle-tick:" + battleSequence++,
                        ExpectedBattleRevision = remoteBattle.BattleRevision,
                        CombatTickCount = 5,
                    });
            }
            Assert.NotEqual(SimulationBattleInstanceCodes.Active,
                remoteBattle.PhaseCode);
            var remoteNature = await client.GetFromJsonAsync<
                SimulationNatureSurvivalStateSnapshot>(
                sessionRoute + "/nature-survival");
            var remoteFinalSession = await client.GetFromJsonAsync<
                경영SimulationSessionSnapshot>(sessionRoute);
            Assert.NotNull(remoteNature);
            Assert.NotNull(remoteFinalSession);
            Assert.Equal(localBattle.Outcome!.ResultCode,
                remoteBattle.Outcome!.ResultCode);
            Assert.Equal(localBattle.ReplayHashSha256,
                remoteBattle.ReplayHashSha256);
            Assert.Equal(localFinalNature.LastCombatResultCode,
                remoteNature!.LastCombatResultCode);
            Assert.Equal(localFinalNature.Encounter!.StateCode,
                remoteNature.Encounter!.StateCode);
            Assert.Equal(localFinalSession.Revision,
                remoteFinalSession!.Revision);

            var remotePackage = await PostAsync<SimulationSessionSavePackage>(
                sessionRoute + "/saves", new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteFinalSession.Revision,
                });
            Assert.Equal(localPackage.SavedWorldRevision,
                remotePackage.SavedWorldRevision);
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
            var restoreSlot = "slot-wi11-remote-" + suffix;
            slotStore.Write(restoreSlot, remotePackage);
            using var restoredRuntime = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var restored = await restoredRuntime.Sessions.LoadSlotAsync(restoreSlot);
            Assert.Equal(remotePackage.ReplayHash, restored.Restore.ReplayHash);
            Assert.Equal(remoteNature.LastCombatResultCode,
                restored.Restore.Session.NatureSurvival.LastCombatResultCode);
            Assert.Equal(SimulationNatureSurvivalCodes.Resolved,
                restored.Restore.Session.NatureSurvival.Encounter!.StateCode);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Fact]
    public async Task HTTP는_Nature상태검토확정실시간경계를_노출한다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var request = CreateRequest();
        request.ClientRequestId = Guid.NewGuid();
        request.NatureSurvival!.StartsWithAxe = false;
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>();
        Assert.NotNull(created);

        var preview = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created!.SessionStableId}/nature-survival/previews",
            new SimulationNatureSurvivalActionPreviewRequest
            {
                ObservedWorldRevision = created.Revision,
                PlayerStableId = "player:solo",
                ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
                TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewSnapshot = await preview.Content.ReadFromJsonAsync<
            SimulationNatureSurvivalActionPreviewSnapshot>();
        Assert.Equal(SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
            previewSnapshot!.WorldInteractionId);

        var confirm = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created.SessionStableId}/nature-survival/commands",
            new SimulationNatureSurvivalCommandRequest
            {
                CommandId = "command:http:acquire-axe",
                ExpectedRevision = created.Revision,
                PlayerStableId = "player:solo",
                ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
                TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var state = await client.GetFromJsonAsync<SimulationNatureSurvivalStateSnapshot>(
            $"/api/simulation/v1/sessions/{created.SessionStableId}/nature-survival");
        Assert.NotNull(state);
        Assert.True(state!.HasAxe);
        Assert.Equal(SimulationNatureSurvivalCodes.ProfileRevision,
            state.ProfileRevision);
    }

    [Fact]
    public void 벌목집중은_완료때만_현장과명상을지급하고_v29재생을보존한다()
    {
        var request = CreateR5Request();
        request.NatureMind = new SimulationNatureMindInitialStateRequest
        {
            Players = new[]
            {
                new SimulationNatureMindPlayerInitialStateRequest
                {
                    PlayerStableId = "player:solo",
                },
            },
        };
        var aggregate = new 경영SimulationSessionAggregate(request);
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var service = new SimulationNatureSurvivalService(sessions);
        var started = service.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:focus:harvest",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = "resource:nature-tree:01",
        });
        var offered = service.Get(aggregate.SessionStableId)
            .ActiveFocusChallenge!;

        Assert.Equal(0, aggregate.GetPlayerDomainProfile()!
            .분야진척들.Single(value => value.분야StableId ==
                Simulation플레이어분야Codes.채집자원).현장숙련도);
        service.SubmitFocusTiming(aggregate.SessionStableId, new()
        {
            CommandId = "command:focus:attempt",
            ChallengeStableId = offered.ChallengeStableId,
            ExpectedWorldRevision = started.Revision,
            ExpectedChallengeRevision = offered.ChallengeRevision,
            InputOffsetMillis = 500,
        });
        service.AdvanceClock(aggregate.SessionStableId, new()
        {
            CommandId = "command:focus:complete",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });

        var state = service.Get(aggregate.SessionStableId);
        var profile = aggregate.GetPlayerDomainProfile()!;
        Assert.Null(state.ActiveFocusChallenge);
        Assert.Equal(Simulation집중판정Codes.Perfect,
            state.LastFocusResult!.ResultCode);
        Assert.Equal(250, profile.명상경험Milli);
        Assert.Equal(0, profile.명상숙련도);
        Assert.Equal(2, profile.분야진척들.Single(value =>
            value.분야StableId == Simulation플레이어분야Codes.채집자원)
            .현장숙련도);
        Assert.Equal(.25m, aggregate.GetNatureMindState().Balances
            .Single().RecoveryOutput);
        Assert.Contains(aggregate.GetNatureMindState().Effects, value =>
            value.SourceCode == SimulationNatureMindCodes.FocusTimingCompleted
            && value.Magnitude == .25m);
        var completionRecord = aggregate.GetActionManifestationLedger()!
            .TailRecords.Single(value => value.CommandId ==
                "command:focus:harvest:completed");
        Assert.Contains(Simulation행위변화의미Codes.플레이어명상변경,
            completionRecord.변화의미Codes);
        Assert.Contains(Simulation행위변화의미Codes.플레이어회복변경,
            completionRecord.변화의미Codes);
        Assert.Contains(state.LastFocusResult!.SourceStableId,
            completionRecord.SourceReferenceIds);
        Assert.Contains(profile.명상기여기록들.Single().ContributionStableId,
            completionRecord.SourceReferenceIds);

        var saved = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-focus:v29",
            ExpectedRevision = aggregate.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var savedAgain = restored.CreateSavePackage(new()
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V29, saved.SchemaVersion);
        Assert.Equal(saved.ReplayHash, savedAgain.ReplayHash);
        Assert.Equal(250,
            restored.GetPlayerDomainProfile()!.명상경험Milli);
    }

    [Fact]
    public void 벌목취소는_집중을무효화하고_숙련과심리효과를남기지않는다()
    {
        var request = CreateR5Request();
        request.NatureMind = new SimulationNatureMindInitialStateRequest
        {
            Players = new[]
            {
                new SimulationNatureMindPlayerInitialStateRequest
                    { PlayerStableId = "player:solo" },
            },
        };
        var aggregate = new 경영SimulationSessionAggregate(request);
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var service = new SimulationNatureSurvivalService(sessions);
        service.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:focus:cancel-start",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = "resource:nature-tree:01",
        });
        var challenge = service.Get(aggregate.SessionStableId)
            .ActiveFocusChallenge!;
        service.SubmitFocusTiming(aggregate.SessionStableId, new()
        {
            CommandId = "command:focus:cancel-attempt",
            ChallengeStableId = challenge.ChallengeStableId,
            ExpectedWorldRevision = aggregate.Revision,
            ExpectedChallengeRevision = challenge.ChallengeRevision,
            InputOffsetMillis = 500,
        });
        service.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:focus:cancel",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.CancelActiveWork,
            TargetStableId = "resource:nature-tree:01",
        });

        Assert.Equal(Simulation집중판정Codes.Voided,
            service.Get(aggregate.SessionStableId).LastFocusResult!.StateCode);
        Assert.Equal(0, aggregate.GetPlayerDomainProfile()!.명상숙련도);
        Assert.Equal(0, aggregate.GetPlayerDomainProfile()!.명상경험Milli);
        Assert.Empty(aggregate.GetNatureMindState().Effects);
    }

    private static 경영SimulationSessionSnapshot Confirm(
        경영SimulationSessionAggregate aggregate,
        string commandId,
        long expectedRevision,
        string actionCode,
        string target,
        string choice = "",
        double localX = 0,
        double localZ = 0,
        double yaw = 0)
        => aggregate.ConfirmNatureSurvivalAction(new()
        {
            CommandId = commandId,
            ExpectedRevision = expectedRevision,
            PlayerStableId = "player:solo",
            ActionCode = actionCode,
            TargetStableId = target,
            ChoiceCode = choice,
            LocalX = localX,
            LocalZ = localZ,
            YawDegrees = yaw,
        });

    private static int FindTriggeringSeed(Guid clientRequestId)
    {
        var sessionId = "simulation-session:" + clientRequestId.ToString("N");
        for (var seed = 1; seed < 10_000; seed++)
        {
            if (NatureSurvivalRules.RollFirstDuskEncounter(seed, sessionId, 0, 1))
                return seed;
        }
        throw new InvalidOperationException("결정적 조우 seed를 찾지 못했습니다.");
    }

    private static int FindNonTriggeringSeed(Guid clientRequestId,
        int throughCycleExclusive)
    {
        var sessionId = "simulation-session:" + clientRequestId.ToString("N");
        for (var seed = 1; seed < 10_000; seed++)
        {
            if (Enumerable.Range(0, throughCycleExclusive).All(cycle =>
                    !NatureSurvivalRules.RollFirstDuskEncounter(
                        seed, sessionId, cycle, 1)))
                return seed;
        }
        throw new InvalidOperationException(
            "자원 재생 회귀용 비조우 seed를 찾지 못했습니다.");
    }

    private static 경영SimulationSessionAggregate BuildR2Cabin(int extraHarvestCount,
        bool includeThreatObservationSpatial = false)
    {
        var request = CreateRequest();
        if (includeThreatObservationSpatial)
            request.SpatialWorld!.Definitions = request.SpatialWorld.Definitions
                .Concat(PyeongchangSimulation공간상호작용Fixture
                    .CreateNatureThreatObservation().Definitions).ToArray();
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR2;
        var aggregate = new 경영SimulationSessionAggregate(request);
        var harvestCount = 3 + extraHarvestCount;
        for (var index = 1; index <= harvestCount; index++)
        {
            Confirm(aggregate, $"command:r2:tree:{index}", aggregate.Revision,
                SimulationNatureSurvivalCodes.BeginHarvest,
                $"resource:nature-tree:{index:00}");
            aggregate.AdvanceNatureSurvivalClock(new()
            {
                CommandId = $"command:r2:tree-hold:{index}",
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
                WorkInputHeld = true,
            });
        }
        Confirm(aggregate, "command:r2:cabin-place", aggregate.Revision,
            SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
            "facility:nature-cabin", localX: 2, localZ: -2);
        Confirm(aggregate, "command:r2:cabin-build", aggregate.Revision,
            SimulationNatureSurvivalCodes.BeginCabinBuild, "facility:nature-cabin");
        aggregate.AdvanceNatureSurvivalClock(new()
        {
            CommandId = "command:r2:cabin-hold",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.CabinWorkSeconds,
            WorkInputHeld = true,
        });
        return aggregate;
    }

    private static void AdvanceNatureTo(경영SimulationSessionAggregate aggregate,
        int elapsedSecond)
    {
        var command = 0;
        while (aggregate.GetNatureSurvivalState().ElapsedSecondsInCycle < elapsedSecond)
        {
            var remaining = elapsedSecond
                - aggregate.GetNatureSurvivalState().ElapsedSecondsInCycle;
            aggregate.AdvanceNatureSurvivalClock(new()
            {
                CommandId = $"command:r2:advance:{elapsedSecond}:{command++}",
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = Math.Min(60, remaining),
            });
        }
    }

    private static 경영SimulationSession생성Request CreateRequest(bool includeNature = true)
        => new()
        {
            ClientRequestId = Guid.Parse("a18a9c48-88ab-41bf-a6b4-02886629d03c"),
            ScenarioStableId = "scenario:nature-survival-fixture",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 1234,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:solo",
                TerritoryStableId = "territory:nature",
                SettlementStableId = "settlement:nature-home",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 23, 0, 0, 0,
                    TimeSpan.Zero),
            },
            SpatialWorld = new Simulation공간세계InitialStateRequest
            {
                Definitions = new[]
                {
                    new Simulation공간정의InitialRequest
                    {
                        SpatialStableId =
                            SimulationNatureSurvivalCodes.ActualE5SpatialStableId(
                                SimulationNatureSurvivalCodes
                                    .AcquireAxeWorldInteractionId),
                        FacilityStableId = "facility:nature-tool-pickup",
                        AreaStableId = "area:nature-home",
                        AreaSetStableId = "area-set:nature",
                        LandscapeGraphStableId =
                            "landscape-graph:nature-survival-home.v1",
                        LandscapeNodeStableId = "nature-tool-pickup",
                        EvidenceKindCode =
                            Simulation공간근거종류Codes.LandscapeGraph,
                        AccessStateCode =
                            Simulation공간접근상태Codes.Available,
                        CapabilityCodes = new[]
                        {
                            Simulation공간능력Codes.Traversable,
                        },
                        DefinitionRevision =
                            "wi-nature-05.actual-e5.r1",
                        DefinitionHashSha256 =
                            "8f08298c84a82e52b8f977d6652b43472b79b3e755ee66c9698c65973ec95eef",
                        SourceStableIds = new[]
                        {
                            "wi-spatial-seedbed:nature-survival-home.v1",
                            "world-interaction:wi-nature-05",
                        },
                    },
                },
            },
            NatureSurvival = includeNature ? new SimulationNatureSurvivalInitialStateRequest
            {
                PlayerStableId = "player:solo",
                ResourceNodes = Enumerable.Range(1, 6).Select(index =>
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId = $"resource:nature-tree:{index:00}",
                        H2StableId = SimulationNatureSurvivalCodes.HarvestH2StableId,
                        H1StableId = "h1-stock:nature-exploration-buffer",
                        LocalX = -8 + index * 2,
                        LocalZ = 8,
                    }).ToArray(),
            } : null,
        };

    private static 경영SimulationSession생성Request CreateR5Request()
    {
        var request = CreateRequest();
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR5;
        request.NatureSurvival.BuildingProgressionCatalog =
            Simulation영역건물발전Catalog.CreateDefault();
        request.SpatialWorld!.Definitions = request.SpatialWorld.Definitions
            .Concat(PyeongchangSimulation공간상호작용Fixture
                .CreateNatureDroppedTimberActualE5().Definitions)
            .ToArray();
        return request;
    }
}
