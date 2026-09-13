using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Options;

namespace Ssalddel.Services.WorldProjection.RegionMobilityGraph;

public interface I지역이동망GraphSource
{
    Task<RegionMobilityGraphManifest?> FindManifestAsync(
        string regionStableId,
        CancellationToken cancellationToken);

    Task<RegionMobilityGraphTile?> FindTileAsync(
        string regionStableId,
        string tileStableId,
        CancellationToken cancellationToken);
}

/// <summary>
/// 동결 OSM으로 만든 이동망 검토 후보를 개발 환경에서만 읽습니다.
/// 이 원천은 경로를 계산하거나 연결선을 추정하지 않으며, 검토 후보의 모든 실행 권위를 false로 강제합니다.
/// </summary>
public sealed class LocalPrivate지역이동망GraphSource(
    IOptions<RegionMobilityGraphOptions> options,
    IHostEnvironment environment) : I지역이동망GraphSource
{
    private const string ManifestFileName = "manifest.json";

    public async Task<RegionMobilityGraphManifest?> FindManifestAsync(
        string regionStableId,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return null;
        var root = ResolveConfiguredRoot();
        if (root is null) return null;
        var path = ResolveChild(root, ManifestFileName);
        if (!File.Exists(path)) return null;

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var manifest = RegionMobilityGraphJsonIntegrity.Deserialize<RegionMobilityGraphManifest>(bytes);
        ValidateManifest(manifest, bytes, regionStableId);
        return manifest;
    }

    public async Task<RegionMobilityGraphTile?> FindTileAsync(
        string regionStableId,
        string tileStableId,
        CancellationToken cancellationToken)
    {
        var manifest = await FindManifestAsync(regionStableId, cancellationToken);
        if (manifest is null) return null;
        var summary = manifest.Tiles.SingleOrDefault(item => string.Equals(
            item.TileStableId,
            tileStableId,
            StringComparison.Ordinal));
        if (summary is null) return null;

        var root = ResolveConfiguredRoot()
            ?? throw new InvalidDataException("RegionMobilityGraphRootUnavailable");
        var path = ResolveChild(root, summary.FileName);
        if (!File.Exists(path))
            throw new InvalidDataException("RegionMobilityGraphTileFileMissing");
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (!HashEquals(summary.FileHashSha256, SHA256.HashData(bytes)))
            throw new InvalidDataException("RegionMobilityGraphTileFileHashMismatch");

        var tile = RegionMobilityGraphJsonIntegrity.Deserialize<RegionMobilityGraphTile>(bytes);
        ValidateTile(manifest, summary, tile, bytes);
        return tile;
    }

    private string? ResolveConfiguredRoot()
    {
        var configured = options.Value.LocalPrivatePreviewRoot?.Trim();
        if (string.IsNullOrWhiteSpace(configured)) return null;
        return Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured));
    }

    private static string ResolveChild(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\')
            || !string.Equals(Path.GetExtension(relativePath), ".json", StringComparison.OrdinalIgnoreCase)
            || relativePath.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new InvalidDataException("RegionMobilityGraphRelativePathInvalid");
        }

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(fullRoot, comparison))
            throw new InvalidDataException("RegionMobilityGraphPathEscapesRoot");
        return fullPath;
    }

    private static void ValidateManifest(
        RegionMobilityGraphManifest manifest,
        byte[] rawBytes,
        string requestedRegionStableId)
    {
        if (!RegionMobilityGraphPolicy.IsRegionStableId(requestedRegionStableId)
            || !string.Equals(manifest.SchemaVersion, RegionMobilityGraphPolicy.ManifestSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(manifest.RegionStableId, requestedRegionStableId.Trim(), StringComparison.Ordinal)
            || !string.Equals(manifest.RegionStableId, RegionMobilityGraphPolicy.FirstRegionStableId, StringComparison.Ordinal)
            || !string.Equals(manifest.GraphStableId, RegionMobilityGraphPolicy.FirstGraphStableId, StringComparison.Ordinal)
            || !string.Equals(manifest.CoverageCode, RegionMobilityGraphPolicy.SagajeongOneKilometerWindow, StringComparison.Ordinal)
            || !string.Equals(manifest.ReadinessCode, RegionMobilityGraphPolicy.PendingHumanReview, StringComparison.Ordinal)
            || manifest.AdministrativeBoundaryClipped
            || manifest.DistributionApproved
            || manifest.TraversalReady
            || manifest.RuntimeAuthorized
            || string.IsNullOrWhiteSpace(manifest.Revision)
            || !IsSha256(manifest.ProjectionHashSha256)
            || !IsSha256(manifest.ContentHashSha256)
            || !string.Equals(
                manifest.ContentHashSha256,
                RegionMobilityGraphJsonIntegrity.ComputeContentHash(rawBytes),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("RegionMobilityGraphManifestPolicyInvalid");
        }

        var administrativeAreas = manifest.AdministrativeAreaStableIds
            ?? throw new InvalidDataException("RegionMobilityGraphAdministrativeAreasMissing");
        if (administrativeAreas.Length == 0
            || administrativeAreas.Any(area => !AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(area))
            || administrativeAreas.Distinct(StringComparer.Ordinal).Count() != administrativeAreas.Length)
            throw new InvalidDataException("RegionMobilityGraphAdministrativeAreasInvalid");

        var provenance = manifest.Provenance
            ?? throw new InvalidDataException("RegionMobilityGraphProvenanceMissing");
        if (string.IsNullOrWhiteSpace(provenance.SourceId)
            || string.IsNullOrWhiteSpace(provenance.DatasetId)
            || string.IsNullOrWhiteSpace(provenance.SourceVersion)
            || string.IsNullOrWhiteSpace(provenance.SourceUrl)
            || string.IsNullOrWhiteSpace(provenance.LicenseCode)
            || string.IsNullOrWhiteSpace(provenance.Attribution)
            || string.IsNullOrWhiteSpace(provenance.CoordinateMethod)
            || !IsSha256(provenance.RawContentHashSha256))
            throw new InvalidDataException("RegionMobilityGraphProvenanceInvalid");

        var coordinates = manifest.Coordinates
            ?? throw new InvalidDataException("RegionMobilityGraphCoordinatesMissing");
        if (!string.Equals(coordinates.Unit, "m", StringComparison.Ordinal)
            || !string.Equals(coordinates.AxisOrder, "EastingNorthing", StringComparison.Ordinal)
            || coordinates.TileSizeMeters != RegionMobilityGraphPolicy.TileSizeMeters
            || coordinates.WorldOffsetX != RegionMobilityGraphPolicy.FirstWorldOffsetX
            || coordinates.WorldOffsetZ != RegionMobilityGraphPolicy.FirstWorldOffsetZ
            || coordinates.MetersPerUnit != RegionMobilityGraphPolicy.FirstMetersPerUnit
            || !EqualBounds(coordinates.Bounds,
                [
                    RegionMobilityGraphPolicy.FirstWorldOffsetX - RegionMobilityGraphPolicy.TileSizeMeters,
                    RegionMobilityGraphPolicy.FirstWorldOffsetZ - RegionMobilityGraphPolicy.TileSizeMeters,
                    RegionMobilityGraphPolicy.FirstWorldOffsetX + RegionMobilityGraphPolicy.TileSizeMeters,
                    RegionMobilityGraphPolicy.FirstWorldOffsetZ + RegionMobilityGraphPolicy.TileSizeMeters
                ]))
            throw new InvalidDataException("RegionMobilityGraphCoordinatesInvalid");

        var summaries = manifest.Tiles
            ?? throw new InvalidDataException("RegionMobilityGraphTilesMissing");
        if (summaries.Length != 4
            || summaries.Any(item => item is null)
            || summaries.GroupBy(item => item.TileStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || summaries.GroupBy(item => item.FileName, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || summaries.GroupBy(item => (item.TileIndexX, item.TileIndexZ)).Any(group => group.Count() != 1)
            || summaries.Any(item => item.TileIndexX is < 0 or > 1
                                     || item.TileIndexZ is < 0 or > 1
                                     || !EqualBounds(item.Bounds, ExpectedTileBounds(item.TileIndexX, item.TileIndexZ))))
            throw new InvalidDataException("RegionMobilityGraphTileSummariesInvalid");
        foreach (var summary in summaries)
        {
            if (string.IsNullOrWhiteSpace(summary.TileStableId)
                || summary.NodeCount < 0
                || summary.EdgeCount < 0
                || summary.PortalCount < 0
                || !ValidBounds(summary.Bounds)
                || !IsSha256(summary.ContentHashSha256)
                || !IsSha256(summary.FileHashSha256))
                throw new InvalidDataException("RegionMobilityGraphTileSummaryInvalid");
            _ = ResolveChild(Path.GetPathRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory, summary.FileName);
        }

        var stitches = manifest.Stitches
            ?? throw new InvalidDataException("RegionMobilityGraphStitchesMissing");
        if (stitches.Any(item => item is null)
            || stitches.GroupBy(item => item.StitchStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || stitches.Any(item => item.RuntimeAuthorized
                         || string.IsNullOrWhiteSpace(item.StitchStableId)
                         || item.Portals is null
                         || !ValidStitchShape(item)
                         || item.Portals.GroupBy(
                                 portal => (portal.TileStableId, portal.NodeStableId))
                             .Any(group => group.Count() != 1)
                         || item.Portals.Any(portal => string.IsNullOrWhiteSpace(portal.TileStableId)
                                                       || string.IsNullOrWhiteSpace(portal.NodeStableId)
                                                       || summaries.All(summary => !string.Equals(
                                                           summary.TileStableId,
                                                           portal.TileStableId,
                                                           StringComparison.Ordinal)))))
            throw new InvalidDataException("RegionMobilityGraphStitchPolicyInvalid");
        if ((manifest.VerticalSliceCandidates
             ?? throw new InvalidDataException("RegionMobilityGraphVerticalSliceCandidatesMissing"))
            .Any(item => item is null
                         || item.RuntimeAuthorized
                         || item.ConnectorCandidates is null
                         || item.ConnectorCandidates.Any(connector => connector is null
                                                                       || connector.RuntimeAuthorized
                                                                       || connector.GeneratedAsGraphEdge
                                                                       || !string.Equals(
                                                                           connector.ReviewStatusCode,
                                                                           RegionMobilityGraphPolicy.PendingHumanReview,
                                                                           StringComparison.Ordinal))))
            throw new InvalidDataException("RegionMobilityGraphConnectorPolicyInvalid");

        var quality = manifest.Quality
            ?? throw new InvalidDataException("RegionMobilityGraphQualityMissing");
        if (!string.Equals(quality.AccessReviewCode, RegionMobilityGraphPolicy.PendingHumanReview, StringComparison.Ordinal)
            || quality.NodeCount != summaries.Sum(item => item.NodeCount)
            || quality.EdgeCount != summaries.Sum(item => item.EdgeCount))
            throw new InvalidDataException("RegionMobilityGraphQualityInvalid");
    }

    private static void ValidateTile(
        RegionMobilityGraphManifest manifest,
        RegionMobilityGraphTileSummary summary,
        RegionMobilityGraphTile tile,
        byte[] rawBytes)
    {
        if (!string.Equals(tile.SchemaVersion, RegionMobilityGraphPolicy.TileSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(tile.GraphStableId, manifest.GraphStableId, StringComparison.Ordinal)
            || !string.Equals(tile.RegionStableId, manifest.RegionStableId, StringComparison.Ordinal)
            || !string.Equals(tile.Revision, manifest.Revision, StringComparison.Ordinal)
            || !string.Equals(tile.CoverageCode, manifest.CoverageCode, StringComparison.Ordinal)
            || !string.Equals(tile.TileStableId, summary.TileStableId, StringComparison.Ordinal)
            || tile.TileIndexX != summary.TileIndexX
            || tile.TileIndexZ != summary.TileIndexZ
            || !EqualBounds(tile.Bounds, summary.Bounds)
            || tile.AdministrativeBoundaryClipped
            || tile.DistributionApproved
            || tile.TraversalReady
            || tile.RuntimeAuthorized
            || !string.Equals(tile.SourceRawContentHashSha256, manifest.Provenance.RawContentHashSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(tile.ContentHashSha256, summary.ContentHashSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                tile.ContentHashSha256,
                RegionMobilityGraphJsonIntegrity.ComputeContentHash(rawBytes),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RegionMobilityGraphTilePolicyInvalid");

        var nodes = tile.Nodes ?? throw new InvalidDataException("RegionMobilityGraphNodesMissing");
        var edges = tile.Edges ?? throw new InvalidDataException("RegionMobilityGraphEdgesMissing");
        if (nodes.Length != summary.NodeCount
            || edges.Length != summary.EdgeCount
            || nodes.Any(item => item is null)
            || edges.Any(item => item is null)
            || nodes.Count(item => item.IsPortal) != summary.PortalCount
            || nodes.GroupBy(item => item.NodeStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || edges.GroupBy(item => item.EdgeStableId, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidDataException("RegionMobilityGraphTileCountsInvalid");
        var nodesById = nodes.ToDictionary(item => item.NodeStableId, StringComparer.Ordinal);
        if (nodes.Any(item => string.IsNullOrWhiteSpace(item.NodeStableId)
                              || item.RuntimeAuthorized
                              || item.Position is null
                              || !ValidPoint(item.Position)
                              || !Inside(item.Position, summary.Bounds)
                              || item.RoleCodes is null
                              || item.SourceTags is null
                              || item.SourceBuildingOsmWayIds is null)
            || edges.Any(item => string.IsNullOrWhiteSpace(item.EdgeStableId)
                                 || item.RuntimeAuthorized
                                 || !nodesById.ContainsKey(item.FromNodeStableId)
                                 || !nodesById.ContainsKey(item.ToNodeStableId)
                                 || item.Geometry is null
                                 || item.Geometry.Length < 2
                                 || item.Geometry.Any(point => point is null || !ValidPoint(point) || !Inside(point, summary.Bounds))
                                 || item.LengthMeters <= 0d
                                 || item.CandidateModeCodes is null
                                 || item.CandidateModeCodes.Any(mode => !ValidMode(mode))
                                 || item.CandidateModeCodes.Distinct(StringComparer.Ordinal).Count() != item.CandidateModeCodes.Length
                                 || !ValidAccessReview(item.AccessReviewCode)
                                 || item.SourceTags is null))
            throw new InvalidDataException("RegionMobilityGraphTileTopologyInvalid");

        foreach (var edge in edges)
        {
            if (!SamePoint(edge.Geometry[0], nodesById[edge.FromNodeStableId].Position)
                || !SamePoint(edge.Geometry[^1], nodesById[edge.ToNodeStableId].Position))
                throw new InvalidDataException("RegionMobilityGraphEdgeEndpointMismatch");
            var measured = 0d;
            for (var index = 1; index < edge.Geometry.Length; index++)
            {
                var dx = edge.Geometry[index].X - edge.Geometry[index - 1].X;
                var dz = edge.Geometry[index].Z - edge.Geometry[index - 1].Z;
                var length = Math.Sqrt(dx * dx + dz * dz);
                if (!double.IsFinite(length) || length <= 0d)
                    throw new InvalidDataException("RegionMobilityGraphEdgeGeometryInvalid");
                measured += length;
            }
            if (Math.Abs(measured - edge.LengthMeters) > 0.001d)
                throw new InvalidDataException("RegionMobilityGraphEdgeLengthMismatch");
        }

        var expectedPortals = (manifest.Stitches ?? [])
            .SelectMany(stitch => stitch.Portals
                .Where(portal => string.Equals(portal.TileStableId, tile.TileStableId, StringComparison.Ordinal))
                .Select(portal => (stitch.StitchStableId, portal.NodeStableId)))
            .ToHashSet();
        var actualPortals = nodes
            .Where(node => node.IsPortal)
            .Select(node => (node.StitchStableId, node.NodeStableId))
            .ToHashSet();
        if (!expectedPortals.SetEquals(actualPortals))
            throw new InvalidDataException("RegionMobilityGraphPortalReferenceMismatch");
    }

    private static bool ValidAccessReview(string value)
        => string.Equals(value, RegionMobilityGraphPolicy.PendingHumanReview, StringComparison.Ordinal)
           || string.Equals(value, RegionMobilityGraphPolicy.SourceExplicitlyRestricted, StringComparison.Ordinal);

    private static bool ValidStitchShape(RegionMobilityGraphStitch value)
        => string.Equals(value.KindCode, RegionMobilityGraphStitchKindCodes.TileStitch, StringComparison.Ordinal)
            ? value.Portals.Length >= 2
            : string.Equals(value.KindCode, RegionMobilityGraphStitchKindCodes.OpenBoundaryPortal, StringComparison.Ordinal)
              && value.Portals.Length == 1;

    private static bool ValidMode(string value)
        => string.Equals(value, RegionMobilityGraphModeCodes.Vehicle, StringComparison.Ordinal)
           || string.Equals(value, RegionMobilityGraphModeCodes.Pedestrian, StringComparison.Ordinal)
           || string.Equals(value, RegionMobilityGraphModeCodes.Motorcycle, StringComparison.Ordinal);

    private static bool ValidBounds(double[]? bounds)
        => bounds is { Length: 4 }
           && bounds.All(double.IsFinite)
           && bounds[0] <= bounds[2]
           && bounds[1] <= bounds[3];

    private static double[] ExpectedTileBounds(int indexX, int indexZ)
        =>
        [
            RegionMobilityGraphPolicy.FirstWorldOffsetX - RegionMobilityGraphPolicy.TileSizeMeters
                + indexX * RegionMobilityGraphPolicy.TileSizeMeters,
            RegionMobilityGraphPolicy.FirstWorldOffsetZ - RegionMobilityGraphPolicy.TileSizeMeters
                + indexZ * RegionMobilityGraphPolicy.TileSizeMeters,
            RegionMobilityGraphPolicy.FirstWorldOffsetX
                + indexX * RegionMobilityGraphPolicy.TileSizeMeters,
            RegionMobilityGraphPolicy.FirstWorldOffsetZ
                + indexZ * RegionMobilityGraphPolicy.TileSizeMeters
        ];

    private static bool ValidPoint(RegionMobilityGraphPoint point)
        => double.IsFinite(point.X) && double.IsFinite(point.Z);

    private static bool Inside(RegionMobilityGraphPoint point, double[] bounds)
        => point.X >= bounds[0] - 0.000001d
           && point.X <= bounds[2] + 0.000001d
           && point.Z >= bounds[1] - 0.000001d
           && point.Z <= bounds[3] + 0.000001d;

    private static bool SamePoint(RegionMobilityGraphPoint first, RegionMobilityGraphPoint second)
        => Math.Abs(first.X - second.X) <= 0.000001d
           && Math.Abs(first.Z - second.Z) <= 0.000001d;

    private static bool EqualBounds(double[]? first, double[]? second)
        => ValidBounds(first)
           && ValidBounds(second)
           && first!.SequenceEqual(second!);

    private static bool IsSha256(string? value)
        => value is { Length: 64 }
           && value.All(character => char.IsAsciiHexDigit(character));

    private static bool HashEquals(string expectedHex, byte[] actual)
        => string.Equals(expectedHex, Convert.ToHexString(actual), StringComparison.OrdinalIgnoreCase);
}

internal static class RegionMobilityGraphJsonIntegrity
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static T Deserialize<T>(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 128
            });
            EnsureNoDuplicateProperties(document.RootElement);
            return JsonSerializer.Deserialize<T>(bytes, SerializerOptions)
                   ?? throw new InvalidDataException("RegionMobilityGraphJsonNull");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RegionMobilityGraphJsonInvalid", exception);
        }
    }

    internal static string ComputeContentHash(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 128
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("RegionMobilityGraphJsonRootInvalid");
            EnsureNoDuplicateProperties(document.RootElement);

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                       Indented = false,
                       SkipValidation = false
                   }))
            {
                WriteCanonical(writer, document.RootElement, blankRootContentHash: true);
            }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(buffer.WrittenSpan);
            hash.AppendData(NewLine);
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RegionMobilityGraphJsonInvalid", exception);
        }
    }

    internal static byte[] Canonicalize(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        EnsureNoDuplicateProperties(document.RootElement);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                   Indented = false,
                   SkipValidation = false
               }))
        {
            WriteCanonical(writer, document.RootElement, blankRootContentHash: false);
        }
        var result = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(result);
        result[^1] = (byte)'\n';
        return result;
    }

    private static void WriteCanonical(
        Utf8JsonWriter writer,
        JsonElement element,
        bool blankRootContentHash = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    if (blankRootContentHash
                        && string.Equals(property.Name, "contentHashSha256", StringComparison.Ordinal))
                        writer.WriteStringValue(string.Empty);
                    else
                        WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException("RegionMobilityGraphJsonValueInvalid");
        }
    }

    private static void EnsureNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException("RegionMobilityGraphDuplicateJsonProperty");
                EnsureNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) EnsureNoDuplicateProperties(item);
        }
    }
}
