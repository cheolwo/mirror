using Ssalddel.Services.WorldProjection.RegionMobilityGraph;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Application.WorldProjection;

public interface I지역이동망조회UseCase
{
    Task<RegionMobilityGraphManifest?> ManifestAsync(
        string regionStableId,
        CancellationToken cancellationToken);

    Task<RegionMobilityGraphTile?> TileAsync(
        string regionStableId,
        string tileStableId,
        CancellationToken cancellationToken);
}

public sealed class 지역이동망조회UseCase(I지역이동망GraphSource source) : I지역이동망조회UseCase
{
    public Task<RegionMobilityGraphManifest?> ManifestAsync(
        string regionStableId,
        CancellationToken cancellationToken)
        => source.FindManifestAsync(ValidateRegion(regionStableId), cancellationToken);

    public async Task<RegionMobilityGraphTile?> TileAsync(
        string regionStableId,
        string tileStableId,
        CancellationToken cancellationToken)
    {
        var region = ValidateRegion(regionStableId);
        if (string.IsNullOrWhiteSpace(tileStableId))
            throw new ArgumentException("RegionMobilityGraphTileStableIdRequired", nameof(tileStableId));
        var normalizedTile = tileStableId.Trim();
        var manifest = await source.FindManifestAsync(region, cancellationToken);
        if (manifest is null
            || !manifest.Tiles.Any(item => string.Equals(
                item.TileStableId,
                normalizedTile,
                StringComparison.Ordinal)))
            return null;
        return await source.FindTileAsync(region, normalizedTile, cancellationToken);
    }

    private static string ValidateRegion(string value)
    {
        if (!RegionMobilityGraphPolicy.IsRegionStableId(value))
            throw new ArgumentException("RegionMobilityGraphRegionStableIdInvalid", nameof(value));
        return value.Trim();
    }
}
