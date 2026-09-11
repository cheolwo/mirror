using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationWorldStreamingTests
{
    [Fact]
    public void 대관령FarmRecipe는_자료조사기반_3x3상세_5x5활성_9x9준비규칙을_제공한다()
    {
        var first = new SimulationWorldStreamingService();
        var second = new SimulationWorldStreamingService();

        Assert.True(first.TryGetRecipe(SimulationWorldStreamCodes.PyeongchangFarmRecipe, out var a));
        Assert.True(second.TryGetRecipe(SimulationWorldStreamCodes.PyeongchangFarmRecipe, out var b));
        Assert.Equal(121, a.CoverageTileKeys.Length);
        Assert.Equal(1, a.DetailRadius);
        Assert.Equal(2, a.ActiveRadius);
        Assert.Equal(4, a.PrefetchRadius);
        Assert.Equal(4, a.MaxConcurrentTileLoads);
        Assert.Equal(0.25d, a.BoundaryPrefetchFraction);
        Assert.Equal("region-presentation-summary.v1", a.RegionSummaryProfileRevision);
        Assert.Equal(64, a.RegionSummaryProfileHashSha256.Length);
        Assert.Equal(new[] { "L0", "L1", "L2" }, a.SupportedSummaryLodCodes);
        Assert.Equal(64, a.RecipeHashSha256.Length);
        Assert.Equal(a.RecipeHashSha256, b.RecipeHashSha256);
        Assert.False(a.IsOperationalState);
    }

    [Fact]
    public void 공간산출물이없으면_주소나해시를꾸며내지않고_자료대기로남긴다()
    {
        var service = new SimulationWorldStreamingService();
        var key = SimulationWorldStreamingService.TileKey(
            SimulationWorldStreamingService.CenterX,
            SimulationWorldStreamingService.CenterY);

        Assert.True(service.TryGetManifest(key, out var manifest));
        Assert.All(manifest.Layers, layer =>
        {
            Assert.Equal(SimulationWorldStreamCodes.WaitingForSpatialArtifact, layer.StatusCode);
            Assert.Null(layer.ArtifactRelativePath);
            Assert.Null(layer.ArtifactHashSha256);
        });
        Assert.Equal(60, manifest.HaloMeters);
        Assert.Equal(SimulationWorldStreamCodes.RegionSummaryWaitingForDerivedData,
            manifest.RegionSummaryStatusCode);
        Assert.Null(manifest.RegionSummaryHashSha256);
    }

    [Fact]
    public void 파생Db산출물이있으면_원본계보와본문경로를_Manifest에투영한다()
    {
        var service = new SimulationWorldStreamingService(new FixtureArtifactReader());
        var key = SimulationWorldStreamingService.TileKey(700, 1145);

        Assert.True(service.TryGetManifest(key, out var manifest));
        var elevation = Assert.Single(manifest.Layers,
            item => item.LayerCode == SimulationWorldStreamCodes.ElevationLayer);
        Assert.Equal(SimulationWorldStreamCodes.Available, elevation.StatusCode);
        Assert.Equal("Copernicus-DEM-GLO30-N37E128", elevation.SourceRevision);
        Assert.Equal("EPSG:5186", elevation.HorizontalCrsCode);
        Assert.Equal("Unverified", elevation.VerticalDatumCode);
        Assert.Equal("height-f32-v1", elevation.ArtifactFormatCode);
        Assert.Equal(63, elevation.SampleWidth);
        Assert.Equal(63, elevation.SampleHeight);
        Assert.EndsWith("/artifacts/elevation/content", elevation.ArtifactContentPath);
        Assert.False(elevation.PresentationOnly);

        Assert.True(service.TryGetArtifact(key, "elevation", out var descriptor));
        Assert.Equal(elevation.ArtifactHashSha256, descriptor.ArtifactHashSha256);
        Assert.Equal("검증된 공간 산출물 사용 가능", descriptor.KoreanStatusLabel);
    }

    [Fact]
    public void 범위밖타일은_계약에포함하지않는다()
    {
        var service = new SimulationWorldStreamingService();
        Assert.False(service.TryGetManifest("kr5186:l2:900:900", out _));
        Assert.False(service.TryGetActivities("kr5186:l2:900:900", out _));
        Assert.False(service.TryGetObjects("kr5186:l2:900:900", out _));
    }

    [Fact]
    public void Scenario건물은_결정적배치와_비권위근거를가진다()
    {
        var first = new SimulationWorldStreamingService();
        var second = new SimulationWorldStreamingService();
        var key = SimulationWorldStreamingService.TileKey(700, 1145);

        Assert.True(first.TryGetObjects(key, out var a));
        Assert.True(second.TryGetObjects(key, out var b));
        Assert.Equal(2, a.Objects.Length);
        Assert.Equal(a.PlacementHashSha256, b.PlacementHashSha256);
        Assert.All(a.Objects, item =>
        {
            Assert.Equal(SimulationWorldStreamCodes.Scenario, item.EvidenceKindCode);
            Assert.True(item.PresentationOnly);
            Assert.False(item.CollisionEligible);
            Assert.StartsWith("legal.", item.VisualKey);
        });
        Assert.True(a.PresentationOnly);
        Assert.False(a.IsOperationalState);
    }

    [Fact]
    public async Task 읽기API는_RecipeManifestArtifactActivity를_로그인없이제공한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var recipeId = Uri.EscapeDataString(SimulationWorldStreamCodes.PyeongchangFarmRecipe);
        var tileKey = Uri.EscapeDataString(SimulationWorldStreamingService.TileKey(700, 1145));

        var recipe = await client.GetFromJsonAsync<SimulationWorldStreamRecipeResponse>(
            "/api/simulation/v1/world-stream/recipes/" + recipeId);
        var manifest = await client.GetFromJsonAsync<SimulationWorldTileStreamManifestResponse>(
            "/api/simulation/v1/world-stream/tiles/" + tileKey + "/manifest");
        var artifact = await client.GetFromJsonAsync<SimulationWorldTileArtifactDescriptorResponse>(
            "/api/simulation/v1/world-stream/tiles/" + tileKey + "/artifacts/elevation");
        var activities = await client.GetFromJsonAsync<SimulationWorldTileActivityProjectionResponse>(
            "/api/simulation/v1/world-stream/tiles/" + tileKey + "/activities");
        var objects = await client.GetFromJsonAsync<SimulationWorldTileObjectProjectionResponse>(
            "/api/simulation/v1/world-stream/tiles/" + tileKey + "/objects");

        Assert.NotNull(recipe);
        Assert.NotNull(manifest);
        Assert.NotNull(artifact);
        Assert.NotNull(activities);
        Assert.NotNull(objects);
        Assert.Equal(SimulationWorldStreamCodes.WaitingForSpatialArtifact, artifact.StatusCode);
        Assert.True(activities.PresentationOnly);
        Assert.Equal(0, activities.WorldTick);
        Assert.Equal(2, objects.Objects.Length);
        Assert.True(objects.PresentationOnly);

        using var missing = await client.GetAsync(
            "/api/simulation/v1/world-stream/tiles/"
            + Uri.EscapeDataString("kr5186:l2:900:900") + "/manifest");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var regionWhileDatabaseDisabled = await client.GetAsync(
            "/api/simulation/v1/world-stream/regions/"
            + Uri.EscapeDataString("region:kr:administrative:5176038000"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, regionWhileDatabaseDisabled.StatusCode);

        using var summaryWhileDatabaseDisabled = await client.GetAsync(
            "/api/simulation/v1/world-stream/regions/"
            + Uri.EscapeDataString("region:kr:bjd:5176038000") + "/summary?lod=L1");
        Assert.Equal(HttpStatusCode.NotFound, summaryWhileDatabaseDisabled.StatusCode);

        using var detailWhileDatabaseDisabled = await client.GetAsync(
            "/api/simulation/v1/world-stream/objects/"
            + Uri.EscapeDataString("business:public-license:test") + "/public-detail");
        Assert.Equal(HttpStatusCode.NotFound, detailWhileDatabaseDisabled.StatusCode);
    }

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

    private sealed class FixtureArtifactReader : ISimulationWorldTileArtifactReader
    {
        public bool TryRead(
            string tileKey,
            string layerCode,
            out SimulationWorldTileArtifactSnapshot value)
        {
            value = new SimulationWorldTileArtifactSnapshot();
            if (tileKey != SimulationWorldStreamingService.TileKey(700, 1145)
                || layerCode != SimulationWorldStreamCodes.ElevationLayer)
                return false;
            value = new SimulationWorldTileArtifactSnapshot
            {
                TileKey = tileKey,
                LayerCode = layerCode,
                SourceRevision = "Copernicus-DEM-GLO30-N37E128",
                ArtifactHashSha256 = new string('a', 64),
                SourceHashSha256 = new string('b', 64),
                HorizontalCrsCode = "EPSG:5186",
                VerticalDatumCode = "Unverified",
                ResolutionMeters = 30m,
                NoDataValue = "-32767",
                ArtifactFormatCode = "height-f32-v1",
                ArtifactRelativePath = "generated/tiles/kr5186_l2_700_1145/elevation.bin",
                ArtifactByteLength = 15876,
                SampleWidth = 63,
                SampleHeight = 63,
            };
            return true;
        }
    }
}
