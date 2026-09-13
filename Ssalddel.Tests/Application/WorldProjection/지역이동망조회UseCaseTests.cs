using Ssalddel.Application.WorldProjection;
using Ssalddel.Services.WorldProjection.RegionMobilityGraph;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Application.WorldProjection;

public sealed class 지역이동망조회UseCaseTests
{
    [Fact]
    public async Task Manifest에없는Tile은_원천파일을읽지않고없음으로처리한다()
    {
        var source = new FakeSource();
        var useCase = new 지역이동망조회UseCase(source);

        var result = await useCase.TileAsync(
            RegionMobilityGraphPolicy.FirstRegionStableId,
            "mobility-tile:unknown",
            CancellationToken.None);

        Assert.Null(result);
        Assert.False(source.TileRead);
    }

    [Fact]
    public async Task 지역과Tile식별자를_문자열추측없이검증한다()
    {
        var useCase = new 지역이동망조회UseCase(new FakeSource());

        var region = await Assert.ThrowsAsync<ArgumentException>(() => useCase.ManifestAsync(
            "region:kr:hjd:1126057500",
            CancellationToken.None));
        var tile = await Assert.ThrowsAsync<ArgumentException>(() => useCase.TileAsync(
            RegionMobilityGraphPolicy.FirstRegionStableId,
            " ",
            CancellationToken.None));

        Assert.StartsWith("RegionMobilityGraphRegionStableIdInvalid", region.Message);
        Assert.StartsWith("RegionMobilityGraphTileStableIdRequired", tile.Message);
    }

    private sealed class FakeSource : I지역이동망GraphSource
    {
        public bool TileRead { get; private set; }

        public Task<RegionMobilityGraphManifest?> FindManifestAsync(
            string regionStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<RegionMobilityGraphManifest?>(new RegionMobilityGraphManifest
            {
                RegionStableId = regionStableId,
                Tiles =
                [
                    new RegionMobilityGraphTileSummary
                    {
                        TileStableId = "mobility-tile:sagajeong:x0:z0.r1"
                    }
                ]
            });

        public Task<RegionMobilityGraphTile?> FindTileAsync(
            string regionStableId,
            string tileStableId,
            CancellationToken cancellationToken)
        {
            TileRead = true;
            return Task.FromResult<RegionMobilityGraphTile?>(new RegionMobilityGraphTile
            {
                RegionStableId = regionStableId,
                TileStableId = tileStableId
            });
        }
    }
}
