using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

/// <summary>
/// 서울시 행정동 경계 Shapefile ZIP(EPSG:5181)을 동결 사본에서 직접 읽습니다.
/// 공급 파일의 8자리 행정동 코드는 행정안전부 10자리 코드의 끝 <c>00</c>만 보완합니다.
/// </summary>
public static class 행정동경계ShapefileZipReader
{
    private const int ShapeTypePolygon = 5;

    public static IReadOnlyList<행정동경계GeoJsonSnapshot> Read(
        ReadOnlySpan<byte> zipBytes,
        IEnumerable<string> administrativeAreaStableIds,
        string sourceVintage,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        if (zipBytes.Length == 0) throw new InvalidDataException("AdministrativeBoundarySnapshotRequired");
        if (string.IsNullOrWhiteSpace(sourceVintage))
            throw new ArgumentException("AdministrativeDongSourceVintageRequired", nameof(sourceVintage));

        var requested = administrativeAreaStableIds
            .Select(ShortCode)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(code => code, code => "region:kr:hjd:" + code + "00", StringComparer.Ordinal);
        if (requested.Count == 0) throw new ArgumentException("AdministrativeDongStableIdRequired", nameof(administrativeAreaStableIds));

        using var stream = new MemoryStream(zipBytes.ToArray(), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var shp = SingleEntry(archive, ".shp");
        var dbf = SingleEntry(archive, ".dbf");
        var cpg = ReadText(SingleEntry(archive, ".cpg"), Encoding.ASCII).Trim();
        if (cpg is not ("UTF-8" or "UTF8" or "utf_8"))
            throw new InvalidDataException("AdministrativeBoundaryEncodingUnsupported");
        var prj = ReadText(SingleEntry(archive, ".prj"), Encoding.ASCII);
        if (!prj.Contains("Korea_2000_Korea_Central_Belt", StringComparison.Ordinal)
            || !prj.Contains("False_Easting\",200000", StringComparison.Ordinal)
            || !prj.Contains("False_Northing\",500000", StringComparison.Ordinal)
            || !prj.Contains("Central_Meridian\",127", StringComparison.Ordinal))
            throw new InvalidDataException("AdministrativeBoundaryCrsUnsupported");

        var rows = ReadDbf(dbf);
        var shapes = ReadShapes(shp);
        if (rows.Count != shapes.Count) throw new InvalidDataException("AdministrativeBoundaryRecordCountMismatch");

        var found = new Dictionary<string, 행정동경계GeoJsonSnapshot>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Count; index++)
        {
            var code = rows[index].GetValueOrDefault("ADSTRD_CD")?.Trim() ?? string.Empty;
            if (!requested.TryGetValue(code, out var stableId)) continue;
            if (found.ContainsKey(code)) throw new InvalidDataException("AdministrativeBoundaryFeatureDuplicate");
            var name = rows[index].GetValueOrDefault("ADSTRD_NM")?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("AdministrativeBoundaryDisplayNameMissing");
            var sourceRing = shapes[index];
            if (sourceRing.Length < 4) throw new InvalidDataException("AdministrativeBoundaryRingInvalid");
            var boundary = sourceRing
                .Select(point =>
                {
                    var wgs84 = InverseKorea2000CentralBelt(point.X, point.Y);
                    return 행정동경계GeoJsonReader.ProjectWgs84(wgs84.Latitude, wgs84.Longitude, frame);
                })
                .ToArray();
            if (boundary.Length > 1 && Same(boundary[0], boundary[^1])) boundary = boundary[..^1];
            found[code] = new 행정동경계GeoJsonSnapshot(stableId, name, sourceVintage, boundary, "Polygon");
        }

        var missing = requested.Keys.Where(code => !found.ContainsKey(code)).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0) throw new InvalidDataException("AdministrativeBoundaryFeatureMissing:" + string.Join(",", missing));
        return requested.Keys.Order(StringComparer.Ordinal).Select(code => found[code]).ToArray();
    }

    internal static (double Longitude, double Latitude) InverseKorea2000CentralBelt(double easting, double northing)
    {
        const double semiMajor = 6378137d;
        const double inverseFlattening = 298.257222101d;
        const double falseEasting = 200000d;
        const double falseNorthing = 500000d;
        const double scale = 1d;
        var flattening = 1d / inverseFlattening;
        var eccentricitySquared = 2d * flattening - flattening * flattening;
        var secondEccentricitySquared = eccentricitySquared / (1d - eccentricitySquared);
        var originLatitude = DegreesToRadians(38d);
        var centralMeridian = DegreesToRadians(127d);
        var originMeridionalArc = MeridionalArc(originLatitude, semiMajor, eccentricitySquared);
        var meridionalArc = originMeridionalArc + (northing - falseNorthing) / scale;
        var denominator = semiMajor * (1d - eccentricitySquared / 4d
            - 3d * Math.Pow(eccentricitySquared, 2) / 64d
            - 5d * Math.Pow(eccentricitySquared, 3) / 256d);
        var mu = meridionalArc / denominator;
        var e1 = (1d - Math.Sqrt(1d - eccentricitySquared)) / (1d + Math.Sqrt(1d - eccentricitySquared));
        var footprintLatitude = mu
            + (3d * e1 / 2d - 27d * Math.Pow(e1, 3) / 32d) * Math.Sin(2d * mu)
            + (21d * Math.Pow(e1, 2) / 16d - 55d * Math.Pow(e1, 4) / 32d) * Math.Sin(4d * mu)
            + 151d * Math.Pow(e1, 3) / 96d * Math.Sin(6d * mu)
            + 1097d * Math.Pow(e1, 4) / 512d * Math.Sin(8d * mu);
        var sin = Math.Sin(footprintLatitude);
        var cos = Math.Cos(footprintLatitude);
        var tan = Math.Tan(footprintLatitude);
        var n1 = semiMajor / Math.Sqrt(1d - eccentricitySquared * sin * sin);
        var r1 = semiMajor * (1d - eccentricitySquared)
                 / Math.Pow(1d - eccentricitySquared * sin * sin, 1.5d);
        var t1 = tan * tan;
        var c1 = secondEccentricitySquared * cos * cos;
        var d = (easting - falseEasting) / (n1 * scale);
        var latitude = footprintLatitude - n1 * tan / r1 *
            (d * d / 2d
             - (5d + 3d * t1 + 10d * c1 - 4d * c1 * c1 - 9d * secondEccentricitySquared) * Math.Pow(d, 4) / 24d
             + (61d + 90d * t1 + 298d * c1 + 45d * t1 * t1 - 252d * secondEccentricitySquared - 3d * c1 * c1) * Math.Pow(d, 6) / 720d);
        var longitude = centralMeridian +
            (d - (1d + 2d * t1 + c1) * Math.Pow(d, 3) / 6d
             + (5d - 2d * c1 + 28d * t1 - 3d * c1 * c1 + 8d * secondEccentricitySquared + 24d * t1 * t1) * Math.Pow(d, 5) / 120d) / cos;
        return (RadiansToDegrees(longitude), RadiansToDegrees(latitude));
    }

    private static double MeridionalArc(double latitude, double semiMajor, double eccentricitySquared)
        => semiMajor * ((1d - eccentricitySquared / 4d - 3d * Math.Pow(eccentricitySquared, 2) / 64d
                         - 5d * Math.Pow(eccentricitySquared, 3) / 256d) * latitude
                        - (3d * eccentricitySquared / 8d + 3d * Math.Pow(eccentricitySquared, 2) / 32d
                           + 45d * Math.Pow(eccentricitySquared, 3) / 1024d) * Math.Sin(2d * latitude)
                        + (15d * Math.Pow(eccentricitySquared, 2) / 256d
                           + 45d * Math.Pow(eccentricitySquared, 3) / 1024d) * Math.Sin(4d * latitude)
                        - 35d * Math.Pow(eccentricitySquared, 3) / 3072d * Math.Sin(6d * latitude));

    private static List<Dictionary<string, string>> ReadDbf(ZipArchiveEntry entry)
    {
        var bytes = ReadBytes(entry);
        if (bytes.Length < 33) throw new InvalidDataException("AdministrativeBoundaryDbfInvalid");
        var recordCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2));
        var fields = new List<(string Name, int Length)>();
        for (var offset = 32; offset + 32 <= headerLength && bytes[offset] != 0x0d; offset += 32)
        {
            var end = Array.IndexOf(bytes, (byte)0, offset, 11);
            if (end < 0) end = offset + 11;
            fields.Add((Encoding.ASCII.GetString(bytes, offset, end - offset), bytes[offset + 16]));
        }
        if (!fields.Any(field => field.Name == "ADSTRD_CD") || !fields.Any(field => field.Name == "ADSTRD_NM"))
            throw new InvalidDataException("AdministrativeBoundaryDbfFieldsMissing");
        if (headerLength + recordCount * recordLength > bytes.Length)
            throw new InvalidDataException("AdministrativeBoundaryDbfTruncated");
        var rows = new List<Dictionary<string, string>>(recordCount);
        for (var index = 0; index < recordCount; index++)
        {
            var recordOffset = headerLength + index * recordLength;
            if (bytes[recordOffset] == (byte)'*') throw new InvalidDataException("AdministrativeBoundaryDeletedRecordUnsupported");
            var fieldOffset = recordOffset + 1;
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                row[field.Name] = Encoding.UTF8.GetString(bytes, fieldOffset, field.Length).Trim(' ', '\0');
                fieldOffset += field.Length;
            }
            rows.Add(row);
        }
        return rows;
    }

    private static List<(double X, double Y)[]> ReadShapes(ZipArchiveEntry entry)
    {
        var bytes = ReadBytes(entry);
        if (bytes.Length < 100 || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4)) != 9994)
            throw new InvalidDataException("AdministrativeBoundaryShapefileInvalid");
        var shapes = new List<(double X, double Y)[]>();
        var offset = 100;
        while (offset < bytes.Length)
        {
            if (offset + 8 > bytes.Length) throw new InvalidDataException("AdministrativeBoundaryShapefileTruncated");
            var contentBytes = checked(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 4, 4)) * 2);
            offset += 8;
            if (contentBytes < 48 || offset + contentBytes > bytes.Length)
                throw new InvalidDataException("AdministrativeBoundaryShapefileRecordInvalid");
            var shapeType = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
            if (shapeType != ShapeTypePolygon) throw new InvalidDataException("AdministrativeBoundaryShapeTypeUnsupported");
            var partCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 36, 4));
            var pointCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 40, 4));
            if (partCount != 1 || pointCount < 4) throw new InvalidDataException("AdministrativeBoundaryMultipartRequiresReview");
            var pointsOffset = offset + 44 + partCount * 4;
            if (pointsOffset + pointCount * 16 > offset + contentBytes)
                throw new InvalidDataException("AdministrativeBoundaryShapefilePointsTruncated");
            var points = new (double X, double Y)[pointCount];
            for (var point = 0; point < pointCount; point++)
            {
                var itemOffset = pointsOffset + point * 16;
                points[point] = (
                    BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(itemOffset, 8))),
                    BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(itemOffset + 8, 8))));
            }
            shapes.Add(points);
            offset += contentBytes;
        }
        return shapes;
    }

    private static string ShortCode(string stableId)
    {
        if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(stableId))
            throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(stableId));
        var code = stableId["region:kr:hjd:".Length..];
        if (!code.EndsWith("00", StringComparison.Ordinal))
            throw new InvalidDataException("AdministrativeBoundaryEightDigitCodeUnsupported");
        return code[..8];
    }

    private static ZipArchiveEntry SingleEntry(ZipArchive archive, string extension)
    {
        var matches = archive.Entries.Where(entry => entry.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("AdministrativeBoundaryArchiveEntryInvalid:" + extension);
        return matches[0];
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        if (entry.Length <= 0 || entry.Length > 32 * 1024 * 1024)
            throw new InvalidDataException("AdministrativeBoundaryArchiveEntrySizeInvalid");
        using var input = entry.Open();
        using var output = new MemoryStream((int)entry.Length);
        input.CopyTo(output);
        return output.ToArray();
    }

    private static string ReadText(ZipArchiveEntry entry, Encoding encoding) => encoding.GetString(ReadBytes(entry));
    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static double RadiansToDegrees(double value) => value * 180d / Math.PI;
    private static bool Same(AdministrativeDongDioramaPoint a, AdministrativeDongDioramaPoint b)
        => Math.Abs(a.X - b.X) < 0.000001d && Math.Abs(a.Z - b.Z) < 0.000001d;
}
