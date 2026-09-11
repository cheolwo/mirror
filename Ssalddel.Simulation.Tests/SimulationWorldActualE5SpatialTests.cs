using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationWorldActualE5SpatialTests
{
    [Fact]
    public void 이론공간을_네개의실제AreaSet과_하나의Network로읽는다()
    {
        var reader = Reader();

        Assert.True(reader.TryRead(out var catalog, out var errorCode), errorCode);
        Assert.Equal(PyeongchangAreaSetStableIds.ActualNetwork,
            catalog.Network.NetworkStableId);
        Assert.Equal(4, catalog.AreaSets.Count);
        Assert.Equal(19, catalog.Graphs.Count);
        Assert.Equal(8, catalog.Network.Relations.Length);
        Assert.Equal(3, catalog.Network.RouteGraphs.Length);
        Assert.All(catalog.AreaSets.Values, areaSet =>
        {
            Assert.Equal(catalog.Network.NetworkStableId,
                areaSet.CanonicalNetworkStableId);
            Assert.Equal(SimulationWorldLandscapeCompositionCodes.ScenarioLocalMeters,
                areaSet.CoordinateSpaceCode);
        });
        Assert.All(catalog.Graphs.Values, graph =>
        {
            Assert.Equal(SimulationWorldLandscapeCompositionCodes.Available,
                graph.StatusCode);
            Assert.Empty(graph.Unresolved);
            Assert.True(graph.PresentationOnly);
            Assert.False(graph.IsOperationalState);
        });
    }

    [Fact]
    public async Task 예순네개WI를_직접42_문맥6_비공간9_E5대기7로분류한다()
    {
        var service = new SimulationWorld상호작용NetworkService(Reader());

        var result = await service.EvaluateAsync(PyeongchangAreaSetStableIds.ActualNetwork);

        Assert.Equal(SimulationWorld상호작용Graph상태Codes.Partial,
            result.OverallStatusCode);
        Assert.Equal(64, result.TotalWorldInteractionCount);
        Assert.Equal(42, result.DirectBindings.Length);
        Assert.Equal(6, result.ContextualBindings.Length);
        Assert.Equal(9, result.NonSpatialBindings.Length);
        Assert.Equal(new[]
        {
            "WI-CITY-01", "WI-CITY-02", "WI-CITY-03", "WI-CITY-04",
            "WI-NATURE-16", "WI-NATURE-17", "WI-REFLECT-01",
        }, result.PendingE5WiIds);
        Assert.Equal(19, result.GraphAudits.Length);
        Assert.All(result.DirectBindings, item =>
        {
            Assert.Equal(SimulationWorld상호작용Graph상태Codes.Ready,
                item.StatusCode);
            Assert.True(item.SpatialClosedLoop);
            Assert.NotNull(item.SpatialDefinition);
            Assert.StartsWith("h1-", item.H1Ref);
            Assert.StartsWith("h2-", item.H2Ref);
            Assert.StartsWith("h3-", item.H3Ref);
        });
        Assert.All(result.ContextualBindings, item => Assert.Equal(
            SimulationWorld상호작용Graph상태Codes.ContextBound, item.StatusCode));
        Assert.All(result.NonSpatialBindings, item => Assert.Equal(
            SimulationWorld상호작용Graph상태Codes.NotSpatiallyApplicable,
            item.StatusCode));
        Assert.All(result.Transitions.Where(item =>
            item.StatusCode == SimulationWorld상호작용Graph상태Codes.PathUnresolved),
            item => Assert.True(
                result.PendingE5WiIds.Contains(item.FromWorldInteractionId)
                || result.PendingE5WiIds.Contains(item.ToWorldInteractionId)));
    }

    [Fact]
    public async Task 실제E5_AreaSet은_시나리오지역Graph인덱스를제공한다()
    {
        var service = new SimulationWorldActualE5SpatialService(Reader());

        var index = await service.ReadGraphIndexAsync(
            PyeongchangAreaSetStableIds.FarmAreaSet, null, 4);

        Assert.NotNull(index);
        Assert.Equal(4, index!.Graphs.Length);
        Assert.All(index.CoveredTileKeys,
            item => Assert.StartsWith("scenario-local:", item));
    }

    [Fact]
    public async Task Farm몰입WI는_실제E5_H3공간정의로세션에공급된다()
    {
        var service = new SimulationWorld상호작용NetworkService(Reader());

        var world = await service.ResolveSpatialWorldAsync(
            PyeongchangAreaSetStableIds.ActualNetwork,
            PyeongchangAreaSetStableIds.FarmAreaSet,
            new[] { "WI-FARM-04", "WI-FARM-05", "WI-FARM-06", "WI-LOG-01", "WI-LOG-02" });

        Assert.Equal(5, world.Definitions.Length);
        Assert.All(world.Definitions, definition =>
        {
            Assert.Equal(PyeongchangAreaSetStableIds.FarmAreaSet,
                definition.AreaSetStableId);
            Assert.Equal(Simulation공간근거종류Codes.LandscapeGraph,
                definition.EvidenceKindCode);
            Assert.StartsWith("landscape-graph:sim:pyeongchang:",
                definition.LandscapeGraphStableId);
            Assert.Contains("binding-sha256:", string.Join("|", definition.SourceStableIds));
        });
    }

    [Fact]
    public async Task Nature생활WI는_실제E5_H1_H2_H3와Graph근거로세션에공급된다()
    {
        var service = new SimulationWorld상호작용NetworkService(Reader());
        var wiIds = Enumerable.Range(5, 7)
            .Select(number => $"WI-NATURE-{number:00}")
            .ToArray();

        var world = await service.ResolveSpatialWorldAsync(
            PyeongchangAreaSetStableIds.ActualNetwork,
            PyeongchangAreaSetStableIds.NatureAreaSet,
            wiIds);

        Assert.Equal(7, world.Definitions.Length);
        Assert.All(world.Definitions, definition =>
        {
            Assert.Equal(PyeongchangAreaSetStableIds.NatureAreaSet,
                definition.AreaSetStableId);
            Assert.Equal(Simulation공간근거종류Codes.LandscapeGraph,
                definition.EvidenceKindCode);
            Assert.StartsWith("landscape-graph:sim:pyeongchang:nature-",
                definition.LandscapeGraphStableId);
            Assert.Contains(definition.SourceStableIds,
                item => item.StartsWith("h1-", StringComparison.Ordinal));
            Assert.Contains(definition.SourceStableIds,
                item => item.StartsWith("h2-", StringComparison.Ordinal));
            Assert.Contains(definition.SourceStableIds,
                item => item.StartsWith("h3-", StringComparison.Ordinal));
            Assert.Contains(definition.SourceStableIds,
                item => item.StartsWith("binding-sha256:", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task Nature위협감지는_승인된E5관찰공간과결정적용량으로세션에공급된다()
    {
        var service = new SimulationWorld상호작용NetworkService(Reader());

        var world = await service.ResolveSpatialWorldAsync(
            PyeongchangAreaSetStableIds.ActualNetwork,
            PyeongchangAreaSetStableIds.NatureAreaSet,
            new[] { "WI-NATURE-01" });

        var definition = Assert.Single(world.Definitions);
        Assert.Equal("spatial:actual-e5:wi-nature-01", definition.SpatialStableId);
        Assert.Equal(Simulation공간근거종류Codes.LandscapeGraph,
            definition.EvidenceKindCode);
        Assert.Equal(new[]
        {
            "Spatial.ObservationArea",
            "Spatial.ThreatMonitoringArea",
            "Spatial.Traversable",
        }, definition.CapabilityCodes);
        Assert.Contains(definition.BaseCapacities, item =>
            item.CapacityCode == "WorkArea" && item.Quantity == 1m
            && item.UnitCode == "slot");
        Assert.Contains(definition.BaseCapacities, item =>
            item.CapacityCode == "Actor" && item.Quantity == 1m
            && item.UnitCode == "player");
        Assert.Contains(definition.BaseCapacities, item =>
            item.CapacityCode == "MonitoredThreatRoute" && item.Quantity == 1m
            && item.UnitCode == "route");
        Assert.Contains("wi-spatial-seedbed:nature-survival-encounter.v1",
            definition.SourceStableIds);
        Assert.Contains(definition.SourceStableIds,
            item => item.StartsWith("binding-sha256:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WorldStream_API에서_Network와예순개WI준비도를조회한다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var escaped = Uri.EscapeDataString(PyeongchangAreaSetStableIds.ActualNetwork);

        var networkResponse = await client.GetAsync(
            "/api/simulation/v1/world-stream/area-set-networks/" + escaped);
        var readinessResponse = await client.GetAsync(
            "/api/simulation/v1/world-stream/area-set-networks/" + escaped
            + "/interaction-readiness");

        Assert.Equal(HttpStatusCode.OK, networkResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        var network = await networkResponse.Content
            .ReadFromJsonAsync<SimulationWorldAreaSetNetworkResponse>();
        var readiness = await readinessResponse.Content
            .ReadFromJsonAsync<SimulationWorld상호작용Network준비도Response>();
        Assert.Equal(4, network!.AreaSets.Length);
        Assert.Equal(64, readiness!.TotalWorldInteractionCount);
        Assert.Equal(7, readiness.PendingE5WiIds.Length);
        Assert.Equal(SimulationWorld상호작용Graph상태Codes.Partial,
            readiness.OverallStatusCode);
    }

    [Fact]
    public async Task 실제E5세션_API는_FarmH5배치와공간폐루프를고정하고_E6를요구하지않는다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        const string layoutId = "world-layout:sim:pyeongchang:nature-farm-hub-town.v1";
        var layout = await client.GetFromJsonAsync<SimulationWorldLayoutDefinitionResponse>(
            "/api/simulation/v1/world-stream/world-layouts/" + Uri.EscapeDataString(layoutId));
        Assert.NotNull(layout);

        var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/actual-e5",
            new SimulationActualE5SessionCreateRequest
            {
                AreaSetNetworkStableId = PyeongchangAreaSetStableIds.ActualNetwork,
                AreaSetStableId = PyeongchangAreaSetStableIds.FarmAreaSet,
                WorldLayoutStableId = layoutId,
                ExpectedWorldLayoutRevision = layout!.WorldLayoutRevision,
                ExpectedWorldLayoutHashSha256 = layout.WorldLayoutHashSha256,
                WorldInteractionIds = new[]
                {
                    "WI-FARM-04", "WI-FARM-05", "WI-FARM-06", "WI-LOG-01", "WI-LOG-02",
                },
                Session = CreateSessionRequest(),
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<SimulationActualE5SessionCreateResponse>();
        Assert.NotNull(result);
        Assert.Equal("E5", result!.EvidenceStageCode);
        Assert.Equal(SimulationWorldLayoutCodes.ScenarioRelative,
            result.PlacementAuthorityCode);
        Assert.Equal(SimulationWorldLayoutCodes.NotApplied,
            result.WorldGroundingStateCode);
        Assert.Equal(5, result.Session.SpatialDefinitions.Length);
        Assert.All(result.Session.SpatialDefinitions, item => Assert.Equal(
            Simulation공간근거종류Codes.LandscapeGraph, item.EvidenceKindCode));
    }

    [Fact]
    public async Task 실제E5세션_API는_낡은H5해시와_클라이언트공간주입을거부한다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        const string layoutId = "world-layout:sim:pyeongchang:nature-farm-hub-town.v1";
        var request = new SimulationActualE5SessionCreateRequest
        {
            AreaSetNetworkStableId = PyeongchangAreaSetStableIds.ActualNetwork,
            AreaSetStableId = PyeongchangAreaSetStableIds.FarmAreaSet,
            WorldLayoutStableId = layoutId,
            ExpectedWorldLayoutRevision = 2,
            ExpectedWorldLayoutHashSha256 = new string('0', 64),
            WorldInteractionIds = new[] { "WI-FARM-04" },
            Session = CreateSessionRequest(),
        };

        var stale = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/actual-e5", request);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        Assert.Equal("SimulationWorldLayoutRevisionMismatch",
            (await stale.Content.ReadFromJsonAsync<SimulationErrorResponse>())!.ErrorCode);

        var layout = await client.GetFromJsonAsync<SimulationWorldLayoutDefinitionResponse>(
            "/api/simulation/v1/world-stream/world-layouts/" + Uri.EscapeDataString(layoutId));
        request.ExpectedWorldLayoutRevision = layout!.WorldLayoutRevision;
        request.ExpectedWorldLayoutHashSha256 = layout.WorldLayoutHashSha256;
        request.Session.SpatialWorld = new Simulation공간세계InitialStateRequest();
        var injected = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/actual-e5", request);
        Assert.Equal(HttpStatusCode.BadRequest, injected.StatusCode);
        Assert.Equal("SimulationActualE5ClientSpatialWorldForbidden",
            (await injected.Content.ReadFromJsonAsync<SimulationErrorResponse>())!.ErrorCode);
    }

    [Fact]
    public async Task Farm_E6는_E5와GIS축을보존하고_네H3와교차폐루프를승격한다()
    {
        var service = new SimulationAreaSetImmersionService(
            new FileSimulationAreaSetImmersionCatalogReader(ImmersionCatalogPath()));

        var result = await service.ReadAsync(PyeongchangAreaSetStableIds.FarmAreaSet);

        Assert.NotNull(result);
        Assert.Equal(SimulationAreaSetImmersionCodes.SpatialE5Qualified,
            result!.SpatialMaturityCode);
        Assert.Equal(SimulationAreaSetImmersionCodes.ImmersionQualified,
            result.ImmersionMaturityCode);
        Assert.Equal(SimulationAreaSetImmersionCodes.Current,
            result.FreshnessStateCode);
        Assert.Equal(SimulationAreaSetImmersionCodes.NotApplied,
            result.GroundingStatusCode);
        Assert.Equal(SimulationAreaSetImmersionCodes.Open,
            result.E7GateStateCode);
        Assert.Equal(4, result.H3Audits.Length);
        Assert.All(result.H3Audits, audit =>
        {
            Assert.Equal(SimulationAreaSetImmersionCodes.ImmersionQualified,
                audit.ImmersionMaturityCode);
            Assert.Equal(SimulationAreaSetImmersionCodes.Current,
                audit.FreshnessStateCode);
            Assert.NotEmpty(audit.H2StableIds);
            Assert.NotEmpty(audit.H1StableIds);
            Assert.All(audit.Questions, question =>
                Assert.Equal("Pass", question.QualificationResultCode));
        });
        Assert.Equal(3, result.CrossH3Closures.Length);
        Assert.All(result.CrossH3Closures, closure =>
            Assert.Equal("Pass", closure.QualificationResultCode));
        Assert.False(result.PublicDataChangesSimulationRules);
        Assert.False(result.PublicDataMovesSpatialDefinitions);
        Assert.False(result.RuntimeValidated);
    }

    [Fact]
    public async Task Farm_E7시작_API는_Current_E6관문을요구하지만_E7완료를주장하지않는다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        const string layoutId = "world-layout:sim:pyeongchang:nature-farm-hub-town.v1";
        var layout = await client.GetFromJsonAsync<SimulationWorldLayoutDefinitionResponse>(
            "/api/simulation/v1/world-stream/world-layouts/" + Uri.EscapeDataString(layoutId));
        Assert.NotNull(layout);

        var readiness = await client.GetFromJsonAsync<SimulationAreaSetImmersionReadinessResponse>(
            "/api/simulation/v1/world-stream/area-sets/"
            + Uri.EscapeDataString(PyeongchangAreaSetStableIds.FarmAreaSet)
            + "/immersion-readiness");
        Assert.NotNull(readiness);
        Assert.Equal(SimulationAreaSetImmersionCodes.Open, readiness!.E7GateStateCode);

        var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/actual-e5/e7",
            new SimulationActualE5SessionCreateRequest
            {
                AreaSetNetworkStableId = PyeongchangAreaSetStableIds.ActualNetwork,
                AreaSetStableId = PyeongchangAreaSetStableIds.FarmAreaSet,
                WorldLayoutStableId = layoutId,
                ExpectedWorldLayoutRevision = layout!.WorldLayoutRevision,
                ExpectedWorldLayoutHashSha256 = layout.WorldLayoutHashSha256,
                WorldInteractionIds = new[]
                {
                    "WI-FARM-04", "WI-FARM-05", "WI-FARM-06", "WI-LOG-01", "WI-LOG-02",
                },
                Session = CreateSessionRequest(),
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SimulationE7LaunchResponse>();
        Assert.NotNull(result);
        Assert.Equal("E7", result!.TargetEvidenceStageCode);
        Assert.False(result.RuntimeValidationCompleted);
        Assert.Equal(SimulationAreaSetImmersionCodes.ImmersionQualified,
            result.ImmersionReadiness.ImmersionMaturityCode);
        Assert.Equal("E5", result.SessionCreation.EvidenceStageCode);
    }

    [Fact]
    public async Task E7관문은_과거합격이더라도_Stale이면닫힌다()
    {
        var stale = new SimulationAreaSetImmersionReadinessResponse
        {
            AreaSetStableId = PyeongchangAreaSetStableIds.FarmAreaSet,
            SpatialMaturityCode = SimulationAreaSetImmersionCodes.SpatialE5Qualified,
            ImmersionMaturityCode = SimulationAreaSetImmersionCodes.ImmersionQualified,
            FreshnessStateCode = SimulationAreaSetImmersionCodes.Stale,
            E7GateStateCode = SimulationAreaSetImmersionCodes.Closed,
        };
        var service = new SimulationAreaSetImmersionService(
            new FixedImmersionReader(stale));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequireE7GateAsync(PyeongchangAreaSetStableIds.FarmAreaSet));

        Assert.Equal("AreaSetImmersionStale", error.Message);
        Assert.Equal(SimulationAreaSetImmersionCodes.ImmersionQualified,
            stale.ImmersionMaturityCode);
    }

    private static 경영SimulationSession생성Request CreateSessionRequest() => new()
    {
        ClientRequestId = Guid.NewGuid(),
        ScenarioStableId = "scenario:sim.farm-immersive-e5-1",
        ScenarioDataRevision = "scenario-data:r1",
        ScenarioSeed = 20260820,
        RuleRevision = "rule:farm-immersive.r1",
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:sim.farmers-1",
            TerritoryStableId = "territory:sim.farm-production-1",
            SettlementStableId = "settlement:sim.farm-home-1",
            GameDateStartsOn = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        },
    };

    private static FileSimulationWorldActualE5SpatialCatalogReader Reader() =>
        new(CatalogPath());

    private static string CatalogPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "eng", "world-seedbeds",
                "generated", "actual-e5-spatial.v1.json");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException("actual-e5-spatial.v1.json");
    }

    private static string ImmersionCatalogPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "eng", "world-seedbeds",
                "generated", "area-set-immersion-readiness.v1.json");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException("area-set-immersion-readiness.v1.json");
    }

    private sealed class FixedImmersionReader(
        SimulationAreaSetImmersionReadinessResponse readiness) :
        ISimulationAreaSetImmersionCatalogReader
    {
        public bool TryRead(out SimulationAreaSetImmersionReadinessResponse value,
            out string errorCode)
        {
            value = readiness;
            errorCode = string.Empty;
            return true;
        }
    }
}
