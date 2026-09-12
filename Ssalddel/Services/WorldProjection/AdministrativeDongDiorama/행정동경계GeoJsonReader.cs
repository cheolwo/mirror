using System.Text.Json;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

public sealed record 행정동경계GeoJsonSnapshot(
    string AdministrativeAreaStableId,
    string DisplayName,
    string SourceVintage,
    AdministrativeDongDioramaPoint[] Boundary,
    string GeometryTypeCode);

/// <summary>
/// 공식 출처에서 동결한 GeoJSON 중 행정동 하나를 기존 사가정 ENU 좌표계로 옮깁니다.
/// 속성 이름은 공급처별 차이를 허용하지만 10자리 행정동 코드가 정확히 일치해야 합니다.
/// </summary>
public static class 행정동경계GeoJsonReader
{
    private static readonly string[] CodeProperties =
        ["adm_cd", "ADM_CD", "hjd_cd", "HJD_CD", "행정동코드", "code"];
    private static readonly string[] NameProperties =
        ["adm_nm", "ADM_NM", "hjd_nm", "HJD_NM", "행정동명", "name"];

    public static 행정동경계GeoJsonSnapshot Read(
        ReadOnlySpan<byte> utf8Json,
        string administrativeAreaStableId,
        string sourceVintage,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(administrativeAreaStableId))
            throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(administrativeAreaStableId));
        if (string.IsNullOrWhiteSpace(sourceVintage))
            throw new ArgumentException("AdministrativeDongSourceVintageRequired", nameof(sourceVintage));
        using var document = JsonDocument.Parse(utf8Json.ToArray());
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var rootType)
            || rootType.GetString() != "FeatureCollection"
            || !root.TryGetProperty("features", out var features)
            || features.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("AdministrativeBoundaryGeoJsonFeatureCollectionRequired");

        var code = administrativeAreaStableId["region:kr:hjd:".Length..];
        var matches = features.EnumerateArray()
            .Where(feature => ReadProperty(feature, CodeProperties) == code)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException(matches.Length == 0
                ? "AdministrativeBoundaryFeatureMissing"
                : "AdministrativeBoundaryFeatureDuplicate");
        var feature = matches[0];
        var name = ReadProperty(feature, NameProperties);
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("AdministrativeBoundaryDisplayNameMissing");
        if (!feature.TryGetProperty("geometry", out var geometry)
            || !geometry.TryGetProperty("type", out var geometryType)
            || !geometry.TryGetProperty("coordinates", out var coordinates))
            throw new InvalidDataException("AdministrativeBoundaryGeometryMissing");

        var type = geometryType.GetString();
        var rings = type switch
        {
            "Polygon" => ReadPolygonRings(coordinates),
            "MultiPolygon" => coordinates.EnumerateArray().SelectMany(ReadPolygonRings).ToArray(),
            _ => throw new InvalidDataException("AdministrativeBoundaryGeometryTypeUnsupported")
        };
        if (rings.Length == 0) throw new InvalidDataException("AdministrativeBoundaryRingMissing");
        // 첫 디오라마 판본은 가장 큰 외곽 고리 하나만 허용한다. 섬·구멍은 손실 여부가 드러나도록 거절한다.
        var outerRings = rings.Where(ring => ring.Length >= 4).OrderByDescending(AbsoluteArea).ToArray();
        if (outerRings.Length != 1)
            throw new InvalidDataException("AdministrativeBoundaryMultipartRequiresReview");
        var projected = outerRings[0]
            .Select(point => ProjectWgs84(point.Latitude, point.Longitude, frame))
            .ToArray();
        if (projected.Length > 1 && Same(projected[0], projected[^1])) projected = projected[..^1];
        if (projected.Length < 3) throw new InvalidDataException("AdministrativeBoundaryRingInvalid");
        return new(administrativeAreaStableId, name, sourceVintage, projected, type!);
    }

    private static (double Longitude, double Latitude)[][] ReadPolygonRings(JsonElement coordinates)
        => coordinates.EnumerateArray().Select(ReadRing).ToArray();

    private static (double Longitude, double Latitude)[] ReadRing(JsonElement ring)
        => ring.EnumerateArray().Select(position =>
        {
            var values = position.EnumerateArray().ToArray();
            if (values.Length < 2) throw new InvalidDataException("AdministrativeBoundaryCoordinateInvalid");
            var longitude = values[0].GetDouble();
            var latitude = values[1].GetDouble();
            if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
                throw new InvalidDataException("AdministrativeBoundaryCoordinateOutOfRange");
            return (longitude, latitude);
        }).ToArray();

    private static string ReadProperty(JsonElement feature, IEnumerable<string> candidates)
    {
        if (!feature.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var candidate in candidates)
            if (properties.TryGetProperty(candidate, out var value))
                return value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim() ?? string.Empty
                    : value.ToString().Trim();
        return string.Empty;
    }

    public static AdministrativeDongDioramaPoint ProjectWgs84(
        double latitude,
        double longitude,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        const double a = 6378137d;
        const double eccentricitySquared = 6.69437999014e-3d;
        var lat = DegreesToRadians(latitude);
        var lon = DegreesToRadians(longitude);
        var originLat = DegreesToRadians(frame.OriginLatitude);
        var originLon = DegreesToRadians(frame.OriginLongitude);
        var point = Ecef(lat, lon, a, eccentricitySquared);
        var origin = Ecef(originLat, originLon, a, eccentricitySquared);
        var dx = point.X - origin.X;
        var dy = point.Y - origin.Y;
        var dz = point.Z - origin.Z;
        var east = -Math.Sin(originLon) * dx + Math.Cos(originLon) * dy;
        var north = -Math.Sin(originLat) * Math.Cos(originLon) * dx
                    - Math.Sin(originLat) * Math.Sin(originLon) * dy
                    + Math.Cos(originLat) * dz;
        return new AdministrativeDongDioramaPoint
        {
            X = frame.WorldOffsetX + east / frame.MetersPerUnit,
            Z = frame.WorldOffsetZ + north / frame.MetersPerUnit
        };
    }

    private static (double X, double Y, double Z) Ecef(
        double latitude,
        double longitude,
        double a,
        double eccentricitySquared)
    {
        var n = a / Math.Sqrt(1d - eccentricitySquared * Math.Sin(latitude) * Math.Sin(latitude));
        return (n * Math.Cos(latitude) * Math.Cos(longitude),
            n * Math.Cos(latitude) * Math.Sin(longitude),
            n * (1d - eccentricitySquared) * Math.Sin(latitude));
    }

    private static double AbsoluteArea((double Longitude, double Latitude)[] ring)
    {
        var area = 0d;
        for (var index = 0; index < ring.Length; index++)
        {
            var next = ring[(index + 1) % ring.Length];
            area += ring[index].Longitude * next.Latitude - next.Longitude * ring[index].Latitude;
        }
        return Math.Abs(area / 2d);
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static bool Same(AdministrativeDongDioramaPoint a, AdministrativeDongDioramaPoint b)
        => Math.Abs(a.X - b.X) < 0.000001d && Math.Abs(a.Z - b.Z) < 0.000001d;
}
