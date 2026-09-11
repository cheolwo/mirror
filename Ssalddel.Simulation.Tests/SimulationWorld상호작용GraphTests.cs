using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Persistence;
using Xunit;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationWorld상호작용GraphTests
{
    internal static async Task<Simulation공간세계InitialStateRequest>
        CreateP1GraphSpatialWorldAsync()
    {
        await using var db = CreateDb();
        var store = new SimulationWorldAreaSetGraphStore(db);
        await CreateJob(store).BuildAsync();
        return await CreateService(store).ResolveSpatialWorldAsync(
            PyeongchangAreaSetStableIds.AreaSet,
            new[] { "WI-FARM-01", "WI-FARM-02", "WI-FARM-03" });
    }

    [Fact]
    public async Task 현재FarmGraph를감사하고_생산흐름일부만공간폐루프로승격한다()
    {
        await using var db = CreateDb();
        var store = new SimulationWorldAreaSetGraphStore(db);
        await CreateJob(store).BuildAsync();
        var service = CreateService(store);

        var readiness = await service.EvaluateAsync(PyeongchangAreaSetStableIds.AreaSet);

        var audit = Assert.Single(readiness.GraphAudits);
        Assert.Equal(5, audit.NodeCount);
        Assert.Equal(3, audit.EdgeCount);
        Assert.Equal(0, audit.ExternalConnectorCount);
        Assert.Equal(3, audit.UnresolvedCount);
        Assert.Equal(SimulationWorld상호작용Graph상태Codes.Partial,
            readiness.OverallStatusCode);
        Assert.All(new[] { "WI-FARM-01", "WI-FARM-02", "WI-FARM-03" }, wiId =>
        {
            var binding = readiness.Bindings.Single(item => item.WorldInteractionId == wiId);
            Assert.True(binding.SpatialClosedLoop);
            Assert.Equal("farm:700-1145", binding.MatchedLandscapeNodeStableId);
            Assert.Equal(Simulation공간근거종류Codes.LandscapeGraph,
                binding.SpatialDefinition!.EvidenceKindCode);
        });
        Assert.False(readiness.Bindings.Single(item =>
            item.WorldInteractionId == "WI-FARM-04").SpatialClosedLoop);
        Assert.Equal(SimulationWorld상호작용Graph상태Codes.WaitingForNode,
            readiness.Bindings.Single(item =>
                item.WorldInteractionId == "WI-FARM-05").StatusCode);
    }

    [Fact]
    public async Task 폐루프가닫힌WI만Graph근거공간정의로해결하고_Scenario로대체하지않는다()
    {
        await using var db = CreateDb();
        var store = new SimulationWorldAreaSetGraphStore(db);
        await CreateJob(store).BuildAsync();
        var service = CreateService(store);

        var world = await service.ResolveSpatialWorldAsync(
            PyeongchangAreaSetStableIds.AreaSet,
            new[] { "WI-FARM-01", "WI-FARM-02", "WI-FARM-03" });

        var definition = Assert.Single(world.Definitions);
        Assert.Equal("farm:700-1145", definition.LandscapeNodeStableId);
        Assert.Equal(Simulation공간근거종류Codes.LandscapeGraph, definition.EvidenceKindCode);
        Assert.Contains(Simulation공간능력Codes.TillingWorkArea, definition.CapabilityCodes);
        Assert.Contains(Simulation공간능력Codes.SowingWorkArea, definition.CapabilityCodes);
        Assert.Contains(Simulation공간능력Codes.CropCareWorkArea, definition.CapabilityCodes);
        Assert.Equal(1m, Assert.Single(definition.BaseCapacities).Quantity);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveSpatialWorldAsync(PyeongchangAreaSetStableIds.AreaSet,
                new[] { "WI-FARM-04" }));
        Assert.Equal("SimulationSpatialClosedLoopUnavailable:WI-FARM-04", error.Message);
    }

    [Fact]
    public async Task 승인한GraphHash가다르면_공간정의를만들지않는다()
    {
        await using var db = CreateDb();
        var store = new SimulationWorldAreaSetGraphStore(db);
        await CreateJob(store).BuildAsync();
        var catalog = await new FileSimulationWorld상호작용GraphCatalogReader(CatalogPath())
            .ReadAsync();
        catalog.Bindings.Single(item => item.WorldInteractionId == "WI-FARM-01")
            .RequiredGraphHashSha256 = new string('0', 64);
        var service = new SimulationWorld상호작용GraphService(store,
            new FixedCatalogReader(catalog));

        var readiness = await service.EvaluateAsync(PyeongchangAreaSetStableIds.AreaSet);

        var binding = readiness.Bindings.Single(item =>
            item.WorldInteractionId == "WI-FARM-01");
        Assert.Equal(SimulationWorld상호작용Graph상태Codes.GraphRevisionMismatch,
            binding.StatusCode);
        Assert.Null(binding.SpatialDefinition);
    }

    [Fact]
    public async Task 준비도실행결과를_파생DB에저장하고같은계보로재조회한다()
    {
        await using var db = CreateDb();
        var graphStore = new SimulationWorldAreaSetGraphStore(db);
        await CreateJob(graphStore).BuildAsync();
        var readinessStore = new SimulationWorld상호작용GraphReadinessStore(db);
        var job = new SimulationWorld상호작용GraphJobShell(
            CreateService(graphStore), readinessStore);

        var built = await job.BuildAsync(PyeongchangAreaSetStableIds.AreaSet);
        var stored = await readinessStore.ReadLatestAsync(PyeongchangAreaSetStableIds.AreaSet);

        Assert.NotNull(stored);
        Assert.Equal(built.BindingCatalogHashSha256, stored!.BindingCatalogHashSha256);
        Assert.Equal(built.GraphAudits.Single().GraphHashSha256,
            stored.GraphAudits.Single().GraphHashSha256);
        Assert.Equal(3, stored.Bindings.Count(item => item.SpatialClosedLoop));
        Assert.Single(await db.WorldInteractionGraphReadiness.ToArrayAsync());
    }

    [Fact]
    public async Task 기존WorldStream경계에서_WI공간Graph준비도를조회한다()
    {
        var store = new CapturingGraphStore();
        await CreateJob(store).BuildAsync();
        using var factory = new SimulationWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ISimulationWorldAreaSetGraphStore>(store);
                services.AddSingleton<ISimulationWorld상호작용GraphCatalogReader>(
                    new FileSimulationWorld상호작용GraphCatalogReader(CatalogPath()));
            }));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/simulation/v1/world-stream/area-sets/"
            + Uri.EscapeDataString(PyeongchangAreaSetStableIds.AreaSet)
            + "/interaction-graph-readiness");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<
            SimulationWorld상호작용Graph준비도Response>();

        Assert.NotNull(body);
        Assert.Equal(3, body!.Bindings.Count(item => item.SpatialClosedLoop));
        Assert.Equal(0, Assert.Single(body.GraphAudits).ExternalConnectorCount);
    }

    private static SimulationWorld상호작용GraphService CreateService(
        ISimulationWorldAreaSetGraphStore store) => new(store,
        new FileSimulationWorld상호작용GraphCatalogReader(CatalogPath()));

    private static SimulationWorldAreaSetLandscapeGraphJobShell CreateJob(
        ISimulationWorldAreaSetGraphStore store) => new(
        new FileSimulationWorldAreaSetDefinitionReader(AreaSetPath()),
        new SimulationWorldLandscapeGrammarManifestReader(GrammarPath()),
        new PyeongchangFirstLandscapeSkeletonSource(new CentralTileArtifactReader()),
        new SimulationWorldLandscapeGraphAssembler(),
        store);

    private static SimulationWorld파생DbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SimulationWorld파생DbContext>()
            .UseInMemoryDatabase("wi-graph-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SimulationWorld파생DbContext(options);
    }

    private static string AreaSetPath() => FindRepoFile("eng", "world-seedbeds", "area-sets",
        "pyeongchang-farm-hub-town.v1", "area-set.json");
    private static string CatalogPath() => FindRepoFile("eng", "world-seedbeds", "area-sets",
        "pyeongchang-farm-hub-town.v1", "spatial-capabilities.v1.json");
    private static string GrammarPath() => FindRepoFile("eng", "world-seedbeds", "manifests",
        "pyeongchang-landscape-grammar.v1.json");

    private static string FindRepoFile(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException(string.Join("/", parts));
    }

    private sealed class CentralTileArtifactReader : ISimulationWorldTileArtifactReader
    {
        public bool TryRead(
            string tileKey,
            string layerCode,
            out SimulationWorldTileArtifactSnapshot value)
        {
            value = new SimulationWorldTileArtifactSnapshot
            {
                TileKey = tileKey,
                LayerCode = layerCode,
            };
            return tileKey == "kr5186:l2:700:1145";
        }
    }

    private sealed class FixedCatalogReader(SimulationWorld상호작용GraphBindingCatalog catalog) :
        ISimulationWorld상호작용GraphCatalogReader
    {
        public Task<SimulationWorld상호작용GraphBindingCatalog> ReadAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(catalog);
    }

    private sealed class CapturingGraphStore : ISimulationWorldAreaSetGraphStore
    {
        private SimulationWorldAreaSetDefinitionResponse? areaSet;
        private SimulationWorldLandscapeGraphResponse[] graphs =
            Array.Empty<SimulationWorldLandscapeGraphResponse>();

        public Task ReplaceAreaSetBuildAsync(
            SimulationWorldAreaSetDefinitionResponse value,
            System.Collections.Generic.IReadOnlyList<SimulationWorldLandscapeGraphResponse> items,
            CancellationToken cancellationToken = default)
        {
            areaSet = value;
            graphs = items.ToArray();
            return Task.CompletedTask;
        }

        public Task<SimulationWorldAreaSetDefinitionResponse?> ReadAreaSetAsync(
            string areaSetStableId,
            CancellationToken cancellationToken = default) => Task.FromResult(areaSet);

        public Task<SimulationWorldLandscapeGraphResponse?> ReadGraphAsync(
            string landscapeGraphStableId,
            CancellationToken cancellationToken = default) => Task.FromResult(
                graphs.SingleOrDefault(item =>
                    item.LandscapeGraphStableId == landscapeGraphStableId));

        public Task<SimulationWorldLandscapeCompositionTileResponse?> ReadTileFacadeAsync(
            string tileKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SimulationWorldLandscapeCompositionTileResponse?>(null);
    }
}
