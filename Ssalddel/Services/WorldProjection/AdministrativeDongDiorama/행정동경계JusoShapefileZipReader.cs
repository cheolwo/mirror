using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

/// <summary>
/// 행정안전부 주소정보누리집의 구역의 도형 중 행정동(<c>TL_SCCO_GEMD</c>)
/// Shapefile ZIP을 읽습니다.
/// </summary>
/// <remarks>
/// 주소정보누리집의 월 전체분은 PRJ를 제공하지 않으므로 호출자가
/// <c>EPSG:5179</c>를 명시해야 합니다. 원본 파일명·기준월·SHA-256이
/// 승인된 동결 사본과 일치하는지는 이 리더가 아니라 상위 반입기가 검증합니다.
/// 현재 v1 경계 계약은 단일 외곽 고리만 표현하므로 출장소, 중복 코드,
/// 다중 부분 또는 구멍이 있는 형상은 임의로 합치거나 버리지 않고 거절합니다.
/// </remarks>
public static class 행정동경계JusoShapefileZipReader
{
    public const string RequiredCrs = "EPSG:5179";

    private const int ShapeTypePolygon = 5;
    private const int MaximumArchiveEntryBytes = 256 * 1024 * 1024;
    private const int MaximumRecordCount = 100_000;
    private const int MaximumPointsPerBoundary = 4_096;
    private const string StableIdPrefix = "region:kr:hjd:";

    public static IReadOnlyList<행정동경계GeoJsonSnapshot> Read(
        ReadOnlySpan<byte> zipBytes,
        IEnumerable<string> administrativeAreaStableIds,
        string sourceVintage,
        string expectedCrs,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        if (zipBytes.Length == 0)
            throw new InvalidDataException("AdministrativeBoundarySnapshotRequired");
        if (string.IsNullOrWhiteSpace(sourceVintage))
            throw new ArgumentException("AdministrativeDongSourceVintageRequired", nameof(sourceVintage));
        if (string.IsNullOrWhiteSpace(expectedCrs))
            throw new ArgumentException("AdministrativeBoundaryExpectedCrsRequired", nameof(expectedCrs));
        if (!string.Equals(expectedCrs.Trim(), RequiredCrs, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("AdministrativeBoundaryCrsUnsupported:" + expectedCrs.Trim());
        ValidateFrame(frame);

        ArgumentNullException.ThrowIfNull(administrativeAreaStableIds);
        var requested = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stableId in administrativeAreaStableIds)
        {
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(stableId))
                throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(administrativeAreaStableIds));
            var code = stableId[StableIdPrefix.Length..];
            if (!requested.TryAdd(code, stableId))
                throw new InvalidDataException("AdministrativeBoundaryAllowListDuplicate:" + code);
        }
        if (requested.Count == 0)
            throw new ArgumentException("AdministrativeDongStableIdRequired", nameof(administrativeAreaStableIds));

        using var stream = new MemoryStream(zipBytes.ToArray(), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var shp = SingleEntry(archive, ".shp");
        var shx = SingleEntry(archive, ".shx");
        var dbf = SingleEntry(archive, ".dbf");
        EnsureMatchingBaseNames(shp, shx, dbf);
        if (archive.Entries.Any(entry => !IsDirectory(entry)
                                         && entry.FullName.EndsWith(".prj", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("AdministrativeBoundaryUnexpectedPrjRequiresReview");

        var encoding = ResolveDbfEncoding(archive, shp);
        var rows = ReadDbf(dbf, encoding);
        var requestedRecordIndexes = rows
            .Select((row, index) => (row, index))
            .Where(item => item.row.Values.TryGetValue("EMD_CD", out var code)
                           && requested.ContainsKey(code.Trim()))
            .Select(item => item.index)
            .ToHashSet();
        var shapes = ReadShapes(shp, shx, requestedRecordIndexes);
        if (rows.Count != shapes.Count)
            throw new InvalidDataException("AdministrativeBoundaryRecordCountMismatch");

        var found = new Dictionary<string, 행정동경계GeoJsonSnapshot>(StringComparer.Ordinal);
        var seenCodes = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var code = RequiredValue(row.Values, "EMD_CD");
            if (!requested.TryGetValue(code, out var stableId))
                continue;
            if (!IsTenDigitCode(code))
                throw new InvalidDataException("AdministrativeBoundaryCodeInvalid:" + code);
            if (row.Deleted)
                throw new InvalidDataException("AdministrativeBoundaryDeletedRecordUnsupported:" + index);
            if (!seenCodes.Add(code))
                throw new InvalidDataException("AdministrativeBoundaryFeatureDuplicate:" + code);

            var name = ReadOfficialName(row.Values);
            if (name.Contains("출장소", StringComparison.Ordinal))
                throw new InvalidDataException("AdministrativeBoundaryBranchOfficeRequiresReview:" + code);
            var sourceRing = shapes[index]
                             ?? throw new InvalidDataException("AdministrativeBoundaryGeometryRequiresReview:" + index);
            var boundary = sourceRing
                .Select(point =>
                {
                    var wgs84 = InverseKorea2000UnifiedCs(point.X, point.Y);
                    return 행정동경계GeoJsonReader.ProjectWgs84(
                        wgs84.Latitude,
                        wgs84.Longitude,
                        frame);
                })
                .ToArray();
            if (boundary.Length > 1 && Same(boundary[0], boundary[^1]))
                boundary = boundary[..^1];
            if (boundary.Length < 3)
                throw new InvalidDataException("AdministrativeBoundaryRingInvalid:" + code);
            found.Add(code, new 행정동경계GeoJsonSnapshot(
                stableId,
                name,
                sourceVintage.Trim(),
                boundary,
                "Polygon"));
        }

        var missing = requested.Keys
            .Where(code => !found.ContainsKey(code))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException("AdministrativeBoundaryFeatureMissing:" + string.Join(",", missing));

        return requested.Keys
            .Order(StringComparer.Ordinal)
            .Select(code => found[code])
            .ToArray();
    }

    /// <summary>
    /// EPSG:5179(Korea 2000 / Unified CS, GRS80 UTM-K)을 WGS84 경위도로 역변환합니다.
    /// </summary>
    internal static (double Longitude, double Latitude) InverseKorea2000UnifiedCs(
        double easting,
        double northing)
    {
        if (!double.IsFinite(easting) || !double.IsFinite(northing))
            throw new InvalidDataException("AdministrativeBoundaryCoordinateInvalid");

        const double semiMajor = 6378137d;
        const double inverseFlattening = 298.257222101d;
        const double falseEasting = 1_000_000d;
        const double falseNorthing = 2_000_000d;
        const double scale = 0.9996d;
        var flattening = 1d / inverseFlattening;
        var eccentricitySquared = 2d * flattening - flattening * flattening;
        var secondEccentricitySquared = eccentricitySquared / (1d - eccentricitySquared);
        var originLatitude = DegreesToRadians(38d);
        var centralMeridian = DegreesToRadians(127.5d);
        var originMeridionalArc = MeridionalArc(originLatitude, semiMajor, eccentricitySquared);
        var meridionalArc = originMeridionalArc + (northing - falseNorthing) / scale;
        var denominator = semiMajor * (1d - eccentricitySquared / 4d
            - 3d * Math.Pow(eccentricitySquared, 2) / 64d
            - 5d * Math.Pow(eccentricitySquared, 3) / 256d);
        var mu = meridionalArc / denominator;
        var e1 = (1d - Math.Sqrt(1d - eccentricitySquared))
                 / (1d + Math.Sqrt(1d - eccentricitySquared));
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
        var latitude = footprintLatitude - n1 * tan / r1
            * (d * d / 2d
               - (5d + 3d * t1 + 10d * c1 - 4d * c1 * c1 - 9d * secondEccentricitySquared)
               * Math.Pow(d, 4) / 24d
               + (61d + 90d * t1 + 298d * c1 + 45d * t1 * t1
                  - 252d * secondEccentricitySquared - 3d * c1 * c1)
               * Math.Pow(d, 6) / 720d);
        var longitude = centralMeridian
            + (d - (1d + 2d * t1 + c1) * Math.Pow(d, 3) / 6d
               + (5d - 2d * c1 + 28d * t1 - 3d * c1 * c1
                  + 8d * secondEccentricitySquared + 24d * t1 * t1)
               * Math.Pow(d, 5) / 120d) / cos;
        return (RadiansToDegrees(longitude), RadiansToDegrees(latitude));
    }

    private static List<DbfRow> ReadDbf(ZipArchiveEntry entry, Encoding encoding)
    {
        var bytes = ReadBytes(entry);
        if (bytes.Length < 33)
            throw new InvalidDataException("AdministrativeBoundaryDbfInvalid");
        var recordCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2));
        if (recordCount < 0 || recordCount > MaximumRecordCount || headerLength < 33 || recordLength < 2)
            throw new InvalidDataException("AdministrativeBoundaryDbfHeaderInvalid");
        if (headerLength > bytes.Length || (headerLength - 33) % 32 != 0 || bytes[headerLength - 1] != 0x0d)
            throw new InvalidDataException("AdministrativeBoundaryDbfHeaderInvalid");

        var fields = new List<DbfField>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var offset = 32; offset + 32 <= headerLength - 1; offset += 32)
        {
            var end = Array.IndexOf(bytes, (byte)0, offset, 11);
            if (end < 0)
                end = offset + 11;
            var name = Encoding.ASCII.GetString(bytes, offset, end - offset).Trim();
            var type = (char)bytes[offset + 11];
            var length = bytes[offset + 16];
            if (string.IsNullOrWhiteSpace(name) || length == 0 || !names.Add(name))
                throw new InvalidDataException("AdministrativeBoundaryDbfFieldInvalid");
            fields.Add(new DbfField(name, type, length));
        }
        var codeField = fields.SingleOrDefault(field => field.Name == "EMD_CD");
        if (codeField is null || codeField.Type != 'C' || codeField.Length < 10)
            throw new InvalidDataException("AdministrativeBoundaryDbfCodeFieldMissing");
        var nameField = fields.SingleOrDefault(field => field.Name == "EMD_KOR_NM");
        if (nameField is null || nameField.Type != 'C')
            throw new InvalidDataException("AdministrativeBoundaryDbfNameFieldMissing");
        if (1 + fields.Sum(field => field.Length) != recordLength)
            throw new InvalidDataException("AdministrativeBoundaryDbfRecordLengthMismatch");
        if ((long)headerLength + (long)recordCount * recordLength > bytes.Length)
            throw new InvalidDataException("AdministrativeBoundaryDbfTruncated");

        var rows = new List<DbfRow>(recordCount);
        for (var index = 0; index < recordCount; index++)
        {
            var recordOffset = headerLength + index * recordLength;
            var deleted = bytes[recordOffset] == (byte)'*';
            if (!deleted && bytes[recordOffset] != (byte)' ')
                throw new InvalidDataException("AdministrativeBoundaryDbfRecordInvalid:" + index);
            var fieldOffset = recordOffset + 1;
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                row[field.Name] = Decode(encoding, bytes.AsSpan(fieldOffset, field.Length)).Trim(' ', '\0');
                fieldOffset += field.Length;
            }
            rows.Add(new DbfRow(row, deleted));
        }
        return rows;
    }

    private static List<(double X, double Y)[]?> ReadShapes(
        ZipArchiveEntry shpEntry,
        ZipArchiveEntry shxEntry,
        IReadOnlySet<int> requestedRecordIndexes)
    {
        var shp = ReadBytes(shpEntry);
        var shx = ReadBytes(shxEntry);
        ValidateShapefileHeader(shp, "SHP");
        ValidateShapefileHeader(shx, "SHX");
        if (shx.Length < 100 || (shx.Length - 100) % 8 != 0)
            throw new InvalidDataException("AdministrativeBoundaryShxInvalid");

        var indexRecords = new List<(int OffsetWords, int ContentLengthWords)>();
        for (var offset = 100; offset < shx.Length; offset += 8)
        {
            var offsetWords = BinaryPrimitives.ReadInt32BigEndian(shx.AsSpan(offset, 4));
            var contentLengthWords = BinaryPrimitives.ReadInt32BigEndian(shx.AsSpan(offset + 4, 4));
            if (offsetWords < 50 || contentLengthWords < 2)
                throw new InvalidDataException("AdministrativeBoundaryShxRecordInvalid");
            indexRecords.Add((offsetWords, contentLengthWords));
        }

        var shapes = new List<(double X, double Y)[]?>(indexRecords.Count);
        var shpOffset = 100;
        for (var recordIndex = 0; recordIndex < indexRecords.Count; recordIndex++)
        {
            if (shpOffset + 8 > shp.Length)
                throw new InvalidDataException("AdministrativeBoundaryShapefileTruncated");
            var indexRecord = indexRecords[recordIndex];
            if (indexRecord.OffsetWords * 2 != shpOffset)
                throw new InvalidDataException("AdministrativeBoundaryShxOffsetMismatch:" + recordIndex);
            var contentLengthWords = BinaryPrimitives.ReadInt32BigEndian(shp.AsSpan(shpOffset + 4, 4));
            if (contentLengthWords != indexRecord.ContentLengthWords)
                throw new InvalidDataException("AdministrativeBoundaryShxLengthMismatch:" + recordIndex);
            var contentBytes = checked(contentLengthWords * 2);
            var contentOffset = shpOffset + 8;
            if (contentBytes < 4 || contentOffset + contentBytes > shp.Length)
                throw new InvalidDataException("AdministrativeBoundaryShapefileRecordInvalid:" + recordIndex);
            if (!requestedRecordIndexes.Contains(recordIndex))
            {
                shapes.Add(null);
                shpOffset = contentOffset + contentBytes;
                continue;
            }
            if (contentBytes < 48)
                throw new InvalidDataException("AdministrativeBoundaryShapefileRecordInvalid:" + recordIndex);
            if (BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(contentOffset, 4)) != ShapeTypePolygon)
                throw new InvalidDataException("AdministrativeBoundaryGeometryRequiresReview:" + recordIndex);

            var partCount = BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(contentOffset + 36, 4));
            var pointCount = BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(contentOffset + 40, 4));
            if (partCount != 1)
                throw new InvalidDataException("AdministrativeBoundaryMultipartOrHoleRequiresReview:" + recordIndex);
            if (pointCount < 4)
                throw new InvalidDataException("AdministrativeBoundaryRingInvalid:" + recordIndex);
            if (pointCount > MaximumPointsPerBoundary)
                throw new InvalidDataException("AdministrativeBoundaryComplexityLimitExceeded:" + recordIndex);
            var partsOffset = contentOffset + 44;
            var pointsOffset = checked(partsOffset + partCount * 4);
            if (pointsOffset + (long)pointCount * 16 > contentOffset + contentBytes)
                throw new InvalidDataException("AdministrativeBoundaryShapefilePointsTruncated:" + recordIndex);
            if (BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(partsOffset, 4)) != 0)
                throw new InvalidDataException("AdministrativeBoundaryPartIndexInvalid:" + recordIndex);

            var points = new (double X, double Y)[pointCount];
            for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                var pointOffset = pointsOffset + pointIndex * 16;
                var x = BitConverter.Int64BitsToDouble(
                    BinaryPrimitives.ReadInt64LittleEndian(shp.AsSpan(pointOffset, 8)));
                var y = BitConverter.Int64BitsToDouble(
                    BinaryPrimitives.ReadInt64LittleEndian(shp.AsSpan(pointOffset + 8, 8)));
                if (!double.IsFinite(x) || !double.IsFinite(y))
                    throw new InvalidDataException("AdministrativeBoundaryCoordinateInvalid:" + recordIndex);
                points[pointIndex] = (x, y);
            }
            if (!Same(points[0], points[^1]) || Math.Abs(SignedArea(points)) < 0.000001d)
                throw new InvalidDataException("AdministrativeBoundaryRingInvalid:" + recordIndex);
            if (HasSelfIntersection(points))
                throw new InvalidDataException("AdministrativeBoundarySelfIntersectionRequiresReview:" + recordIndex);
            shapes.Add(points);
            shpOffset = contentOffset + contentBytes;
        }
        if (shpOffset != shp.Length)
            throw new InvalidDataException("AdministrativeBoundaryShapefileTrailingData");
        return shapes;
    }

    private static void ValidateShapefileHeader(byte[] bytes, string kind)
    {
        if (bytes.Length < 100
            || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4)) != 9994
            || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28, 4)) != 1000
            || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(32, 4)) != ShapeTypePolygon)
            throw new InvalidDataException("AdministrativeBoundary" + kind + "HeaderInvalid");
        var declaredBytes = checked(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(24, 4)) * 2);
        if (declaredBytes != bytes.Length)
            throw new InvalidDataException("AdministrativeBoundary" + kind + "LengthMismatch");
    }

    private static string ReadOfficialName(IReadOnlyDictionary<string, string> row)
    {
        // JUSO TL_SCCO_GEMD의 공식 국문명 필드만 읽는다. EMD_CD나 외부 코드표
        // join을 표시명 fallback으로 쓰면 서로 다른 판본의 경계를 숨길 수 있다.
        if (!row.TryGetValue("EMD_KOR_NM", out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException("AdministrativeBoundaryDisplayNameMissing");
        return value.Trim();
    }

    private static Encoding ResolveDbfEncoding(ZipArchive archive, ZipArchiveEntry shp)
    {
        var cpgEntries = archive.Entries
            .Where(entry => !IsDirectory(entry)
                            && entry.FullName.EndsWith(".cpg", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (cpgEntries.Length > 1)
            throw new InvalidDataException("AdministrativeBoundaryArchiveEntryInvalid:.cpg");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (cpgEntries.Length == 0)
            return Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        if (!string.Equals(BaseName(cpgEntries[0]), BaseName(shp), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("AdministrativeBoundaryArchiveBaseNameMismatch");
        var codePage = Encoding.ASCII.GetString(ReadBytes(cpgEntries[0])).Trim().Replace("_", "-", StringComparison.Ordinal);
        return codePage.ToUpperInvariant() switch
        {
            "UTF-8" or "UTF8" or "65001" =>
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            "EUC-KR" or "EUCKR" or "CP949" or "949" or "ANSI-949" =>
                Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            _ => throw new InvalidDataException("AdministrativeBoundaryEncodingUnsupported:" + codePage)
        };
    }

    private static ZipArchiveEntry SingleEntry(ZipArchive archive, string extension)
    {
        var matches = archive.Entries
            .Where(entry => !IsDirectory(entry)
                            && entry.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("AdministrativeBoundaryArchiveEntryInvalid:" + extension);
        return matches[0];
    }

    private static void EnsureMatchingBaseNames(params ZipArchiveEntry[] entries)
    {
        if (entries.Select(BaseName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
            throw new InvalidDataException("AdministrativeBoundaryArchiveBaseNameMismatch");
    }

    private static string BaseName(ZipArchiveEntry entry)
        => Path.GetFileNameWithoutExtension(entry.FullName);

    private static bool IsDirectory(ZipArchiveEntry entry)
        => entry.FullName.EndsWith("/", StringComparison.Ordinal)
           || entry.FullName.EndsWith("\\", StringComparison.Ordinal);

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        if (entry.Length <= 0 || entry.Length > MaximumArchiveEntryBytes)
            throw new InvalidDataException("AdministrativeBoundaryArchiveEntrySizeInvalid:" + entry.Name);
        using var input = entry.Open();
        using var output = new MemoryStream((int)entry.Length);
        input.CopyTo(output);
        if (output.Length != entry.Length)
            throw new InvalidDataException("AdministrativeBoundaryArchiveEntryLengthMismatch:" + entry.Name);
        return output.ToArray();
    }

    private static string RequiredValue(IReadOnlyDictionary<string, string> row, string field)
        => row.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidDataException("AdministrativeBoundaryDbfValueMissing:" + field);

    private static string Decode(Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        try
        {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("AdministrativeBoundaryDbfEncodingInvalid", exception);
        }
    }

    private static bool IsTenDigitCode(string value)
        => value.Length == 10 && value.All(character => character is >= '0' and <= '9');

    private static void ValidateFrame(AdministrativeDongDioramaCoordinateFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!double.IsFinite(frame.OriginLatitude)
            || !double.IsFinite(frame.OriginLongitude)
            || !double.IsFinite(frame.WorldOffsetX)
            || !double.IsFinite(frame.WorldOffsetZ)
            || !double.IsFinite(frame.MetersPerUnit)
            || frame.OriginLatitude is < -90d or > 90d
            || frame.OriginLongitude is < -180d or > 180d
            || frame.MetersPerUnit <= 0d)
            throw new ArgumentException("AdministrativeDongCoordinateFrameInvalid", nameof(frame));
    }

    private static double MeridionalArc(double latitude, double semiMajor, double eccentricitySquared)
        => semiMajor * ((1d - eccentricitySquared / 4d
                         - 3d * Math.Pow(eccentricitySquared, 2) / 64d
                         - 5d * Math.Pow(eccentricitySquared, 3) / 256d) * latitude
                        - (3d * eccentricitySquared / 8d
                           + 3d * Math.Pow(eccentricitySquared, 2) / 32d
                           + 45d * Math.Pow(eccentricitySquared, 3) / 1024d) * Math.Sin(2d * latitude)
                        + (15d * Math.Pow(eccentricitySquared, 2) / 256d
                           + 45d * Math.Pow(eccentricitySquared, 3) / 1024d) * Math.Sin(4d * latitude)
                        - 35d * Math.Pow(eccentricitySquared, 3) / 3072d * Math.Sin(6d * latitude));

    private static double SignedArea(IReadOnlyList<(double X, double Y)> ring)
    {
        var area = 0d;
        for (var index = 0; index < ring.Count - 1; index++)
            area += ring[index].X * ring[index + 1].Y - ring[index + 1].X * ring[index].Y;
        return area / 2d;
    }

    private static bool HasSelfIntersection(IReadOnlyList<(double X, double Y)> closedRing)
    {
        var edgeCount = closedRing.Count - 1;
        for (var first = 0; first < edgeCount; first++)
        {
            var a = closedRing[first];
            var b = closedRing[first + 1];
            for (var second = first + 1; second < edgeCount; second++)
            {
                if (second == first + 1 || first == 0 && second == edgeCount - 1)
                    continue;
                if (SegmentsIntersect(a, b, closedRing[second], closedRing[second + 1]))
                    return true;
            }
        }
        return false;
    }

    private static bool SegmentsIntersect(
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c,
        (double X, double Y) d)
    {
        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        const double epsilon = 0.000000001d;
        if (Math.Abs(abC) <= epsilon && OnSegment(a, b, c)) return true;
        if (Math.Abs(abD) <= epsilon && OnSegment(a, b, d)) return true;
        if (Math.Abs(cdA) <= epsilon && OnSegment(c, d, a)) return true;
        if (Math.Abs(cdB) <= epsilon && OnSegment(c, d, b)) return true;
        return (abC > 0d) != (abD > 0d) && (cdA > 0d) != (cdB > 0d);
    }

    private static double Cross(
        (double X, double Y) origin,
        (double X, double Y) first,
        (double X, double Y) second)
        => (first.X - origin.X) * (second.Y - origin.Y)
           - (first.Y - origin.Y) * (second.X - origin.X);

    private static bool OnSegment(
        (double X, double Y) start,
        (double X, double Y) end,
        (double X, double Y) point)
        => point.X >= Math.Min(start.X, end.X) - 0.000000001d
           && point.X <= Math.Max(start.X, end.X) + 0.000000001d
           && point.Y >= Math.Min(start.Y, end.Y) - 0.000000001d
           && point.Y <= Math.Max(start.Y, end.Y) + 0.000000001d;

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static double RadiansToDegrees(double value) => value * 180d / Math.PI;
    private static bool Same((double X, double Y) left, (double X, double Y) right)
        => Math.Abs(left.X - right.X) < 0.000001d && Math.Abs(left.Y - right.Y) < 0.000001d;
    private static bool Same(AdministrativeDongDioramaPoint left, AdministrativeDongDioramaPoint right)
        => Math.Abs(left.X - right.X) < 0.000001d && Math.Abs(left.Z - right.Z) < 0.000001d;

    private sealed record DbfField(string Name, char Type, int Length);
    private sealed record DbfRow(IReadOnlyDictionary<string, string> Values, bool Deleted);
}
