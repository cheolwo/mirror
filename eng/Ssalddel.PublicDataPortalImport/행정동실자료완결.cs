using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;

// 면목동 법정동 공간 후보를 서울시 공식 6개 행정동 경계로 전수 판정한다.
// OSM 후보를 공식 건축물대장으로 승격하지 않으며, 좌표가 있는 상권 관측도 표시 승인을 만들지 않는다.
internal static class 행정동실자료완결
{
    private const string BoundaryRelative = "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip";
    private const string ArtifactRelative = "artifacts/local/public-data/admin-dong-diorama-20260912-r1";
    private const string UnityMapRelative = "Assets/Ssalddel/Resources/SagajeongReference.json";
    private const string BoundaryHash = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68";
    private const string MapHash = "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
    private const string BoundaryVintage = "seoul-oa22160:updated:20260611:retrieved:20260912";
    private const string LegalArea = "region:kr:bjd:1126010100";
    private const string TargetArea = "region:kr:hjd:1126057500";
    private const string AuditCollection = "administrative_dong_diorama_assignment_audits";
    private const string AuditCurrentCollection = "administrative_dong_diorama_assignment_audit_current";
    private static readonly string[] AdministrativeAreas =
    [
        "region:kr:hjd:1126052000", "region:kr:hjd:1126054000", "region:kr:hjd:1126055000",
        "region:kr:hjd:1126056500", "region:kr:hjd:1126057000", TargetArea
    ];

    internal static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        const string batchPrefix = "batch-";
        if (mode.StartsWith(batchPrefix, StringComparison.Ordinal))
        {
            await 행정동디오라마Batch.RunAsync(mode[batchPrefix.Length..], root, result);
            return;
        }

        result["stage"] = "ValidateArguments";
        Require(mode is "preview" or "apply" or "verify", "AdministrativeDongDioramaModeInvalid");
        var boundaryPath = Path.Combine(root, BoundaryRelative);
        var unityRoot = Environment.GetEnvironmentVariable("SSALDDEL_UNITY_ROOT");
        if (string.IsNullOrWhiteSpace(unityRoot))
            unityRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ssalddel");
        var mapPath = Path.Combine(Path.GetFullPath(unityRoot), UnityMapRelative);
        SafeRead(boundaryPath, root, Path.GetFullPath(unityRoot), 8 * 1024 * 1024);
        SafeRead(mapPath, root, Path.GetFullPath(unityRoot), 4 * 1024 * 1024);
        Require(HashFile(boundaryPath) == BoundaryHash, "AdministrativeBoundaryInputHashChanged");
        Require(HashFile(mapPath) == MapHash, "SagajeongMapInputHashChanged");

        result["stage"] = "ReadFrozenSources";
        using var mapDocument = JsonDocument.Parse(await File.ReadAllBytesAsync(mapPath));
        var map = mapDocument.RootElement;
        Require(map.GetProperty("revision").GetString() == "sagajeong-reference.r3", "SagajeongMapRevisionChanged");
        var frame = new AdministrativeDongDioramaCoordinateFrame
        {
            Method = map.GetProperty("coordinateMethod").GetString() ?? string.Empty,
            OriginLatitude = map.GetProperty("originLatitude").GetDouble(),
            OriginLongitude = map.GetProperty("originLongitude").GetDouble(),
            WorldOffsetX = map.GetProperty("offsetX").GetDouble(),
            WorldOffsetZ = map.GetProperty("offsetZ").GetDouble(),
            MetersPerUnit = 1d
        };
        result["stage"] = "ReadAdministrativeBoundaries";
        var boundaryBytes = await File.ReadAllBytesAsync(boundaryPath);
        var boundarySnapshots = 행정동경계ShapefileZipReader.Read(
            boundaryBytes, AdministrativeAreas, BoundaryVintage, frame);
        var boundaries = boundarySnapshots.Select(item =>
            new 행정동공간Boundary(item.AdministrativeAreaStableId, item.Boundary)).ToArray();
        result["stage"] = "ReadSpatialCandidates";
        var buildings = ReadBuildings(map).ToArray();
        var roads = ReadRoads(map).ToArray();
        Require(buildings.Length == 602 && roads.Length == 2397, "SagajeongMapCountChanged");
        var assignments = 행정동공간귀속Classifier.AssignBuildings(buildings, boundaries);

        result["stage"] = "ReadAdministrativeCodeLedger";
        await using var publicData = new Ssalddel.Infrastructure.Persistence.PublicData.PublicDataIngestionDbContext(
            await 로컬공공자료Db.OptionsAsync(root));
        var codeRows = (await publicData.NormalizedRecords.AsNoTracking()
                .Where(item => item.MetricCode == "geography.administrative-agency.name"
                               || item.MetricCode == "geography.administrative-legal-jurisdiction")
                .ToArrayAsync())
            .Where(item => AdministrativeAreas.Contains(item.RegionStableId, StringComparer.Ordinal))
            .OrderBy(item => item.RegionStableId, StringComparer.Ordinal).ThenBy(item => item.MetricCode, StringComparer.Ordinal)
            .ToArray();
        foreach (var area in AdministrativeAreas)
        {
            var rows = codeRows.Where(item => item.RegionStableId == area).ToArray();
            Require(rows.Length == 2, "AdministrativeCodeLedgerIncomplete:" + area);
            Require(rows.Any(item => item.MetricCode == "geography.administrative-legal-jurisdiction"
                                     && item.DimensionKey.Contains("legal=" + LegalArea, StringComparison.Ordinal)),
                "AdministrativeLegalCrosswalkMismatch:" + area);
        }
        var codeVersions = codeRows.Select(item => item.SourceVersion).Distinct(StringComparer.Ordinal).ToArray();
        Require(codeVersions.Length == 1 && codeVersions[0].StartsWith("mois-jscode:20260301:", StringComparison.Ordinal),
            "AdministrativeCodeVintageMismatch");
        var codeHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", codeRows.Select(item => string.Join("|",
            item.RegionStableId, item.MetricCode, item.TextValue, item.DimensionKey, item.SourceVersion)))));

        result["stage"] = "ReadPublicBusinessObservations";
        var publicBusinesses = await publicData.NormalizedRecords.AsNoTracking()
            .Where(item => item.SourceId == "semas-commercial-listing"
                           && item.DatasetId == "data-go-kr-15083033"
                           && item.RegionStableId == LegalArea
                           && item.MetricCode == "business-shop")
            .OrderBy(item => item.StableId).ToArrayAsync();
        var businessAssignments = AssignBusinesses(publicBusinesses, boundaries, frame);
        result["stage"] = "BuildProjection";
        var auditHash = AuditHash(assignments, businessAssignments, codeHash);
        var targetBuildings = assignments.Assignments
            .Where(item => item.AdministrativeAreaStableId == TargetArea)
            .Select(item => item.BuildingStableId).ToHashSet(StringComparer.Ordinal);
        var targetBoundary = boundarySnapshots.Single(item => item.AdministrativeAreaStableId == TargetArea);
        var sources = new[]
        {
            new AdministrativeDongDioramaSourceAttribution
            {
                SourceId = "source:seoul-open-data", DatasetId = "OA-22160",
                SourceRevision = BoundaryVintage, ContentHashSha256 = BoundaryHash.ToLowerInvariant(),
                LicenseCode = "KOGL-Type1", LimitationCode = "AdministrativeBoundaryObservation;EPSG5181"
            },
            new AdministrativeDongDioramaSourceAttribution
            {
                SourceId = "source:openstreetmap", DatasetId = "sagajeong-reference",
                SourceRevision = "sagajeong-reference.r3", ContentHashSha256 = MapHash.ToLowerInvariant(),
                LicenseCode = "ODbL-1.0", LimitationCode = "CandidateGeometryOnly;NotOfficialBuildingRegister"
            },
            new AdministrativeDongDioramaSourceAttribution
            {
                SourceId = "source:mois-standard-codes", DatasetId = "korea-administrative-legal-crosswalk",
                SourceRevision = codeVersions[0], ContentHashSha256 = codeHash.ToLowerInvariant(),
                LicenseCode = "PublicOpenData", LimitationCode = "CodeAndJurisdictionOnly;NoBoundary"
            },
            new AdministrativeDongDioramaSourceAttribution
            {
                SourceId = "derived:administrative-assignment-audit", DatasetId = "myeonmok-six-administrative-dong-assignment",
                SourceRevision = "administrative-dong-assignment-audit.r1", ContentHashSha256 = auditHash.ToLowerInvariant(),
                LicenseCode = "DerivedMixedSources", LimitationCode = "ObservationPresentationOnly;DistributionNotApproved"
            }
        };
        var input = new 행정동디오라마ProjectionBuildInput
        {
            AdministrativeAreaStableId = TargetArea,
            DisplayName = targetBoundary.DisplayName,
            SourceVintage = BoundaryVintage,
            GeneratedAtUtc = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc),
            CoordinateFrame = frame,
            LegalAreaStableIds = [LegalArea],
            Sources = sources,
            Buildings = buildings.Where(item => targetBuildings.Contains(item.BuildingStableId)).ToArray(),
            Roads = roads,
            PublicBusinesses = [],
            UnresolvedBuildingCount = assignments.UnresolvedCount
        };
        var preview = 행정동디오라마ProjectionBuilder.Build(new 행정동디오라마ProjectionBuildInput
        {
            AdministrativeAreaStableId = input.AdministrativeAreaStableId, DisplayName = input.DisplayName,
            SourceVintage = input.SourceVintage, GeneratedAtUtc = input.GeneratedAtUtc, CoordinateFrame = input.CoordinateFrame,
            Boundary = targetBoundary.Boundary, LegalAreaStableIds = input.LegalAreaStableIds, Sources = input.Sources,
            Buildings = input.Buildings, Roads = input.Roads, PublicBusinesses = input.PublicBusinesses,
            UnresolvedBuildingCount = input.UnresolvedBuildingCount
        });
        Require(preview.Manifest.ReadinessCode == AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
            "AdministrativeDongDioramaNotReviewReady");
        Require(preview.Tiles.Sum(tile => tile.Buildings.Length) == targetBuildings.Count,
            "AdministrativeDongDioramaTargetBuildingCountMismatch");
        var audit = AuditDocument(assignments, businessAssignments, boundarySnapshots, codeVersions[0], codeHash, auditHash,
            buildings.Length, roads.Length, preview);

        result["stage"] = "FreezeInputs";
        var artifact = Path.Combine(root, ArtifactRelative);
        Directory.CreateDirectory(artifact);
        var frozen = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "administrative-dong-diorama-inputs.r1", boundaryPath = BoundaryRelative,
            boundarySha256 = BoundaryHash, mapPath = "unity:" + UnityMapRelative, mapSha256 = MapHash,
            administrativeAreas = AdministrativeAreas, legalAreaStableId = LegalArea, codeSourceVersion = codeVersions[0],
            codeRowsSha256 = codeHash, auditHash
        }, new JsonSerializerOptions { WriteIndented = true });
        공간자료CatalogImport.SaveExact(Path.Combine(artifact, "inputs.json"), frozen);

        result["stage"] = mode == "preview" ? "Complete" : "ConnectMongo";
        result["mode"] = mode;
        result["target"] = "hongdal-mysql-1 / hongdal_dev + hongdal-mongo-1 / ssalddel_dev";
        result["administrativeBoundaryAreas"] = boundarySnapshots.Count;
        result["sourceBuildings"] = buildings.Length;
        result["assignedBuildings"] = assignments.AssignedCount;
        result["unresolvedBuildings"] = assignments.UnresolvedCount;
        result["ambiguousBoundaryBuildings"] = assignments.AmbiguousBoundaryCount;
        result["buildingCountsByAdministrativeArea"] = AdministrativeAreas.ToDictionary(
            area => area,
            area => assignments.Assignments.Count(item => item.AdministrativeAreaStableId == area),
            StringComparer.Ordinal);
        result["sourceRoadSegments"] = roads.Length;
        result["targetClippedRoadSegments"] = preview.Tiles.Sum(tile => tile.Roads.Length);
        result["publicBusinessObservations"] = publicBusinesses.Length;
        result["businessCoordinatesAssigned"] = businessAssignments.Count(item => item.Area is not null);
        result["businessCoordinatesUnresolved"] = businessAssignments.Count(item => item.Area is null);
        result["businessCountsByAdministrativeArea"] = AdministrativeAreas.ToDictionary(
            area => area,
            area => businessAssignments.Count(item => item.Area == area),
            StringComparer.Ordinal);
        result["businessPresentationMarkers"] = 0;
        result["targetProjectionHash"] = preview.Manifest.ProjectionHashSha256;
        result["targetTiles"] = preview.Tiles.Length;
        result["assignmentAuditHash"] = auditHash;
        result["distributionApproved"] = false;
        result["gameStateConnected"] = false;
        result["unityExecuted"] = false;
        result["mySqlWriteAttempted"] = false;

        if (mode != "preview")
        {
            var connection = await 공간자료CatalogImport.ConnectAsync(root);
            var projectionStore = new Mongo행정동디오라마ProjectionStore(connection.Client, Options.Create(connection.Options));
            var publication = new 행정동디오라마PublicationService(projectionStore);
            if (mode == "apply")
            {
                result["databaseWriteAttempted"] = true;
                var request = new 행정동디오라마PublicationRequest
                {
                    FrozenAdministrativeBoundaryShapefileZip = boundaryBytes,
                    ExpectedBoundaryContentHashSha256 = BoundaryHash,
                    ProjectionInput = input
                };
                var published = await publication.PublishAsync(request, CancellationToken.None);
                Require(published.Manifest.ProjectionHashSha256 == preview.Manifest.ProjectionHashSha256,
                    "AdministrativeDongProjectionBuildDrift");
                await InsertAuditAsync(connection.Client, audit, auditHash);
                // 동일 입력 재적용이 새 사본이나 충돌을 만들지 않는지 같은 실행에서 확인한다.
                var repeated = await publication.PublishAsync(request, CancellationToken.None);
                await InsertAuditAsync(connection.Client, audit, auditHash);
                Require(repeated.Manifest.ProjectionHashSha256 == published.Manifest.ProjectionHashSha256,
                    "AdministrativeDongDioramaIdempotencyMismatch");
                result["idempotentReapplyVerified"] = true;
                result["committed"] = true;
            }
            await VerifyReadbackAsync(root, preview, auditHash, assignments, businessAssignments);
            result["independentReadbackVerified"] = true;
        }
        else
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
        }

        result["stage"] = "Complete";
        var report = JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions { WriteIndented = true });
        공간자료CatalogImport.SaveExact(Path.Combine(artifact,
            mode + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffff", CultureInfo.InvariantCulture) + ".json"), report);
    }

    private static IEnumerable<행정동디오라마BuildingInput> ReadBuildings(JsonElement map)
    {
        foreach (var item in map.GetProperty("buildings").EnumerateArray())
        {
            var footprint = item.GetProperty("points").EnumerateArray().Select(point => new AdministrativeDongDioramaPoint
            { X = point.GetProperty("x").GetDouble(), Z = point.GetProperty("z").GetDouble() }).ToArray();
            var levelsText = item.GetProperty("levelsText").GetString();
            int? levels = int.TryParse(levelsText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed : null;
            var area = PolygonArea(footprint);
            yield return new 행정동디오라마BuildingInput
            {
                BuildingStableId = item.GetProperty("id").GetString() ?? string.Empty,
                CategoryCode = Category(item.GetProperty("buildingKind").GetString()),
                EvidenceKindCode = "PublicOpenDataCandidate",
                AboveGroundFloorCount = levels,
                HeightMeters = item.GetProperty("height").GetDouble(),
                BuildingAreaSquareMeters = area,
                TotalFloorAreaSquareMeters = levels.HasValue ? area * levels.Value : 0d,
                Footprint = footprint
            };
        }
    }

    private static IEnumerable<행정동디오라마RoadInput> ReadRoads(JsonElement map)
    {
        foreach (var item in map.GetProperty("roads").EnumerateArray())
            yield return new 행정동디오라마RoadInput
            {
                RoadStableId = item.GetProperty("id").GetString() ?? string.Empty,
                From = new() { X = item.GetProperty("x1").GetDouble(), Z = item.GetProperty("z1").GetDouble() },
                To = new() { X = item.GetProperty("x2").GetDouble(), Z = item.GetProperty("z2").GetDouble() },
                EvidenceKindCode = "PublicOpenDataCandidate"
            };
    }

    private static (string StableId, string? Area, string Confidence)[] AssignBusinesses(
        IEnumerable<Ssalddel.Domain.PublicData.외부데이터정규화Record> rows,
        IReadOnlyList<행정동공간Boundary> boundaries,
        AdministrativeDongDioramaCoordinateFrame frame)
        => rows.Select(row =>
        {
            try
            {
                using var json = JsonDocument.Parse(row.TextValue);
                var root = json.RootElement;
                if (!double.TryParse(root.GetProperty("Longitude").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
                    || !double.TryParse(root.GetProperty("Latitude").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
                    return (row.StableId, (string?)null, "UnresolvedCoordinateMissing");
                var point = 행정동경계GeoJsonReader.ProjectWgs84(latitude, longitude, frame);
                var matches = boundaries.Where(area => 행정동디오라마ProjectionBuilder.PointInPolygon(point, area.Boundary)).ToArray();
                return matches.Length == 1
                    ? (row.StableId, (string?)matches[0].AdministrativeAreaStableId, "CoordinateWithinUniqueAdministrativeBoundary")
                    : (row.StableId, (string?)null, matches.Length == 0 ? "UnresolvedOutsideBoundarySet" : "UnresolvedOverlappingBoundaries");
            }
            catch (JsonException) { return (row.StableId, (string?)null, "UnresolvedCoordinateInvalid"); }
            catch (KeyNotFoundException) { return (row.StableId, (string?)null, "UnresolvedCoordinateMissing"); }
        }).OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();

    private static BsonDocument AuditDocument(
        행정동공간AssignmentResult buildingAssignments,
        (string StableId, string? Area, string Confidence)[] businesses,
        IReadOnlyList<행정동경계GeoJsonSnapshot> boundaries,
        string codeVersion,
        string codeHash,
        string auditHash,
        int sourceBuildingCount,
        int sourceRoadCount,
        행정동디오라마ProjectionBuildResult projection)
        => new()
        {
            ["_id"] = auditHash,
            ["schemaVersion"] = "administrative-dong-spatial-assignment-audit.v1",
            ["legalAreaStableId"] = LegalArea,
            ["targetAdministrativeAreaStableId"] = TargetArea,
            ["sourceVintage"] = BoundaryVintage,
            ["boundaryContentHashSha256"] = BoundaryHash.ToLowerInvariant(),
            ["spatialSourceHashSha256"] = MapHash.ToLowerInvariant(),
            ["administrativeCodeSourceVersion"] = codeVersion,
            ["administrativeCodeRowsHashSha256"] = codeHash,
            ["auditHashSha256"] = auditHash,
            ["observationPresentationOnly"] = true,
            ["distributionApproved"] = false,
            ["assignmentMethodCode"] = "PointOnSurface",
            ["sourceBuildingCount"] = sourceBuildingCount,
            ["assignedBuildingCount"] = buildingAssignments.AssignedCount,
            ["unresolvedBuildingCount"] = buildingAssignments.UnresolvedCount,
            ["ambiguousBoundaryBuildingCount"] = buildingAssignments.AmbiguousBoundaryCount,
            ["sourceRoadSegmentCount"] = sourceRoadCount,
            ["targetClippedRoadSegmentCount"] = projection.Tiles.Sum(tile => tile.Roads.Length),
            ["sourcePublicBusinessObservationCount"] = businesses.Length,
            ["assignedPublicBusinessCoordinateCount"] = businesses.Count(item => item.Area is not null),
            ["publicBusinessPresentationMarkerCount"] = 0,
            ["boundaryAreas"] = new BsonArray(boundaries.OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                .Select(item => new BsonDocument
                {
                    ["administrativeAreaStableId"] = item.AdministrativeAreaStableId,
                    ["displayName"] = item.DisplayName,
                    ["assignedBuildingCount"] = buildingAssignments.Assignments.Count(assignment => assignment.AdministrativeAreaStableId == item.AdministrativeAreaStableId),
                    ["assignedPublicBusinessCoordinateCount"] = businesses.Count(business => business.Area == item.AdministrativeAreaStableId)
                })),
            ["buildingAssignments"] = new BsonArray(buildingAssignments.Assignments.Select(item => new BsonDocument
                {
                    ["buildingStableId"] = item.BuildingStableId,
                    ["administrativeAreaStableId"] = item.AdministrativeAreaStableId is null ? BsonNull.Value : item.AdministrativeAreaStableId,
                    ["assignmentMethodCode"] = item.AssignmentMethodCode,
                    ["confidenceCode"] = item.ConfidenceCode
                })),
            ["publicBusinessAssignments"] = new BsonArray(businesses.Select(item => new BsonDocument
                {
                    ["businessObservationStableId"] = item.StableId,
                    ["administrativeAreaStableId"] = item.Area is null ? BsonNull.Value : item.Area,
                    ["assignmentMethodCode"] = "SourceCoordinateWithinBoundary",
                    ["confidenceCode"] = item.Confidence,
                    ["presentationApproved"] = false
                }))
        };

    private static async Task InsertAuditAsync(MongoClient client, BsonDocument audit, string auditHash)
    {
        var database = client.GetDatabase("ssalddel_dev");
        var collection = database.GetCollection<BsonDocument>(AuditCollection);
        try { await collection.InsertOneAsync(audit); }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", auditHash)).SingleAsync();
            Require(existing.Equals(audit), "AdministrativeAssignmentAuditImmutableConflict");
        }
        await database.GetCollection<BsonDocument>(AuditCurrentCollection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", LegalArea),
            new BsonDocument { ["_id"] = LegalArea, ["auditHashSha256"] = auditHash },
            new ReplaceOptions { IsUpsert = true });
    }

    private static async Task VerifyReadbackAsync(
        string root,
        행정동디오라마ProjectionBuildResult expected,
        string auditHash,
        행정동공간AssignmentResult assignments,
        (string StableId, string? Area, string Confidence)[] businesses)
    {
        var independent = await 공간자료CatalogImport.ConnectAsync(root);
        var store = new Mongo행정동디오라마ProjectionStore(independent.Client, Options.Create(independent.Options));
        var manifest = await store.FindManifestAsync(TargetArea, CancellationToken.None)
                       ?? throw new InvalidDataException("AdministrativeDongDioramaManifestReadbackMissing");
        Require(manifest.ProjectionHashSha256 == expected.Manifest.ProjectionHashSha256,
            "AdministrativeDongDioramaManifestReadbackMismatch");
        foreach (var summary in manifest.Tiles)
        {
            var tile = await store.FindTileAsync(TargetArea, summary.TileStableId, CancellationToken.None)
                       ?? throw new InvalidDataException("AdministrativeDongDioramaTileReadbackMissing");
            Require(tile.TileHashSha256 == summary.TileHashSha256, "AdministrativeDongDioramaTileReadbackMismatch");
        }
        var database = independent.Client.GetDatabase("ssalddel_dev");
        var audit = await database.GetCollection<BsonDocument>(AuditCollection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", auditHash)).SingleOrDefaultAsync()
                    ?? throw new InvalidDataException("AdministrativeAssignmentAuditReadbackMissing");
        Require(audit["assignedBuildingCount"].AsInt32 == assignments.AssignedCount
                && audit["unresolvedBuildingCount"].AsInt32 == assignments.UnresolvedCount
                && audit["sourcePublicBusinessObservationCount"].AsInt32 == businesses.Length,
            "AdministrativeAssignmentAuditReadbackMismatch");
        var current = await database.GetCollection<BsonDocument>(AuditCurrentCollection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", LegalArea)).SingleOrDefaultAsync();
        Require(current is not null && current["auditHashSha256"].AsString == auditHash,
            "AdministrativeAssignmentAuditCurrentMismatch");
    }

    private static string AuditHash(
        행정동공간AssignmentResult assignments,
        IEnumerable<(string StableId, string? Area, string Confidence)> businesses,
        string codeHash)
        => Hash(Encoding.UTF8.GetBytes(string.Join("\n", new[]
        {
            BoundaryHash, MapHash, codeHash, BoundaryVintage,
            string.Join("|", assignments.Assignments.Select(item => string.Join(":", item.BuildingStableId,
                item.AdministrativeAreaStableId ?? "Unresolved", item.AssignmentMethodCode, item.ConfidenceCode))),
            string.Join("|", businesses.Select(item => string.Join(":", item.StableId, item.Area ?? "Unresolved", item.Confidence)))
        })));

    private static double PolygonArea(IReadOnlyList<AdministrativeDongDioramaPoint> points)
    {
        var sum = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var next = points[(index + 1) % points.Count];
            sum += points[index].X * next.Z - next.X * points[index].Z;
        }
        return Math.Abs(sum / 2d);
    }

    private static string Category(string? kind) => kind switch
    {
        "house" or "residential" or "apartments" or "detached" => "residential-candidate",
        "commercial" or "retail" => "commercial-candidate",
        "industrial" or "warehouse" => "industrial-candidate",
        "civic" or "public" => "public-candidate",
        _ => "unresolved-candidate"
    };

    private static void SafeRead(string path, string repositoryRoot, string unityRoot, long maxBytes)
    {
        var full = Path.GetFullPath(path);
        var repo = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var unity = Path.TrimEndingDirectorySeparator(Path.GetFullPath(unityRoot));
        Require(full.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(unity + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "AdministrativeDongDioramaPathOutsideRoots");
        for (var item = new FileInfo(full).Directory; item is not null; item = item.Parent)
            if (item.Exists) Require(!item.Attributes.HasFlag(FileAttributes.ReparsePoint), "AdministrativeDongDioramaReparseRejected");
        var file = new FileInfo(full);
        Require(file.Exists && file.Length is > 0 && file.Length <= maxBytes
                && !file.Attributes.HasFlag(FileAttributes.ReparsePoint), "AdministrativeDongDioramaInputMissingOrInvalid");
    }

    private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }
}
