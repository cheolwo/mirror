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

// 동결 OA-23081 횡단보도 점과 2023 역사 행정동 경계의 결속 후보만
// 로컬 비공개 검토 원장에 보존한다. 현행 경계·신호 현시·통행·Unity 권위는 만들지 않는다.
internal static class 행정동횡단보도후보
{
    private const string SourceId = "seoul-open-data-admin-dong-crosswalk-private-review";
    private const string DatasetId = "northeast-seoul-admin-dong-crosswalk-candidate-g3a-r2";
    private const string MetricCode = "administrative-dong-crosswalk-point-candidate";
    private const string Revision = "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r2";
    private const string DesignRevision = "administrative-dong-diorama:crosswalk-candidate.g3a.r2";
    private const string SupersededDatasetId = "northeast-seoul-admin-dong-crosswalk-candidate-g3a-r1";
    private const string SupersededRevision = "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r1";
    private const string SupersededAuditStatus = "RejectedAfterIndependentAudit";
    private const string SupersededDisposition = "PreservedNotPromoted";
    private const string CandidateSchemaVersion = "administrative-dong-crosswalk-candidate.v1";
    private const string ManifestSchemaVersion = "administrative-dong-crosswalk-candidate-manifest.v1";
    private const string AuditSchemaVersion = "administrative-dong-crosswalk-candidate-audit.v1";
    private const string CompleteSchemaVersion = "administrative-dong-crosswalk-candidate-complete.v1";
    private const string ScopeSchemaVersion = "administrative-dong-crosswalk-generation-scope.v1";
    private const string ScopeStableId = "scope:administrative-dong-crosswalk:northeast-seoul-rider:g3a:r2";
    private const string CoordinateReferenceStatus = "EmpiricallyValidatedCandidateNotSourceDeclared";
    private const string UniqueAssignment = "UniqueHistoricalBoundaryCover";
    private const string MultipleAssignment = "UnresolvedMultipleHistoricalBoundaryCover";
    private const string Quality = "PendingHumanReview";
    private const string ConflictQuality = "PendingHumanReviewWithSourceDistrictSpatialAssignmentConflict";
    private const string ConflictDiagnostic = "SourceDistrictSpatialAssignmentConflict";
    private const string OwnershipBasis = "HistoricalBoundaryCover";
    private const string SourceVintage = "oa23081-file-20260824+oa22160-file-20231031+northeast-seoul-rider-r2+station-reference-r1+crosswalk-g3a-r2";
    private const string DesignHash = "005DBDF1C5B501362FF0445C72F3BED91FAE2CC99A21372C6FD0AA3F09C18466";
    private const string ScopeDefinitionHash = "96CC3D689F7D2BFEF8F4ADE7DC292A792ECE09C365D89DA38AFE0D805B2F8076";
    private const string GeneratorHash = "FC949D8350EFCA7A80F4D7BC1C7071DA1362C3E4D6D104D584B909A3F9771CC8";
    private const string CandidateSetHash = "78312E5F0DDB89BFFA9AD1881379CBF7E8037A6C6454D4F0ABDF2A3E4D22D30A";
    private const string CoordinateValidationHash = "5A9555F46D0FEE48F8E6598B166E21BCFEC0B8623B38219C56B7B332F8284972";
    private const string AlternativeCrsEvidenceHash = "D5B5291CB54834B29AB6C068436BDDB624B7E8020B54AB967F2BBEED7181A2EC";
    private const string HistoricalBoundaryConversionHash = "EA41BA84D64259F8577ABE9BECAAA537F7A03DE35154BEDF7262307D6D0FC3E8";
    private const string FeasibilityDigest = "DC34A66F2390281AA5BF4D70F77ABE87AEEBCFEC97328B5AE5C6D5D7FA62DDC3";
    private const string ManifestHash = "FFF09A9A2360E67911C6BDF839BC6D98C1D905BC92BD23A1930407EAC94FE803";
    private const string CandidatesHash = "F852174430E66554363F38FA8996BA6895C84F81CA92787969F869CE4AA89EB7";
    private const string AuditHash = "08F59331BDD0845E8A1333F5DCF6D3492FB6D0C37779CFAB42CFEB9954C81F25";
    private const string CompleteHash = "31D21E9CCEC1A2A09E0DFA197B4DB2BC67517E942776040147368FC7D55784BB";
    private const string XlsxHash = "1A5DB9EA7A1CD58E2D7F2B4246BAF099A3E4D5278F2867A1B87F3D50D21541BE";
    private const string AcquisitionHash = "AD9BD163328B66A0B700EE100249DCA60193226EF156D3D1D5FB02CF6642889C";
    private const string BoundaryHash = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68";
    private const string R2ScopeDefinitionHash = "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B";
    private const string R2ScopeManifestHash = "5F651C5460173A5C2EFD68F0E04DB06880B8F8F4CAE74A38AD0BDE11637D5113";
    private const string OfficialStationAnchorSelectionHash = "0EFE8A791CBD9D0171997B09ACD71A24C8E7D39F50FA62AD8D22E57CD9768CEC";
    private const string OfficialStationAnchorWorkbookHash = "CDF1D84A7E5C898B2AACD622783BA8BA9AF35C40BEE0561DC97D55CE8E063F94";
    private const string ScopeRelative = "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-crosswalk.g3a.r2.json";
    private const string GeneratorRelative = "eng/neighborhood/administrative_dong_crosswalk_candidate.py";
    private const string DesignRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-crosswalk-candidate.implementation.r8.md";
    private const string WorkOrderRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-crosswalk-candidate.e7-work-order.json";
    private const string OutputRelative = "artifacts/local/public-data/admin-dong-crosswalk-northeast-seoul-20260915-g3a-r2";
    private const string GenerationRelative = OutputRelative + "/generations/" + "78312e5f0ddb89bffa9ad1881379cbf7e8037a6c6454d4f0abdf2a3e4d22d30a";
    private const string Limitation = "PrivateReviewOnly;HistoricalBoundaryBootstrapOnly;CrsCandidate;CrosswalkPointOnly;DistrictConflictDiagnostic;NoCrosswalkGeometry;NoStopLine;NoSignalPhase;NoSidewalkConnection;NoDistribution;NoRuntime;NoTraversal;NoGameplay;NoUnity";
    private const int ExpectedSourceRows = 21_776;
    private const int ExpectedCoordinateRows = 21_775;
    private const int ExpectedCoordinateMissingRows = 1;
    private const int ExpectedCandidateRows = 1_533;
    private const int ExpectedOutsideRows = 20_242;
    private const int ExpectedMultipleRows = 0;
    private const int ExpectedConflictRows = 21;
    private const int ExpectedSignalPresentRows = 933;
    private const int ExpectedSignalAbsentRows = 600;
    private const int ExpectedAreaCount = 30;
    private const string OriginalInputDatasetId = DatasetId + "-oa23081-input";
    private static readonly DateTimeOffset EvidenceAsOfUtc = new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DerivedAtUtc = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, string> ExpectedSourceHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oa23081Xlsx"] = XlsxHash,
            ["oa23081AcquisitionReceipt"] = AcquisitionHash,
            ["oa22160BoundaryArchive"] = BoundaryHash,
            ["r2ScopeDefinition"] = R2ScopeDefinitionHash,
            ["r2ScopeManifest"] = R2ScopeManifestHash,
            ["officialStationAnchorSelection"] = OfficialStationAnchorSelectionHash,
            ["officialStationAnchorWorkbook"] = OfficialStationAnchorWorkbookHash
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedDistribution =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["region:kr:hjd:1121574000"] = 36, ["region:kr:hjd:1121575000"] = 31,
            ["region:kr:hjd:1121576000"] = 37, ["region:kr:hjd:1121577000"] = 37,
            ["region:kr:hjd:1123056000"] = 75, ["region:kr:hjd:1123057000"] = 21,
            ["region:kr:hjd:1123060000"] = 75, ["region:kr:hjd:1123061000"] = 48,
            ["region:kr:hjd:1123065000"] = 61, ["region:kr:hjd:1123066000"] = 81,
            ["region:kr:hjd:1123072000"] = 33, ["region:kr:hjd:1123073000"] = 46,
            ["region:kr:hjd:1123074000"] = 47, ["region:kr:hjd:1123075000"] = 40,
            ["region:kr:hjd:1126052000"] = 50, ["region:kr:hjd:1126054000"] = 41,
            ["region:kr:hjd:1126055000"] = 34, ["region:kr:hjd:1126056500"] = 54,
            ["region:kr:hjd:1126057000"] = 47, ["region:kr:hjd:1126057500"] = 50,
            ["region:kr:hjd:1126058000"] = 19, ["region:kr:hjd:1126059000"] = 45,
            ["region:kr:hjd:1126060000"] = 21, ["region:kr:hjd:1126061000"] = 59,
            ["region:kr:hjd:1126062000"] = 78, ["region:kr:hjd:1126063000"] = 57,
            ["region:kr:hjd:1126065500"] = 133, ["region:kr:hjd:1126066000"] = 26,
            ["region:kr:hjd:1126068000"] = 108, ["region:kr:hjd:1126069000"] = 43
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedConflictDistribution =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["region:kr:hjd:1121574000"] = 0, ["region:kr:hjd:1121575000"] = 0,
            ["region:kr:hjd:1121576000"] = 5, ["region:kr:hjd:1121577000"] = 0,
            ["region:kr:hjd:1123056000"] = 0, ["region:kr:hjd:1123057000"] = 0,
            ["region:kr:hjd:1123060000"] = 5, ["region:kr:hjd:1123061000"] = 2,
            ["region:kr:hjd:1123065000"] = 3, ["region:kr:hjd:1123066000"] = 0,
            ["region:kr:hjd:1123072000"] = 0, ["region:kr:hjd:1123073000"] = 0,
            ["region:kr:hjd:1123074000"] = 0, ["region:kr:hjd:1123075000"] = 1,
            ["region:kr:hjd:1126052000"] = 0, ["region:kr:hjd:1126054000"] = 3,
            ["region:kr:hjd:1126055000"] = 0, ["region:kr:hjd:1126056500"] = 0,
            ["region:kr:hjd:1126057000"] = 0, ["region:kr:hjd:1126057500"] = 0,
            ["region:kr:hjd:1126058000"] = 0, ["region:kr:hjd:1126059000"] = 0,
            ["region:kr:hjd:1126060000"] = 0, ["region:kr:hjd:1126061000"] = 0,
            ["region:kr:hjd:1126062000"] = 0, ["region:kr:hjd:1126063000"] = 2,
            ["region:kr:hjd:1126065500"] = 0, ["region:kr:hjd:1126066000"] = 0,
            ["region:kr:hjd:1126068000"] = 0, ["region:kr:hjd:1126069000"] = 0
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedConflictPairs =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["광진구>중랑구"] = 3,
            ["노원구>중랑구"] = 2,
            ["성동구>동대문구"] = 10,
            ["성북구>동대문구"] = 1,
            ["중랑구>광진구"] = 5
        };

    private static readonly string[] TrueAuthorityFlags =
        ["privateReviewOnly", "historicalBoundaryBootstrapOnly", "observationCandidateOnly"];

    private static readonly string[] FalseAuthorityFlags =
    [
        "currentAdministrativeBoundaryEstablished", "sourceDeclaredCoordinateReference",
        "crosswalkGeometryEstablished", "stopLineEstablished", "signalPhaseTimingEstablished",
        "sidewalkConnectionEstablished", "distributionApproved", "publicDisplayAllowed",
        "runtimeAuthorized", "traversalReady", "gameplayReady", "unityApplyAllowed"
    ];

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private sealed record FrozenSource(string Role, string Path, string RelativePath, string Hash, long Length);

    private sealed record Candidate(
        string SourceFeatureKey,
        string AdministrativeAreaStableId,
        string AssignmentStateCode,
        string CandidateQualityCode,
        string SourceBoroughName,
        string SpatialAssignmentBoroughName,
        bool SourceDistrictSpatialAssignmentConflict,
        double DistanceToAssignedHistoricalBoundaryMeters,
        string DistanceToAssignedHistoricalBoundaryMetersCanonicalJson,
        string[] CandidateAdministrativeAreaStableIds,
        string CandidateAreaIdsCanonicalJson,
        int SourceSheetRow,
        string SourceSerialNumber,
        string District,
        string CrosswalkManagementNumber,
        string? IntersectionManagementNumber,
        string? IntersectionName,
        string IntersectionCanonicalJson,
        string CrosswalkType,
        bool PedestrianSignalPresent,
        string[] QualityDiagnosticCodes,
        string QualityDiagnosticCodesCanonicalJson,
        string OwnershipBasisCode,
        double SourceX,
        double SourceY,
        string SourcePointCanonicalJson,
        long CommonEnuXMillimeters,
        long CommonEnuZMillimeters,
        string CommonEnuCanonicalJson);

    private sealed record AreaSummary(
        string AdministrativeAreaStableId,
        int CandidateCount,
        int ConflictCount,
        int SignalPresentCount,
        int SignalAbsentCount);

    private sealed record PreservedRevisionSummary(int NormalizedRows, int RawSnapshots, string StateHashSha256);

    private sealed record Bundle(
        string Root,
        string ScopePath,
        string GeneratorPath,
        string GenerationPath,
        string ManifestPath,
        string CandidatesPath,
        string AuditPath,
        string CompletePath,
        IReadOnlyDictionary<string, FrozenSource> Sources,
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyList<AreaSummary> PerArea,
        DateTimeOffset AcquiredAtUtc);

    private sealed record RegistrationInput(
        string DatasetId,
        string Path,
        string RelativePath,
        string Hash,
        string SourceVersion,
        string ContentType);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "ModeInvalid");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var bundle = LoadAndValidateBundle(root);
        var rows = BuildRows(bundle);
        SetSummary(result, mode, bundle);

        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest(bundle, rows);
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        var preservedBefore = await ReadPreservedRevisionSummaryAsync(options);
        SetPreservedRevisionSummary(result, preservedBefore);
        if (mode == "preview")
        {
            await using var preflight = new PublicDataIngestionDbContext(options);
            await ValidateSourceSnapshotPreflightAsync(preflight, bundle);
            var existing = await LoadScopedRowsAsync(preflight);
            await ValidateExistingAsync(preflight, bundle, existing, rows, requireComplete: false);
            result["beforeCount"] = existing.Count;
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            return;
        }

        if (mode == "apply")
            await ApplyAsync(options, bundle, rows, result);
        else
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
        }

        await VerifyReadbackAsync(options, bundle, rows, result);
        var preservedAfter = await ReadPreservedRevisionSummaryAsync(options);
        Require(preservedAfter == preservedBefore, "SupersededRevisionWasChanged");
        SetPreservedRevisionSummary(result, preservedAfter);
    }

    private static Bundle LoadAndValidateBundle(string root)
    {
        var designPath = ResolveRepositoryPath(root, DesignRelative);
        var workOrderPath = ResolveRepositoryPath(root, WorkOrderRelative);
        Require(HashFile(designPath) == DesignHash, "DesignHashChanged");
        using (var workOrder = ReadJson(workOrderPath, 128_000))
        {
            var gate = workOrder.RootElement.GetProperty("planningGate");
            Require(Text(gate, "statusCode") == "Approved"
                    && Text(gate, "designHashSha256") == DesignHash
                    && Text(gate, "designRevision") == DesignRevision
                    && Text(gate, "designDocumentRef") == DesignRelative,
                "WorkOrderPlanningGateChanged");
            Require(!Bool(workOrder.RootElement, "promotionEligible"), "WorkOrderPromotionBoundaryChanged");
        }

        var scopePath = ResolveRepositoryPath(root, ScopeRelative);
        var generatorPath = ResolveRepositoryPath(root, GeneratorRelative);
        Require(HashFile(scopePath) == ScopeDefinitionHash, "ScopeDefinitionHashChanged");
        Require(HashFile(generatorPath) == GeneratorHash, "GeneratorHashChanged");
        using var scopeDocument = ReadJson(scopePath, 256_000);
        var scope = scopeDocument.RootElement;
        ValidateScope(scope, root, generatorPath, out var sources, out var areaBoroughs);

        var generationPath = ResolveRepositoryPath(root, GenerationRelative);
        Require(Directory.Exists(generationPath) && !IsReparsePoint(generationPath), "GenerationMissingOrUnsafe");
        var expectedFiles = new HashSet<string>(["manifest.json", "candidates.ndjson", "audit.json", "complete.json"], StringComparer.Ordinal);
        Require(Directory.EnumerateFiles(generationPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal).SetEquals(expectedFiles),
            "GenerationFileSetChanged");

        var manifestPath = Path.Combine(generationPath, "manifest.json");
        var candidatesPath = Path.Combine(generationPath, "candidates.ndjson");
        var auditPath = Path.Combine(generationPath, "audit.json");
        var completePath = Path.Combine(generationPath, "complete.json");
        Require(HashFile(completePath) == CompleteHash, "CompleteFileHashChanged");
        using var completeDocument = ReadJson(completePath, 64_000);
        ValidateComplete(completeDocument.RootElement, generationPath);

        Require(HashFile(manifestPath) == ManifestHash, "ManifestFileHashChanged");
        Require(HashFile(candidatesPath) == CandidatesHash, "CandidateFileHashChanged");
        Require(HashFile(auditPath) == AuditHash, "AuditFileHashChanged");
        using var manifestDocument = ReadJson(manifestPath, 256_000);
        using var auditDocument = ReadJson(auditPath, 256_000);
        ValidateManifest(manifestDocument.RootElement);
        ValidateAudit(auditDocument.RootElement);

        var candidates = ReadCandidates(candidatesPath, areaBoroughs);
        Require(ComputeCandidateSetHash(candidates) == CandidateSetHash, "CandidateSetHashMismatch");
        var perArea = ValidatePerArea(manifestDocument.RootElement.GetProperty("perAdministrativeArea"), candidates);
        _ = ValidatePerArea(auditDocument.RootElement.GetProperty("perAdministrativeArea"), candidates);

        var acquisition = sources["oa23081AcquisitionReceipt"];
        using var acquisitionDocument = ReadJson(acquisition.Path, 64_000);
        var acquiredAtUtc = acquisitionDocument.RootElement.GetProperty("acquiredAtUtc").GetDateTimeOffset();
        Require(acquiredAtUtc.ToUniversalTime() == new DateTimeOffset(2026, 9, 13, 7, 3, 32, 60, TimeSpan.Zero),
            "AcquisitionTimestampChanged");

        return new Bundle(root, scopePath, generatorPath, generationPath, manifestPath, candidatesPath,
            auditPath, completePath, sources, candidates, perArea, acquiredAtUtc);
    }

    private static void ValidateScope(
        JsonElement scope,
        string root,
        string generatorPath,
        out IReadOnlyDictionary<string, FrozenSource> sources,
        out IReadOnlyDictionary<string, string> areaBoroughs)
    {
        Require(Text(scope, "schemaVersion") == ScopeSchemaVersion
                && Text(scope, "scopeStableId") == ScopeStableId
                && Text(scope, "revision") == Revision
                && Text(scope, "generatedAtUtc") == "2026-09-15T00:00:00Z"
                && Text(scope, "sourceVintage") == SourceVintage
                && Text(scope, "supersedesRevision") == SupersededRevision
                && Text(scope, "supersededRevisionAuditStatusCode") == SupersededAuditStatus
                && Text(scope, "supersededCandidateDispositionCode") == SupersededDisposition
                && Text(scope, "designDocumentRef") == DesignRelative
                && Text(scope, "designHashSha256") == DesignHash
                && Text(scope, "reviewStatus") == Quality
                && Text(scope, "coordinateReferenceStatus") == CoordinateReferenceStatus,
            "ScopeIdentityChanged");
        ValidateAuthority(scope.GetProperty("authorityFlags"), "Scope");
        ValidateCounts(scope.GetProperty("expectedCounts"));

        var expectedDistribution = scope.GetProperty("expectedDistribution").EnumerateObject()
            .ToDictionary(item => "region:kr:hjd:" + item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
        Require(DictionaryEqual(expectedDistribution, ExpectedDistribution), "ScopeDistributionChanged");
        var expectedConflictDistribution = scope.GetProperty("expectedConflictDistribution").EnumerateObject()
            .ToDictionary(item => "region:kr:hjd:" + item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
        var expectedConflictPairs = scope.GetProperty("expectedConflictPairs").EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
        var expectedConflictRange = scope.GetProperty("expectedConflictDistanceRangeMeters");
        Require(DictionaryEqual(expectedConflictDistribution, ExpectedConflictDistribution)
                && DictionaryEqual(expectedConflictPairs, ExpectedConflictPairs)
                && Double(expectedConflictRange, "minimum") == 0.286d
                && Double(expectedConflictRange, "maximum") == 22.076d,
            "ScopeConflictDiagnosticsChanged");
        Require(Text(scope, "expectedFeasibilityDigestSha256") == FeasibilityDigest, "ScopeFeasibilityDigestChanged");

        var coordinateValidation = scope.GetProperty("coordinateValidation");
        Require(Text(coordinateValidation, "contentHashSha256") == CoordinateValidationHash
                && ContentHash(coordinateValidation) == CoordinateValidationHash
                && Text(coordinateValidation, "statusCode") == CoordinateReferenceStatus
                && !Bool(coordinateValidation, "sourceDeclared")
                && Text(coordinateValidation, "crosswalkManagementNumber") == "06-0000016264"
                && Int(coordinateValidation, "sourceSheetRow") == 21_271,
            "CoordinateValidationChanged");
        var stationProvenance = coordinateValidation.GetProperty("stationAnchorProvenance");
        Require(Text(stationProvenance, "stationStableId") == "station:kr:kric:s1107:0722"
                && Text(stationProvenance, "selectionHashSha256") == OfficialStationAnchorSelectionHash
                && Text(stationProvenance, "sourceWorkbookHashSha256") == OfficialStationAnchorWorkbookHash
                && Text(coordinateValidation, "alternativeCrsEvidenceHashSha256") == AlternativeCrsEvidenceHash,
            "CoordinateAnchorProvenanceChanged");
        var alternativeCrsEvidence = scope.GetProperty("alternativeCrsEvidence");
        Require(Text(alternativeCrsEvidence, "contentHashSha256") == AlternativeCrsEvidenceHash
                && ContentHash(alternativeCrsEvidence) == AlternativeCrsEvidenceHash
                && Int(alternativeCrsEvidence, "coordinatePresentRowsEvaluated") == ExpectedCoordinateRows,
            "AlternativeCrsEvidenceChanged");
        var historicalBoundaryConversion = scope.GetProperty("historicalBoundaryConversion");
        Require(Text(historicalBoundaryConversion, "contentHashSha256") == HistoricalBoundaryConversionHash
                && ContentHash(historicalBoundaryConversion) == HistoricalBoundaryConversionHash
                && Text(historicalBoundaryConversion, "sourceCoordinateReference") == "EPSG:5181"
                && Text(historicalBoundaryConversion, "assignmentCoordinateReferenceCandidate") == "EPSG:5186"
                && Double(historicalBoundaryConversion, "xOffsetMeters") == 0d
                && Double(historicalBoundaryConversion, "yOffsetMeters") == 100_000d
                && !Bool(historicalBoundaryConversion, "rotationApplied")
                && !Bool(historicalBoundaryConversion, "scaleApplied")
                && !Bool(historicalBoundaryConversion, "inferredTranslationApplied"),
            "HistoricalBoundaryConversionChanged");

        var toolchain = scope.GetProperty("output").GetProperty("toolchain");
        var output = scope.GetProperty("output");
        Require(Text(output, "repositoryRelativeDirectory") == OutputRelative
                && Text(output, "generationDirectoryName") == "generations"
                && Text(output, "candidateSchemaVersion") == CandidateSchemaVersion
                && Text(output, "persistenceDatasetId") == DatasetId
                && Text(toolchain, "generatorRelativePath") == GeneratorRelative
                && Text(toolchain, "generatorSha256") == GeneratorHash
                && HashFile(generatorPath) == GeneratorHash,
            "ScopeOutputOrToolchainChanged");

        var areaItems = scope.GetProperty("administrativeAreas").EnumerateArray().ToArray();
        var areas = areaItems.Select(item => Text(item, "administrativeAreaStableId")).ToArray();
        Require(areas.SequenceEqual(ExpectedDistribution.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "ScopeAdministrativeAreaSetChanged");
        areaBoroughs = areaItems.ToDictionary(
            item => Text(item, "administrativeAreaStableId"), item => Text(item, "boroughName"), StringComparer.Ordinal);
        Require(areaBoroughs.Count == ExpectedAreaCount && areaBoroughs.Values.All(item => item is "광진구" or "동대문구" or "중랑구"),
            "ScopeAdministrativeAreaBoroughSetChanged");

        var sourceItems = new Dictionary<string, FrozenSource>(StringComparer.Ordinal);
        foreach (var item in scope.GetProperty("sources").EnumerateArray())
        {
            var role = Text(item, "role");
            Require(ExpectedSourceHashes.TryGetValue(role, out var expectedHash) && !sourceItems.ContainsKey(role),
                "ScopeSourceRoleChanged");
            var relativePath = Text(item, "repositoryRelativePath");
            var path = ResolveRepositoryPath(root, relativePath);
            var length = item.GetProperty("byteLength").GetInt64();
            Require(Text(item, "contentHashSha256") == expectedHash
                    && File.Exists(path) && !IsReparsePoint(path)
                    && new FileInfo(path).Length == length
                    && HashFile(path) == expectedHash,
                "FrozenSourceChanged:" + role);
            sourceItems.Add(role, new FrozenSource(role, path, relativePath, expectedHash!, length));
        }
        Require(sourceItems.Count == ExpectedSourceHashes.Count, "ScopeSourceSetChanged");
        ValidateR2AreaSets(sourceItems);
        sources = sourceItems;
    }

    private static void ValidateR2AreaSets(IReadOnlyDictionary<string, FrozenSource> sources)
    {
        using var definition = ReadJson(sources["r2ScopeDefinition"].Path, 256_000);
        using var manifest = ReadJson(sources["r2ScopeManifest"].Path, 512_000);
        var definitionAreas = definition.RootElement.GetProperty("administrativeAreas").EnumerateArray()
            .Select(item => Text(item, "administrativeAreaStableId")).Order(StringComparer.Ordinal).ToArray();
        var manifestAreas = manifest.RootElement.GetProperty("modules").EnumerateArray()
            .Select(item => Text(item, "administrativeAreaStableId")).Order(StringComparer.Ordinal).ToArray();
        var boundarySources = manifest.RootElement.GetProperty("sources").EnumerateArray()
            .Where(item => Text(item, "datasetId") == "OA-22160").ToArray();
        var expected = ExpectedDistribution.Keys.Order(StringComparer.Ordinal).ToArray();
        Require(definitionAreas.SequenceEqual(expected, StringComparer.Ordinal)
                && manifestAreas.SequenceEqual(expected, StringComparer.Ordinal)
                && boundarySources.Length == 1
                && Text(boundarySources[0], "contentHashSha256") == BoundaryHash
                && !Bool(manifest.RootElement, "distributionApproved"),
            "R2AdministrativeAreaSetChanged");
    }

    private static void ValidateComplete(JsonElement complete, string generationPath)
    {
        Require(Text(complete, "schemaVersion") == CompleteSchemaVersion
                && Text(complete, "scopeStableId") == ScopeStableId
                && Text(complete, "revision") == Revision
                && Text(complete, "designHashSha256") == DesignHash
                && Text(complete, "candidateSetHashSha256") == CandidateSetHash
                && Text(complete, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(complete, "coordinateValidationHashSha256") == CoordinateValidationHash
                && Text(complete, "alternativeCrsEvidenceHashSha256") == AlternativeCrsEvidenceHash
                && Text(complete, "historicalBoundaryConversionHashSha256") == HistoricalBoundaryConversionHash
                && Text(complete, "feasibilityDigestSha256") == FeasibilityDigest
                && Text(complete, "supersedesRevision") == SupersededRevision
                && Text(complete, "supersededCandidateDispositionCode") == SupersededDisposition
                && Text(complete, "generationRelativePath") == GenerationRelative
                && Bool(complete, "completeMarker")
                && Text(complete, "contentHashSha256") == ContentHash(complete),
            "CompleteMarkerChanged");
        ValidateAuthority(complete, "Complete");
        var expected = new Dictionary<string, (string Hash, long Length, int? Records)>(StringComparer.Ordinal)
        {
            ["manifest.json"] = (ManifestHash, 18_275, null),
            ["candidates.ndjson"] = (CandidatesHash, 2_905_027, ExpectedCandidateRows),
            ["audit.json"] = (AuditHash, 18_371, null)
        };
        var entries = complete.GetProperty("files").EnumerateArray().ToArray();
        Require(entries.Length == expected.Count, "CompleteFileReceiptSetChanged");
        foreach (var entry in entries)
        {
            var relative = Text(entry, "relativePath");
            Require(expected.TryGetValue(relative, out var receipt), "CompleteUnknownFileReceipt");
            var path = Path.Combine(generationPath, relative);
            Require(Text(entry, "sha256") == receipt.Hash
                    && entry.GetProperty("byteLength").GetInt64() == receipt.Length
                    && File.Exists(path) && !IsReparsePoint(path)
                    && new FileInfo(path).Length == receipt.Length
                    && HashFile(path) == receipt.Hash,
                "CompleteFileReceiptChanged:" + relative);
            if (receipt.Records.HasValue)
                Require(Int(entry, "recordCount") == receipt.Records.Value, "CompleteRecordCountChanged");
        }
    }

    private static void ValidateManifest(JsonElement manifest)
    {
        Require(Text(manifest, "schemaVersion") == ManifestSchemaVersion
                && Text(manifest, "scopeStableId") == ScopeStableId
                && Text(manifest, "revision") == Revision
                && Text(manifest, "generatedAtUtc") == "2026-09-15T00:00:00Z"
                && Text(manifest, "sourceVintage") == SourceVintage
                && Text(manifest, "designDocumentRef") == DesignRelative
                && Text(manifest, "designHashSha256") == DesignHash
                && Text(manifest, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(manifest, "candidateSetHashSha256") == CandidateSetHash
                && Text(manifest, "supersedesRevision") == SupersededRevision
                && Text(manifest, "supersededCandidateDispositionCode") == SupersededDisposition
                && Text(manifest, "coordinateReferenceStatus") == CoordinateReferenceStatus
                && Text(manifest, "coordinateValidationHashSha256") == CoordinateValidationHash
                && Text(manifest, "alternativeCrsEvidenceHashSha256") == AlternativeCrsEvidenceHash
                && Text(manifest, "historicalBoundaryConversionHashSha256") == HistoricalBoundaryConversionHash
                && Text(manifest, "feasibilityDigestSha256") == FeasibilityDigest
                && Text(manifest, "contentHashSha256") == ContentHash(manifest),
            "ManifestIdentityChanged");
        ValidateAuthority(manifest, "Manifest");
        ValidateCounts(manifest.GetProperty("counts"));
        ValidateSourceHashes(manifest.GetProperty("sourceHashes"));
        ValidateQualityDiagnostics(manifest.GetProperty("qualityDiagnostics"));
        var areas = manifest.GetProperty("administrativeAreaIds").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Require(areas.SequenceEqual(ExpectedDistribution.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "ManifestAdministrativeAreaSetChanged");
        var files = manifest.GetProperty("files").EnumerateArray()
            .ToDictionary(item => Text(item, "relativePath"), StringComparer.Ordinal);
        Require(files.Count == 2
                && Text(files["audit.json"], "sha256") == AuditHash
                && Text(files["candidates.ndjson"], "sha256") == CandidatesHash
                && Int(files["candidates.ndjson"], "recordCount") == ExpectedCandidateRows,
            "ManifestFileReceiptsChanged");
        Require(Text(manifest.GetProperty("toolchain"), "generatorSha256") == GeneratorHash,
            "ManifestGeneratorHashChanged");
    }

    private static void ValidateAudit(JsonElement audit)
    {
        Require(Text(audit, "schemaVersion") == AuditSchemaVersion
                && Text(audit, "scopeStableId") == ScopeStableId
                && Text(audit, "revision") == Revision
                && Text(audit, "designHashSha256") == DesignHash
                && Text(audit, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(audit, "candidateSetHashSha256") == CandidateSetHash
                && Text(audit, "supersedesRevision") == SupersededRevision
                && Text(audit, "supersededCandidateDispositionCode") == SupersededDisposition
                && Text(audit, "coordinateReferenceStatus") == CoordinateReferenceStatus
                && Text(audit, "coordinateValidationHashSha256") == CoordinateValidationHash
                && Text(audit, "alternativeCrsEvidenceHashSha256") == AlternativeCrsEvidenceHash
                && Text(audit, "historicalBoundaryConversionHashSha256") == HistoricalBoundaryConversionHash
                && Text(audit, "feasibilityDigestSha256") == FeasibilityDigest
                && Text(audit, "contentHashSha256") == ContentHash(audit),
            "AuditIdentityChanged");
        ValidateAuthority(audit, "Audit");
        ValidateCounts(audit.GetProperty("counts"));
        ValidateSourceHashes(audit.GetProperty("sourceHashes"));
        ValidateQualityDiagnostics(audit.GetProperty("qualityDiagnostics"));
        foreach (var check in audit.GetProperty("checks").EnumerateObject())
            Require(check.Value.ValueKind == JsonValueKind.True, "AuditCheckFailed:" + check.Name);
    }

    private static IReadOnlyList<Candidate> ReadCandidates(
        string path,
        IReadOnlyDictionary<string, string> areaBoroughs)
    {
        var candidates = new List<Candidate>(ExpectedCandidateRows);
        using var reader = new StreamReader(path, new UTF8Encoding(false, true), false);
        string? previousKey = null;
        while (reader.ReadLine() is { } line)
        {
            Require(line.Length is > 0 and <= 8_000, "CandidateLineLengthInvalid");
            using var document = JsonDocument.Parse(line);
            var item = document.RootElement;
            Require(item.ValueKind == JsonValueKind.Object, "CandidateRootInvalid");
            Require(Text(item, "schemaVersion") == CandidateSchemaVersion
                    && Text(item, "revision") == Revision
                    && Text(item, "coordinateReferenceStatus") == CoordinateReferenceStatus
                    && Text(item, "sourceHashSha256") == XlsxHash
                    && Text(item, "historicalBoundaryHashSha256") == BoundaryHash
                    && Text(item, "candidateSetHashSha256") == CandidateSetHash,
                "CandidateIdentityChanged");
            ValidateAuthority(item, "Candidate");

            var sourceFeatureKey = Text(item, "sourceFeatureKey");
            var area = NullableText(item, "administrativeAreaStableId") ?? string.Empty;
            var state = Text(item, "assignmentStateCode");
            var candidateQuality = Text(item, "candidateQualityCode");
            var sourceBorough = Text(item, "sourceBoroughName");
            var spatialBorough = Text(item, "spatialAssignmentBoroughName");
            var conflict = Bool(item, "sourceDistrictSpatialAssignmentConflict");
            var distanceElement = item.GetProperty("distanceToAssignedHistoricalBoundaryMeters");
            Require(distanceElement.ValueKind == JsonValueKind.Number, "CandidateBoundaryDistanceShapeInvalid");
            var distance = distanceElement.GetDouble();
            Require(double.IsFinite(distance) && distance >= 0d, "CandidateBoundaryDistanceInvalid");
            var candidateAreasElement = item.GetProperty("candidateAdministrativeAreaStableIds");
            var candidateAreas = candidateAreasElement.EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
            Require(candidateAreas.SequenceEqual(candidateAreas.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                    && candidateAreas.Distinct(StringComparer.Ordinal).Count() == candidateAreas.Length
                    && candidateAreas.All(ExpectedDistribution.ContainsKey),
                "CandidateAreaSetInvalid");
            Require(state == UniqueAssignment && area.Length > 0
                    && candidateAreas.Length == 1 && candidateAreas[0] == area,
                "CandidateAssignmentInvalid");
            Require(areaBoroughs.TryGetValue(area, out var expectedSpatialBorough)
                    && spatialBorough == expectedSpatialBorough
                    && Text(item, "district") == sourceBorough
                    && OwnershipBasis == Text(item, "ownershipBasisCode"),
                "CandidateBoroughOrOwnershipInvalid");

            var diagnosticCodesElement = item.GetProperty("qualityDiagnosticCodes");
            var diagnosticCodes = diagnosticCodesElement.EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty).ToArray();
            Require(diagnosticCodes.SequenceEqual(diagnosticCodes.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                    && diagnosticCodes.Distinct(StringComparer.Ordinal).Count() == diagnosticCodes.Length,
                "CandidateQualityDiagnosticSetInvalid");
            if (conflict)
            {
                Require(sourceBorough != spatialBorough
                        && candidateQuality == ConflictQuality
                        && diagnosticCodes.SequenceEqual([ConflictDiagnostic], StringComparer.Ordinal),
                    "CandidateConflictDiagnosticInvalid");
            }
            else
            {
                Require(sourceBorough == spatialBorough
                        && candidateQuality == Quality
                        && diagnosticCodes.Length == 0,
                    "CandidateNormalQualityInvalid");
            }

            var sheetRow = Int(item, "sourceSheetRow");
            var managementNumber = Text(item, "crosswalkManagementNumber");
            Require(sourceFeatureKey == $"oa23081:crosswalk:{managementNumber}:sheet-row:{sheetRow:D5}"
                    && (previousKey is null || StringComparer.Ordinal.Compare(previousKey, sourceFeatureKey) < 0),
                "CandidateFeatureKeyOrOrderInvalid");
            previousKey = sourceFeatureKey;

            var sourcePoint = item.GetProperty("sourcePointEpsg5186");
            RequireExactProperties(sourcePoint, ["x", "y"], "CandidateSourcePointShape");
            var sourceX = Double(sourcePoint, "x");
            var sourceY = Double(sourcePoint, "y");
            Require(double.IsFinite(sourceX) && double.IsFinite(sourceY), "CandidateSourcePointInvalid");
            var commonEnu = item.GetProperty("commonEnuMillimeters");
            RequireExactProperties(commonEnu, ["x", "z"], "CandidateCommonEnuShape");

            var intersectionManagement = NullableText(item, "intersectionManagementNumber");
            var intersectionName = NullableText(item, "intersectionName");
            candidates.Add(new Candidate(
                sourceFeatureKey,
                area,
                state,
                candidateQuality,
                sourceBorough,
                spatialBorough,
                conflict,
                distance,
                CanonicalJsonText(distanceElement),
                candidateAreas,
                CanonicalJsonText(candidateAreasElement),
                sheetRow,
                Text(item, "sourceSerialNumber"),
                Text(item, "district"),
                managementNumber,
                intersectionManagement,
                intersectionName,
                CanonicalIntersection(intersectionManagement, intersectionName),
                Text(item, "crosswalkType"),
                Bool(item, "pedestrianSignalPresent"),
                diagnosticCodes,
                CanonicalJsonText(diagnosticCodesElement),
                Text(item, "ownershipBasisCode"),
                sourceX,
                sourceY,
                CanonicalJsonText(sourcePoint),
                Long(commonEnu, "x"),
                Long(commonEnu, "z"),
                CanonicalJsonText(commonEnu)));
        }
        Require(candidates.Count == ExpectedCandidateRows
                && candidates.Select(item => item.SourceFeatureKey).Distinct(StringComparer.Ordinal).Count() == ExpectedCandidateRows
                && candidates.All(item => item.AssignmentStateCode != MultipleAssignment),
            "CandidateExactSetChanged");
        ValidateComputedConflictDiagnostics(candidates);
        return candidates;
    }

    private static IReadOnlyList<AreaSummary> ValidatePerArea(JsonElement element, IReadOnlyList<Candidate> candidates)
    {
        var result = new List<AreaSummary>();
        foreach (var item in element.EnumerateArray())
        {
            var area = Text(item, "administrativeAreaStableId");
            Require(ExpectedDistribution.TryGetValue(area, out var expected) && !result.Any(value => value.AdministrativeAreaStableId == area),
                "PerAreaIdentityChanged");
            var areaCandidates = candidates.Where(candidate => candidate.AdministrativeAreaStableId == area).ToArray();
            var summary = new AreaSummary(area, Int(item, "candidateCount"),
                Int(item, "sourceDistrictSpatialAssignmentConflictCount"),
                Int(item, "pedestrianSignalPresentCount"), Int(item, "pedestrianSignalAbsentCount"));
            Require(summary.CandidateCount == expected
                    && summary.CandidateCount == areaCandidates.Length
                    && summary.ConflictCount == ExpectedConflictDistribution[area]
                    && summary.ConflictCount == areaCandidates.Count(candidate => candidate.SourceDistrictSpatialAssignmentConflict)
                    && summary.SignalPresentCount == areaCandidates.Count(candidate => candidate.PedestrianSignalPresent)
                    && summary.SignalAbsentCount == areaCandidates.Count(candidate => !candidate.PedestrianSignalPresent)
                    && summary.SignalPresentCount + summary.SignalAbsentCount == summary.CandidateCount,
                "PerAreaDistributionChanged:" + area);
            result.Add(summary);
        }
        Require(result.Count == ExpectedAreaCount
                && result.Select(item => item.AdministrativeAreaStableId)
                    .SequenceEqual(ExpectedDistribution.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "PerAreaSetChanged");
        return result;
    }

    private static List<외부데이터정규화Record> BuildRows(Bundle bundle)
    {
        var rows = new List<외부데이터정규화Record>(ExpectedCandidateRows);
        foreach (var candidate in bundle.Candidates)
        {
            var payload = JsonSerializer.Serialize(new
            {
                schemaVersion = CandidateSchemaVersion,
                revision = Revision,
                candidate.SourceFeatureKey,
                candidate.AdministrativeAreaStableId,
                candidate.AssignmentStateCode,
                candidate.CandidateQualityCode,
                candidate.SourceBoroughName,
                candidate.SpatialAssignmentBoroughName,
                candidate.SourceDistrictSpatialAssignmentConflict,
                distanceToAssignedHistoricalBoundaryMeters = ParseCanonicalJsonValue(
                    candidate.DistanceToAssignedHistoricalBoundaryMetersCanonicalJson),
                candidate.QualityDiagnosticCodes,
                candidate.OwnershipBasisCode,
                candidate.CandidateAdministrativeAreaStableIds,
                candidate.SourceSheetRow,
                candidate.CrosswalkManagementNumber,
                candidate.IntersectionManagementNumber,
                candidate.IntersectionName,
                candidate.CrosswalkType,
                candidate.PedestrianSignalPresent,
                coordinateReferenceStatus = CoordinateReferenceStatus,
                sourcePointEpsg5186 = new { x = candidate.SourceX, y = candidate.SourceY },
                commonEnuMillimeters = new { x = candidate.CommonEnuXMillimeters, z = candidate.CommonEnuZMillimeters },
                sourceHashSha256 = XlsxHash.ToLowerInvariant(),
                historicalBoundaryHashSha256 = BoundaryHash.ToLowerInvariant(),
                candidateSetHashSha256 = CandidateSetHash.ToLowerInvariant(),
                privateReviewOnly = true,
                historicalBoundaryBootstrapOnly = true,
                observationCandidateOnly = true,
                currentAdministrativeBoundaryEstablished = false,
                sourceDeclaredCoordinateReference = false,
                crosswalkGeometryEstablished = false,
                stopLineEstablished = false,
                signalPhaseTimingEstablished = false,
                sidewalkConnectionEstablished = false,
                distributionApproved = false,
                publicDisplayAllowed = false,
                runtimeAuthorized = false,
                traversalReady = false,
                gameplayReady = false,
                unityApplyAllowed = false
            }, CompactJson);
            var dimension = $"area={candidate.AdministrativeAreaStableId};feature={candidate.SourceFeatureKey};revision={Revision}";
            Require(payload.Length <= 2_000 && dimension.Length <= 500 && Limitation.Length <= 240,
                "DatabaseFieldBudgetExceeded");
            rows.Add(new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId, candidate.AdministrativeAreaStableId,
                    MetricCode, EvidenceAsOfUtc, dimension),
                StableId = "administrative-dong-crosswalk-candidate:" +
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.SourceFeatureKey))).ToLowerInvariant(),
                SourceId = SourceId,
                DatasetId = DatasetId,
                RegionStableId = candidate.AdministrativeAreaStableId,
                MetricCode = MetricCode,
                NumericValue = null,
                TextValue = payload,
                UnitCode = "crosswalk-point-candidate-json",
                EvidenceAsOfUtc = EvidenceAsOfUtc,
                CollectedAtUtc = DerivedAtUtc,
                SpatialPrecisionCode = "epsg5186-point-within-2023-historical-hjd-candidate",
                TemporalPrecisionCode = "portal-file-vintage-not-observation-date",
                QualityCode = Quality,
                LimitationCode = Limitation,
                DimensionKey = dimension,
                SourceVersion = "OA-23081:2026-08-24;candidate-set-sha256=" + CandidateSetHash.ToLowerInvariant(),
                DataRevision = Revision,
                FirstSeenAtUtc = DerivedAtUtc,
                LastSeenAtUtc = DerivedAtUtc
            });
        }
        Require(rows.Count == ExpectedCandidateRows
                && rows.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == rows.Count
                && rows.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == rows.Count,
            "NormalizedExactSetInvalid");
        return rows;
    }

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows,
        Dictionary<string, object?> result)
    {
        await using (var preflight = new PublicDataIngestionDbContext(options))
        {
            await ValidateSourceSnapshotPreflightAsync(preflight, bundle);
            var existing = await LoadScopedRowsAsync(preflight);
            await ValidateExistingAsync(preflight, bundle, existing, rows, requireComplete: false);
            result["beforeCount"] = existing.Count;
            if (existing.Count == rows.Count)
            {
                SetNoWriteResult(result, existing.Count);
                return;
            }
        }

        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        var locked = false;
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:admin-dong-crosswalk-g3a-r2',0)";
                locked = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
            }
            Require(locked, "ImportBusy");

            var lockedExisting = await LoadScopedRowsAsync(db);
            await ValidateSourceSnapshotPreflightAsync(db, bundle);
            await ValidateExistingAsync(db, bundle, lockedExisting, rows, requireComplete: false);
            if (lockedExisting.Count == rows.Count)
            {
                SetNoWriteResult(result, lockedExisting.Count);
                return;
            }

            result["databaseWriteAttempted"] = true;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var registrationService = new 평창군공공공간원본등록Service(db);
            var registrations = new Dictionary<string, 공공공간원본등록Result>(StringComparer.Ordinal);
            var inputs = BuildRegistrationInputs(bundle);
            foreach (var input in inputs)
            {
                var registration = await registrationService.RegisterFileAsync(
                    input.Path,
                    new 공공공간원본등록Request(
                        SourceId,
                        input.DatasetId,
                        input.SourceVersion,
                        Revision,
                        EvidenceAsOfUtc,
                        input.ContentType,
                        input.RelativePath));
                Require(registration.SourceHashSha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase),
                    "RegisteredSourceHashMismatch:" + input.DatasetId);
                registrations.Add(input.DatasetId, registration);
            }
            foreach (var input in inputs)
                await RequireSourceSnapshotAsync(db, input);

            var original = registrations[OriginalInputDatasetId];
            foreach (var row in rows) row.RawSnapshotId = original.RawSnapshotId;
            if (original.Inserted)
            {
                var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == original.RawSnapshotId);
                snapshot.CollectedAtUtc = bundle.AcquiredAtUtc;
                var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
                run.StatusCode = 외부데이터수집StatusCodes.Partial;
                run.FetchedCount = ExpectedSourceRows;
                run.NormalizedCount = ExpectedCandidateRows;
                run.RejectedCount = ExpectedCoordinateMissingRows + ExpectedOutsideRows;
                run.InsertedCount = 0;
                run.ExistingCount = 0;
                run.ErrorCode = Quality;
                run.ErrorSummary = "OA-23081 points assigned only by exact cover of frozen 2023 historical HJD boundaries; 21 source-district conflicts remain explicit review diagnostics; CRS is empirically supported but not source-declared; no publication, runtime, traversal, gameplay or Unity authority.";
                await db.SaveChangesAsync();
            }

            var inserted = 0;
            var existing = 0;
            foreach (var batch in rows.Chunk(500))
            {
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch);
                Require(saved.UpdatedCount == 0, "UnexpectedNormalizedUpdate");
                inserted += saved.InsertedCount;
                existing += saved.ExistingCount;
                db.ChangeTracker.Clear();
            }
            Require(inserted + existing == rows.Count, "NormalizedSaveCountMismatch");

            if (original.Inserted)
            {
                var runId = await db.RawSnapshots.AsNoTracking()
                    .Where(item => item.Id == original.RawSnapshotId)
                    .Select(item => item.FirstCollectionRunId)
                    .SingleAsync();
                var run = await db.IngestionRuns.SingleAsync(item => item.Id == runId);
                run.InsertedCount = inserted;
                run.ExistingCount = existing;
                await db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            result["committed"] = true;
            result["inserted"] = inserted;
            result["existing"] = existing;
            result["updated"] = 0;
            result["rawSnapshotInserted"] = registrations.Values.Count(item => item.Inserted);
            result["rawSnapshotIds"] = registrations.Values.Select(item => item.RawSnapshotId).Order().ToArray();
        }
        finally
        {
            if (locked)
            {
                await using var release = db.Database.GetDbConnection().CreateCommand();
                release.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:admin-dong-crosswalk-g3a-r2')";
                _ = await release.ExecuteScalarAsync();
            }
        }
    }

    private static void SetNoWriteResult(Dictionary<string, object?> result, int existing)
    {
        result["databaseWriteAttempted"] = false;
        result["committed"] = false;
        result["inserted"] = 0;
        result["existing"] = existing;
        result["updated"] = 0;
        result["rawSnapshotInserted"] = 0;
    }

    private static IReadOnlyList<RegistrationInput> BuildRegistrationInputs(Bundle bundle)
    {
        var inputs = new List<RegistrationInput>();
        foreach (var source in bundle.Sources.Values.OrderBy(item => item.Role, StringComparer.Ordinal))
        {
            var dataset = source.Role == "oa23081Xlsx"
                ? OriginalInputDatasetId
                : DatasetId + "-support-" + source.Role.ToLowerInvariant();
            inputs.Add(new RegistrationInput(dataset, source.Path, source.RelativePath, source.Hash,
                "sha256:" + source.Hash.ToLowerInvariant(), ContentType(source.Path)));
        }
        inputs.Add(new RegistrationInput(DatasetId + "-scope-input", bundle.ScopePath, ScopeRelative,
            ScopeDefinitionHash, "sha256:" + ScopeDefinitionHash.ToLowerInvariant(), "application/json"));
        inputs.Add(new RegistrationInput(DatasetId + "-generator-input", bundle.GeneratorPath, GeneratorRelative,
            GeneratorHash, "sha256:" + GeneratorHash.ToLowerInvariant(), "text/x-python"));
        foreach (var item in new[]
        {
            ("manifest", bundle.ManifestPath, ManifestHash, "application/json"),
            ("candidates", bundle.CandidatesPath, CandidatesHash, "application/x-ndjson"),
            ("audit", bundle.AuditPath, AuditHash, "application/json"),
            ("complete", bundle.CompletePath, CompleteHash, "application/json")
        })
        {
            inputs.Add(new RegistrationInput(DatasetId + "-artifact-" + item.Item1, item.Item2,
                GenerationRelative + "/" + Path.GetFileName(item.Item2), item.Item3,
                Revision + ";candidate-set:" + CandidateSetHash.ToLowerInvariant(), item.Item4));
        }
        Require(inputs.Count == 13
                && inputs.Select(item => item.DatasetId).Distinct(StringComparer.Ordinal).Count() == inputs.Count
                && inputs.All(item => item.DatasetId.Length <= 160 && item.SourceVersion.Length <= 200),
            "RegistrationInputSetInvalid");
        return inputs;
    }

    private static async Task VerifyReadbackAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows,
        Dictionary<string, object?> result)
    {
        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await LoadScopedRowsAsync(verify);
        await ValidateExistingAsync(verify, bundle, stored, rows, requireComplete: true);
        var readbackCandidates = stored.OrderBy(item => item.StableId, StringComparer.Ordinal)
            .Select(CandidateFromStoredPayload).ToArray();
        Require(ComputeCandidateSetHash(readbackCandidates) == CandidateSetHash, "ReadbackCandidateSetHashMismatch");
        ValidateComputedDistribution(readbackCandidates);
        ValidateComputedConflictDiagnostics(readbackCandidates);
        Require(readbackCandidates.Count(item => item.PedestrianSignalPresent) == ExpectedSignalPresentRows
                && readbackCandidates.Count(item => !item.PedestrianSignalPresent) == ExpectedSignalAbsentRows,
            "ReadbackSignalDistributionChanged");
        result["verifiedRows"] = stored.Count;
        result["verifiedAdministrativeAreas"] = stored.Select(item => item.RegionStableId).Distinct(StringComparer.Ordinal).Count();
        result["verifiedSourceDistrictSpatialAssignmentConflicts"] = readbackCandidates.Count(item => item.SourceDistrictSpatialAssignmentConflict);
        result["readbackCandidateSetHashSha256"] = CandidateSetHash.ToLowerInvariant();
        result["independentReadback"] = true;
        result["firstId"] = stored.Min(item => item.Id);
        result["lastId"] = stored.Max(item => item.Id);
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
    }

    private static Task<List<외부데이터정규화Record>> LoadScopedRowsAsync(PublicDataIngestionDbContext db)
        => db.NormalizedRecords.AsNoTracking()
            .Where(item => item.SourceId == SourceId && item.DatasetId == DatasetId)
            .ToListAsync();

    private static async Task<PreservedRevisionSummary> ReadPreservedRevisionSummaryAsync(
        DbContextOptions<PublicDataIngestionDbContext> options)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        var normalizedRows = await db.NormalizedRecords.AsNoTracking()
            .Where(item => item.SourceId == SourceId && item.DatasetId == SupersededDatasetId)
            .ToListAsync();
        var rawSnapshots = await db.RawSnapshots.AsNoTracking()
            .Where(item => item.SourceId == SourceId
                           && (item.DatasetId == SupersededDatasetId
                               || item.DatasetId.StartsWith(SupersededDatasetId + "-")))
            .ToListAsync();
        Require(normalizedRows.Count is 0 or ExpectedCandidateRows
                && rawSnapshots.Count is 0 or 11
                && (normalizedRows.Count == 0) == (rawSnapshots.Count == 0)
                && normalizedRows.All(item => item.DataRevision == SupersededRevision),
            "SupersededRevisionPreservationStateInvalid");
        return new PreservedRevisionSummary(
            normalizedRows.Count,
            rawSnapshots.Count,
            ComputePreservedRevisionStateHash(normalizedRows, rawSnapshots));
    }

    private static void SetPreservedRevisionSummary(
        Dictionary<string, object?> result,
        PreservedRevisionSummary summary)
    {
        result["supersededNormalizedRowsPreserved"] = summary.NormalizedRows;
        result["supersededRawSnapshotsPreserved"] = summary.RawSnapshots;
        result["supersededRevisionStateHashSha256"] = summary.StateHashSha256.ToLowerInvariant();
        result["supersededRevisionMutated"] = false;
    }

    private static string ComputePreservedRevisionStateHash(
        IEnumerable<외부데이터정규화Record> normalizedRows,
        IEnumerable<외부데이터RawSnapshot> rawSnapshots)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, SupersededDatasetId);
        Append(hash, SupersededRevision);
        foreach (var item in normalizedRows.OrderBy(value => value.RecordKey, StringComparer.Ordinal)
                     .ThenBy(value => value.Id))
        {
            Append(hash, item.Id.ToString(CultureInfo.InvariantCulture));
            Append(hash, item.RawSnapshotId.ToString(CultureInfo.InvariantCulture));
            Append(hash, item.RecordKey);
            Append(hash, item.StableId);
            Append(hash, item.SourceId);
            Append(hash, item.DatasetId);
            Append(hash, item.RegionStableId);
            Append(hash, item.MetricCode);
            Append(hash, item.NumericValue?.ToString(CultureInfo.InvariantCulture) ?? "<null>");
            Append(hash, item.TextValue);
            Append(hash, item.UnitCode);
            Append(hash, item.EvidenceAsOfUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Append(hash, item.CollectedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Append(hash, item.SpatialPrecisionCode);
            Append(hash, item.TemporalPrecisionCode);
            Append(hash, item.QualityCode);
            Append(hash, item.LimitationCode);
            Append(hash, item.DimensionKey);
            Append(hash, item.SourceVersion);
            Append(hash, item.DataRevision);
            Append(hash, item.FirstSeenAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Append(hash, item.LastSeenAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }
        foreach (var item in rawSnapshots.OrderBy(value => value.DatasetId, StringComparer.Ordinal)
                     .ThenBy(value => value.ContentHashSha256, StringComparer.Ordinal)
                     .ThenBy(value => value.Id))
        {
            Append(hash, item.Id.ToString(CultureInfo.InvariantCulture));
            Append(hash, item.FirstCollectionRunId.ToString(CultureInfo.InvariantCulture));
            Append(hash, item.SourceId);
            Append(hash, item.DatasetId);
            Append(hash, item.SourceVersion);
            Append(hash, item.CollectedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Append(hash, item.EvidenceAsOfUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "<null>");
            Append(hash, item.ContentHashSha256);
            Append(hash, item.ContentLength.ToString(CultureInfo.InvariantCulture));
            Append(hash, item.ContentType);
            Append(hash, item.OriginalFileName);
            Append(hash, item.StorageContainer);
            Append(hash, item.StorageObjectName);
            Append(hash, item.StorageLocation);
            Append(hash, item.FirstSeenAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Append(hash, item.LastSeenAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task ValidateExistingAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle,
        IReadOnlyCollection<외부데이터정규화Record> existing,
        IReadOnlyCollection<외부데이터정규화Record> expected,
        bool requireComplete)
    {
        var expectedByKey = expected.ToDictionary(item => item.RecordKey, StringComparer.Ordinal);
        Require(existing.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == existing.Count,
            "StoredRecordKeyDuplicate");
        foreach (var stored in existing)
        {
            Require(expectedByKey.TryGetValue(stored.RecordKey, out var candidate), "UnexpectedStoredRecord");
            Require(Equivalent(stored, candidate!), "StoredRecordConflict");
        }
        if (requireComplete) Require(existing.Count == expected.Count, "StoredExactSetIncomplete");
        if (existing.Count == 0) return;

        var rawIds = existing.Select(item => item.RawSnapshotId).Distinct().ToArray();
        Require(rawIds.Length == 1, "StoredOriginalSnapshotSetChanged");
        var raw = await db.RawSnapshots.AsNoTracking().SingleAsync(item => item.Id == rawIds[0]);
        Require(raw.SourceId == SourceId && raw.DatasetId == OriginalInputDatasetId
                && raw.ContentHashSha256.Equals(XlsxHash, StringComparison.OrdinalIgnoreCase),
            "StoredOriginalSnapshotChanged");
        foreach (var input in BuildRegistrationInputs(bundle))
            await RequireSourceSnapshotAsync(db, input);
    }

    private static async Task RequireSourceSnapshotAsync(PublicDataIngestionDbContext db, RegistrationInput input)
    {
        var snapshots = await db.RawSnapshots.AsNoTracking().Where(item => item.SourceId == SourceId
                && item.DatasetId == input.DatasetId)
            .ToListAsync();
        Require(snapshots.Count == 1 && SourceSnapshotMatches(snapshots[0], input),
            "SourceSnapshotMissingOrChanged:" + input.DatasetId);
    }

    private static async Task ValidateSourceSnapshotPreflightAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle)
    {
        foreach (var input in BuildRegistrationInputs(bundle))
        {
            var snapshots = await db.RawSnapshots.AsNoTracking()
                .Where(item => item.SourceId == SourceId && item.DatasetId == input.DatasetId)
                .ToListAsync();
            Require(snapshots.Count <= 1
                    && (snapshots.Count == 0 || SourceSnapshotMatches(snapshots[0], input)),
                "SourceSnapshotPreflightConflict:" + input.DatasetId);
        }
    }

    private static bool SourceSnapshotMatches(외부데이터RawSnapshot snapshot, RegistrationInput input)
    {
        var relative = input.RelativePath.Replace('\\', '/');
        return snapshot.FirstCollectionRunId > 0
               && snapshot.SourceId == SourceId
               && snapshot.DatasetId == input.DatasetId
               && snapshot.SourceVersion == input.SourceVersion
               && snapshot.EvidenceAsOfUtc == EvidenceAsOfUtc
               && snapshot.ContentHashSha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase)
               && snapshot.ContentLength == new FileInfo(input.Path).Length
               && snapshot.ContentType == input.ContentType
               && snapshot.OriginalFileName == Path.GetFileName(input.Path)
               && snapshot.StorageContainer == "local-private-public-spatial"
               && snapshot.StorageObjectName == relative
               && snapshot.StorageLocation == "private-file://" + relative;
    }

    private static bool Equivalent(외부데이터정규화Record stored, 외부데이터정규화Record expected)
        => stored.RecordKey == expected.RecordKey
            && stored.StableId == expected.StableId
            && stored.SourceId == expected.SourceId
            && stored.DatasetId == expected.DatasetId
            && stored.RegionStableId == expected.RegionStableId
            && stored.MetricCode == expected.MetricCode
            && stored.NumericValue == expected.NumericValue
            && stored.TextValue == expected.TextValue
            && stored.UnitCode == expected.UnitCode
            && stored.EvidenceAsOfUtc == expected.EvidenceAsOfUtc
            && stored.CollectedAtUtc == expected.CollectedAtUtc
            && stored.SpatialPrecisionCode == expected.SpatialPrecisionCode
            && stored.TemporalPrecisionCode == expected.TemporalPrecisionCode
            && stored.QualityCode == expected.QualityCode
            && stored.LimitationCode == expected.LimitationCode
            && stored.DimensionKey == expected.DimensionKey
            && stored.SourceVersion == expected.SourceVersion
            && stored.DataRevision == expected.DataRevision
            && stored.FirstSeenAtUtc == expected.FirstSeenAtUtc
            && stored.LastSeenAtUtc == expected.LastSeenAtUtc;

    private static Candidate CandidateFromStoredPayload(외부데이터정규화Record row)
    {
        using var document = JsonDocument.Parse(row.TextValue);
        var item = document.RootElement;
        Require(Text(item, "schemaVersion") == CandidateSchemaVersion
                && Text(item, "revision") == Revision
                && Text(item, "coordinateReferenceStatus") == CoordinateReferenceStatus
                && Text(item, "sourceHashSha256") == XlsxHash.ToLowerInvariant()
                && Text(item, "historicalBoundaryHashSha256") == BoundaryHash.ToLowerInvariant()
                && Text(item, "candidateSetHashSha256") == CandidateSetHash.ToLowerInvariant(),
            "StoredPayloadIdentityChanged");
        ValidateAuthority(item, "StoredPayload");
        var area = Text(item, "administrativeAreaStableId");
        var candidateAreasElement = item.GetProperty("candidateAdministrativeAreaStableIds");
        var candidateAreas = candidateAreasElement.EnumerateArray().Select(value => value.GetString()!).ToArray();
        var sourcePoint = item.GetProperty("sourcePointEpsg5186");
        var commonEnu = item.GetProperty("commonEnuMillimeters");
        var distanceElement = item.GetProperty("distanceToAssignedHistoricalBoundaryMeters");
        var qualityDiagnosticCodesElement = item.GetProperty("qualityDiagnosticCodes");
        var qualityDiagnosticCodes = qualityDiagnosticCodesElement.EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty).ToArray();
        var intersectionManagement = NullableText(item, "intersectionManagementNumber");
        var intersectionName = NullableText(item, "intersectionName");
        return new Candidate(
            Text(item, "sourceFeatureKey"), area, Text(item, "assignmentStateCode"),
            Text(item, "candidateQualityCode"), Text(item, "sourceBoroughName"),
            Text(item, "spatialAssignmentBoroughName"), Bool(item, "sourceDistrictSpatialAssignmentConflict"),
            distanceElement.GetDouble(), CanonicalJsonText(distanceElement), candidateAreas,
            CanonicalJsonText(candidateAreasElement), Int(item, "sourceSheetRow"), string.Empty,
            Text(item, "sourceBoroughName"),
            Text(item, "crosswalkManagementNumber"), intersectionManagement, intersectionName,
            CanonicalIntersection(intersectionManagement, intersectionName), Text(item, "crosswalkType"),
            Bool(item, "pedestrianSignalPresent"), qualityDiagnosticCodes,
            CanonicalJsonText(qualityDiagnosticCodesElement), Text(item, "ownershipBasisCode"),
            Double(sourcePoint, "x"), Double(sourcePoint, "y"),
            CanonicalJsonText(sourcePoint), Long(commonEnu, "x"), Long(commonEnu, "z"), CanonicalJsonText(commonEnu));
    }

    private static int SelfTest(Bundle bundle, IReadOnlyList<외부데이터정규화Record> rows)
    {
        var tests = 0;
        void Test(bool condition, string code)
        {
            Require(condition, "SelfTest:" + code);
            tests++;
        }

        Test(bundle.Candidates.Count == ExpectedCandidateRows && rows.Count == ExpectedCandidateRows, "ExactRows");
        Test(ComputeCandidateSetHash(bundle.Candidates) == CandidateSetHash, "CandidateSetHash");
        Test(ComputeCandidateSetHash(bundle.Candidates.Reverse().ToArray()) == CandidateSetHash, "CandidateHashOrderIndependence");
        var payloadCandidates = rows.Select(CandidateFromStoredPayload).ToArray();
        Test(ComputeCandidateSetHash(payloadCandidates) == CandidateSetHash, "StoredPayloadHashRoundTrip");
        Test(bundle.Candidates.Count(item => item.PedestrianSignalPresent) == ExpectedSignalPresentRows
             && bundle.Candidates.Count(item => !item.PedestrianSignalPresent) == ExpectedSignalAbsentRows, "SignalDistribution");
        Test(bundle.Candidates.Count(item => item.SourceDistrictSpatialAssignmentConflict) == ExpectedConflictRows
             && bundle.Candidates.Count(item => item.CandidateQualityCode == ConflictQuality) == ExpectedConflictRows,
            "SourceDistrictConflictDistribution");
        Test(rows.All(item => item.QualityCode == Quality && item.TextValue.Length <= 2_000
             && !item.TextValue.Contains("address", StringComparison.OrdinalIgnoreCase)
             && !item.TextValue.Contains("담당자", StringComparison.Ordinal)), "PrivatePayloadBoundary");
        Test(bundle.PerArea.Count == ExpectedAreaCount && bundle.PerArea.Sum(item => item.CandidateCount) == ExpectedCandidateRows,
            "AdministrativeAreaDistribution");
        Test(DatasetId != SupersededDatasetId
             && Revision != SupersededRevision
             && GenerationRelative.EndsWith(CandidateSetHash.ToLowerInvariant(), StringComparison.Ordinal)
             && BuildRegistrationInputs(bundle).Count == 13,
            "SupersededRevisionAndPrototypeIsolation");

        var snapshotInput = BuildRegistrationInputs(bundle)[0];
        var snapshotRelative = snapshotInput.RelativePath.Replace('\\', '/');
        var snapshot = new 외부데이터RawSnapshot
        {
            Id = 1,
            FirstCollectionRunId = 1,
            SourceId = SourceId,
            DatasetId = snapshotInput.DatasetId,
            SourceVersion = snapshotInput.SourceVersion,
            EvidenceAsOfUtc = EvidenceAsOfUtc,
            ContentHashSha256 = snapshotInput.Hash.ToLowerInvariant(),
            ContentLength = new FileInfo(snapshotInput.Path).Length,
            ContentType = snapshotInput.ContentType,
            OriginalFileName = Path.GetFileName(snapshotInput.Path),
            StorageContainer = "local-private-public-spatial",
            StorageObjectName = snapshotRelative,
            StorageLocation = "private-file://" + snapshotRelative
        };
        Test(SourceSnapshotMatches(snapshot, snapshotInput), "SourceSnapshotMetadataAccepted");
        snapshot.StorageObjectName += ".changed";
        Test(!SourceSnapshotMatches(snapshot, snapshotInput), "SourceSnapshotConflictRejectedBeforeWrite");

        var preservedRow = new 외부데이터정규화Record
        {
            Id = 1,
            RawSnapshotId = 2,
            RecordKey = "preserved-record",
            StableId = "preserved-stable-id",
            SourceId = SourceId,
            DatasetId = SupersededDatasetId,
            TextValue = "preserved-payload",
            EvidenceAsOfUtc = EvidenceAsOfUtc,
            CollectedAtUtc = DerivedAtUtc,
            DataRevision = SupersededRevision,
            FirstSeenAtUtc = DerivedAtUtc,
            LastSeenAtUtc = DerivedAtUtc
        };
        snapshot.StorageObjectName = snapshotRelative;
        snapshot.DatasetId = SupersededDatasetId;
        var preservedHash = ComputePreservedRevisionStateHash([preservedRow], [snapshot]);
        preservedRow.TextValue = "changed-payload";
        Test(ComputePreservedRevisionStateHash([preservedRow], [snapshot]) != preservedHash,
            "SupersededNormalizedMutationDetected");
        preservedRow.TextValue = "preserved-payload";
        snapshot.StorageLocation += ".changed";
        Test(ComputePreservedRevisionStateHash([preservedRow], [snapshot]) != preservedHash,
            "SupersededRawSnapshotMutationDetected");

        using (var authority = JsonDocument.Parse(JsonSerializer.Serialize(AuthorityDictionary(), CompactJson)))
        {
            ValidateAuthority(authority.RootElement, "SelfTestAuthority");
            Test(true, "AuthorityShape");
        }
        var leaked = AuthorityDictionary();
        leaked["runtimeAuthorized"] = true;
        var rejected = false;
        try
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(leaked, CompactJson));
            ValidateAuthority(document.RootElement, "SelfTestLeak");
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Test(rejected, "AuthorityLeakRejected");
        return tests;
    }

    private static void SetSummary(Dictionary<string, object?> result, string mode, Bundle bundle)
    {
        result["mode"] = mode;
        result["schemaVersion"] = CandidateSchemaVersion;
        result["dataRevision"] = Revision;
        result["candidateSetHashSha256"] = CandidateSetHash.ToLowerInvariant();
        result["scopeDefinitionHashSha256"] = ScopeDefinitionHash.ToLowerInvariant();
        result["generatorHashSha256"] = GeneratorHash.ToLowerInvariant();
        result["sourceRows"] = ExpectedSourceRows;
        result["coordinatePresentRows"] = ExpectedCoordinateRows;
        result["coordinateMissingRows"] = ExpectedCoordinateMissingRows;
        result["candidateRows"] = ExpectedCandidateRows;
        result["outsideScopeRows"] = ExpectedOutsideRows;
        result["unresolvedMultipleBoundaryRows"] = ExpectedMultipleRows;
        result["sourceDistrictSpatialAssignmentConflictRows"] = ExpectedConflictRows;
        result["administrativeAreaCount"] = ExpectedAreaCount;
        result["pedestrianSignalPresentRows"] = ExpectedSignalPresentRows;
        result["pedestrianSignalAbsentRows"] = ExpectedSignalAbsentRows;
        result["perAdministrativeArea"] = bundle.PerArea;
        result["reviewState"] = Quality;
        result["supersedesRevision"] = SupersededRevision;
        result["supersededRevisionAuditStatusCode"] = SupersededAuditStatus;
        result["supersededCandidateDispositionCode"] = SupersededDisposition;
        result["coordinateReferenceStatus"] = CoordinateReferenceStatus;
        result["authority"] = AuthorityDictionary();
        result["generationRelativePath"] = GenerationRelative;
    }

    private static string ComputeCandidateSetHash(IEnumerable<Candidate> source)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in new[]
        {
            CandidateSchemaVersion,
            Revision,
            DesignHash,
            XlsxHash,
            AcquisitionHash,
            BoundaryHash,
            R2ScopeDefinitionHash,
            R2ScopeManifestHash,
            ScopeDefinitionHash,
            OfficialStationAnchorSelectionHash,
            OfficialStationAnchorWorkbookHash,
            GeneratorHash,
            CoordinateValidationHash,
            HistoricalBoundaryConversionHash,
            FeasibilityDigest
        }) Append(hash, value);
        foreach (var candidate in source.OrderBy(item => item.SourceFeatureKey, StringComparer.Ordinal))
        {
            Append(hash, candidate.SourceFeatureKey);
            Append(hash, candidate.AdministrativeAreaStableId);
            Append(hash, candidate.AssignmentStateCode);
            Append(hash, candidate.CandidateQualityCode);
            Append(hash, candidate.SourceBoroughName);
            Append(hash, candidate.SpatialAssignmentBoroughName);
            Append(hash, candidate.DistanceToAssignedHistoricalBoundaryMetersCanonicalJson);
            Append(hash, candidate.CandidateAreaIdsCanonicalJson);
            Append(hash, candidate.CrosswalkManagementNumber);
            Append(hash, candidate.IntersectionCanonicalJson);
            Append(hash, candidate.CrosswalkType);
            Append(hash, candidate.PedestrianSignalPresent ? "true" : "false");
            Append(hash, candidate.SourcePointCanonicalJson);
            Append(hash, candidate.CommonEnuCanonicalJson);
            Append(hash, candidate.SourceDistrictSpatialAssignmentConflict ? "true" : "false");
            Append(hash, candidate.QualityDiagnosticCodesCanonicalJson);
            Append(hash, candidate.OwnershipBasisCode);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static string CanonicalIntersection(string? managementNumber, string? name)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["managementNumber"] = managementNumber,
            ["name"] = name
        }, CompactJson));
        return CanonicalJsonText(document.RootElement);
    }

    private static string CanonicalJsonText(JsonElement value)
        => Encoding.UTF8.GetString(CanonicalJsonBytes(value, blankRootContentHash: false));

    private static JsonElement ParseCanonicalJsonValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string ContentHash(JsonElement document)
        => Convert.ToHexString(SHA256.HashData(CanonicalJsonBytes(document, blankRootContentHash: true)));

    private static byte[] CanonicalJsonBytes(JsonElement value, bool blankRootContentHash)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false
        }))
        {
            WriteCanonical(writer, value, blankRootContentHash);
        }
        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value, bool blankRootContentHash = false)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    if (blankRootContentHash && property.Name == "contentHashSha256")
                        writer.WriteStringValue(string.Empty);
                    else
                        WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: false);
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
                throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonValueKindInvalid");
        }
    }

    private static void ValidateCounts(JsonElement value)
    {
        var actual = value.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["sourceRows"] = ExpectedSourceRows,
            ["coordinatePresentRows"] = ExpectedCoordinateRows,
            ["coordinateMissingRows"] = ExpectedCoordinateMissingRows,
            ["uniquelyAssignedRows"] = ExpectedCandidateRows,
            ["unresolvedMultipleBoundaryRows"] = ExpectedMultipleRows,
            ["outsideScopeRows"] = ExpectedOutsideRows,
            ["candidateRows"] = ExpectedCandidateRows,
            ["sourceDistrictSpatialAssignmentConflictRows"] = ExpectedConflictRows
        };
        Require(DictionaryEqual(actual, expected), "CountsChanged");
    }

    private static void ValidateQualityDiagnostics(JsonElement value)
    {
        RequireExactProperties(value, ["sourceDistrictSpatialAssignmentConflict"], "QualityDiagnosticRootChanged");
        var conflict = value.GetProperty("sourceDistrictSpatialAssignmentConflict");
        Require(Text(conflict, "diagnosticCode") == ConflictDiagnostic
                && Text(conflict, "candidateQualityCode") == ConflictQuality
                && Text(conflict, "ownershipDispositionCode") == "RetainedWithHistoricalBoundaryOwner"
                && Int(conflict, "count") == ExpectedConflictRows,
            "QualityDiagnosticIdentityChanged");
        var perArea = conflict.GetProperty("perAdministrativeArea").EnumerateObject()
            .ToDictionary(item => "region:kr:hjd:" + item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
        var pairs = conflict.GetProperty("sourceToSpatialBoroughPairs").EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
        var range = conflict.GetProperty("distanceToAssignedHistoricalBoundaryMeters");
        Require(DictionaryEqual(perArea, ExpectedConflictDistribution)
                && DictionaryEqual(pairs, ExpectedConflictPairs)
                && Double(range, "minimum") == 0.286d
                && Double(range, "maximum") == 22.076d,
            "QualityDiagnosticDistributionChanged");
    }

    private static void ValidateSourceHashes(JsonElement value)
    {
        var actual = value.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString()!, StringComparer.Ordinal);
        Require(DictionaryEqual(actual, ExpectedSourceHashes), "SourceHashSetChanged");
    }

    private static void ValidateComputedDistribution(IReadOnlyCollection<Candidate> candidates)
    {
        var actual = candidates.GroupBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Require(DictionaryEqual(actual, ExpectedDistribution), "ComputedDistributionChanged");
    }

    private static void ValidateComputedConflictDiagnostics(IReadOnlyCollection<Candidate> candidates)
    {
        var conflicts = candidates.Where(item => item.SourceDistrictSpatialAssignmentConflict).ToArray();
        var perArea = candidates.GroupBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(item => item.SourceDistrictSpatialAssignmentConflict), StringComparer.Ordinal);
        var pairs = conflicts.GroupBy(item => item.SourceBoroughName + ">" + item.SpatialAssignmentBoroughName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Require(conflicts.Length == ExpectedConflictRows
                && DictionaryEqual(perArea, ExpectedConflictDistribution)
                && DictionaryEqual(pairs, ExpectedConflictPairs)
                && conflicts.Min(item => item.DistanceToAssignedHistoricalBoundaryMeters) == 0.286d
                && conflicts.Max(item => item.DistanceToAssignedHistoricalBoundaryMeters) == 22.076d,
            "ComputedConflictDiagnosticsChanged");
    }

    private static void ValidateAuthority(JsonElement value, string source)
    {
        foreach (var name in TrueAuthorityFlags) Require(Bool(value, name), source + "AuthorityTrueFlagChanged:" + name);
        foreach (var name in FalseAuthorityFlags) Require(!Bool(value, name), source + "AuthorityFalseFlagChanged:" + name);
    }

    private static Dictionary<string, bool> AuthorityDictionary()
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var name in TrueAuthorityFlags) result.Add(name, true);
        foreach (var name in FalseAuthorityFlags) result.Add(name, false);
        return result;
    }

    private static bool DictionaryEqual<T>(IReadOnlyDictionary<string, T> first, IReadOnlyDictionary<string, T> second)
        => first.Count == second.Count && first.All(item => second.TryGetValue(item.Key, out var value)
            && EqualityComparer<T>.Default.Equals(item.Value, value));

    private static void RequireExactProperties(JsonElement value, IReadOnlyCollection<string> expected, string code)
    {
        Require(value.ValueKind == JsonValueKind.Object
                && value.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected)
                && value.EnumerateObject().Count() == expected.Count,
            code);
    }

    private static JsonDocument ReadJson(string path, long maximumBytes)
    {
        Require(File.Exists(path) && !IsReparsePoint(path), "JsonFileMissingOrUnsafe");
        var info = new FileInfo(path);
        Require(info.Length is > 0 && info.Length <= maximumBytes, "JsonFileSizeInvalid");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    private static string ResolveRepositoryPath(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative), "RepositoryRelativePathRequired");
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var path = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = normalizedRoot + Path.DirectorySeparatorChar;
        Require(path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "PathOutsideRepository");
        Require(!IsReparsePoint(normalizedRoot), "RepositoryRootReparsePointRejected");
        Require(!IsReparsePoint(path), "RepositoryPathReparsePointRejected");
        var cursor = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(cursor))
        {
            Require(!IsReparsePoint(cursor), "RepositoryPathReparsePointRejected");
            if (string.Equals(cursor, normalizedRoot, StringComparison.OrdinalIgnoreCase)) break;
            cursor = Path.GetDirectoryName(cursor);
        }
        Require(string.Equals(cursor, normalizedRoot, StringComparison.OrdinalIgnoreCase), "RepositoryPathParentInvalid");
        return path;
    }

    private static string ContentType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".py" => "text/x-python",
            ".ndjson" => "application/x-ndjson",
            _ => "application/octet-stream"
        };

    private static bool IsReparsePoint(string path)
        => File.Exists(path) || Directory.Exists(path)
            ? (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
            : false;

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Text(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? string.Empty
            : throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonTextContract:" + property);

    private static string? NullableText(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var item))
            throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonNullableTextContract:" + property);
        return item.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => item.GetString(),
            _ => throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonNullableTextContract:" + property)
        };
    }

    private static int Int(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonIntegerContract:" + property);

    private static long Long(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt64(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonLongContract:" + property);

    private static double Double(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetDouble(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonDoubleContract:" + property);

    private static bool Bool(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? item.GetBoolean()
            : throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:JsonBooleanContract:" + property);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException("AdministrativeDongCrosswalkCandidate:" + code);
    }
}
