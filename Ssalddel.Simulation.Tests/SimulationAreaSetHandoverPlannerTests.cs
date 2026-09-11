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
public sealed class SimulationAreaSetHandoverPlannerTests
{
    [Fact]
    public void Farm에서_북쪽이동은_Hub회랑의_H3준비를_우선한다()
    {
        var planner = new SimulationAreaSetHandoverPlanner(Reader());

        var plan = planner.Plan(Request(
            SimulationLhWorldService.L3CellKey(2806, 4580),
            SimulationLhWorldCodes.North,
            SimulationAreaAccessCodes.Locked));

        Assert.Equal(SimulationAreaAccessCodes.FarmAreaSet,
            plan.CurrentAreaSetStableId);
        var candidate = Assert.IsType<SimulationAreaSetHandoverCandidateResponse>(
            plan.Candidates.First());
        Assert.Equal(SimulationAreaAccessCodes.HubAreaSet,
            candidate.TargetAreaSetStableId);
        Assert.Equal(SimulationAreaAccessCodes.FarmToHubConnector,
            candidate.RelationStableId);
        Assert.Equal(SimulationWorldLayoutCodes.TransitionOverlap,
            candidate.OverlapPolicyCode);
        Assert.Equal(SimulationAreaSetHandoverCodes.H3PrepareRequested,
            candidate.PreparationTargetCode);
        Assert.Equal("H3", candidate.SemanticDepthCode);
        Assert.Equal(SimulationAreaSetHandoverCodes.H5Known,
            candidate.ArtifactAvailabilityCode);
        Assert.Equal(SimulationAreaAccessCodes.Locked,
            candidate.SimulationAccessStateCode);
        Assert.Contains("SimulationAreaAccessEvidenceMissing",
            candidate.BlockingReasonCodes);
        Assert.False(candidate.CanActivate);
        Assert.False(plan.ChangesCurrentAreaSet);
        Assert.True(plan.RequiresExplicitTraversalConfirm);
        Assert.Equal(64, plan.PlanHashSha256.Length);
    }

    [Fact]
    public void 회랑근처에서도_패키지검증전에는_통과후보만만들고_활성화하지않는다()
    {
        var planner = new SimulationAreaSetHandoverPlanner(Reader());

        var plan = planner.Plan(Request(
            SimulationLhWorldService.L3CellKey(2805, 4588),
            SimulationLhWorldCodes.South,
            SimulationAreaAccessCodes.Granted));

        var hub = plan.Candidates.Single(value =>
            value.TargetAreaSetStableId == SimulationAreaAccessCodes.HubAreaSet);
        Assert.Equal(SimulationAreaSetHandoverCodes.H1TraversalPreparationRequested,
            hub.PreparationTargetCode);
        Assert.Equal("H1", hub.SemanticDepthCode);
        Assert.Contains(SimulationLhWorldCodes.Collision,
            hub.RequiredCapabilityCodes);
        Assert.Contains(SimulationLhWorldCodes.H1Interaction,
            hub.RequiredCapabilityCodes);
        Assert.DoesNotContain("SimulationAreaAccessEvidenceMissing",
            hub.BlockingReasonCodes);
        Assert.Contains("AreaSetPackageReadinessUnverified",
            hub.BlockingReasonCodes);
        Assert.False(hub.CanRequestTraversal);
        Assert.False(hub.CanActivate);
    }

    [Fact]
    public void 같은입력은_같은후보순서와_hash를_만든다()
    {
        var planner = new SimulationAreaSetHandoverPlanner(Reader());
        var request = Request(
            SimulationLhWorldService.L3CellKey(2806, 4580),
            SimulationLhWorldCodes.East,
            SimulationAreaAccessCodes.Granted);

        var first = planner.Plan(request);
        var second = planner.Plan(request);

        Assert.Equal(first.PlanHashSha256, second.PlanHashSha256);
        Assert.Equal(first.Candidates.Select(value => value.TargetAreaSetStableId),
            second.Candidates.Select(value => value.TargetAreaSetStableId));
        Assert.Equal("area-set:sim:pyeongchang:nature-home.v1",
            first.Candidates.First().TargetAreaSetStableId);
    }

    [Fact]
    public void Town에서_City는_H4메타데이터후보로만보이고_통행과활성화가차단된다()
    {
        var planner = new SimulationAreaSetHandoverPlanner(Reader());
        var plan = planner.Plan(new SimulationAreaSetHandoverPlanRequest
        {
            RequestEpoch = "epoch:handover:reserved-city",
            FocusL3CellKey = SimulationLhWorldService.L3CellKey(
                SimulationLhWorldService.CenterL3X - 3,
                SimulationLhWorldService.CenterL3Y - 15),
            MovementDirectionCode = SimulationLhWorldCodes.SouthWest,
            CurrentAreaSetStableId = "area-set:sim:pyeongchang:town-market.v1",
            AreaAccess = new SimulationPlayerAreaAccessStateSnapshot
            {
                CurrentAreaSetStableId = "area-set:sim:pyeongchang:town-market.v1",
                AccessEntries = new[]
                {
                    new SimulationPlayerAreaAccessSnapshot
                    {
                        AreaSetStableId = "area-set:sim:pyeongchang:city-service.v1",
                        AccessStateCode = SimulationAreaAccessCodes.Granted,
                    },
                },
            },
        });

        var city = Assert.Single(plan.Candidates, value =>
            value.TargetAreaSetStableId == "area-set:sim:pyeongchang:city-service.v1");
        Assert.Equal(SimulationWorldLayoutCodes.ReservedCorridor,
            city.SpatialRealizationCode);
        Assert.Equal(SimulationAreaSetHandoverCodes.H5Reserved,
            city.ArtifactAvailabilityCode);
        Assert.Equal("H4", city.SemanticDepthCode);
        Assert.Contains(SimulationAreaSetHandoverCodes.AreaSetPlacementReserved,
            city.BlockingReasonCodes);
        Assert.Contains(SimulationAreaSetHandoverCodes.AreaSetCorridorReserved,
            city.BlockingReasonCodes);
        Assert.False(city.CanRequestTraversal);
        Assert.False(city.CanActivate);
        Assert.False(plan.ChangesCurrentAreaSet);
    }

    [Fact]
    public async Task LH_HTTP_Preview는_세션현재AreaSet의_인계후보를함께제공한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateSessionRequest());
        var session = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(session);

        var request = new SimulationLhCellPreviewRequest
        {
            RequestEpoch = "epoch:handover:http",
            SessionStableId = session!.SessionStableId,
            RecipeStableId = SimulationWorldStreamCodes.PyeongchangFarmRecipe,
            AreaSetStableId = PyeongchangAreaSetStableIds.AreaSet,
            FocusL3CellKey = SimulationLhWorldService.L3CellKey(2806, 4580),
            MovementDirectionCode = SimulationLhWorldCodes.North,
            ExpectedWorldRevision = session.WorldContext.WorldRevision,
        };
        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/world-stream/lh/cells/preview", request);
        var preview = await response.Content
            .ReadFromJsonAsync<SimulationLhCellPreviewResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(preview);
        Assert.Equal(SimulationAreaAccessCodes.FarmAreaSet,
            preview!.AreaSetHandover.CurrentAreaSetStableId);
        Assert.NotEmpty(preview.AreaSetHandover.Candidates);
        Assert.Equal(SimulationAreaAccessCodes.HubAreaSet,
            preview.AreaSetHandover.Candidates.First().TargetAreaSetStableId);
        Assert.True(preview.AreaSetHandover.IsCandidateOnly);
        Assert.False(preview.AreaSetHandover.ChangesCurrentAreaSet);
    }

    private static SimulationAreaSetHandoverPlanRequest Request(
        string cellKey, string direction, string hubAccessState) => new()
        {
            RequestEpoch = "epoch:handover:deterministic",
            FocusL3CellKey = cellKey,
            MovementDirectionCode = direction,
            CurrentAreaSetStableId = SimulationAreaAccessCodes.FarmAreaSet,
            AreaAccess = new SimulationPlayerAreaAccessStateSnapshot
            {
                CurrentAreaSetStableId = SimulationAreaAccessCodes.FarmAreaSet,
                AccessEntries = new[]
                {
                    new SimulationPlayerAreaAccessSnapshot
                    {
                        AreaSetStableId = SimulationAreaAccessCodes.FarmAreaSet,
                        AccessStateCode = SimulationAreaAccessCodes.Entered,
                    },
                    new SimulationPlayerAreaAccessSnapshot
                    {
                        AreaSetStableId = SimulationAreaAccessCodes.HubAreaSet,
                        AccessStateCode = hubAccessState,
                    },
                },
            },
        };

    private static 경영SimulationSession생성Request CreateSessionRequest() => new()
    {
        ClientRequestId = Guid.Parse("4c315e9b-26b1-43e7-897f-34d101c82832"),
        ScenarioStableId = "scenario:area-set-handover-test",
        ScenarioDataRevision = "scenario-data:area-set-handover-r1",
        ScenarioSeed = 20260823,
        RuleRevision = "rule:area-set-handover-r1",
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:sim.handover-test",
            TerritoryStableId = "territory:sim.pyeongchang-handover-test",
            SettlementStableId = "settlement:sim.handover-test",
            GameDateStartsOn = new DateTimeOffset(
                2026, 8, 23, 0, 0, 0, TimeSpan.Zero),
        },
    };

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

    private static FileSimulationWorldLayoutCatalogReader Reader() => new(CatalogPath());

    private static string CatalogPath()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current != null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "eng", "world-seedbeds",
                "generated", "h5-world-layout.v1.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("h5-world-layout.v1.json");
    }
}
