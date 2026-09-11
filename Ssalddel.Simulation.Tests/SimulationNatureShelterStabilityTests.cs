using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Nature 도끼·벌목·오두막 기초의 반복 결정성·Save/Replay·Local/Remote 행위 계보를 검증한다.",
    Boundary = "자동 안정성 시험은 canonical Scene 실제 입력과 Game View 증거를 대신하지 않는다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
public sealed class SimulationNatureShelterStabilityTests
{
    private const string SaveStableId = "save:nature-shelter:e8";

    [Fact]
    public async Task 오두막기초는_세번의Local실행과_RemoteHost에서_같은계보hash를만든다()
    {
        var localRuns = new List<StabilitySnapshot>();
        for (var index = 0; index < 3; index++)
            localRuns.Add(await RunLocalAsync());

        Assert.All(localRuns.Skip(1), value => Assert.Equal(localRuns[0], value));
        var remote = await RunRemoteAsync();
        Assert.Equal(localRuns[0], remote);
    }

    private static async Task<StabilitySnapshot> RunLocalAsync()
    {
        var savesRoot = Path.Combine(Path.GetTempPath(),
            "ssalddel-nature-shelter-e8-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new InMemory경영SimulationSessionStore();
            var traceSink = new InMemorySimulationPlayableLoopEngineTraceSink();
            using var runtime = new LocalSimulationRuntime(store,
                new InMemorySimulationSessionSaveStore(),
                new FileSimulationLocalSaveSlotStore(savesRoot),
                playableLoopEngineTraceSink: traceSink);
            var created = await runtime.Sessions.CreateAsync(
                SimulationNature생활거점동등성Tests
                    .Create장비원장통나무회수생활거점Request());
            var completed = await SimulationNature생활거점동등성Tests
                .RunLocalLoopAsync(runtime, created.SessionStableId,
                    created.Revision, 장비원장및통나무회수규칙사용: true);
            SimulationNature생활거점동등성Tests.AssertClosedLoop(completed);
            var package = store.Find(created.SessionStableId)!.CreateSavePackage(
                new SimulationSessionSaveRequest
                {
                    SaveStableId = SaveStableId,
                    ExpectedRevision = completed.Revision,
                });
            var restored = SimulationSessionReplay.Restore(package);
            var replayed = restored.CreateSavePackage(new SimulationSessionSaveRequest
            {
                SaveStableId = SaveStableId,
                ExpectedRevision = restored.Revision,
            });
            Assert.Equal(package.ReplayHash, replayed.ReplayHash);
            return Snapshot(package, traceSink.SnapshotForLoop(
                SimulationNatureSurvivalCodes.ShelterFoundationPlayableLoopStableId));
        }
        finally
        {
            if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
        }
    }

    private static async Task<StabilitySnapshot> RunRemoteAsync()
    {
        var traceSink = new InMemorySimulationPlayableLoopEngineTraceSink();
        using var factory = new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<I세계상호작용실행Pipeline>();
                services.AddSingleton<I세계상호작용실행Pipeline>(
                    new 세계상호작용실행Pipeline(traceSink));
            }));
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            SimulationNature생활거점동등성Tests
                .Create장비원장통나무회수생활거점Request());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>();
        Assert.NotNull(created);
        var completed = await SimulationNature생활거점동등성Tests
            .RunRemoteLoopAsync(client, created!.SessionStableId,
                created.Revision, 장비원장및통나무회수규칙사용: true);
        SimulationNature생활거점동등성Tests.AssertClosedLoop(completed);

        var saveResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created.SessionStableId}/saves",
            new SimulationSessionSaveRequest
            {
                SaveStableId = SaveStableId,
                ExpectedRevision = completed.Revision,
            });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var package = await saveResponse.Content.ReadFromJsonAsync<
            SimulationSessionSavePackage>();
        Assert.NotNull(package);
        return Snapshot(package!, traceSink.SnapshotForLoop(
            SimulationNatureSurvivalCodes.ShelterFoundationPlayableLoopStableId));
    }

    private static StabilitySnapshot Snapshot(
        SimulationSessionSavePackage package,
        SimulationPlayableLoopEngineTraceEntry[] trace)
    {
        Assert.Equal(SimulationSaveSchemaVersions.V28, package.SchemaVersion);
        var ledger = Assert.IsType<Simulation행위기록LedgerSnapshot>(
            package.ActionManifestationLedger);
        Assert.NotEmpty(ledger.TailRecords);
        Assert.Contains(ledger.TailRecords, value =>
            value.WorldInteractionId ==
            SimulationNatureSurvivalCodes.BeginHarvestWorldInteractionId);
        var profile = Assert.IsType<Simulation플레이어분야ProfileSnapshot>(
            package.PlayerDomainProfile);
        Assert.False(string.IsNullOrWhiteSpace(profile.StateHashSha256));
        Assert.NotEmpty(trace);
        Assert.DoesNotContain(trace, value => value.StatusCode ==
            SimulationEngineInteractionStatusCodes.Blocked);
        Assert.Contains(trace, value => value.PhaseCode ==
            SimulationEngineInteractionPhaseCodes.ActionRecordAppend);
        Assert.Contains(trace, value => value.PhaseCode ==
            SimulationEngineInteractionPhaseCodes.PlayerProgressionApply);
        return new StabilitySnapshot(package.ReplayHash,
            package.ActorEquipment!.StateHashSha256,
            ledger.StateHashSha256, profile.StateHashSha256,
            TraceDigest(trace));
    }

    private static string TraceDigest(
        IEnumerable<SimulationPlayableLoopEngineTraceEntry> trace)
    {
        var canonical = string.Join("\n", trace
            .OrderBy(value => value.BeforeAuthorityRevision)
            .ThenBy(value => value.CommandId, StringComparer.Ordinal)
            .ThenBy(value => value.Sequence)
            .Select(value => string.Join("|",
                value.PlayableLoopStableId, value.WorldInteractionId,
                value.CommandId, value.ComponentCode, value.ComponentKindCode,
                value.ComponentRevision, value.PhaseCode, value.Sequence,
                value.InputHashSha256, value.OutputHashSha256,
                value.StatusCode, value.BeforeAuthorityRevision,
                value.AfterAuthorityRevision, value.ReasonCode)));
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record StabilitySnapshot(string ReplayHash,
        string EquipmentHash, string ActionJournalHash,
        string PlayerProgressionHash, string PipelineTraceDigest);
}
