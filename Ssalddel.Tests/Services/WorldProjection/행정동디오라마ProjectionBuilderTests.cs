using System.Text;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Services.WorldProjection;

public sealed class 행정동디오라마ProjectionBuilderTests
{
    private const string AreaId = "region:kr:hjd:1126057500";

    [Fact]
    public void 공식행정동경계가없으면_임의배치하지않고_대기상태를반환한다()
    {
        var result = 행정동디오라마ProjectionBuilder.Build(Input(boundary: []));

        Assert.Equal(AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary,
            result.Manifest.ReadinessCode);
        Assert.Empty(result.Manifest.Boundary);
        Assert.Empty(result.Tiles);
        Assert.False(result.Manifest.TraversalReady);
        Assert.False(result.Manifest.GameplayReady);
    }

    [Fact]
    public void 경계안의근거자료만_500m타일과표시오버레이로투영한다()
    {
        var input = Input(Square(0, 0, 1_000));
        input.Buildings =
        [
            Building("building:inside", Square(100, 100, 30)),
            Building("building:outside", Square(1_100, 1_100, 30))
        ];
        input.Roads =
        [
            new 행정동디오라마RoadInput
            {
                RoadStableId = "road:crossing",
                From = Point(-100, 250),
                To = Point(1_100, 250),
                EvidenceKindCode = "PublicOpenData"
            }
        ];
        input.PublicBusinesses =
        [
            new 행정동디오라마PublicBusinessInput
            {
                BusinessStableId = "business:inside",
                DisplayName = "공개 검토 완료 사업장",
                CategoryCode = "restaurant",
                SemanticPlaceStableId = "place:restaurant:inside",
                Position = Point(200, 200),
                PresentationApproved = true
            },
            new 행정동디오라마PublicBusinessInput
            {
                BusinessStableId = "business:not-approved",
                DisplayName = "검토 전 사업장",
                CategoryCode = "restaurant",
                SemanticPlaceStableId = "place:restaurant:not-approved",
                Position = Point(300, 300),
                PresentationApproved = false
            }
        ];

        var result = 행정동디오라마ProjectionBuilder.Build(input);

        Assert.Equal(AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly, result.Manifest.ReadinessCode);
        Assert.Equal(2, result.Tiles.Length);
        Assert.Single(result.Tiles.SelectMany(tile => tile.Buildings));
        var road = Assert.Single(result.Tiles.SelectMany(tile => tile.Roads));
        Assert.Equal(0, road.From.X, 6);
        Assert.Equal(1_000, road.To.X, 6);
        var overlay = Assert.Single(result.DisplayOverlays.Items);
        Assert.Equal(AdministrativeDongDisplayOverlayKinds.PublicBusiness, overlay.OverlayKindCode);
        Assert.False(overlay.AdvertisementDisclosureRequired);
        Assert.Contains("place:restaurant:inside", result.Manifest.SemanticPlaceStableIds);
        Assert.DoesNotContain("place:restaurant:not-approved", result.Manifest.SemanticPlaceStableIds);
    }

    [Fact]
    public void 동일한자료는_입력순서와생성시각이달라도_같은Hash를만든다()
    {
        var first = Input(Square(0, 0, 1_000));
        first.Buildings = [Building("building:b", Square(600, 100, 30)), Building("building:a", Square(100, 100, 30))];
        var second = Input(Square(0, 0, 1_000));
        second.GeneratedAtUtc = first.GeneratedAtUtc.AddDays(1);
        second.Buildings = first.Buildings.Reverse().ToArray();

        var a = 행정동디오라마ProjectionBuilder.Build(first);
        var b = 행정동디오라마ProjectionBuilder.Build(second);

        Assert.Equal(a.Manifest.ProjectionHashSha256, b.Manifest.ProjectionHashSha256);
        Assert.Equal(a.Tiles.Select(tile => tile.TileHashSha256), b.Tiles.Select(tile => tile.TileHashSha256));
    }

    [Fact]
    public void GeoJson은_정확한행정동코드만_기존사가정좌표계로변환한다()
    {
        const string json = """
        {"type":"FeatureCollection","features":[
          {"type":"Feature","properties":{"adm_cd":"1126057500","adm_nm":"면목제3·8동"},
           "geometry":{"type":"Polygon","coordinates":[[[127.0884106,37.5806971],[127.0894106,37.5806971],[127.0894106,37.5816971],[127.0884106,37.5816971],[127.0884106,37.5806971]]]}}
        ]}
        """;
        var frame = Frame();

        var result = 행정동경계GeoJsonReader.Read(Encoding.UTF8.GetBytes(json), AreaId, "2026-09", frame);

        Assert.Equal("면목제3·8동", result.DisplayName);
        Assert.Equal(4, result.Boundary.Length);
        Assert.Equal(frame.WorldOffsetX, result.Boundary[0].X, 3);
        Assert.Equal(frame.WorldOffsetZ, result.Boundary[0].Z, 3);
    }

    [Fact]
    public async Task 게시서비스는_동결경계Hash가다르면_Mongo게시를시작하지않는다()
    {
        var store = new RecordingStore();
        var service = new 행정동디오라마PublicationService(store);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => service.PublishAsync(
            new 행정동디오라마PublicationRequest
            {
                FrozenAdministrativeBoundaryGeoJson = Encoding.UTF8.GetBytes("{}"),
                ExpectedBoundaryContentHashSha256 = new string('0', 64),
                ProjectionInput = Input([])
            },
            CancellationToken.None));

        Assert.Equal("AdministrativeBoundaryContentHashMismatch", error.Message);
        Assert.False(store.Published);
    }

    [Fact]
    public void 서울시5181좌표를_Wgs84로_결정적으로변환한다()
    {
        var point = 행정동경계ShapefileZipReader.InverseKorea2000CentralBelt(208687d, 453886d);

        Assert.Equal(127.0983518657d, point.Longitude, 7);
        Assert.Equal(37.5844897022d, point.Latitude, 7);
    }

    [Fact]
    public void 여섯행정동귀속은_하나또는미해결만반환한다()
    {
        var buildings = new[]
        {
            Building("building:left", Square(10, 10, 10)),
            Building("building:right", Square(110, 10, 10)),
            Building("building:outside", Square(250, 10, 10))
        };
        var result = 행정동공간귀속Classifier.AssignBuildings(buildings,
        [
            new 행정동공간Boundary("region:kr:hjd:1126052000", Square(0, 0, 100)),
            new 행정동공간Boundary("region:kr:hjd:1126057500", Square(100, 0, 100))
        ]);

        Assert.Equal(2, result.AssignedCount);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.Equal("region:kr:hjd:1126052000", result.Assignments.Single(item => item.BuildingStableId == "building:left").AdministrativeAreaStableId);
        Assert.Null(result.Assignments.Single(item => item.BuildingStableId == "building:outside").AdministrativeAreaStableId);
    }

    private static 행정동디오라마ProjectionBuildInput Input(AdministrativeDongDioramaPoint[] boundary)
        => new()
        {
            AdministrativeAreaStableId = AreaId,
            DisplayName = "면목제3·8동",
            SourceVintage = "2026-09",
            GeneratedAtUtc = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc),
            CoordinateFrame = new AdministrativeDongDioramaCoordinateFrame
            {
                Method = "test-local-meters",
                MetersPerUnit = 1
            },
            Boundary = boundary,
            LegalAreaStableIds = ["region:kr:bjd:1126010100"],
            Sources =
            [
                new AdministrativeDongDioramaSourceAttribution
                {
                    SourceId = "source:official-boundary",
                    DatasetId = "dataset:hjd-boundary",
                    SourceRevision = "2026-09",
                    ContentHashSha256 = new string('a', 64),
                    LicenseCode = "PublicOpenData",
                    LimitationCode = "ReviewRequired"
                }
            ]
        };

    private static 행정동디오라마BuildingInput Building(string id, AdministrativeDongDioramaPoint[] footprint)
        => new()
        {
            BuildingStableId = id,
            CategoryCode = "residential",
            EvidenceKindCode = "PublicOpenData",
            BuildingAreaSquareMeters = 900,
            TotalFloorAreaSquareMeters = 2_700,
            AboveGroundFloorCount = 3,
            Footprint = footprint
        };

    private static AdministrativeDongDioramaPoint[] Square(double x, double z, double size)
        => [Point(x, z), Point(x + size, z), Point(x + size, z + size), Point(x, z + size), Point(x, z)];

    private static AdministrativeDongDioramaPoint Point(double x, double z) => new() { X = x, Z = z };

    private static AdministrativeDongDioramaCoordinateFrame Frame()
        => new()
        {
            Method = "WGS84-ECEF-ENU-at-zero-altitude",
            OriginLatitude = 37.5806971,
            OriginLongitude = 127.0884106,
            WorldOffsetX = 550,
            WorldOffsetZ = 8,
            MetersPerUnit = 1
        };

    private sealed class RecordingStore : I행정동디오라마ProjectionStore
    {
        public bool Published { get; private set; }

        public Task PublishAsync(행정동디오라마ProjectionBuildResult projection, CancellationToken cancellationToken)
        {
            Published = true;
            return Task.CompletedTask;
        }

        public Task<AdministrativeDongDioramaManifest?> FindManifestAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaManifest?>(null);

        public Task<AdministrativeDongDioramaTile?> FindTileAsync(
            string administrativeAreaStableId,
            string tileStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaTile?>(null);

        public Task<AdministrativeDongDisplayOverlayResponse?> FindDisplayOverlaysAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDisplayOverlayResponse?>(null);
    }
}
