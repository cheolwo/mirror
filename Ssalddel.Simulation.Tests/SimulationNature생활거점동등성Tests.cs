using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Nature 생활거점 폐루프의 LocalProcess·RemoteHost 결정성을 검증한다.",
    Boundary = "자동 동등성 증거와 사람의 Play Mode·Game View 증거를 구분한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
public sealed class SimulationNature생활거점동등성Tests
{
    [Fact]
    public async Task 취소재수확오두막저장복원은_LocalProcess와_RemoteHost에서_같은ReplayHash를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-nature-shelter-parity-" + Guid.NewGuid().ToString("N"));
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var localRuntime = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var request = CreateRequest();
            var localCreated = await localRuntime.Sessions.CreateAsync(request);
            var localFinal = await RunLocalLoopAsync(localRuntime,
                localCreated.SessionStableId, localCreated.Revision);
            var localSaved = await localRuntime.Sessions.SaveSlotAsync(
                localCreated.SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-nature-shelter-parity",
                    ExpectedRevision = localFinal.Revision,
                });
            var localPackage = slotStore.Read("slot-nature-shelter-parity").Package;

            AssertClosedLoop(localFinal);
            Assert.Equal(15, localFinal.Revision);
            Assert.Equal(SimulationSaveSchemaVersions.V28,
                localPackage.SchemaVersion);

            using (var restoredRuntime = new LocalSimulationRuntime(
                       new InMemory경영SimulationSessionStore(),
                       new InMemorySimulationSessionSaveStore(), slotStore))
            {
                var restored = await restoredRuntime.Sessions.LoadSlotAsync(
                    "slot-nature-shelter-parity");
                Assert.Equal(localSaved.ReplayHash, restored.Restore.ReplayHash);
                AssertClosedLoop(restored.Restore.Session);
            }

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", CreateRequest());
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteCreated = await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>();
            Assert.NotNull(remoteCreated);
            Assert.Equal(localCreated.SessionStableId,
                remoteCreated!.SessionStableId);

            var remoteFinal = await RunRemoteLoopAsync(client,
                remoteCreated.SessionStableId, remoteCreated.Revision);
            AssertClosedLoop(remoteFinal);
            Assert.Equal(localFinal.Revision, remoteFinal.Revision);

            var saveResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{remoteCreated.SessionStableId}/saves",
                new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteFinal.Revision,
                });
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            var remotePackage = await saveResponse.Content.ReadFromJsonAsync<
                SimulationSessionSavePackage>();
            Assert.NotNull(remotePackage);
            Assert.Equal(localPackage.SchemaVersion, remotePackage!.SchemaVersion);
            Assert.Equal(localPackage.SavedWorldRevision,
                remotePackage.SavedWorldRevision);
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);

            var verificationResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions/replay-verifications",
                new SimulationSessionRestoreRequest
                {
                    SaveStableId = remotePackage.SaveStableId,
                });
            Assert.Equal(HttpStatusCode.OK, verificationResponse.StatusCode);
            var verified = await verificationResponse.Content.ReadFromJsonAsync<
                SimulationSessionRestoreResult>();
            Assert.NotNull(verified);
            Assert.Equal(remotePackage.ReplayHash, verified!.ReplayHash);
            AssertClosedLoop(verified.Session);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Fact]
    public async Task 거점보관WI는_LocalProcess와_RemoteHost에서_같은Transfer와ReplayHash를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-nature-storage-parity-" + Guid.NewGuid().ToString("N"));
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var localRuntime = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localCreated = await localRuntime.Sessions.CreateAsync(
                CreateR2Request());
            var localFinal = await RunLocalStorageLoopAsync(localRuntime,
                localCreated.SessionStableId, localCreated.Revision);
            var localSaved = await localRuntime.Sessions.SaveSlotAsync(
                localCreated.SessionStableId, new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-nature-storage-parity",
                    ExpectedRevision = localFinal.Revision,
                });
            var localPackage = slotStore.Read("slot-nature-storage-parity").Package;
            AssertStorageClosed(localFinal);
            AssertStorageTransfer(localPackage.WorldInventory);

            using (var restoredRuntime = new LocalSimulationRuntime(
                       new InMemory경영SimulationSessionStore(),
                       new InMemorySimulationSessionSaveStore(), slotStore))
            {
                var restored = await restoredRuntime.Sessions.LoadSlotAsync(
                    "slot-nature-storage-parity");
                Assert.Equal(localSaved.ReplayHash, restored.Restore.ReplayHash);
                AssertStorageClosed(restored.Restore.Session);
            }

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", CreateR2Request());
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteCreated = await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>();
            Assert.NotNull(remoteCreated);
            var remoteFinal = await RunRemoteStorageLoopAsync(client,
                remoteCreated!.SessionStableId, remoteCreated.Revision);
            AssertStorageClosed(remoteFinal);
            Assert.Equal(localFinal.Revision, remoteFinal.Revision);

            var saveResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{remoteCreated.SessionStableId}/saves",
                new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteFinal.Revision,
                });
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            var remotePackage = await saveResponse.Content.ReadFromJsonAsync<
                SimulationSessionSavePackage>();
            Assert.NotNull(remotePackage);
            Assert.Equal(localPackage.SchemaVersion, remotePackage!.SchemaVersion);
            Assert.Equal(localPackage.SavedWorldRevision,
                remotePackage.SavedWorldRevision);
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
            AssertStorageTransfer(remotePackage.WorldInventory);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Fact]
    public async Task 수면WI는_LocalProcess와_RemoteHost에서_같은새벽과ReplayHash를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-nature-sleep-parity-" + Guid.NewGuid().ToString("N"));
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var localRuntime = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localCreated = await localRuntime.Sessions.CreateAsync(
                CreateR2Request());
            var localFinal = await RunLocalSleepLoopAsync(localRuntime,
                localCreated.SessionStableId, localCreated.Revision);
            var localSaved = await localRuntime.Sessions.SaveSlotAsync(
                localCreated.SessionStableId, new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-nature-sleep-parity",
                    ExpectedRevision = localFinal.Revision,
                });
            var localPackage = slotStore.Read("slot-nature-sleep-parity").Package;
            AssertDawnReached(localFinal);

            using (var restoredRuntime = new LocalSimulationRuntime(
                       new InMemory경영SimulationSessionStore(),
                       new InMemorySimulationSessionSaveStore(), slotStore))
            {
                var restored = await restoredRuntime.Sessions.LoadSlotAsync(
                    "slot-nature-sleep-parity");
                Assert.Equal(localSaved.ReplayHash, restored.Restore.ReplayHash);
                AssertDawnReached(restored.Restore.Session);
            }

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", CreateR2Request());
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteCreated = await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>();
            Assert.NotNull(remoteCreated);
            var remoteFinal = await RunRemoteSleepLoopAsync(client,
                remoteCreated!.SessionStableId, remoteCreated.Revision);
            AssertDawnReached(remoteFinal);
            Assert.Equal(localFinal.Revision, remoteFinal.Revision);

            var saveResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{remoteCreated.SessionStableId}/saves",
                new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteFinal.Revision,
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
    public async Task Day2계획WI는_LocalProcess와_RemoteHost에서_같은선택과ReplayHash를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-nature-day2-parity-" + Guid.NewGuid().ToString("N"));
        try
        {
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var localRuntime = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localCreated = await localRuntime.Sessions.CreateAsync(
                CreateR2Request());
            var localFinal = await RunLocalDay2LoopAsync(localRuntime,
                localCreated.SessionStableId, localCreated.Revision);
            var localSaved = await localRuntime.Sessions.SaveSlotAsync(
                localCreated.SessionStableId, new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-nature-day2-parity",
                    ExpectedRevision = localFinal.Revision,
                });
            var localPackage = slotStore.Read("slot-nature-day2-parity").Package;
            AssertDay2Ready(localFinal);

            using (var restoredRuntime = new LocalSimulationRuntime(
                       new InMemory경영SimulationSessionStore(),
                       new InMemorySimulationSessionSaveStore(), slotStore))
            {
                var restored = await restoredRuntime.Sessions.LoadSlotAsync(
                    "slot-nature-day2-parity");
                Assert.Equal(localSaved.ReplayHash, restored.Restore.ReplayHash);
                AssertDay2Ready(restored.Restore.Session);
            }

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", CreateR2Request());
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteCreated = await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>();
            Assert.NotNull(remoteCreated);
            var remoteFinal = await RunRemoteDay2LoopAsync(client,
                remoteCreated!.SessionStableId, remoteCreated.Revision);
            AssertDay2Ready(remoteFinal);
            Assert.Equal(localFinal.Revision, remoteFinal.Revision);

            var saveResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{remoteCreated.SessionStableId}/saves",
                new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteFinal.Revision,
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
    public async Task 보관수면Day2자원재생은_한Session계보로조회되고_중복과경합을거부한다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-nature-day2-regrowth-" + Guid.NewGuid().ToString("N"));
        try
        {
            var request = CreateR2Request();
            var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
            using var local = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var localCreated = await local.Sessions.CreateAsync(request);
            var localCurrent = await RunLocalDay2LoopAsync(local,
                localCreated.SessionStableId, localCreated.Revision);

            var createResponse = await client.PostAsJsonAsync(
                "/api/simulation/v1/sessions", request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var remoteCreated = (await createResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
            var remoteCurrent = await RunRemoteDay2LoopAsync(client,
                remoteCreated.SessionStableId, remoteCreated.Revision);
            var remoteRoute = "/api/simulation/v1/sessions/" +
                Uri.EscapeDataString(remoteCreated.SessionStableId);

            SimulationNatureSurvivalClockAdvanceRequest? regeneration = null;
            var sequence = 0;
            while (localCurrent.NatureSurvival.CycleIndex <
                   NatureSurvivalRules.TreeRegrowthCycleCount)
            {
                var elapsed = Math.Min(60, NatureSurvivalRules.CycleSeconds -
                    localCurrent.NatureSurvival.ElapsedSecondsInCycle);
                var command = Clock($"nature-day2:regrowth:{sequence++}",
                    localCurrent.Revision, elapsed);
                if (localCurrent.NatureSurvival.CycleIndex ==
                        NatureSurvivalRules.TreeRegrowthCycleCount - 1
                    && localCurrent.NatureSurvival.ElapsedSecondsInCycle + elapsed
                    >= NatureSurvivalRules.CycleSeconds)
                    regeneration = command;
                localCurrent = await local.Nature.AdvanceRealtimeAsync(
                    localCreated.SessionStableId, command);
                remoteCurrent = await RemoteAdvanceAsync(client,
                    remoteCreated.SessionStableId, Clock(command.CommandId,
                        remoteCurrent.Revision, elapsed));
            }
            Assert.NotNull(regeneration);
            Assert.Equal(localCurrent.Revision, remoteCurrent.Revision);

            var localObserved = await local.GetNature표현관측Async(
                localCreated.SessionStableId);
            var remoteObserved = await client.GetFromJsonAsync<
                SimulationNature표현관측Snapshot>(remoteRoute +
                    "/nature-survival/observation");
            Assert.NotNull(remoteObserved);
            Assert.Equal(localObserved.Session.Revision,
                remoteObserved!.Session.Revision);
            Assert.Equal(localObserved.Nature.StoredTimberQuantity,
                remoteObserved.Nature.StoredTimberQuantity);
            Assert.True(localObserved.Nature.Day2Ready);
            Assert.Equal(SimulationNatureSurvivalCodes.Workbench,
                localObserved.Nature.SelectedExpansionPlanCode);
            Assert.Equal(SimulationNatureSurvivalCodes.Standing,
                localObserved.Nature.ResourceNodes.Single(value =>
                    value.ResourceNodeStableId == "resource:nature-tree:01")
                    .StateCode);
            Assert.Contains(localObserved.PlayerOpportunities, value =>
                value.ActionCode == SimulationNatureSurvivalCodes.BeginHarvest
                && value.TargetStableId == "resource:nature-tree:01"
                && value.Available);

            var records = localObserved.ActionLedger!.TailRecords;
            Assert.Contains(records, value => value.WorldInteractionId ==
                SimulationNatureSurvivalCodes.StoreAtCabinWorldInteractionId);
            Assert.Contains(records, value => value.WorldInteractionId ==
                SimulationNatureSurvivalCodes.SleepInCabinWorldInteractionId);
            Assert.Contains(records, value => value.WorldInteractionId ==
                SimulationNatureSurvivalCodes.SelectExpansionPlanWorldInteractionId);
            var regenerated = Assert.Single(records, value =>
                value.WorldInteractionId ==
                    Simulation세계자원재생Codes.WorldInteractionId);
            Assert.Equal(regeneration!.CommandId, regenerated.CommandId);
            Assert.Equal(localObserved.Session.Revision,
                regenerated.AfterWorldRevision);
            Assert.Contains("origin-command:nature-storage:tree:1:start",
                regenerated.SourceReferenceIds);

            var recordCount = records.Length;
            var duplicate = await local.Nature.AdvanceRealtimeAsync(
                localCreated.SessionStableId, regeneration);
            Assert.Equal(localCurrent.Revision, duplicate.Revision);
            Assert.Equal(recordCount, (await local.GetNature표현관측Async(
                localCreated.SessionStableId)).ActionLedger!.TailRecords.Length);
            var remoteDuplicate = await client.PostAsJsonAsync(remoteRoute +
                "/nature-survival/clock/advance", regeneration);
            Assert.Equal(HttpStatusCode.OK, remoteDuplicate.StatusCode);
            Assert.Equal(recordCount, (await client.GetFromJsonAsync<
                SimulationNature표현관측Snapshot>(remoteRoute +
                    "/nature-survival/observation"))!.ActionLedger!.TailRecords.Length);

            var stale = Clock("nature-day2:stale", localCurrent.Revision - 1, 0);
            var staleError = await Assert.ThrowsAsync<SimulationConflictException>(
                () => local.Nature.AdvanceRealtimeAsync(
                    localCreated.SessionStableId, stale).AsTask());
            Assert.Equal(SimulationNatureSurvivalCodes.ExpectedRevisionMismatch,
                staleError.Message);
            var remoteStale = await client.PostAsJsonAsync(remoteRoute +
                "/nature-survival/clock/advance", stale);
            Assert.Equal(HttpStatusCode.Conflict, remoteStale.StatusCode);

            await local.Sessions.SaveSlotAsync(localCreated.SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = "slot-nature-day2-regrowth",
                    ExpectedRevision = localCurrent.Revision,
                });
            var localPackage = slotStore.Read(
                "slot-nature-day2-regrowth").Package;
            var remoteSave = await client.PostAsJsonAsync(remoteRoute + "/saves",
                new SimulationSessionSaveRequest
                {
                    SaveStableId = localPackage.SaveStableId,
                    ExpectedRevision = remoteCurrent.Revision,
                });
            Assert.Equal(HttpStatusCode.OK, remoteSave.StatusCode);
            var remotePackage = (await remoteSave.Content.ReadFromJsonAsync<
                SimulationSessionSavePackage>())!;
            Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);

            using var restored = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), slotStore);
            var restoredResult = await restored.Sessions.LoadSlotAsync(
                "slot-nature-day2-regrowth");
            var restoredObserved = await restored.GetNature표현관측Async(
                restoredResult.Restore.Session.SessionStableId);
            Assert.Equal(localObserved.Session.Revision,
                restoredObserved.Session.Revision);
            Assert.True(restoredObserved.Nature.Day2Ready);
            Assert.Single(restoredObserved.ActionLedger!.TailRecords, value =>
                value.WorldInteractionId ==
                    Simulation세계자원재생Codes.WorldInteractionId);

            var raceRevision = localObserved.Session.Revision;
            async Task<string> RaceAsync(string commandId)
            {
                try
                {
                    await local.Nature.AdvanceRealtimeAsync(
                        localCreated.SessionStableId,
                        Clock(commandId, raceRevision, 0));
                    return "Applied";
                }
                catch (SimulationConflictException error)
                {
                    return error.Message;
                }
            }
            var raceResults = await Task.WhenAll(
                Task.Run(() => RaceAsync("nature-day2:race:a")),
                Task.Run(() => RaceAsync("nature-day2:race:b")));
            Assert.Single(raceResults, value => value == "Applied");
            Assert.Single(raceResults, value => value ==
                SimulationNatureSurvivalCodes.ExpectedRevisionMismatch);
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    [Fact]
    public async Task 작업대건설취소재시도는_LocalProcess와_RemoteHost에서_같은운영상태와ReplayHash를만든다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-nature-workbench-parity-" + Guid.NewGuid().ToString("N"));
        var slotStore = new FileSimulationLocalSaveSlotStore(savesRoot);
        using var localRuntime = new LocalSimulationRuntime(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(), slotStore);
        var request = CreateR4Request();
        var localCreated = await localRuntime.Sessions.CreateAsync(request);
        var localFinal = await RunWorkbenchLoopAsync(
            command => localRuntime.Nature.ConfirmAsync(
                localCreated.SessionStableId, command).AsTask(),
            clock => localRuntime.Nature.AdvanceRealtimeAsync(
                localCreated.SessionStableId, clock).AsTask(),
            localCreated.Revision);
        AssertWorkbenchOperational(localFinal);
        await localRuntime.Sessions.SaveSlotAsync(
            localCreated.SessionStableId, new SimulationLocalSaveSlotRequest
            {
                SlotStableId = "slot-nature-workbench-parity",
                ExpectedRevision = localFinal.Revision,
            });
        var localPackage = slotStore.Read("slot-nature-workbench-parity").Package;

        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateR4Request());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var remoteCreated = (await createResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>())!;
        var remoteFinal = await RunWorkbenchLoopAsync(
            command => RemoteConfirmAsync(client, remoteCreated.SessionStableId,
                command),
            clock => RemoteAdvanceAsync(client, remoteCreated.SessionStableId,
                clock), remoteCreated.Revision);
        AssertWorkbenchOperational(remoteFinal);
        Assert.Equal(localFinal.Revision, remoteFinal.Revision);

        var saveResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{remoteCreated.SessionStableId}/saves",
            new SimulationSessionSaveRequest
            {
                SaveStableId = localPackage.SaveStableId,
                ExpectedRevision = remoteFinal.Revision,
            });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var remotePackage = (await saveResponse.Content.ReadFromJsonAsync<
            SimulationSessionSavePackage>())!;
        Assert.Equal(localPackage.SchemaVersion, remotePackage.SchemaVersion);
        Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
        if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
    }

    private static async Task<경영SimulationSessionSnapshot>
        RunWorkbenchLoopAsync(
            Func<SimulationNatureSurvivalCommandRequest,
                Task<경영SimulationSessionSnapshot>> confirm,
            Func<SimulationNatureSurvivalClockAdvanceRequest,
                Task<경영SimulationSessionSnapshot>> advance,
            long initialRevision)
    {
        var current = await confirm(Command("workbench:axe", initialRevision,
            SimulationNatureSurvivalCodes.AcquireAxe,
            SimulationNatureSurvivalCodes.AxePickupStableId));
        for (var index = 1; index <= 6; index++)
        {
            current = await confirm(Command($"workbench:tree:{index}:start",
                current.Revision, SimulationNatureSurvivalCodes.BeginHarvest,
                $"resource:nature-tree:{index:00}"));
            current = await advance(Clock($"workbench:tree:{index}:complete",
                current.Revision, NatureSurvivalRules.HarvestWorkSeconds));
        }
        current = await confirm(Command("workbench:cabin:place", current.Revision,
            SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
            "facility:nature-cabin", localX: 2, localZ: -2));
        current = await confirm(Command("workbench:cabin:build", current.Revision,
            SimulationNatureSurvivalCodes.BeginCabinBuild,
            "facility:nature-cabin"));
        current = await advance(Clock("workbench:cabin:complete", current.Revision,
            NatureSurvivalRules.CabinWorkSeconds));
        current = await confirm(Command("workbench:cabin:enter", current.Revision,
            SimulationNatureSurvivalCodes.EnterCabin, "facility:nature-cabin"));
        current = await confirm(Command("workbench:cabin:store", current.Revision,
            SimulationNatureSurvivalCodes.StoreAtCabin,
            SimulationNatureSurvivalCodes.CabinStorageContainerStableId));
        current = await AdvanceToAsync(advance, current,
            NatureSurvivalRules.DaylightEndsAtSecond + 1, "workbench:dusk");
        var encounterId = current.NatureSurvival.Encounter!.EncounterStableId;
        current = await confirm(Command("workbench:fight", current.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter, encounterId,
            choiceCode: SimulationNatureSurvivalCodes.Fight));
        var victory = Command("workbench:victory", current.Revision,
            SimulationNatureSurvivalCodes.ResolveEncounter, encounterId,
            choiceCode: SimulationNatureSurvivalCodes.Victory);
        current = await confirm(victory);
        current = await AdvanceToAsync(advance, current,
            NatureSurvivalRules.DuskEndsAtSecond, "workbench:night");
        current = await confirm(Command("workbench:sleep", current.Revision,
            SimulationNatureSurvivalCodes.SleepInCabin, "facility:nature-cabin"));
        current = await advance(Clock("workbench:dawn", current.Revision, 60));
        current = await confirm(Command("workbench:plan", current.Revision,
            SimulationNatureSurvivalCodes.SelectExpansionPlan,
            "plan:nature:day2", choiceCode: SimulationNatureSurvivalCodes.Workbench));

        current = await confirm(Command("workbench:cancel:start", current.Revision,
            Simulation영역건물발전Codes.BeginBuildingConstruction,
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
            localX: 10, localZ: -2));
        current = await confirm(Command("workbench:cancel", current.Revision,
            SimulationNatureSurvivalCodes.CancelActiveWork,
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint));
        current = await confirm(Command("workbench:retry:start", current.Revision,
            Simulation영역건물발전Codes.BeginBuildingConstruction,
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
            localX: 10, localZ: -2));
        return await advance(Clock("workbench:complete", current.Revision, 20));
    }

    private static async Task<경영SimulationSessionSnapshot> AdvanceToAsync(
        Func<SimulationNatureSurvivalClockAdvanceRequest,
            Task<경영SimulationSessionSnapshot>> advance,
        경영SimulationSessionSnapshot current, int target, string prefix)
    {
        var sequence = 0;
        while (current.NatureSurvival.ElapsedSecondsInCycle < target)
        {
            var elapsed = Math.Min(60,
                target - current.NatureSurvival.ElapsedSecondsInCycle);
            current = await advance(Clock($"{prefix}:{++sequence}",
                current.Revision, elapsed));
        }
        return current;
    }

    private static void AssertWorkbenchOperational(
        경영SimulationSessionSnapshot value)
    {
        Assert.Null(value.NatureSurvival.ActiveWork);
        var node = value.NatureSurvival.BuildingProgression!.Nodes.Single(node =>
            node.BlueprintStableId ==
            Simulation영역건물발전Codes.NatureWorkbenchBlueprint);
        Assert.Equal(Simulation영역건물발전Codes.Operational, node.StateCode);
        Assert.Equal("h1:nature:workbench", node.H1StableId);
        Assert.Equal(10, node.LocalX);
        Assert.True(value.NatureSurvival.Day2Ready);
    }

    private static async Task<경영SimulationSessionSnapshot>
        RunLocalStorageLoopAsync(LocalSimulationRuntime runtime,
            string sessionStableId, long initialRevision)
    {
        var current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-storage:axe", initialRevision,
                SimulationNatureSurvivalCodes.AcquireAxe,
                SimulationNatureSurvivalCodes.AxePickupStableId));
        for (var index = 1; index <= 4; index++)
        {
            current = await runtime.Nature.ConfirmAsync(sessionStableId,
                Command($"nature-storage:tree:{index}:start", current.Revision,
                    SimulationNatureSurvivalCodes.BeginHarvest,
                    $"resource:nature-tree:{index:00}"));
            current = await runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
                Clock($"nature-storage:tree:{index}:complete", current.Revision,
                    NatureSurvivalRules.HarvestWorkSeconds));
        }
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-storage:cabin:place", current.Revision,
                SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
                "facility:nature-cabin", localX: 2, localZ: -2));
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-storage:cabin:build", current.Revision,
                SimulationNatureSurvivalCodes.BeginCabinBuild,
                "facility:nature-cabin"));
        current = await runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
            Clock("nature-storage:cabin:complete", current.Revision,
                NatureSurvivalRules.CabinWorkSeconds));
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-storage:cabin:enter", current.Revision,
                SimulationNatureSurvivalCodes.EnterCabin,
                "facility:nature-cabin"));
        return await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-storage:store", current.Revision,
                SimulationNatureSurvivalCodes.StoreAtCabin,
                SimulationNatureSurvivalCodes.CabinStorageContainerStableId));
    }

    private static async Task<경영SimulationSessionSnapshot>
        RunRemoteStorageLoopAsync(HttpClient client, string sessionStableId,
            long initialRevision)
    {
        var current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-storage:axe", initialRevision,
                SimulationNatureSurvivalCodes.AcquireAxe,
                SimulationNatureSurvivalCodes.AxePickupStableId));
        for (var index = 1; index <= 4; index++)
        {
            current = await RemoteConfirmAsync(client, sessionStableId,
                Command($"nature-storage:tree:{index}:start", current.Revision,
                    SimulationNatureSurvivalCodes.BeginHarvest,
                    $"resource:nature-tree:{index:00}"));
            current = await RemoteAdvanceAsync(client, sessionStableId,
                Clock($"nature-storage:tree:{index}:complete", current.Revision,
                    NatureSurvivalRules.HarvestWorkSeconds));
        }
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-storage:cabin:place", current.Revision,
                SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
                "facility:nature-cabin", localX: 2, localZ: -2));
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-storage:cabin:build", current.Revision,
                SimulationNatureSurvivalCodes.BeginCabinBuild,
                "facility:nature-cabin"));
        current = await RemoteAdvanceAsync(client, sessionStableId,
            Clock("nature-storage:cabin:complete", current.Revision,
                NatureSurvivalRules.CabinWorkSeconds));
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-storage:cabin:enter", current.Revision,
                SimulationNatureSurvivalCodes.EnterCabin,
                "facility:nature-cabin"));
        return await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-storage:store", current.Revision,
                SimulationNatureSurvivalCodes.StoreAtCabin,
                SimulationNatureSurvivalCodes.CabinStorageContainerStableId));
    }

    private static async Task<경영SimulationSessionSnapshot>
        RunLocalSleepLoopAsync(LocalSimulationRuntime runtime,
            string sessionStableId, long initialRevision)
    {
        var current = await RunLocalStorageLoopAsync(runtime, sessionStableId,
            initialRevision);
        current = await AdvanceLocalToAsync(runtime, sessionStableId, current,
            NatureSurvivalRules.DaylightEndsAtSecond, "nature-sleep:dusk");
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-sleep:retreat", current.Revision,
                SimulationNatureSurvivalCodes.ResolveEncounter,
                current.NatureSurvival.Encounter!.EncounterStableId,
                choiceCode: SimulationNatureSurvivalCodes.Retreat));
        current = await AdvanceLocalToAsync(runtime, sessionStableId, current,
            NatureSurvivalRules.DuskEndsAtSecond, "nature-sleep:night");
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-sleep:start", current.Revision,
                SimulationNatureSurvivalCodes.SleepInCabin,
                "facility:nature-cabin"));
        return await runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
            Clock("nature-sleep:dawn", current.Revision, 60));
    }

    private static async Task<경영SimulationSessionSnapshot>
        RunRemoteSleepLoopAsync(HttpClient client, string sessionStableId,
            long initialRevision)
    {
        var current = await RunRemoteStorageLoopAsync(client, sessionStableId,
            initialRevision);
        current = await AdvanceRemoteToAsync(client, sessionStableId, current,
            NatureSurvivalRules.DaylightEndsAtSecond, "nature-sleep:dusk");
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-sleep:retreat", current.Revision,
                SimulationNatureSurvivalCodes.ResolveEncounter,
                current.NatureSurvival.Encounter!.EncounterStableId,
                choiceCode: SimulationNatureSurvivalCodes.Retreat));
        current = await AdvanceRemoteToAsync(client, sessionStableId, current,
            NatureSurvivalRules.DuskEndsAtSecond, "nature-sleep:night");
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-sleep:start", current.Revision,
                SimulationNatureSurvivalCodes.SleepInCabin,
                "facility:nature-cabin"));
        return await RemoteAdvanceAsync(client, sessionStableId,
            Clock("nature-sleep:dawn", current.Revision, 60));
    }

    private static async Task<경영SimulationSessionSnapshot>
        RunLocalDay2LoopAsync(LocalSimulationRuntime runtime,
            string sessionStableId, long initialRevision)
    {
        var current = await RunLocalSleepLoopAsync(runtime, sessionStableId,
            initialRevision);
        return await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-day2:workbench", current.Revision,
                SimulationNatureSurvivalCodes.SelectExpansionPlan,
                "plan:nature:day2",
                choiceCode: SimulationNatureSurvivalCodes.Workbench));
    }

    private static async Task<경영SimulationSessionSnapshot>
        RunRemoteDay2LoopAsync(HttpClient client, string sessionStableId,
            long initialRevision)
    {
        var current = await RunRemoteSleepLoopAsync(client, sessionStableId,
            initialRevision);
        return await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-day2:workbench", current.Revision,
                SimulationNatureSurvivalCodes.SelectExpansionPlan,
                "plan:nature:day2",
                choiceCode: SimulationNatureSurvivalCodes.Workbench));
    }

    private static async Task<경영SimulationSessionSnapshot> AdvanceLocalToAsync(
        LocalSimulationRuntime runtime, string sessionStableId,
        경영SimulationSessionSnapshot current, int targetElapsedSeconds,
        string commandPrefix)
    {
        var index = 0;
        while (current.NatureSurvival.ElapsedSecondsInCycle < targetElapsedSeconds)
        {
            var elapsed = Math.Min(60, targetElapsedSeconds
                - current.NatureSurvival.ElapsedSecondsInCycle);
            current = await runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
                Clock($"{commandPrefix}:{++index}", current.Revision, elapsed));
        }
        return current;
    }

    private static async Task<경영SimulationSessionSnapshot> AdvanceRemoteToAsync(
        HttpClient client, string sessionStableId,
        경영SimulationSessionSnapshot current, int targetElapsedSeconds,
        string commandPrefix)
    {
        var index = 0;
        while (current.NatureSurvival.ElapsedSecondsInCycle < targetElapsedSeconds)
        {
            var elapsed = Math.Min(60, targetElapsedSeconds
                - current.NatureSurvival.ElapsedSecondsInCycle);
            current = await RemoteAdvanceAsync(client, sessionStableId,
                Clock($"{commandPrefix}:{++index}", current.Revision, elapsed));
        }
        return current;
    }

    private static void AssertStorageClosed(경영SimulationSessionSnapshot value)
    {
        Assert.NotNull(value.NatureSurvival);
        Assert.Equal(0, value.NatureSurvival!.TimberQuantity);
        Assert.Equal(2, value.NatureSurvival.StoredTimberQuantity);
        Assert.True(value.NatureSurvival.PlayerInsideCabin);
    }

    private static void AssertStorageTransfer(
        SimulationWorldInventorySnapshot inventory)
    {
        Assert.Contains(inventory.Containers, container =>
            container.ContainerStableId ==
            SimulationNatureSurvivalCodes.CabinStorageContainerStableId);
        Assert.Contains(inventory.Transfers, transfer =>
            transfer.BuildingStableId == "facility:nature-cabin"
            && transfer.ItemCode == SimulationNatureSurvivalCodes.TimberItemCode
            && transfer.Quantity == 2);
    }

    private static void AssertDawnReached(경영SimulationSessionSnapshot value)
    {
        Assert.NotNull(value.NatureSurvival);
        Assert.Equal(NatureSurvivalClockPhaseCodes.Dawn,
            value.NatureSurvival.ClockPhaseCode);
        Assert.Equal(NatureSurvivalRules.NightEndsAtSecond,
            value.NatureSurvival.ElapsedSecondsInCycle);
        Assert.False(value.NatureSurvival.Sleeping);
        Assert.True(value.NatureSurvival.PlayerInsideCabin);
        Assert.Equal(2, value.NatureSurvival.StoredTimberQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.Resolved,
            value.NatureSurvival.Encounter?.StateCode);
    }

    private static void AssertDay2Ready(경영SimulationSessionSnapshot value)
    {
        AssertDawnReached(value);
        Assert.True(value.NatureSurvival.Day2Ready);
        Assert.Equal(SimulationNatureSurvivalCodes.Workbench,
            value.NatureSurvival.SelectedExpansionPlanCode);
        Assert.Equal(2, value.NatureSurvival.StoredTimberQuantity);
        Assert.Equal(0, value.NatureSurvival.TimberQuantity);
    }

    internal static async Task<경영SimulationSessionSnapshot> RunLocalLoopAsync(
        LocalSimulationRuntime runtime,
        string sessionStableId,
        long initialRevision,
        bool 장비원장및통나무회수규칙사용 = false)
    {
        var current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-shelter:axe", initialRevision,
                SimulationNatureSurvivalCodes.AcquireAxe,
                SimulationNatureSurvivalCodes.AxePickupStableId));
        if (장비원장및통나무회수규칙사용)
        {
            var equipment = await runtime.ActorEquipment.GetActorEquipmentAsync(
                sessionStableId);
            await runtime.ActorEquipment.ConfirmActorEquipmentChangeAsync(
                sessionStableId, new SimulationActorEquipmentChangeConfirmRequest
                {
                    CommandId = "nature-shelter:equip",
                    ExpectedEquipmentRevision = equipment.EquipmentRevision,
                    ActorStableId = "player:solo",
                    OperationCode = SimulationActorEquipmentCodes.Equip,
                    ItemInstanceStableId =
                        SimulationNatureSurvivalCodes.AxePickupStableId,
                    SlotCode = SimulationActorEquipmentCodes.MainHand,
                });
        }
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-shelter:cancel:start", current.Revision,
                SimulationNatureSurvivalCodes.BeginHarvest,
                "resource:nature-tree:01"));
        current = await runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
            Clock("nature-shelter:cancel:progress", current.Revision, 2));
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-shelter:cancel:confirm", current.Revision,
                SimulationNatureSurvivalCodes.CancelActiveWork,
                "resource:nature-tree:01"));

        for (var index = 1; index <= 3; index++)
        {
            current = await runtime.Nature.ConfirmAsync(sessionStableId,
                Command($"nature-shelter:tree:{index}:start", current.Revision,
                    SimulationNatureSurvivalCodes.BeginHarvest,
                    $"resource:nature-tree:{index:00}"));
            current = await runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
                Clock($"nature-shelter:tree:{index}:complete", current.Revision,
                    NatureSurvivalRules.HarvestWorkSeconds));
            if (장비원장및통나무회수규칙사용)
            {
                var dropped = current.NatureSurvival.DroppedTimber.Single(value =>
                    value.SourceResourceNodeStableId ==
                    $"resource:nature-tree:{index:00}"
                    && value.StateCode ==
                    SimulationNatureSurvivalCodes.DroppedTimberAvailable);
                current = await runtime.Nature.ConfirmAsync(sessionStableId,
                    Command($"nature-shelter:tree:{index}:collect",
                        current.Revision,
                        SimulationNatureSurvivalCodes.CollectDroppedTimber,
                        dropped.DroppedTimberStableId));
            }
        }

        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-shelter:cabin:place", current.Revision,
                SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
                "facility:nature-cabin", localX: 3, localZ: -2, yawDegrees: 90));
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-shelter:cabin:build", current.Revision,
                SimulationNatureSurvivalCodes.BeginCabinBuild,
                "facility:nature-cabin"));
        current = await runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
            Clock("nature-shelter:cabin:complete", current.Revision,
                NatureSurvivalRules.CabinWorkSeconds));
        current = await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-shelter:cabin:enter", current.Revision,
                SimulationNatureSurvivalCodes.EnterCabin,
                "facility:nature-cabin"));
        return await runtime.Nature.ConfirmAsync(sessionStableId,
            Command("nature-shelter:cabin:leave", current.Revision,
                SimulationNatureSurvivalCodes.LeaveCabin,
                "facility:nature-cabin"));
    }

    internal static async Task<경영SimulationSessionSnapshot> RunRemoteLoopAsync(
        HttpClient client,
        string sessionStableId,
        long initialRevision,
        bool 장비원장및통나무회수규칙사용 = false)
    {
        var current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-shelter:axe", initialRevision,
                SimulationNatureSurvivalCodes.AcquireAxe,
                SimulationNatureSurvivalCodes.AxePickupStableId));
        if (장비원장및통나무회수규칙사용)
        {
            var equipment = await client.GetFromJsonAsync<
                SimulationActorEquipmentStateSnapshot>(
                $"/api/simulation/v1/sessions/{sessionStableId}/actor-equipment");
            Assert.NotNull(equipment);
            var equipResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{sessionStableId}/actor-equipment/changes/confirm",
                new SimulationActorEquipmentChangeConfirmRequest
                {
                    CommandId = "nature-shelter:equip",
                    ExpectedEquipmentRevision = equipment!.EquipmentRevision,
                    ActorStableId = "player:solo",
                    OperationCode = SimulationActorEquipmentCodes.Equip,
                    ItemInstanceStableId =
                        SimulationNatureSurvivalCodes.AxePickupStableId,
                    SlotCode = SimulationActorEquipmentCodes.MainHand,
                });
            Assert.Equal(HttpStatusCode.OK, equipResponse.StatusCode);
        }
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-shelter:cancel:start", current.Revision,
                SimulationNatureSurvivalCodes.BeginHarvest,
                "resource:nature-tree:01"));
        current = await RemoteAdvanceAsync(client, sessionStableId,
            Clock("nature-shelter:cancel:progress", current.Revision, 2));
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-shelter:cancel:confirm", current.Revision,
                SimulationNatureSurvivalCodes.CancelActiveWork,
                "resource:nature-tree:01"));

        for (var index = 1; index <= 3; index++)
        {
            current = await RemoteConfirmAsync(client, sessionStableId,
                Command($"nature-shelter:tree:{index}:start", current.Revision,
                    SimulationNatureSurvivalCodes.BeginHarvest,
                    $"resource:nature-tree:{index:00}"));
            current = await RemoteAdvanceAsync(client, sessionStableId,
                Clock($"nature-shelter:tree:{index}:complete", current.Revision,
                    NatureSurvivalRules.HarvestWorkSeconds));
            if (장비원장및통나무회수규칙사용)
            {
                var dropped = current.NatureSurvival.DroppedTimber.Single(value =>
                    value.SourceResourceNodeStableId ==
                    $"resource:nature-tree:{index:00}"
                    && value.StateCode ==
                    SimulationNatureSurvivalCodes.DroppedTimberAvailable);
                current = await RemoteConfirmAsync(client, sessionStableId,
                    Command($"nature-shelter:tree:{index}:collect",
                        current.Revision,
                        SimulationNatureSurvivalCodes.CollectDroppedTimber,
                        dropped.DroppedTimberStableId));
            }
        }

        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-shelter:cabin:place", current.Revision,
                SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
                "facility:nature-cabin", localX: 3, localZ: -2, yawDegrees: 90));
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-shelter:cabin:build", current.Revision,
                SimulationNatureSurvivalCodes.BeginCabinBuild,
                "facility:nature-cabin"));
        current = await RemoteAdvanceAsync(client, sessionStableId,
            Clock("nature-shelter:cabin:complete", current.Revision,
                NatureSurvivalRules.CabinWorkSeconds));
        current = await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-shelter:cabin:enter", current.Revision,
                SimulationNatureSurvivalCodes.EnterCabin,
                "facility:nature-cabin"));
        return await RemoteConfirmAsync(client, sessionStableId,
            Command("nature-shelter:cabin:leave", current.Revision,
                SimulationNatureSurvivalCodes.LeaveCabin,
                "facility:nature-cabin"));
    }

    private static async Task<경영SimulationSessionSnapshot> RemoteConfirmAsync(
        HttpClient client,
        string sessionStableId,
        SimulationNatureSurvivalCommandRequest request)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{sessionStableId}/nature-survival/commands",
            request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>())!;
    }

    private static async Task<경영SimulationSessionSnapshot> RemoteAdvanceAsync(
        HttpClient client,
        string sessionStableId,
        SimulationNatureSurvivalClockAdvanceRequest request)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{sessionStableId}/nature-survival/clock/advance",
            request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>())!;
    }

    private static SimulationNatureSurvivalCommandRequest Command(
        string commandId,
        long expectedRevision,
        string actionCode,
        string targetStableId,
        string choiceCode = "",
        double localX = 0,
        double localZ = 0,
        double yawDegrees = 0)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = expectedRevision,
            PlayerStableId = "player:solo",
            ActionCode = actionCode,
            TargetStableId = targetStableId,
            ChoiceCode = choiceCode,
            LocalX = localX,
            LocalZ = localZ,
            YawDegrees = yawDegrees,
        };

    private static SimulationNatureSurvivalClockAdvanceRequest Clock(
        string commandId,
        long expectedRevision,
        int elapsedSeconds)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = expectedRevision,
            ElapsedRealtimeSeconds = elapsedSeconds,
            WorkInputHeld = true,
        };

    internal static void AssertClosedLoop(경영SimulationSessionSnapshot snapshot)
    {
        Assert.True(snapshot.NatureSurvival.HasAxe);
        Assert.Equal(0, snapshot.NatureSurvival.TimberQuantity);
        Assert.Equal(SimulationNatureSurvivalCodes.Completed,
            snapshot.NatureSurvival.Cabin.StateCode);
        Assert.True(snapshot.NatureSurvival.Cabin.RecoveryAvailable);
        Assert.False(snapshot.NatureSurvival.PlayerInsideCabin);
        Assert.Equal(3, snapshot.NatureSurvival.ResourceNodes.Count(value =>
            value.StateCode == SimulationNatureSurvivalCodes.Stump));
    }

    internal static 경영SimulationSession생성Request CreateRequest()
        => new()
        {
            ClientRequestId = Guid.Parse("138887fb-692e-4e41-a2f8-b7b7afc67b65"),
            ScenarioStableId = "scenario:nature-shelter-parity",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 1234,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:solo",
                TerritoryStableId = "territory:nature",
                SettlementStableId = "settlement:nature-home",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 26, 0, 0, 0,
                    TimeSpan.Zero),
            },
            NatureSurvival = new SimulationNatureSurvivalInitialStateRequest
            {
                PlayerStableId = "player:solo",
                StartsWithAxe = false,
                ResourceNodes = Enumerable.Range(1, 6).Select(index =>
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId = $"resource:nature-tree:{index:00}",
                        H2StableId = SimulationNatureSurvivalCodes.HarvestH2StableId,
                        H1StableId = "h1-stock:nature-exploration-buffer",
                        LocalX = -8 + index * 2,
                        LocalZ = 8,
                    }).ToArray(),
            },
        };

    internal static 경영SimulationSession생성Request
        Create장비원장통나무회수생활거점Request()
    {
        var request = SimulationActorEquipmentTests.CreateRequest(
            Guid.Parse("138887fb-692e-4e41-a2f8-b7b7afc67b65"));
        request.ScenarioStableId = "scenario:nature-shelter-parity";
        request.ScenarioDataRevision = "fixture.r2";
        request.ScenarioSeed = 1234;
        request.WorldContext.GameDateStartsOn = new DateTimeOffset(
            2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR5;
        request.NatureSurvival.BuildingProgressionCatalog =
            Simulation영역건물발전Catalog.CreateDefault();
        request.NatureSurvival.ResourceNodes = Enumerable.Range(1, 6)
            .Select(index => new SimulationNatureResourceNodeInitialStateRequest
            {
                ResourceNodeStableId = $"resource:nature-tree:{index:00}",
                H2StableId = SimulationNatureSurvivalCodes.HarvestH2StableId,
                H1StableId = "h1-stock:nature-exploration-buffer",
                LocalX = -8 + index * 2,
                LocalZ = 8,
            }).ToArray();
        return request;
    }

    private static 경영SimulationSession생성Request CreateR2Request()
    {
        var request = CreateRequest();
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR2;
        return request;
    }

    private static 경영SimulationSession생성Request CreateR4Request()
    {
        var request = CreateRequest();
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR4;
        request.NatureSurvival.InventoryCapacityUnits = 64;
        request.NatureSurvival.BuildingProgressionCatalog =
            Simulation영역건물발전Catalog.CreateDefault();
        var sessionId = "simulation-session:"
            + request.ClientRequestId.ToString("N");
        request.ScenarioSeed = Enumerable.Range(1, 10_000).First(seed =>
            NatureSurvivalRules.RollFirstDuskEncounter(seed, sessionId, 0, 6));
        request.ScenarioDataRevision = "fixture.workbench.r4";
        return request;
    }
}
