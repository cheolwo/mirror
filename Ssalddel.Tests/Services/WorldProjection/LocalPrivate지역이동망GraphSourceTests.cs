using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ssalddel.Services.WorldProjection.RegionMobilityGraph;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.WorldProjection;

public sealed class LocalPrivate지역이동망GraphSourceTests
{
    [Fact]
    public async Task 동결후보는_내용hash와파일hash를검증한뒤독립재조회한다()
    {
        var root = NewTemporaryDirectory();
        try
        {
            var fixture = WriteFixture(root);
            var source = Create(root, Environments.Development);

            var manifest = await source.FindManifestAsync(
                RegionMobilityGraphPolicy.FirstRegionStableId,
                CancellationToken.None);
            var tile = await source.FindTileAsync(
                RegionMobilityGraphPolicy.FirstRegionStableId,
                fixture.TileStableId,
                CancellationToken.None);

            Assert.NotNull(manifest);
            Assert.NotNull(tile);
            Assert.Equal(fixture.TileContentHash, tile.ContentHashSha256);
            Assert.False(manifest.RuntimeAuthorized);
            Assert.False(tile.RuntimeAuthorized);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Tile파일이동결뒤변하면_hash불일치로거절한다()
    {
        var root = NewTemporaryDirectory();
        try
        {
            var fixture = WriteFixture(root);
            await File.AppendAllTextAsync(fixture.TilePath, " ");
            var source = Create(root, Environments.Development);

            var error = await Assert.ThrowsAsync<InvalidDataException>(() => source.FindTileAsync(
                RegionMobilityGraphPolicy.FirstRegionStableId,
                fixture.TileStableId,
                CancellationToken.None));

            Assert.Equal("RegionMobilityGraphTileFileHashMismatch", error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task 운영환경은_경로가있어도검토후보를제공하지않는다()
    {
        var root = NewTemporaryDirectory();
        try
        {
            WriteFixture(root);
            var result = await Create(root, Environments.Production).FindManifestAsync(
                RegionMobilityGraphPolicy.FirstRegionStableId,
                CancellationToken.None);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static LocalPrivate지역이동망GraphSource Create(string root, string environmentName)
        => new(
            Options.Create(new RegionMobilityGraphOptions { LocalPrivatePreviewRoot = root }),
            new FakeEnvironment
            {
                EnvironmentName = environmentName,
                ApplicationName = "Ssalddel.Tests",
                ContentRootPath = AppContext.BaseDirectory,
                ContentRootFileProvider = new NullFileProvider()
            });

    private static FixtureFiles WriteFixture(string root)
    {
        var rawSourceHash = new string('A', 64);
        var summaries = new List<RegionMobilityGraphTileSummary>();
        string? selectedTileId = null;
        string? selectedTilePath = null;
        string? selectedTileContentHash = null;
        for (var indexX = 0; indexX <= 1; indexX++)
        {
            for (var indexZ = 0; indexZ <= 1; indexZ++)
            {
                var tileStableId = $"mobility-tile:sagajeong:x{indexX}:z{indexZ}.r1";
                var tileFileName = $"tiles/tile-x{indexX}-z{indexZ}.json";
                var bounds = new[]
                {
                    50d + indexX * 500d,
                    -492d + indexZ * 500d,
                    550d + indexX * 500d,
                    8d + indexZ * 500d
                };
                var tile = new RegionMobilityGraphTile
                {
                    GraphStableId = RegionMobilityGraphPolicy.FirstGraphStableId,
                    RegionStableId = RegionMobilityGraphPolicy.FirstRegionStableId,
                    Revision = "sagajeong-mobility-graph.test.r1",
                    CoverageCode = RegionMobilityGraphPolicy.SagajeongOneKilometerWindow,
                    TileStableId = tileStableId,
                    TileIndexX = indexX,
                    TileIndexZ = indexZ,
                    Bounds = bounds,
                    SourceRawContentHashSha256 = rawSourceHash,
                    Nodes =
                    [
                        new RegionMobilityGraphNode
                        {
                            NodeStableId = tileStableId + ":osm-node:1",
                            SourceOsmNodeId = "1",
                            Position = new RegionMobilityGraphPoint
                            {
                                X = bounds[0] + 100d,
                                Z = bounds[1] + 100d
                            },
                            RoleCodes = ["RoadEndpoint"],
                            SourceTags = [],
                            SourceBuildingOsmWayIds = []
                        }
                    ],
                    Edges = [],
                    DistributionApproved = false,
                    TraversalReady = false,
                    RuntimeAuthorized = false
                };
                var tileBytes = Seal(
                    tile,
                    value => value.ContentHashSha256,
                    (value, hash) => value.ContentHashSha256 = hash);
                var tilePath = Path.Combine(root, tileFileName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
                File.WriteAllBytes(tilePath, tileBytes);
                summaries.Add(new RegionMobilityGraphTileSummary
                {
                    TileStableId = tileStableId,
                    TileIndexX = indexX,
                    TileIndexZ = indexZ,
                    Bounds = bounds,
                    FileName = tileFileName,
                    ContentHashSha256 = tile.ContentHashSha256,
                    FileHashSha256 = Convert.ToHexString(SHA256.HashData(tileBytes)),
                    NodeCount = 1,
                    EdgeCount = 0,
                    PortalCount = 0
                });
                if (indexX == 0 && indexZ == 0)
                {
                    selectedTileId = tileStableId;
                    selectedTilePath = tilePath;
                    selectedTileContentHash = tile.ContentHashSha256;
                }
            }
        }

        var manifest = new RegionMobilityGraphManifest
        {
            GraphStableId = RegionMobilityGraphPolicy.FirstGraphStableId,
            RegionStableId = RegionMobilityGraphPolicy.FirstRegionStableId,
            AdministrativeAreaStableIds = [AdministrativeDongDioramaPolicy.FirstAdministrativeAreaStableId],
            Revision = "sagajeong-mobility-graph.test.r1",
            CoverageCode = RegionMobilityGraphPolicy.SagajeongOneKilometerWindow,
            ReadinessCode = RegionMobilityGraphPolicy.PendingHumanReview,
            ProjectionHashSha256 = new string('B', 64),
            Provenance = new RegionMobilityGraphProvenance
            {
                SourceId = "openstreetmap",
                DatasetId = "sagajeong-test",
                SourceVersion = "test.r1",
                SourceUrl = "https://www.openstreetmap.org",
                RawContentHashSha256 = rawSourceHash,
                LicenseCode = "ODbL-1.0",
                Attribution = "OpenStreetMap contributors",
                CoordinateMethod = "WGS84-ECEF-ENU-at-zero-altitude"
            },
            Coordinates = new RegionMobilityGraphCoordinates
            {
                Bounds = [50d, -492d, 1050d, 508d],
                WorldOffsetX = RegionMobilityGraphPolicy.FirstWorldOffsetX,
                WorldOffsetZ = RegionMobilityGraphPolicy.FirstWorldOffsetZ,
                MetersPerUnit = RegionMobilityGraphPolicy.FirstMetersPerUnit
            },
            Tiles = summaries.ToArray(),
            Stitches = [],
            VerticalSliceCandidates = [],
            Quality = new RegionMobilityGraphQuality
            {
                AccessReviewCode = RegionMobilityGraphPolicy.PendingHumanReview,
                NodeCount = 4,
                EdgeCount = 0
            },
            DistributionApproved = false,
            TraversalReady = false,
            RuntimeAuthorized = false
        };
        File.WriteAllBytes(
            Path.Combine(root, "manifest.json"),
            Seal(manifest, value => value.ContentHashSha256, (value, hash) => value.ContentHashSha256 = hash));
        return new FixtureFiles(selectedTileId!, selectedTilePath!, selectedTileContentHash!);
    }

    private static byte[] Seal<T>(
        T value,
        Func<T, string> readHash,
        Action<T, string> writeHash)
    {
        writeHash(value, string.Empty);
        var emptyHashJson = JsonSerializer.SerializeToUtf8Bytes(
            value,
            RegionMobilityGraphJsonIntegrity.SerializerOptions);
        writeHash(value, RegionMobilityGraphJsonIntegrity.ComputeContentHash(emptyHashJson));
        Assert.NotEmpty(readHash(value));
        return RegionMobilityGraphJsonIntegrity.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(
            value,
            RegionMobilityGraphJsonIntegrity.SerializerOptions));
    }

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ssalddel-region-mobility-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record FixtureFiles(string TileStableId, string TilePath, string TileContentHash);

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
