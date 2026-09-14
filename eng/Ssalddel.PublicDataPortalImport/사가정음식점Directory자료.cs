using System.Globalization;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;

// 실제 상호는 비공개 검토용 파생 자료로만 보존한다. 표시 승인과 주문 참여 권한은 이 투영에서 만들지 않는다.
internal static class 사가정음식점Directory자료
{
    private const string InputRelativeFolder = "artifacts/local/public-data/myeonmok-business-20260908-r1";
    private const string OutputRelativeFolder = "artifacts/local/public-data/sagajeong-restaurant-directory-20260913-r1";
    private const string OutputFileName = "restaurant-directory.v1.json";
    private const string WideReferenceFileName = "wide-reference.json";
    private const string ConnectionFileName = "connection.json";
    private const string BusinessReadbackFileName = "business-readback.json";
    private const string AddressReviewFileName = "address-review.json";

    private const string WideReferenceHash = "e2420e86bdbcd323e7e52ba7431cf941091c44e0f2d74701c747b810f5fdf928";
    private const string ConnectionHash = "d109a0af26608e9627551600b37fe8b22f473893d5f6fea542e83e5d1fd176e2";
    private const string BusinessReadbackHash = "ec5f0d22859a3661411896e0edc52b14b123d8ff533481b514fedbc8140d5cf1";
    private const string AddressReviewHash = "a48b62059b2fe2b6b5d5165949c1c40ecfad25292154b59bd64fd20ec6a3f41d";
    private const string ParentSourceId = "semas-commercial-listing";
    private const string ParentDatasetId = "data-go-kr-15083033";
    private const string ParentMetricCode = "business-shop";
    private const string ParentDataRevision = "myeonmok-business-20260908.r1";
    private const string DirectoryMetricCode = "sagajeong-food-business-directory";
    private const string DataRevision = "sagajeong-restaurant-directory-20260913.r1";
    private const string RegionStableId = "region:kr:bjd:1126010100";
    private const string WorldRegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1";
    private const string ProjectionKind = "DerivedProjectionFromStoredObservation";
    private const string DisplayReviewStatus = "PendingHumanReview";
    private const string OrderParticipationStatus = "Disabled";
    private const string BuildingCandidateRelation = "UniqueAddressCandidateNotVerifiedOccupancy";
    private const string RestaurantSourceId = "seoul-jungnang-open-data";
    private const string RestaurantDatasetId = "jungnang-restaurant-status";

    private const decimal CenterX = 550m;
    private const decimal CenterZ = 8m;
    private const decimal MinX = 50m;
    private const decimal MaxX = 1050m;
    private const decimal MinZ = -492m;
    private const decimal MaxZ = 508m;

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal sealed record DirectoryScope(
        string kind,
        string worldRegionStableId,
        string legalAreaStableId,
        decimal centerX,
        decimal centerZ,
        decimal minX,
        decimal maxX,
        decimal minZ,
        decimal maxZ,
        string coordinateSpace,
        string boundaryMeaning);

    internal sealed record DirectoryProvenance(
        string wideReferencePath,
        string wideReferenceSha256,
        string connectionPath,
        string connectionSha256,
        string businessReadbackPath,
        string businessReadbackSha256,
        string addressReviewPath,
        string addressReviewSha256,
        string parentSourceId,
        string parentDatasetId,
        string[] parentRawContentHashesSha256,
        string[] restaurantLicenseSourceHashesSha256);

    internal sealed record DirectoryReadiness(
        string nameReadinessCode,
        string displayReviewStatus,
        string orderParticipationStatus,
        bool publicDisplayEnabled,
        bool distributionApproved,
        bool orderScenarioEligible);

    internal sealed record DirectoryCounts(
        int sourceMarkerCount,
        int selectedRestaurantObservationCount,
        int uniqueDisplayNameCount,
        int singleBuildingCandidateCount,
        int licenseMatchedObservationCount,
        int uniqueRestaurantLicenseCandidateCount,
        int restaurantLicenseCandidateLinkCount);

    internal sealed record DirectoryEntry(
        string directoryStableId,
        string parentStableId,
        string parentRecordKey,
        long parentRawSnapshotId,
        string displayNameVerbatim,
        string displayNameSearchKey,
        string sourceRoadAddress,
        string sourceCategory,
        string sourceIndustry,
        string sourceFloor,
        decimal unityX,
        decimal unityZ,
        string? buildingCandidateStableId,
        string buildingCandidateRelation,
        string[] restaurantLicenseCandidateIds,
        string parentSourceId,
        string parentDatasetId,
        string parentSourceVersion,
        string parentSourceRowHashSha256,
        string parentRawContentHashSha256,
        DateTimeOffset evidenceAsOfUtc,
        string dataRevision,
        string projectionKind,
        string displayReviewStatus,
        string orderParticipationStatus,
        bool distributionApproved,
        bool orderScenarioEligible);

    internal sealed record DirectoryManifest(
        string schemaVersion,
        string dataRevision,
        string projectionKind,
        DirectoryScope scope,
        DirectoryProvenance provenance,
        DirectoryReadiness readiness,
        DirectoryCounts counts,
        DirectoryEntry[] restaurants);

    private sealed record ParentObservation(
        string Name,
        string RoadAddress,
        string Category,
        string Industry,
        string Floor,
        string RawRowHash);

    private sealed record ParentFileRecord(
        string RecordKey,
        string StableId,
        long RawSnapshotId,
        string SourceId,
        string DatasetId,
        DateTimeOffset EvidenceAsOfUtc,
        string SourceVersion,
        string QualityCode,
        ParentObservation Observation);

    private sealed record ConnectionEvidence(
        string StableId,
        string Name,
        string RoadAddress,
        string SourceId,
        string DatasetId,
        string SourceHash,
        string Revision,
        DateTimeOffset EvidenceAsOfUtc,
        string QualityCode,
        string Result,
        string[] BuildingIds);

    private sealed record RestaurantLicenseEvidence(
        Guid Id,
        string SourceId,
        string SourceDatasetId,
        string SourceRevision,
        string SourceHashSha256,
        string BusinessName,
        string RoadAddress,
        string NormalizedRoadAddressKey);

    private sealed record Projection(
        DirectoryManifest Manifest,
        byte[] Bytes,
        IReadOnlyDictionary<Guid, RestaurantLicenseEvidence> RestaurantLicenses);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "prepare" or "preview" or "apply" or "verify", "RestaurantDirectoryModeInvalid");
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(result);

        var projection = Project(root);
        PopulateProjectionResult(result, projection);

        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest(projection);
            result["databaseWriteAttempted"] = false;
            return;
        }

        var outputPath = ResolveOutputPath(root);
        if (mode == "prepare")
        {
            var created = await WriteIfAbsentOrSameAsync(outputPath, projection.Bytes);
            result["prepared"] = true;
            result["created"] = created;
            result["databaseWriteAttempted"] = false;
            return;
        }

        Require(File.Exists(outputPath), "RestaurantDirectoryNotPrepared");
        면목동사업체수집.Safe(outputPath);
        Require(File.ReadAllBytes(outputPath).AsSpan().SequenceEqual(projection.Bytes), "RestaurantDirectoryPreparedProjectionMismatch");

        var options = await 로컬공공자료Db.OptionsAsync(root);
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        List<외부데이터정규화Record> expected;
        List<외부데이터정규화Record> before;
        await using (var db = new PublicDataIngestionDbContext(options))
        {
            expected = await ValidateParentsAndBuildRecordsAsync(db, projection);
            var licenseLedgerVerified = await ValidateRestaurantLicensesIfAvailableAsync(
                db,
                projection.RestaurantLicenses);
            before = await LoadNormalizedByKeysAsync(db, expected.Select(item => item.RecordKey), includeRawSnapshot: false);
            AssertNoConflicts(before, expected);
            result["parentRowsVerified"] = expected.Count;
            result["restaurantLicenseCandidatesInFrozenInput"] = projection.Manifest.counts.uniqueRestaurantLicenseCandidateCount;
            result["restaurantLicenseLedgerValidation"] = licenseLedgerVerified
                ? "Verified"
                : "SkippedTableUnavailable";
            result["beforeCount"] = before.Count;

            if (mode == "preview")
            {
                result["wouldInsert"] = expected.Count - before.Count;
                result["existing"] = before.Count;
                result["inserted"] = 0;
                result["updated"] = 0;
                result["databaseWriteAttempted"] = false;
                return;
            }

            if (mode == "verify")
            {
                Require(before.Count == expected.Count, "RestaurantDirectoryReadbackCountMismatch");
                result["databaseWriteAttempted"] = false;
                result["inserted"] = 0;
                result["updated"] = 0;
                result["existing"] = before.Count;
            }
            else
            {
                Require(mode == "apply", "RestaurantDirectoryModeNotHandled");
                if (before.Count == expected.Count)
                {
                    result["databaseWriteAttempted"] = false;
                    result["committed"] = false;
                    result["inserted"] = 0;
                    result["updated"] = 0;
                    result["existing"] = before.Count;
                }
                else
                {
                    await ApplyMissingAsync(db, projection, expected, result);
                }
            }
        }

        // 적용에 사용한 추적 상태를 재사용하지 않고 새 DbContext에서 완전한 구조 동등성을 확인한다.
        await using var verify = new PublicDataIngestionDbContext(options);
        var independentlyExpected = await ValidateParentsAndBuildRecordsAsync(verify, projection);
        var stored = await LoadNormalizedByKeysAsync(verify, independentlyExpected.Select(item => item.RecordKey), includeRawSnapshot: true);
        Require(stored.Count == independentlyExpected.Count, "RestaurantDirectoryIndependentReadbackCountMismatch");
        AssertNoConflicts(stored, independentlyExpected);
        foreach (var row in stored)
        {
            var rawSnapshot = row.RawSnapshot ?? throw new InvalidDataException("RestaurantDirectoryRawSnapshotMissing");
            var expectedRow = independentlyExpected.Single(item => item.RecordKey == row.RecordKey);
            Require(row.RawSnapshotId == expectedRow.RawSnapshotId, "RestaurantDirectoryRawSnapshotIdMismatch");
            Require(rawSnapshot.SourceId == ParentSourceId && rawSnapshot.DatasetId == ParentDatasetId, "RestaurantDirectoryRawSnapshotSourceMismatch");
        }

        result["independentReadbackVerified"] = true;
        result["verifiedRows"] = stored.Count;
    }

    private static Projection Project(string root)
    {
        var inputFolder = Path.GetFullPath(Path.Combine(root, InputRelativeFolder));
        EnsureContained(root, inputFolder, "RestaurantDirectoryInputPathEscaped");
        var widePath = InputPath(inputFolder, WideReferenceFileName, WideReferenceHash);
        var connectionPath = InputPath(inputFolder, ConnectionFileName, ConnectionHash);
        var readbackPath = InputPath(inputFolder, BusinessReadbackFileName, BusinessReadbackHash);
        var addressPath = InputPath(inputFolder, AddressReviewFileName, AddressReviewHash);

        using var wide = JsonDocument.Parse(File.ReadAllBytes(widePath));
        using var connection = JsonDocument.Parse(File.ReadAllBytes(connectionPath));
        using var readback = JsonDocument.Parse(File.ReadAllBytes(readbackPath));
        using var address = JsonDocument.Parse(File.ReadAllBytes(addressPath));

        var wideRoot = wide.RootElement;
        Require(RequiredString(wideRoot, "revision") == "myeonmok-wide-reference.r1", "WideReferenceRevisionMismatch");
        Require(HashEquals(RequiredString(wideRoot, "sourceHash"), AddressReviewHash), "WideReferenceSourceHashMismatch");
        Require(!string.IsNullOrWhiteSpace(RequiredString(wideRoot, "coordinateNote")), "WideReferenceCoordinateNoteMissing");
        var markers = wideRoot.GetProperty("markers").EnumerateArray().ToArray();
        Require(markers.Length == 5411, "WideReferenceMarkerCountMismatch");

        var connectionRoot = connection.RootElement;
        Require(RequiredString(connectionRoot, "schema") == "myeonmok-building-reference.r1", "ConnectionSchemaMismatch");
        Require(HashEquals(RequiredString(connectionRoot, "inputHash"), AddressReviewHash), "ConnectionInputHashMismatch");
        Require(RequiredString(connectionRoot, "mapRevision") == "sagajeong-reference.r3", "ConnectionMapRevisionMismatch");
        Require(RequiredString(connectionRoot, "addressRule") == "kr-public-business-building-match-v1", "ConnectionAddressRuleMismatch");
        Require(RequiredString(connectionRoot, "relation") == BuildingCandidateRelation, "ConnectionRelationMismatch");
        Require(connectionRoot.GetProperty("privateReviewOnly").GetBoolean(), "ConnectionPublicationBoundaryMissing");
        Require(!connectionRoot.GetProperty("gameStateConnected").GetBoolean(), "ConnectionGameStateMustRemainDisabled");
        Require(connectionRoot.GetProperty("inputCount").GetInt32() == 7543, "ConnectionInputCountMismatch");
        var connectionById = ReadConnections(connectionRoot.GetProperty("links"));
        Require(connectionById.Count == 7543, "ConnectionLinkCountMismatch");

        var readbackRoot = readback.RootElement;
        Require(HashEquals(RequiredString(readbackRoot, "sourceReceiptHash"), "a15b270ab8ac12018f11471bf5f1c39b0e56d989639d91c6b1adc9958ee373c8"), "BusinessReceiptHashMismatch");
        Require(HashEquals(RequiredString(readbackRoot, "selectedHash"), "a1f9d1bcf1f04bd20c1285f9d2f0cf21a93b4151b27ae92d86852f343a1dc053"), "BusinessSelectionHashMismatch");
        Require(RequiredString(readbackRoot, "quality") == DisplayReviewStatus, "BusinessReadbackQualityMismatch");
        var parentByStableId = ReadParentRecords(readbackRoot.GetProperty("rows"));
        Require(parentByStableId.Count == 5537, "BusinessReadbackRowCountMismatch");

        var addressRoot = address.RootElement;
        Require(RequiredString(addressRoot, "schema") == "myeonmok-address-review.r1", "AddressReviewSchemaMismatch");
        Require(RequiredString(addressRoot, "quality") == DisplayReviewStatus, "AddressReviewQualityMismatch");
        Require(!addressRoot.GetProperty("unityApplied").GetBoolean(), "AddressReviewMustNotBeAppliedToUnity");
        var licenses = ReadRestaurantLicenses(addressRoot.GetProperty("restaurants"));
        Require(licenses.Count == 1594, "RestaurantLicenseCountMismatch");
        var licensesByMatchKey = licenses.Values
            .GroupBy(item => MatchKey(item.BusinessName, item.NormalizedRoadAddressKey), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Id).ToArray(),
                StringComparer.Ordinal);

        var entries = new List<DirectoryEntry>(599);
        foreach (var marker in markers)
        {
            if (RequiredString(marker, "category") != "음식") continue;
            var x = marker.GetProperty("x").GetDecimal();
            var z = marker.GetProperty("z").GetDecimal();
            if (x < MinX || x > MaxX || z < MinZ || z > MaxZ) continue;

            var stableId = RequiredString(marker, "id");
            Require(parentByStableId.TryGetValue(stableId, out var parent), "DirectoryParentFileRowMissing");
            Require(connectionById.TryGetValue(stableId, out var link), "DirectoryConnectionRowMissing");
            ValidateFileLineage(marker, parent!, link!);

            var matchKey = MatchKey(parent!.Observation.Name, parent.Observation.RoadAddress);
            var candidateLicenses = licensesByMatchKey.TryGetValue(matchKey, out var matches)
                ? matches.Select(item => item.Id.ToString()).ToArray()
                : [];
            var buildingCandidate = link!.Result == BuildingCandidateRelation
                ? RequireSingleBuilding(link)
                : null;

            entries.Add(new DirectoryEntry(
                DirectoryStableId(stableId),
                stableId,
                parent.RecordKey,
                parent.RawSnapshotId,
                parent.Observation.Name,
                NameKey(parent.Observation.Name),
                parent.Observation.RoadAddress,
                parent.Observation.Category,
                parent.Observation.Industry,
                parent.Observation.Floor,
                x,
                z,
                buildingCandidate,
                link.Result,
                candidateLicenses,
                parent.SourceId,
                parent.DatasetId,
                parent.SourceVersion,
                parent.Observation.RawRowHash,
                link.SourceHash.ToLowerInvariant(),
                parent.EvidenceAsOfUtc,
                DataRevision,
                ProjectionKind,
                DisplayReviewStatus,
                OrderParticipationStatus,
                false,
                false));
        }

        entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.parentStableId, right.parentStableId));
        Require(entries.Count == 599, "RestaurantDirectorySelectionCountMismatch");
        Require(entries.Select(item => item.parentStableId).Distinct(StringComparer.Ordinal).Count() == entries.Count, "RestaurantDirectoryDuplicateParent");
        Require(entries.Select(item => item.directoryStableId).Distinct(StringComparer.Ordinal).Count() == entries.Count, "RestaurantDirectoryDuplicateStableId");
        var uniqueNameCount = entries.Select(item => item.displayNameVerbatim).Distinct(StringComparer.Ordinal).Count();
        var singleBuildingCount = entries.Count(item => item.buildingCandidateStableId is not null);
        var licenseMatchedCount = entries.Count(item => item.restaurantLicenseCandidateIds.Length > 0);
        var candidateIds = entries.SelectMany(item => item.restaurantLicenseCandidateIds).Distinct(StringComparer.Ordinal).ToArray();
        var candidateLinks = entries.Sum(item => item.restaurantLicenseCandidateIds.Length);
        Require(uniqueNameCount == 587, "RestaurantDirectoryUniqueNameCountMismatch");
        Require(singleBuildingCount == 180, "RestaurantDirectoryBuildingCandidateCountMismatch");
        Require(licenseMatchedCount == 334, "RestaurantDirectoryLicenseMatchedCountMismatch");
        Require(candidateIds.Length == 334, "RestaurantDirectoryUniqueLicenseCandidateCountMismatch");
        Require(candidateLinks == 336, "RestaurantDirectoryLicenseCandidateLinkCountMismatch");
        Require(entries.All(item => item.displayReviewStatus == DisplayReviewStatus
                                    && item.orderParticipationStatus == OrderParticipationStatus
                                    && !item.distributionApproved
                                    && !item.orderScenarioEligible), "RestaurantDirectoryReadinessEscalated");
        Require(entries.All(item => JsonSerializer.Serialize(item, CompactJson).Length <= 2000), "RestaurantDirectoryTextValueBudgetExceeded");

        var selectedLicenseIds = candidateIds.Select(Guid.Parse).ToHashSet();
        var selectedLicenses = licenses
            .Where(pair => selectedLicenseIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        Require(selectedLicenses.Count == candidateIds.Length, "RestaurantDirectoryLicenseEvidenceMissing");
        var rawHashes = entries.Select(item => item.parentRawContentHashSha256).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        var licenseHashes = selectedLicenses.Values.Select(item => item.SourceHashSha256.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        var manifest = new DirectoryManifest(
            "sagajeong-restaurant-directory.v1",
            DataRevision,
            ProjectionKind,
            new DirectoryScope(
                "StationCenteredUnitySquare",
                WorldRegionStableId,
                RegionStableId,
                CenterX,
                CenterZ,
                MinX,
                MaxX,
                MinZ,
                MaxZ,
                "SagajeongReference.UnityXZ",
                "1km x 1km station-centered review square; not an administrative boundary or service area"),
            new DirectoryProvenance(
                InputRelativeFolder + "/" + WideReferenceFileName,
                WideReferenceHash,
                InputRelativeFolder + "/" + ConnectionFileName,
                ConnectionHash,
                InputRelativeFolder + "/" + BusinessReadbackFileName,
                BusinessReadbackHash,
                InputRelativeFolder + "/" + AddressReviewFileName,
                AddressReviewHash,
                ParentSourceId,
                ParentDatasetId,
                rawHashes,
                licenseHashes),
            new DirectoryReadiness(
                "StoredVerbatimFromPublicSource",
                DisplayReviewStatus,
                OrderParticipationStatus,
                false,
                false,
                false),
            new DirectoryCounts(
                markers.Length,
                entries.Count,
                uniqueNameCount,
                singleBuildingCount,
                licenseMatchedCount,
                candidateIds.Length,
                candidateLinks),
            entries.ToArray());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, PrettyJson);
        Require(bytes.Length is > 0 and <= 16 * 1024 * 1024, "RestaurantDirectoryManifestBudgetExceeded");
        return new Projection(manifest, bytes, selectedLicenses);
    }

    private static async Task<List<외부데이터정규화Record>> ValidateParentsAndBuildRecordsAsync(
        PublicDataIngestionDbContext db,
        Projection projection)
    {
        var parentKeys = projection.Manifest.restaurants.Select(item => item.parentRecordKey).ToArray();
        var parents = await LoadNormalizedByKeysAsync(db, parentKeys, includeRawSnapshot: true);
        Require(parents.Count == parentKeys.Length, "RestaurantDirectoryParentDatabaseRowsMissing");
        var parentByKey = parents.ToDictionary(item => item.RecordKey, StringComparer.Ordinal);
        var expected = new List<외부데이터정규화Record>(projection.Manifest.restaurants.Length);
        foreach (var entry in projection.Manifest.restaurants)
        {
            Require(parentByKey.TryGetValue(entry.parentRecordKey, out var parent), "RestaurantDirectoryParentDatabaseRecordMissing");
            ValidateDatabaseParent(parent!, entry);
            expected.Add(BuildNormalizedRecord(parent!, entry));
        }

        Require(expected.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == expected.Count, "RestaurantDirectoryDuplicateRecordKey");
        return expected;
    }

    private static void ValidateDatabaseParent(외부데이터정규화Record parent, DirectoryEntry entry)
    {
        Require(parent.RecordKey == entry.parentRecordKey, "RestaurantDirectoryParentRecordKeyMismatch");
        Require(parent.StableId == entry.parentStableId, "RestaurantDirectoryParentStableIdMismatch");
        Require(parent.RawSnapshotId == entry.parentRawSnapshotId, "RestaurantDirectoryParentRawSnapshotIdMismatch");
        Require(parent.SourceId == ParentSourceId && parent.DatasetId == ParentDatasetId, "RestaurantDirectoryParentSourceMismatch");
        Require(parent.MetricCode == ParentMetricCode && parent.DataRevision == ParentDataRevision, "RestaurantDirectoryParentProjectionMismatch");
        Require(parent.SourceVersion == entry.parentSourceVersion, "RestaurantDirectoryParentSourceVersionMismatch");
        Require(parent.EvidenceAsOfUtc == entry.evidenceAsOfUtc, "RestaurantDirectoryParentEvidenceDateMismatch");
        Require(parent.QualityCode == DisplayReviewStatus, "RestaurantDirectoryParentQualityMismatch");
        var rawSnapshot = parent.RawSnapshot ?? throw new InvalidDataException("RestaurantDirectoryParentRawSnapshotMissing");
        Require(rawSnapshot.SourceId == ParentSourceId && rawSnapshot.DatasetId == ParentDatasetId, "RestaurantDirectoryParentRawSnapshotSourceMismatch");
        Require(HashEquals(rawSnapshot.ContentHashSha256, entry.parentRawContentHashSha256), "RestaurantDirectoryParentRawSnapshotHashMismatch");

        using var text = JsonDocument.Parse(parent.TextValue);
        var observation = text.RootElement;
        Require(RequiredString(observation, "Name") == entry.displayNameVerbatim, "RestaurantDirectoryParentNameMismatch");
        Require(RequiredString(observation, "RoadAddress") == entry.sourceRoadAddress, "RestaurantDirectoryParentRoadAddressMismatch");
        Require(RequiredString(observation, "Category") == entry.sourceCategory, "RestaurantDirectoryParentCategoryMismatch");
        Require(RequiredString(observation, "Industry") == entry.sourceIndustry, "RestaurantDirectoryParentIndustryMismatch");
        Require(OptionalString(observation, "Floor") == entry.sourceFloor, "RestaurantDirectoryParentFloorMismatch");
        Require(HashEquals(RequiredString(observation, "RawRowHash"), entry.parentSourceRowHashSha256), "RestaurantDirectoryParentRowHashMismatch");
        Require(parent.SourceVersion == "source-row-sha256:" + entry.parentSourceRowHashSha256, "RestaurantDirectoryParentVersionHashMismatch");
    }

    private static 외부데이터정규화Record BuildNormalizedRecord(외부데이터정규화Record parent, DirectoryEntry entry)
    {
        var dimensionKey = "projection=" + DataRevision + ";parentRecordKey=" + entry.parentRecordKey;
        var textValue = JsonSerializer.Serialize(entry, CompactJson);
        var record = new 외부데이터정규화Record
        {
            RawSnapshotId = parent.RawSnapshotId,
            RecordKey = 외부데이터RecordKey.Create(
                ParentSourceId,
                ParentDatasetId,
                RegionStableId,
                DirectoryMetricCode,
                parent.EvidenceAsOfUtc,
                dimensionKey),
            StableId = entry.directoryStableId,
            SourceId = ParentSourceId,
            DatasetId = ParentDatasetId,
            RegionStableId = RegionStableId,
            MetricCode = DirectoryMetricCode,
            NumericValue = null,
            TextValue = textValue,
            UnitCode = "business-directory-entry",
            EvidenceAsOfUtc = parent.EvidenceAsOfUtc,
            CollectedAtUtc = parent.CollectedAtUtc,
            SpatialPrecisionCode = "station-centered-unity-xz-square-1km",
            TemporalPrecisionCode = "source-date-only",
            QualityCode = DisplayReviewStatus,
            LimitationCode = "DerivedProjectionFromStoredObservation;PrivateReviewOnly;DisplayDisabled;OrderScenarioDisabled",
            DimensionKey = dimensionKey,
            SourceVersion = parent.SourceVersion,
            DataRevision = DataRevision,
            FirstSeenAtUtc = parent.FirstSeenAtUtc,
            LastSeenAtUtc = parent.LastSeenAtUtc,
        };
        ValidateDatabaseLengths(record);
        return record;
    }

    private static async Task ApplyMissingAsync(
        PublicDataIngestionDbContext db,
        Projection projection,
        IReadOnlyCollection<외부데이터정규화Record> initiallyExpected,
        Dictionary<string, object?> result)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK('mirror:public-data:sagajeong-restaurant-directory-r1',0)";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1, "RestaurantDirectoryImportBusy");
        await using var transaction = await db.Database.BeginTransactionAsync();

        // 잠금을 얻은 뒤 부모 원장과 파생 목표를 다시 계산해 TOCTOU 쓰기를 막는다.
        var expected = await ValidateParentsAndBuildRecordsAsync(db, projection);
        Require(expected.Count == initiallyExpected.Count, "RestaurantDirectoryExpectedCountChanged");
        var existing = await LoadNormalizedByKeysAsync(db, expected.Select(item => item.RecordKey), includeRawSnapshot: false);
        AssertNoConflicts(existing, expected);
        var existingKeys = existing.Select(item => item.RecordKey).ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(item => !existingKeys.Contains(item.RecordKey)).ToArray();
        if (missing.Length == 0)
        {
            await transaction.RollbackAsync();
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            result["inserted"] = 0;
            result["updated"] = 0;
            result["existing"] = existing.Count;
            return;
        }

        result["databaseWriteAttempted"] = true;
        var inserted = 0;
        var storeExisting = 0;
        foreach (var batch in missing.Chunk(250))
        {
            var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch);
            Require(saved.UpdatedCount == 0, "RestaurantDirectoryUnexpectedUpdate");
            inserted += saved.InsertedCount;
            storeExisting += saved.ExistingCount;
        }

        Require(inserted + storeExisting == missing.Length, "RestaurantDirectoryWriteCountMismatch");
        await transaction.CommitAsync();
        result["committed"] = true;
        result["inserted"] = inserted;
        result["updated"] = 0;
        result["existing"] = existing.Count + storeExisting;
    }

    private static async Task<List<외부데이터정규화Record>> LoadNormalizedByKeysAsync(
        PublicDataIngestionDbContext db,
        IEnumerable<string> recordKeys,
        bool includeRawSnapshot)
    {
        var keys = recordKeys.Distinct(StringComparer.Ordinal).ToArray();
        var rows = new List<외부데이터정규화Record>(keys.Length);
        foreach (var batch in keys.Chunk(250))
        {
            var keyList = batch.ToList();
            IQueryable<외부데이터정규화Record> query = db.NormalizedRecords.AsNoTracking();
            if (includeRawSnapshot) query = query.Include(item => item.RawSnapshot);
            rows.AddRange(await query.Where(item => keyList.Contains(item.RecordKey)).ToListAsync());
        }

        Require(rows.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == rows.Count, "RestaurantDirectoryDatabaseDuplicateRecordKey");
        return rows;
    }

    private static async Task<bool> ValidateRestaurantLicensesIfAvailableAsync(
        PublicDataIngestionDbContext db,
        IReadOnlyDictionary<Guid, RestaurantLicenseEvidence> expected)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'public_licensed_business_records'";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0)
                return false;
        }
        finally
        {
            if (closeWhenDone) await connection.CloseAsync();
        }

        var stored = new List<Ssalddel.Domain.PublicData.Korea.공개인허가사업장Record>(expected.Count);
        foreach (var batch in expected.Keys.Chunk(250))
        {
            var idList = batch.ToList();
            stored.AddRange(await db.공개인허가사업장Records.AsNoTracking()
                .Where(item => idList.Contains(item.Id))
                .ToListAsync());
        }

        Require(stored.Count == expected.Count, "RestaurantDirectoryLicenseDatabaseRowsMissing");
        foreach (var item in stored)
        {
            Require(expected.TryGetValue(item.Id, out var file), "RestaurantDirectoryUnexpectedLicenseDatabaseRow");
            Require(item.SourceId == file!.SourceId && item.SourceDatasetId == file.SourceDatasetId, "RestaurantDirectoryLicenseSourceMismatch");
            Require(item.BusinessName == file.BusinessName, "RestaurantDirectoryLicenseNameMismatch");
            Require((item.RoadAddress ?? string.Empty) == file.RoadAddress, "RestaurantDirectoryLicenseRoadAddressMismatch");
            Require((item.NormalizedRoadAddressKey ?? string.Empty) == file.NormalizedRoadAddressKey, "RestaurantDirectoryLicenseAddressKeyMismatch");
            Require(item.SourceRevision == file.SourceRevision, "RestaurantDirectoryLicenseRevisionMismatch");
            Require(HashEquals(item.SourceHashSha256, file.SourceHashSha256), "RestaurantDirectoryLicenseHashMismatch");
        }

        return true;
    }

    private static void AssertNoConflicts(
        IReadOnlyCollection<외부데이터정규화Record> stored,
        IReadOnlyCollection<외부데이터정규화Record> expected)
    {
        var expectedByKey = expected.ToDictionary(item => item.RecordKey, StringComparer.Ordinal);
        foreach (var item in stored)
        {
            Require(expectedByKey.TryGetValue(item.RecordKey, out var wanted), "RestaurantDirectoryUnexpectedStoredRecord");
            Require(Equivalent(item, wanted!), "RestaurantDirectoryExistingRecordConflict");
        }
    }

    private static bool Equivalent(외부데이터정규화Record left, 외부데이터정규화Record right)
        => left.RawSnapshotId == right.RawSnapshotId
           && left.RecordKey == right.RecordKey
           && left.StableId == right.StableId
           && left.SourceId == right.SourceId
           && left.DatasetId == right.DatasetId
           && left.RegionStableId == right.RegionStableId
           && left.MetricCode == right.MetricCode
           && left.NumericValue == right.NumericValue
           && left.TextValue == right.TextValue
           && left.UnitCode == right.UnitCode
           && left.EvidenceAsOfUtc == right.EvidenceAsOfUtc
           && left.CollectedAtUtc == right.CollectedAtUtc
           && left.SpatialPrecisionCode == right.SpatialPrecisionCode
           && left.TemporalPrecisionCode == right.TemporalPrecisionCode
           && left.QualityCode == right.QualityCode
           && left.LimitationCode == right.LimitationCode
           && left.DimensionKey == right.DimensionKey
           && left.SourceVersion == right.SourceVersion
           && left.DataRevision == right.DataRevision
           && left.FirstSeenAtUtc == right.FirstSeenAtUtc
           && left.LastSeenAtUtc == right.LastSeenAtUtc;

    private static Dictionary<string, ParentFileRecord> ReadParentRecords(JsonElement rows)
    {
        var result = new Dictionary<string, ParentFileRecord>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            var observation = row.GetProperty("observation");
            var item = new ParentFileRecord(
                RequiredString(row, "RecordKey"),
                RequiredString(row, "StableId"),
                row.GetProperty("RawSnapshotId").GetInt64(),
                RequiredString(row, "SourceId"),
                RequiredString(row, "DatasetId"),
                RequiredDateTimeOffset(row, "EvidenceAsOfUtc"),
                RequiredString(row, "SourceVersion"),
                RequiredString(row, "QualityCode"),
                new ParentObservation(
                    RequiredString(observation, "Name"),
                    OptionalString(observation, "RoadAddress"),
                    RequiredString(observation, "Category"),
                    RequiredString(observation, "Industry"),
                    OptionalString(observation, "Floor"),
                    RequiredString(observation, "RawRowHash")));
            Require(result.TryAdd(item.StableId, item), "BusinessReadbackDuplicateStableId");
        }

        return result;
    }

    private static Dictionary<string, ConnectionEvidence> ReadConnections(JsonElement links)
    {
        var result = new Dictionary<string, ConnectionEvidence>(StringComparer.Ordinal);
        foreach (var link in links.EnumerateArray())
        {
            var source = link.GetProperty("item");
            var item = new ConnectionEvidence(
                RequiredString(source, "id"),
                RequiredString(source, "name"),
                OptionalString(source, "roadAddress"),
                RequiredString(source, "sourceId"),
                RequiredString(source, "datasetId"),
                RequiredString(source, "sourceHash"),
                RequiredString(source, "revision"),
                RequiredDateTimeOffset(source, "evidenceAsOf"),
                RequiredString(source, "quality"),
                RequiredString(link, "result"),
                link.GetProperty("buildingIds").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray());
            Require(item.BuildingIds.All(id => !string.IsNullOrWhiteSpace(id)), "ConnectionBuildingIdMissing");
            Require(result.TryAdd(item.StableId, item), "ConnectionDuplicateStableId");
        }

        return result;
    }

    private static Dictionary<Guid, RestaurantLicenseEvidence> ReadRestaurantLicenses(JsonElement restaurants)
    {
        var result = new Dictionary<Guid, RestaurantLicenseEvidence>();
        foreach (var source in restaurants.EnumerateArray())
        {
            var item = new RestaurantLicenseEvidence(
                Guid.Parse(RequiredString(source, "Id")),
                RequiredString(source, "SourceId"),
                RequiredString(source, "SourceDatasetId"),
                RequiredString(source, "SourceRevision"),
                RequiredString(source, "SourceHashSha256"),
                RequiredString(source, "BusinessName"),
                OptionalString(source, "RoadAddress"),
                RequiredString(source, "NormalizedRoadAddressKey"));
            Require(item.SourceId == RestaurantSourceId && item.SourceDatasetId == RestaurantDatasetId, "RestaurantLicenseFileSourceMismatch");
            Require(result.TryAdd(item.Id, item), "RestaurantLicenseDuplicateId");
        }

        return result;
    }

    private static void ValidateFileLineage(JsonElement marker, ParentFileRecord parent, ConnectionEvidence link)
    {
        var markerId = RequiredString(marker, "id");
        var markerName = RequiredString(marker, "name");
        var markerAddress = RequiredString(marker, "address");
        var markerCategory = RequiredString(marker, "category");
        var markerFloor = OptionalString(marker, "floor");
        var markerSource = RequiredString(marker, "source");
        var markerDate = RequiredDateTimeOffset(marker, "date");

        Require(parent.StableId == markerId && link.StableId == markerId, "RestaurantDirectoryStableIdLineageMismatch");
        Require(parent.SourceId == ParentSourceId && parent.DatasetId == ParentDatasetId, "RestaurantDirectoryParentFileSourceMismatch");
        Require(markerSource == ParentSourceId && link.SourceId == ParentSourceId && link.DatasetId == ParentDatasetId, "RestaurantDirectorySourceLineageMismatch");
        Require(parent.QualityCode == DisplayReviewStatus && link.QualityCode == DisplayReviewStatus, "RestaurantDirectorySourceQualityMismatch");
        Require(parent.Observation.Name == markerName && link.Name == markerName, "RestaurantDirectoryFileNameMismatch");
        Require(parent.Observation.RoadAddress == markerAddress && link.RoadAddress == markerAddress, "RestaurantDirectoryFileRoadAddressMismatch");
        Require(parent.Observation.Category == markerCategory && markerCategory == "음식", "RestaurantDirectoryFileCategoryMismatch");
        Require(parent.Observation.Floor == markerFloor, "RestaurantDirectoryFileFloorMismatch");
        Require(parent.EvidenceAsOfUtc == markerDate && link.EvidenceAsOfUtc == markerDate, "RestaurantDirectoryFileEvidenceDateMismatch");
        Require(parent.SourceVersion == link.Revision, "RestaurantDirectoryFileSourceVersionMismatch");
        Require(parent.SourceVersion == "source-row-sha256:" + parent.Observation.RawRowHash, "RestaurantDirectoryFileRowHashMismatch");
        Require(link.Result is "AddressMissing" or "MultipleBuildingsForAddress" or "NoExactBuildingInCurrentMap" or BuildingCandidateRelation, "RestaurantDirectoryBuildingResultUnknown");
        if (link.Result == BuildingCandidateRelation) Require(link.BuildingIds.Length == 1, "RestaurantDirectorySingleBuildingCandidateInvalid");
    }

    private static string? RequireSingleBuilding(ConnectionEvidence link)
    {
        Require(link.Result == BuildingCandidateRelation && link.BuildingIds.Length == 1, "RestaurantDirectoryBuildingCandidateInvalid");
        return link.BuildingIds[0];
    }

    private static int SelfTest(Projection projection)
    {
        var passed = 0;
        void Check(bool value, string code)
        {
            Require(value, "RestaurantDirectorySelfTest:" + code);
            passed++;
        }

        Check(projection.Manifest.restaurants.Length == 599, "Selection");
        Check(projection.Manifest.counts.uniqueDisplayNameCount == 587, "Names");
        Check(projection.Manifest.counts.uniqueRestaurantLicenseCandidateCount == 334, "Licenses");
        Check(projection.Manifest.counts.singleBuildingCandidateCount == 180, "Buildings");
        Check(NameKey("테스트 음식점") == NameKey("테스트음식점"), "WhitespaceSearchKey");
        Check(NameKey("Ａ식당") == NameKey("A식당"), "UnicodeSearchKey");
        Check(DirectoryStableId("business:test") == DirectoryStableId("business:test"), "StableIdDeterminism");
        Check(DirectoryStableId("business:test") != DirectoryStableId("business:other"), "StableIdIdentity");
        Check(projection.Manifest.restaurants.All(item => item.unityX >= MinX && item.unityX <= MaxX && item.unityZ >= MinZ && item.unityZ <= MaxZ), "Bounds");
        Check(projection.Manifest.restaurants.All(item => !item.distributionApproved && !item.orderScenarioEligible), "AuthorityBoundary");
        Check(projection.Bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(projection.Manifest, PrettyJson)), "SerializationDeterminism");
        Check(Hash(projection.Bytes).Length == 64, "ProjectionHash");
        return passed;
    }

    private static void PopulateProjectionResult(Dictionary<string, object?> result, Projection projection)
    {
        result["schemaVersion"] = projection.Manifest.schemaVersion;
        result["dataRevision"] = projection.Manifest.dataRevision;
        result["projectionKind"] = projection.Manifest.projectionKind;
        result["manifestPath"] = OutputRelativeFolder + "/" + OutputFileName;
        result["manifestSha256"] = Hash(projection.Bytes);
        result["restaurants"] = projection.Manifest.counts.selectedRestaurantObservationCount;
        result["uniqueDisplayNames"] = projection.Manifest.counts.uniqueDisplayNameCount;
        result["singleBuildingCandidates"] = projection.Manifest.counts.singleBuildingCandidateCount;
        result["licenseMatchedObservations"] = projection.Manifest.counts.licenseMatchedObservationCount;
        result["uniqueRestaurantLicenseCandidates"] = projection.Manifest.counts.uniqueRestaurantLicenseCandidateCount;
        result["restaurantLicenseCandidateLinks"] = projection.Manifest.counts.restaurantLicenseCandidateLinkCount;
        result["displayReviewStatus"] = projection.Manifest.readiness.displayReviewStatus;
        result["orderParticipationStatus"] = projection.Manifest.readiness.orderParticipationStatus;
        result["distributionApproved"] = projection.Manifest.readiness.distributionApproved;
        result["orderScenarioEligible"] = projection.Manifest.readiness.orderScenarioEligible;
    }

    private static void ValidateDatabaseLengths(외부데이터정규화Record record)
    {
        Require(record.RecordKey.Length <= 64, "RestaurantDirectoryRecordKeyBudget");
        Require(record.StableId.Length <= 240, "RestaurantDirectoryStableIdBudget");
        Require(record.SourceId.Length <= 160 && record.DatasetId.Length <= 160, "RestaurantDirectorySourceBudget");
        Require(record.RegionStableId.Length <= 240, "RestaurantDirectoryRegionBudget");
        Require(record.MetricCode.Length <= 160, "RestaurantDirectoryMetricBudget");
        Require(record.TextValue.Length <= 2000, "RestaurantDirectoryTextValueBudget");
        Require(record.UnitCode.Length <= 80, "RestaurantDirectoryUnitBudget");
        Require(record.SpatialPrecisionCode.Length <= 80 && record.TemporalPrecisionCode.Length <= 80, "RestaurantDirectoryPrecisionBudget");
        Require(record.QualityCode.Length <= 80, "RestaurantDirectoryQualityBudget");
        Require(record.LimitationCode.Length <= 240, "RestaurantDirectoryLimitationBudget");
        Require(record.DimensionKey.Length <= 500, "RestaurantDirectoryDimensionBudget");
        Require(record.SourceVersion.Length <= 200 && record.DataRevision.Length <= 200, "RestaurantDirectoryRevisionBudget");
    }

    private static string InputPath(string folder, string fileName, string expectedHash)
    {
        var path = Path.Combine(folder, fileName);
        Require(File.Exists(path), "RestaurantDirectoryInputMissing:" + fileName);
        면목동사업체수집.Safe(path);
        Require(HashEquals(FileHash(path), expectedHash), "RestaurantDirectoryInputChanged:" + fileName);
        return path;
    }

    private static string ResolveOutputPath(string root)
    {
        var folder = Path.GetFullPath(Path.Combine(root, OutputRelativeFolder));
        EnsureContained(root, folder, "RestaurantDirectoryOutputPathEscaped");
        return Path.Combine(folder, OutputFileName);
    }

    private static async Task<bool> WriteIfAbsentOrSameAsync(string path, byte[] bytes)
    {
        면목동사업체수집.Safe(path);
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        면목동사업체수집.Safe(path);
        if (File.Exists(path))
        {
            Require(File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes), "RestaurantDirectoryExistingManifestConflict");
            return false;
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(bytes);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            Require(File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes), "RestaurantDirectoryConcurrentManifestConflict");
            return false;
        }
    }

    private static void EnsureContained(string root, string path, string code)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        Require(fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase), code);
    }

    private static string DirectoryStableId(string parentStableId)
        => "directory:sagajeong:" + Hash(Encoding.UTF8.GetBytes(parentStableId.Normalize(NormalizationForm.FormKC)));

    private static string MatchKey(string name, string normalizedRoadAddress)
        => NameKey(name) + "\u001f" + normalizedRoadAddress;

    private static string NameKey(string name)
        => Regex.Replace(name.Normalize(NormalizationForm.FormKC), @"\s+", string.Empty);

    private static DateTimeOffset RequiredDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = RequiredString(element, propertyName);
        Require(DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed), "RestaurantDirectoryDateInvalid:" + propertyName);
        return parsed;
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        Require(element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String, "RestaurantDirectoryPropertyMissing:" + propertyName);
        var value = property.GetString()!;
        Require(!string.IsNullOrWhiteSpace(value), "RestaurantDirectoryPropertyEmpty:" + propertyName);
        return value;
    }

    private static string OptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null) return string.Empty;
        Require(property.ValueKind == JsonValueKind.String, "RestaurantDirectoryPropertyInvalid:" + propertyName);
        return property.GetString() ?? string.Empty;
    }

    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool HashEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
