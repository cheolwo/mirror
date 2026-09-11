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
public sealed class SimulationWorldLayoutTests
{
    private const string LayoutId = "world-layout:sim:pyeongchang:nature-farm-hub-town.v1";

    [Fact]
    public void H5는_네조립지역과_다섯고정앵커_세물리회랑을_부모좌표계로읽는다()
    {
        Assert.True(Reader().TryRead(out var catalog, out var errorCode), errorCode);

        Assert.Equal(LayoutId, catalog.Definition.WorldLayoutStableId);
        Assert.Equal(4, catalog.Definition.AreaSetInstances.Length);
        Assert.Equal(5, catalog.Definition.AreaAnchors.Length);
        Assert.Equal(3, catalog.Definition.CorridorInstances.Length);
        Assert.All(catalog.Definition.AreaSetInstances, area =>
        {
            Assert.Equal(SimulationWorldLayoutCodes.ScenarioLocalMeters,
                area.PlacementTransform.CoordinateSpaceCode);
            Assert.All(area.GraphInstances, graph => Assert.Equal(
                SimulationWorldLayoutCodes.ParentLocalMeters,
                graph.PlacementTransform.CoordinateSpaceCode));
        });
        Assert.Equal(3, catalog.Definition.Relations.Count(item =>
            item.SpatialRealizationCode == SimulationWorldLayoutCodes.PhysicalCorridor));
        Assert.Equal(5, catalog.Definition.Relations.Count(item =>
            item.SpatialRealizationCode == SimulationWorldLayoutCodes.AbstractTravel));
        Assert.Single(catalog.Definition.Relations, item =>
            item.SpatialRealizationCode == SimulationWorldLayoutCodes.ReservedCorridor);
        var hub = Assert.Single(catalog.Definition.AreaSetInstances, item =>
            item.AreaSetInstanceStableId == "area-set:sim:pyeongchang:logistics-hub.v1");
        Assert.Equal(SimulationWorldLayoutCodes.Hub, hub.AreaRoleCode);
        Assert.Contains(SimulationWorldLayoutCodes.LegacyCityHub, hub.LegacyAreaRoleCodes);
    }

    [Fact]
    public void 다섯영역좌표와특징은고정되고_City는예약상태다()
    {
        Assert.True(Reader().TryRead(out var catalog, out var errorCode), errorCode);

        var anchors = catalog.Definition.AreaAnchors.ToDictionary(
            item => item.CanonicalAreaRoleCode, StringComparer.Ordinal);
        AssertAnchor(anchors["NatureHome"], 0d, 0d,
            "nature-woodland-recovery.r1", SimulationWorldLayoutCodes.Composed, true);
        AssertAnchor(anchors["Farm"], 634.910789d, -93.977416d,
            "farm-crossroad-potato-production.r1", SimulationWorldLayoutCodes.Composed, true);
        AssertAnchor(anchors[SimulationWorldLayoutCodes.Hub], 395.256719d, -564.642079d,
            "hub-flat-logistics-junction.r1", SimulationWorldLayoutCodes.Composed, true);
        Assert.Contains(SimulationWorldLayoutCodes.LegacyCityHub,
            anchors[SimulationWorldLayoutCodes.Hub].LegacyAreaRoleCodes);
        AssertAnchor(anchors["Town"], -384.825022d, -1929.888118d,
            "town-lowrise-market-life.r1", SimulationWorldLayoutCodes.Composed, true);
        AssertAnchor(anchors[SimulationWorldLayoutCodes.City], -980.157889d, -2971.799236d,
            "city-dense-service-grid.r1", SimulationWorldLayoutCodes.Reserved, false);
        Assert.True(anchors[SimulationWorldLayoutCodes.City].CanPrefetchMetadata);
        Assert.False(anchors[SimulationWorldLayoutCodes.City].CanTraverse);
        Assert.Single(catalog.Definition.ReservedConnections, value =>
            value.FromAreaSetInstanceStableId == "area-set:sim:pyeongchang:town-market.v1"
            && value.ToAreaSetInstanceStableId == "area-set:sim:pyeongchang:city-service.v1");
        Assert.Equal(SimulationWorldLayoutCodes.Hub,
            SimulationWorldLayoutCodes.NormalizeAreaRoleCode(
                SimulationWorldLayoutCodes.LegacyCityHub));
    }

    [Fact]
    public void 선택형E6는_H5해시를바꾸지않고_권위적용과준비도를분리한다()
    {
        Assert.True(Reader().TryRead(out var catalog, out var errorCode), errorCode);

        Assert.Equal(SimulationWorldLayoutCodes.Optional,
            catalog.Definition.WorldGroundingPolicyCode);
        Assert.Equal(catalog.Definition.WorldLayoutHashSha256,
            catalog.GroundingBinding.WorldLayoutHashSha256);
        Assert.Equal(SimulationWorldLayoutCodes.ScenarioRelative,
            catalog.GroundingBinding.PlacementAuthorityCode);
        Assert.Equal(SimulationWorldLayoutCodes.NotApplied,
            catalog.GroundingBinding.WorldGroundingStateCode);
        Assert.Empty(catalog.GroundingBinding.E6AnchorStableId);
        Assert.Equal(SimulationWorldLayoutCodes.Partial,
            catalog.GroundingReadiness.GroundingReadinessStateCode);
        Assert.False(catalog.GroundingReadiness.AppliesAuthority);
    }

    [Fact]
    public void LH는_E6미적용이어도_H5권위셀내용을사용한다()
    {
        var source = new H5AuthoritativeSimulationLhCellContentSource(Reader());

        var cell = source.CreateCellPlan(new SimulationLhWindowCell
        {
            CellKey = SimulationLhWorldService.L3CellKey(
                SimulationLhWorldService.CenterL3X, SimulationLhWorldService.CenterL3Y),
            CellX = SimulationLhWorldService.CenterL3X,
            CellY = SimulationLhWorldService.CenterL3Y,
            WindowRoleCode = SimulationLhWorldCodes.Detail,
        }, new SimulationLhCellContentContext
        {
            Season = SimulationLhWorldService.CreateSeason(1),
        });

        Assert.Equal(SimulationLhWorldCodes.AuthoritativeWorld, source.ContentSourceCode);
        Assert.Equal(SimulationLhWorldCodes.AuthoritativeWorld, cell.ContentSourceCode);
        Assert.Contains(cell.HBindings, item => item.HLevelCode == "H4");
        Assert.Contains(cell.HBindings, item => item.HLevelCode == "H3");
        Assert.All(cell.Placements, item => Assert.Equal(1d, item.UniformScale));
    }

    [Fact]
    public void H5대장이없으면_LH가절차생성으로조용히후퇴하지않는다()
    {
        var missing = new FileSimulationWorldLayoutCatalogReader(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

        var error = Assert.Throws<InvalidOperationException>(() =>
            new H5AuthoritativeSimulationLhCellContentSource(missing));

        Assert.Equal("WorldLayoutCatalogUnavailable", error.Message);
    }

    [Fact]
    public void 구판H5의_CityHub역할은읽기호환으로유지한다()
    {
        var legacyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var legacyJson = File.ReadAllText(CatalogPath())
                .Replace("\"worldLayoutRevision\": 3", "\"worldLayoutRevision\": 2", StringComparison.Ordinal)
                .Replace("\"groundingBindingRevision\": 3", "\"groundingBindingRevision\": 2", StringComparison.Ordinal)
                .Replace("\"areaRoleCode\": \"Hub\"", "\"areaRoleCode\": \"CityHub\"", StringComparison.Ordinal);
            File.WriteAllText(legacyPath, legacyJson);

            var reader = new FileSimulationWorldLayoutCatalogReader(legacyPath);
            Assert.True(reader.TryRead(out var catalog, out var errorCode), errorCode);
            Assert.Contains(catalog.Definition.AreaSetInstances, item =>
                item.AreaRoleCode == SimulationWorldLayoutCodes.LegacyCityHub);
        }
        finally
        {
            if (File.Exists(legacyPath)) File.Delete(legacyPath);
        }
    }

    [Fact]
    public async Task WorldStream_API가_H5정의와_E6결속_준비도를분리해제공한다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var escaped = Uri.EscapeDataString(LayoutId);

        var definitionResponse = await client.GetAsync(
            "/api/simulation/v1/world-stream/world-layouts/" + escaped);
        var bindingResponse = await client.GetAsync(
            "/api/simulation/v1/world-stream/world-layouts/" + escaped + "/grounding-binding");
        var readinessResponse = await client.GetAsync(
            "/api/simulation/v1/world-stream/world-layouts/" + escaped + "/grounding-readiness");

        Assert.Equal(HttpStatusCode.OK, definitionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bindingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        var definition = (await definitionResponse.Content
            .ReadFromJsonAsync<SimulationWorldLayoutDefinitionResponse>())!;
        Assert.Equal(4, definition.AreaSetInstances.Length);
        Assert.Equal(5, definition.AreaAnchors.Length);
        Assert.Single(definition.ReservedConnections);
        Assert.Equal(SimulationWorldLayoutCodes.NotApplied, (await bindingResponse.Content
            .ReadFromJsonAsync<SimulationWorldGroundingBindingResponse>())!.WorldGroundingStateCode);
        Assert.Equal(SimulationWorldLayoutCodes.Partial, (await readinessResponse.Content
            .ReadFromJsonAsync<SimulationWorldGroundingReadinessResponse>())!.GroundingReadinessStateCode);
    }

    private static void AssertAnchor(
        SimulationWorldAreaAnchorResponse anchor,
        double x,
        double z,
        string profile,
        string state,
        bool canActivate)
    {
        Assert.Equal(x, anchor.FixedPlacementTransform.LocalXMeters, 6);
        Assert.Equal(z, anchor.FixedPlacementTransform.LocalZMeters, 6);
        Assert.Equal(profile, anchor.AreaCharacterProfileCode);
        Assert.Equal(state, anchor.PlacementStateCode);
        Assert.Equal(canActivate, anchor.CanActivate);
        Assert.NotEmpty(anchor.PlacementRuleCodes);
        Assert.Equal(64, anchor.AnchorHashSha256.Length);
    }

    private static FileSimulationWorldLayoutCatalogReader Reader() => new(CatalogPath());

    private static string CatalogPath()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "eng", "world-seedbeds", "generated", "h5-world-layout.v1.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("h5-world-layout.v1.json");
    }
}
