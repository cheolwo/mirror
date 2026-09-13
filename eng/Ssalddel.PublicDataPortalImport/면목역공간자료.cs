using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 면목역 중심 1km 표현 사본과 그 계보만 로컬 비공개 검토 원본으로 등록한다.
// 행정동 current 투영은 행정동 단일 key이므로 역 중심 좌표계를 게시하면 기존 사본을 덮어쓴다.
// 따라서 station-scoped store/contract가 생기기 전에는 Mongo/API/Unity 게시를 시도하지 않는다.
internal static class 면목역공간자료
{
    private const string RawFolder =
        "artifacts/local/neighborhood-source-acquisition/myeonmok-station-0721-r1";
    private const string SnapshotFolder =
        "artifacts/local/myeonmok-station-spatial-snapshot";
    private const string RawRelative = RawFolder + "/map.osm";
    private const string ReceiptRelative = RawFolder + "/receipt.json";
    private const string SnapshotRelative = SnapshotFolder + "/private-review.json";
    private const string AuditRelative = SnapshotFolder + "/coverage-audit.json";

    private const string RawHash = "BD218CB2D9CC49F996DB7BCF2384F6950CCEAC772D5322987879AA21570D0A66";
    private const string ReceiptHash = "7ABEC1DC9B23077244675A37290F1EE90A8088F47BD5EB76C4D00FB39BC11FFD";
    private const string SnapshotFileHash = "4B8D45AE3414218228FEB1436AE8E99AB3999C83A30607292AC594C0099530B1";
    private const string AuditFileHash = "271DF7BA344128A5F04D4CB7E170E430B9512172D7E0DD0BE984E528D607C8CA";
    private const string SnapshotContentHash = "48FED5BFA9B06075B27D7DCF22EFDF3AE3F98222D337D0298C01C098A62B913A";
    private const string AuditContentHash = "14443C09D65ABA18CCF9D9369136EB0B839584AB29C9F887E28D38B1A17DFC24";
    private const string StationStableId = "station:kr:kric:s1107:0721";
    private const string SnapshotRevision = "myeonmok-station-spatial-snapshot.private-review.r1";

    private static readonly string[] AdministrativeAreaStableIds =
    [
        "region:kr:hjd:1126052000",
        "region:kr:hjd:1126055000",
        "region:kr:hjd:1126056500",
        "region:kr:hjd:1126057500",
        "region:kr:hjd:1126059000",
        "region:kr:hjd:1126066000"
    ];

    private sealed record Registration(
        string Path,
        string RelativePath,
        string ExpectedHash,
        long ExpectedLength,
        string SourceId,
        string DatasetId,
        string SourceVersion,
        string DataRevision,
        DateTimeOffset? EvidenceAsOfUtc,
        string ContentType,
        string Summary);

    internal static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "MyeonmokStationSpatialModeInvalid");
        result["mode"] = mode;
        result["transitStationStableId"] = StationStableId;
        result["reviewStatus"] = "LocalPrivateReview";
        result["distributionApproved"] = false;
        result["gameplayReady"] = false;
        result["traversalReady"] = false;
        result["missingCoverageRetained"] = true;
        result["administrativeProjectionPublished"] = false;
        result["administrativeProjectionReason"] =
            "StationScopedProjectionStoreContractRequired;AdministrativeAreaCurrentProjectionMustNotBeOverwritten";
        result["buildingPartConversion"] =
            "NotAttempted;AllPartsAndHolesRetainedOnlyInFrozenRawSnapshot";
        result["roadSegmentConversion"] =
            "NotAttempted;AllPolylinePartsRetainedOnlyInFrozenRawSnapshot";

        var rawPath = SafeRead(root, RawRelative, 5 * 1024 * 1024);
        var receiptPath = SafeRead(root, ReceiptRelative, 128 * 1024);
        var snapshotPath = SafeRead(root, SnapshotRelative, 20 * 1024 * 1024);
        var auditPath = SafeRead(root, AuditRelative, 1024 * 1024);
        Require(Hash(rawPath) == RawHash, "MyeonmokStationOsmHashChanged");
        Require(Hash(receiptPath) == ReceiptHash, "MyeonmokStationReceiptHashChanged");
        Require(Hash(snapshotPath) == SnapshotFileHash, "MyeonmokStationSnapshotFileHashChanged");
        Require(Hash(auditPath) == AuditFileHash, "MyeonmokStationAuditFileHashChanged");

        using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(receiptPath));
        using var snapshot = JsonDocument.Parse(await File.ReadAllBytesAsync(snapshotPath));
        using var audit = JsonDocument.Parse(await File.ReadAllBytesAsync(auditPath));
        var fetchedAt = ValidateReceipt(receipt.RootElement, new FileInfo(rawPath));
        var counts = ValidateSnapshot(snapshot.RootElement);
        ValidateAudit(audit.RootElement, counts);

        result["snapshotContentHash"] = SnapshotContentHash;
        result["auditHash"] = AuditContentHash;
        result["buildingCount"] = counts.Buildings;
        result["roadCount"] = counts.Roads;
        result["surfaceCount"] = counts.Surfaces;
        result["administrativeAreaCount"] = counts.AdministrativeAreas;
        result["tileCount"] = counts.Tiles;
        result["coverageCellCount"] = counts.CoverageCells;
        result["missingCoverageCellCount"] = counts.MissingCoverageCells;
        result["buildingPartCount"] = counts.BuildingParts;
        result["buildingPartsWithHoles"] = counts.BuildingPartsWithHoles;
        result["roadPartCount"] = counts.RoadParts;
        result["roadLineSegmentCount"] = counts.RoadLineSegments;

        if (mode == "self-test")
        {
            using var canonicalFixture = JsonDocument.Parse("{\"b\":2,\"contentHash\":\"changed\",\"a\":1}");
            Require(Encoding.UTF8.GetString(CanonicalJson(canonicalFixture.RootElement, "contentHash"))
                    == "{\"a\":1,\"b\":2,\"contentHash\":\"\"}",
                "MyeonmokStationCanonicalJsonFailed");
            Require(counts.Buildings == 4165 && counts.Roads == 124 && counts.Surfaces == 48
                    && counts.AdministrativeAreas == 6 && counts.Tiles == 4
                    && counts.CoverageCells == 100 && counts.MissingCoverageCells == 2
                    && counts.BuildingParts == 4166 && counts.BuildingPartsWithHoles == 1
                    && counts.RoadParts == 124 && counts.RoadLineSegments == 242,
                "MyeonmokStationFixtureCountsChanged");
            result["selfTestsPassed"] = 18;
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            return;
        }

        var registrations = Registrations(
            rawPath, receiptPath, snapshotPath, auditPath, fetchedAt).ToArray();
        var options = await 로컬공공자료Db.OptionsAsync(root);
        if (mode == "apply")
            await ApplyAsync(options, registrations, result);
        else
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
        }

        var verified = await VerifyReadbackAsync(options, registrations, mode == "verify" || mode == "apply");
        result["existingSourceCount"] = verified.Length;
        result["rawSnapshotIds"] = verified.Select(item => item.Id).OrderBy(id => id).ToArray();
        result["independentReadbackVerified"] = mode == "apply" || mode == "verify";
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
    }

    private static DateTimeOffset ValidateReceipt(JsonElement root, FileInfo raw)
    {
        Require(root.GetProperty("schemaVersion").GetString() == "station-osm-source-acquisition.v1",
            "MyeonmokStationReceiptSchemaChanged");
        Require(root.GetProperty("transitStationStableId").GetString() == StationStableId,
            "MyeonmokStationReceiptIdentityChanged");
        Require(root.GetProperty("scope").GetString() == "SingleOfficialApiBboxGet",
            "MyeonmokStationReceiptScopeChanged");
        Require(root.GetProperty("sourceUrl").GetString() ==
                "https://api.openstreetmap.org/api/0.6/map?bbox=127.0818422,37.5841659,127.0931645,37.5931758",
            "MyeonmokStationReceiptUrlChanged");
        var fetchedAt = root.GetProperty("fetchedAtUtc").GetDateTimeOffset();
        var rawReceipt = root.GetProperty("raw");
        Require(rawReceipt.GetProperty("file").GetString() == "map.osm"
                && rawReceipt.GetProperty("byteLength").GetInt64() == raw.Length
                && rawReceipt.GetProperty("sha256").GetString() == RawHash
                && rawReceipt.GetProperty("format").GetString() == "OSM XML 0.6"
                && rawReceipt.GetProperty("originalCrs").GetString() == "EPSG:4326",
            "MyeonmokStationReceiptRawChanged");
        var policy = root.GetProperty("policy");
        Require(policy.GetProperty("reviewStatus").GetString() == "LocalPrivateReview"
                && !policy.GetProperty("distributionApproved").GetBoolean()
                && !policy.GetProperty("gameplayReady").GetBoolean()
                && !policy.GetProperty("traversalReady").GetBoolean()
                && !policy.GetProperty("periodicAcquisition").GetBoolean()
                && policy.GetProperty("relationMembersMayBeIncomplete").GetBoolean(),
            "MyeonmokStationReceiptPolicyChanged");
        Require(root.GetProperty("license").GetProperty("code").GetString() == "ODbL-1.0",
            "MyeonmokStationOsmLicenseChanged");
        return fetchedAt;
    }

    private static (int Buildings, int Roads, int Surfaces, int AdministrativeAreas, int Tiles,
        int CoverageCells, int MissingCoverageCells, int BuildingParts, int BuildingPartsWithHoles,
        int RoadParts, int RoadLineSegments) ValidateSnapshot(JsonElement root)
    {
        Require(root.GetProperty("schemaVersion").GetString() == "ssalddel.station-spatial-snapshot.v1",
            "MyeonmokStationSnapshotSchemaChanged");
        Require(root.GetProperty("revision").GetString() == SnapshotRevision
                && root.GetProperty("contentHash").GetString() == SnapshotContentHash
                && Hash(CanonicalJson(root, "contentHash")) == SnapshotContentHash,
            "MyeonmokStationSnapshotContentHashMismatch");
        Require(root.GetProperty("status").GetString() == "LocalPrivateReview"
                && root.GetProperty("transitStationStableId").GetString() == StationStableId
                && root.GetProperty("presentationOnly").GetBoolean()
                && !root.GetProperty("distributionApproved").GetBoolean()
                && !root.GetProperty("gameplayReady").GetBoolean()
                && !root.GetProperty("traversalReady").GetBoolean(),
            "MyeonmokStationSnapshotBoundaryChanged");
        var window = root.GetProperty("window");
        Require(window.GetProperty("widthMeters").GetInt32() == 1000
                && window.GetProperty("depthMeters").GetInt32() == 1000,
            "MyeonmokStationSnapshotWindowChanged");
        var frame = root.GetProperty("coordinateFrame");
        Require(frame.GetProperty("originLatitude").GetDouble() == 37.588671d
                && frame.GetProperty("originLongitude").GetDouble() == 127.087503d
                && frame.GetProperty("metersPerUnit").GetDouble() == 1d
                && frame.GetProperty("halfExtentMeters").GetDouble() == 500d,
            "MyeonmokStationSnapshotFrameChanged");

        var buildings = root.GetProperty("buildings").GetArrayLength();
        var roads = root.GetProperty("roads").GetArrayLength();
        var surfaces = root.GetProperty("surfaces").GetArrayLength();
        var buildingParts = root.GetProperty("buildings").EnumerateArray()
            .SelectMany(item => item.GetProperty("parts").EnumerateArray()).ToArray();
        var roadParts = root.GetProperty("roads").EnumerateArray()
            .SelectMany(item => item.GetProperty("parts").EnumerateArray()).ToArray();
        var buildingPartsWithHoles = buildingParts.Count(item =>
            item.GetProperty("holes").GetArrayLength() > 0);
        var roadLineSegments = roadParts.Sum(item =>
            Math.Max(0, item.GetProperty("points").GetArrayLength() - 1));
        var areas = root.GetProperty("administrativeAreas").EnumerateArray().ToArray();
        Require(areas.Select(item => item.GetProperty("administrativeAreaStableId").GetString())
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(AdministrativeAreaStableIds, StringComparer.Ordinal)
                && areas.Count(item => item.GetProperty("containsStation").GetBoolean()) == 1
                && areas.Single(item => item.GetProperty("containsStation").GetBoolean())
                    .GetProperty("administrativeAreaStableId").GetString() == "region:kr:hjd:1126056500",
            "MyeonmokStationAdministrativeCoverageChanged");

        var tiles = root.GetProperty("tiles").EnumerateArray().ToArray();
        Require(tiles.Length == 4
                && tiles.Select(item => (item.GetProperty("tileIndexX").GetInt32(),
                        item.GetProperty("tileIndexZ").GetInt32()))
                    .ToHashSet().SetEquals([(-1, -1), (-1, 0), (0, -1), (0, 0)]),
            "MyeonmokStationTilesChanged");
        foreach (var tile in tiles)
            Require(tile.GetProperty("tileHash").GetString() == Hash(CanonicalJson(tile, "tileHash")),
                "MyeonmokStationTileHashMismatch:" + tile.GetProperty("tileStableId").GetString());

        var receipts = root.GetProperty("sourceReceipts").EnumerateArray().ToArray();
        Require(receipts.Length == 5
                && receipts.All(item => item.GetProperty("reviewStatus").GetString() == "LocalPrivateReview"
                                        && !item.GetProperty("distributionApproved").GetBoolean())
                && receipts.Select(item => item.GetProperty("sourceId").GetString()).ToHashSet(StringComparer.Ordinal)
                    .IsSupersetOf(new[]
                    {
                        "kric:urban-rail-stations",
                        "vworld:gis-building-integrated-information",
                        "data-go-kr:standard-nodelink",
                        "seoul-open-data:administrative-dong-boundary",
                        "openstreetmap:myeonmok-station-0721-r1"
                    }),
            "MyeonmokStationSourceReviewBoundaryChanged");
        var osm = receipts.Single(item => item.GetProperty("sourceId").GetString()
                                           == "openstreetmap:myeonmok-station-0721-r1");
        Require(osm.GetProperty("rawHash").GetString() == RawHash
                && osm.GetProperty("licenseCode").GetString() == "ODbL-1.0",
            "MyeonmokStationOsmAttributionChanged");

        var coverageCells = root.GetProperty("coverage").GetProperty("cells").EnumerateArray().ToArray();
        var missingCellCount = coverageCells.Count(item => item.GetProperty("classification").GetString()
                                                            == "MissingCoverage");
        var missingCodes = root.GetProperty("missingCoverage").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString()).ToHashSet(StringComparer.Ordinal);
        Require(new[]
            {
                "BuildingRightsConflictUnresolved", "LegalDongBoundaryCoverageUnverified",
                "RoadWidthUnavailable", "SurfaceCoverageIncomplete", "TraversalAuthorityUnavailable"
            }.All(missingCodes.Contains),
            "MyeonmokStationRequiredMissingCoverageRemoved");
        return (buildings, roads, surfaces, areas.Length, tiles.Length, coverageCells.Length,
            missingCellCount, buildingParts.Length, buildingPartsWithHoles, roadParts.Length, roadLineSegments);
    }

    private static void ValidateAudit(
        JsonElement root,
        (int Buildings, int Roads, int Surfaces, int AdministrativeAreas, int Tiles,
            int CoverageCells, int MissingCoverageCells, int BuildingParts, int BuildingPartsWithHoles,
            int RoadParts, int RoadLineSegments) counts)
    {
        Require(root.GetProperty("schemaVersion").GetString() == "ssalddel.station-spatial-snapshot-audit.v1"
                && root.GetProperty("status").GetString() == "PassedWithMissingCoverage"
                && root.GetProperty("snapshotRevision").GetString() == SnapshotRevision
                && root.GetProperty("snapshotContentHash").GetString() == SnapshotContentHash
                && root.GetProperty("auditHash").GetString() == AuditContentHash
                && Hash(CanonicalJson(root, "auditHash")) == AuditContentHash,
            "MyeonmokStationAuditChanged");
        var observed = root.GetProperty("observed");
        Require(observed.GetProperty("buildings").GetInt32() == counts.Buildings
                && observed.GetProperty("roads").GetInt32() == counts.Roads
                && observed.GetProperty("surfaces").GetInt32() == counts.Surfaces
                && observed.GetProperty("administrativeAreas").GetInt32() == counts.AdministrativeAreas
                && observed.GetProperty("tiles").GetInt32() == counts.Tiles
                && observed.GetProperty("coverageCells").GetInt32() == counts.CoverageCells,
            "MyeonmokStationAuditCountsChanged");
        Require(root.GetProperty("missingCoverageCodes").EnumerateArray()
                .Any(item => item.GetString() == "SurfaceCoverageIncomplete")
                && counts.MissingCoverageCells > 0,
            "MyeonmokStationAuditMissingCoverageRemoved");
    }

    private static IEnumerable<Registration> Registrations(
        string rawPath,
        string receiptPath,
        string snapshotPath,
        string auditPath,
        DateTimeOffset fetchedAt)
    {
        var fetched = fetchedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        yield return new(rawPath, RawRelative, RawHash, 1_503_543,
            "openstreetmap:myeonmok-station-0721-r1", "osm-api-0.6-map-bbox",
            "fetched:" + fetched + ";sha256:" + RawHash.ToLowerInvariant(),
            "station-osm-source-acquisition.r1", fetchedAt, "application/xml",
            "Single bounded OSM API response; relation members may be incomplete; no traversal or gameplay authority.");
        yield return new(receiptPath, ReceiptRelative, ReceiptHash, 1_189,
            "openstreetmap:myeonmok-station-0721-r1", "osm-api-0.6-map-bbox-receipt",
            "fetched:" + fetched + ";raw-sha256:" + RawHash.ToLowerInvariant(),
            "station-osm-source-acquisition.r1", fetchedAt, "application/json",
            "Acquisition receipt only; ODbL attribution retained; distribution remains unapproved.");
        yield return new(snapshotPath, SnapshotRelative, SnapshotFileHash, 10_111_746,
            "derived:ssalddel-station-spatial-snapshot", "station:kr:kric:s1107:0721:one-kilometer",
            "content-sha256:" + SnapshotContentHash.ToLowerInvariant(), SnapshotRevision,
            null, "application/json",
            "Station-centered presentation snapshot; MissingCoverage retained; no publication, gameplay or traversal authority.");
        yield return new(auditPath, AuditRelative, AuditFileHash, 4_201,
            "derived:ssalddel-station-spatial-snapshot-audit", "station:kr:kric:s1107:0721:one-kilometer",
            "snapshot-content-sha256:" + SnapshotContentHash.ToLowerInvariant(),
            "myeonmok-station-spatial-snapshot-audit.r1", null, "application/json",
            "Deterministic rebuild audit with unresolved coverage; private review only.");
    }

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Registration[] registrations,
        Dictionary<string, object?> result)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK('mirror:public-data:myeonmok-station-spatial-r1',0)";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1,
            "MyeonmokStationSpatialImportBusy");
        await using var transaction = await db.Database.BeginTransactionAsync();
        result["databaseWriteAttempted"] = true;
        var service = new 평창군공공공간원본등록Service(db);
        var inserted = 0;
        var ids = new List<long>();
        foreach (var item in registrations)
        {
            var registration = await service.RegisterFileAsync(item.Path, new 공공공간원본등록Request(
                item.SourceId, item.DatasetId, item.SourceVersion, item.DataRevision,
                item.EvidenceAsOfUtc, item.ContentType, item.RelativePath));
            Require(registration.SourceHashSha256 == item.ExpectedHash.ToLowerInvariant()
                    && registration.ContentLength == item.ExpectedLength,
                "MyeonmokStationRegistrationChanged:" + item.DatasetId);
            ids.Add(registration.RawSnapshotId);
            if (registration.Inserted)
            {
                inserted++;
                var raw = await db.RawSnapshots.SingleAsync(value => value.Id == registration.RawSnapshotId);
                if (item.EvidenceAsOfUtc.HasValue)
                    raw.CollectedAtUtc = item.EvidenceAsOfUtc.Value;
                var run = await db.IngestionRuns.SingleAsync(value => value.Id == raw.FirstCollectionRunId);
                run.StatusCode = 외부데이터수집StatusCodes.Partial;
                run.ErrorCode = "PendingHumanReview";
                run.ErrorSummary = item.Summary;
                run.NormalizedCount = 0;
                await db.SaveChangesAsync();
            }
            var repeated = await service.RegisterFileAsync(item.Path, new 공공공간원본등록Request(
                item.SourceId, item.DatasetId, item.SourceVersion, item.DataRevision,
                item.EvidenceAsOfUtc, item.ContentType, item.RelativePath));
            Require(!repeated.Inserted && repeated.RawSnapshotId == registration.RawSnapshotId,
                "MyeonmokStationRegistrationIdempotencyFailed:" + item.DatasetId);
        }
        Require(ids.Distinct().Count() == registrations.Length, "MyeonmokStationRegistrationIdsNotDistinct");
        await transaction.CommitAsync();
        command.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:myeonmok-station-spatial-r1')";
        _ = await command.ExecuteScalarAsync();
        result["committed"] = true;
        result["insertedSourceCount"] = inserted;
        result["existingSourceCountBeforeReapply"] = registrations.Length - inserted;
        result["idempotentReapplyVerified"] = true;
    }

    private static async Task<외부데이터RawSnapshot[]> VerifyReadbackAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Registration[] registrations,
        bool required)
    {
        await using var independent = new PublicDataIngestionDbContext(options);
        var matched = new List<외부데이터RawSnapshot>();
        foreach (var item in registrations)
        {
            var expectedHash = item.ExpectedHash.ToLowerInvariant();
            var candidates = await independent.RawSnapshots.AsNoTracking()
                .Include(row => row.FirstCollectionRun)
                .Where(row => row.SourceId == item.SourceId
                              && row.DatasetId == item.DatasetId
                              && row.ContentHashSha256 == expectedHash)
                .ToArrayAsync();
            if (!required && candidates.Length == 0) continue;
            Require(candidates.Length == 1, "MyeonmokStationReadbackCountMismatch:" + item.DatasetId);
            var row = candidates[0];
            Require(row.ContentLength == item.ExpectedLength
                    && row.ContentType == item.ContentType
                    && row.SourceVersion == item.SourceVersion
                    && row.StorageContainer == "local-private-public-spatial"
                    && row.StorageObjectName == item.RelativePath
                    && row.StorageLocation == "private-file://" + item.RelativePath
                    && row.FirstCollectionRun is not null
                    && row.FirstCollectionRun.DataRevision == item.DataRevision
                    && row.FirstCollectionRun.StatusCode == 외부데이터수집StatusCodes.Partial
                    && row.FirstCollectionRun.ErrorCode == "PendingHumanReview",
                "MyeonmokStationReadbackMismatch:" + item.DatasetId);
            matched.Add(row);
        }
        if (required) Require(matched.Count == registrations.Length, "MyeonmokStationReadbackIncomplete");
        return matched.ToArray();
    }

    private static string SafeRead(string root, string relative, long maxBytes)
    {
        var repository = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var local = Path.Combine(repository, "artifacts", "local") + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(repository, relative));
        Require(path.StartsWith(local, StringComparison.OrdinalIgnoreCase), "MyeonmokStationPathOutsideLocalArtifacts");
        for (var directory = new FileInfo(path).Directory; directory is not null; directory = directory.Parent)
            if (directory.Exists)
                Require(!directory.Attributes.HasFlag(FileAttributes.ReparsePoint), "MyeonmokStationReparsePointRejected");
        var file = new FileInfo(path);
        Require(file.Exists && file.Length is > 0 && file.Length <= maxBytes
                && !file.Attributes.HasFlag(FileAttributes.ReparsePoint),
            "MyeonmokStationInputMissingOrInvalid:" + Path.GetFileName(path));
        return path;
    }

    private static byte[] CanonicalJson(JsonElement element, string rootHashProperty)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                   Indented = false
               }))
        {
            WriteCanonical(writer, element, rootHashProperty, true);
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteCanonical(
        Utf8JsonWriter writer,
        JsonElement element,
        string rootHashProperty,
        bool root)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    if (root && property.Name == rootHashProperty)
                        writer.WriteStringValue(string.Empty);
                    else
                        WriteCanonical(writer, property.Value, rootHashProperty, false);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item, rootHashProperty, false);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), false);
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
                throw new InvalidDataException("MyeonmokStationCanonicalJsonKindInvalid");
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes));

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
