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
public sealed class SimulationLhWorldTests
{
    [Fact]
    public void LH_Profile은_L실행해상도별_H주조회계층을제공한다()
    {
        var profile = SimulationLhWorldService.CreateDefaultProfile();

        Assert.Equal(SimulationLhWorldCodes.SchemaVersion, profile.SchemaVersion);
        Assert.Equal(64, profile.ProfileHashSha256.Length);
        Assert.Equal("simulation-world-interaction-eh-status.r1",
            profile.SpatialKnowledgeRevision);
        Assert.Equal(
            "wi-spatial-composition-plan:reference-play-01-harvest-shipping.v1",
            profile.SpatialCompositionPlanStableId);
        Assert.Collection(profile.Levels,
            value => AssertLevel(value, "L0", 8000, "H4"),
            value => AssertLevel(value, "L1", 2000, "H3"),
            value => AssertLevel(value, "L2", 500, "H2"),
            value => AssertLevel(value, "L3", 125, "H1"));
        Assert.Equal(1, profile.DetailRadius);
        Assert.Equal(2, profile.ActiveRadius);
        Assert.Equal(4, profile.PrefetchRadius);
        Assert.Equal(6, profile.GenerationLayers.Length);
        Assert.False(profile.IsOperationalState);
    }

    [Fact]
    public void 스트리밍범위계산기는_H주조회계층을바꿔도_같은L3창을계산한다()
    {
        var profile = SimulationLhWorldService.CreateDefaultProfile();
        var original = new SimulationLhWindowPlanner().Plan(
            Request("epoch:window:original", "E"), profile);
        foreach (var level in profile.Levels)
        {
            level.DefaultHLevelCode = "H1";
            level.PrimaryHQueryLevelCode = "H1";
        }
        var changed = new SimulationLhWindowPlanner().Plan(
            Request("epoch:window:changed", "E"), profile);

        Assert.Equal(original.Cells.Select(value => value.CellKey),
            changed.Cells.Select(value => value.CellKey));
        Assert.Equal(original.Cells.Select(value => value.WindowRoleCode),
            changed.Cells.Select(value => value.WindowRoleCode));
        Assert.Equal(original.Cells.Select(value => value.Priority),
            changed.Cells.Select(value => value.Priority));
    }

    [Fact]
    public void 스트리밍범위계산기는_고정3_5_9가아닌_프로필반경을사용한다()
    {
        var profile = AdaptiveWindowProfile(0, 1, 2);

        var plan = new SimulationLhWindowPlanner().Plan(
            AdaptiveWindowRequest("epoch:window:adaptive", "E"), profile);

        Assert.Equal(25, plan.Cells.Length);
        Assert.Single(plan.Cells, value =>
            value.WindowRoleCode == SimulationLhWorldCodes.Detail);
        Assert.Equal(8, plan.Cells.Count(value =>
            value.WindowRoleCode == SimulationLhWorldCodes.Active));
        Assert.Equal(16, plan.Cells.Count(value =>
            value.WindowRoleCode == SimulationLhWorldCodes.Prefetch));
        var east = plan.Cells.Single(value => value.CellX == 2803
            && value.CellY == 4581);
        var west = plan.Cells.Single(value => value.CellX == 2799
            && value.CellY == 4581);
        Assert.True(east.Priority < west.Priority);
    }

    [Fact]
    public void 스트리밍범위계산기는_역전된동적Map반경을거부한다()
    {
        var profile = AdaptiveWindowProfile(2, 1, 4);

        var exception = Assert.Throws<ArgumentException>(() =>
            new SimulationLhWindowPlanner().Plan(
                AdaptiveWindowRequest("epoch:window:invalid", "None"), profile));

        Assert.Contains("SimulationDynamicMapWindowPolicyInvalid", exception.Message);
    }

    [Fact]
    public void 셀내용공급자는_L3범위계산과분리해_교체할수있다()
    {
        var source = new RecordingCellContentSource();
        var service = new SimulationLhWorldService(
            new SimulationLhWindowPlanner(), source);
        var preview = service.Preview(
            Request("epoch:content-source", "None"), 1, 0, 7);

        Assert.Equal(81, source.RequestedCellKeys.Count);
        Assert.Equal(SimulationLhWorldCodes.AuthoritativeWorld,
            preview.ContentSourceCode);
        Assert.All(preview.Cells, value => Assert.Equal(
            SimulationLhWorldCodes.AuthoritativeWorld, value.ContentSourceCode));
    }

    [Fact]
    public void 같은시드의_L3_Cell은_요청순서와계절에관계없이_기본Plan이같다()
    {
        var service = new SimulationLhWorldService();
        var spring = service.Preview(Request("epoch:spring", "None"), 1, 10, 7);
        var winter = service.Preview(Request("epoch:winter", "NE"), 85, 99, 7);

        var springByKey = spring.Cells.ToDictionary(value => value.CellKey);
        var winterByKey = winter.Cells.ToDictionary(value => value.CellKey);
        Assert.Equal(81, spring.Cells.Length);
        Assert.Equal(springByKey.Keys.Order(), winterByKey.Keys.Order());
        foreach (var key in springByKey.Keys)
        {
            Assert.Equal(springByKey[key].BasePlanHashSha256,
                winterByKey[key].BasePlanHashSha256);
            Assert.NotEqual(springByKey[key].PresentationHashSha256,
                winterByKey[key].PresentationHashSha256);
        }
        Assert.Equal(SimulationLhWorldCodes.Spring, spring.Season.SeasonCode);
        Assert.Equal(SimulationLhWorldCodes.Winter, winter.Season.SeasonCode);
    }

    [Fact]
    public void 인접Cell은_같은Connector경계Hash를공유하고_H2후보를권위로승격하지않는다()
    {
        var service = new SimulationLhWorldService();
        var preview = service.Preview(Request("epoch:connector", "E"), 1, 0, 7);
        var center = preview.Cells.Single(value =>
            value.CellKey == SimulationLhWorldService.L3CellKey(
                SimulationLhWorldService.CenterL3X,
                SimulationLhWorldService.CenterL3Y));
        var east = preview.Cells.Single(value =>
            value.CellKey == SimulationLhWorldService.L3CellKey(
                SimulationLhWorldService.CenterL3X + 1,
                SimulationLhWorldService.CenterL3Y));

        Assert.Equal(
            center.Connectors.Single(value => value.SideCode == "E").BoundaryHashSha256,
            east.Connectors.Single(value => value.SideCode == "W").BoundaryHashSha256);
        var h2 = center.HBindings.Single(value => value.HLevelCode == "H2");
        Assert.Equal(SimulationLhWorldCodes.IdeaInventory, h2.StateCode);
        Assert.Contains(center.HBindings, value => value.HLevelCode == "H1"
            && value.SpatialStableId == "h1-stock:farm-production"
            && value.StateCode == SimulationLhWorldCodes.ApprovedReference);
        Assert.DoesNotContain(center.HBindings, value => value.HLevelCode == "H1"
            && value.SpatialStableId == "h1-stock:farm-harvest-staging");
        Assert.Contains(center.HBindings, value => value.HLevelCode == "H1"
            && value.SpatialStableId == "h1-stock:farm-loading-gate"
            && value.WorldInteractionIds.SequenceEqual(new[] { "WI-LOG-01" }));
        Assert.Contains(center.Placements, value =>
            value.CompositionKey == "farm:헛간 작업마당:A"
            && value.H1StableId == "h1-stock:farm-work-yard");
    }

    [Fact]
    public void H4밖의Focus와_stale개정은_명시적으로거부한다()
    {
        var service = new SimulationLhWorldService();
        var outside = Request("epoch:outside", "None");
        outside.FocusL3CellKey = SimulationLhWorldService.L3CellKey(9000, 9000);
        var stale = Request("epoch:stale", "None");
        stale.ExpectedWorldRevision = 6;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Preview(outside, 1, 0, 7));
        Assert.Equal("SimulationExpectedRevisionMismatch",
            Assert.Throws<InvalidOperationException>(() =>
                service.Preview(stale, 1, 0, 7)).Message);
    }

    [Fact]
    public void 사계절은_각28일이고_다음계절을서버가제공한다()
    {
        AssertSeason(1, SimulationLhWorldCodes.Spring, 1,
            SimulationLhWorldCodes.Summer);
        AssertSeason(28, SimulationLhWorldCodes.Spring, 28,
            SimulationLhWorldCodes.Summer);
        AssertSeason(29, SimulationLhWorldCodes.Summer, 1,
            SimulationLhWorldCodes.Autumn);
        AssertSeason(57, SimulationLhWorldCodes.Autumn, 1,
            SimulationLhWorldCodes.Winter);
        AssertSeason(85, SimulationLhWorldCodes.Winter, 1,
            SimulationLhWorldCodes.Spring);
    }

    [Fact]
    public void SaveV3는_기본World대신_시드와Delta만봉인하고Replay한다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateSessionRequest());
        var profile = SimulationLhWorldService.CreateDefaultProfile();
        var state = new SimulationLhWorldStateSnapshot
        {
            WorldSeed = profile.WorldSeed,
            GeneratorVersion = profile.GeneratorVersion,
            AreaSetStableId = profile.AreaSetStableId,
            AreaSetRevision = profile.AreaSetRevision,
            AreaSetBoundaryHashSha256 = profile.AreaSetBoundaryHashSha256,
            LastL3CellKey = SimulationLhWorldService.L3CellKey(
                SimulationLhWorldService.CenterL3X,
                SimulationLhWorldService.CenterL3Y),
            Deltas = new[]
            {
                new SimulationLhWorldDeltaSnapshot
                {
                    GeneratedStableId = "lh-object:harvested-potato-field",
                    DeltaKindCode = SimulationLhWorldCodes.DeltaStateChanged,
                    StateCode = "Harvested",
                    AppliedWorldRevision = 0,
                },
            },
        };
        var saved = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:lh-world:delta-1",
            ExpectedRevision = 0,
            LhWorldState = state,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var replayed = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V7, saved.SchemaVersion);
        Assert.NotNull(saved.LhWorld);
        Assert.Single(saved.LhWorld!.Deltas);
        Assert.Equal(saved.ReplayHash, replayed.ReplayHash);
        Assert.Equal(saved.LhWorld.LastL3CellKey, replayed.LhWorld!.LastL3CellKey);
    }

    [Fact]
    public async Task HTTP_Preview는_서버Day와Revision으로_LH창을제공하고_stale요청을거부한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateSessionRequest());
        var session = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(session);

        var request = Request("epoch:http", "E");
        request.SessionStableId = session!.SessionStableId;
        request.ExpectedWorldRevision = session.WorldContext.WorldRevision;
        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/world-stream/lh/cells/preview", request);
        var preview = await response.Content
            .ReadFromJsonAsync<SimulationLhCellPreviewResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(preview);
        Assert.Equal(session.WorldContext.WorldRevision, preview!.WorldRevision);
        Assert.Equal(81, preview.Cells.Length);
        Assert.True(preview.IsCandidateOnly);
        Assert.True(preview.DoesNotApplyResourceLedgers);
        Assert.All(preview.Cells, cell =>
            Assert.True(cell.WorldAssetAssembly?.IsAvailable));
        var natureOwner = Assert.Single(preview.Cells, cell =>
            cell.CellX == SimulationNatureWorldCellAssemblyEngine
                .DefaultNatureOwnerL3X
            && cell.CellY == SimulationNatureWorldCellAssemblyEngine
                .DefaultNatureOwnerL3Y);
        Assert.NotNull(natureOwner.WorldAssetAssembly);
        Assert.Equal(64, natureOwner.WorldAssetAssembly!
            .AssemblyHashSha256.Length);
        Assert.Equal(natureOwner.CellKey, natureOwner.WorldAssetAssembly
            .ExteriorPlacement.CellStableId);
        Assert.Equal(natureOwner.BasePlanHashSha256,
            natureOwner.WorldAssetAssembly.ExteriorPlacement
                .SourceLhBasePlanHashSha256);

        request.RequestEpoch = "epoch:http:stale";
        request.ExpectedWorldRevision++;
        using var conflict = await client.PostAsJsonAsync(
            "/api/simulation/v1/world-stream/lh/cells/preview", request);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private static SimulationLhCellPreviewRequest Request(string epoch, string direction)
        => new()
        {
            RequestEpoch = epoch,
            SessionStableId = "session:sim:lh-world-test",
            RecipeStableId = SimulationWorldStreamCodes.PyeongchangFarmRecipe,
            AreaSetStableId = PyeongchangAreaSetStableIds.AreaSet,
            FocusL3CellKey = SimulationLhWorldService.L3CellKey(
                SimulationLhWorldService.CenterL3X,
                SimulationLhWorldService.CenterL3Y),
            MovementDirectionCode = direction,
            ExpectedWorldRevision = 7,
        };

    private static SimulationLhWorldProfileResponse AdaptiveWindowProfile(
        int detailRadius, int activeRadius, int prefetchRadius)
        => new()
        {
            DetailRadius = detailRadius,
            ActiveRadius = activeRadius,
            PrefetchRadius = prefetchRadius,
            MaxConcurrentPreparations = 4,
            BoundaryPrefetchFraction = .25d,
        };

    private static SimulationLhCellPreviewRequest AdaptiveWindowRequest(
        string epoch, string direction)
        => new()
        {
            RequestEpoch = epoch,
            FocusL3CellKey = "kr5186:l3:2801:4581",
            MovementDirectionCode = direction,
        };

    private static void AssertLevel(
        SimulationLhLevelResponse value, string code, int meters, string hCode)
    {
        Assert.Equal(code, value.LevelCode);
        Assert.Equal(meters, value.CellSizeMeters);
        Assert.Equal(hCode, value.DefaultHLevelCode);
        Assert.Equal(hCode, value.PrimaryHQueryLevelCode);
    }

    private static void AssertSeason(
        int day, string season, int seasonDay, string next)
    {
        var value = SimulationLhWorldService.CreateSeason(day);
        Assert.Equal(season, value.SeasonCode);
        Assert.Equal(seasonDay, value.SeasonDay);
        Assert.Equal(next, value.NextSeasonCode);
    }

    private static 경영SimulationSession생성Request CreateSessionRequest()
        => new()
        {
            ClientRequestId = Guid.Parse("da0a0c2e-872a-47e2-9242-24c56812a77a"),
            ScenarioStableId = "scenario:sim.lh-world-0",
            ScenarioDataRevision = "scenario-data:lh-r1",
            ScenarioSeed = 20260818,
            RuleRevision = "rule:lh-r1",
            DurationTicks = 112,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim.farmers-lh",
                TerritoryStableId = "territory:sim.pyeongchang-lh",
                SettlementStableId = "settlement:sim.daegwallyeong-lh",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
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

    private sealed class RecordingCellContentSource : ISimulationLhCellContentSource
    {
        public List<string> RequestedCellKeys { get; } = new();

        public string ContentSourceCode => SimulationLhWorldCodes.AuthoritativeWorld;

        public SimulationLhCellPlanResponse CreateCellPlan(
            SimulationLhWindowCell windowCell,
            SimulationLhCellContentContext context)
        {
            RequestedCellKeys.Add(windowCell.CellKey);
            return new SimulationLhCellPlanResponse
            {
                CellKey = windowCell.CellKey,
                CellX = windowCell.CellX,
                CellY = windowCell.CellY,
                WindowRoleCode = windowCell.WindowRoleCode,
                Priority = windowCell.Priority,
                ContentSourceCode = ContentSourceCode,
                BasePlanHashSha256 = new string('a', 64),
                PresentationHashSha256 = new string('b', 64),
                PresentationOnly = true,
            };
        }
    }
}
