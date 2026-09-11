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
    "Nature 전술 도끼 획득의 반복 결정성·Save/Replay·Local/Remote 행위 계보를 검증한다.",
    Boundary = "자동 안정성 시험은 canonical Scene 실제 입력 2회와 Game View 증거를 대신하지 않는다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
public sealed class SimulationNatureTacticalStabilityTests
{
    private const string CommandId = "command:nature-tactical:e8:acquire-axe";
    private const string SaveStableId = "save:nature-tactical:e8";

    [Theory]
    [InlineData(SimulationNatureSurvivalCodes.AcquireAxe,
        SimulationNatureSurvivalCodes.TacticalSelfNavigationPlayableLoopStableId)]
    [InlineData(SimulationNatureSurvivalCodes.BeginHarvest,
        SimulationNatureSurvivalCodes.ShelterFoundationPlayableLoopStableId)]
    [InlineData(SimulationNatureSurvivalCodes.ResolveEncounter,
        SimulationNatureSurvivalCodes.TwilightReturnPlayableLoopStableId)]
    [InlineData(SimulationNatureSurvivalCodes.StoreAtCabin,
        SimulationNatureSurvivalCodes.NightDay2PlayableLoopStableId)]
    [InlineData(SimulationNatureSurvivalCodes.BeginBuildingConstruction,
        SimulationNatureSurvivalCodes.BuildingLearningPlayableLoopStableId)]
    [InlineData(SimulationNatureSurvivalCodes.PrepareFieldSupply,
        SimulationNatureSurvivalCodes.FieldSupplyReturnPlayableLoopStableId)]
    public void Nature행동은_권위와표현이공유하는_폐루프주제로정규화된다(
        string actionCode, string expectedLoopStableId)
        => Assert.Equal(expectedLoopStableId,
            SimulationNatureSurvivalCodes.PlayableLoopStableIdForAction(
                actionCode));

    [Fact]
    public async Task 도끼획득은_세번의Local실행과_RemoteHost에서_같은계보hash를만든다()
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
            "ssalddel-nature-tactical-e8-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new InMemory경영SimulationSessionStore();
            var traceSink = new InMemorySimulationPlayableLoopEngineTraceSink();
            using var runtime = new LocalSimulationRuntime(store,
                new InMemorySimulationSessionSaveStore(),
                new FileSimulationLocalSaveSlotStore(savesRoot),
                playableLoopEngineTraceSink: traceSink);
            var created = await runtime.Sessions.CreateAsync(
                CreateTacticalRequest());
            var completed = await runtime.Nature.ConfirmAsync(
                created.SessionStableId, Command(created.Revision));
            var aggregate = store.Find(created.SessionStableId)!;
            var package = aggregate.CreateSavePackage(new()
            {
                SaveStableId = SaveStableId,
                ExpectedRevision = completed.Revision,
            });
            var restored = SimulationSessionReplay.Restore(package);
            var replayed = restored.CreateSavePackage(new()
            {
                SaveStableId = SaveStableId,
                ExpectedRevision = restored.Revision,
            });
            Assert.Equal(package.ReplayHash, replayed.ReplayHash);
            return Snapshot(package, traceSink.Snapshot(
                SimulationNatureSurvivalCodes
                    .TacticalSelfNavigationPlayableLoopStableId,
                SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
                CommandId));
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
            CreateTacticalRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>();
        Assert.NotNull(created);

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created!.SessionStableId}/nature-survival/commands",
            Command(created.Revision));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var completed = await confirmResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>();
        Assert.NotNull(completed);

        var saveResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created.SessionStableId}/saves",
            new SimulationSessionSaveRequest
            {
                SaveStableId = SaveStableId,
                ExpectedRevision = completed!.Revision,
            });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var package = await saveResponse.Content.ReadFromJsonAsync<
            SimulationSessionSavePackage>();
        Assert.NotNull(package);
        return Snapshot(package!, traceSink.Snapshot(
            SimulationNatureSurvivalCodes
                .TacticalSelfNavigationPlayableLoopStableId,
            SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
            CommandId));
    }

    private static SimulationNatureSurvivalCommandRequest Command(long revision)
        => new()
        {
            CommandId = CommandId,
            ExpectedRevision = revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        };

    private static 경영SimulationSession생성Request CreateTacticalRequest()
    {
        var request = SimulationActorEquipmentTests.CreateRequest();
        request.NatureSurvival!.ProfileRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR5;
        request.NatureSurvival.BuildingProgressionCatalog =
            Simulation영역건물발전Catalog.CreateDefault();
        return request;
    }

    private static StabilitySnapshot Snapshot(
        SimulationSessionSavePackage package,
        SimulationPlayableLoopEngineTraceEntry[] trace)
    {
        Assert.Equal(SimulationSaveSchemaVersions.V28, package.SchemaVersion);
        var ledger = Assert.IsType<Simulation행위기록LedgerSnapshot>(
            package.ActionManifestationLedger);
        var record = Assert.Single(ledger.TailRecords);
        Assert.Equal(SimulationNatureSurvivalCodes
                .TacticalSelfNavigationPlayableLoopStableId,
            record.PlayableLoopStableId);
        Assert.Equal(SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
            record.WorldInteractionId);
        Assert.Equal(CommandId, record.CommandId);
        var profile = Assert.IsType<Simulation플레이어분야ProfileSnapshot>(
            package.PlayerDomainProfile);
        Assert.False(string.IsNullOrWhiteSpace(profile.StateHashSha256));
        AssertTrace(trace);
        return new StabilitySnapshot(package.ReplayHash,
            package.ActorEquipment!.StateHashSha256,
            ledger.StateHashSha256, profile.StateHashSha256,
            record.기록HashSha256, TraceDigest(trace));
    }

    private static void AssertTrace(
        SimulationPlayableLoopEngineTraceEntry[] trace)
    {
        Assert.NotEmpty(trace);
        Assert.All(trace, value =>
        {
            Assert.Equal(SimulationNatureSurvivalCodes
                    .TacticalSelfNavigationPlayableLoopStableId,
                value.PlayableLoopStableId);
            Assert.Equal(SimulationNatureSurvivalCodes
                    .AcquireAxeWorldInteractionId,
                value.WorldInteractionId);
        });
        var phases = trace.OrderBy(value => value.Sequence)
            .Select(value => value.PhaseCode).ToArray();
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
        }, phases);
        Assert.DoesNotContain(trace, value => value.StatusCode ==
            SimulationEngineInteractionStatusCodes.Blocked);
    }

    private static string TraceDigest(
        IEnumerable<SimulationPlayableLoopEngineTraceEntry> trace)
    {
        var canonical = string.Join("\n", trace.OrderBy(value => value.Sequence)
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
        string PlayerProgressionHash, string ActionRecordHash,
        string PipelineTraceDigest);
}
