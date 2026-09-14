using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Services.WorldProjection;

public sealed class 행정동경계JusoShapefileZipReaderTests
{
    private const string AreaCode = "1126057500";
    private const string AreaId = "region:kr:hjd:" + AreaCode;
    private const double OriginEasting = 963658.6890049018d;
    private const double OriginNorthing = 1953558.741138048d;

    [Fact]
    public void Juso행정동Zip은_10자리AllowList와공식국문명만_공통좌표계로읽는다()
    {
        var bytes = CreateArchive(
        [
            Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d)),
            Feature("1126056500", "면목제5동", Square(OriginEasting + 200d, OriginNorthing, 100d))
        ]);
        var frame = Frame();

        var result = 행정동경계JusoShapefileZipReader.Read(
            bytes,
            [AreaId],
            "2026-09",
            "EPSG:5179",
            frame);

        var boundary = Assert.Single(result);
        Assert.Equal(AreaId, boundary.AdministrativeAreaStableId);
        Assert.Equal("면목제3·8동", boundary.DisplayName);
        Assert.Equal("2026-09", boundary.SourceVintage);
        Assert.Equal("Polygon", boundary.GeometryTypeCode);
        Assert.Equal(4, boundary.Boundary.Length);
        Assert.InRange(Math.Abs(boundary.Boundary[0].X - frame.WorldOffsetX), 0d, 0.02d);
        Assert.InRange(Math.Abs(boundary.Boundary[0].Z - frame.WorldOffsetZ), 0d, 0.02d);
    }

    [Fact]
    public void Cpg가없는JusoZip은_공식기본Cp949국문명을읽는다()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = CreateArchive(
            [Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d))],
            includeCpg: false,
            dbfEncoding: Encoding.GetEncoding(949));

        var result = 행정동경계JusoShapefileZipReader.Read(
            bytes,
            [AreaId],
            "2026-09",
            행정동경계JusoShapefileZipReader.RequiredCrs,
            Frame());

        Assert.Equal("면목제3·8동", Assert.Single(result).DisplayName);
    }

    [Theory]
    [InlineData(null, "AdministrativeBoundaryExpectedCrsRequired")]
    [InlineData("", "AdministrativeBoundaryExpectedCrsRequired")]
    [InlineData("EPSG:5181", "AdministrativeBoundaryCrsUnsupported:EPSG:5181")]
    public void Prj없는JusoZip은_호출자가Epsg5179를명시하지않으면거절한다(
        string? expectedCrs,
        string expectedMessage)
    {
        var bytes = CreateArchive(
            [Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d))]);

        var error = Assert.ThrowsAny<Exception>(() => 행정동경계JusoShapefileZipReader.Read(
            bytes,
            [AreaId],
            "2026-09",
            expectedCrs!,
            Frame()));

        Assert.StartsWith(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 같은10자리코드가두번나오면_출장소중첩으로간주해거절한다()
    {
        var bytes = CreateArchive(
        [
            Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d)),
            Feature(AreaCode, "면목제3·8동 출장소", Square(OriginEasting + 20d, OriginNorthing + 20d, 20d))
        ]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryFeatureDuplicate:" + AreaCode, error.Message);
    }

    [Fact]
    public void 출장소표시명이AllowList에직접걸려도_겹친경계를사용하지않는다()
    {
        var bytes = CreateArchive(
            [Feature(AreaCode, "면목제3·8동 출장소", Square(OriginEasting, OriginNorthing, 100d))]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryBranchOfficeRequiresReview:" + AreaCode, error.Message);
    }

    [Fact]
    public void AllowList코드가없으면_이름이나다른코드로대체하지않는다()
    {
        var bytes = CreateArchive(
            [Feature("1126056500", "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d))]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryFeatureMissing:" + AreaCode, error.Message);
    }

    [Fact]
    public void 다중부분이나구멍은_단일외곽선으로조용히단순화하지않는다()
    {
        var outer = Square(OriginEasting, OriginNorthing, 100d);
        var inner = Square(OriginEasting + 20d, OriginNorthing + 20d, 20d);
        var bytes = CreateArchive([new ShapeFeature(AreaCode, "면목제3·8동", [outer, inner])]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryMultipartOrHoleRequiresReview:0", error.Message);
    }

    [Fact]
    public void Polygon이아닌형상은_경계로해석하지않고검토오류로남긴다()
    {
        var bytes = CreateArchive(
            [Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d)) with { ShapeType = 3 }]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryGeometryRequiresReview:0", error.Message);
    }

    [Fact]
    public void 자기교차하는대상경계는_PointInPolygon에넘기지않고거절한다()
    {
        var selfIntersecting = new (double X, double Y)[]
        {
            (OriginEasting, OriginNorthing),
            (OriginEasting + 100d, OriginNorthing + 100d),
            (OriginEasting, OriginNorthing + 100d),
            (OriginEasting + 100d, OriginNorthing),
            (OriginEasting + 50d, OriginNorthing - 20d),
            (OriginEasting, OriginNorthing)
        };
        var bytes = CreateArchive([Feature(AreaCode, "면목제3·8동", selfIntersecting)]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundarySelfIntersectionRequiresReview:0", error.Message);
    }

    [Fact]
    public void 과도하게복잡한대상경계는_자기교차검사전에예산으로차단한다()
    {
        var ring = Enumerable.Range(0, 4_097)
            .Select(index =>
            {
                var angle = 2d * Math.PI * index / 4_097d;
                return (OriginEasting + Math.Cos(angle) * 100d,
                    OriginNorthing + Math.Sin(angle) * 100d);
            })
            .Append((OriginEasting + 100d, OriginNorthing))
            .ToArray();
        var bytes = CreateArchive([Feature(AreaCode, "면목제3·8동", ring)]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryComplexityLimitExceeded:0", error.Message);
    }

    [Fact]
    public void 삭제표시된Dbf행은_형상과결합하지않고거절한다()
    {
        var bytes = CreateArchive(
            [Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d)) with { Deleted = true }]);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryDeletedRecordUnsupported:0", error.Message);
    }

    [Fact]
    public void AllowList밖의출장소중복과미지원형상은_대상행정동반입을막지않는다()
    {
        var outsideOuter = Square(OriginEasting + 200d, OriginNorthing, 100d);
        var outsideInner = Square(OriginEasting + 220d, OriginNorthing + 20d, 20d);
        var bytes = CreateArchive(
        [
            Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d)),
            new ShapeFeature("9999999999", "대상 밖 출장소", [outsideOuter, outsideInner], Deleted: true),
            Feature("9999999999", "대상 밖 중복", Square(OriginEasting + 400d, OriginNorthing, 100d))
                with { ShapeType = 3 }
        ]);

        var result = Read(bytes);

        Assert.Equal(AreaId, Assert.Single(result).AdministrativeAreaStableId);
    }

    [Fact]
    public void ShpShxDbf중같은확장자가두개면_임의파일을고르지않는다()
    {
        var bytes = CreateArchive(
            [Feature(AreaCode, "면목제3·8동", Square(OriginEasting, OriginNorthing, 100d))],
            duplicateShp: true);

        var error = Assert.Throws<InvalidDataException>(() => Read(bytes));

        Assert.Equal("AdministrativeBoundaryArchiveEntryInvalid:.shp", error.Message);
    }

    [Fact]
    public void Epsg5179알려진좌표를_Wgs84로허용오차내역변환한다()
    {
        var point = 행정동경계JusoShapefileZipReader.InverseKorea2000UnifiedCs(
            OriginEasting,
            OriginNorthing);

        Assert.InRange(Math.Abs(point.Longitude - 127.0884106d), 0d, 0.0000001d);
        Assert.InRange(Math.Abs(point.Latitude - 37.5806971d), 0d, 0.0000001d);
    }

    private static IReadOnlyList<행정동경계GeoJsonSnapshot> Read(byte[] bytes)
        => 행정동경계JusoShapefileZipReader.Read(
            bytes,
            [AreaId],
            "2026-09",
            행정동경계JusoShapefileZipReader.RequiredCrs,
            Frame());

    private static AdministrativeDongDioramaCoordinateFrame Frame()
        => new()
        {
            Method = "WGS84-ECEF-ENU-at-zero-altitude",
            OriginLatitude = 37.5806971d,
            OriginLongitude = 127.0884106d,
            WorldOffsetX = 550d,
            WorldOffsetZ = 8d,
            MetersPerUnit = 1d
        };

    private static ShapeFeature Feature(
        string code,
        string name,
        (double X, double Y)[] ring)
        => new(code, name, [ring]);

    private static (double X, double Y)[] Square(double x, double y, double size)
        =>
        [
            (x, y),
            (x + size, y),
            (x + size, y + size),
            (x, y + size),
            (x, y)
        ];

    private static byte[] CreateArchive(
        IReadOnlyList<ShapeFeature> features,
        bool includeCpg = true,
        Encoding? dbfEncoding = null,
        bool duplicateShp = false)
    {
        dbfEncoding ??= new UTF8Encoding(false, true);
        var (shp, shx) = CreateShapeFiles(features);
        var dbf = CreateDbf(features, dbfEncoding);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "TL_SCCO_GEMD.shp", shp);
            Add(archive, "TL_SCCO_GEMD.shx", shx);
            Add(archive, "TL_SCCO_GEMD.dbf", dbf);
            if (includeCpg)
                Add(archive, "TL_SCCO_GEMD.cpg", Encoding.ASCII.GetBytes("UTF-8"));
            if (duplicateShp)
                Add(archive, "duplicate.shp", shp);
        }
        return output.ToArray();
    }

    private static (byte[] Shp, byte[] Shx) CreateShapeFiles(IReadOnlyList<ShapeFeature> features)
    {
        var contents = features.Select(CreateShapeContent).ToArray();
        var allPoints = features.SelectMany(feature => feature.Parts).SelectMany(part => part).ToArray();
        var bounds = Bounds(allPoints);
        var shpLength = 100 + contents.Sum(content => 8 + content.Length);
        var shp = new byte[shpLength];
        WriteShapeHeader(shp, bounds);
        var shpOffset = 100;
        var index = new List<(int OffsetWords, int ContentLengthWords)>(contents.Length);
        for (var recordIndex = 0; recordIndex < contents.Length; recordIndex++)
        {
            var content = contents[recordIndex];
            index.Add((shpOffset / 2, content.Length / 2));
            WriteBigEndian(shp.AsSpan(shpOffset, 4), recordIndex + 1);
            WriteBigEndian(shp.AsSpan(shpOffset + 4, 4), content.Length / 2);
            content.CopyTo(shp, shpOffset + 8);
            shpOffset += 8 + content.Length;
        }

        var shx = new byte[100 + index.Count * 8];
        WriteShapeHeader(shx, bounds);
        for (var recordIndex = 0; recordIndex < index.Count; recordIndex++)
        {
            var offset = 100 + recordIndex * 8;
            WriteBigEndian(shx.AsSpan(offset, 4), index[recordIndex].OffsetWords);
            WriteBigEndian(shx.AsSpan(offset + 4, 4), index[recordIndex].ContentLengthWords);
        }
        return (shp, shx);
    }

    private static byte[] CreateShapeContent(ShapeFeature feature)
    {
        var points = feature.Parts.SelectMany(part => part).ToArray();
        var bounds = Bounds(points);
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(feature.ShapeType);
        writer.Write(bounds.MinX);
        writer.Write(bounds.MinY);
        writer.Write(bounds.MaxX);
        writer.Write(bounds.MaxY);
        writer.Write(feature.Parts.Length);
        writer.Write(points.Length);
        var pointOffset = 0;
        foreach (var part in feature.Parts)
        {
            writer.Write(pointOffset);
            pointOffset += part.Length;
        }
        foreach (var point in points)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
        }
        return output.ToArray();
    }

    private static byte[] CreateDbf(IReadOnlyList<ShapeFeature> features, Encoding encoding)
    {
        const int codeLength = 10;
        const int nameLength = 60;
        const int fieldCount = 2;
        const int headerLength = 32 + fieldCount * 32 + 1;
        const int recordLength = 1 + codeLength + nameLength;
        var bytes = new byte[headerLength + features.Count * recordLength + 1];
        bytes[0] = 0x03;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), features.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), headerLength);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10, 2), recordLength);
        WriteDbfField(bytes.AsSpan(32, 32), "EMD_CD", codeLength);
        WriteDbfField(bytes.AsSpan(64, 32), "EMD_KOR_NM", nameLength);
        bytes[headerLength - 1] = 0x0d;

        for (var index = 0; index < features.Count; index++)
        {
            var offset = headerLength + index * recordLength;
            bytes[offset] = features[index].Deleted ? (byte)'*' : (byte)' ';
            WriteDbfValue(bytes.AsSpan(offset + 1, codeLength), features[index].Code, Encoding.ASCII);
            WriteDbfValue(bytes.AsSpan(offset + 1 + codeLength, nameLength), features[index].Name, encoding);
        }
        bytes[^1] = 0x1a;
        return bytes;
    }

    private static void WriteShapeHeader(
        Span<byte> destination,
        (double MinX, double MinY, double MaxX, double MaxY) bounds)
    {
        WriteBigEndian(destination[..4], 9994);
        WriteBigEndian(destination.Slice(24, 4), destination.Length / 2);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(28, 4), 1000);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(32, 4), 5);
        WriteDouble(destination.Slice(36, 8), bounds.MinX);
        WriteDouble(destination.Slice(44, 8), bounds.MinY);
        WriteDouble(destination.Slice(52, 8), bounds.MaxX);
        WriteDouble(destination.Slice(60, 8), bounds.MaxY);
    }

    private static void WriteDbfField(Span<byte> destination, string name, byte length)
    {
        destination.Clear();
        Encoding.ASCII.GetBytes(name).CopyTo(destination);
        destination[11] = (byte)'C';
        destination[16] = length;
    }

    private static void WriteDbfValue(Span<byte> destination, string value, Encoding encoding)
    {
        destination.Fill((byte)' ');
        var encoded = encoding.GetBytes(value);
        Assert.True(encoded.Length <= destination.Length, "Synthetic DBF value exceeds its field length.");
        encoded.CopyTo(destination);
    }

    private static void Add(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(
        IReadOnlyList<(double X, double Y)> points)
        => (points.Min(point => point.X), points.Min(point => point.Y),
            points.Max(point => point.X), points.Max(point => point.Y));

    private static void WriteBigEndian(Span<byte> destination, int value)
        => BinaryPrimitives.WriteInt32BigEndian(destination, value);

    private static void WriteDouble(Span<byte> destination, double value)
        => BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(value));

    private sealed record ShapeFeature(
        string Code,
        string Name,
        (double X, double Y)[][] Parts,
        bool Deleted = false,
        int ShapeType = 5);
}
