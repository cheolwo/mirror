using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// OA-21208의 2020 도보망과 2023 역사 행정동 경계를 결속한 조사 후보만
// 로컬 비공개 원장에 보존한다. 현행 통행·보도 폭·차량/오토바이 경로·Unity 권위는 만들지 않는다.
internal static class 행정동보행망후보
{
    private const string SourceId = "seoul-open-data-admin-dong-walk-network-private-review";
    private const string DatasetId = "northeast-seoul-admin-dong-walk-network-candidate-g3c-r1";
    private const string CandidateSnapshotDatasetId = DatasetId + "-artifact-candidates";
    private const string MetricCode = "administrative-dong-walk-network-candidate";
    private const string Revision = "northeast-seoul-admin-dong-walk-network-candidate.g3c.r1";
    private const string ScopeStableId = "scope:administrative-dong-walk-network:northeast-seoul-rider:g3c:r1";
    private const string CandidateSchemaVersion = "administrative-dong-walk-network-candidate.v1";
    private const string ProtectedPayloadSchemaVersion = "administrative-dong-walk-network-protected-rdb-payload.v1";
    private const string Quality = "PendingHumanReview";
    private const string CompletionUpperBound = "LocalPrivateHistoricalWalkNetworkCandidateStoredAndVerified";
    private const string LedgerRecordCode = "LocalPrivateHistoricalWalkNetworkCandidateLedgerRecord";
    private const string Limitation = "PrivateReviewOnly;Historical2020Source;HistoricalBoundary2023;DistrictHaloIncomplete;NoCurrentPassability;NoWidth;NoCurb;NoEntrance;NoMotorcycle;NoVehicleLane;NoSignal;NoDistribution;NoRuntime;NoTraversal;NoGameplay;NoUnity";
    private const string LockName = "mirror:public-data:admin-dong-walk-network-g3c-r1";

    private const string DesignRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-walk-network-candidate.implementation.r13.md";
    private const string WorkOrderRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-walk-network-candidate.data-implementation.v1.json";
    private const string ScopeRelative = "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-walk-network.g3c.r1.json";
    private const string GeneratorRelative = "eng/neighborhood/administrative_dong_walk_network_candidate.py";
    private const string OutputRelative = "artifacts/local/public-data/admin-dong-walk-network-northeast-seoul-20260915-g3c-r1";
    private const string GenerationRelative = OutputRelative + "/generations/43248f7f563dcdfe059650f3ec000085cd0d7cfb65fc5300efdfcbdd685990aa";

    private const string DesignHash = "3FACFF9E4671C907C5AD4DB44A76BD134E5D12FCE74B939265F64361044851B3";
    private const string WorkOrderHash = "D8B274862268B773C33AD8D43FAAE95DFE79761AF6784ADAC5DA3D2718E60252";
    private const string ScopeDefinitionHash = "E6E487E6A83B625A81698CA101A6ED13DCB787C36F6E98AF2911C05FD0B1732B";
    private const string GeneratorHash = "E132CB0EAA961A0ED421A3EB2175078C3E81D6B7C9C2FF08638D88FE9092A208";
    private const string CandidateSetHash = "43248F7F563DCDFE059650F3EC000085CD0D7CFB65FC5300EFDFCBDD685990AA";
    private const string ManifestHash = "A030BEAC34681E9CEF941ADF6B4CABB9F19A3269FD61D467A234043A92B12F8E";
    private const string CandidatesHash = "9775B1EDD02A1922583FAEDB5253E83290A4318DF61A196E958A61867EEF0B23";
    private const string AuditHash = "100D5886FB63AC60DF0794CD8A5BE9BB57A716DCED8784115D2B4FFA51428F49";
    private const string GeneratorSourceHash = GeneratorHash;
    private const string CompleteHash = "3485F738B0BCF13FB0DE49B1781BDFB31511230C1A4DCBB6AF2C9F360AC3BED7";
    private const string ManifestContentHash = "EA3028775557E288A1097CB7FA17F36AC38C68E7CB45C1BA8FC6DA2D9A502F3C";
    private const string AuditContentHash = "202D919BC50199E595BC2F762F6A43E2150AD9C5BC715D05FECC411BC187D576";
    private const string CompleteContentHash = "5466F39EC9F17F358CF713590390399E3FF12CDB3FDD562F931CE76DD3134B66";

    private const int ExpectedSourceRows = 59_724;
    private const int ExpectedNodeSourceRows = 25_680;
    private const int ExpectedLinkSourceRows = 34_044;
    private const int ExpectedOutsideRows = 19_770;
    private const int ExpectedCandidateRows = 40_739;
    private const int ExpectedNodeCandidates = 17_267;
    private const int ExpectedLinkFragments = 23_472;
    private const int ExpectedAreaCount = 30;
    private const int ExpectedDistrictConflicts = 48;
    private const int ExpectedSpanningLinks = 728;
    private const int ExpectedSnapshots = 17;

    private static readonly DateTimeOffset EvidenceAsOfUtc =
        new(2020, 12, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CollectedAtUtc =
        new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] HashHeaderFields =
    [
        "candidateSchemaVersion", "revision", "scopeDefinitionHashSha256", "designHashSha256",
        "dataImplementationHashSha256", "generatorHashSha256", "sourceHashesCanonicalJson",
        "authorityFlagsCanonicalJson", "candidateCountCanonicalJson"
    ];

    private static readonly string[] HashCandidateFields =
    [
        "candidateStableId", "candidateKindCode", "administrativeAreaStableId", "assignmentStateCode",
        "sourceFeatureIdSha256", "sourceSemanticBodySha256", "sourceCompleteBodySha256",
        "sourceFileHashSha256", "sourceOccurrenceKey", "sourceBoroughCode", "sourceBoroughName",
        "sourceLegalDongCode", "sourceLegalDongName", "sourceTypeCode", "geometryCanonicalJson",
        "sourceReportedActorCodesCanonicalJson", "sourceBeginNodeIdSha256", "sourceEndNodeIdSha256",
        "sourceReportedLengthMetersCanonicalJson", "fragmentOrdinalCanonicalJson",
        "physicalFragmentHashSha256", "sharedAdministrativeAreaIdsCanonicalJson",
        "qualityDiagnosticCodesCanonicalJson", "authorityFlagsCanonicalJson"
    ];

    private static readonly string[] NodeCandidateProperties =
    [
        "administrativeAreaDisplayName", "administrativeAreaStableId", "assignmentStateCode",
        "authorityFlags", "candidateKindCode", "candidateStableId", "legalAreaStableId",
        "pointEpsg5186Millimeters", "qualityDiagnosticCodes", "revision", "schemaVersion",
        "sourceBoroughCode", "sourceBoroughName", "sourceCompleteBodySha256",
        "sourceCsvDataRowNumber", "sourceFeatureIdSha256", "sourceFileHashSha256", "sourceKindCode",
        "sourceLegalDongCode", "sourceLegalDongName", "sourceNodeTypeCode", "sourceOccurrenceKey",
        "sourceRonum", "sourceSemanticBodySha256", "sourceWorkTimestamp"
    ];

    private static readonly string[] LinkCandidateProperties =
    [
        "administrativeAreaDisplayName", "administrativeAreaStableId", "assignmentStateCode",
        "authorityFlags", "boundaryCoincidentShared", "candidateKindCode", "candidateStableId",
        "fragmentGeometryEpsg5186Millimeters", "fragmentLengthMeters", "fragmentOrdinal",
        "legalAreaStableId", "physicalFragmentHashSha256", "qualityDiagnosticCodes", "revision",
        "schemaVersion", "sharedAdministrativeAreaIds", "sourceBeginNodeIdSha256", "sourceBoroughCode",
        "sourceBoroughName", "sourceCompleteBodySha256", "sourceCsvDataRowNumber",
        "sourceEndNodeIdSha256", "sourceFeatureIdSha256", "sourceFileHashSha256", "sourceFlags",
        "sourceKindCode", "sourceLegalDongCode", "sourceLegalDongName", "sourceLinkTypeCode",
        "sourceOccurrenceKey", "sourceReportedActorCodes", "sourceReportedLengthMeters",
        "sourceRonum", "sourceSemanticBodySha256", "sourceWorkTimestamp"
    ];

    private static readonly string[] TrueAuthorityFlags =
        ["privateReviewOnly", "historicalBoundaryBootstrapOnly", "observationCandidateOnly", "sourceDeclaredCoordinateReference"];

    private static readonly string[] FalseAuthorityFlags =
    [
        "currentAdministrativeBoundaryEstablished", "currentPassabilityEstablished", "sidewalkWidthEstablished",
        "curbEstablished", "entranceBindingEstablished", "motorcycleAccessEstablished",
        "vehicleLaneEstablished", "signalBindingEstablished", "distributionApproved", "publicDisplayAllowed",
        "runtimeAuthorized", "traversalReady", "gameplayReady", "unityApplyAllowed"
    ];

    private static readonly Regex UpperDigest = new("^[0-9A-F]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex CandidateStableIdPattern = new(
        "^walk-(?:node|link-fragment)-candidate:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex AreaStableIdPattern = new(
        "^region:kr:hjd:[0-9]{10}$", RegexOptions.CultureInvariant);
    private static readonly Regex OccurrencePattern = new(
        "^(?:gwangjinCsv|dongdaemunCsv|jungnangCsv):csv-data-row:[1-9][0-9]*$",
        RegexOptions.CultureInvariant);

    private static readonly string[] PreservationLockNames =
    [
        "mirror:public-data:admin-dong-business-g4a-r1",
        "mirror:public-data:admin-dong-business-g4a-r2",
        "mirror:public-data:admin-dong-crosswalk-g3a-r1",
        "mirror:public-data:admin-dong-crosswalk-g3a-r2",
        "mirror:public-data:admin-dong-intersection-g3b-r1",
        LockName,
        "mirror:public-data:myeonmok-business",
        "mirror:public-data:sagajeong-spatial-supplement-r1"
    ];

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private sealed record FrozenSource(
        string Role,
        string Path,
        string RelativePath,
        string Hash,
        long Length,
        string SourceRevision);

    private sealed record AreaCounts(int NodeCandidates, int LinkFragmentCandidates, int CandidateRows);

    private sealed record Candidate(
        string CandidateStableId,
        string CandidateIdentitySha256,
        string CandidateKindCode,
        string AdministrativeAreaStableId,
        string AssignmentStateCode,
        string SourceFileHashSha256,
        string SourceTypeCode,
        string GeometryBodyHashSha256,
        double? FragmentLengthMeters,
        IReadOnlyList<string> QualityDiagnosticCodes,
        string CandidateBodyHashSha256);

    private sealed record CandidateScan(
        string CandidateSetHashSha256,
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyDictionary<string, AreaCounts> PerArea,
        int DistrictConflictCandidates);

    private sealed record RegistrationInput(
        string DatasetId,
        string Path,
        string RelativePath,
        string Hash,
        long Length,
        string SourceVersion,
        string ContentType,
        bool CanonicalSource);

    private sealed record Bundle(
        string Root,
        string ScopePath,
        string GeneratorPath,
        string ManifestPath,
        string CandidatesPath,
        string AuditPath,
        string GeneratorSourcePath,
        string CompletePath,
        IReadOnlyDictionary<string, FrozenSource> Sources,
        IReadOnlyList<string> HeaderValues,
        IReadOnlyDictionary<string, AreaCounts> ExpectedPerArea,
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyList<RegistrationInput> Registrations);

    private sealed record ProtectedLedger(
        string Code,
        string SourceId,
        string DatasetId,
        string Revision,
        int ExpectedRows,
        int ExpectedSnapshots);

    private sealed record PreservationState(
        int LedgerCount,
        int NormalizedRows,
        int RawSnapshots,
        int IngestionRuns,
        string StateHashSha256);

    private static readonly ProtectedLedger[] ProtectedLedgers =
    [
        new("g3a-r1", "seoul-open-data-admin-dong-crosswalk-private-review",
            "northeast-seoul-admin-dong-crosswalk-candidate-g3a-r1",
            "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r1", 1_533, 11),
        new("g3a-r2", "seoul-open-data-admin-dong-crosswalk-private-review",
            "northeast-seoul-admin-dong-crosswalk-candidate-g3a-r2",
            "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r2", 1_533, 13),
        new("g3b-r1", "seoul-open-data-admin-dong-intersection-private-review",
            "northeast-seoul-admin-dong-intersection-point-candidate-g3b-r1",
            "northeast-seoul-admin-dong-intersection-point-candidate.g3b.r1", 554, 19),
        new("g4a-r1", "semas-admin-dong-business-private-review",
            "northeast-seoul-admin-dong-business-candidate-g4a-r1",
            "northeast-seoul-admin-dong-business-candidate.g4a.r1", 29_721, 19),
        new("g4a-r2", "semas-admin-dong-business-private-review",
            "northeast-seoul-admin-dong-business-candidate-g4a-r2",
            "northeast-seoul-admin-dong-business-candidate.g4a.r2", 29_721, 19),
        new("sagajeong-r27-oa21208", "seoul-open-data-sagajeong-spatial-supplement",
            "oa-21208-pedestrian-network", "sagajeong-spatial-supplement-20260914.r1", 2_902, 1)
    ];

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "ModeInvalid");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        // 모든 로컬 파일과 후보 집합은 DB 연결보다 먼저 검증한다.
        var bundle = LoadAndValidateBundle(root);
        var rows = BuildRows(bundle);
        SetSummary(result, mode, bundle, rows);

        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest(bundle, rows);
            result["status"] = "PASS";
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        var preservationBefore = await ReadPreservationStateAsync(options);
        SetPreservationSummary(result, preservationBefore);

        if (mode == "preview")
        {
            await using var preview = new PublicDataIngestionDbContext(options);
            await ValidateSnapshotPreflightAsync(preview, bundle);
            var existing = await LoadScopedRowsAsync(preview);
            await ValidateExistingAsync(preview, bundle, existing, rows, requireComplete: false);
            result["beforeCount"] = existing.Count;
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            result["status"] = "PASS";
            return;
        }

        if (mode == "apply")
            await ApplyAsync(options, bundle, rows, preservationBefore, result);
        else
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
        }

        await VerifyReadbackAsync(options, bundle, rows, result);
        var preservationAfter = await ReadPreservationStateAsync(options);
        Require(preservationAfter == preservationBefore, "ProtectedLedgerChangedAfterOperation");
        SetPreservationSummary(result, preservationAfter);
        result["status"] = "PASS";
    }

    private static Bundle LoadAndValidateBundle(string root)
    {
        var designPath = ResolveRepositoryPath(root, DesignRelative);
        var workOrderPath = ResolveRepositoryPath(root, WorkOrderRelative);
        var scopePath = ResolveRepositoryPath(root, ScopeRelative);
        var generatorPath = ResolveRepositoryPath(root, GeneratorRelative);
        Require(HashFile(designPath) == DesignHash, "DesignHashChanged");
        Require(HashFile(workOrderPath) == WorkOrderHash, "WorkOrderHashChanged");
        Require(HashFile(scopePath) == ScopeDefinitionHash, "ScopeDefinitionHashChanged");
        Require(HashFile(generatorPath) == GeneratorHash, "GeneratorHashChanged");

        ValidateWorkOrder(workOrderPath);
        using var scopeDocument = ReadJson(scopePath, 128_000);
        var sources = ValidateScope(scopeDocument.RootElement, root);

        var generationPath = ResolveRepositoryPath(root, GenerationRelative);
        Require(Directory.Exists(generationPath) && !IsReparsePoint(generationPath),
            "GenerationDirectoryMissingOrUnsafe");
        var expectedFiles = new HashSet<string>(
            ["manifest.json", "candidates.ndjson", "audit.json", "generator-source.py", "complete.json"],
            StringComparer.Ordinal);
        Require(Directory.EnumerateFiles(generationPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal).SetEquals(expectedFiles),
            "GenerationFileSetChanged");

        var manifestPath = Path.Combine(generationPath, "manifest.json");
        var candidatesPath = Path.Combine(generationPath, "candidates.ndjson");
        var auditPath = Path.Combine(generationPath, "audit.json");
        var generatorSourcePath = Path.Combine(generationPath, "generator-source.py");
        var completePath = Path.Combine(generationPath, "complete.json");
        RequireFile(manifestPath, ManifestHash, 9_434, "Manifest");
        RequireFile(candidatesPath, CandidatesHash, 98_781_674, "Candidates");
        RequireFile(auditPath, AuditHash, 6_486, "Audit");
        RequireFile(generatorSourcePath, GeneratorSourceHash, 64_644, "GeneratorSource");
        Require(File.ReadAllBytes(generatorSourcePath).SequenceEqual(File.ReadAllBytes(generatorPath)),
            "GeneratorSourceSnapshotChanged");
        RequireFile(completePath, CompleteHash, 1_298, "Complete");

        using var manifestDocument = ReadJson(manifestPath, 128_000);
        using var auditDocument = ReadJson(auditPath, 128_000);
        using var completeDocument = ReadJson(completePath, 32_000);
        var (headerValues, expectedPerArea) = ValidateManifest(manifestDocument.RootElement, sources);
        ValidateAudit(auditDocument.RootElement, expectedPerArea);
        ValidateComplete(completeDocument.RootElement);

        var scan = ScanCandidates(candidatesPath, headerValues, expectedPerArea, collectCandidates: true);
        Require(scan.CandidateSetHashSha256 == CandidateSetHash, "CandidateSetHashMismatch");
        var registrations = BuildRegistrationInputs(root, sources, designPath, workOrderPath, scopePath,
            generatorPath, manifestPath, candidatesPath, auditPath, generatorSourcePath, completePath);
        return new Bundle(root, scopePath, generatorPath, manifestPath, candidatesPath, auditPath,
            generatorSourcePath, completePath, sources, headerValues, expectedPerArea, scan.Candidates,
            registrations);
    }

    private static void ValidateWorkOrder(string path)
    {
        using var document = ReadJson(path, 128_000);
        var value = document.RootElement;
        Require(Text(value, "schemaVersion") == "public-data-candidate-ledger-implementation.v1"
                && Text(value, "implementationStableId") ==
                    "data-implementation:administrative-dong-walk-network:g3c:r1"
                && Text(value, "revision") == Revision
                && Text(value, "workKindCode") == "LocalPrivateHistoricalWalkNetworkCandidateLedger"
                && !Bool(value, "playableLoopWorkOrder")
                && value.GetProperty("evidenceStageClaimed").ValueKind == JsonValueKind.Null
                && Text(value, "statusCode") == "ApprovedForLocalPrivateCandidateGenerationAndLedger"
                && Text(value, "completionCode") == CompletionUpperBound,
            "WorkOrderIdentityChanged");
        var gate = value.GetProperty("planningGate");
        Require(Text(gate, "designDocumentRef") == DesignRelative
                && Text(gate, "designRevision") ==
                    "administrative-dong-diorama:walk-network-candidate.g3c.r1"
                && Text(gate, "designHashSha256") == DesignHash,
            "WorkOrderPlanningGateChanged");
        var persistence = value.GetProperty("persistence");
        Require(Text(persistence, "sourceId") == SourceId
                && Text(persistence, "datasetId") == DatasetId
                && Text(persistence, "dataRevision") == Revision
                && Text(persistence, "qualityCode") == Quality
                && Text(persistence, "namedLock") == LockName
                && !Bool(persistence, "newMigrationAllowed")
                && !Bool(persistence, "currentPointerAllowed"),
            "WorkOrderPersistenceChanged");
        var expectedCounts = value.GetProperty("expectedCounts");
        ValidateExpectedCounts(expectedCounts);
        Require(Int(expectedCounts, "registeredRawSnapshots") == ExpectedSnapshots,
            "WorkOrderRegisteredSnapshotCountChanged");
        ValidateHashContract(value.GetProperty("candidateHashContract"));
        var snapshots = value.GetProperty("snapshotContract");
        Require(Int(snapshots, "exactSnapshotCount") == ExpectedSnapshots
                && Text(snapshots, "canonicalNormalizedSource") == "candidates.ndjson"
                && Int(snapshots, "sourceInputs") == 8
                && Int(snapshots, "designAndToolInputs") == 4
                && Int(snapshots, "generationArtifacts") == 5
                && Bool(snapshots, "extraSnapshotOrMissingSnapshotRejected"),
            "WorkOrderSnapshotContractChanged");
        ValidatePreservationContract(value.GetProperty("preservationContract"));
        ValidateAuthority(value.GetProperty("authorityFlags"), "WorkOrder");
    }

    private static void ValidatePreservationContract(JsonElement value)
    {
        Require(value.ValueKind == JsonValueKind.Array, "WorkOrderPreservationContractTypeChanged");
        var expected = new Dictionary<string, (string SourceId, string DatasetId, int Rows, int Snapshots)>(
            StringComparer.Ordinal)
        {
            ["G3a-r2"] = ("seoul-open-data-admin-dong-crosswalk-private-review",
                "northeast-seoul-admin-dong-crosswalk-candidate-g3a-r2", 1_533, 13),
            ["G3b-r1-ledger"] = ("seoul-open-data-admin-dong-intersection-private-review",
                "northeast-seoul-admin-dong-intersection-point-candidate-g3b-r1", 554, 19),
            ["G4a-r2"] = ("semas-admin-dong-business-private-review",
                "northeast-seoul-admin-dong-business-candidate-g4a-r2", 29_721, 19),
            ["Sagajeong-r27-OA-21208"] = ("seoul-open-data-sagajeong-spatial-supplement",
                "oa-21208-pedestrian-network", 2_902, 1)
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            RequireExactProperties(item,
                ["revisionCode", "sourceId", "datasetId", "expectedNormalizedRows", "expectedRawSnapshots"],
                "WorkOrderPreservationEntryPropertiesChanged");
            var code = Text(item, "revisionCode");
            Require(seen.Add(code) && expected.TryGetValue(code, out var contract)
                    && Text(item, "sourceId") == contract.SourceId
                    && Text(item, "datasetId") == contract.DatasetId
                    && Int(item, "expectedNormalizedRows") == contract.Rows
                    && Int(item, "expectedRawSnapshots") == contract.Snapshots,
                "WorkOrderPreservationEntryChanged");
        }
        Require(seen.SetEquals(expected.Keys), "WorkOrderPreservationContractSetChanged");
    }

    private static IReadOnlyDictionary<string, FrozenSource> ValidateScope(JsonElement value, string root)
    {
        Require(Text(value, "schemaVersion") == "administrative-dong-walk-network-generation-scope.v1"
                && Text(value, "scopeStableId") == ScopeStableId
                && Text(value, "revision") == Revision
                && Text(value, "sourceVintage") ==
                    "oa21208-year2020-portal-update-20260914+oa22160-file-20231031+northeast-seoul-rider-r2+g3c-r1"
                && Text(value, "reviewStatus") == Quality
                && Text(value, "sourceReferenceVintage") == "2020"
                && value.GetProperty("sourceReferenceDateExact").ValueKind == JsonValueKind.Null
                && Text(value, "sourceCoordinateReference") == "WGS84"
                && Text(value, "assignmentCoordinateReference") == "EPSG:5186"
                && !Bool(value, "sourceDistrictFilterBoundaryHaloComplete"),
            "ScopeIdentityChanged");
        var gate = value.GetProperty("planningGate");
        Require(Text(gate, "statusCode") == "ApprovedForLocalPrivateCandidateGenerationAndLedger"
                && Text(gate, "designDocumentRef") == DesignRelative
                && Text(gate, "designHashSha256") == DesignHash
                && Text(gate, "dataImplementationRef") == WorkOrderRelative
                && Text(gate, "dataImplementationHashSha256") == WorkOrderHash,
            "ScopePlanningGateChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "Scope");
        var counts = value.GetProperty("expectedSourceCounts");
        Require(Int(counts, "gwangjinRows") == 16_288
                && Int(counts, "dongdaemunRows") == 21_988
                && Int(counts, "jungnangRows") == 21_448
                && Int(counts, "threeBoroughRows") == ExpectedSourceRows
                && Int(counts, "historicalAdministrativeAreas") == ExpectedAreaCount,
            "ScopeExpectedSourceCountsChanged");

        var expectedRoles = new HashSet<string>(
        [
            "receipt", "codebook", "gwangjinCsv", "dongdaemunCsv", "jungnangCsv",
            "oa22160BoundaryArchive", "r2ScopeDefinition", "r2ScopeManifest"
        ], StringComparer.Ordinal);
        var sources = new Dictionary<string, FrozenSource>(StringComparer.Ordinal);
        foreach (var item in value.GetProperty("sources").EnumerateArray())
        {
            var role = Text(item, "role");
            Require(expectedRoles.Contains(role) && !sources.ContainsKey(role), "ScopeSourceRoleChanged");
            var relative = Text(item, "repositoryRelativePath");
            var path = ResolveRepositoryPath(root, relative);
            var hash = Text(item, "contentHashSha256");
            var length = Long(item, "byteLength");
            RequireFile(path, hash, length, "FrozenSource:" + role);
            sources.Add(role, new FrozenSource(role, path, relative, hash, length,
                Text(item, "sourceRevision")));
        }
        Require(sources.Count == expectedRoles.Count
                && sources.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedRoles)
                && sources["receipt"].Hash == "5280F1BB76CB49B875642F5D4A65B43EEA0A555F686A2C18A32A04CF969B960E"
                && sources["codebook"].Hash == "C718A21352D91B2A9BE50E8C52ED886D92A495ADB14070995B487B0CA5AB4E4F"
                && sources["gwangjinCsv"].Hash == "193806EE9ADE7E294A92E1BDAF9CA3312D2691CCE453C5E68DAE0F05E76F2D02"
                && sources["dongdaemunCsv"].Hash == "2E30FDD65C327FCD3C132D35D2E9C6CE43CE94A21A47EE70321762F1A023D7B9"
                && sources["jungnangCsv"].Hash == "08B904A586A7BFD7EF9FDAB4DA92BCEBC4B5D4B61FD41F09F33C2A35F5AFF773"
                && sources["oa22160BoundaryArchive"].Hash == "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68",
            "ScopeSourceExactSetChanged");

        var output = value.GetProperty("output");
        Require(Text(output, "repositoryRelativeDirectory") == OutputRelative
                && Text(output, "generationDirectoryName") == "generations"
                && Text(output, "candidateSchemaVersion") == CandidateSchemaVersion
                && !Bool(output, "currentPointerAllowed")
                && Text(output, "generatorRelativePath") == GeneratorRelative
                && Text(output, "generatorSha256") == GeneratorHash,
            "ScopeOutputChanged");
        return sources;
    }

    private static (IReadOnlyList<string> HeaderValues, IReadOnlyDictionary<string, AreaCounts> PerArea)
        ValidateManifest(JsonElement value, IReadOnlyDictionary<string, FrozenSource> sources)
    {
        RequireExactProperties(value,
        [
            "schemaVersion", "revision", "scopeStableId", "candidateSetHashSha256", "candidateRows",
            "administrativeAreaCount", "sourceReferenceVintage", "sourceCoordinateReference",
            "boundaryReferenceVintage", "boundaryCoordinateReference", "assignmentCoordinateReference",
            "sourceHashes", "scopeDefinitionHashSha256", "designHashSha256",
            "dataImplementationHashSha256", "generatorHashSha256", "candidateHashContract",
            "candidateHashHeaderValues", "qualityCode", "authorityFlags", "perAdministrativeArea",
            "contentHashSha256"
        ], "ManifestPropertySetChanged");
        Require(Text(value, "schemaVersion") == "administrative-dong-walk-network-candidate-manifest.v1"
                && Text(value, "revision") == Revision
                && Text(value, "scopeStableId") == ScopeStableId
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Int(value, "candidateRows") == ExpectedCandidateRows
                && Int(value, "administrativeAreaCount") == ExpectedAreaCount
                && Text(value, "sourceReferenceVintage") == "2020"
                && Text(value, "sourceCoordinateReference") == "WGS84"
                && Text(value, "boundaryReferenceVintage") == "2023-10-31"
                && Text(value, "boundaryCoordinateReference") == "EPSG:5181"
                && Text(value, "assignmentCoordinateReference") == "EPSG:5186"
                && Text(value, "scopeDefinitionHashSha256") == ScopeDefinitionHash
                && Text(value, "designHashSha256") == DesignHash
                && Text(value, "dataImplementationHashSha256") == WorkOrderHash
                && Text(value, "generatorHashSha256") == GeneratorHash
                && Text(value, "qualityCode") == Quality
                && Text(value, "contentHashSha256") == ManifestContentHash
                && ComputeContentHash(value) == ManifestContentHash,
            "ManifestIdentityChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "Manifest");
        ValidateHashContract(value.GetProperty("candidateHashContract"));

        var sourceHashes = value.GetProperty("sourceHashes");
        RequireExactProperties(sourceHashes, sources.Keys, "ManifestSourceHashPropertySetChanged");
        foreach (var source in sources.Values)
            Require(Text(sourceHashes, source.Role) == source.Hash, "ManifestSourceHashChanged:" + source.Role);

        var headerValues = value.GetProperty("candidateHashHeaderValues").EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : throw new InvalidDataException("AdministrativeDongWalkNetworkCandidate:HeaderValueTypeChanged"))
            .ToArray();
        var expectedHeaderValues = new[]
        {
            CandidateSchemaVersion,
            Revision,
            ScopeDefinitionHash,
            DesignHash,
            WorkOrderHash,
            GeneratorHash,
            CanonicalJsonText(sourceHashes),
            CanonicalJsonText(value.GetProperty("authorityFlags")),
            ExpectedCandidateRows.ToString(CultureInfo.InvariantCulture)
        };
        Require(headerValues.SequenceEqual(expectedHeaderValues, StringComparer.Ordinal),
            "ManifestCandidateHashHeaderValuesChanged");

        var perArea = ReadPerArea(value.GetProperty("perAdministrativeArea"));
        ValidatePerAreaTotals(perArea);
        return (headerValues, perArea);
    }

    private static void ValidateAudit(
        JsonElement value,
        IReadOnlyDictionary<string, AreaCounts> expectedPerArea)
    {
        RequireExactProperties(value,
        [
            "schemaVersion", "revision", "candidateSetHashSha256", "counts", "perAdministrativeArea",
            "boundaryCount", "lengthPreservation", "occurrenceIdentity", "sourceVintageMismatchCode",
            "sourceDistrictFilterBoundaryHaloComplete", "codebookSemanticValidationCompleted",
            "allCsvRowsBoroughFilterValidationCompleted", "authorityFlags", "contentHashSha256"
        ], "AuditPropertySetChanged");
        Require(Text(value, "schemaVersion") == "administrative-dong-walk-network-candidate-audit.v1"
                && Text(value, "revision") == Revision
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Int(value, "boundaryCount") == ExpectedAreaCount
                && Text(value, "sourceVintageMismatchCode") ==
                    "Oa21208Year2020WithOa22160Boundary20231031"
                && !Bool(value, "sourceDistrictFilterBoundaryHaloComplete")
                && Bool(value, "codebookSemanticValidationCompleted")
                && Bool(value, "allCsvRowsBoroughFilterValidationCompleted")
                && Text(value, "contentHashSha256") == AuditContentHash
                && ComputeContentHash(value) == AuditContentHash,
            "AuditIdentityChanged");
        ValidateExpectedCounts(value.GetProperty("counts"));
        Require(DictionaryEqual(ReadPerArea(value.GetProperty("perAdministrativeArea")), expectedPerArea),
            "AuditPerAreaChanged");
        var length = value.GetProperty("lengthPreservation");
        Require(Text(length, "unit") == "meter"
                && Double(length, "comparisonToleranceMetersPerSourceLink") == 0.002d
                && Double(length, "targetIntersectionLengthMeters") == 882_519.308089d
                && Double(length, "uniqueUnionFragmentLengthMeters") == 882_519.308089d
                && Double(length, "assignedFragmentLengthIncludingBoundarySharedDuplicatesMeters") ==
                    882_519.308089d
                && Double(length, "maximumSourceLinkPreservationErrorMeters") == 0d
                && Double(length, "serializedBoundaryBufferToleranceMeters") == 0.002d
                && Double(length, "serializedOutsideLengthToleranceMeters") == 0.001d,
            "AuditLengthPreservationChanged");
        var occurrence = value.GetProperty("occurrenceIdentity");
        Require(!Bool(occurrence, "sourceRonumColumnPresent")
                && Text(occurrence, "methodCode") ==
                    "FrozenBoroughCsvRolePlusOneBasedCsvDataRowNumber"
                && Bool(occurrence, "completeDuplicateRowsRemainDistinctOccurrences"),
            "AuditOccurrenceIdentityChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "Audit");
    }

    private static void ValidateComplete(JsonElement value)
    {
        RequireExactProperties(value,
        [
            "schemaVersion", "revision", "completeMarker", "candidateSetHashSha256", "files",
            "currentPointerCreated", "databasePersistenceCompleted", "runtimeAuthorized", "traversalReady",
            "gameplayReady", "contentHashSha256"
        ], "CompletePropertySetChanged");
        Require(Text(value, "schemaVersion") == "administrative-dong-walk-network-candidate-complete.v1"
                && Text(value, "revision") == Revision
                && Bool(value, "completeMarker")
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && !Bool(value, "currentPointerCreated")
                && !Bool(value, "databasePersistenceCompleted")
                && !Bool(value, "runtimeAuthorized")
                && !Bool(value, "traversalReady")
                && !Bool(value, "gameplayReady")
                && Text(value, "contentHashSha256") == CompleteContentHash
                && ComputeContentHash(value) == CompleteContentHash,
            "CompleteIdentityChanged");
        var expected = new Dictionary<string, (string Hash, long Length, int Records)>(StringComparer.Ordinal)
        {
            ["manifest.json"] = (ManifestHash, 9_434, 1),
            ["candidates.ndjson"] = (CandidatesHash, 98_781_674, ExpectedCandidateRows),
            ["audit.json"] = (AuditHash, 6_486, 1),
            ["generator-source.py"] = (GeneratorSourceHash, 64_644, 1)
        };
        var files = value.GetProperty("files").EnumerateArray().ToArray();
        Require(files.Length == expected.Count, "CompleteFileCountChanged");
        foreach (var item in files)
        {
            RequireExactProperties(item, ["relativePath", "sha256", "byteLength", "recordCount"],
                "CompleteFilePropertySetChanged");
            var relative = Text(item, "relativePath");
            Require(expected.TryGetValue(relative, out var contract)
                    && Text(item, "sha256") == contract.Hash
                    && Long(item, "byteLength") == contract.Length
                    && Int(item, "recordCount") == contract.Records,
                "CompleteFileContractChanged");
        }
    }

    private static void ValidateHashContract(JsonElement value)
    {
        Require(Text(value, "algorithm") == "SHA-256"
                && Text(value, "encoding") == "UTF-8"
                && Text(value, "framing") == "UnsignedBigEndianUInt32ByteLengthThenUtf8",
            "CandidateHashAlgorithmChanged");
        var ordering = value.GetProperty("candidateOrdering").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        var headers = value.GetProperty("headerFields").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        var fields = value.GetProperty("candidateFields").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        Require(ordering.SequenceEqual(
                    ["administrativeAreaStableId", "candidateKindCode", "candidateStableId"],
                    StringComparer.Ordinal)
                && headers.SequenceEqual(HashHeaderFields, StringComparer.Ordinal)
                && fields.SequenceEqual(HashCandidateFields, StringComparer.Ordinal),
            "CandidateHashFieldOrderChanged");
    }

    private static IReadOnlyDictionary<string, AreaCounts> ReadPerArea(JsonElement value)
    {
        var result = new Dictionary<string, AreaCounts>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            Require(Regex.IsMatch(property.Name, "^[0-9]{10}$", RegexOptions.CultureInvariant),
                "AdministrativeAreaCodeInvalid");
            var counts = property.Value;
            RequireExactProperties(counts,
                ["nodeCandidates", "linkFragmentCandidates", "candidateRows"],
                "PerAreaPropertySetChanged");
            var item = new AreaCounts(Int(counts, "nodeCandidates"),
                Int(counts, "linkFragmentCandidates"), Int(counts, "candidateRows"));
            Require(item.CandidateRows == item.NodeCandidates + item.LinkFragmentCandidates,
                "PerAreaCandidateSumChanged");
            Require(result.TryAdd("region:kr:hjd:" + property.Name, item),
                "PerAreaAdministrativeAreaDuplicate");
        }
        return result;
    }

    private static void ValidatePerAreaTotals(IReadOnlyDictionary<string, AreaCounts> perArea)
    {
        Require(perArea.Count == ExpectedAreaCount
                && perArea.Keys.All(AreaStableIdPattern.IsMatch)
                && perArea.Values.All(item => item.CandidateRows > 0)
                && perArea.Values.Sum(item => item.NodeCandidates) == ExpectedNodeCandidates
                && perArea.Values.Sum(item => item.LinkFragmentCandidates) == ExpectedLinkFragments
                && perArea.Values.Sum(item => item.CandidateRows) == ExpectedCandidateRows,
            "PerAreaTotalsChanged");
    }

    private static void ValidateExpectedCounts(JsonElement value)
    {
        Require(Int(value, "sourceRows") == ExpectedSourceRows
                && Int(value, "sourceNodeRows") == ExpectedNodeSourceRows
                && Int(value, "sourceLinkRows") == ExpectedLinkSourceRows
                && Int(value, "outsideScopeRows") == ExpectedOutsideRows
                && Int(value, "candidateRows") == ExpectedCandidateRows
                && Int(value, "candidateNodes") == ExpectedNodeCandidates
                && Int(value, "candidateLinkFragments") == ExpectedLinkFragments
                && Int(value, "administrativeAreasWithCandidates") == ExpectedAreaCount
                && Int(value, "sourceDistrictConflictCandidates") == ExpectedDistrictConflicts
                && Int(value, "sourceLinksSpanningAdministrativeAreas") == ExpectedSpanningLinks
                && Int(value, "physicalFragmentGroups") == ExpectedLinkFragments
                && Int(value, "boundaryCoincidentSharedPhysicalFragmentGroups") == 0
                && Int(value, "boundaryCoincidentSharedCandidateAssignments") == 0
                && Int(value, "duplicateNodeIdDifferentBodyGroups") == 0
                && Int(value, "duplicateLinkIdDifferentBodyGroups") == 0
                && Int(value, "exactDuplicateSourceRowOccurrenceGroups") == 0
                && Int(value, "exactDuplicateSourceRowExtraOccurrences") == 0,
            "ExpectedCountsChanged");
    }

    private static CandidateScan ScanCandidates(
        string path,
        IReadOnlyList<string> headerValues,
        IReadOnlyDictionary<string, AreaCounts> expectedPerArea,
        bool collectCandidates)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in headerValues) AppendFramed(hash, value);
        var candidates = collectCandidates
            ? new List<Candidate>(ExpectedCandidateRows)
            : new List<Candidate>(0);
        var mutableCounts = expectedPerArea.Keys.ToDictionary(
            item => item,
            _ => new int[2],
            StringComparer.Ordinal);
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        var candidateIdentities = new HashSet<string>(StringComparer.Ordinal);
        var districtConflicts = 0;
        string? previousArea = null;
        string? previousKind = null;
        string? previousStableId = null;
        var rowCount = 0;

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            Require(line.Length is > 0 and <= 4_096, "CandidateLineLengthInvalid");
            using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 64 });
            var item = document.RootElement;
            Require(CanonicalJsonText(item) == line, "CandidateLineNotCanonical");
            var candidate = ValidateCandidate(item, line, expectedPerArea, hash);
            Require(stableIds.Add(candidate.CandidateStableId)
                    && candidateIdentities.Add(candidate.CandidateIdentitySha256),
                "CandidateIdentityDuplicate");
            if (previousArea is not null)
            {
                var ordered = CompareCandidateOrder(previousArea, previousKind!, previousStableId!,
                    candidate.AdministrativeAreaStableId, candidate.CandidateKindCode,
                    candidate.CandidateStableId);
                Require(ordered < 0, "CandidateOrderingChanged");
            }
            previousArea = candidate.AdministrativeAreaStableId;
            previousKind = candidate.CandidateKindCode;
            previousStableId = candidate.CandidateStableId;
            var counts = mutableCounts[candidate.AdministrativeAreaStableId];
            counts[candidate.CandidateKindCode == "Node" ? 0 : 1]++;
            if (candidate.QualityDiagnosticCodes.Contains(
                    "SourceDistrictSpatialAssignmentConflict", StringComparer.Ordinal))
                districtConflicts++;
            if (collectCandidates) candidates.Add(candidate);
            rowCount++;
        }

        Require(rowCount == ExpectedCandidateRows, "CandidateRowCountChanged");
        var actualPerArea = mutableCounts.ToDictionary(
            item => item.Key,
            item => new AreaCounts(item.Value[0], item.Value[1], item.Value[0] + item.Value[1]),
            StringComparer.Ordinal);
        Require(DictionaryEqual(actualPerArea, expectedPerArea), "CandidatePerAreaDistributionChanged");
        Require(districtConflicts == ExpectedDistrictConflicts, "CandidateDistrictConflictCountChanged");
        return new CandidateScan(Convert.ToHexString(hash.GetHashAndReset()), candidates,
            actualPerArea, districtConflicts);
    }

    private static Candidate ValidateCandidate(
        JsonElement item,
        string canonicalLine,
        IReadOnlyDictionary<string, AreaCounts> expectedPerArea,
        IncrementalHash candidateSetHash)
    {
        var kind = Text(item, "candidateKindCode");
        Require(kind is "Node" or "LinkFragment", "CandidateKindChanged");
        RequireExactProperties(item, kind == "Node" ? NodeCandidateProperties : LinkCandidateProperties,
            "CandidatePropertySetChanged");
        var stableId = Text(item, "candidateStableId");
        var area = Text(item, "administrativeAreaStableId");
        var assignment = Text(item, "assignmentStateCode");
        Require(Text(item, "schemaVersion") == CandidateSchemaVersion
                && Text(item, "revision") == Revision
                && CandidateStableIdPattern.IsMatch(stableId)
                && expectedPerArea.ContainsKey(area)
                && AreaStableIdPattern.IsMatch(area)
                && assignment == (kind == "Node"
                    ? "UniqueHistoricalBoundaryCover"
                    : "HistoricalBoundaryClippedFragment")
                && Text(item, "sourceKindCode") == (kind == "Node" ? "NODE" : "LINK")
                && Text(item, "sourceBoroughCode").Length == 10
                && Text(item, "sourceBoroughName").Length > 0
                && Text(item, "sourceLegalDongCode").Length == 10
                && Text(item, "sourceLegalDongName").Length > 0
                && item.GetProperty("sourceCsvDataRowNumber").TryGetInt32(out var csvRow)
                && csvRow > 0
                && item.GetProperty("sourceRonum").ValueKind == JsonValueKind.Null
                && OccurrencePattern.IsMatch(Text(item, "sourceOccurrenceKey")),
            "CandidateIdentityOrOccurrenceChanged");
        ValidateAuthority(item.GetProperty("authorityFlags"), "Candidate");

        foreach (var digestField in new[]
                 {
                     "sourceFeatureIdSha256", "sourceSemanticBodySha256", "sourceCompleteBodySha256",
                     "sourceFileHashSha256"
                 })
            Require(UpperDigest.IsMatch(Text(item, digestField)), "CandidateDigestInvalid:" + digestField);
        var allowedSourceHashes = new HashSet<string>(
        [
            "193806EE9ADE7E294A92E1BDAF9CA3312D2691CCE453C5E68DAE0F05E76F2D02",
            "2E30FDD65C327FCD3C132D35D2E9C6CE43CE94A21A47EE70321762F1A023D7B9",
            "08B904A586A7BFD7EF9FDAB4DA92BCEBC4B5D4B61FD41F09F33C2A35F5AFF773"
        ], StringComparer.Ordinal);
        Require(allowedSourceHashes.Contains(Text(item, "sourceFileHashSha256")),
            "CandidateSourceFileHashChanged");

        var diagnosticsElement = item.GetProperty("qualityDiagnosticCodes");
        var diagnostics = diagnosticsElement.EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : throw new InvalidDataException(
                    "AdministrativeDongWalkNetworkCandidate:CandidateDiagnosticTypeChanged"))
            .ToArray();
        Require(diagnostics.Length is >= 4 and <= 5
                && diagnostics.SequenceEqual(diagnostics.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                && diagnostics.Distinct(StringComparer.Ordinal).Count() == diagnostics.Length
                && diagnostics.Contains("Historical2020Source", StringComparer.Ordinal)
                && diagnostics.Contains("HistoricalBoundary2023VintageMismatch", StringComparer.Ordinal)
                && diagnostics.Contains("SourceDistrictFilterBoundaryHaloUnresolved", StringComparer.Ordinal)
                && diagnostics.Contains("SourceReportedActorCodeOnly", StringComparer.Ordinal),
            "CandidateDiagnosticsChanged");

        var geometry = kind == "Node"
            ? item.GetProperty("pointEpsg5186Millimeters")
            : item.GetProperty("fragmentGeometryEpsg5186Millimeters");
        ValidateGeometry(geometry, kind);
        var actors = kind == "Node" ? "[]" : CanonicalJsonText(item.GetProperty("sourceReportedActorCodes"));
        var beginNode = kind == "Node" ? string.Empty : Text(item, "sourceBeginNodeIdSha256");
        var endNode = kind == "Node" ? string.Empty : Text(item, "sourceEndNodeIdSha256");
        var reportedLength = kind == "Node"
            ? "null"
            : CanonicalJsonText(item.GetProperty("sourceReportedLengthMeters"));
        var fragmentOrdinal = kind == "Node"
            ? "null"
            : CanonicalJsonText(item.GetProperty("fragmentOrdinal"));
        var physicalFragmentHash = kind == "Node" ? string.Empty : Text(item, "physicalFragmentHashSha256");
        var sharedAreas = kind == "Node" ? "[]" : CanonicalJsonText(item.GetProperty("sharedAdministrativeAreaIds"));
        var sourceType = kind == "Node" ? Text(item, "sourceNodeTypeCode") : Text(item, "sourceLinkTypeCode");
        double? fragmentLength = null;
        if (kind == "LinkFragment")
        {
            var lengthElement = item.GetProperty("fragmentLengthMeters");
            Require(UpperDigest.IsMatch(beginNode) && UpperDigest.IsMatch(endNode)
                    && UpperDigest.IsMatch(physicalFragmentHash)
                    && item.GetProperty("fragmentOrdinal").TryGetInt32(out var ordinal) && ordinal > 0
                    && lengthElement.TryGetDouble(out _)
                    && lengthElement.GetDouble() > 0d
                    && !Bool(item, "boundaryCoincidentShared")
                    && item.GetProperty("sharedAdministrativeAreaIds").GetArrayLength() == 0,
                "LinkFragmentContractChanged");
            ValidateSourceFlags(item.GetProperty("sourceFlags"));
            fragmentLength = lengthElement.GetDouble();
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["candidateStableId"] = stableId,
            ["candidateKindCode"] = kind,
            ["administrativeAreaStableId"] = area,
            ["assignmentStateCode"] = assignment,
            ["sourceFeatureIdSha256"] = Text(item, "sourceFeatureIdSha256"),
            ["sourceSemanticBodySha256"] = Text(item, "sourceSemanticBodySha256"),
            ["sourceCompleteBodySha256"] = Text(item, "sourceCompleteBodySha256"),
            ["sourceFileHashSha256"] = Text(item, "sourceFileHashSha256"),
            ["sourceOccurrenceKey"] = Text(item, "sourceOccurrenceKey"),
            ["sourceBoroughCode"] = Text(item, "sourceBoroughCode"),
            ["sourceBoroughName"] = Text(item, "sourceBoroughName"),
            ["sourceLegalDongCode"] = Text(item, "sourceLegalDongCode"),
            ["sourceLegalDongName"] = Text(item, "sourceLegalDongName"),
            ["sourceTypeCode"] = sourceType,
            ["geometryCanonicalJson"] = CanonicalJsonText(geometry),
            ["sourceReportedActorCodesCanonicalJson"] = actors,
            ["sourceBeginNodeIdSha256"] = beginNode,
            ["sourceEndNodeIdSha256"] = endNode,
            ["sourceReportedLengthMetersCanonicalJson"] = reportedLength,
            ["fragmentOrdinalCanonicalJson"] = fragmentOrdinal,
            ["physicalFragmentHashSha256"] = physicalFragmentHash,
            ["sharedAdministrativeAreaIdsCanonicalJson"] = sharedAreas,
            ["qualityDiagnosticCodesCanonicalJson"] = CanonicalJsonText(diagnosticsElement),
            ["authorityFlagsCanonicalJson"] = CanonicalJsonText(item.GetProperty("authorityFlags"))
        };
        Require(values.Count == HashCandidateFields.Length, "CandidateHashValueCountChanged");
        foreach (var field in HashCandidateFields)
        {
            Require(values.TryGetValue(field, out var value), "CandidateHashFieldMissing:" + field);
            AppendFramed(candidateSetHash, value!);
        }

        return new Candidate(stableId, HashUtf8(stableId), kind, area, assignment,
            Text(item, "sourceFileHashSha256"), sourceType, HashUtf8(values["geometryCanonicalJson"]),
            fragmentLength, diagnostics, HashUtf8(canonicalLine));
    }

    private static void ValidateGeometry(JsonElement geometry, string kind)
    {
        Require(geometry.ValueKind == JsonValueKind.Array, "CandidateGeometryTypeChanged");
        if (kind == "Node")
        {
            var values = geometry.EnumerateArray().ToArray();
            Require(values.Length == 2 && values.All(item => item.TryGetInt64(out _)),
                "CandidateNodeGeometryChanged");
            return;
        }
        var points = geometry.EnumerateArray().ToArray();
        Require(points.Length >= 2 && points.All(point => point.ValueKind == JsonValueKind.Array
                && point.GetArrayLength() == 2
                && point.EnumerateArray().All(value => value.TryGetInt64(out _))),
            "CandidateLinkGeometryChanged");
    }

    private static void ValidateSourceFlags(JsonElement value)
    {
        RequireExactProperties(value,
            ["bridge", "buildingInterior", "crosswalk", "elevatedRoad", "overpass", "park", "subwayNetwork", "tunnel"],
            "CandidateSourceFlagsChanged");
        foreach (var property in value.EnumerateObject())
            Require(property.Value.ValueKind == JsonValueKind.String
                    && property.Value.GetString() is "0" or "1",
                "CandidateSourceFlagTypeChanged");
    }

    private static int CompareCandidateOrder(
        string leftArea,
        string leftKind,
        string leftStable,
        string rightArea,
        string rightKind,
        string rightStable)
    {
        var comparison = StringComparer.Ordinal.Compare(leftArea, rightArea);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(leftKind, rightKind);
        return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(leftStable, rightStable);
    }

    private static List<외부데이터정규화Record> BuildRows(Bundle bundle)
    {
        var rows = new List<외부데이터정규화Record>(ExpectedCandidateRows);
        foreach (var candidate in bundle.Candidates)
        {
            var dimensionKey = "walk-network-candidate|" +
                (candidate.CandidateKindCode == "Node" ? "node" : "link-fragment") +
                "|sha256|" + candidate.CandidateIdentitySha256;
            var payloadObject = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["administrativeAreaStableId"] = candidate.AdministrativeAreaStableId,
                ["assignmentStateCode"] = candidate.AssignmentStateCode,
                ["authorityFlags"] = AuthorityDictionary(),
                ["candidateBodyHashSha256"] = candidate.CandidateBodyHashSha256,
                ["candidateIdentitySha256"] = candidate.CandidateIdentitySha256,
                ["candidateKindCode"] = candidate.CandidateKindCode,
                ["candidateQualityCode"] = Quality,
                ["candidateSetHashSha256"] = CandidateSetHash,
                ["fragmentLengthMeters"] = candidate.FragmentLengthMeters,
                ["geometryBodyHashSha256"] = candidate.GeometryBodyHashSha256,
                ["ledgerPersistenceCode"] = LedgerRecordCode,
                ["qualityDiagnosticCodes"] = candidate.QualityDiagnosticCodes,
                ["schemaVersion"] = ProtectedPayloadSchemaVersion,
                ["sourceFileHashSha256"] = candidate.SourceFileHashSha256,
                ["sourceTypeCode"] = candidate.SourceTypeCode
            };
            var payload = CanonicalJsonText(payloadObject);
            Require(Encoding.UTF8.GetByteCount(payload) <= 2_000
                    && payload.Length <= 2_000
                    && dimensionKey.Length <= 500
                    && Limitation.Length <= 500,
                "ProtectedPayloadFieldBudgetExceeded");
            ValidatePayloadPrivacy(payload);
            rows.Add(new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId,
                    candidate.AdministrativeAreaStableId, MetricCode, EvidenceAsOfUtc, dimensionKey),
                StableId = "administrative-dong-walk-network-candidate:sha256:" +
                    candidate.CandidateIdentitySha256.ToLowerInvariant(),
                SourceId = SourceId,
                DatasetId = DatasetId,
                RegionStableId = candidate.AdministrativeAreaStableId,
                MetricCode = MetricCode,
                NumericValue = candidate.FragmentLengthMeters is null
                    ? null
                    : Convert.ToDecimal(candidate.FragmentLengthMeters.Value, CultureInfo.InvariantCulture),
                TextValue = payload,
                UnitCode = candidate.FragmentLengthMeters is null
                    ? "walk-network-node-candidate-json"
                    : "walk-network-link-fragment-candidate-json",
                EvidenceAsOfUtc = EvidenceAsOfUtc,
                CollectedAtUtc = CollectedAtUtc,
                SpatialPrecisionCode = "oa21208-wgs84-clipped-to-2023-historical-hjd-candidate",
                TemporalPrecisionCode = "year-only-2020-anchor-not-exact-observation-date",
                QualityCode = Quality,
                LimitationCode = Limitation,
                DimensionKey = dimensionKey,
                SourceVersion = "candidate-body-sha256:" + candidate.CandidateBodyHashSha256.ToLowerInvariant(),
                DataRevision = Revision,
                FirstSeenAtUtc = CollectedAtUtc,
                LastSeenAtUtc = CollectedAtUtc
            });
        }
        Require(rows.Count == ExpectedCandidateRows
                && rows.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == rows.Count
                && rows.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == rows.Count
                && rows.Select(item => item.DimensionKey).Distinct(StringComparer.Ordinal).Count() == rows.Count
                && rows.Count(item => item.NumericValue is null) == ExpectedNodeCandidates
                && rows.Count(item => item.NumericValue is not null) == ExpectedLinkFragments,
            "NormalizedExactSetInvalid");
        return rows;
    }

    private static IReadOnlyList<RegistrationInput> BuildRegistrationInputs(
        string root,
        IReadOnlyDictionary<string, FrozenSource> sources,
        string designPath,
        string workOrderPath,
        string scopePath,
        string generatorPath,
        string manifestPath,
        string candidatesPath,
        string auditPath,
        string generatorSourcePath,
        string completePath)
    {
        var inputs = new List<RegistrationInput>(ExpectedSnapshots);
        foreach (var source in sources.Values.OrderBy(item => item.Role, StringComparer.Ordinal))
        {
            inputs.Add(NewRegistration(
                DatasetId + "-source-" + source.Role.ToLowerInvariant(),
                source.Path,
                source.RelativePath,
                source.Hash,
                "sha256:" + source.Hash.ToLowerInvariant(),
                canonicalSource: false));
        }
        inputs.Add(NewRegistration(DatasetId + "-design-input", designPath, DesignRelative,
            DesignHash, "sha256:" + DesignHash.ToLowerInvariant(), false));
        inputs.Add(NewRegistration(DatasetId + "-work-order-input", workOrderPath, WorkOrderRelative,
            WorkOrderHash, "sha256:" + WorkOrderHash.ToLowerInvariant(), false));
        inputs.Add(NewRegistration(DatasetId + "-scope-input", scopePath, ScopeRelative,
            ScopeDefinitionHash, "sha256:" + ScopeDefinitionHash.ToLowerInvariant(), false));
        inputs.Add(NewRegistration(DatasetId + "-generator-input", generatorPath, GeneratorRelative,
            GeneratorHash, "sha256:" + GeneratorHash.ToLowerInvariant(), false));

        foreach (var artifact in new[]
                 {
                     (Suffix: "manifest", Path: manifestPath, Hash: ManifestHash),
                     (Suffix: "candidates", Path: candidatesPath, Hash: CandidatesHash),
                     (Suffix: "audit", Path: auditPath, Hash: AuditHash),
                     (Suffix: "generator-source", Path: generatorSourcePath, Hash: GeneratorSourceHash),
                     (Suffix: "complete", Path: completePath, Hash: CompleteHash)
                 })
        {
            inputs.Add(NewRegistration(DatasetId + "-artifact-" + artifact.Suffix,
                artifact.Path,
                GenerationRelative + "/" + Path.GetFileName(artifact.Path),
                artifact.Hash,
                Revision + ";candidate-set:" + CandidateSetHash.ToLowerInvariant(),
                artifact.Suffix == "candidates"));
        }
        Require(inputs.Count == ExpectedSnapshots
                && inputs.Count(item => item.CanonicalSource) == 1
                && inputs.Single(item => item.CanonicalSource).DatasetId == CandidateSnapshotDatasetId
                && inputs.Select(item => item.DatasetId).Distinct(StringComparer.Ordinal).Count() == inputs.Count
                && inputs.All(item => item.DatasetId.Length <= 160
                    && item.SourceVersion.Length <= 200
                    && item.RelativePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false),
            "RegistrationInputSetInvalid");
        return inputs;
    }

    private static RegistrationInput NewRegistration(
        string datasetId,
        string path,
        string relativePath,
        string hash,
        string sourceVersion,
        bool canonicalSource)
        => new(datasetId, path, relativePath, hash, new FileInfo(path).Length,
            sourceVersion, ContentType(path), canonicalSource);

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows,
        PreservationState preservationBefore,
        Dictionary<string, object?> result)
    {
        await using (var preflight = new PublicDataIngestionDbContext(options))
        {
            await ValidateSnapshotPreflightAsync(preflight, bundle);
            var existing = await LoadScopedRowsAsync(preflight);
            await ValidateExistingAsync(preflight, bundle, existing, rows, requireComplete: false);
            result["beforeCount"] = existing.Count;
            if (existing.Count == rows.Count && await AreSnapshotsCompleteAsync(preflight, bundle))
            {
                SetNoWriteResult(result, existing.Count);
                return;
            }
        }

        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        var acquiredLocks = new List<string>();
        try
        {
            foreach (var lockName in PreservationLockNames.Order(StringComparer.Ordinal))
            {
                Require(await AcquireLockAsync(db, lockName), "ImportOrProtectedLedgerBusy");
                acquiredLocks.Add(lockName);
            }

            await using var transaction = await db.Database.BeginTransactionAsync();
            var lockedPreservation = await ReadPreservationStateAsync(db);
            Require(lockedPreservation == preservationBefore, "ProtectedLedgerChangedBeforeApply");
            await ValidateSnapshotPreflightAsync(db, bundle);
            var lockedExisting = await LoadScopedRowsAsync(db);
            await ValidateExistingAsync(db, bundle, lockedExisting, rows, requireComplete: false);
            if (lockedExisting.Count == rows.Count && await AreSnapshotsCompleteAsync(db, bundle))
            {
                SetNoWriteResult(result, lockedExisting.Count);
                return;
            }

            result["databaseWriteAttempted"] = true;
            var registrations = new Dictionary<string, (long SnapshotId, bool Inserted)>(StringComparer.Ordinal);
            foreach (var input in bundle.Registrations)
            {
                var stored = await db.RawSnapshots.SingleOrDefaultAsync(item =>
                    item.SourceId == SourceId && item.DatasetId == input.DatasetId);
                if (stored is not null)
                {
                    await RequireSnapshotAsync(db, input);
                    registrations.Add(input.DatasetId, (stored.Id, false));
                    continue;
                }
                Require(!await db.IngestionRuns.AnyAsync(item => item.RunKey == ExpectedRunKey(input)),
                    "SourceRegistrationRunConflict");
                var registration = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(
                    input.Path,
                    new 공공공간원본등록Request(SourceId, input.DatasetId, input.SourceVersion, Revision,
                        EvidenceAsOfUtc, input.ContentType, input.RelativePath));
                Require(registration.Inserted
                        && registration.SourceHashSha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase)
                        && registration.ContentLength == input.Length,
                    "SourceRegistrationChanged");
                await NormalizeRegisteredSourceStateAsync(db, input, registration.RawSnapshotId);
                registrations.Add(input.DatasetId, (registration.RawSnapshotId, true));
            }
            foreach (var input in bundle.Registrations) await RequireSnapshotAsync(db, input);

            var canonicalSnapshot = registrations[CandidateSnapshotDatasetId].SnapshotId;
            foreach (var row in rows) row.RawSnapshotId = canonicalSnapshot;
            var inserted = 0;
            var existingCount = 0;
            foreach (var batch in rows.Chunk(500))
            {
                Require(batch.Length is > 0 and <= 500, "NormalizedChunkSizeExceeded");
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch);
                Require(saved.UpdatedCount == 0, "UnexpectedNormalizedUpdate");
                inserted += saved.InsertedCount;
                existingCount += saved.ExistingCount;
                db.ChangeTracker.Clear();
            }
            Require(inserted + existingCount == rows.Count, "NormalizedSaveCountMismatch");
            // DB coercion이나 저장소 구현 이상도 commit 뒤가 아니라 같은 transaction 안에서
            // 전체 행·본문·17개 계보 사본·canonical 결속을 다시 읽어 즉시 rollback한다.
            var transactionReadback = await LoadScopedRowsAsync(db);
            await ValidateExistingAsync(db, bundle, transactionReadback, rows, requireComplete: true);
            Require(await ReadPreservationStateAsync(db) == preservationBefore,
                "ProtectedLedgerChangedDuringApply");
            await transaction.CommitAsync();
            result["committed"] = true;
            result["inserted"] = inserted;
            result["existing"] = existingCount;
            result["updated"] = 0;
            result["rawSnapshotInserted"] = registrations.Values.Count(item => item.Inserted);
            result["rawSnapshotCount"] = registrations.Count;
        }
        finally
        {
            for (var index = acquiredLocks.Count - 1; index >= 0; index--)
                await ReleaseLockAsync(db, acquiredLocks[index]);
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
        result["rawSnapshotCount"] = ExpectedSnapshots;
    }

    private static async Task NormalizeRegisteredSourceStateAsync(
        PublicDataIngestionDbContext db,
        RegistrationInput input,
        long rawSnapshotId)
    {
        var snapshot = await db.RawSnapshots.SingleAsync(item => item.Id == rawSnapshotId);
        var run = await db.IngestionRuns.SingleAsync(item => item.Id == snapshot.FirstCollectionRunId);
        snapshot.SourceVersion = input.SourceVersion;
        snapshot.CollectedAtUtc = CollectedAtUtc;
        snapshot.EvidenceAsOfUtc = EvidenceAsOfUtc;
        snapshot.FirstSeenAtUtc = CollectedAtUtc;
        snapshot.LastSeenAtUtc = CollectedAtUtc;
        run.RunKey = ExpectedRunKey(input);
        run.SourceId = SourceId;
        run.DatasetId = input.DatasetId;
        run.StatusCode = 외부데이터수집StatusCodes.Partial;
        run.StartedAtUtc = CollectedAtUtc;
        run.CompletedAtUtc = CollectedAtUtc;
        run.AttemptCount = 1;
        run.FetchedCount = input.CanonicalSource ? ExpectedCandidateRows : 1;
        run.NormalizedCount = input.CanonicalSource ? ExpectedCandidateRows : 0;
        run.RejectedCount = 0;
        run.InsertedCount = input.CanonicalSource ? ExpectedCandidateRows : 1;
        run.UpdatedCount = 0;
        run.ExistingCount = 0;
        run.SourceVersion = input.SourceVersion;
        run.DataRevision = Revision;
        run.ErrorCode = Quality;
        run.ErrorSummary = ExpectedRunErrorSummary(input);
        Require(run.RunKey.Length <= 80 && run.ErrorSummary.Length <= 500,
            "SourceRunFieldBudgetExceeded");
        await db.SaveChangesAsync();
    }

    private static async Task ValidateSnapshotPreflightAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle)
    {
        var expectedDatasetIds = bundle.Registrations.Select(item => item.DatasetId)
            .ToHashSet(StringComparer.Ordinal);
        var scopedSnapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            item.SourceId == SourceId && item.DatasetId.StartsWith(DatasetId + "-"))
            .ToListAsync();
        Require(scopedSnapshots.All(item => expectedDatasetIds.Contains(item.DatasetId)),
            "UnexpectedRevisionSnapshot");
        foreach (var input in bundle.Registrations)
        {
            var snapshots = scopedSnapshots.Where(item => item.DatasetId == input.DatasetId).ToArray();
            Require(snapshots.Length <= 1, "SourceSnapshotPreflightDuplicate");
            if (snapshots.Length == 1)
                await RequireSnapshotAsync(db, input);
            else
                Require(!await db.IngestionRuns.AsNoTracking().AnyAsync(item =>
                        item.RunKey == ExpectedRunKey(input)),
                    "SourceRunWithoutSnapshot");
        }
    }

    private static async Task<bool> AreSnapshotsCompleteAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle)
    {
        // MySQL 제공자마다 로컬 배열 Contains 번역이 다르므로 revision prefix만 SQL로
        // 제한하고, 정확한 17개 ID 집합/중복 검사는 메모리에서 결정적으로 수행한다.
        var expected = bundle.Registrations.Select(item => item.DatasetId)
            .ToHashSet(StringComparer.Ordinal);
        var snapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            item.SourceId == SourceId && item.DatasetId.StartsWith(DatasetId + "-"))
            .Select(item => item.DatasetId).ToListAsync();
        return snapshots.Count == ExpectedSnapshots
               && snapshots.Distinct(StringComparer.Ordinal).Count() == ExpectedSnapshots
               && expected.SetEquals(snapshots);
    }

    private static async Task RequireSnapshotAsync(PublicDataIngestionDbContext db, RegistrationInput input)
    {
        var snapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            item.SourceId == SourceId && item.DatasetId == input.DatasetId).ToListAsync();
        Require(snapshots.Count == 1 && SnapshotMatches(snapshots[0], input),
            "SourceSnapshotMissingOrChanged");
        var run = await db.IngestionRuns.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == snapshots[0].FirstCollectionRunId);
        Require(run is not null && SourceRunMatches(run, input), "SourceRunMissingOrChanged");
    }

    private static bool SnapshotMatches(외부데이터RawSnapshot value, RegistrationInput input)
    {
        var relative = input.RelativePath.Replace('\\', '/');
        return value.FirstCollectionRunId > 0
               && value.SourceId == SourceId
               && value.DatasetId == input.DatasetId
               && value.SourceVersion == input.SourceVersion
               && value.CollectedAtUtc == CollectedAtUtc
               && value.EvidenceAsOfUtc == EvidenceAsOfUtc
               && value.ContentHashSha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase)
               && value.ContentLength == input.Length
               && value.ContentType == input.ContentType
               && value.OriginalFileName == Path.GetFileName(input.Path)
               && value.StorageContainer == "local-private-public-spatial"
               && value.StorageObjectName == relative
               && value.StorageLocation == "private-file://" + relative
               && value.FirstSeenAtUtc == CollectedAtUtc
               && value.LastSeenAtUtc == CollectedAtUtc;
    }

    private static bool SourceRunMatches(외부데이터수집Run value, RegistrationInput input)
        => value.RunKey == ExpectedRunKey(input)
           && value.SourceId == SourceId
           && value.DatasetId == input.DatasetId
           && value.StatusCode == 외부데이터수집StatusCodes.Partial
           && value.StartedAtUtc == CollectedAtUtc
           && value.CompletedAtUtc == CollectedAtUtc
           && value.AttemptCount == 1
           && value.FetchedCount == (input.CanonicalSource ? ExpectedCandidateRows : 1)
           && value.NormalizedCount == (input.CanonicalSource ? ExpectedCandidateRows : 0)
           && value.RejectedCount == 0
           && value.InsertedCount == (input.CanonicalSource ? ExpectedCandidateRows : 1)
           && value.UpdatedCount == 0
           && value.ExistingCount == 0
           && value.SourceVersion == input.SourceVersion
           && value.DataRevision == Revision
           && value.ErrorCode == Quality
           && value.ErrorSummary == ExpectedRunErrorSummary(input);

    private static string ExpectedRunErrorSummary(RegistrationInput input)
        => input.CanonicalSource
            ? "Canonical private G3c candidate snapshot derived from frozen OA-21208 and a 2023 historical boundary; no current passability, distribution, runtime, traversal, gameplay or Unity authority."
            : "Frozen private G3c source, design, tool or generation evidence; no current passability, distribution, runtime, traversal, gameplay or Unity authority.";

    private static string ExpectedRunKey(RegistrationInput input)
    {
        var digest = HashUtf8(input.DatasetId).ToLowerInvariant();
        return "public-data-candidate:g3c-r1:" + digest[..24];
    }

    private static async Task<bool> AcquireLockAsync(PublicDataIngestionDbContext db, string name)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@name,0)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task ReleaseLockAsync(PublicDataIngestionDbContext db, string name)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT RELEASE_LOCK(@name)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        _ = await command.ExecuteScalarAsync();
    }

    private static async Task<PreservationState> ReadPreservationStateAsync(
        DbContextOptions<PublicDataIngestionDbContext> options)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        return await ReadPreservationStateAsync(db);
    }

    private static async Task<PreservationState> ReadPreservationStateAsync(
        PublicDataIngestionDbContext db)
    {
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var normalizedRows = 0;
        var snapshots = 0;
        var runs = 0;
        foreach (var ledger in ProtectedLedgers)
        {
            var state = await ReadProtectedLedgerAsync(db, ledger);
            normalizedRows += state.Rows;
            snapshots += state.Snapshots;
            runs += state.Runs;
            hashes.Add(ledger.Code, state.Hash);
        }
        var legacy = await ReadLegacyMyeonmokLedgerAsync(db);
        normalizedRows += legacy.Rows;
        snapshots += legacy.Snapshots;
        runs += legacy.Runs;
        hashes.Add("legacy-myeonmok-business", legacy.Hash);
        using var combined = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendPersistedFramed(combined, "g3c-protected-ledger-set.v1");
        foreach (var item in hashes)
        {
            AppendPersistedFramed(combined, item.Key);
            AppendPersistedFramed(combined, item.Value);
        }
        return new PreservationState(hashes.Count, normalizedRows, snapshots, runs,
            Convert.ToHexString(combined.GetHashAndReset()));
    }

    private static async Task<(int Rows, int Snapshots, int Runs, string Hash)> ReadProtectedLedgerAsync(
        PublicDataIngestionDbContext db,
        ProtectedLedger ledger)
    {
        var rows = await db.NormalizedRecords.AsNoTracking().Where(item =>
            item.SourceId == ledger.SourceId && item.DatasetId == ledger.DatasetId).ToListAsync();
        Require(rows.Count == ledger.ExpectedRows
                && rows.All(item => item.DataRevision == ledger.Revision),
            "ProtectedLedgerRowsChanged:" + ledger.Code);
        var rawSnapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            item.SourceId == ledger.SourceId
            && (item.DatasetId == ledger.DatasetId
                || item.DatasetId.StartsWith(ledger.DatasetId + "-"))).ToListAsync();
        Require(rawSnapshots.Count == ledger.ExpectedSnapshots,
            "ProtectedLedgerSnapshotsChanged:" + ledger.Code);
        var runIds = rawSnapshots.Select(item => item.FirstCollectionRunId).Distinct().ToList();
        var ingestionRuns = await db.IngestionRuns.AsNoTracking().Where(item =>
            runIds.Contains(item.Id)).ToListAsync();
        Require(ingestionRuns.Count == ledger.ExpectedSnapshots
                && ingestionRuns.Count == runIds.Count,
            "ProtectedLedgerRunsChanged:" + ledger.Code);
        return (rows.Count, rawSnapshots.Count, ingestionRuns.Count,
            ComputePersistedStateHash("protected-ledger:" + ledger.Code,
                rows, rawSnapshots, ingestionRuns));
    }

    private static async Task<(int Rows, int Snapshots, int Runs, string Hash)>
        ReadLegacyMyeonmokLedgerAsync(PublicDataIngestionDbContext db)
    {
        const string revision = "myeonmok-business-20260908.r1";
        const string shopSource = "semas-commercial-listing";
        const string shopDataset = "data-go-kr-15083033";
        const string factorySource = "seoul-jungnang-open-data";
        const string factoryDataset = "jungnang-registered-factory-15034963";
        var rows = await db.NormalizedRecords.AsNoTracking().Where(item =>
            item.DataRevision == revision
            && ((item.SourceId == shopSource && item.DatasetId == shopDataset)
                || (item.SourceId == factorySource && item.DatasetId == factoryDataset))).ToListAsync();
        Require(rows.Count(item => item.SourceId == shopSource && item.DatasetId == shopDataset) == 5_411
                && rows.Count(item => item.SourceId == factorySource && item.DatasetId == factoryDataset) == 126,
            "LegacyMyeonmokRowsChanged");
        var rawSnapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            (item.SourceId == shopSource && item.DatasetId == shopDataset)
            || (item.SourceId == factorySource && item.DatasetId == factoryDataset)).ToListAsync();
        var referencedRawIds = rows.Select(item => item.RawSnapshotId).Distinct().ToHashSet();
        Require(rawSnapshots.Count > 0
                && rawSnapshots.Select(item => item.Id).ToHashSet().SetEquals(referencedRawIds),
            "LegacyMyeonmokSnapshotsChanged");
        var runIds = rawSnapshots.Select(item => item.FirstCollectionRunId).Distinct().ToList();
        var ingestionRuns = await db.IngestionRuns.AsNoTracking().Where(item =>
            runIds.Contains(item.Id)).ToListAsync();
        Require(ingestionRuns.Count == runIds.Count, "LegacyMyeonmokRunsChanged");
        return (rows.Count, rawSnapshots.Count, ingestionRuns.Count,
            ComputePersistedStateHash("protected-ledger:legacy-myeonmok-business",
                rows, rawSnapshots, ingestionRuns));
    }

    private static string ComputePersistedStateHash(
        string domain,
        IEnumerable<외부데이터정규화Record> rows,
        IEnumerable<외부데이터RawSnapshot> snapshots,
        IEnumerable<외부데이터수집Run> runs)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendPersistedFramed(hash, domain);
        foreach (var item in rows.OrderBy(value => value.SourceId, StringComparer.Ordinal)
                     .ThenBy(value => value.DatasetId, StringComparer.Ordinal)
                     .ThenBy(value => value.RecordKey, StringComparer.Ordinal).ThenBy(value => value.Id))
        {
            AppendPersistedFramed(hash, item.Id); AppendPersistedFramed(hash, item.RawSnapshotId);
            AppendPersistedFramed(hash, item.RecordKey); AppendPersistedFramed(hash, item.StableId);
            AppendPersistedFramed(hash, item.SourceId); AppendPersistedFramed(hash, item.DatasetId);
            AppendPersistedFramed(hash, item.RegionStableId); AppendPersistedFramed(hash, item.MetricCode);
            AppendPersistedFramed(hash, item.NumericValue?.ToString(CultureInfo.InvariantCulture));
            AppendPersistedFramed(hash, item.TextValue); AppendPersistedFramed(hash, item.UnitCode);
            AppendPersistedFramed(hash, Time(item.EvidenceAsOfUtc));
            AppendPersistedFramed(hash, Time(item.CollectedAtUtc));
            AppendPersistedFramed(hash, item.SpatialPrecisionCode);
            AppendPersistedFramed(hash, item.TemporalPrecisionCode);
            AppendPersistedFramed(hash, item.QualityCode); AppendPersistedFramed(hash, item.LimitationCode);
            AppendPersistedFramed(hash, item.DimensionKey); AppendPersistedFramed(hash, item.SourceVersion);
            AppendPersistedFramed(hash, item.DataRevision); AppendPersistedFramed(hash, Time(item.FirstSeenAtUtc));
            AppendPersistedFramed(hash, Time(item.LastSeenAtUtc));
        }
        foreach (var item in snapshots.OrderBy(value => value.SourceId, StringComparer.Ordinal)
                     .ThenBy(value => value.DatasetId, StringComparer.Ordinal)
                     .ThenBy(value => value.ContentHashSha256, StringComparer.Ordinal).ThenBy(value => value.Id))
        {
            AppendPersistedFramed(hash, item.Id); AppendPersistedFramed(hash, item.FirstCollectionRunId);
            AppendPersistedFramed(hash, item.SourceId); AppendPersistedFramed(hash, item.DatasetId);
            AppendPersistedFramed(hash, item.SourceVersion); AppendPersistedFramed(hash, Time(item.CollectedAtUtc));
            AppendPersistedFramed(hash, item.EvidenceAsOfUtc is null ? null : Time(item.EvidenceAsOfUtc.Value));
            AppendPersistedFramed(hash, item.ContentHashSha256); AppendPersistedFramed(hash, item.ContentLength);
            AppendPersistedFramed(hash, item.ContentType); AppendPersistedFramed(hash, item.OriginalFileName);
            AppendPersistedFramed(hash, item.StorageContainer); AppendPersistedFramed(hash, item.StorageObjectName);
            AppendPersistedFramed(hash, item.StorageLocation); AppendPersistedFramed(hash, Time(item.FirstSeenAtUtc));
            AppendPersistedFramed(hash, Time(item.LastSeenAtUtc));
        }
        foreach (var item in runs.OrderBy(value => value.SourceId, StringComparer.Ordinal)
                     .ThenBy(value => value.DatasetId, StringComparer.Ordinal)
                     .ThenBy(value => value.RunKey, StringComparer.Ordinal).ThenBy(value => value.Id))
        {
            AppendPersistedFramed(hash, item.Id); AppendPersistedFramed(hash, item.RunKey);
            AppendPersistedFramed(hash, item.SourceId); AppendPersistedFramed(hash, item.DatasetId);
            AppendPersistedFramed(hash, item.StatusCode); AppendPersistedFramed(hash, Time(item.StartedAtUtc));
            AppendPersistedFramed(hash, item.CompletedAtUtc is null ? null : Time(item.CompletedAtUtc.Value));
            AppendPersistedFramed(hash, item.AttemptCount); AppendPersistedFramed(hash, item.FetchedCount);
            AppendPersistedFramed(hash, item.NormalizedCount); AppendPersistedFramed(hash, item.RejectedCount);
            AppendPersistedFramed(hash, item.InsertedCount); AppendPersistedFramed(hash, item.UpdatedCount);
            AppendPersistedFramed(hash, item.ExistingCount); AppendPersistedFramed(hash, item.SourceVersion);
            AppendPersistedFramed(hash, item.DataRevision); AppendPersistedFramed(hash, item.ErrorCode);
            AppendPersistedFramed(hash, item.ErrorSummary);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void SetPreservationSummary(
        Dictionary<string, object?> result,
        PreservationState state)
    {
        result["protectedLedgerCount"] = state.LedgerCount;
        result["protectedNormalizedRows"] = state.NormalizedRows;
        result["protectedRawSnapshots"] = state.RawSnapshots;
        result["protectedIngestionRuns"] = state.IngestionRuns;
        result["protectedStateHashSha256"] = state.StateHashSha256.ToLowerInvariant();
        result["protectedLedgersMutated"] = false;
    }

    private static Task<List<외부데이터정규화Record>> LoadScopedRowsAsync(
        PublicDataIngestionDbContext db)
        => db.NormalizedRecords.AsNoTracking().Where(item =>
            item.SourceId == SourceId && item.DatasetId == DatasetId).ToListAsync();

    private static async Task ValidateExistingAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle,
        IReadOnlyCollection<외부데이터정규화Record> existing,
        IReadOnlyCollection<외부데이터정규화Record> expected,
        bool requireComplete,
        Action<string>? reportStage = null)
    {
        reportStage?.Invoke("IndexExpectedRows");
        var expectedByKey = expected.ToDictionary(item => item.RecordKey, StringComparer.Ordinal);
        reportStage?.Invoke("CompareStoredRows");
        Require(existing.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == existing.Count,
            "StoredRecordKeyDuplicate");
        foreach (var stored in existing)
        {
            Require(expectedByKey.TryGetValue(stored.RecordKey, out var candidate)
                    && Equivalent(stored, candidate!, compareRawSnapshot: false),
                "StoredNormalizedRecordMismatch");
        }
        reportStage?.Invoke("LoadCanonicalSnapshot");
        var canonicalSnapshotIds = await db.RawSnapshots.AsNoTracking().Where(item =>
                item.SourceId == SourceId && item.DatasetId == CandidateSnapshotDatasetId)
            .Select(item => item.Id).ToListAsync();
        Require(canonicalSnapshotIds.Count <= 1, "StoredCanonicalSnapshotDuplicate");
        reportStage?.Invoke("ValidateCanonicalBindings");
        Require(ExactCanonicalBindings(existing, canonicalSnapshotIds),
            "StoredCanonicalSnapshotBindingChanged");
        if (!requireComplete) return;
        Require(existing.Count == expected.Count, "StoredNormalizedRecordCountChanged");
        Require(canonicalSnapshotIds.Count == 1, "StoredCanonicalSnapshotMissing");
        foreach (var input in bundle.Registrations)
        {
            reportStage?.Invoke("ValidateSnapshot:" + input.DatasetId[(DatasetId.Length + 1)..]);
            await RequireSnapshotAsync(db, input);
        }
        reportStage?.Invoke("CountSnapshotSet");
        Require(await AreSnapshotsCompleteAsync(db, bundle), "StoredSnapshotSetIncomplete");
    }

    private static bool ExactCanonicalBindings(
        IReadOnlyCollection<외부데이터정규화Record> rows,
        IReadOnlyCollection<long> canonicalSnapshotIds)
        => canonicalSnapshotIds.Count <= 1
           && (rows.Count == 0
               || canonicalSnapshotIds.Count == 1
               && rows.All(item => item.RawSnapshotId == canonicalSnapshotIds.Single()));

    private static bool Equivalent(
        외부데이터정규화Record stored,
        외부데이터정규화Record expected,
        bool compareRawSnapshot)
        => (!compareRawSnapshot || stored.RawSnapshotId == expected.RawSnapshotId)
           && stored.RecordKey == expected.RecordKey
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

    private static async Task VerifyReadbackAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows,
        Dictionary<string, object?> result)
    {
        // 적용 context와 분리된 새 연결/context에서 정확 본문과 snapshot 결속을 다시 읽는다.
        result["readbackStageCode"] = "OpenFreshContext";
        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await LoadScopedRowsAsync(verify);
        result["readbackStageCode"] = "ValidateStoredRowsAndSnapshots";
        await ValidateExistingAsync(verify, bundle, stored, rows, requireComplete: true,
            stage => result["readbackStageCode"] = stage);
        result["readbackStageCode"] = "IndexCandidateIdentities";
        var candidatesByIdentity = bundle.Candidates.ToDictionary(
            item => item.CandidateIdentitySha256, StringComparer.Ordinal);
        result["readbackStageCode"] = "ValidateStoredPayloads";
        foreach (var row in stored) ValidateStoredPayload(row, candidatesByIdentity);

        // manifest 계약을 다시 읽고 후보 파일을 다시 순회해 길이-prefix hash를 독립 재계산한다.
        result["readbackStageCode"] = "RecomputeCandidateHash";
        using var manifest = ReadJson(bundle.ManifestPath, 128_000);
        ValidateHashContract(manifest.RootElement.GetProperty("candidateHashContract"));
        var headerValues = manifest.RootElement.GetProperty("candidateHashHeaderValues")
            .EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var independent = ScanCandidates(bundle.CandidatesPath, headerValues,
            bundle.ExpectedPerArea, collectCandidates: false);
        Require(independent.CandidateSetHashSha256 == CandidateSetHash,
            "IndependentCandidateSetHashMismatch");

        result["verifiedRows"] = stored.Count;
        result["verifiedAdministrativeAreas"] = stored.Select(item => item.RegionStableId)
            .Distinct(StringComparer.Ordinal).Count();
        result["verifiedNodeCandidates"] = stored.Count(item => item.NumericValue is null);
        result["verifiedLinkFragmentCandidates"] = stored.Count(item => item.NumericValue is not null);
        result["verifiedDistrictConflictCandidates"] = independent.DistrictConflictCandidates;
        result["verifiedRawSnapshots"] = bundle.Registrations.Count;
        result["readbackCandidateSetHashSha256"] = independent.CandidateSetHashSha256.ToLowerInvariant();
        result["independentFreshContextReadback"] = true;
        result["independentCandidateSetHashRecalculation"] = true;
        result["readbackStageCode"] = "Complete";
        result["completionCode"] = CompletionUpperBound;
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
    }

    private static void ValidateStoredPayload(
        외부데이터정규화Record row,
        IReadOnlyDictionary<string, Candidate> candidatesByIdentity)
    {
        ValidatePayloadPrivacy(row.TextValue);
        using var document = JsonDocument.Parse(row.TextValue);
        var value = document.RootElement;
        Require(CanonicalJsonText(value) == row.TextValue, "StoredPayloadNotCanonical");
        RequireExactProperties(value,
        [
            "administrativeAreaStableId", "assignmentStateCode", "authorityFlags",
            "candidateBodyHashSha256", "candidateIdentitySha256", "candidateKindCode",
            "candidateQualityCode", "candidateSetHashSha256", "fragmentLengthMeters",
            "geometryBodyHashSha256", "ledgerPersistenceCode", "qualityDiagnosticCodes",
            "schemaVersion", "sourceFileHashSha256", "sourceTypeCode"
        ], "StoredPayloadPropertySetChanged");
        var identity = Text(value, "candidateIdentitySha256");
        Require(candidatesByIdentity.TryGetValue(identity, out var candidate),
            "StoredPayloadIdentityUnknown");
        if (candidate is null)
            throw new InvalidDataException("AdministrativeDongWalkNetworkCandidate:StoredPayloadIdentityUnknown");
        Require(Text(value, "schemaVersion") == ProtectedPayloadSchemaVersion
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "candidateBodyHashSha256") == candidate.CandidateBodyHashSha256
                && Text(value, "administrativeAreaStableId") == candidate.AdministrativeAreaStableId
                && Text(value, "candidateKindCode") == candidate.CandidateKindCode
                && Text(value, "assignmentStateCode") == candidate.AssignmentStateCode
                && Text(value, "candidateQualityCode") == Quality
                && Text(value, "sourceFileHashSha256") == candidate.SourceFileHashSha256
                && Text(value, "sourceTypeCode") == candidate.SourceTypeCode
                && Text(value, "geometryBodyHashSha256") == candidate.GeometryBodyHashSha256
                && Text(value, "ledgerPersistenceCode") == LedgerRecordCode,
            "StoredPayloadBodyChanged");
        if (candidate.FragmentLengthMeters is null)
            Require(value.GetProperty("fragmentLengthMeters").ValueKind == JsonValueKind.Null,
                "StoredNodeLengthChanged");
        else
            Require(Double(value, "fragmentLengthMeters") == candidate.FragmentLengthMeters.Value,
                "StoredFragmentLengthChanged");
        var diagnostics = value.GetProperty("qualityDiagnosticCodes").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        Require(diagnostics.SequenceEqual(candidate.QualityDiagnosticCodes, StringComparer.Ordinal),
            "StoredPayloadDiagnosticsChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "StoredPayload");
        Require(row.StableId == "administrative-dong-walk-network-candidate:sha256:" +
                    identity.ToLowerInvariant()
                && row.DimensionKey.EndsWith("|sha256|" + identity, StringComparison.Ordinal),
            "StoredHashedIdentityChanged");
    }

    private static void ValidatePayloadPrivacy(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var names = document.RootElement.EnumerateObject().Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var forbidden = new[]
        {
            "candidateStableId", "sourceOccurrenceKey", "sourceCsvDataRowNumber", "sourceRonum",
            "sourceBeginNodeId", "sourceEndNodeId", "sourceLegalDongCode", "sourceLegalDongName",
            "sourceBoroughName", "sourceWorkTimestamp", "pointEpsg5186Millimeters",
            "fragmentGeometryEpsg5186Millimeters", "wkt", "coordinates"
        };
        Require(!forbidden.Any(names.Contains), "RawIdWktOrCoordinateLeakedToProtectedPayload");
    }

    private static int SelfTest(
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows)
    {
        var tests = 0;
        void Check(bool condition, string code)
        {
            Require(condition, "SelfTest:" + code);
            tests++;
        }

        Check(bundle.Candidates.Count == ExpectedCandidateRows && rows.Count == ExpectedCandidateRows,
            "ExactCandidateCount");
        Check(bundle.ExpectedPerArea.Count == ExpectedAreaCount,
            "ExactAdministrativeAreaCount");
        Check(bundle.Candidates.Count(item => item.CandidateKindCode == "Node") == ExpectedNodeCandidates,
            "ExactNodeCount");
        Check(bundle.Candidates.Count(item => item.CandidateKindCode == "LinkFragment") == ExpectedLinkFragments,
            "ExactLinkFragmentCount");
        Check(bundle.Candidates.Count(item => item.QualityDiagnosticCodes.Contains(
            "SourceDistrictSpatialAssignmentConflict", StringComparer.Ordinal)) == ExpectedDistrictConflicts,
            "ExactDistrictConflictCount");
        Check(rows.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == rows.Count
              && rows.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == rows.Count
              && rows.Select(item => item.DimensionKey).Distinct(StringComparer.Ordinal).Count() == rows.Count,
            "ExactNormalizedIdentitySet");
        Check(rows.All(item => Encoding.UTF8.GetByteCount(item.TextValue) <= 2_000),
            "CanonicalPayloadTwoThousandByteBudget");
        Check(rows.All(item =>
        {
            ValidatePayloadPrivacy(item.TextValue);
            using var document = JsonDocument.Parse(item.TextValue);
            return CanonicalJsonText(document.RootElement) == item.TextValue;
        }), "NoRawIdWktOrCoordinatesAndCanonicalPayload");
        Check(bundle.Registrations.Count == ExpectedSnapshots
              && bundle.Registrations.Count(item => item.CanonicalSource) == 1,
            "ExactSnapshotContract");
        Check(PreservationLockNames.Length == 8
              && PreservationLockNames.Distinct(StringComparer.Ordinal).Count() == PreservationLockNames.Length
              && PreservationLockNames.Contains(LockName, StringComparer.Ordinal),
            "DeterministicProtectionLockSet");
        Check(rows.Chunk(500).Count() == 82
              && rows.Chunk(500).All(item => item.Length is > 0 and <= 500),
            "ChunkUpperBound");
        Check(FrameDigest(["ab", "c"]) != FrameDigest(["a", "bc"]),
            "LengthPrefixBoundary");
        var first = rows[0];
        var conflicting = CloneRow(first);
        conflicting.TextValue += " ";
        Check(!Equivalent(first, conflicting, compareRawSnapshot: false),
            "NormalizedMismatchDetected");
        var bindingProbe = CloneRow(first);
        bindingProbe.RawSnapshotId = 101;
        Check(ExactCanonicalBindings([bindingProbe], [101])
              && !ExactCanonicalBindings([bindingProbe], [102])
              && !ExactCanonicalBindings([bindingProbe], Array.Empty<long>()),
            "CanonicalSnapshotMismatchDetectedBeforeWrite");
        var independent = ScanCandidates(bundle.CandidatesPath, bundle.HeaderValues,
            bundle.ExpectedPerArea, collectCandidates: false);
        Check(independent.CandidateSetHashSha256 == CandidateSetHash,
            "IndependentLengthPrefixedCandidateHash");
        Check(independent.PerArea.Count == ExpectedAreaCount
              && DictionaryEqual(independent.PerArea, bundle.ExpectedPerArea),
            "IndependentExactThirtyDongDistribution");
        return tests;
    }

    private static 외부데이터정규화Record CloneRow(외부데이터정규화Record value)
        => new()
        {
            Id = value.Id,
            RawSnapshotId = value.RawSnapshotId,
            RecordKey = value.RecordKey,
            StableId = value.StableId,
            SourceId = value.SourceId,
            DatasetId = value.DatasetId,
            RegionStableId = value.RegionStableId,
            MetricCode = value.MetricCode,
            NumericValue = value.NumericValue,
            TextValue = value.TextValue,
            UnitCode = value.UnitCode,
            EvidenceAsOfUtc = value.EvidenceAsOfUtc,
            CollectedAtUtc = value.CollectedAtUtc,
            SpatialPrecisionCode = value.SpatialPrecisionCode,
            TemporalPrecisionCode = value.TemporalPrecisionCode,
            QualityCode = value.QualityCode,
            LimitationCode = value.LimitationCode,
            DimensionKey = value.DimensionKey,
            SourceVersion = value.SourceVersion,
            DataRevision = value.DataRevision,
            FirstSeenAtUtc = value.FirstSeenAtUtc,
            LastSeenAtUtc = value.LastSeenAtUtc
        };

    private static void SetSummary(
        Dictionary<string, object?> result,
        string mode,
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows)
    {
        result["mode"] = mode;
        result["revision"] = Revision;
        result["candidateSetHashSha256"] = CandidateSetHash.ToLowerInvariant();
        result["sourceRows"] = ExpectedSourceRows;
        result["candidateRows"] = ExpectedCandidateRows;
        result["nodeCandidates"] = ExpectedNodeCandidates;
        result["linkFragmentCandidates"] = ExpectedLinkFragments;
        result["administrativeAreas"] = ExpectedAreaCount;
        result["sourceDistrictConflicts"] = ExpectedDistrictConflicts;
        result["maximumProtectedPayloadBytes"] = rows.Max(item => Encoding.UTF8.GetByteCount(item.TextValue));
        result["lineageSnapshots"] = bundle.Registrations.Count;
        result["privateReviewOnly"] = true;
        result["historicalBoundaryBootstrapOnly"] = true;
        result["currentAdministrativeBoundaryEstablished"] = false;
        result["currentPassabilityEstablished"] = false;
        result["sidewalkWidthEstablished"] = false;
        result["curbEstablished"] = false;
        result["entranceBindingEstablished"] = false;
        result["motorcycleAccessEstablished"] = false;
        result["vehicleLaneEstablished"] = false;
        result["signalBindingEstablished"] = false;
        result["distributionApproved"] = false;
        result["publicDisplayAllowed"] = false;
        result["runtimeAuthorized"] = false;
        result["traversalReady"] = false;
        result["gameplayReady"] = false;
        result["unityApplyAllowed"] = false;
        result["mongoChanged"] = false;
        result["currentPointerChanged"] = false;
        result["unityChanged"] = false;
    }

    private static void ValidateAuthority(JsonElement value, string source)
    {
        var expected = TrueAuthorityFlags.Concat(FalseAuthorityFlags).ToArray();
        RequireExactProperties(value, expected, source + "AuthorityPropertySetChanged");
        foreach (var name in TrueAuthorityFlags)
            Require(Bool(value, name), source + "AuthorityTrueFlagChanged:" + name);
        foreach (var name in FalseAuthorityFlags)
            Require(!Bool(value, name), source + "AuthorityFalseFlagChanged:" + name);
    }

    private static IReadOnlyDictionary<string, bool> AuthorityDictionary()
    {
        var result = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (var name in TrueAuthorityFlags) result.Add(name, true);
        foreach (var name in FalseAuthorityFlags) result.Add(name, false);
        return result;
    }

    private static string FrameDigest(IEnumerable<string> values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in values) AppendFramed(hash, value);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendFramed(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static void AppendPersistedFramed(IncrementalHash hash, object? value)
    {
        Span<byte> header = stackalloc byte[5];
        if (value is null)
        {
            header.Clear();
            hash.AppendData(header);
            return;
        }
        var text = value switch
        {
            string item => item,
            bool item => item ? "true" : "false",
            int item => item.ToString(CultureInfo.InvariantCulture),
            long item => item.ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidDataException(
                "AdministrativeDongWalkNetworkCandidate:UnsupportedPersistedHashValue")
        };
        var bytes = Encoding.UTF8.GetBytes(text);
        header[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(header[1..], checked((uint)bytes.Length));
        hash.AppendData(header);
        hash.AppendData(bytes);
    }

    private static string ComputeContentHash(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false
        }))
            WriteCanonical(writer, value, skipRootContentHash: true);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string CanonicalJsonText(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false
        }))
            WriteCanonical(writer, value, skipRootContentHash: false);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CanonicalJsonText(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, CompactJson));
        return CanonicalJsonText(document.RootElement);
    }

    private static void WriteCanonical(
        Utf8JsonWriter writer,
        JsonElement value,
        bool skipRootContentHash,
        bool root = true)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (root && skipRootContentHash && property.Name == "contentHashSha256") continue;
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value, skipRootContentHash, root: false);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteCanonical(writer, item, skipRootContentHash, root: false);
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
                throw new InvalidDataException(
                    "AdministrativeDongWalkNetworkCandidate:JsonValueKindInvalid");
        }
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
        var path = Path.GetFullPath(Path.Combine(normalizedRoot,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        Require(path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase),
            "PathOutsideRepository");
        Require(!IsReparsePoint(normalizedRoot) && !IsReparsePoint(path),
            "RepositoryPathReparsePointRejected");
        var cursor = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(cursor))
        {
            Require(!IsReparsePoint(cursor), "RepositoryPathReparsePointRejected");
            if (string.Equals(cursor, normalizedRoot, StringComparison.OrdinalIgnoreCase)) break;
            cursor = Path.GetDirectoryName(cursor);
        }
        Require(string.Equals(cursor, normalizedRoot, StringComparison.OrdinalIgnoreCase),
            "RepositoryPathParentInvalid");
        return path;
    }

    private static void RequireFile(string path, string hash, long length, string code)
        => Require(File.Exists(path)
                   && !IsReparsePoint(path)
                   && new FileInfo(path).Length == length
                   && HashFile(path) == hash,
            code + "FileChanged");

    private static string ContentType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".py" => "text/x-python",
            ".ndjson" => "application/x-ndjson",
            ".csv" => "text/csv",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
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

    private static string HashUtf8(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Text(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? string.Empty
            : throw new InvalidDataException(
                "AdministrativeDongWalkNetworkCandidate:JsonTextContract:" + property);

    private static int Int(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException(
                "AdministrativeDongWalkNetworkCandidate:JsonIntegerContract:" + property);

    private static long Long(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt64(out var result)
            ? result
            : throw new InvalidDataException(
                "AdministrativeDongWalkNetworkCandidate:JsonLongContract:" + property);

    private static double Double(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetDouble(out var result)
            ? result
            : throw new InvalidDataException(
                "AdministrativeDongWalkNetworkCandidate:JsonDoubleContract:" + property);

    private static bool Bool(JsonElement value, string property)
        => value.TryGetProperty(property, out var item)
           && item.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? item.GetBoolean()
            : throw new InvalidDataException(
                "AdministrativeDongWalkNetworkCandidate:JsonBooleanContract:" + property);

    private static void RequireExactProperties(
        JsonElement value,
        IEnumerable<string> expected,
        string code)
    {
        var names = expected.ToArray();
        Require(value.ValueKind == JsonValueKind.Object
                && value.EnumerateObject().Count() == names.Length
                && value.EnumerateObject().Select(item => item.Name)
                    .ToHashSet(StringComparer.Ordinal).SetEquals(names),
            code);
    }

    private static bool DictionaryEqual<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> first,
        IReadOnlyDictionary<TKey, TValue> second)
        where TKey : notnull
        => first.Count == second.Count && first.All(item =>
            second.TryGetValue(item.Key, out var value)
            && EqualityComparer<TValue>.Default.Equals(item.Value, value));

    private static string Time(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void Require(bool condition, string code)
    {
        if (!condition)
            throw new InvalidDataException("AdministrativeDongWalkNetworkCandidate:" + code);
    }
}
