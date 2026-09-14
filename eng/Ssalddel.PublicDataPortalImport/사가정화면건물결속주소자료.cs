using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 화면용 건물 4,062개의 기존 결속 원장과 주소 후보 원장을 공공자료 정규화 테이블에
// 비공개 검토 증거로만 보존한다. 형상·공개 API·Unity·배달·가격 권위는 만들지 않는다.
internal static class 사가정화면건물결속주소자료
{
    public const string SourceId = "sagajeong-presentation-building-evidence";
    public const string BindingDatasetId = "sagajeong-presentation-building-binding-ledger";
    public const string AddressDatasetId = "sagajeong-presentation-building-address-ledger";
    public const string BindingRevision = "sagajeong-presentation-building-binding.r1";
    public const string AddressRevision = "sagajeong-presentation-building-address-assignment.r1";
    public const string RegionStableId = "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer";
    public const string StationStableId = "station:kr:kric:s1107:0722";

    public const string BindingManifestMetric = "presentation-building-binding-manifest";
    public const string BindingSourceReceiptMetric = "presentation-building-binding-source-receipt";
    public const string PresentationBindingMetric = "presentation-building-binding-state";
    public const string ReferenceBindingMetric = "reference-building-binding-state";
    public const string BindingEdgeMetric = "presentation-reference-building-binding";
    public const string AddressManifestMetric = "presentation-building-address-manifest";
    public const string AddressSourceReceiptMetric = "presentation-building-address-source-receipt";
    public const string AddressAssignmentMetric = "presentation-building-address-assignment";
    public const string AddressCandidateMetric = "road-address-candidate-catalog";

    private const string Relative = "artifacts/local/public-data/sagajeong-presentation-building-evidence-20260914-r1";
    private const string BindingFileName = "presentation-building-bindings.json";
    private const string AddressFileName = "presentation-building-addresses.json";
    private const string BindingSchema = "presentation-building-binding-ledger.v1";
    private const string AddressSchema = "presentation-building-address-assignment-ledger.v1";
    private const string BindingFileHash = "A11460EB08BA3D27188AE2BD5FFA7E3BCA22861140EB2973EE869078B5F17149";
    private const string AddressFileHash = "E751344083E98C28E3F4D6B153FA71E7679A7C47534D71B9721DEBE467596984";
    private const string BindingContentHash = "7B47519C1FC9D4D89A04B734710F48FE64BFE8C0EEC9264C947D5DC6206D1411";
    private const string AddressContentHash = "9908B700914DC9CB9AB22592E96A5A699FA3AE108F90063DB010CC9C8B870C33";
    public const string BindingNormalizedDataRowsSha256 = "2426E8ABB083EFA3BA07B06675584768ECD1A30E97436E6CE1AED4A4ABA1CA47";
    public const string AddressNormalizedDataRowsSha256 = "07AF37D600F0B0DC080847F6E65FC1738AFFC827ED333E3ED173A6DC9758F20F";
    private const string SourceVintage = "202608";
    private const string Limitation = "PrivateReviewOnly;ObservationPresentationOnly;NoParcelGeometry;NoOfficialPromotion;NoNearestInference;NoPublicApi;NoPublicDistribution;NoOperationalAuthority;NoUnityApply;NoDelivery;NoPrice;NoBusinessLocation;NoTraversal;NoGameplay";
    private const string AdvisoryLockName = "mirror:public-data:sagajeong-presentation-building-evidence-r1";
    private static readonly DateTimeOffset EvidenceAt = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DerivedAt = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Compact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private sealed record LedgerRows(
        IReadOnlyList<외부데이터정규화Record> BindingRows,
        IReadOnlyList<외부데이터정규화Record> AddressRows,
        string BindingNormalizedDataRowsSha256,
        string AddressNormalizedDataRowsSha256,
        int MaximumTextValueLength,
        string MaximumTextValueMetric)
    {
        public IReadOnlyList<외부데이터정규화Record> AllRows => BindingRows.Concat(AddressRows).ToArray();
    }

    private sealed record CandidateCatalog(
        string AddressStableId,
        string OfficialCompositeKey,
        string CanonicalRoadAddress,
        string[] OfficialBuildingManagementNumbers);

    private sealed record CandidateReference(string AddressStableId, string[] EvidenceMethods);

    private sealed record SourceReceipt(
        string RoleCode,
        string FileName,
        string Sha256,
        string SourceRevision,
        string? ContentSha256 = null);

    private static void Check(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException("SagajeongPresentationBuildingEvidenceImport:" + code);
    }

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Check(mode is "self-test" or "preview" or "apply" or "verify" or "replay", "Mode");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var folder = Path.Combine(root, Relative);
        var ledger = Build(folder);
        var allRows = ledger.AllRows;

        result["stationStableId"] = StationStableId;
        result["regionStableId"] = RegionStableId;
        result["bindingRevision"] = BindingRevision;
        result["bindingContentSha256"] = BindingContentHash.ToLowerInvariant();
        result["bindingNormalizedDataRowsSha256"] = ledger.BindingNormalizedDataRowsSha256.ToLowerInvariant();
        result["addressRevision"] = AddressRevision;
        result["addressContentSha256"] = AddressContentHash.ToLowerInvariant();
        result["addressNormalizedDataRowsSha256"] = ledger.AddressNormalizedDataRowsSha256.ToLowerInvariant();
        result["bindingRows"] = ledger.BindingRows.Count;
        result["addressRows"] = ledger.AddressRows.Count;
        result["totalRows"] = allRows.Count;
        result["maximumTextValueLength"] = ledger.MaximumTextValueLength;
        result["maximumTextValueMetric"] = ledger.MaximumTextValueMetric;
        result["distributionApproved"] = false;
        result["unityApplyAllowed"] = false;
        result["operationalAuthority"] = false;

        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest(ledger);
            result["databaseWriteAttempted"] = false;
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        if (mode == "apply")
        {
            await ApplyAsync(options, folder, ledger, result);
            await VerifyAsync(options, ledger, requireComplete: true, result);
            return;
        }

        var requireComplete = mode is "verify" or "replay";
        await VerifyAsync(options, ledger, requireComplete, result);
        result["databaseWriteAttempted"] = false;
        result["committed"] = false;
        if (mode == "replay") result["replayMatched"] = true;
    }

    private static LedgerRows Build(string folder)
    {
        var bindingPath = Path.Combine(folder, BindingFileName);
        var addressPath = Path.Combine(folder, AddressFileName);
        Check(File.Exists(bindingPath), "BindingLedgerMissing");
        Check(File.Exists(addressPath), "AddressLedgerMissing");
        Check(HashFile(bindingPath) == BindingFileHash, "BindingFileHashChanged");
        Check(HashFile(addressPath) == AddressFileHash, "AddressFileHashChanged");

        using var bindingDocument = JsonDocument.Parse(File.ReadAllBytes(bindingPath));
        using var addressDocument = JsonDocument.Parse(File.ReadAllBytes(addressPath));
        var binding = bindingDocument.RootElement;
        var address = addressDocument.RootElement;
        ValidateBindingHeader(binding);
        ValidateAddressHeader(address);

        var bindingRows = BuildBindingRows(binding);
        var addressRows = BuildAddressRows(address, binding);
        var allRows = bindingRows.Concat(addressRows).ToArray();
        Check(allRows.Select(row => row.RecordKey).Distinct(StringComparer.Ordinal).Count() == allRows.Length, "DuplicateRecordKey");
        Check(allRows.All(row => row.TextValue.Length <= 2_000), "TextValueBudget");
        Check(allRows.All(row => row.DimensionKey.Length <= 500 && row.LimitationCode.Length <= 240), "ColumnBudget");
        Check(allRows.All(row => row.QualityCode == "PendingHumanReview" && row.NumericValue == null), "UnexpectedAuthority");
        var bindingNormalizedHash = NormalizedDataRowsHash(bindingRows.Where(row => row.MetricCode != BindingManifestMetric));
        var addressNormalizedHash = NormalizedDataRowsHash(addressRows.Where(row => row.MetricCode != AddressManifestMetric));
        Check(bindingNormalizedHash == BindingNormalizedDataRowsSha256, "BindingNormalizedRowsHashChanged");
        Check(addressNormalizedHash == AddressNormalizedDataRowsSha256, "AddressNormalizedRowsHashChanged");
        var maximum = allRows.MaxBy(row => row.TextValue.Length)!;
        return new LedgerRows(bindingRows, addressRows, bindingNormalizedHash, addressNormalizedHash,
            maximum.TextValue.Length, maximum.MetricCode);
    }

    private static void ValidateBindingHeader(JsonElement root)
    {
        Check(S(root, "schemaVersion") == BindingSchema, "BindingSchema");
        Check(S(root, "revision") == BindingRevision, "BindingRevision");
        Check(S(root, "stationStableId") == StationStableId, "BindingStation");
        Check(S(root, "regionStableId") == RegionStableId, "BindingRegion");
        Check(S(root, "contentSha256") == BindingContentHash, "BindingContentHash");
        Check(S(root, "presentationOverlayRevision") == "sagajeong-spatial-presentation.private-review.r2", "OverlayRevision");
        Check(S(root, "presentationOverlayFileSha256") == "3EC6B95DC083F13F99042989E0B58957B3EE031E06C501F94022BE455DEC0A44", "OverlayFileHash");
        Check(S(root, "presentationOverlayContentSha256") == "138D1A47B286CE350CF339C1F69C6FFAD7778EBA7B6C95F3CF9F172F8248EBB1", "OverlayContentHash");
        Check(S(root, "referenceMapRevision") == "sagajeong-reference.r3", "ReferenceMapRevision");
        Check(S(root, "referenceMapSha256") == "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3", "ReferenceMapHash");
        ValidateBoundary(root.GetProperty("boundary"), binding: true);

        var summary = root.GetProperty("summary");
        Check(I(summary, "presentationBuildingCount") == 4_062, "PresentationCount");
        Check(I(summary, "referenceBuildingCount") == 602, "ReferenceCount");
        Check(I(summary, "bindingCount") == 544, "BindingCount");
        Check(I(summary, "unresolvedReferenceBuildingCount") == 58, "UnresolvedReferenceCount");
        var states = summary.GetProperty("presentationBindingStates");
        Check(I(states, "AmbiguousGlobalAliasExcluded") == 88, "AmbiguousGlobalAliasCount");
        Check(I(states, "AmbiguousMultipleFootprintsExcluded") == 2, "AmbiguousMultipleCount");
        Check(I(states, "BackdropOnly") == 3_423, "BackdropCount");
        Check(I(states, "Bound") == 544, "BoundCount");
        Check(I(states, "WeakFootprintCandidateExcluded") == 5, "WeakFootprintCount");
    }

    private static void ValidateAddressHeader(JsonElement root)
    {
        Check(S(root, "schemaVersion") == AddressSchema, "AddressSchema");
        Check(S(root, "revision") == AddressRevision, "AddressRevision");
        Check(S(root, "stationStableId") == StationStableId, "AddressStation");
        Check(S(root, "regionStableId") == RegionStableId, "AddressRegion");
        Check(S(root, "sourceVintage") == SourceVintage, "SourceVintage");
        Check(S(root, "evidenceAsOf") == "2026-08-31", "EvidenceDate");
        Check(S(root, "bindingLedgerRevision") == BindingRevision, "AddressBindingRevision");
        Check(S(root, "bindingLedgerContentSha256") == BindingContentHash, "AddressBindingHash");
        Check(S(root, "contentSha256") == AddressContentHash, "AddressContentHash");
        ValidateBoundary(root.GetProperty("boundary"), binding: false);

        var expectedReceipts = new Dictionary<string, (string FileName, string Hash, string Revision)>(StringComparer.Ordinal)
        {
            ["PresentationBuildingAndParcelIdentifier"] = ("AL_D010_11_20260809.zip", "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755", "AL_D010:Seoul:20260809"),
            ["RoadAddressBuildingDatabase"] = ("202608_건물DB_전체분.zip", "4AA70C569AAF14F550313B5836346A1BA438491FB4CEC61877E615C9E602AFA7", SourceVintage),
            ["RoadAddressBuildingRows"] = ("build_seoul.txt", "285286D60409E1CC93898146952D9BC4A805C8F955A765C9F0D72E8A6EA16081", SourceVintage),
            ["RoadAddressRelatedParcelRows"] = ("jibun_seoul.txt", "DF8DBCA1823F1404CDE056C2E2255D97B8525164B2E7BEE4B52A0754B7519555", SourceVintage),
            ["ReferenceBuildingAddressAssignment"] = ("building-address-assignments.json", "86D318AFDC34BD3AF0E6470CCD49F29692A3F71B888FA0252C0B4AAAC84C80BF", "sagajeong-building-address-assignment.r1")
        };
        var receipts = root.GetProperty("sourceReceipts").EnumerateArray().ToArray();
        Check(receipts.Length == expectedReceipts.Count, "SourceReceiptCount");
        foreach (var receipt in receipts)
        {
            var role = S(receipt, "roleCode");
            Check(expectedReceipts.Remove(role, out var expected), "SourceReceiptRole:" + role);
            Check(S(receipt, "fileName") == expected.FileName, "SourceReceiptFile:" + role);
            Check(S(receipt, "sha256") == expected.Hash, "SourceReceiptHash:" + role);
            Check(S(receipt, "sourceRevision") == expected.Revision, "SourceReceiptRevision:" + role);
        }
        Check(expectedReceipts.Count == 0, "SourceReceiptMissing");

        var summary = root.GetProperty("summary");
        Check(I(summary, "presentationBuildingCount") == 4_062, "AddressPresentationCount");
        Check(I(summary, "parcelIdentifierCount") == 4_062, "ParcelIdentifierCount");
        Check(I(summary, "uniqueParcelIdentifierCount") == 3_774, "UniqueParcelIdentifierCount");
        Check(I(summary, "legalDongAndJibunCount") == 4_062, "LegalDongJibunCount");
        Check(I(summary, "sourceBuildingIdentifierCount") == 3_796, "SourceBuildingIdentifierCount");
        Check(I(summary, "sourceUfidCount") == 4_034, "SourceUfidCount");
        Check(I(summary.GetProperty("rawParcelAddressCandidates"), "single") == 3_796, "SingleCandidateCount");
        Check(I(summary.GetProperty("rawParcelAddressCandidates"), "multiple") == 60, "MultipleCandidateCount");
        Check(I(summary.GetProperty("rawParcelAddressCandidates"), "none") == 206, "NoCandidateCount");
        Check(I(summary, "referenceOfficialAddressCount") == 511, "ReferenceOfficialCount");
        var reconciliation = summary.GetProperty("crossSourceReconciliation");
        Check(I(reconciliation, "agreement") == 491 && I(reconciliation, "conflict") == 18 && I(reconciliation, "parcelCandidateMissing") == 2, "ReconciliationCounts");
        var resolutions = summary.GetProperty("resolutionStates");
        Check(I(resolutions, "CrossSourceConflict") == 18, "CrossSourceConflictCount");
        Check(I(resolutions, "MultipleAddressCandidates") == 47, "MultipleAddressResolutionCount");
        Check(I(resolutions, "ParcelAddressCandidate") == 3_278, "ParcelAddressCandidateCount");
        Check(I(resolutions, "ReferenceBindingCandidate") == 516, "ReferenceBindingCandidateCount");
        Check(I(resolutions, "Unresolved") == 203, "UnresolvedAddressCount");
        Check(I(summary, "officialPromotedCount") == 0 && I(summary, "nearestAddressInferenceCount") == 0, "ForbiddenPromotionCount");
    }

    private static void ValidateBoundary(JsonElement boundary, bool binding)
    {
        Check(B(boundary, "observationPresentationOnly"), "ObservationPresentationOnly");
        foreach (var name in new[]
        {
            "distributionApproved", "deliveryEligible", "priceObservationEligible", "businessLocationEligible",
            "unityApplyAllowed", "traversalReady", "gameplayReady"
        }) Check(!B(boundary, name), "BoundaryMustBeFalse:" + name);
        if (binding)
        {
            Check(!B(boundary, "geometryIncluded") && !B(boundary, "nearestBindingInferenceAllowed"), "BindingBoundary");
        }
        else
        {
            Check(!B(boundary, "parcelGeometryCollected") && !B(boundary, "nearestAddressInferenceAllowed")
                && !B(boundary, "officialBuildingIdentityConfirmed"), "AddressBoundary");
        }
    }

    private static List<외부데이터정규화Record> BuildBindingRows(JsonElement root)
    {
        var rows = new List<외부데이터정규화Record>(5_211);
        var presentationRows = root.GetProperty("presentationBuildings").EnumerateArray().ToArray();
        var referenceRows = root.GetProperty("referenceBuildings").EnumerateArray().ToArray();
        var edgeRows = root.GetProperty("bindings").EnumerateArray().ToArray();
        Check(presentationRows.Length == 4_062 && referenceRows.Length == 602 && edgeRows.Length == 544, "BindingArrayCount");

        var presentationIds = new HashSet<string>(StringComparer.Ordinal);
        var boundPresentation = new Dictionary<string, string>(StringComparer.Ordinal);
        var bindingStates = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in presentationRows)
        {
            var presentationId = S(item, "presentationBuildingStableId");
            var sourceFeatureId = S(item, "sourceFeatureId");
            var state = S(item, "bindingState");
            var references = Strings(item, "referenceBuildingStableIds");
            var method = S(item, "bindingMethod");
            var ambiguous = B(item, "ambiguous");
            Check(presentationIds.Add(presentationId), "DuplicatePresentationId:" + presentationId);
            Check(presentationId == "vworld:al-d010:" + sourceFeatureId, "PresentationSourceIdentity:" + presentationId);
            Check(references.Length <= 1 && (state == "Bound") == (references.Length == 1), "PresentationBindingCardinality:" + presentationId);
            Check((state is "AmbiguousGlobalAliasExcluded" or "AmbiguousMultipleFootprintsExcluded" or "WeakFootprintCandidateExcluded") == ambiguous,
                "PresentationAmbiguousState:" + presentationId);
            if (references.Length == 1) boundPresentation[presentationId] = references[0];
            bindingStates[state] = bindingStates.GetValueOrDefault(state) + 1;
            rows.Add(Row(
                BindingDatasetId,
                BindingRevision,
                PresentationBindingMetric,
                "presentation-building-binding:" + presentationId,
                "kind=presentation-binding;presentation=" + presentationId,
                "binding-state",
                "presentation-footprint-binding-candidate",
                new
                {
                    rowType = "presentationBinding",
                    presentationBuildingStableId = presentationId,
                    sourceFeatureId,
                    bindingState = state,
                    referenceBuildingStableIds = references,
                    bindingMethod = method,
                    ambiguous
                }));
        }
        Check(bindingStates.Count == 5 && bindingStates["AmbiguousGlobalAliasExcluded"] == 88
            && bindingStates["AmbiguousMultipleFootprintsExcluded"] == 2 && bindingStates["BackdropOnly"] == 3_423
            && bindingStates["Bound"] == 544 && bindingStates["WeakFootprintCandidateExcluded"] == 5, "BindingStatePartition");

        var referenceIds = new HashSet<string>(StringComparer.Ordinal);
        var boundReference = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in referenceRows)
        {
            var referenceId = S(item, "referenceBuildingStableId");
            var state = S(item, "bindingState");
            var presentationId = S(item, "presentationBuildingStableId");
            Check(referenceIds.Add(referenceId), "DuplicateReferenceId:" + referenceId);
            Check((state == "Bound") == (presentationId.Length > 0), "ReferenceBindingCardinality:" + referenceId);
            Check(state is "Bound" or "UnresolvedNoUniquePresentation", "ReferenceBindingState:" + referenceId);
            if (presentationId.Length > 0) boundReference[referenceId] = presentationId;
            rows.Add(Row(
                BindingDatasetId,
                BindingRevision,
                ReferenceBindingMetric,
                "reference-building-binding:" + referenceId,
                "kind=reference-binding;reference=" + referenceId,
                "binding-state",
                "reference-footprint-binding-candidate",
                new
                {
                    rowType = "referenceBinding",
                    referenceBuildingStableId = referenceId,
                    bindingState = state,
                    presentationBuildingStableId = presentationId
                }));
        }
        Check(boundReference.Count == 544 && referenceRows.Count(item => S(item, "bindingState") == "UnresolvedNoUniquePresentation") == 58, "ReferenceBindingPartition");

        var edgePairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in edgeRows)
        {
            var presentationId = S(item, "presentationBuildingStableId");
            var referenceId = S(item, "referenceBuildingStableId");
            var method = S(item, "bindingMethod");
            var pair = presentationId + "|" + referenceId;
            Check(edgePairs.Add(pair), "DuplicateBindingEdge:" + pair);
            Check(boundPresentation.GetValueOrDefault(presentationId) == referenceId
                && boundReference.GetValueOrDefault(referenceId) == presentationId, "BindingEdgeMismatch:" + pair);
            Check(method == "FootprintOverlap", "BindingMethod:" + pair);
            rows.Add(Row(
                BindingDatasetId,
                BindingRevision,
                BindingEdgeMetric,
                "presentation-reference-building-binding:" + ShortHash(pair),
                "kind=binding-edge;presentation=" + presentationId + ";reference=" + referenceId,
                "binding-edge",
                "one-to-one-footprint-binding-candidate",
                new
                {
                    rowType = "bindingEdge",
                    presentationBuildingStableId = presentationId,
                    referenceBuildingStableId = referenceId,
                    bindingMethod = method,
                    centroidDistanceMeters = item.GetProperty("centroidDistanceMeters").GetDecimal(),
                    intersectionOverUnion = item.GetProperty("intersectionOverUnion").GetDecimal(),
                    smallerFootprintCoverage = item.GetProperty("smallerFootprintCoverage").GetDecimal()
                }));
        }
        Check(edgePairs.Count == boundPresentation.Count && edgePairs.Count == boundReference.Count, "BindingEdgeCoverage");

        var receipts = new[]
        {
            new SourceReceipt("PresentationOverlay", "private-review.json", S(root, "presentationOverlayFileSha256"), S(root, "presentationOverlayRevision"), S(root, "presentationOverlayContentSha256")),
            new SourceReceipt("ReferenceMap", "SagajeongReference.json", S(root, "referenceMapSha256"), S(root, "referenceMapRevision"))
        };
        foreach (var receipt in receipts)
        {
            rows.Add(Row(
                BindingDatasetId,
                BindingRevision,
                BindingSourceReceiptMetric,
                "station-diorama-evidence:sagajeong:binding-source:" + receipt.RoleCode.ToLowerInvariant(),
                "kind=binding-source-receipt;role=" + receipt.RoleCode,
                "source-receipt",
                "frozen-source-hash",
                new
                {
                    rowType = "bindingSourceReceipt",
                    receipt.RoleCode,
                    receipt.FileName,
                    receipt.Sha256,
                    receipt.SourceRevision,
                    receipt.ContentSha256
                }));
        }
        var normalizedDataRowsSha256 = NormalizedDataRowsHash(rows);
        rows.Add(Row(
            BindingDatasetId,
            BindingRevision,
            BindingManifestMetric,
            "station-diorama-evidence:sagajeong:binding-manifest:r1",
            "kind=binding-manifest;revision=" + BindingRevision,
            "ledger-manifest",
            "private-derived-ledger",
            new
            {
                rowType = "bindingManifest",
                schemaVersion = BindingSchema,
                revision = BindingRevision,
                stationStableId = StationStableId,
                regionStableId = RegionStableId,
                presentationOverlayRevision = S(root, "presentationOverlayRevision"),
                presentationOverlayFileSha256 = S(root, "presentationOverlayFileSha256"),
                presentationOverlayContentSha256 = S(root, "presentationOverlayContentSha256"),
                referenceMapRevision = S(root, "referenceMapRevision"),
                referenceMapSha256 = S(root, "referenceMapSha256"),
                fileSha256 = BindingFileHash,
                contentSha256 = BindingContentHash,
                normalizedDataRowsSha256,
                summary = root.GetProperty("summary").Clone(),
                boundary = root.GetProperty("boundary").Clone(),
                normalizedRowCounts = new { manifest = 1, sourceReceipts = receipts.Length, presentationBindings = presentationRows.Length, referenceBindings = referenceRows.Length, bindingEdges = edgeRows.Length, total = 5_211 }
            }));
        Check(rows.Count == 5_211, "BindingNormalizedCount");
        return rows;
    }

    private static List<외부데이터정규화Record> BuildAddressRows(JsonElement root, JsonElement bindingRoot)
    {
        var assignments = root.GetProperty("assignments").EnumerateArray().ToArray();
        Check(assignments.Length == 4_062, "AssignmentArrayCount");
        var bindingPresentationIds = bindingRoot.GetProperty("presentationBuildings").EnumerateArray()
            .Select(item => S(item, "presentationBuildingStableId")).ToHashSet(StringComparer.Ordinal);
        var assignmentIds = new HashSet<string>(StringComparer.Ordinal);
        var parcelIds = new HashSet<string>(StringComparer.Ordinal);
        var candidateCatalog = new Dictionary<string, CandidateCatalog>(StringComparer.Ordinal);
        var candidatePartition = new Dictionary<string, int>(StringComparer.Ordinal) { ["single"] = 0, ["multiple"] = 0, ["none"] = 0 };
        var resolutionStates = new Dictionary<string, int>(StringComparer.Ordinal);
        var reconciliationStates = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<외부데이터정규화Record>(7_582);
        var sourceBuildingCount = 0;
        var sourceUfidCount = 0;
        var referenceOfficialCount = 0;

        foreach (var item in assignments)
        {
            var presentationId = S(item, "presentationBuildingStableId");
            var sourceFeatureId = S(item, "sourceFeatureId");
            var pnu = S(item, "parcelIdentifierPnu");
            var legalDongCode = S(item, "legalDongCode");
            var legalDongName = S(item, "legalDongName");
            var jibun = S(item, "jibun");
            var sourceBuildingIdentifier = S(item, "sourceBuildingIdentifier");
            var sourceUfid = S(item, "sourceUfid");
            var referenceId = S(item, "referenceBuildingStableId");
            var referenceState = S(item, "referenceAddressResolutionState");
            var referenceKeys = Strings(item, "referenceOfficialCompositeKeys");
            var resolutionState = S(item, "resolutionState");
            var reconciliationState = S(item, "reconciliationState");
            var assignmentMethod = S(item, "assignmentMethod");
            var unresolvedReason = S(item, "unresolvedReason");
            Check(assignmentIds.Add(presentationId) && bindingPresentationIds.Contains(presentationId), "AssignmentPresentationId:" + presentationId);
            Check(presentationId == "vworld:al-d010:" + sourceFeatureId, "AssignmentSourceIdentity:" + presentationId);
            Check(pnu.Length == 19 && pnu.All(char.IsAsciiDigit) && legalDongCode == pnu[..10] && legalDongCode == "1126010100", "AssignmentPnu:" + presentationId);
            Check(legalDongName == "면목동" && jibun.Length > 0, "AssignmentLegalAddress:" + presentationId);
            Check(assignmentMethod == "FrozenPresentationBindingThenExactPnuAndOfficialRelatedParcel", "AssignmentMethod:" + presentationId);
            ValidateAssignmentBoundary(item, presentationId);
            parcelIds.Add(pnu);
            if (sourceBuildingIdentifier.Length > 0) sourceBuildingCount++;
            if (sourceUfid.Length > 0) sourceUfidCount++;
            if (referenceState.StartsWith("Official", StringComparison.Ordinal)) referenceOfficialCount++;
            resolutionStates[resolutionState] = resolutionStates.GetValueOrDefault(resolutionState) + 1;
            reconciliationStates[reconciliationState] = reconciliationStates.GetValueOrDefault(reconciliationState) + 1;

            var candidateRefs = new List<CandidateReference>();
            var candidates = item.GetProperty("parcelAddressCandidates").EnumerateArray().ToArray();
            candidatePartition[candidates.Length == 0 ? "none" : candidates.Length == 1 ? "single" : "multiple"]++;
            foreach (var candidate in candidates)
            {
                var addressStableId = S(candidate, "addressStableId");
                var compositeKey = S(candidate, "officialCompositeKey");
                var canonicalAddress = S(candidate, "canonicalRoadAddress");
                var managementNumbers = Strings(candidate, "officialBuildingManagementNumbers");
                var methods = Strings(candidate, "evidenceMethods");
                Check(compositeKey.Length > 0 && methods.Length > 0, "CandidateEvidence:" + addressStableId);
                Check(addressStableId == RoadAddressStableId(compositeKey), "CandidateStableId:" + addressStableId);
                Check(managementNumbers.All(number => number.Length == 25 && number.All(char.IsAsciiDigit)), "CandidateManagementNumber:" + addressStableId);
                var catalog = new CandidateCatalog(addressStableId, compositeKey, canonicalAddress, managementNumbers);
                if (candidateCatalog.TryGetValue(addressStableId, out var existing))
                    Check(existing == catalog || CatalogEquivalent(existing, catalog), "CandidateCatalogConflict:" + addressStableId);
                else
                    candidateCatalog.Add(addressStableId, catalog);
                candidateRefs.Add(new CandidateReference(addressStableId, methods));
            }

            rows.Add(Row(
                AddressDatasetId,
                AddressRevision,
                AddressAssignmentMetric,
                "presentation-building-address:" + presentationId,
                "kind=address-assignment;presentation=" + presentationId,
                "address-assignment",
                "exact-pnu-address-candidate",
                new
                {
                    rowType = "addressAssignment",
                    presentationBuildingStableId = presentationId,
                    sourceFeatureId,
                    parcelIdentifierPnu = pnu,
                    legalDongCode,
                    legalDongName,
                    jibun,
                    sourceBuildingIdentifier,
                    sourceUfid,
                    referenceBuildingStableId = referenceId,
                    referenceAddressResolutionState = referenceState,
                    referenceOfficialCompositeKeys = referenceKeys,
                    candidateRefs,
                    resolutionState,
                    reconciliationState,
                    assignmentMethod,
                    unresolvedReason
                }));
        }
        Check(assignmentIds.SetEquals(bindingPresentationIds), "AssignmentCoverage");
        Check(parcelIds.Count == 3_774 && sourceBuildingCount == 3_796 && sourceUfidCount == 4_034 && referenceOfficialCount == 511, "AssignmentSummaryRecompute");
        Check(candidatePartition["single"] == 3_796 && candidatePartition["multiple"] == 60 && candidatePartition["none"] == 206, "CandidatePartitionRecompute");
        Check(resolutionStates.Count == 5 && resolutionStates["CrossSourceConflict"] == 18
            && resolutionStates["MultipleAddressCandidates"] == 47 && resolutionStates["ParcelAddressCandidate"] == 3_278
            && resolutionStates["ReferenceBindingCandidate"] == 516 && resolutionStates["Unresolved"] == 203, "ResolutionPartitionRecompute");
        Check(reconciliationStates["Agreement"] == 491 && reconciliationStates["Conflict"] == 18
            && reconciliationStates["NotBound"] == 3_528 && reconciliationStates["ParcelCandidateMissing"] == 2
            && reconciliationStates["ReferenceCandidateOnly"] == 23, "ReconciliationPartitionRecompute");
        Check(candidateCatalog.Count == 3_514, "CandidateCatalogCount");

        foreach (var candidate in candidateCatalog.Values.OrderBy(value => value.AddressStableId, StringComparer.Ordinal))
        {
            rows.Add(Row(
                AddressDatasetId,
                AddressRevision,
                AddressCandidateMetric,
                candidate.AddressStableId,
                "kind=address-candidate;address=" + candidate.AddressStableId,
                "road-address-candidate",
                "official-road-address-candidate",
                new
                {
                    rowType = "addressCandidate",
                    candidate.AddressStableId,
                    candidate.OfficialCompositeKey,
                    candidate.CanonicalRoadAddress,
                    candidate.OfficialBuildingManagementNumbers
                }));
        }

        var receipts = root.GetProperty("sourceReceipts").EnumerateArray().Select(item => new SourceReceipt(
            S(item, "roleCode"), S(item, "fileName"), S(item, "sha256"), S(item, "sourceRevision"))).ToArray();
        foreach (var receipt in receipts)
        {
            rows.Add(Row(
                AddressDatasetId,
                AddressRevision,
                AddressSourceReceiptMetric,
                "station-diorama-evidence:sagajeong:address-source:" + receipt.RoleCode.ToLowerInvariant(),
                "kind=address-source-receipt;role=" + receipt.RoleCode,
                "source-receipt",
                "frozen-source-hash",
                new
                {
                    rowType = "addressSourceReceipt",
                    receipt.RoleCode,
                    receipt.FileName,
                    receipt.Sha256,
                    receipt.SourceRevision
                }));
        }

        var normalizedDataRowsSha256 = NormalizedDataRowsHash(rows);
        rows.Add(Row(
            AddressDatasetId,
            AddressRevision,
            AddressManifestMetric,
            "station-diorama-evidence:sagajeong:address-manifest:r1",
            "kind=address-manifest;revision=" + AddressRevision,
            "ledger-manifest",
            "private-derived-ledger",
            new
            {
                rowType = "addressManifest",
                schemaVersion = AddressSchema,
                revision = AddressRevision,
                stationStableId = StationStableId,
                regionStableId = RegionStableId,
                sourceVintage = SourceVintage,
                evidenceAsOf = "2026-08-31",
                bindingLedgerRevision = BindingRevision,
                bindingLedgerContentSha256 = BindingContentHash,
                fileSha256 = AddressFileHash,
                contentSha256 = AddressContentHash,
                normalizedDataRowsSha256,
                summary = root.GetProperty("summary").Clone(),
                boundary = root.GetProperty("boundary").Clone(),
                normalizedRowCounts = new { manifest = 1, sourceReceipts = receipts.Length, addressAssignments = assignments.Length, addressCandidates = candidateCatalog.Count, total = 7_582 }
            }));
        Check(rows.Count == 7_582, "AddressNormalizedCount");
        return rows;
    }

    private static void ValidateAssignmentBoundary(JsonElement item, string presentationId)
    {
        Check(B(item, "observationPresentationOnly"), "AssignmentObservation:" + presentationId);
        foreach (var name in new[]
        {
            "distributionApproved", "deliveryEligible", "priceObservationEligible", "businessLocationEligible",
            "unityApplyAllowed", "traversalReady", "gameplayReady"
        }) Check(!B(item, name), "AssignmentAuthority:" + name + ":" + presentationId);
        var hashes = item.GetProperty("sourceHashes");
        Check(S(hashes, "alD010Sha256") == "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755", "AssignmentAlD010Hash:" + presentationId);
        Check(S(hashes, "moisBuildingDatabaseZipSha256") == "4AA70C569AAF14F550313B5836346A1BA438491FB4CEC61877E615C9E602AFA7", "AssignmentMoisHash:" + presentationId);
        Check(S(hashes, "moisRelatedParcelSha256") == "DF8DBCA1823F1404CDE056C2E2255D97B8525164B2E7BEE4B52A0754B7519555", "AssignmentRelatedParcelHash:" + presentationId);
        Check(S(item, "sourceVintage") == SourceVintage, "AssignmentVintage:" + presentationId);
    }

    private static bool CatalogEquivalent(CandidateCatalog left, CandidateCatalog right)
        => left.AddressStableId == right.AddressStableId
            && left.OfficialCompositeKey == right.OfficialCompositeKey
            && left.CanonicalRoadAddress == right.CanonicalRoadAddress
            && left.OfficialBuildingManagementNumbers.SequenceEqual(right.OfficialBuildingManagementNumbers, StringComparer.Ordinal);

    // Manifest는 순환을 피하려고 제외한다. 나머지 행을 RecordKey ordinal 순으로 정렬한 뒤
    // 지정한 다섯 필드 각각을 [UTF-8 byte length: big-endian int32][UTF-8 bytes]로 이어 붙여 SHA-256한다.
    private static string NormalizedDataRowsHash(IEnumerable<외부데이터정규화Record> source)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (var row in source.OrderBy(item => item.RecordKey, StringComparer.Ordinal))
        {
            foreach (var value in new[] { row.RecordKey, row.StableId, row.MetricCode, row.TextValue, row.DataRevision })
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
                hash.AppendData(length);
                hash.AppendData(bytes);
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static 외부데이터정규화Record Row(
        string datasetId,
        string revision,
        string metric,
        string stableId,
        string dimension,
        string unit,
        string spatialPrecision,
        object value)
    {
        var text = JsonSerializer.Serialize(value, Compact);
        Check(text.Length <= 2_000, "TextValueBudget:" + metric + ":" + stableId + ":" + text.Length.ToString(CultureInfo.InvariantCulture));
        Check(stableId.Length <= 240 && dimension.Length <= 500, "IdentifierBudget:" + metric);
        var sourceVersion = revision + ";content-sha256=" + (datasetId == BindingDatasetId ? BindingContentHash : AddressContentHash).ToLowerInvariant();
        return new 외부데이터정규화Record
        {
            RecordKey = 외부데이터RecordKey.Create(SourceId, datasetId, RegionStableId, metric, EvidenceAt, dimension),
            StableId = stableId,
            SourceId = SourceId,
            DatasetId = datasetId,
            RegionStableId = RegionStableId,
            MetricCode = metric,
            NumericValue = null,
            TextValue = text,
            UnitCode = unit,
            EvidenceAsOfUtc = EvidenceAt,
            CollectedAtUtc = DerivedAt,
            SpatialPrecisionCode = spatialPrecision,
            TemporalPrecisionCode = "month-end-derived-snapshot",
            QualityCode = "PendingHumanReview",
            LimitationCode = Limitation,
            DimensionKey = dimension,
            SourceVersion = sourceVersion,
            DataRevision = revision,
            FirstSeenAtUtc = DerivedAt,
            LastSeenAtUtc = DerivedAt
        };
    }

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        string folder,
        LedgerRows ledger,
        Dictionary<string, object?> result)
    {
        var allRows = ledger.AllRows;
        await using var preflight = new PublicDataIngestionDbContext(options);
        var before = await LoadScopedRowsAsync(preflight);
        ValidateExisting(before, allRows, validateSnapshot: true, requireComplete: false);
        result["beforeCount"] = before.Count;
        if (before.Count == allRows.Count)
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            result["inserted"] = 0;
            result["existing"] = before.Count;
            result["updated"] = 0;
            result["rawSnapshotInserted"] = 0;
            return;
        }

        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        var lockAcquired = false;
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT GET_LOCK('" + AdvisoryLockName + "',0)";
                lockAcquired = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
            }
            Check(lockAcquired, "ImportBusy");
            var lockedExisting = await LoadScopedRowsAsync(db);
            ValidateExisting(lockedExisting, allRows, validateSnapshot: true, requireComplete: false);
            if (lockedExisting.Count == allRows.Count)
            {
                result["databaseWriteAttempted"] = false;
                result["committed"] = false;
                result["inserted"] = 0;
                result["existing"] = lockedExisting.Count;
                result["updated"] = 0;
                result["rawSnapshotInserted"] = 0;
                return;
            }

            result["databaseWriteAttempted"] = true;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var bindingRegistration = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(
                Path.Combine(folder, BindingFileName),
                new(SourceId, BindingDatasetId, BindingRevision + ";content-sha256=" + BindingContentHash.ToLowerInvariant(), BindingRevision,
                    EvidenceAt, "application/json", Relative + "/" + BindingFileName));
            Check(bindingRegistration.SourceHashSha256.Equals(BindingFileHash, StringComparison.OrdinalIgnoreCase), "BindingSnapshotHash");
            var addressRegistration = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(
                Path.Combine(folder, AddressFileName),
                new(SourceId, AddressDatasetId, AddressRevision + ";content-sha256=" + AddressContentHash.ToLowerInvariant(), AddressRevision,
                    EvidenceAt, "application/json", Relative + "/" + AddressFileName));
            Check(addressRegistration.SourceHashSha256.Equals(AddressFileHash, StringComparison.OrdinalIgnoreCase), "AddressSnapshotHash");
            foreach (var row in ledger.BindingRows) row.RawSnapshotId = bindingRegistration.RawSnapshotId;
            foreach (var row in ledger.AddressRows) row.RawSnapshotId = addressRegistration.RawSnapshotId;

            var inserted = 0;
            var existing = 0;
            foreach (var batch in allRows.Chunk(250))
            {
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch);
                Check(saved.UpdatedCount == 0, "UnexpectedUpdate");
                inserted += saved.InsertedCount;
                existing += saved.ExistingCount;
            }
            Check(inserted + existing == allRows.Count, "SaveCount");
            await transaction.CommitAsync();
            result["committed"] = true;
            result["inserted"] = inserted;
            result["existing"] = existing;
            result["updated"] = 0;
            result["rawSnapshotInserted"] = (bindingRegistration.Inserted ? 1 : 0) + (addressRegistration.Inserted ? 1 : 0);
            result["rawSnapshotIds"] = new[] { bindingRegistration.RawSnapshotId, addressRegistration.RawSnapshotId };
        }
        finally
        {
            if (lockAcquired)
            {
                await using var release = db.Database.GetDbConnection().CreateCommand();
                release.CommandText = "SELECT RELEASE_LOCK('" + AdvisoryLockName + "')";
                _ = await release.ExecuteScalarAsync();
            }
        }
    }

    private static async Task VerifyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        LedgerRows ledger,
        bool requireComplete,
        Dictionary<string, object?> result)
    {
        var allRows = ledger.AllRows;
        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await LoadScopedRowsAsync(verify);
        ValidateExisting(stored, allRows, validateSnapshot: true, requireComplete);
        result["verifiedRows"] = stored.Count;
        result["verifiedBindingRows"] = stored.Count(row => row.DatasetId == BindingDatasetId);
        result["verifiedAddressRows"] = stored.Count(row => row.DatasetId == AddressDatasetId);
        result["rawSnapshotIds"] = stored.Select(row => row.RawSnapshotId).Distinct().Order().ToArray();
        result["firstId"] = stored.Count == 0 ? null : stored.Min(row => row.Id);
        result["lastId"] = stored.Count == 0 ? null : stored.Max(row => row.Id);
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
    }

    private static Task<List<외부데이터정규화Record>> LoadScopedRowsAsync(PublicDataIngestionDbContext db)
        => db.NormalizedRecords.AsNoTracking().Include(row => row.RawSnapshot)
            .Where(row => row.SourceId == SourceId
                && row.RegionStableId == RegionStableId
                && (row.DatasetId == BindingDatasetId || row.DatasetId == AddressDatasetId))
            .ToListAsync();

    private static void ValidateExisting(
        IReadOnlyCollection<외부데이터정규화Record> existing,
        IReadOnlyCollection<외부데이터정규화Record> expected,
        bool validateSnapshot,
        bool requireComplete)
    {
        var expectedByKey = expected.ToDictionary(row => row.RecordKey, StringComparer.Ordinal);
        Check(existing.Select(row => row.RecordKey).Distinct(StringComparer.Ordinal).Count() == existing.Count, "DuplicateScopedRecordKey");
        foreach (var stored in existing)
        {
            Check(expectedByKey.TryGetValue(stored.RecordKey, out var candidate), "ExistingRecordKey:" + stored.RecordKey);
            Check(Equivalent(stored, candidate!), "ExistingConflict:" + stored.RecordKey);
            if (!validateSnapshot) continue;
            Check(stored.RawSnapshot != null, "RawSnapshotMissing:" + stored.RecordKey);
            var snapshot = stored.RawSnapshot!;
            var expectedHash = stored.DatasetId == BindingDatasetId ? BindingFileHash : AddressFileHash;
            var expectedFile = stored.DatasetId == BindingDatasetId ? BindingFileName : AddressFileName;
            Check(snapshot.SourceId == SourceId && snapshot.DatasetId == stored.DatasetId, "RawSnapshotDataset:" + stored.RecordKey);
            Check(snapshot.ContentHashSha256.Equals(expectedHash, StringComparison.OrdinalIgnoreCase), "RawSnapshotHash:" + stored.RecordKey);
            Check(snapshot.OriginalFileName == expectedFile && snapshot.EvidenceAsOfUtc == EvidenceAt, "RawSnapshotLineage:" + stored.RecordKey);
        }
        if (requireComplete)
        {
            var actualKeys = existing.Select(row => row.RecordKey).ToHashSet(StringComparer.Ordinal);
            Check(actualKeys.Count == expectedByKey.Count && actualKeys.SetEquals(expectedByKey.Keys), "ReadbackRecordKeySet");
        }
    }

    private static bool Equivalent(외부데이터정규화Record left, 외부데이터정규화Record right)
        => EquivalentExceptText(left, right) && left.TextValue == right.TextValue;

    private static bool EquivalentExceptText(외부데이터정규화Record left, 외부데이터정규화Record right)
        => left.RecordKey == right.RecordKey && left.StableId == right.StableId && left.SourceId == right.SourceId
            && left.DatasetId == right.DatasetId && left.RegionStableId == right.RegionStableId && left.MetricCode == right.MetricCode
            && left.NumericValue == null && right.NumericValue == null && left.UnitCode == right.UnitCode
            && left.EvidenceAsOfUtc == right.EvidenceAsOfUtc && left.CollectedAtUtc == right.CollectedAtUtc
            && left.SpatialPrecisionCode == right.SpatialPrecisionCode && left.TemporalPrecisionCode == right.TemporalPrecisionCode
            && left.QualityCode == right.QualityCode && left.LimitationCode == right.LimitationCode
            && left.DimensionKey == right.DimensionKey && left.SourceVersion == right.SourceVersion && left.DataRevision == right.DataRevision
            && left.FirstSeenAtUtc == right.FirstSeenAtUtc && left.LastSeenAtUtc == right.LastSeenAtUtc;

    private static int SelfTest(LedgerRows ledger)
    {
        var count = 0;
        void Test(bool condition, string code) { Check(condition, "SelfTest:" + code); count++; }
        bool Rejects(Action action)
        {
            try { action(); return false; }
            catch (InvalidDataException) { return true; }
        }
        Test(ledger.BindingRows.Count == 5_211, "BindingRows");
        Test(ledger.AddressRows.Count == 7_582, "AddressRows");
        Test(ledger.AllRows.Count == 12_793, "TotalRows");
        Test(ledger.MaximumTextValueLength <= 2_000, "TextBudget");
        Test(ledger.AllRows.Count(row => row.MetricCode == BindingManifestMetric) == 1, "BindingManifest");
        Test(ledger.AllRows.Count(row => row.MetricCode == BindingSourceReceiptMetric) == 2, "BindingReceipts");
        Test(ledger.AllRows.Count(row => row.MetricCode == PresentationBindingMetric) == 4_062, "PresentationBindings");
        Test(ledger.AllRows.Count(row => row.MetricCode == ReferenceBindingMetric) == 602, "ReferenceBindings");
        Test(ledger.AllRows.Count(row => row.MetricCode == BindingEdgeMetric) == 544, "BindingEdges");
        Test(ledger.AllRows.Count(row => row.MetricCode == AddressManifestMetric) == 1, "AddressManifest");
        Test(ledger.AllRows.Count(row => row.MetricCode == AddressSourceReceiptMetric) == 5, "AddressReceipts");
        Test(ledger.AllRows.Count(row => row.MetricCode == AddressAssignmentMetric) == 4_062, "AddressAssignments");
        Test(ledger.AllRows.Count(row => row.MetricCode == AddressCandidateMetric) == 3_514, "AddressCandidates");
        Test(ledger.AllRows.Select(row => row.RecordKey).Distinct(StringComparer.Ordinal).Count() == 12_793, "RecordKeys");
        Test(ledger.AllRows.All(row => row.QualityCode == "PendingHumanReview"), "QualityBoundary");
        Test(ledger.AllRows.All(row => row.LimitationCode == Limitation), "LimitationBoundary");
        Test(ledger.AllRows.All(row => !row.TextValue.Contains("\"distributionApproved\":true", StringComparison.Ordinal)), "NoDistributionAuthority");
        Test(ledger.AllRows.All(row => !row.TextValue.Contains("\"unityApplyAllowed\":true", StringComparison.Ordinal)), "NoUnityAuthority");
        Test(ledger.AllRows.All(row => !row.TextValue.Contains("\"deliveryEligible\":true", StringComparison.Ordinal)), "NoDeliveryAuthority");
        using var bindingManifest = JsonDocument.Parse(ledger.BindingRows.Single(row => row.MetricCode == BindingManifestMetric).TextValue);
        using var addressManifest = JsonDocument.Parse(ledger.AddressRows.Single(row => row.MetricCode == AddressManifestMetric).TextValue);
        Test(S(bindingManifest.RootElement, "normalizedDataRowsSha256") == ledger.BindingNormalizedDataRowsSha256, "BindingNormalizedRowsHash");
        Test(S(addressManifest.RootElement, "normalizedDataRowsSha256") == ledger.AddressNormalizedDataRowsSha256, "AddressNormalizedRowsHash");
        var bindingDataRows = ledger.BindingRows.Where(row => row.MetricCode != BindingManifestMetric).ToArray();
        Test(NormalizedDataRowsHash(bindingDataRows.Reverse()) == ledger.BindingNormalizedDataRowsSha256, "NormalizedHashOrderIndependent");
        var mutationTarget = bindingDataRows.First(row => row.TextValue.Contains("\"ambiguous\":false", StringComparison.Ordinal));
        var originalText = mutationTarget.TextValue;
        mutationTarget.TextValue = originalText.Replace("\"ambiguous\":false", "\"ambiguous\":true", StringComparison.Ordinal);
        Test(NormalizedDataRowsHash(bindingDataRows) != ledger.BindingNormalizedDataRowsSha256, "NormalizedHashMutationDetected");
        mutationTarget.TextValue = originalText;
        Test(ledger.AddressRows.Where(row => row.MetricCode == AddressAssignmentMetric).All(row =>
            JsonDocument.Parse(row.TextValue).RootElement.GetProperty("candidateRefs").ValueKind == JsonValueKind.Array), "NormalizedCandidateReferences");
        Test(ledger.AddressRows.Where(row => row.MetricCode == AddressCandidateMetric).All(row =>
            JsonDocument.Parse(row.TextValue).RootElement.GetProperty("rowType").GetString() == "addressCandidate"), "CandidateCatalogShape");
        var allRows = ledger.AllRows;
        ValidateExisting(new[] { allRows[0] }, allRows, validateSnapshot: false, requireComplete: false);
        Test(true, "ExpectedPartialPreflightAccepted");
        Test(Rejects(() => ValidateExisting(
            new[] { new 외부데이터정규화Record { RecordKey = "rogue-extra-row" } },
            allRows,
            validateSnapshot: false,
            requireComplete: false)), "RogueScopedRowRejected");
        Test(Rejects(() => ValidateExisting(
            allRows.Skip(1).ToArray(),
            allRows,
            validateSnapshot: false,
            requireComplete: true)), "IncompleteExactSetRejected");
        using var candidateDocument = JsonDocument.Parse(
            ledger.AddressRows.First(row => row.MetricCode == AddressCandidateMetric).TextValue);
        var candidateStableId = S(candidateDocument.RootElement, "addressStableId");
        var candidateCompositeKey = S(candidateDocument.RootElement, "officialCompositeKey");
        Test(candidateStableId == RoadAddressStableId(candidateCompositeKey), "RoadAddressStableIdFormula");
        var mutatedStableId = candidateStableId[..^1] + (candidateStableId[^1] == '0' ? "1" : "0");
        Test(mutatedStableId != RoadAddressStableId(candidateCompositeKey), "RoadAddressStableIdMutationDetected");
        return count;
    }

    private static string S(JsonElement value, string name)
        => value.GetProperty(name).ValueKind == JsonValueKind.Null ? string.Empty : value.GetProperty(name).GetString() ?? string.Empty;

    private static bool B(JsonElement value, string name) => value.GetProperty(name).GetBoolean();

    private static int I(JsonElement value, string name) => value.GetProperty(name).GetInt32();

    private static string[] Strings(JsonElement value, string name)
        => value.GetProperty(name).EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string ShortHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    private static string RoadAddressStableId(string officialCompositeKey)
        => "road-address:kr:" + ShortHash(officialCompositeKey);
}
