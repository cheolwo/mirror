using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationWorldExplorationTests
{
    private const string Session = "simulation-exploration:test-session";
    private const string Tile = PyeongchangWorldExplorationFixtureIds.DaegwallyeongFarmCenterTile;

    [Fact]
    public void L2타일은_아이템을즉시생성하지않고_기존건물과조건규칙관계만제공한다()
    {
        var service = CreateService();
        var package = service.GetBuildingItemRules(Tile);
        var repeated = service.GetBuildingItemRules(Tile);
        var objects = new SimulationWorldStreamingService();
        Assert.True(objects.TryGetObjects(Tile, out var placement));
        var objectIds = placement.Objects.Select(value => value.ObjectStableId).ToHashSet();

        Assert.Equal(3, package.BuildingItemRelations.Length);
        Assert.All(package.BuildingItemRelations, relation =>
        {
            Assert.Contains(relation.AnchorObjectStableId, objectIds);
            Assert.Equal(SimulationWorldExplorationCodes.PendingRuleEvaluation,
                relation.InitialStateCode);
            Assert.Contains(SimulationWorldExplorationCodes.PlayerInsideBuilding,
                relation.RequiredConditionCodes);
            Assert.True(relation.PresentationOnly);
        });
        Assert.Equal(package.RelationHashSha256, repeated.RelationHashSha256);
        Assert.Equal(64, package.RelationHashSha256.Length);
        Assert.False(package.CreatesItemInstances);
        Assert.False(package.ChangesSimulationState);
        Assert.False(package.IsOperationalState);
        Assert.Empty(package.ObservedPlaces);
        Assert.Contains(package.DataGaps, value =>
            value.GapCode == SimulationWorldExplorationCodes.PublicLicensedBusinessDataMissing);
    }

    [Fact]
    public void 같은건물과필수시뮬레이션조건이모두맞을때만_해당아이템후보가된다()
    {
        var service = CreateService();
        var preview = service.PreviewEligibility(Session, Tile,
            new SimulationWorldBuildingItemEligibilityPreviewRequest
            {
                ObservedWorldRevision = 0,
                EnteredBuildingStableId = PyeongchangWorldExplorationFixtureIds.Barn,
                ActiveSimulationConditionCodes = new[]
                {
                    SimulationWorldExplorationCodes.FarmExplorationActive,
                    SimulationWorldExplorationCodes.HarvestContextActive,
                },
            });

        var eligible = Assert.Single(preview.Evaluations, value => value.IsEligible);
        Assert.Equal(PyeongchangWorldExplorationFixtureIds.PotatoSample, eligible.ItemCode);
        Assert.True(preview.HasEligibleCandidate);
        Assert.False(preview.StateChanged);
        Assert.True(preview.SimulationOnly);
    }

    [Fact]
    public void 타일진입만으로는_조건이충족되지않아_아이템후보가없다()
    {
        var preview = CreateService().PreviewEligibility(Session, Tile,
            new SimulationWorldBuildingItemEligibilityPreviewRequest
            {
                ObservedWorldRevision = 0,
                EnteredBuildingStableId = PyeongchangWorldExplorationFixtureIds.Barn,
                ActiveSimulationConditionCodes = Array.Empty<string>(),
            });

        Assert.False(preview.HasEligibleCandidate);
        Assert.Equal(2, preview.Evaluations.Length);
        Assert.All(preview.Evaluations, value =>
        {
            Assert.False(value.IsEligible);
            Assert.DoesNotContain(
                SimulationWorldExplorationCodes.PlayerInsideBuilding,
                value.MissingConditionCodes);
            Assert.Contains(
                SimulationWorldExplorationCodes.FarmExplorationActive,
                value.MissingConditionCodes);
        });
    }

    [Fact]
    public void 타일에없는건물은_규칙후보로판정하지않는다()
    {
        var error = Assert.Throws<SimulationNotFoundException>(() =>
            CreateService().PreviewEligibility(Session, Tile,
                new SimulationWorldBuildingItemEligibilityPreviewRequest
                {
                    ObservedWorldRevision = 0,
                    EnteredBuildingStableId = "scenario-object:not-in-tile",
                    ActiveSimulationConditionCodes = new[]
                    {
                        SimulationWorldExplorationCodes.FarmExplorationActive,
                    },
                }));

        Assert.Equal(SimulationWorldExplorationCodes.EnteredBuildingNotFound, error.ErrorCode);
    }

    [Fact]
    public async Task HTTP는_건물아이템관계조회와_무상태Preview판정까지만제공한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tile = Uri.EscapeDataString(Tile);
        var package = await client.GetFromJsonAsync<SimulationWorldBuildingItemRulePackageResponse>(
            "/api/simulation/v1/world-stream/tiles/" + tile + "/building-item-rules");
        Assert.NotNull(package);
        Assert.False(package.CreatesItemInstances);
        Assert.False(package.ChangesSimulationState);

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/world-stream/sessions/" + Uri.EscapeDataString(Session)
            + "/tiles/" + tile + "/building-item-eligibility-preview",
            new SimulationWorldBuildingItemEligibilityPreviewRequest
            {
                ObservedWorldRevision = 0,
                EnteredBuildingStableId = PyeongchangWorldExplorationFixtureIds.Silo,
                ActiveSimulationConditionCodes = new[]
                {
                    SimulationWorldExplorationCodes.FarmExplorationActive,
                    SimulationWorldExplorationCodes.SupplyInspectionActive,
                },
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content
            .ReadFromJsonAsync<SimulationWorldBuildingItemEligibilityPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(PyeongchangWorldExplorationFixtureIds.BasicWaterSupply,
            Assert.Single(preview.Evaluations, value => value.IsEligible).ItemCode);
        Assert.False(preview.StateChanged);
    }

    private static SimulationWorldExplorationService CreateService()
        => new SimulationWorldExplorationService(new SimulationWorldStreamingService());

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
}
