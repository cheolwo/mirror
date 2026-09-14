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

// OA-15534 교차로 점과 2023 역사 행정동 경계의 결속 후보만 로컬 비공개 검토 원장에 보존한다.
// 이 원장은 교차로 위상·진입 방향·제어기·차로·신호·현행 경계·Unity 권위가 아니다.
internal static class 행정동교차로점후보
{
    private const string SourceId = "seoul-open-data-admin-dong-intersection-private-review";
    private const string DatasetId = "northeast-seoul-admin-dong-intersection-point-candidate-g3b-r1";
    private const string OriginalInputDatasetId = DatasetId + "-oa15534-input";
    private const string MetricCode = "administrative-dong-intersection-point-candidate";
    private const string Revision = "northeast-seoul-admin-dong-intersection-point-candidate.g3b.r1";
    private const string ScopeStableId = "scope:administrative-dong-intersection:northeast-seoul-rider:g3b:r1";
    private const string CandidateSchemaVersion = "administrative-dong-intersection-point-candidate.v1";
    private const string ProtectedPayloadSchemaVersion = "administrative-dong-intersection-point-protected-rdb-payload.v1";
    private const string Quality = "PendingHumanReview";
    private const string SourceVintage = "oa15534-file-20250814+oa22160-file-20231031+northeast-seoul-rider-r2+crosswalk-g3a-r2+intersection-g3b-r1";
    private const string CompletionUpperBound = "LocalPrivateHistoricalIntersectionPointCandidateLedgerStoredAndVerified";
    private const string LedgerRecordCode = "LocalPrivateHistoricalIntersectionPointCandidateLedgerRecord";
    private const string Limitation = "PrivateReviewOnly;HistoricalBoundaryBootstrapOnly;IntersectionPointOnly;NoCurrentBoundary;NoTopology;NoDirection;NoController;NoLane;NoSignal;NoDistribution;NoRuntime;NoTraversal;NoGameplay;NoUnity";
    private const string LockName = "mirror:public-data:admin-dong-intersection-g3b-r1";
    private const string G3aLockName = "mirror:public-data:admin-dong-crosswalk-g3a-r2";
    private const string G4aLockName = "mirror:public-data:admin-dong-business-g4a-r1";
    private const string LegacyLockName = "mirror:public-data:myeonmok-business";

    private const string CandidateDesignRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-intersection-point-candidate.implementation.r10.md";
    private const string CandidateDataImplementationRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-intersection-point-candidate.data-implementation.v1.json";
    private const string LedgerDesignRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-intersection-point-ledger.implementation.r11.md";
    private const string LedgerDataImplementationRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-intersection-point-ledger.data-implementation.v1.json";
    private const string ScopeRelative = "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-intersection.g3b.r1.json";
    private const string GeneratorRelative = "eng/neighborhood/administrative_dong_intersection_candidate.py";
    private const string OutputRelative = "artifacts/local/public-data/admin-dong-intersection-northeast-seoul-20260915-g3b-r1";
    private const string GenerationRelative = OutputRelative + "/generations/db83f1cc82eb65f864188dd790a003edf570dbb459d56d4c07ce690988f2837b";

    private const string CandidateDesignHash = "1FE3EE996A68FDC03C6E4552D51F4DF923276EE95CCCDACEBD75DE6B28888FFD";
    private const string CandidateDataImplementationHash = "A7147E524766684A51FC335BA8DFECA7C40803C48C7BC5F2820F8C749A2A7882";
    private const string LedgerDesignHash = "5B1C34AE5AE45C89A5E0FAC16B3EAA93064113F0ECF732E250E877FAAB0A68B2";
    private const string LedgerDataImplementationHash = "65AE63E6C5D4F1A644D8D3B56B237D0C14B5600DAF19755D98D6414C28C505C7";
    private const string ScopeDefinitionHash = "820F999B717BD7584F6CABDE5D3A7B1E6882BF3B51619E3B19762331ED2657C6";
    private const string GeneratorHash = "7CA125BFAE2B18E1AABCE489AECC803B3A668DE6BEA142492EB94A1455BA6107";
    private const string CandidateSetHash = "DB83F1CC82EB65F864188DD790A003EDF570DBB459D56D4C07CE690988F2837B";
    private const string ManifestHash = "17DBA105F6C9F4FA70235B0ACA62CDF61B17E6B585B05CABFAE0D608CC2C3BDC";
    private const string CandidatesHash = "2D72DEF4E530E84D6D3DC4A344B688A8694129C0DC8F95D3568F6468C5506ABD";
    private const string AuditHash = "C9028FC8861C8EE310823C4F5301930EE236F2B551497A438D5B03B6E7AF7694";
    private const string GeneratorSourceHash = "7CA125BFAE2B18E1AABCE489AECC803B3A668DE6BEA142492EB94A1455BA6107";
    private const string CompleteHash = "FC6D0A9410FA16D243E666DE9A3B4EB8BCCDAAF32FBCCF8FBBD3A2A8EF27603F";
    private const string ManifestContentHash = "25F976C726C08AD0126FD85F171F9F4F2AC138089045BF782AB1D853E84F30F4";
    private const string AuditContentHash = "94712A7CEBC7ED800159FEFFDF0E38F48DF229CC77630EB00BCBCC5482A26054";
    private const string CompleteContentHash = "BBA981A5A90E7CFD55F177D0F90513C3B8A6D2E8E61A9491500D11767F9143EF";
    private const string Oa15534Hash = "A77B4D4FD2886C934D2D097558A52580FA95ADB079BA828F1305DFD15F0E0449";
    private const string Oa22160Hash = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68";
    private const string G3aCandidateSetHash = "78312E5F0DDB89BFFA9AD1881379CBF7E8037A6C6454D4F0ABDF2A3E4D22D30A";

    private const int ExpectedSourceRows = 8_097;
    private const int ExpectedCandidateRows = 554;
    private const int ExpectedOutsideRows = 7_543;
    private const int ExpectedAmbiguousRows = 0;
    private const int ExpectedAreaCount = 30;
    private const int ExpectedNormalRows = 553;
    private const int ExpectedConflictRows = 1;
    private const int ExpectedCrosswalkLinkedRows = 524;
    private const int ExpectedCrosswalkUnlinkedRows = 30;
    private const int ExpectedLinkedRelations = 1_491;
    private const int ExpectedSameAreaRelations = 1_271;
    private const int ExpectedOtherAreaRelations = 220;
    private const int ExpectedG3aRows = 1_533;
    private const int ExpectedG3aSnapshots = 13;
    private const int ExpectedG4aRows = 29_721;
    private const int ExpectedG4aSnapshots = 19;
    private const int ExpectedLegacyShopRows = 5_411;
    private const int ExpectedLegacyFactoryRows = 126;

    private static readonly DateTimeOffset EvidenceAsOfUtc = new(2025, 8, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CollectedAtUtc =
        new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] CandidateHashHeaderFields =
    [
        "candidateSchemaVersion", "revision", "designHashSha256", "dataImplementationHashSha256",
        "scopeDefinitionHashSha256", "oa15534SourceHashSha256", "oa15534AcquisitionReceiptHashSha256",
        "oa22160BoundaryArchiveHashSha256", "r2ScopeDefinitionHashSha256", "r2ScopeManifestHashSha256",
        "g3aScopeDefinitionHashSha256", "g3aManifestHashSha256", "g3aCandidatesHashSha256",
        "g3aCompleteHashSha256", "g3aCandidateSetHashSha256", "generatorHashSha256",
        "authorityFlagsCanonicalJson"
    ];

    private static readonly string[] CandidateHashFields =
    [
        "candidateStableId", "sourceFeatureKey", "administrativeAreaStableId", "assignmentStateCode",
        "candidateQualityCode", "sourceManagementNumber", "intersectionManagementNumber",
        "sourceGeometryPointEpsg5186CanonicalJson", "commonEnuMillimetersCanonicalJson",
        "sourceDistrictCode", "sourceBoroughName", "spatialAssignmentBoroughName",
        "sourceDistrictSpatialAssignmentConflictLowercase", "sourceCoordinateAttributeStateCode",
        "linkedCrosswalksCanonicalJson", "qualityDiagnosticCodesCanonicalJson", "ownershipBasisCode"
    ];

    private static readonly string[] CandidateProperties =
    [
        "administrativeAreaStableId", "assignmentStateCode", "authorityFlags", "candidateQualityCode",
        "candidateSetHashSha256", "candidateStableId", "commonEnuMillimeters",
        "crosswalkDiagnosticCandidateSetHashSha256", "crosswalkLinkStateCode",
        "distanceToAssignedHistoricalBoundaryMeters", "historicalBoundaryHashSha256",
        "intersectionManagementNumber", "linkedCrosswalks", "ownershipBasisCode",
        "qualityDiagnosticCodes", "revision", "schemaVersion", "sourceBoroughName",
        "sourceCoordinateAttributeStateCode", "sourceCoordinateReferenceStatus", "sourceDistrictCode",
        "sourceDistrictSpatialAssignmentConflict", "sourceFeatureKey", "sourceGeometryPointEpsg5186",
        "sourceHashSha256", "sourceManagementNumber", "sourceRecordNumber", "spatialAssignmentBoroughName"
    ];

    private static readonly string[] TrueAuthorityFlags =
        ["privateReviewOnly", "historicalBoundaryBootstrapOnly", "observationCandidateOnly", "sourceDeclaredCoordinateReference"];

    private static readonly string[] FalseAuthorityFlags =
    [
        "currentAdministrativeBoundaryEstablished", "currentIntersectionTopologyEstablished",
        "approachDirectionEstablished", "controllerBindingEstablished", "laneBindingEstablished",
        "signalBindingEstablished", "crosswalkAssignmentInherited", "distributionApproved",
        "publicDisplayAllowed", "databasePersistenceCompleted", "runtimeAuthorized", "traversalReady",
        "gameplayReady", "unityApplyAllowed"
    ];

    private static readonly string[] PreservationLockNames =
        [G4aLockName, G3aLockName, LockName, LegacyLockName];

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private sealed record FrozenSource(string Role, string Path, string RelativePath, string Hash, long Length,
        string SourceRevision);

    private sealed record LinkedCrosswalk(
        string AdministrativeAreaStableId,
        string CrosswalkManagementNumber,
        bool SameHistoricalAdministrativeArea,
        string SourceFeatureKey);

    private sealed record Candidate(
        string CandidateStableId,
        string SourceFeatureKey,
        string AdministrativeAreaStableId,
        string SourceManagementNumber,
        string SourceDistrictCode,
        bool SourceDistrictSpatialAssignmentConflict,
        string SourceCoordinateAttributeStateCode,
        string CrosswalkLinkStateCode,
        IReadOnlyList<LinkedCrosswalk> LinkedCrosswalks,
        IReadOnlyList<string> QualityDiagnosticCodes,
        string CandidateBodyHashSha256,
        string LinkedCrosswalkSetHashSha256,
        string[] HashValues);

    private sealed record AreaCounts(
        int CandidateRows,
        int CrosswalkLinkedCandidateRows,
        int CrosswalkUnlinkedCandidateRows,
        int LinkedCrosswalkRelations,
        int LinkedCrosswalkSameHistoricalHjdRelations,
        int LinkedCrosswalkOtherHistoricalHjdRelations,
        int SourceDistrictSpatialAssignmentConflictRows);

    private sealed record Bundle(
        string Root,
        string ScopePath,
        string GeneratorPath,
        string GenerationPath,
        string ManifestPath,
        string CandidatesPath,
        string AuditPath,
        string GeneratorSourcePath,
        string CompletePath,
        IReadOnlyDictionary<string, FrozenSource> Sources,
        IReadOnlyList<string> HeaderValues,
        IReadOnlyDictionary<string, AreaCounts> ExpectedPerArea,
        IReadOnlyList<Candidate> Candidates);

    private sealed record RegistrationInput(
        string DatasetId,
        string Path,
        string RelativePath,
        string Hash,
        long Length,
        string SourceVersion,
        string ContentType,
        bool CanonicalSource);

    private sealed record LedgerStateSummary(
        int NormalizedRows,
        int RawSnapshots,
        int IngestionRuns,
        string StateHashSha256);

    private sealed record LegacyStateSummary(
        int ShopRows,
        int FactoryRows,
        int RawSnapshots,
        int IngestionRuns,
        string StateHashSha256);

    private sealed record PreservationState(
        LedgerStateSummary G3a,
        LedgerStateSummary G4a,
        LegacyStateSummary LegacyMyeonmok);

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
            result["status"] = "PASS";
            return;
        }

        var options = await 로컬공공자료Db.OptionsAsync(root);
        var preservationBefore = await ReadPreservationStateAsync(options);
        SetPreservationSummary(result, preservationBefore);

        if (mode == "preview")
        {
            await using var preview = new PublicDataIngestionDbContext(options);
            await ValidateSourceSnapshotPreflightAsync(preview, bundle);
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
        Require(preservationAfter == preservationBefore, "PreservedLedgerChanged");
        SetPreservationSummary(result, preservationAfter);
        result["status"] = "PASS";
    }

    private static Bundle LoadAndValidateBundle(string root)
    {
        var candidateDesignPath = ResolveRepositoryPath(root, CandidateDesignRelative);
        var candidateDataPath = ResolveRepositoryPath(root, CandidateDataImplementationRelative);
        var ledgerDesignPath = ResolveRepositoryPath(root, LedgerDesignRelative);
        var ledgerDataPath = ResolveRepositoryPath(root, LedgerDataImplementationRelative);
        Require(HashFile(candidateDesignPath) == CandidateDesignHash, "CandidateDesignHashChanged");
        Require(HashFile(candidateDataPath) == CandidateDataImplementationHash, "CandidateDataImplementationHashChanged");
        Require(HashFile(ledgerDesignPath) == LedgerDesignHash, "LedgerDesignHashChanged");
        Require(HashFile(ledgerDataPath) == LedgerDataImplementationHash, "LedgerDataImplementationHashChanged");
        ValidateLedgerDataImplementation(ledgerDataPath);

        var scopePath = ResolveRepositoryPath(root, ScopeRelative);
        var generatorPath = ResolveRepositoryPath(root, GeneratorRelative);
        Require(HashFile(scopePath) == ScopeDefinitionHash, "ScopeDefinitionHashChanged");
        Require(HashFile(generatorPath) == GeneratorHash, "GeneratorHashChanged");
        using var scopeDocument = ReadJson(scopePath, 128_000);
        ValidateScope(scopeDocument.RootElement, root, generatorPath, out var sources, out var expectedPerArea);

        var generationPath = ResolveRepositoryPath(root, GenerationRelative);
        Require(Directory.Exists(generationPath) && !IsReparsePoint(generationPath), "GenerationMissingOrUnsafe");
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
        Require(HashFile(manifestPath) == ManifestHash && new FileInfo(manifestPath).Length == 22_428,
            "ManifestFileChanged");
        Require(HashFile(candidatesPath) == CandidatesHash && new FileInfo(candidatesPath).Length == 1_562_936,
            "CandidatesFileChanged");
        Require(HashFile(auditPath) == AuditHash && new FileInfo(auditPath).Length == 18_952,
            "AuditFileChanged");
        Require(HashFile(generatorSourcePath) == GeneratorSourceHash
                && new FileInfo(generatorSourcePath).Length == 82_458
                && File.ReadAllBytes(generatorSourcePath).SequenceEqual(File.ReadAllBytes(generatorPath)),
            "GeneratorSourceSnapshotChanged");
        Require(HashFile(completePath) == CompleteHash && new FileInfo(completePath).Length == 2_271,
            "CompleteFileChanged");

        using var manifestDocument = ReadJson(manifestPath, 128_000);
        using var auditDocument = ReadJson(auditPath, 128_000);
        using var completeDocument = ReadJson(completePath, 32_000);
        var headerValues = ValidateManifest(manifestDocument.RootElement, sources, expectedPerArea);
        ValidateAudit(auditDocument.RootElement);
        ValidateComplete(completeDocument.RootElement);
        var candidates = ReadCandidates(candidatesPath, expectedPerArea);
        Require(ComputeCandidateSetHash(headerValues, candidates) == CandidateSetHash,
            "CandidateSetHashMismatch");

        return new Bundle(root, scopePath, generatorPath, generationPath, manifestPath, candidatesPath,
            auditPath, generatorSourcePath, completePath, sources, headerValues, expectedPerArea, candidates);
    }

    private static void ValidateLedgerDataImplementation(string path)
    {
        using var document = ReadJson(path, 64_000);
        var value = document.RootElement;
        Require(Text(value, "schemaVersion") == "public-data-candidate-ledger-implementation.v1",
            "LedgerDataImplementationSchemaChanged");
        Require(value.TryGetProperty("playableLoopWorkOrder", out var playable)
                && playable.ValueKind == JsonValueKind.False,
            "LedgerDataImplementationPlayableLoopBoundaryChanged");
        Require(!value.TryGetProperty("evidenceStageClaimed", out var evidence)
                || evidence.ValueKind == JsonValueKind.Null,
            "LedgerDataImplementationEvidenceClaimChanged");
        var gate = value.GetProperty("planningGate");
        Require(Text(gate, "designDocumentRef") == LedgerDesignRelative
                && Text(gate, "designHashSha256") == LedgerDesignHash,
            "LedgerDataImplementationPlanningGateChanged");
    }

    private static void ValidateScope(
        JsonElement value,
        string root,
        string generatorPath,
        out IReadOnlyDictionary<string, FrozenSource> sources,
        out IReadOnlyDictionary<string, AreaCounts> expectedPerArea)
    {
        Require(Text(value, "schemaVersion") == "administrative-dong-intersection-generation-scope.v1"
                && Text(value, "scopeStableId") == ScopeStableId
                && Text(value, "revision") == Revision
                && Text(value, "generatedAtUtc") == "2026-09-15T00:00:00Z"
                && Text(value, "sourceVintage") == SourceVintage
                && Text(value, "reviewStatus") == Quality
                && Text(value, "coordinateReferenceStatus") == "SourceDeclaredEpsg5186",
            "ScopeIdentityChanged");
        var gate = value.GetProperty("planningGate");
        Require(Text(gate, "statusCode") == "ApprovedForLocalPrivateCandidateGeneration"
                && Text(gate, "designDocumentRef") == CandidateDesignRelative
                && Text(gate, "designHashSha256") == CandidateDesignHash
                && Text(gate, "dataImplementationRef") == CandidateDataImplementationRelative
                && Text(gate, "dataImplementationHashSha256") == CandidateDataImplementationHash,
            "ScopePlanningGateChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "Scope");
        ValidateExpectedCounts(value.GetProperty("expectedCounts"));

        var coordinateConversion = value.GetProperty("historicalBoundaryConversion");
        Require(Text(coordinateConversion, "sourceCoordinateReference") == "EPSG:5181"
                && Text(coordinateConversion, "assignmentCoordinateReference") == "EPSG:5186"
                && Double(coordinateConversion, "xOffsetMeters") == 0d
                && Double(coordinateConversion, "yOffsetMeters") == 100_000d
                && !Bool(coordinateConversion, "rotationApplied")
                && !Bool(coordinateConversion, "scaleApplied")
                && !Bool(coordinateConversion, "inferredTranslationApplied"),
            "HistoricalBoundaryConversionChanged");
        Require(Text(value, "sourceGeometryAuthorityCode") == "ShapefilePointGeometry"
                && Text(value, "dbfCoordinateAttributeDispositionCode") == "NonAuthoritativeQualityDiagnosticOnly"
                && Text(value, "walkNetworkDispositionCode") == "Oa21208Year2020NotConsumedNotCurrent"
                && Text(value, "laneDatasetDispositionCode") == "Oa15537LaneMarkingsNotConsumedNotDrivableLanes",
            "SourceAuthorityBoundaryChanged");
        var consumed = value.GetProperty("consumedTrafficSourceKeys").EnumerateArray()
            .Select(item => item.GetString()).ToArray();
        Require(consumed.SequenceEqual(["intersection"], StringComparer.Ordinal), "ConsumedTrafficSourceSetChanged");
        var excluded = value.GetProperty("explicitlyExcludedTrafficLayers").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal);
        Require(excluded.SetEquals(["lane", "direction", "controller", "signal"]),
            "ExcludedTrafficLayerSetChanged");
        var crosswalk = value.GetProperty("crosswalkDiagnostic");
        Require(Text(crosswalk, "revision") == "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r2"
                && Text(crosswalk, "candidateSetHashSha256") == G3aCandidateSetHash
                && Text(crosswalk, "linkMethodCode") == "CssNumEqualsG3aIntersectionManagementNumberAfterOwnAssignment"
                && !Bool(crosswalk, "crosswalkLinkCanAssignAdministrativeArea"),
            "CrosswalkDiagnosticAuthorityChanged");
        var receiptSelection = value.GetProperty("sourceReceiptSelectionDisposition");
        Require(Int(receiptSelection, "receiptSagajeongBboxSelectedRows") == 25
                && !Bool(receiptSelection, "usedForThirtyDongCandidateSelection")
                && Text(receiptSelection, "selectionMethodCode") == "WholeSourceOwnPointHistoricalBoundaryCover",
            "ReceiptSelectionDispositionChanged");
        var sourceDistricts = value.GetProperty("expectedAssignedSourceDistrictCodeCounts")
            .EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
        Require(DictionaryEqual(sourceDistricts, new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["200"] = 1, ["210"] = 55, ["230"] = 192, ["260"] = 306
        }), "ExpectedSourceDistrictDistributionChanged");

        var perArea = value.GetProperty("expectedPerAdministrativeArea").EnumerateObject()
            .ToDictionary(item => "region:kr:hjd:" + item.Name, item => ReadAreaCounts(item.Value),
                StringComparer.Ordinal);
        Require(perArea.Count == ExpectedAreaCount
                && perArea.Values.Sum(item => item.CandidateRows) == ExpectedCandidateRows
                && perArea.Values.Sum(item => item.CrosswalkLinkedCandidateRows) == ExpectedCrosswalkLinkedRows
                && perArea.Values.Sum(item => item.CrosswalkUnlinkedCandidateRows) == ExpectedCrosswalkUnlinkedRows
                && perArea.Values.Sum(item => item.LinkedCrosswalkRelations) == ExpectedLinkedRelations
                && perArea.Values.Sum(item => item.LinkedCrosswalkSameHistoricalHjdRelations) == ExpectedSameAreaRelations
                && perArea.Values.Sum(item => item.LinkedCrosswalkOtherHistoricalHjdRelations) == ExpectedOtherAreaRelations
                && perArea.Values.Sum(item => item.SourceDistrictSpatialAssignmentConflictRows) == ExpectedConflictRows,
            "ExpectedPerAreaTotalsChanged");
        var areas = value.GetProperty("administrativeAreas").EnumerateArray()
            .Select(item => Text(item, "administrativeAreaStableId")).Order(StringComparer.Ordinal).ToArray();
        Require(areas.SequenceEqual(perArea.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "ScopeAdministrativeAreaSetChanged");
        expectedPerArea = perArea;

        var expectedRoles = new HashSet<string>(
        [
            "oa15534IntersectionArchive", "oa15534AcquisitionReceipt", "oa22160BoundaryArchive",
            "r2ScopeDefinition", "r2ScopeManifest", "g3aScopeDefinition", "g3aManifest",
            "g3aCandidates", "g3aComplete"
        ], StringComparer.Ordinal);
        var frozen = new Dictionary<string, FrozenSource>(StringComparer.Ordinal);
        foreach (var item in value.GetProperty("sources").EnumerateArray())
        {
            var role = Text(item, "role");
            Require(expectedRoles.Contains(role) && !frozen.ContainsKey(role), "ScopeSourceRoleChanged");
            var relative = Text(item, "repositoryRelativePath");
            var path = ResolveRepositoryPath(root, relative);
            var hash = Text(item, "contentHashSha256");
            var length = Long(item, "byteLength");
            Require(File.Exists(path) && !IsReparsePoint(path)
                    && new FileInfo(path).Length == length && HashFile(path) == hash,
                "FrozenSourceChanged:" + role);
            frozen.Add(role, new FrozenSource(role, path, relative, hash, length, Text(item, "sourceRevision")));
        }
        Require(frozen.Count == expectedRoles.Count
                && frozen["oa15534IntersectionArchive"].Hash == Oa15534Hash
                && frozen["oa15534IntersectionArchive"].Length == 534_342
                && frozen["oa22160BoundaryArchive"].Hash == Oa22160Hash
                && frozen["g3aCandidates"].Hash == "F852174430E66554363F38FA8996BA6895C84F81CA92787969F869CE4AA89EB7"
                && frozen["g3aCandidates"].Length == 2_905_027,
            "ScopeSourceExactSetChanged");
        sources = frozen;

        var output = value.GetProperty("output");
        var toolchain = output.GetProperty("toolchain");
        Require(Text(output, "repositoryRelativeDirectory") == OutputRelative
                && Text(output, "generationDirectoryName") == "generations"
                && Text(output, "candidateSchemaVersion") == CandidateSchemaVersion
                && Text(output, "persistenceDatasetId") == DatasetId
                && !Bool(output, "currentPointerAllowed")
                && Text(toolchain, "generatorRelativePath") == GeneratorRelative
                && Text(toolchain, "generatorSha256") == GeneratorHash
                && HashFile(generatorPath) == GeneratorHash,
            "ScopeOutputContractChanged");
    }

    private static IReadOnlyList<string> ValidateManifest(
        JsonElement value,
        IReadOnlyDictionary<string, FrozenSource> sources,
        IReadOnlyDictionary<string, AreaCounts> expectedPerArea)
    {
        Require(Text(value, "schemaVersion") == "administrative-dong-intersection-point-candidate-manifest.v1"
                && Text(value, "scopeStableId") == ScopeStableId
                && Text(value, "revision") == Revision
                && Text(value, "generatedAtUtc") == "2026-09-15T00:00:00Z"
                && Text(value, "sourceVintage") == SourceVintage
                && Text(value, "completionUpperBoundCode") == "LocalPrivateHistoricalIntersectionPointCandidateGenerated"
                && Text(value, "designDocumentRef") == CandidateDesignRelative
                && Text(value, "designHashSha256") == CandidateDesignHash
                && Text(value, "dataImplementationRef") == CandidateDataImplementationRelative
                && Text(value, "dataImplementationHashSha256") == CandidateDataImplementationHash
                && Text(value, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "contentHashSha256") == ManifestContentHash
                && ComputeContentHash(value) == ManifestContentHash,
            "ManifestIdentityChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "Manifest");
        ValidateExpectedCounts(value.GetProperty("counts"));

        var contract = value.GetProperty("candidateSetHashContract");
        Require(Text(contract, "algorithmCode") == "Sha256UnsignedBigEndianUInt32LengthPrefixedUtf8Fields"
                && Text(contract, "canonicalJsonCode") == "Utf8SortedKeysCompactNoNaN",
            "CandidateHashAlgorithmChanged");
        var fields = contract.GetProperty("headerFieldOrder").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        var candidateFields = contract.GetProperty("candidateFieldOrder").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        Require(fields.SequenceEqual(CandidateHashHeaderFields, StringComparer.Ordinal)
                && candidateFields.SequenceEqual(CandidateHashFields, StringComparer.Ordinal),
            "CandidateHashFieldOrderChanged");
        var header = contract.GetProperty("headerValues").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        var expectedHeader = new[]
        {
            CandidateSchemaVersion, Revision, CandidateDesignHash, CandidateDataImplementationHash,
            ScopeDefinitionHash, sources["oa15534IntersectionArchive"].Hash,
            sources["oa15534AcquisitionReceipt"].Hash, sources["oa22160BoundaryArchive"].Hash,
            sources["r2ScopeDefinition"].Hash, sources["r2ScopeManifest"].Hash,
            sources["g3aScopeDefinition"].Hash, sources["g3aManifest"].Hash,
            sources["g3aCandidates"].Hash, sources["g3aComplete"].Hash,
            G3aCandidateSetHash, GeneratorHash, CanonicalJsonText(AuthorityDictionary())
        };
        Require(header.SequenceEqual(expectedHeader, StringComparer.Ordinal), "CandidateHashHeaderValuesChanged");

        var ids = value.GetProperty("administrativeAreaIds").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        Require(ids.SequenceEqual(expectedPerArea.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "ManifestAdministrativeAreaSetChanged");
        var perArea = value.GetProperty("perAdministrativeArea").EnumerateArray()
            .ToDictionary(item => Text(item, "administrativeAreaStableId"), ReadAreaCounts, StringComparer.Ordinal);
        Require(DictionaryEqual(perArea, expectedPerArea), "ManifestPerAreaDistributionChanged");
        return header;
    }

    private static void ValidateAudit(JsonElement value)
    {
        Require(Text(value, "schemaVersion") == "administrative-dong-intersection-point-candidate-audit.v1"
                && Text(value, "scopeStableId") == ScopeStableId
                && Text(value, "revision") == Revision
                && Text(value, "generatedAtUtc") == "2026-09-15T00:00:00Z"
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(value, "walkNetworkDispositionCode") == "Oa21208Year2020NotConsumedNotCurrent"
                && Text(value, "laneDatasetDispositionCode") == "Oa15537LaneMarkingsNotConsumedNotDrivableLanes"
                && Text(value, "contentHashSha256") == AuditContentHash
                && ComputeContentHash(value) == AuditContentHash,
            "AuditIdentityChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "Audit");
    }

    private static void ValidateComplete(JsonElement value)
    {
        Require(Text(value, "schemaVersion") == "administrative-dong-intersection-point-candidate-complete.v1"
                && Text(value, "scopeStableId") == ScopeStableId
                && Text(value, "revision") == Revision
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(value, "generationRelativePath") == GenerationRelative
                && Bool(value, "completeMarker")
                && Text(value, "completionCode") == "LocalPrivateHistoricalIntersectionPointCandidateGenerated"
                && Text(value, "contentHashSha256") == CompleteContentHash
                && ComputeContentHash(value) == CompleteContentHash,
            "CompleteIdentityChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "Complete");
        var expected = new Dictionary<string, (string Hash, long Length)>(StringComparer.Ordinal)
        {
            ["manifest.json"] = (ManifestHash, 22_428), ["candidates.ndjson"] = (CandidatesHash, 1_562_936),
            ["audit.json"] = (AuditHash, 18_952), ["generator-source.py"] = (GeneratorSourceHash, 82_458)
        };
        var files = value.GetProperty("files").EnumerateArray().ToArray();
        Require(files.Length == expected.Count, "CompleteFileCountChanged");
        foreach (var item in files)
        {
            var relative = Text(item, "relativePath");
            Require(expected.TryGetValue(relative, out var contract)
                    && Text(item, "sha256") == contract.Hash
                    && Long(item, "byteLength") == contract.Length,
                "CompleteFileContractChanged");
        }
    }

    private static AreaCounts ReadAreaCounts(JsonElement value)
        => new(
            Int(value, "candidateRows"),
            Int(value, "crosswalkLinkedCandidateRows"),
            Int(value, "crosswalkUnlinkedCandidateRows"),
            Int(value, "linkedCrosswalkRelations"),
            Int(value, "linkedCrosswalkSameHistoricalHjdRelations"),
            Int(value, "linkedCrosswalkOtherHistoricalHjdRelations"),
            Int(value, "sourceDistrictSpatialAssignmentConflictRows"));

    private static void ValidateExpectedCounts(JsonElement value)
    {
        Require(Int(value, "sourceRows") == ExpectedSourceRows
                && Int(value, "sourcePointGeometryRows") == ExpectedSourceRows
                && Int(value, "sourceCoordinateAttributePresentRows") == 7_796
                && Int(value, "sourceCoordinateAttributeMissingRows") == 301
                && Int(value, "uniquelyAssignedRows") == ExpectedCandidateRows
                && Int(value, "outsideScopeRows") == ExpectedOutsideRows
                && Int(value, "unresolvedMultipleBoundaryRows") == ExpectedAmbiguousRows
                && Int(value, "candidateRows") == ExpectedCandidateRows
                && Int(value, "administrativeAreasWithCandidates") == ExpectedAreaCount
                && Int(value, "sourceDistrictSpatialAssignmentNormalRows") == ExpectedNormalRows
                && Int(value, "sourceDistrictSpatialAssignmentConflictRows") == ExpectedConflictRows
                && Int(value, "crosswalkLinkedCandidateRows") == ExpectedCrosswalkLinkedRows
                && Int(value, "crosswalkUnlinkedCandidateRows") == ExpectedCrosswalkUnlinkedRows
                && Int(value, "linkedCrosswalkRelations") == ExpectedLinkedRelations
                && Int(value, "linkedCrosswalkSameHistoricalHjdRelations") == ExpectedSameAreaRelations
                && Int(value, "linkedCrosswalkOtherHistoricalHjdRelations") == ExpectedOtherAreaRelations,
            "ExpectedCountsChanged");
    }

    private static IReadOnlyList<Candidate> ReadCandidates(
        string path,
        IReadOnlyDictionary<string, AreaCounts> expectedPerArea)
    {
        var candidates = new List<Candidate>(ExpectedCandidateRows);
        var utf8 = new UTF8Encoding(false, true);
        foreach (var line in File.ReadLines(path, utf8))
        {
            Require(line.Length > 0 && Encoding.UTF8.GetByteCount(line) <= 32_000, "CandidateLineSizeInvalid");
            using var document = JsonDocument.Parse(line);
            var item = document.RootElement;
            Require(CanonicalJsonText(item) == line, "CandidateLineNotCanonical");
            RequireExactProperties(item, CandidateProperties, "CandidatePropertySetChanged");
            Require(Text(item, "schemaVersion") == CandidateSchemaVersion
                    && Text(item, "revision") == Revision
                    && Text(item, "candidateSetHashSha256") == CandidateSetHash
                    && Text(item, "sourceHashSha256") == Oa15534Hash
                    && Text(item, "historicalBoundaryHashSha256") == Oa22160Hash
                    && Text(item, "crosswalkDiagnosticCandidateSetHashSha256") == G3aCandidateSetHash
                    && Text(item, "sourceCoordinateReferenceStatus") == "SourceDeclaredEpsg5186"
                    && Text(item, "assignmentStateCode") == "UniqueHistoricalBoundaryCover"
                    && Text(item, "candidateQualityCode") == Quality
                    && Text(item, "ownershipBasisCode") == "OwnSourcePointHistoricalBoundaryCover",
                "CandidateIdentityChanged");
            ValidateAuthority(item.GetProperty("authorityFlags"), "Candidate");

            var sourceManagementNumber = Text(item, "sourceManagementNumber");
            var sourceFeatureKey = Text(item, "sourceFeatureKey");
            var candidateStableId = Text(item, "candidateStableId");
            Require(Regex.IsMatch(sourceManagementNumber, "^82-[0-9]{10}$", RegexOptions.CultureInvariant)
                    && sourceFeatureKey == "oa15534:intersection:" + sourceManagementNumber
                    && Text(item, "intersectionManagementNumber").Length > 0
                    && Int(item, "sourceRecordNumber") > 0,
                "CandidateSourceIdentityChanged");
            var expectedCandidateStableId = "candidate:kr:seoul:oa15534:intersection:" +
                FrameDigest(["OA-15534", "file-modified:20250814:retrieved:20260913", sourceManagementNumber])
                    .ToLowerInvariant();
            Require(candidateStableId == expectedCandidateStableId, "CandidateStableIdChanged");

            var area = Text(item, "administrativeAreaStableId");
            Require(expectedPerArea.ContainsKey(area), "CandidateAdministrativeAreaChanged");
            var sourcePoint = item.GetProperty("sourceGeometryPointEpsg5186");
            var commonEnu = item.GetProperty("commonEnuMillimeters");
            RequireExactProperties(sourcePoint, ["x", "y"], "CandidateSourcePointShapeChanged");
            RequireExactProperties(commonEnu, ["x", "z"], "CandidateCommonEnuShapeChanged");
            Require(double.IsFinite(Double(sourcePoint, "x")) && double.IsFinite(Double(sourcePoint, "y"))
                    && commonEnu.GetProperty("x").TryGetInt64(out _)
                    && commonEnu.GetProperty("z").TryGetInt64(out _)
                    && double.IsFinite(Double(item, "distanceToAssignedHistoricalBoundaryMeters")),
                "CandidateCoordinateChanged");

            var links = new List<LinkedCrosswalk>();
            foreach (var linkElement in item.GetProperty("linkedCrosswalks").EnumerateArray())
            {
                RequireExactProperties(linkElement,
                    ["administrativeAreaStableId", "crosswalkManagementNumber", "sameHistoricalAdministrativeArea", "sourceFeatureKey"],
                    "LinkedCrosswalkPropertySetChanged");
                var link = new LinkedCrosswalk(
                    Text(linkElement, "administrativeAreaStableId"),
                    Text(linkElement, "crosswalkManagementNumber"),
                    Bool(linkElement, "sameHistoricalAdministrativeArea"),
                    Text(linkElement, "sourceFeatureKey"));
                Require(expectedPerArea.ContainsKey(link.AdministrativeAreaStableId)
                        && link.SameHistoricalAdministrativeArea == (link.AdministrativeAreaStableId == area)
                        && link.SourceFeatureKey.StartsWith("oa23081:crosswalk:", StringComparison.Ordinal)
                        && link.CrosswalkManagementNumber.Length > 0,
                    "LinkedCrosswalkContractChanged");
                links.Add(link);
            }
            Require(links.Select(link => link.SourceFeatureKey)
                    .SequenceEqual(links.Select(link => link.SourceFeatureKey).Order(StringComparer.Ordinal),
                        StringComparer.Ordinal)
                    && links.Select(link => link.SourceFeatureKey).Distinct(StringComparer.Ordinal).Count() == links.Count,
                "LinkedCrosswalkOrderOrIdentityChanged");

            var linkState = Text(item, "crosswalkLinkStateCode");
            var otherAreaLinks = links.Count(link => !link.SameHistoricalAdministrativeArea);
            Require(linkState == (links.Count == 0
                    ? "NoInScopeCrosswalkLinkObserved"
                    : otherAreaLinks > 0 ? "ObservedWithOtherHistoricalHjd" : "ObservedSameHistoricalHjdOnly"),
                "CrosswalkLinkStateChanged");
            var quality = item.GetProperty("qualityDiagnosticCodes").EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty).ToArray();
            Require(quality.Length > 0
                    && quality.SequenceEqual(quality.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                    && quality.Distinct(StringComparer.Ordinal).Count() == quality.Length
                    && quality.Contains("HistoricalBoundaryBootstrapOnly", StringComparer.Ordinal)
                    && quality.Contains("CrosswalkLinkObservedOtherHistoricalHjd", StringComparer.Ordinal) == (otherAreaLinks > 0)
                    && quality.Contains("NoInScopeCrosswalkLinkObserved", StringComparer.Ordinal) == (links.Count == 0)
                    && quality.Contains("SourceCoordinateAttributeMissing", StringComparer.Ordinal) ==
                        (Text(item, "sourceCoordinateAttributeStateCode") == "MissingBoth")
                    && quality.Contains("SourceDistrictSpatialAssignmentConflict", StringComparer.Ordinal) ==
                        Bool(item, "sourceDistrictSpatialAssignmentConflict"),
                "CandidateQualityDiagnosticsChanged");
            Require(Text(item, "sourceCoordinateAttributeStateCode") is "PresentNonAuthoritativeInteger" or "MissingBoth",
                "SourceCoordinateAttributeStateChanged");

            var hashValues = new[]
            {
                candidateStableId,
                sourceFeatureKey,
                area,
                Text(item, "assignmentStateCode"),
                Text(item, "candidateQualityCode"),
                sourceManagementNumber,
                Text(item, "intersectionManagementNumber"),
                CanonicalJsonText(sourcePoint),
                CanonicalJsonText(commonEnu),
                Text(item, "sourceDistrictCode"),
                Text(item, "sourceBoroughName"),
                Text(item, "spatialAssignmentBoroughName"),
                Bool(item, "sourceDistrictSpatialAssignmentConflict") ? "true" : "false",
                Text(item, "sourceCoordinateAttributeStateCode"),
                CanonicalJsonText(item.GetProperty("linkedCrosswalks")),
                CanonicalJsonText(item.GetProperty("qualityDiagnosticCodes")),
                Text(item, "ownershipBasisCode")
            };
            Require(hashValues.Length == CandidateHashFields.Length, "CandidateHashValueCountChanged");
            var bodyHash = HashUtf8(line);
            var linkSetHash = HashUtf8(hashValues[14]);
            candidates.Add(new Candidate(candidateStableId, sourceFeatureKey, area, sourceManagementNumber,
                hashValues[9], Bool(item, "sourceDistrictSpatialAssignmentConflict"), hashValues[13],
                linkState, links, quality, bodyHash, linkSetHash, hashValues));
        }

        ValidateComputedCandidateSet(candidates, expectedPerArea);
        return candidates;
    }

    private static void ValidateComputedCandidateSet(
        IReadOnlyList<Candidate> candidates,
        IReadOnlyDictionary<string, AreaCounts> expectedPerArea)
    {
        Require(candidates.Count == ExpectedCandidateRows
                && candidates.Select(item => item.SourceManagementNumber)
                    .SequenceEqual(candidates.Select(item => item.SourceManagementNumber).Order(StringComparer.Ordinal),
                        StringComparer.Ordinal)
                && candidates.Select(item => item.CandidateStableId).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(item => item.SourceFeatureKey).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(item => item.SourceManagementNumber).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(CandidateIdentityHash).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(item => item.CandidateBodyHashSha256).Distinct(StringComparer.Ordinal).Count() == candidates.Count,
            "CandidateExactIdentitySetChanged");

        var sourceDistricts = candidates.GroupBy(item => item.SourceDistrictCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Require(DictionaryEqual(sourceDistricts, new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["200"] = 1, ["210"] = 55, ["230"] = 192, ["260"] = 306
        }), "ComputedSourceDistrictDistributionChanged");
        Require(candidates.Count(item => item.SourceDistrictSpatialAssignmentConflict) == ExpectedConflictRows
                && candidates.Count(item => !item.SourceDistrictSpatialAssignmentConflict) == ExpectedNormalRows
                && candidates.Count(item => item.LinkedCrosswalks.Count > 0) == ExpectedCrosswalkLinkedRows
                && candidates.Count(item => item.LinkedCrosswalks.Count == 0) == ExpectedCrosswalkUnlinkedRows
                && candidates.Sum(item => item.LinkedCrosswalks.Count) == ExpectedLinkedRelations
                && candidates.Sum(item => item.LinkedCrosswalks.Count(link => link.SameHistoricalAdministrativeArea)) == ExpectedSameAreaRelations
                && candidates.Sum(item => item.LinkedCrosswalks.Count(link => !link.SameHistoricalAdministrativeArea)) == ExpectedOtherAreaRelations,
            "ComputedCandidateDiagnosticsChanged");

        var actualPerArea = candidates.GroupBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new AreaCounts(
                group.Count(),
                group.Count(item => item.LinkedCrosswalks.Count > 0),
                group.Count(item => item.LinkedCrosswalks.Count == 0),
                group.Sum(item => item.LinkedCrosswalks.Count),
                group.Sum(item => item.LinkedCrosswalks.Count(link => link.SameHistoricalAdministrativeArea)),
                group.Sum(item => item.LinkedCrosswalks.Count(link => !link.SameHistoricalAdministrativeArea)),
                group.Count(item => item.SourceDistrictSpatialAssignmentConflict)), StringComparer.Ordinal);
        Require(DictionaryEqual(actualPerArea, expectedPerArea), "ComputedPerAreaDistributionChanged");
    }

    private static string ComputeCandidateSetHash(
        IEnumerable<string> headerValues,
        IEnumerable<Candidate> candidates)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in headerValues) AppendFramed(hash, value);
        foreach (var candidate in candidates)
            foreach (var value in candidate.HashValues)
                AppendFramed(hash, value);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string CandidateIdentityHash(Candidate candidate)
        => HashUtf8(candidate.CandidateStableId);

    private static List<외부데이터정규화Record> BuildRows(Bundle bundle)
    {
        var rows = new List<외부데이터정규화Record>(ExpectedCandidateRows);
        foreach (var candidate in bundle.Candidates)
        {
            var identityHash = CandidateIdentityHash(candidate);
            var stableId = "administrative-dong-intersection-point-candidate:sha256:" +
                identityHash.ToLowerInvariant();
            var dimensionKey = "intersection-point-candidate|sha256|" + identityHash;
            var payload = JsonSerializer.Serialize(new
            {
                schemaVersion = ProtectedPayloadSchemaVersion,
                candidateSetHashSha256 = CandidateSetHash,
                candidateBodyHashSha256 = candidate.CandidateBodyHashSha256,
                candidateIdentitySha256 = identityHash,
                candidate.AdministrativeAreaStableId,
                sourceHashSha256 = Oa15534Hash,
                historicalBoundaryHashSha256 = Oa22160Hash,
                crosswalkDiagnosticCandidateSetHashSha256 = G3aCandidateSetHash,
                assignmentStateCode = candidate.HashValues[3],
                candidateQualityCode = Quality,
                candidate.SourceDistrictSpatialAssignmentConflict,
                candidate.SourceCoordinateAttributeStateCode,
                candidate.CrosswalkLinkStateCode,
                linkedCrosswalkCount = candidate.LinkedCrosswalks.Count,
                linkedCrosswalkSetHashSha256 = candidate.LinkedCrosswalkSetHashSha256,
                candidate.QualityDiagnosticCodes,
                authorityFlags = AuthorityDictionary(),
                ledgerPersistenceCode = LedgerRecordCode
            }, CompactJson);
            Require(payload.Length <= 2_000 && Encoding.UTF8.GetByteCount(payload) <= 8_000
                    && dimensionKey.Length <= 500 && Limitation.Length <= 240,
                "DatabaseFieldBudgetExceeded");
            rows.Add(new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId,
                    candidate.AdministrativeAreaStableId, MetricCode, EvidenceAsOfUtc, dimensionKey),
                StableId = stableId,
                SourceId = SourceId,
                DatasetId = DatasetId,
                RegionStableId = candidate.AdministrativeAreaStableId,
                MetricCode = MetricCode,
                NumericValue = null,
                TextValue = payload,
                UnitCode = "intersection-point-candidate-json",
                EvidenceAsOfUtc = EvidenceAsOfUtc,
                CollectedAtUtc = CollectedAtUtc,
                SpatialPrecisionCode = "source-declared-epsg5186-point-within-2023-historical-hjd-candidate",
                TemporalPrecisionCode = "portal-file-vintage-not-observation-date",
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
                && rows.All(item => !Regex.IsMatch(item.StableId, "82-[0-9]{10}", RegexOptions.CultureInvariant)
                    && !Regex.IsMatch(item.DimensionKey, "82-[0-9]{10}", RegexOptions.CultureInvariant)),
            "NormalizedExactSetInvalid");
        return rows;
    }

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows,
        PreservationState preservationBefore,
        Dictionary<string, object?> result)
    {
        await using (var preflight = new PublicDataIngestionDbContext(options))
        {
            await ValidateSourceSnapshotPreflightAsync(preflight, bundle);
            var existing = await LoadScopedRowsAsync(preflight);
            await ValidateExistingAsync(preflight, bundle, existing, rows, requireComplete: false);
            result["beforeCount"] = existing.Count;
            if (existing.Count == rows.Count && await AreSourceSnapshotsCompleteAsync(preflight, bundle))
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
            foreach (var lockName in PreservationLockNames)
            {
                Require(await AcquireLockAsync(db, lockName), "ImportOrPreservationLedgerBusy");
                acquiredLocks.Add(lockName);
            }
            await using var transaction = await db.Database.BeginTransactionAsync();
            var lockedPreservation = await ReadPreservationStateAsync(db);
            Require(lockedPreservation == preservationBefore, "PreservedLedgerChangedBeforeApply");
            var lockedExisting = await LoadScopedRowsAsync(db);
            await ValidateSourceSnapshotPreflightAsync(db, bundle);
            await ValidateExistingAsync(db, bundle, lockedExisting, rows, requireComplete: false);
            if (lockedExisting.Count == rows.Count && await AreSourceSnapshotsCompleteAsync(db, bundle))
            {
                SetNoWriteResult(result, lockedExisting.Count);
                return;
            }

            result["databaseWriteAttempted"] = true;
            var registrations = new Dictionary<string, (long RawSnapshotId, bool Inserted)>(StringComparer.Ordinal);
            var registrationInputs = BuildRegistrationInputs(bundle);
            foreach (var input in registrationInputs)
            {
                var stored = await db.RawSnapshots.SingleOrDefaultAsync(item =>
                    item.SourceId == SourceId && item.DatasetId == input.DatasetId);
                if (stored is not null)
                {
                    await RequireSourceSnapshotAsync(db, input);
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
                        && registration.SourceHashSha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase),
                    "SourceRegistrationChanged");
                await NormalizeRegisteredSourceStateAsync(db, input, registration.RawSnapshotId);
                registrations.Add(input.DatasetId, (registration.RawSnapshotId, true));
            }
            foreach (var input in registrationInputs) await RequireSourceSnapshotAsync(db, input);

            var original = registrations[OriginalInputDatasetId];
            foreach (var row in rows) row.RawSnapshotId = original.RawSnapshotId;
            var inserted = 0;
            var existingCount = lockedExisting.Count == rows.Count ? rows.Count : 0;
            if (lockedExisting.Count != rows.Count)
            {
                foreach (var batch in rows.Chunk(500))
                {
                    Require(batch.Length <= 500, "NormalizedChunkSizeExceeded");
                    var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch);
                    Require(saved.UpdatedCount == 0, "UnexpectedNormalizedUpdate");
                    inserted += saved.InsertedCount;
                    existingCount += saved.ExistingCount;
                    db.ChangeTracker.Clear();
                }
            }
            Require(inserted + existingCount == rows.Count, "NormalizedSaveCountMismatch");
            Require(await ReadPreservationStateAsync(db) == preservationBefore,
                "PreservedLedgerChangedDuringApply");
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
    }

    private static IReadOnlyList<RegistrationInput> BuildRegistrationInputs(Bundle bundle)
    {
        var inputs = new List<RegistrationInput>();
        foreach (var source in bundle.Sources.Values.OrderBy(item => item.Role, StringComparer.Ordinal))
        {
            var canonical = source.Role == "oa15534IntersectionArchive";
            var dataset = canonical ? OriginalInputDatasetId : DatasetId + "-support-" + source.Role.ToLowerInvariant();
            inputs.Add(NewRegistration(dataset, source.Path, source.RelativePath, source.Hash,
                "sha256:" + source.Hash.ToLowerInvariant(), canonical));
        }

        var candidateDesign = ResolveRepositoryPath(bundle.Root, CandidateDesignRelative);
        var candidateData = ResolveRepositoryPath(bundle.Root, CandidateDataImplementationRelative);
        inputs.Add(NewRegistration(DatasetId + "-candidate-design-input", candidateDesign,
            CandidateDesignRelative, CandidateDesignHash, "sha256:" + CandidateDesignHash.ToLowerInvariant(), false));
        inputs.Add(NewRegistration(DatasetId + "-candidate-data-implementation-input", candidateData,
            CandidateDataImplementationRelative, CandidateDataImplementationHash,
            "sha256:" + CandidateDataImplementationHash.ToLowerInvariant(), false));
        inputs.Add(NewRegistration(DatasetId + "-scope-input", bundle.ScopePath, ScopeRelative,
            ScopeDefinitionHash, "sha256:" + ScopeDefinitionHash.ToLowerInvariant(), false));
        inputs.Add(NewRegistration(DatasetId + "-generator-input", bundle.GeneratorPath, GeneratorRelative,
            GeneratorHash, "sha256:" + GeneratorHash.ToLowerInvariant(), false));

        const string g3aAuditRelative = "artifacts/local/public-data/admin-dong-crosswalk-northeast-seoul-20260915-g3a-r2/generations/78312e5f0ddb89bffa9ad1881379cbf7e8037a6c6454d4f0abdf2a3e4d22d30a/audit.json";
        const string g3aAuditHash = "08F59331BDD0845E8A1333F5DCF6D3492FB6D0C37779CFAB42CFEB9954C81F25";
        var g3aAudit = ResolveRepositoryPath(bundle.Root, g3aAuditRelative);
        inputs.Add(NewRegistration(DatasetId + "-support-g3aaudit", g3aAudit, g3aAuditRelative,
            g3aAuditHash, "sha256:" + g3aAuditHash.ToLowerInvariant(), false));

        foreach (var artifact in new[]
        {
            (Suffix: "manifest", Path: bundle.ManifestPath, Hash: ManifestHash),
            (Suffix: "candidates", Path: bundle.CandidatesPath, Hash: CandidatesHash),
            (Suffix: "audit", Path: bundle.AuditPath, Hash: AuditHash),
            (Suffix: "generator-source", Path: bundle.GeneratorSourcePath, Hash: GeneratorSourceHash),
            (Suffix: "complete", Path: bundle.CompletePath, Hash: CompleteHash)
        })
        {
            inputs.Add(NewRegistration(DatasetId + "-artifact-" + artifact.Suffix, artifact.Path,
                GenerationRelative + "/" + Path.GetFileName(artifact.Path), artifact.Hash,
                Revision + ";candidate-set:" + CandidateSetHash.ToLowerInvariant(), false));
        }
        Require(inputs.Count == 19
                && inputs.Count(item => item.CanonicalSource) == 1
                && inputs.Select(item => item.DatasetId).Distinct(StringComparer.Ordinal).Count() == inputs.Count
                && inputs.All(item => item.DatasetId.Length <= 160 && item.SourceVersion.Length <= 200),
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
    {
        Require(File.Exists(path) && !IsReparsePoint(path) && HashFile(path) == hash,
            "RegistrationFileChanged:" + datasetId);
        return new RegistrationInput(datasetId, path, relativePath, hash, new FileInfo(path).Length,
            sourceVersion, ContentType(path), canonicalSource);
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
        run.FetchedCount = input.CanonicalSource ? ExpectedSourceRows : 1;
        run.NormalizedCount = input.CanonicalSource ? ExpectedCandidateRows : 0;
        run.RejectedCount = input.CanonicalSource ? ExpectedOutsideRows : 0;
        run.InsertedCount = input.CanonicalSource ? ExpectedCandidateRows : 1;
        run.UpdatedCount = 0;
        run.ExistingCount = 0;
        run.SourceVersion = input.SourceVersion;
        run.DataRevision = Revision;
        run.ErrorCode = Quality;
        run.ErrorSummary = ExpectedRunErrorSummary(input);
        Require(run.RunKey.Length <= 80 && run.ErrorSummary.Length <= 500, "SourceRunFieldBudgetExceeded");
        await db.SaveChangesAsync();
    }

    private static async Task ValidateSourceSnapshotPreflightAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle)
    {
        foreach (var input in BuildRegistrationInputs(bundle))
        {
            var snapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
                item.SourceId == SourceId && item.DatasetId == input.DatasetId).ToListAsync();
            Require(snapshots.Count <= 1, "SourceSnapshotPreflightDuplicate");
            if (snapshots.Count == 1)
                await RequireSourceSnapshotAsync(db, input);
            else
                Require(!await db.IngestionRuns.AsNoTracking().AnyAsync(item => item.RunKey == ExpectedRunKey(input)),
                    "SourceRunWithoutSnapshot");
        }
    }

    private static async Task<bool> AreSourceSnapshotsCompleteAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle)
    {
        foreach (var input in BuildRegistrationInputs(bundle))
        {
            if (!await db.RawSnapshots.AsNoTracking().AnyAsync(item =>
                    item.SourceId == SourceId && item.DatasetId == input.DatasetId)) return false;
        }
        return true;
    }

    private static async Task RequireSourceSnapshotAsync(PublicDataIngestionDbContext db, RegistrationInput input)
    {
        var snapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            item.SourceId == SourceId && item.DatasetId == input.DatasetId).ToListAsync();
        Require(snapshots.Count == 1 && SourceSnapshotMatches(snapshots[0], input),
            "SourceSnapshotMissingOrChanged");
        var run = await db.IngestionRuns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == snapshots[0].FirstCollectionRunId);
        Require(run is not null && SourceRunMatches(run, input), "SourceRunMissingOrChanged");
    }

    private static bool SourceSnapshotMatches(외부데이터RawSnapshot value, RegistrationInput input)
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
           && value.FetchedCount == (input.CanonicalSource ? ExpectedSourceRows : 1)
           && value.NormalizedCount == (input.CanonicalSource ? ExpectedCandidateRows : 0)
           && value.RejectedCount == (input.CanonicalSource ? ExpectedOutsideRows : 0)
           && value.InsertedCount == (input.CanonicalSource ? ExpectedCandidateRows : 1)
           && value.UpdatedCount == 0
           && value.ExistingCount == 0
           && value.SourceVersion == input.SourceVersion
           && value.DataRevision == Revision
           && value.ErrorCode == Quality
           && value.ErrorSummary == ExpectedRunErrorSummary(input);

    private static string ExpectedRunErrorSummary(RegistrationInput input)
        => input.CanonicalSource
            ? "Frozen OA-15534 intersection points assigned only by own source geometry against frozen 2023 historical HJD boundaries; private review only; no current topology, direction, controller, lane, signal, distribution, runtime, traversal, gameplay or Unity authority."
            : "Frozen source, scope, design, generator or lineage snapshot for the private G3b intersection-point candidate ledger; no public, current, runtime, traversal, gameplay or Unity authority.";

    private static string ExpectedRunKey(RegistrationInput input)
        => "public-data-candidate:g3b-r1:" + HashUtf8(input.DatasetId).ToLowerInvariant()[..24];

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

    private static Task<List<외부데이터정규화Record>> LoadScopedRowsAsync(PublicDataIngestionDbContext db)
        => db.NormalizedRecords.AsNoTracking()
            .Where(item => item.SourceId == SourceId && item.DatasetId == DatasetId)
            .ToListAsync();

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
                && raw.ContentHashSha256.Equals(Oa15534Hash, StringComparison.OrdinalIgnoreCase),
            "StoredOriginalSnapshotChanged");
        if (requireComplete)
            foreach (var input in BuildRegistrationInputs(bundle)) await RequireSourceSnapshotAsync(db, input);
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

    private static async Task<PreservationState> ReadPreservationStateAsync(
        DbContextOptions<PublicDataIngestionDbContext> options)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        return await ReadPreservationStateAsync(db);
    }

    private static async Task<PreservationState> ReadPreservationStateAsync(PublicDataIngestionDbContext db)
    {
        LedgerStateSummary g3a;
        LedgerStateSummary g4a;
        LegacyStateSummary legacy;
        try
        {
            g3a = await ReadCandidateLedgerStateAsync(db,
                "seoul-open-data-admin-dong-crosswalk-private-review",
                "northeast-seoul-admin-dong-crosswalk-candidate-g3a-r2",
                "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r2",
                ExpectedG3aRows, ExpectedG3aSnapshots, "g3a-crosswalk-r2-full-persisted-state.v1");
        }
        catch (Exception error) when (error is not InvalidDataException)
        {
            throw new InvalidDataException("AdministrativeDongIntersectionCandidate:G3aPreservationReadFailed", error);
        }
        try
        {
            g4a = await ReadCandidateLedgerStateAsync(db,
                "semas-admin-dong-business-private-review",
                "northeast-seoul-admin-dong-business-candidate-g4a-r1",
                "northeast-seoul-admin-dong-business-candidate.g4a.r1",
                ExpectedG4aRows, ExpectedG4aSnapshots, "g4a-business-r1-full-persisted-state.v1");
        }
        catch (Exception error) when (error is not InvalidDataException)
        {
            throw new InvalidDataException("AdministrativeDongIntersectionCandidate:G4aPreservationReadFailed", error);
        }
        try
        {
            legacy = await ReadLegacyMyeonmokStateAsync(db);
        }
        catch (Exception error) when (error is not InvalidDataException)
        {
            throw new InvalidDataException("AdministrativeDongIntersectionCandidate:LegacyPreservationReadFailed", error);
        }
        return new PreservationState(g3a, g4a, legacy);
    }

    private static async Task<LedgerStateSummary> ReadCandidateLedgerStateAsync(
        PublicDataIngestionDbContext db,
        string sourceId,
        string datasetId,
        string revision,
        int expectedRows,
        int expectedSnapshots,
        string domain)
    {
        List<외부데이터정규화Record> rows;
        try
        {
            rows = await db.NormalizedRecords.AsNoTracking().Where(item =>
                item.SourceId == sourceId && item.DatasetId == datasetId).ToListAsync();
        }
        catch (Exception error)
        {
            throw new InvalidDataException("AdministrativeDongIntersectionCandidate:PreservedRowsQueryFailed:" + domain, error);
        }
        Require(rows.Count == expectedRows && rows.All(item => item.DataRevision == revision),
            "PreservedCandidateLedgerRowSetChanged:" + domain);
        List<외부데이터RawSnapshot> snapshots;
        try
        {
            snapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
                item.SourceId == sourceId
                && (item.DatasetId == datasetId || item.DatasetId.StartsWith(datasetId + "-"))).ToListAsync();
        }
        catch (Exception error)
        {
            throw new InvalidDataException("AdministrativeDongIntersectionCandidate:PreservedSnapshotsQueryFailed:" + domain, error);
        }
        Require(snapshots.Count == expectedSnapshots
                && rows.Select(item => item.RawSnapshotId).Distinct().All(id => snapshots.Any(item => item.Id == id)),
            "PreservedCandidateLedgerSnapshotSetChanged:" + domain);
        var runIds = snapshots.Select(item => item.FirstCollectionRunId).Distinct().ToList();
        List<외부데이터수집Run> runs;
        try
        {
            runs = await db.IngestionRuns.AsNoTracking().Where(item => runIds.Contains(item.Id)).ToListAsync();
        }
        catch (Exception error)
        {
            throw new InvalidDataException("AdministrativeDongIntersectionCandidate:PreservedRunsQueryFailed:" + domain, error);
        }
        Require(runs.Count == runIds.Count, "PreservedCandidateLedgerRunSetChanged:" + domain);
        return new LedgerStateSummary(rows.Count, snapshots.Count, runs.Count,
            ComputePersistedStateHash(domain, rows, snapshots, runs));
    }

    private static async Task<LegacyStateSummary> ReadLegacyMyeonmokStateAsync(PublicDataIngestionDbContext db)
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
        var shopRows = rows.Count(item => item.SourceId == shopSource && item.DatasetId == shopDataset);
        var factoryRows = rows.Count(item => item.SourceId == factorySource && item.DatasetId == factoryDataset);
        Require(shopRows == ExpectedLegacyShopRows && factoryRows == ExpectedLegacyFactoryRows,
            "LegacyMyeonmokLedgerCountChanged");
        var snapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            (item.SourceId == shopSource && item.DatasetId == shopDataset)
            || (item.SourceId == factorySource && item.DatasetId == factoryDataset)).ToListAsync();
        var referencedRawIds = rows.Select(item => item.RawSnapshotId).Distinct().ToHashSet();
        Require(snapshots.Count > 0 && snapshots.Select(item => item.Id).ToHashSet().SetEquals(referencedRawIds),
            "LegacyMyeonmokSnapshotSetChanged");
        var runIds = snapshots.Select(item => item.FirstCollectionRunId).Distinct().ToList();
        var runs = await db.IngestionRuns.AsNoTracking().Where(item => runIds.Contains(item.Id)).ToListAsync();
        Require(runs.Count == runIds.Count, "LegacyMyeonmokRunSetChanged");
        return new LegacyStateSummary(shopRows, factoryRows, snapshots.Count, runs.Count,
            ComputePersistedStateHash("myeonmok-business-ledger-full-persisted-state.v1", rows, snapshots, runs));
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
            AppendPersistedFramed(hash, Time(item.EvidenceAsOfUtc)); AppendPersistedFramed(hash, Time(item.CollectedAtUtc));
            AppendPersistedFramed(hash, item.SpatialPrecisionCode); AppendPersistedFramed(hash, item.TemporalPrecisionCode);
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

    private static void SetPreservationSummary(Dictionary<string, object?> result, PreservationState state)
    {
        result["g3aNormalizedRowsPreserved"] = state.G3a.NormalizedRows;
        result["g3aRawSnapshotsPreserved"] = state.G3a.RawSnapshots;
        result["g3aStateHashSha256"] = state.G3a.StateHashSha256.ToLowerInvariant();
        result["g4aNormalizedRowsPreserved"] = state.G4a.NormalizedRows;
        result["g4aRawSnapshotsPreserved"] = state.G4a.RawSnapshots;
        result["g4aStateHashSha256"] = state.G4a.StateHashSha256.ToLowerInvariant();
        result["legacyMyeonmokShopRowsPreserved"] = state.LegacyMyeonmok.ShopRows;
        result["legacyMyeonmokFactoryRowsPreserved"] = state.LegacyMyeonmok.FactoryRows;
        result["legacyMyeonmokStateHashSha256"] = state.LegacyMyeonmok.StateHashSha256.ToLowerInvariant();
        result["preservedLedgersMutated"] = false;
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
        var expectedByStableId = rows.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        var candidatesByIdentity = bundle.Candidates.ToDictionary(
            item => CandidateIdentityHash(item), StringComparer.Ordinal);
        foreach (var row in stored)
        {
            Require(expectedByStableId.ContainsKey(row.StableId), "ReadbackUnexpectedStableId");
            ValidateStoredPayload(row, candidatesByIdentity);
        }
        var independentlyRecomputed = ComputeCandidateSetHash(bundle.HeaderValues, bundle.Candidates);
        Require(independentlyRecomputed == CandidateSetHash, "ReadbackCandidateSetHashMismatch");
        ValidateComputedCandidateSet(bundle.Candidates, bundle.ExpectedPerArea);
        result["verifiedRows"] = stored.Count;
        result["verifiedAdministrativeAreas"] = stored.Select(item => item.RegionStableId)
            .Distinct(StringComparer.Ordinal).Count();
        result["verifiedSourceDistrictSpatialAssignmentConflicts"] =
            bundle.Candidates.Count(item => item.SourceDistrictSpatialAssignmentConflict);
        result["verifiedCrosswalkLinkedCandidates"] =
            bundle.Candidates.Count(item => item.LinkedCrosswalks.Count > 0);
        result["verifiedCrosswalkUnlinkedCandidates"] =
            bundle.Candidates.Count(item => item.LinkedCrosswalks.Count == 0);
        result["verifiedLinkedCrosswalkRelations"] = bundle.Candidates.Sum(item => item.LinkedCrosswalks.Count);
        result["verifiedOtherHistoricalHjdCrosswalkRelations"] = bundle.Candidates.Sum(item =>
            item.LinkedCrosswalks.Count(link => !link.SameHistoricalAdministrativeArea));
        result["readbackCandidateSetHashSha256"] = independentlyRecomputed.ToLowerInvariant();
        result["verifiedRawSnapshots"] = BuildRegistrationInputs(bundle).Count;
        result["independentReadback"] = true;
        result["completionCode"] = CompletionUpperBound;
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
    }

    private static void ValidateStoredPayload(
        외부데이터정규화Record row,
        IReadOnlyDictionary<string, Candidate> candidatesByIdentity)
    {
        Require(!Regex.IsMatch(row.TextValue, "82-[0-9]{10}", RegexOptions.CultureInvariant),
            "RawSourceManagementNumberLeakedToNormalizedPayload");
        using var document = JsonDocument.Parse(row.TextValue);
        var value = document.RootElement;
        RequireExactProperties(value,
        [
            "schemaVersion", "candidateSetHashSha256", "candidateBodyHashSha256", "candidateIdentitySha256",
            "AdministrativeAreaStableId", "sourceHashSha256", "historicalBoundaryHashSha256",
            "crosswalkDiagnosticCandidateSetHashSha256", "assignmentStateCode", "candidateQualityCode",
            "SourceDistrictSpatialAssignmentConflict", "SourceCoordinateAttributeStateCode",
            "CrosswalkLinkStateCode", "linkedCrosswalkCount", "linkedCrosswalkSetHashSha256",
            "QualityDiagnosticCodes", "authorityFlags", "ledgerPersistenceCode"
        ], "StoredPayloadPropertySetChanged");
        var identity = Text(value, "candidateIdentitySha256");
        Require(candidatesByIdentity.TryGetValue(identity, out var candidate), "StoredPayloadIdentityUnknown");
        if (candidate is null)
            throw new InvalidDataException("AdministrativeDongIntersectionCandidate:StoredPayloadIdentityUnknown");
        Require(Text(value, "schemaVersion") == ProtectedPayloadSchemaVersion
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "candidateBodyHashSha256") == candidate.CandidateBodyHashSha256
                && Text(value, "AdministrativeAreaStableId") == candidate.AdministrativeAreaStableId
                && Text(value, "sourceHashSha256") == Oa15534Hash
                && Text(value, "historicalBoundaryHashSha256") == Oa22160Hash
                && Text(value, "crosswalkDiagnosticCandidateSetHashSha256") == G3aCandidateSetHash
                && Text(value, "assignmentStateCode") == "UniqueHistoricalBoundaryCover"
                && Text(value, "candidateQualityCode") == Quality
                && Bool(value, "SourceDistrictSpatialAssignmentConflict") ==
                    candidate.SourceDistrictSpatialAssignmentConflict
                && Text(value, "SourceCoordinateAttributeStateCode") == candidate.SourceCoordinateAttributeStateCode
                && Text(value, "CrosswalkLinkStateCode") == candidate.CrosswalkLinkStateCode
                && Int(value, "linkedCrosswalkCount") == candidate.LinkedCrosswalks.Count
                && Text(value, "linkedCrosswalkSetHashSha256") == candidate.LinkedCrosswalkSetHashSha256
                && Text(value, "ledgerPersistenceCode") == LedgerRecordCode,
            "StoredPayloadBodyChanged");
        var diagnostics = value.GetProperty("QualityDiagnosticCodes").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty).ToArray();
        Require(diagnostics.SequenceEqual(candidate.QualityDiagnosticCodes, StringComparer.Ordinal),
            "StoredPayloadQualityDiagnosticsChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "StoredPayload");
        Require(row.StableId == "administrative-dong-intersection-point-candidate:sha256:" +
                    identity.ToLowerInvariant()
                && row.DimensionKey == "intersection-point-candidate|sha256|" + identity,
            "StoredHashedIdentityChanged");
    }

    private static int SelfTest(Bundle bundle, IReadOnlyList<외부데이터정규화Record> rows)
    {
        var tests = 0;
        void Check(bool condition, string code)
        {
            Require(condition, "SelfTest:" + code);
            tests++;
        }

        Check(ComputeCandidateSetHash(bundle.HeaderValues, bundle.Candidates) == CandidateSetHash,
            "CandidateSetHash");
        Check(ComputeCandidateSetHash(bundle.HeaderValues, bundle.Candidates.Reverse()) != CandidateSetHash,
            "CandidateOrderSensitivity");
        Check(FrameDigest(["ab", "c"]) != FrameDigest(["a", "bc"]), "FramingBoundary");
        Check(bundle.Candidates.Count == ExpectedCandidateRows && rows.Count == ExpectedCandidateRows,
            "ExactRowCount");
        Check(bundle.ExpectedPerArea.Count == ExpectedAreaCount, "AdministrativeAreaCount");
        Check(bundle.Candidates.Count(item => item.LinkedCrosswalks.Count > 0) == ExpectedCrosswalkLinkedRows,
            "LinkedCandidateCount");
        Check(bundle.Candidates.Sum(item => item.LinkedCrosswalks.Count) == ExpectedLinkedRelations,
            "CrosswalkRelationCount");
        Check(bundle.Candidates.Sum(item => item.LinkedCrosswalks.Count(link =>
            !link.SameHistoricalAdministrativeArea)) == ExpectedOtherAreaRelations, "OtherHjdRelationCount");
        Check(bundle.Candidates.Count(item => item.SourceDistrictSpatialAssignmentConflict) == ExpectedConflictRows,
            "DistrictConflictCount");
        Check(rows.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == rows.Count
              && rows.Select(item => item.DimensionKey).Distinct(StringComparer.Ordinal).Count() == rows.Count,
            "HashedIdentityUniqueness");
        Check(rows.All(item => item.TextValue.Length <= 2_000 && item.DimensionKey.Length <= 500),
            "DatabaseFieldBudgets");
        Check(rows.All(item => !Regex.IsMatch(item.TextValue, "82-[0-9]{10}", RegexOptions.CultureInvariant)),
            "NormalizedPayloadDoesNotExposeManagementNumber");
        Check(BuildRegistrationInputs(bundle).Count == 19, "LineageSnapshotCount");
        Check(rows.Chunk(500).All(chunk => chunk.Length <= 500) && rows.Chunk(500).Count() == 2,
            "ChunkBoundary");
        var first = rows[0];
        var conflicting = CloneRow(first);
        conflicting.TextValue += " ";
        Check(!Equivalent(first, conflicting), "ExactBodyMismatchDetection");
        var before = ComputePersistedStateHash("self-test", [first], [], []);
        var originalText = first.TextValue;
        first.TextValue += " ";
        var after = ComputePersistedStateHash("self-test", [first], [], []);
        first.TextValue = originalText;
        Check(before != after, "PersistedStateMutationDetection");
        return tests;
    }

    private static 외부데이터정규화Record CloneRow(외부데이터정규화Record value)
        => new()
        {
            Id = value.Id, RawSnapshotId = value.RawSnapshotId, RecordKey = value.RecordKey,
            StableId = value.StableId, SourceId = value.SourceId, DatasetId = value.DatasetId,
            RegionStableId = value.RegionStableId, MetricCode = value.MetricCode,
            NumericValue = value.NumericValue, TextValue = value.TextValue, UnitCode = value.UnitCode,
            EvidenceAsOfUtc = value.EvidenceAsOfUtc, CollectedAtUtc = value.CollectedAtUtc,
            SpatialPrecisionCode = value.SpatialPrecisionCode, TemporalPrecisionCode = value.TemporalPrecisionCode,
            QualityCode = value.QualityCode, LimitationCode = value.LimitationCode,
            DimensionKey = value.DimensionKey, SourceVersion = value.SourceVersion,
            DataRevision = value.DataRevision, FirstSeenAtUtc = value.FirstSeenAtUtc,
            LastSeenAtUtc = value.LastSeenAtUtc
        };

    private static void SetSummary(Dictionary<string, object?> result, string mode, Bundle bundle)
    {
        result["mode"] = mode;
        result["sourceId"] = SourceId;
        result["datasetId"] = DatasetId;
        result["revision"] = Revision;
        result["completionUpperBoundCode"] = CompletionUpperBound;
        result["candidateRows"] = bundle.Candidates.Count;
        result["administrativeAreas"] = bundle.ExpectedPerArea.Count;
        result["sourceRows"] = ExpectedSourceRows;
        result["outsideScopeRows"] = ExpectedOutsideRows;
        result["ambiguousRows"] = ExpectedAmbiguousRows;
        result["sourceDistrictNormalRows"] = ExpectedNormalRows;
        result["sourceDistrictConflictRows"] = ExpectedConflictRows;
        result["crosswalkLinkedCandidates"] = ExpectedCrosswalkLinkedRows;
        result["crosswalkUnlinkedCandidates"] = ExpectedCrosswalkUnlinkedRows;
        result["linkedCrosswalkRelations"] = ExpectedLinkedRelations;
        result["sameHistoricalHjdCrosswalkRelations"] = ExpectedSameAreaRelations;
        result["otherHistoricalHjdCrosswalkRelations"] = ExpectedOtherAreaRelations;
        result["candidateSetHashSha256"] = CandidateSetHash.ToLowerInvariant();
        result["scopeDefinitionHashSha256"] = ScopeDefinitionHash.ToLowerInvariant();
        result["generatorHashSha256"] = GeneratorHash.ToLowerInvariant();
        result["lineageSnapshots"] = 19;
        result["privateReviewOnly"] = true;
        result["sourceDeclaredCoordinateReference"] = true;
        result["currentAdministrativeBoundaryEstablished"] = false;
        result["currentIntersectionTopologyEstablished"] = false;
        result["approachDirectionEstablished"] = false;
        result["controllerBindingEstablished"] = false;
        result["laneBindingEstablished"] = false;
        result["signalBindingEstablished"] = false;
        result["crosswalkAssignmentInherited"] = false;
        result["distributionApproved"] = false;
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
        foreach (var name in TrueAuthorityFlags) Require(Bool(value, name), source + "AuthorityTrueFlagChanged:" + name);
        foreach (var name in FalseAuthorityFlags) Require(!Bool(value, name), source + "AuthorityFalseFlagChanged:" + name);
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

    // G4a가 기존 면목 원장에 사용하는 null 표시 포함 5-byte framing을 동일하게 보존한다.
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
            _ => throw new InvalidDataException("AdministrativeDongIntersectionCandidate:UnsupportedPersistedHashValue")
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
                throw new InvalidDataException("AdministrativeDongIntersectionCandidate:JsonValueKindInvalid");
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
        var path = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        Require(path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "PathOutsideRepository");
        Require(!IsReparsePoint(normalizedRoot) && !IsReparsePoint(path), "RepositoryPathReparsePointRejected");
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

    private static string ContentType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".md" => "text/markdown",
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

    private static string HashUtf8(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Text(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? string.Empty
            : throw new InvalidDataException("AdministrativeDongIntersectionCandidate:JsonTextContract:" + property);

    private static int Int(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongIntersectionCandidate:JsonIntegerContract:" + property);

    private static long Long(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt64(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongIntersectionCandidate:JsonLongContract:" + property);

    private static double Double(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetDouble(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongIntersectionCandidate:JsonDoubleContract:" + property);

    private static bool Bool(JsonElement value, string property)
        => value.TryGetProperty(property, out var item)
           && item.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? item.GetBoolean()
            : throw new InvalidDataException("AdministrativeDongIntersectionCandidate:JsonBooleanContract:" + property);

    private static void RequireExactProperties(JsonElement value, IEnumerable<string> expected, string code)
    {
        var names = expected.ToArray();
        Require(value.ValueKind == JsonValueKind.Object
                && value.EnumerateObject().Count() == names.Length
                && value.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal).SetEquals(names),
            code);
    }

    private static bool DictionaryEqual<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> first,
        IReadOnlyDictionary<TKey, TValue> second)
        where TKey : notnull
        => first.Count == second.Count && first.All(item => second.TryGetValue(item.Key, out var value)
            && EqualityComparer<TValue>.Default.Equals(item.Value, value));

    private static string Time(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException("AdministrativeDongIntersectionCandidate:" + code);
    }
}
