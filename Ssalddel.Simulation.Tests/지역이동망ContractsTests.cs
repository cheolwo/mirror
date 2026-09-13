using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "사가정 이동망 계약의 지역 범위·좌표·타일·검토보류·비권위 불변조건을 검증한다.",
    Boundary = "계약 자동 시험이며 실제 도로 통행 승인·Unity Play Mode·Game View 증거가 아니다.")]
public sealed class 지역이동망ContractsTests
{
    [Fact]
    public void 지역이동망_공개계약은_역세권창과행정동경계를혼동하지않는다()
    {
        Assert.Equal("region-mobility-graph-manifest.v1", RegionMobilityGraphPolicy.ManifestSchemaVersion);
        Assert.Equal("region-mobility-graph-tile.v1", RegionMobilityGraphPolicy.TileSchemaVersion);
        Assert.Equal("SourceExplicitlyRestricted", RegionMobilityGraphPolicy.SourceExplicitlyRestricted);
        Assert.Equal(500, RegionMobilityGraphPolicy.TileSizeMeters);
        Assert.Equal(
            "api/v1/world/regions/{regionStableId}/mobility-graph-manifest",
            RegionMobilityGraphRoutes.Manifest);
        Assert.Equal(
            "api/v1/world/regions/{regionStableId}/mobility-graph-tiles/{tileStableId}",
            RegionMobilityGraphRoutes.Tile);

        var manifest = new RegionMobilityGraphManifest
        {
            GraphStableId = RegionMobilityGraphPolicy.FirstGraphStableId,
            RegionStableId = RegionMobilityGraphPolicy.FirstRegionStableId,
            AdministrativeAreaStableIds = new[] { "region:kr:hjd:1126057500" },
            CoverageCode = RegionMobilityGraphPolicy.SagajeongOneKilometerWindow,
            Coordinates = new RegionMobilityGraphCoordinates
            {
                Bounds = new[] { 50d, -492d, 1050d, 508d },
                WorldOffsetX = RegionMobilityGraphPolicy.FirstWorldOffsetX,
                WorldOffsetZ = RegionMobilityGraphPolicy.FirstWorldOffsetZ,
                MetersPerUnit = RegionMobilityGraphPolicy.FirstMetersPerUnit,
            },
        };

        Assert.True(RegionMobilityGraphPolicy.IsRegionStableId(manifest.RegionStableId));
        Assert.False(manifest.AdministrativeBoundaryClipped);
        Assert.False(manifest.DistributionApproved);
        Assert.False(manifest.TraversalReady);
        Assert.False(manifest.RuntimeAuthorized);
        Assert.Equal(RegionMobilityGraphPolicy.PendingHumanReview, manifest.ReadinessCode);
        Assert.Equal(RegionMobilityGraphPolicy.FirstWorldOffsetX, manifest.Coordinates.WorldOffsetX);
        Assert.Equal(RegionMobilityGraphPolicy.FirstWorldOffsetZ, manifest.Coordinates.WorldOffsetZ);
        Assert.Equal(RegionMobilityGraphPolicy.FirstMetersPerUnit, manifest.Coordinates.MetersPerUnit);
    }

    [Fact]
    public void 동네이동수단의_기존숫자값을보존하고_오토바이를추가한다()
    {
        Assert.Equal(0, (int)동네이동수단.Vehicle);
        Assert.Equal(1, (int)동네이동수단.Pedestrian);
        Assert.Equal(2, (int)동네이동수단.Motorcycle);
    }

    [Fact]
    public void 오토바이는_검토된차량계열경로를따라_차량속도로진행한다()
    {
        var map = 오토바이지도();
        var route = new 동네이동경로Engine().탐색(
            map,
            map.Revision,
            "restaurant-stop",
            "residence-a-stop",
            동네이동수단.Motorcycle);

        Assert.True(route.Found);
        Assert.Equal(40, route.LengthMeters);
        var engine = new 동네이동진행Engine();
        var afterOneTick = engine.진행(route, engine.시작(route), 1);
        Assert.Equal(동네이동진행Engine.VehicleMetersPerTick, afterOneTick.DistanceMeters);
        Assert.False(afterOneTick.RuntimeAuthorized);
    }

    [Fact]
    public void 오토바이는_출입구를_경로끝점으로사용하지않는다()
    {
        var map = 오토바이지도();
        var route = new 동네이동경로Engine().탐색(
            map,
            map.Revision,
            "restaurant-stop",
            "restaurant-door",
            동네이동수단.Motorcycle);

        Assert.False(route.Found);
        Assert.Equal("NeighborhoodVehicleEntranceForbidden", route.ReasonCode);
    }

    [Fact]
    public void 가져오기는_세이동수단을보존하되_오토바이출입구간선을거부한다()
    {
        var threeModes = 원본Fixture();
        var firstVehicleRoad = threeModes["features"]!.AsArray()
            .Select(item => item!.AsObject())
            .First(item => item["kind"]!.GetValue<string>() == "Road"
                && item["properties"]!["modes"]!.AsArray()
                    .Any(mode => mode!.GetValue<string>() == "Vehicle"));
        firstVehicleRoad["properties"]!["modes"] = new JsonArray("Vehicle", "Pedestrian", "Motorcycle");
        var imported = 읽기(threeModes);
        Assert.Equal(3, imported.Roads.Single(road => road.StableId == firstVehicleRoad["id"]!.GetValue<string>()).Modes.Count);

        var invalidEntrance = 원본Fixture();
        var entranceRoad = invalidEntrance["features"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["id"]!.GetValue<string>() == "restaurant-access");
        entranceRoad["properties"]!["modes"] = new JsonArray("Motorcycle");
        var error = Assert.Throws<InvalidDataException>(() => 읽기(invalidEntrance));
        Assert.Equal("NeighborhoodVehicleEntranceForbidden", error.Message);
    }

    private static 동네공간Snapshot 오토바이지도()
    {
        var json = 원본Fixture();
        foreach (var feature in json["features"]!.AsArray().Select(item => item!.AsObject()))
        {
            if (feature["kind"]!.GetValue<string>() != "Road") continue;
            var modes = feature["properties"]!["modes"]!.AsArray();
            if (modes.Any(mode => mode!.GetValue<string>() == "Vehicle"))
                feature["properties"]!["modes"] = new JsonArray("Motorcycle");
        }
        return 읽기(json);
    }

    private static JsonObject 원본Fixture()
    {
        using var stream = typeof(지역이동망ContractsTests).Assembly
            .GetManifestResourceStream("Ssalddel.Simulation.Tests.synthetic-delivery-block.v1.json")!;
        return JsonNode.Parse(stream)!.AsObject();
    }

    private static 동네공간Snapshot 읽기(JsonObject json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
        return new 동네공간ImportService().읽기(bytes, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
