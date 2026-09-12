using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

public sealed class 행정동디오라마BuildingInput
{
    public string BuildingStableId { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string EvidenceKindCode { get; set; } = string.Empty;
    public int? AboveGroundFloorCount { get; set; }
    public double? HeightMeters { get; set; }
    public double BuildingAreaSquareMeters { get; set; }
    public double TotalFloorAreaSquareMeters { get; set; }
    public AdministrativeDongDioramaPoint[] Footprint { get; set; } = [];
}

public sealed class 행정동디오라마RoadInput
{
    public string RoadStableId { get; set; } = string.Empty;
    public AdministrativeDongDioramaPoint From { get; set; } = new();
    public AdministrativeDongDioramaPoint To { get; set; } = new();
    public string EvidenceKindCode { get; set; } = string.Empty;
}

public sealed class 행정동디오라마PublicBusinessInput
{
    public string BusinessStableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string SemanticPlaceStableId { get; set; } = string.Empty;
    public AdministrativeDongDioramaPoint Position { get; set; } = new();
    public bool PresentationApproved { get; set; }
}

public sealed class 행정동디오라마ProjectionBuildInput
{
    public string AdministrativeAreaStableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SourceVintage { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public AdministrativeDongDioramaCoordinateFrame CoordinateFrame { get; set; } = new();
    public AdministrativeDongDioramaPoint[] Boundary { get; set; } = [];
    public string[] LegalAreaStableIds { get; set; } = [];
    public string[] OperationalAreaStableIds { get; set; } = [];
    public string[] SemanticPlaceStableIds { get; set; } = [];
    public AdministrativeDongDioramaSourceAttribution[] Sources { get; set; } = [];
    public 행정동디오라마BuildingInput[] Buildings { get; set; } = [];
    public 행정동디오라마RoadInput[] Roads { get; set; } = [];
    public 행정동디오라마PublicBusinessInput[] PublicBusinesses { get; set; } = [];
    public int UnresolvedBuildingCount { get; set; }
}

public sealed record 행정동디오라마ProjectionBuildResult(
    AdministrativeDongDioramaManifest Manifest,
    AdministrativeDongDioramaTile[] Tiles,
    AdministrativeDongDisplayOverlayResponse DisplayOverlays);

/// <summary>
/// 검토된 행정동 경계와 기존 법정동 배치 자료를 Unity가 읽을 수 있는 불변 사본으로 바꿉니다.
/// 업무 상태를 만들거나 운영 객체의 행정동을 추측하지 않습니다.
/// </summary>
public static class 행정동디오라마ProjectionBuilder
{
    private const double Epsilon = 0.000001d;

    public static 행정동디오라마ProjectionBuildResult Build(행정동디오라마ProjectionBuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(input.AdministrativeAreaStableId))
            throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(input));
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            throw new ArgumentException("AdministrativeDongDisplayNameRequired", nameof(input));
        if (string.IsNullOrWhiteSpace(input.SourceVintage))
            throw new ArgumentException("AdministrativeDongSourceVintageRequired", nameof(input));
        if (input.CoordinateFrame.MetersPerUnit <= 0)
            throw new ArgumentException("AdministrativeDongCoordinateScaleInvalid", nameof(input));

        var boundary = NormalizePolygon(input.Boundary ?? []);
        if (boundary.Length < 3)
            return WaitingForBoundary(input);

        ValidateStableIds(input.Buildings.Select(x => x.BuildingStableId), "BuildingStableIdInvalid");
        ValidateStableIds(input.Roads.Select(x => x.RoadStableId), "RoadStableIdInvalid");
        ValidateStableIds(input.PublicBusinesses.Select(x => x.BusinessStableId), "BusinessStableIdInvalid");

        var acceptedBuildings = (input.Buildings ?? [])
            .Where(building => building.Footprint is { Length: >= 3 })
            .Select(building => (Building: building, Point: PointOnSurface(building.Footprint)))
            .Where(item => PointInPolygon(item.Point, boundary))
            .OrderBy(item => item.Building.BuildingStableId, StringComparer.Ordinal)
            .ToArray();
        var acceptedRoads = (input.Roads ?? [])
            .OrderBy(road => road.RoadStableId, StringComparer.Ordinal)
            .SelectMany(road => ClipRoad(road, boundary))
            .ToArray();
        var publicMarkers = (input.PublicBusinesses ?? [])
            .Where(item => item.PresentationApproved && PointInPolygon(item.Position, boundary))
            .OrderBy(item => item.BusinessStableId, StringComparer.Ordinal)
            .ToArray();

        var bounds = Bounds(boundary);
        var tileKeys = acceptedBuildings.Select(item => Tile(item.Point, input.CoordinateFrame))
            .Concat(acceptedRoads.Select(item => Tile(Midpoint(item.From, item.To), input.CoordinateFrame)))
            .Distinct()
            .OrderBy(item => item.X)
            .ThenBy(item => item.Z)
            .ToArray();
        var tiles = tileKeys.Select(key => BuildTile(input, key, acceptedBuildings, acceptedRoads)).ToArray();

        var manifest = new AdministrativeDongDioramaManifest
        {
            AdministrativeAreaStableId = input.AdministrativeAreaStableId.Trim(),
            DisplayName = input.DisplayName.Trim(),
            SourceVintage = input.SourceVintage.Trim(),
            ReadinessCode = tiles.Length == 0
                ? AdministrativeDongDioramaReadinessCodes.WaitingForSpatialLinkage
                : AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
            GeneratedAtUtc = input.GeneratedAtUtc,
            LegalAreaStableIds = DistinctSorted(input.LegalAreaStableIds),
            OperationalAreaStableIds = DistinctSorted(input.OperationalAreaStableIds),
            SemanticPlaceStableIds = DistinctSorted((input.SemanticPlaceStableIds ?? [])
                .Concat(publicMarkers.Select(item => item.SemanticPlaceStableId))),
            CoordinateFrame = Copy(input.CoordinateFrame),
            Bounds = bounds,
            Boundary = boundary,
            Tiles = tiles.Select(tile => new AdministrativeDongDioramaTileSummary
            {
                TileStableId = tile.TileStableId,
                TileIndexX = tile.TileIndexX,
                TileIndexZ = tile.TileIndexZ,
                TileHashSha256 = tile.TileHashSha256,
                BuildingCount = tile.Buildings.Length,
                RoadSegmentCount = tile.Roads.Length
            }).ToArray(),
            BuildingSummaries = acceptedBuildings
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Building.CategoryCode)
                    ? "unresolved"
                    : item.Building.CategoryCode.Trim(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new AdministrativeDongDioramaBuildingSummary
                {
                    CategoryCode = group.Key,
                    BuildingCount = group.Count(),
                    BuildingAreaSquareMeters = group.Sum(item => item.Building.BuildingAreaSquareMeters),
                    TotalFloorAreaSquareMeters = group.Sum(item => item.Building.TotalFloorAreaSquareMeters)
                }).ToArray(),
            UnresolvedBuildingCount = Math.Max(0, input.UnresolvedBuildingCount),
            PublicBusinessMarkerCount = publicMarkers.Length,
            Sources = (input.Sources ?? [])
                .OrderBy(item => item.SourceId, StringComparer.Ordinal)
                .ThenBy(item => item.DatasetId, StringComparer.Ordinal)
                .ThenBy(item => item.SourceRevision, StringComparer.Ordinal)
                .ToArray()
        };
        manifest.ProjectionHashSha256 = Hash(ManifestCanonical(manifest));
        foreach (var tile in tiles) tile.ProjectionHashSha256 = manifest.ProjectionHashSha256;

        var overlays = new AdministrativeDongDisplayOverlayResponse
        {
            AdministrativeAreaStableId = manifest.AdministrativeAreaStableId,
            AsOfUtc = input.GeneratedAtUtc,
            Items = publicMarkers.Select(item => new AdministrativeDongDisplayOverlay
            {
                OverlayStableId = "public-business:" + item.BusinessStableId,
                OverlayKindCode = AdministrativeDongDisplayOverlayKinds.PublicBusiness,
                DisplayLabel = item.DisplayName,
                CategoryCode = item.CategoryCode,
                SemanticPlaceStableId = item.SemanticPlaceStableId,
                AdvertisementDisclosureRequired = false
            }).ToArray()
        };
        overlays.OverlayRevision = Hash(string.Join("\n", overlays.Items.Select(OverlayCanonical)));
        return new(manifest, tiles, overlays);
    }

    private static 행정동디오라마ProjectionBuildResult WaitingForBoundary(행정동디오라마ProjectionBuildInput input)
    {
        var manifest = new AdministrativeDongDioramaManifest
        {
            AdministrativeAreaStableId = input.AdministrativeAreaStableId.Trim(),
            DisplayName = input.DisplayName.Trim(),
            SourceVintage = input.SourceVintage.Trim(),
            ReadinessCode = AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary,
            GeneratedAtUtc = input.GeneratedAtUtc,
            LegalAreaStableIds = DistinctSorted(input.LegalAreaStableIds),
            OperationalAreaStableIds = DistinctSorted(input.OperationalAreaStableIds),
            SemanticPlaceStableIds = DistinctSorted(input.SemanticPlaceStableIds),
            CoordinateFrame = Copy(input.CoordinateFrame),
            UnresolvedBuildingCount = Math.Max(0, input.UnresolvedBuildingCount),
            Sources = (input.Sources ?? []).OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray()
        };
        manifest.ProjectionHashSha256 = Hash(ManifestCanonical(manifest));
        return new(manifest, [], new AdministrativeDongDisplayOverlayResponse
        {
            AdministrativeAreaStableId = manifest.AdministrativeAreaStableId,
            AsOfUtc = input.GeneratedAtUtc,
            OverlayRevision = Hash(string.Empty)
        });
    }

    private static AdministrativeDongDioramaTile BuildTile(
        행정동디오라마ProjectionBuildInput input,
        (int X, int Z) key,
        IReadOnlyList<(행정동디오라마BuildingInput Building, AdministrativeDongDioramaPoint Point)> buildings,
        IReadOnlyList<AdministrativeDongDioramaRoadSegment> roads)
    {
        var tileBounds = TileBounds(key, input.CoordinateFrame);
        var tileBuildings = buildings.Where(item => Tile(item.Point, input.CoordinateFrame) == key)
            .Select(item => new AdministrativeDongDioramaBuilding
            {
                BuildingStableId = item.Building.BuildingStableId,
                CategoryCode = item.Building.CategoryCode,
                EvidenceKindCode = item.Building.EvidenceKindCode,
                AboveGroundFloorCount = item.Building.AboveGroundFloorCount,
                HeightMeters = item.Building.HeightMeters,
                Footprint = NormalizePolygon(item.Building.Footprint)
            }).ToArray();
        var tileRoads = roads.Where(item => Tile(Midpoint(item.From, item.To), input.CoordinateFrame) == key)
            .OrderBy(item => item.RoadStableId, StringComparer.Ordinal)
            .ToArray();
        var tile = new AdministrativeDongDioramaTile
        {
            AdministrativeAreaStableId = input.AdministrativeAreaStableId.Trim(),
            TileStableId = $"tile:{input.AdministrativeAreaStableId.Trim()}:{AdministrativeDongDioramaPolicy.TileSizeMeters}m:x{key.X}:z{key.Z}",
            TileIndexX = key.X,
            TileIndexZ = key.Z,
            Bounds = tileBounds,
            Buildings = tileBuildings,
            Roads = tileRoads
        };
        tile.TileHashSha256 = Hash(TileCanonical(tile));
        return tile;
    }

    private static IEnumerable<AdministrativeDongDioramaRoadSegment> ClipRoad(
        행정동디오라마RoadInput road,
        IReadOnlyList<AdministrativeDongDioramaPoint> boundary)
    {
        var parameters = new List<double> { 0d, 1d };
        for (var index = 0; index < boundary.Count; index++)
        {
            var a = boundary[index];
            var b = boundary[(index + 1) % boundary.Count];
            if (TryIntersectionParameter(road.From, road.To, a, b, out var value))
                parameters.Add(Math.Clamp(value, 0d, 1d));
        }
        var ordered = parameters.OrderBy(value => value)
            .Aggregate(new List<double>(), (values, value) =>
            {
                if (values.Count == 0 || Math.Abs(values[^1] - value) > Epsilon) values.Add(value);
                return values;
            });
        var part = 0;
        for (var index = 0; index < ordered.Count - 1; index++)
        {
            var start = ordered[index];
            var end = ordered[index + 1];
            if (end - start <= Epsilon) continue;
            if (!PointInPolygon(PointAt(road.From, road.To, (start + end) / 2d), boundary)) continue;
            yield return new AdministrativeDongDioramaRoadSegment
            {
                RoadStableId = road.RoadStableId + ":part:" + part++,
                From = PointAt(road.From, road.To, start),
                To = PointAt(road.From, road.To, end),
                EvidenceKindCode = road.EvidenceKindCode
            };
        }
    }

    private static bool TryIntersectionParameter(
        AdministrativeDongDioramaPoint p,
        AdministrativeDongDioramaPoint p2,
        AdministrativeDongDioramaPoint q,
        AdministrativeDongDioramaPoint q2,
        out double t)
    {
        var rx = p2.X - p.X;
        var rz = p2.Z - p.Z;
        var sx = q2.X - q.X;
        var sz = q2.Z - q.Z;
        var cross = rx * sz - rz * sx;
        if (Math.Abs(cross) <= Epsilon) { t = 0; return false; }
        var qpx = q.X - p.X;
        var qpz = q.Z - p.Z;
        t = (qpx * sz - qpz * sx) / cross;
        var u = (qpx * rz - qpz * rx) / cross;
        return t >= -Epsilon && t <= 1d + Epsilon && u >= -Epsilon && u <= 1d + Epsilon;
    }

    public static bool PointInPolygon(
        AdministrativeDongDioramaPoint point,
        IReadOnlyList<AdministrativeDongDioramaPoint> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = i == 0 ? polygon.Count - 1 : i - 1;
            if (PointOnSegment(point, polygon[j], polygon[i])) return true;
            var intersects = (polygon[i].Z > point.Z) != (polygon[j].Z > point.Z)
                && point.X < (polygon[j].X - polygon[i].X) * (point.Z - polygon[i].Z)
                / (polygon[j].Z - polygon[i].Z) + polygon[i].X;
            if (intersects) inside = !inside;
        }
        return inside;
    }

    private static bool PointOnSegment(AdministrativeDongDioramaPoint point,
        AdministrativeDongDioramaPoint a, AdministrativeDongDioramaPoint b)
    {
        var cross = (point.Z - a.Z) * (b.X - a.X) - (point.X - a.X) * (b.Z - a.Z);
        if (Math.Abs(cross) > Epsilon) return false;
        return point.X >= Math.Min(a.X, b.X) - Epsilon && point.X <= Math.Max(a.X, b.X) + Epsilon
            && point.Z >= Math.Min(a.Z, b.Z) - Epsilon && point.Z <= Math.Max(a.Z, b.Z) + Epsilon;
    }

    internal static AdministrativeDongDioramaPoint PointOnSurface(
        IReadOnlyList<AdministrativeDongDioramaPoint> footprint)
    {
        var polygon = NormalizePolygon(footprint.ToArray());
        var signedArea = 0d;
        var cx = 0d;
        var cz = 0d;
        for (var index = 0; index < polygon.Length; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Length];
            var cross = current.X * next.Z - next.X * current.Z;
            signedArea += cross;
            cx += (current.X + next.X) * cross;
            cz += (current.Z + next.Z) * cross;
        }
        if (Math.Abs(signedArea) > Epsilon)
        {
            var candidate = new AdministrativeDongDioramaPoint
            {
                X = cx / (3d * signedArea),
                Z = cz / (3d * signedArea)
            };
            if (PointInPolygon(candidate, polygon)) return candidate;
        }
        // 오목 다각형의 평균점이 바깥에 놓일 수 있으므로 각 꼭짓점 사이의 수평선을 훑어
        // 가장 긴 내부 구간의 중점을 선택한다.
        var scanLines = polygon.Select(point => point.Z).Distinct()
            .OrderBy(value => value).Zip(polygon.Select(point => point.Z).Distinct().OrderBy(value => value).Skip(1),
                (a, b) => (a + b) / 2d).ToArray();
        AdministrativeDongDioramaPoint? best = null;
        var bestWidth = -1d;
        foreach (var z in scanLines)
        {
            var intersections = new List<double>();
            for (var index = 0; index < polygon.Length; index++)
            {
                var a = polygon[index];
                var b = polygon[(index + 1) % polygon.Length];
                if ((a.Z > z) == (b.Z > z)) continue;
                intersections.Add(a.X + (b.X - a.X) * (z - a.Z) / (b.Z - a.Z));
            }
            intersections.Sort();
            for (var index = 0; index + 1 < intersections.Count; index += 2)
            {
                var width = intersections[index + 1] - intersections[index];
                if (width <= bestWidth) continue;
                bestWidth = width;
                best = new AdministrativeDongDioramaPoint { X = (intersections[index] + intersections[index + 1]) / 2d, Z = z };
            }
        }
        return best ?? new AdministrativeDongDioramaPoint { X = polygon[0].X, Z = polygon[0].Z };
    }

    private static AdministrativeDongDioramaPoint[] NormalizePolygon(AdministrativeDongDioramaPoint[] points)
    {
        if (points.Length > 1 && Same(points[0], points[^1])) points = points[..^1];
        return points.Select(Copy).ToArray();
    }

    private static AdministrativeDongDioramaBounds Bounds(IReadOnlyList<AdministrativeDongDioramaPoint> points)
        => new()
        {
            MinX = points.Min(point => point.X),
            MinZ = points.Min(point => point.Z),
            MaxX = points.Max(point => point.X),
            MaxZ = points.Max(point => point.Z)
        };

    private static (int X, int Z) Tile(AdministrativeDongDioramaPoint point,
        AdministrativeDongDioramaCoordinateFrame frame)
        => ((int)Math.Floor((point.X - frame.WorldOffsetX) / AdministrativeDongDioramaPolicy.TileSizeMeters),
            (int)Math.Floor((point.Z - frame.WorldOffsetZ) / AdministrativeDongDioramaPolicy.TileSizeMeters));

    private static AdministrativeDongDioramaBounds TileBounds((int X, int Z) key,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        var minX = frame.WorldOffsetX + key.X * AdministrativeDongDioramaPolicy.TileSizeMeters;
        var minZ = frame.WorldOffsetZ + key.Z * AdministrativeDongDioramaPolicy.TileSizeMeters;
        return new AdministrativeDongDioramaBounds
        {
            MinX = minX,
            MinZ = minZ,
            MaxX = minX + AdministrativeDongDioramaPolicy.TileSizeMeters,
            MaxZ = minZ + AdministrativeDongDioramaPolicy.TileSizeMeters
        };
    }

    private static AdministrativeDongDioramaPoint Midpoint(AdministrativeDongDioramaPoint a,
        AdministrativeDongDioramaPoint b) => new() { X = (a.X + b.X) / 2d, Z = (a.Z + b.Z) / 2d };
    private static AdministrativeDongDioramaPoint PointAt(AdministrativeDongDioramaPoint a,
        AdministrativeDongDioramaPoint b, double t) => new()
        { X = a.X + (b.X - a.X) * t, Z = a.Z + (b.Z - a.Z) * t };
    private static AdministrativeDongDioramaPoint Copy(AdministrativeDongDioramaPoint value)
        => new() { X = value.X, Z = value.Z };
    private static AdministrativeDongDioramaCoordinateFrame Copy(AdministrativeDongDioramaCoordinateFrame value)
        => new()
        {
            Method = value.Method,
            OriginLatitude = value.OriginLatitude,
            OriginLongitude = value.OriginLongitude,
            WorldOffsetX = value.WorldOffsetX,
            WorldOffsetZ = value.WorldOffsetZ,
            MetersPerUnit = value.MetersPerUnit
        };
    private static bool Same(AdministrativeDongDioramaPoint a, AdministrativeDongDioramaPoint b)
        => Math.Abs(a.X - b.X) <= Epsilon && Math.Abs(a.Z - b.Z) <= Epsilon;
    private static string[] DistinctSorted(IEnumerable<string>? values)
        => (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static void ValidateStableIds(IEnumerable<string>? values, string error)
    {
        if ((values ?? []).Any(string.IsNullOrWhiteSpace)) throw new ArgumentException(error);
    }

    private static string ManifestCanonical(AdministrativeDongDioramaManifest manifest)
        => string.Join("\n", new[]
        {
            manifest.SchemaVersion, manifest.AdministrativeAreaStableId, manifest.DisplayName, manifest.SourceVintage,
            manifest.ReadinessCode, Points(manifest.Boundary),
            string.Join("|", manifest.LegalAreaStableIds), string.Join("|", manifest.OperationalAreaStableIds),
            string.Join("|", manifest.SemanticPlaceStableIds),
            string.Join("|", manifest.Tiles.Select(tile => tile.TileStableId + ":" + tile.TileHashSha256)),
            string.Join("|", manifest.BuildingSummaries.Select(summary => string.Join(":", summary.CategoryCode,
                summary.BuildingCount.ToString(CultureInfo.InvariantCulture), Number(summary.BuildingAreaSquareMeters),
                Number(summary.TotalFloorAreaSquareMeters)))),
            manifest.UnresolvedBuildingCount.ToString(CultureInfo.InvariantCulture),
            manifest.PublicBusinessMarkerCount.ToString(CultureInfo.InvariantCulture),
            string.Join("|", manifest.Sources.Select(source => string.Join(":", source.SourceId, source.DatasetId,
                source.SourceRevision, source.ContentHashSha256, source.LicenseCode, source.LimitationCode)))
        });

    private static string TileCanonical(AdministrativeDongDioramaTile tile)
        => string.Join("\n", tile.AdministrativeAreaStableId, tile.TileStableId,
            string.Join("|", tile.Buildings.Select(building => string.Join(":", building.BuildingStableId,
                building.CategoryCode, building.EvidenceKindCode, building.AboveGroundFloorCount,
                building.HeightMeters.HasValue ? Number(building.HeightMeters.Value) : string.Empty,
                Points(building.Footprint)))),
            string.Join("|", tile.Roads.Select(road => string.Join(":", road.RoadStableId,
                Point(road.From), Point(road.To), road.EvidenceKindCode))));

    private static string OverlayCanonical(AdministrativeDongDisplayOverlay overlay)
        => string.Join("|", overlay.OverlayStableId, overlay.OverlayKindCode, overlay.DisplayLabel,
            overlay.CategoryCode, overlay.SemanticPlaceStableId, overlay.BadgeText, overlay.DetailCardText,
            overlay.AdvertisementDisclosureRequired, overlay.StartsAtUtc, overlay.EndsAtUtc);
    private static string Points(IEnumerable<AdministrativeDongDioramaPoint> points)
        => string.Join(";", points.Select(Point));
    private static string Point(AdministrativeDongDioramaPoint point) => Number(point.X) + "," + Number(point.Z);
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
