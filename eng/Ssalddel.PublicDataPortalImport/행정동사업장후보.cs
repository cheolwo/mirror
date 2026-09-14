using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 동결 SEMAS 관측을 30개 행정동의 비공개 검토 후보로만 저장한다.
// 정확 상호·주소·공급자 ID·원좌표는 local-private 입력과 보호 RDB payload 밖으로 내보내지 않는다.
internal static class 행정동사업장후보
{
    private const string SourceId = "semas-admin-dong-business-private-review";
    private const string DatasetId = "northeast-seoul-admin-dong-business-candidate-g4a-r2";
    private const string RejectedR1DatasetId = "northeast-seoul-admin-dong-business-candidate-g4a-r1";
    private const string OriginalInputDatasetId = DatasetId + "-semas-archive-input";
    private const string MetricCode = "administrative-dong-business-candidate";
    private const string CandidateSchemaVersion = "administrative-dong-business-candidate.v1";
    private const string ProtectedPayloadSchemaVersion = "administrative-dong-business-protected-rdb-payload.v1";
    private const string Revision = "northeast-seoul-admin-dong-business-candidate.g4a.r2";
    private const string DesignRevision = "administrative-dong-diorama:business-candidate.g4a.r2";
    private const string DesignHash = "F4DC06DF8FC300F564B646EDD4295D4EC5B941D143046CAA0C1D1422A2CEEDA4";
    private const string DesignRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-business-candidate.implementation.r12.md";
    private const string WorkOrderRelative = "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-business-candidate.r2.data-implementation.v1.json";
    private const string ScopeRelative = "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-business.g4a.r2.json";
    private const string GeneratorRelative = "eng/neighborhood/administrative_dong_business_candidate.py";
    private const string OutputRelative = "artifacts/local/public-data/admin-dong-business-northeast-seoul-20260915-g4a-r2";
    private const string ScopeDefinitionHash = "156DE74B56409EA9E0FBDBCEE509F09AB751D96974C400D7A7AC443D42343124";
    private const string GeneratorHash = "E0E2146B6D3A135A304BE9DA995582CF5624F83E515A929418D488F58945BA82";
    private const string CandidateSetHash = "5F9C61BD71112C69EE3988CFB3B8542AE22BF60AB95D542C702D2355B8A3774C";
    private const string ManifestHash = "19BF1D5C7C66569CD41345D48621EDD8F0783999177B73D5EB5239EBB04FCBC2";
    private const string CandidatesHash = "22C21AD047C9D3FCB151D72878027691BBAE3615C6490B7C3A07A007CD8AAAB3";
    private const string AuditHash = "133E4B9F38FCACFF7DB97BF51E4606693F63DF487A0BEF722E7952453D45A1E5";
    private const string GeneratorSourceHash = "E0E2146B6D3A135A304BE9DA995582CF5624F83E515A929418D488F58945BA82";
    private const string CompleteHash = "450C17FE1EAD6AF012DF88C7DBAA16AB8321A3379A7A76988D84FA820F0639D4";
    private const string GenerationRelative = OutputRelative + "/generations/" +
        "5f9c61bd71112c69ee3988cfb3b8542ae22bf60ab95d542c702d2355b8a3774c";
    private const string Quality = "PendingHumanReview";
    private const string CoordinateStatus = "SourceCoordinateDatumUnconfirmed";
    private const string BoundaryMatched = "SourceAdministrativeDongMappedHistoricalBoundaryMatched";
    private const string BoundaryConflict = "SourceAdministrativeDongMappedHistoricalBoundaryConflict";
    private const string BoundaryOutside = "SourceAdministrativeDongMappedOutsideHistoricalBoundaryScope";
    private const string Limitation = "PrivateReviewOnly;SourceCoordinateDatumUnconfirmed;HistoricalBoundaryDiagnosticOnly;NoCurrentOperation;NoBuildingBinding;NoClaim;NoDistribution;NoOrder;NoRuntime;NoGameplay;NoUnity";
    private const string LockName = "mirror:public-data:admin-dong-business-g4a-r2";
    private const string RejectedR1LockName = "mirror:public-data:admin-dong-business-g4a-r1";
    private const string LegacyLockName = "mirror:public-data:myeonmok-business";
    private const int ExpectedCandidateRows = 29_721;
    private const int ExpectedFoodRows = 8_246;
    private const int ExpectedMissingBuildingRows = 67;
    private const int ExpectedBoundaryConflictRows = 29;
    private const int ExpectedBoundaryOutsideRows = 6;
    private const int ExpectedBoundaryDiagnosticRows = 35;
    private const int ExpectedIdentityGroups = 236;
    private const int ExpectedIdentityRows = 490;
    private const int ExpectedLegacyShopRows = 5_411;
    private const int ExpectedLegacyFactoryRows = 126;
    private const int ExpectedLegacyOverlapRows = 5_411;
    private const int ExpectedNewProviderRows = 24_310;
    private const int ExpectedAreaCount = 30;
    private static readonly DateTimeOffset EvidenceAsOfUtc = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DerivedAtUtc = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CollectedAtUtc = DbTime(DateTimeOffset.Parse(
        "2026-09-08T10:31:09.6612043Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));

    private static readonly Regex ProviderIdPattern = new("^MA[0-9A-Z]{18}$", RegexOptions.CultureInvariant);
    private static readonly Regex BuildingManagementPattern = new("^[0-9]{25}$", RegexOptions.CultureInvariant);
    private static readonly Regex ProviderIdLeakPattern = new("MA[0-9A-Z]{18}", RegexOptions.CultureInvariant);

    private static readonly string[] HeaderHashFieldOrder =
    [
        "domainSeparator", "schemaVersion", "revision", "designDocumentRef", "designHashSha256",
        "scopeRelativePath", "scopeDefinitionSha256", "generatorRelativePath", "generatorSha256",
        "semasArchiveRelativePath", "semasArchiveSha256", "semasArchiveBytes",
        "seoulEntryLogicalName", "seoulEntryLogicalNameUtf8Sha256", "seoulEntryRawNameEncoding",
        "seoulEntryRawNameBytes", "seoulEntryRawNameBase64", "seoulEntryRawNameSha256",
        "seoulEntryBytes", "seoulEntryCrc32", "seoulEntrySha256", "seoulEntryRows",
        "receiptRelativePath", "receiptSha256", "receiptBytes",
        "downloadMetadataRelativePath", "downloadMetadataSha256", "downloadMetadataBytes",
        "datasetMetadataRelativePath", "datasetMetadataSha256", "datasetMetadataBytes",
        "moisArchiveRelativePath", "moisArchiveSha256", "moisArchiveBytes",
        "moisAdministrativeEntryName", "moisAdministrativeEntrySha256", "moisAdministrativeEntryBytes",
        "moisAdministrativeEntryCrc32", "moisAdministrativeLegalEntryName",
        "moisAdministrativeLegalEntrySha256", "moisAdministrativeLegalEntryBytes",
        "moisAdministrativeLegalEntryCrc32", "r2ScopeRelativePath", "r2ScopeSha256", "r2ScopeBytes",
        "r2ScopeManifestRelativePath", "r2ScopeManifestFileSha256", "r2ScopeManifestBytes",
        "r2ScopeManifestContentHashSha256", "historicalBoundaryRelativePath", "historicalBoundarySha256",
        "historicalBoundaryBytes", "legacySelectedRelativePath", "legacySelectedSha256", "legacySelectedBytes",
        "legacyFactoryRelativePath", "legacyFactorySha256", "legacyFactoryBytes", "mappingContractSha256",
        "sourceRowContractSha256", "historicalBoundaryContractSha256", "coordinateContractSha256",
        "identityContractSha256", "expectedCountsContractSha256"
    ];

    private static readonly string[] CandidateHashFieldOrder =
    [
        "schemaVersion", "revision", "stableId", "sourceFeatureKey", "dimensionKey",
        "providerBusinessIdSha256", "protectedSource.providerShopIdCanonical", "sourceRowHashSha256",
        "sourceProvinceCode", "sourceBoroughCode", "sourceBoroughName", "sourceAdministrativeDongCode",
        "mappedAdministrativeDongCode", "administrativeAreaStableId",
        "protectedSource.sourceAdministrativeDongName", "sourceLegalDongCode", "legalAreaStableId",
        "protectedSource.sourceLegalDongName", "largeCategoryCode", "largeCategoryName",
        "middleCategoryCode", "middleCategoryName", "smallCategoryCode", "smallCategoryName",
        "standardIndustryCode", "standardIndustryName", "foodCategory", "protectedSource.businessName",
        "protectedSource.branchName", "protectedSource.landLotAddress", "protectedSource.roadNameAddress",
        "protectedSource.buildingManagementNumber", "protectedSource.buildingName",
        "protectedSource.buildingDong", "protectedSource.floor", "protectedSource.unit",
        "protectedSource.sourceLongitudeText", "protectedSource.sourceLatitudeText",
        "sourceCoordinateDatumStatusCode", "commonEnuMillimeters.x", "commonEnuMillimeters.z",
        "historicalBoundaryHitAdministrativeAreaStableIds[]", "historicalBoundaryDiagnosticCode",
        "historicalBoundaryOwnershipDispositionCode", "identityCandidateGroupSha256",
        "identityCandidateGroupSize", "identityMergeState", "qualityCode", "qualityDiagnosticCodes[]",
        "authorityFlags.privateReviewOnly", "authorityFlags.currentAdministrativeBoundaryEstablished",
        "authorityFlags.sourceCoordinateDatumEstablished", "authorityFlags.exactBuildingBindingEstablished",
        "authorityFlags.currentOperationVerified", "authorityFlags.merchantClaimVerified",
        "authorityFlags.distributionApproved", "authorityFlags.publicDisplayAllowed",
        "authorityFlags.orderScenarioEligible", "authorityFlags.runtimeAuthorized",
        "authorityFlags.gameplayReady", "authorityFlags.unityApplyAllowed"
    ];

    private static readonly string[] CandidatePropertyOrder =
    [
        "schemaVersion", "revision", "candidateSetHashSha256", "stableId", "sourceFeatureKey",
        "dimensionKey", "providerBusinessIdSha256", "sourceRowHashSha256", "sourceProvinceCode",
        "sourceBoroughCode", "sourceBoroughName", "sourceAdministrativeDongCode",
        "mappedAdministrativeDongCode", "administrativeAreaStableId", "sourceLegalDongCode",
        "legalAreaStableId", "largeCategoryCode", "largeCategoryName", "middleCategoryCode",
        "middleCategoryName", "smallCategoryCode", "smallCategoryName", "standardIndustryCode",
        "standardIndustryName", "foodCategory", "sourceCoordinateDatumStatusCode", "commonEnuMillimeters",
        "historicalBoundaryHitAdministrativeAreaStableIds", "historicalBoundaryDiagnosticCode",
        "historicalBoundaryOwnershipDispositionCode", "identityCandidateGroupSha256",
        "identityCandidateGroupSize", "identityMergeState", "qualityCode", "qualityDiagnosticCodes",
        "authorityFlags", "protectedSource"
    ];

    private static readonly string[] ProtectedSourcePropertyOrder =
    [
        "providerShopIdCanonical", "sourceAdministrativeDongName", "sourceLegalDongName", "businessName",
        "branchName", "landLotAddress", "roadNameAddress", "buildingManagementNumber", "buildingName",
        "buildingDong", "floor", "unit", "sourceLongitudeText", "sourceLatitudeText"
    ];

    private static readonly string[] AuthorityPropertyOrder =
    [
        "privateReviewOnly", "currentAdministrativeBoundaryEstablished", "sourceCoordinateDatumEstablished",
        "exactBuildingBindingEstablished", "currentOperationVerified", "merchantClaimVerified",
        "distributionApproved", "publicDisplayAllowed", "orderScenarioEligible", "runtimeAuthorized",
        "gameplayReady", "unityApplyAllowed"
    ];

    private static readonly HashSet<string> AggregateBannedKeys = new(StringComparer.Ordinal)
    {
        "protectedSource", "providerShopIdCanonical", "businessName", "branchName", "landLotAddress",
        "roadNameAddress", "buildingManagementNumber", "buildingName", "buildingDong", "floor", "unit",
        "sourceLongitudeText", "sourceLatitudeText"
    };

    private static readonly IReadOnlyDictionary<string, (int CandidateRows, int FoodRows)> ExpectedDistribution =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["1121574000"]=(1196,316), ["1121575000"]=(1190,251), ["1121576000"]=(785,225), ["1121577000"]=(921,241),
            ["1123056000"]=(1706,462), ["1123057000"]=(517,138), ["1123060000"]=(929,207), ["1123061000"]=(1012,211),
            ["1123065000"]=(2153,626), ["1123066000"]=(1688,517), ["1123072000"]=(625,170), ["1123073000"]=(544,155),
            ["1123074000"]=(995,349), ["1123075000"]=(408,96), ["1126052000"]=(971,287), ["1126054000"]=(526,123),
            ["1126055000"]=(362,92), ["1126056500"]=(1440,408), ["1126057000"]=(866,218), ["1126057500"]=(1246,406),
            ["1126058000"]=(648,176), ["1126059000"]=(1578,586), ["1126060000"]=(517,91), ["1126061000"]=(1155,347),
            ["1126062000"]=(929,258), ["1126063000"]=(923,253), ["1126065500"]=(1755,538), ["1126066000"]=(507,107),
            ["1126068000"]=(1039,279), ["1126069000"]=(590,113)
        };

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private sealed record FrozenSource(string Role, string Path, string RelativePath, string Hash, long Length);
    private sealed record RegistrationInput(string DatasetId, string Path, string RelativePath, string Hash,
        long Length, string OriginalFileName, string SourceVersion, string ContentType,
        bool CanonicalSource, bool VirtualArchiveEntry);
    private sealed record Candidate(
        string ProviderShopIdCanonical,
        string StableId,
        string SourceFeatureKey,
        string DimensionKey,
        string ProviderBusinessIdSha256,
        string SourceRowHashSha256,
        string AdministrativeAreaStableId,
        bool FoodCategory,
        string BuildingManagementNumber,
        string HistoricalBoundaryDiagnosticCode,
        string? IdentityCandidateGroupSha256,
        long? IdentityCandidateGroupSize,
        string[] QualityDiagnosticCodes,
        IReadOnlyDictionary<string, string> ProtectedSource,
        object?[] HashValues);
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
        object?[] HeaderHashValues,
        IReadOnlyList<Candidate> Candidates);
    private sealed record LegacySummary(
        int ShopRows,
        int FactoryRows,
        int RawSnapshots,
        int IngestionRuns,
        string StateHashSha256);
    private sealed record RejectedR1Summary(
        int CandidateRows,
        int RawSnapshots,
        int IngestionRuns,
        string StateHashSha256);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "ModeInvalid");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
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
        var legacyBefore = await ReadLegacySummaryAsync(options, bundle.Candidates);
        var rejectedR1Before = await ReadRejectedR1SummaryAsync(options);
        SetLegacySummary(result, legacyBefore);
        SetRejectedR1Summary(result, rejectedR1Before);

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
            await ApplyAsync(options, bundle, rows, legacyBefore, rejectedR1Before, result);
        else
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
        }

        await VerifyReadbackAsync(options, bundle, rows, result);
        var legacyAfter = await ReadLegacySummaryAsync(options, bundle.Candidates);
        var rejectedR1After = await ReadRejectedR1SummaryAsync(options);
        Require(legacyAfter == legacyBefore, "LegacyMyeonmokLedgerChanged");
        Require(rejectedR1After == rejectedR1Before, "RejectedR1LedgerChanged");
        SetLegacySummary(result, legacyAfter);
        SetRejectedR1Summary(result, rejectedR1After);
        result["status"] = "PASS";
    }

    private static Bundle LoadAndValidateBundle(string root)
    {
        RequireFrozenConstants();
        var designPath = ResolveRepositoryPath(root, DesignRelative);
        var workOrderPath = ResolveRepositoryPath(root, WorkOrderRelative);
        Require(HashFile(designPath) == DesignHash, "DesignHashChanged");
        using (var workOrder = ReadJson(workOrderPath, 256_000))
        {
            var value = workOrder.RootElement;
            Require(Text(value, "schemaVersion") == "public-data-candidate-implementation-work-order.v1"
                    && Text(value, "workOrderId") == "DATA-WO-NORTHEAST-SEOUL-ADMIN-DONG-BUSINESS-CANDIDATE-R2"
                    && Text(value, "workOrderKind") == "FrozenPublicDataCandidateLedgerCorrection"
                    && !Bool(value, "playableLoopWorkOrder"),
                "WorkOrderIdentityChanged");
            var gate = value.GetProperty("planningGate");
            Require(Text(gate, "statusCode") == "Approved"
                    && Text(gate, "designHashSha256") == DesignHash
                    && Text(gate, "designRevision") == DesignRevision
                    && Text(gate, "designDocumentRef") == DesignRelative,
                "WorkOrderPlanningGateChanged");
            Require(!Bool(value, "promotionEligible"), "WorkOrderPromotionBoundaryChanged");
        }

        var scopePath = ResolveRepositoryPath(root, ScopeRelative);
        var generatorPath = ResolveRepositoryPath(root, GeneratorRelative);
        Require(HashFile(scopePath) == ScopeDefinitionHash, "ScopeDefinitionHashChanged");
        Require(HashFile(generatorPath) == GeneratorHash, "GeneratorHashChanged");
        using var scopeDocument = ReadJson(scopePath, 1_000_000);
        var scope = scopeDocument.RootElement;
        var sources = ValidateScope(root, scope, scopePath, generatorPath);
        var headerValues = BuildHeaderHashValues(scope, scopePath, generatorPath, root);

        var generationPath = ResolveRepositoryPath(root, GenerationRelative);
        Require(Directory.Exists(generationPath) && !IsReparsePoint(generationPath), "GenerationMissingOrUnsafe");
        var expectedFiles = new HashSet<string>(
            ["manifest.json", "candidates.ndjson", "audit.json", "generator-source.py", "complete.json"],
            StringComparer.Ordinal);
        Require(Directory.EnumerateFiles(generationPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal).SetEquals(expectedFiles)
                && !Directory.EnumerateDirectories(generationPath).Any(),
            "GenerationFileSetChanged");

        var manifestPath = Path.Combine(generationPath, "manifest.json");
        var candidatesPath = Path.Combine(generationPath, "candidates.ndjson");
        var auditPath = Path.Combine(generationPath, "audit.json");
        var generatorSourcePath = Path.Combine(generationPath, "generator-source.py");
        var completePath = Path.Combine(generationPath, "complete.json");
        Require(HashFile(manifestPath) == ManifestHash, "ManifestFileHashChanged");
        Require(HashFile(candidatesPath) == CandidatesHash, "CandidateFileHashChanged");
        Require(HashFile(auditPath) == AuditHash, "AuditFileHashChanged");
        Require(HashFile(generatorSourcePath) == GeneratorSourceHash
                && GeneratorSourceHash == GeneratorHash
                && FilesEqual(generatorPath, generatorSourcePath),
            "GeneratorSourceSnapshotChanged");
        Require(HashFile(completePath) == CompleteHash, "CompleteFileHashChanged");

        using var manifestDocument = ReadJson(manifestPath, 2_000_000);
        using var auditDocument = ReadJson(auditPath, 2_000_000);
        using var completeDocument = ReadJson(completePath, 256_000);
        ValidateManifest(manifestDocument.RootElement);
        ValidateAudit(auditDocument.RootElement);
        ValidateComplete(completeDocument.RootElement, generationPath);
        ValidateAggregateDocument(manifestDocument.RootElement);
        ValidateAggregateDocument(auditDocument.RootElement);
        ValidateAggregateDocument(completeDocument.RootElement);

        var candidates = ReadCandidates(candidatesPath);
        Require(ComputeCandidateSetHash(headerValues, candidates) == CandidateSetHash,
            "CandidateSetHashMismatch");
        ValidateComputedCounts(candidates);
        ValidatePerArea(manifestDocument.RootElement.GetProperty("perAdministrativeArea"), candidates,
            "ManifestPerAreaChanged");
        ValidatePerArea(auditDocument.RootElement.GetProperty("perAdministrativeArea"), candidates,
            "AuditPerAreaChanged");
        return new Bundle(root, scopePath, generatorPath, generationPath, manifestPath, candidatesPath,
            auditPath, generatorSourcePath, completePath, sources, headerValues, candidates);
    }

    private static IReadOnlyDictionary<string, FrozenSource> ValidateScope(
        string root,
        JsonElement scope,
        string scopePath,
        string generatorPath)
    {
        Require(Text(scope, "schemaVersion") == "administrative-dong-business-generation-scope.v1"
                && Text(scope, "revision") == Revision
                && Text(scope, "designDocumentRef") == DesignRelative
                && Text(scope, "designHashSha256") == DesignHash
                && Text(scope, "reviewStatus") == Quality
                && Text(scope, "sourceCoordinateDatumStatusCode") == CoordinateStatus,
            "ScopeIdentityChanged");
        ValidateAuthority(scope.GetProperty("authorityFlags"), "ScopeAuthorityChanged");
        ValidateCounts(scope.GetProperty("expectedCounts"), "ScopeExpectedCountsChanged");
        var expectedDistribution = scope.GetProperty("expectedDistribution");
        Require(expectedDistribution.ValueKind == JsonValueKind.Object
                && expectedDistribution.EnumerateObject().Count() == ExpectedAreaCount,
            "ScopeDistributionShapeChanged");
        foreach (var pair in ExpectedDistribution)
        {
            Require(expectedDistribution.TryGetProperty(pair.Key, out var item)
                    && Int(item, "candidateRows") == pair.Value.CandidateRows
                    && Int(item, "foodRows") == pair.Value.FoodRows,
                "ScopeDistributionChanged");
        }

        var areas = scope.GetProperty("administrativeAreas").EnumerateArray().ToArray();
        Require(areas.Length == ExpectedAreaCount, "ScopeAreaCountChanged");
        foreach (var area in areas)
        {
            var mapped = Text(area, "mappedAdministrativeDongCode");
            Require(ExpectedDistribution.ContainsKey(mapped)
                    && Text(area, "administrativeAreaStableId") == "region:kr:hjd:" + mapped
                    && Int(area, "expectedCandidateRows") == ExpectedDistribution[mapped].CandidateRows
                    && Int(area, "expectedFoodRows") == ExpectedDistribution[mapped].FoodRows,
                "ScopeAreaChanged");
        }
        Require(areas.Select(item => Text(item, "mappedAdministrativeDongCode"))
                .Distinct(StringComparer.Ordinal).Count() == ExpectedAreaCount,
            "ScopeAreaDuplicate");

        var output = scope.GetProperty("output");
        Require(Text(output, "candidateSchemaVersion") == CandidateSchemaVersion
                && Text(output, "repositoryRelativeDirectory") == OutputRelative
                && Text(output, "generationDirectoryName") == "generations",
            "ScopeOutputChanged");
        Require(output.GetProperty("fileNames").EnumerateArray().Select(item => item.GetString()).SequenceEqual(
            new[] { "manifest.json", "candidates.ndjson", "audit.json", "generator-source.py", "complete.json" }),
            "ScopeOutputFileSetChanged");
        var toolchain = output.GetProperty("toolchain");
        Require(Text(toolchain, "generatorRelativePath") == GeneratorRelative
                && Text(toolchain, "generatorSha256") == GeneratorHash
                && string.Equals(generatorPath, ResolveRepositoryPath(root, Text(toolchain, "generatorRelativePath")),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(scopePath, ResolveRepositoryPath(root, ScopeRelative), StringComparison.OrdinalIgnoreCase),
            "ScopeToolchainChanged");

        var expectedRoles = new HashSet<string>(
        [
            "semasNationalArchive", "semasAcquisitionReceipt", "semasDownloadMetadata", "semasDatasetMetadata",
            "moisAdministrativeCodes", "r2ScopeDefinition", "r2ScopeManifest", "oa22160BoundaryArchive",
            "legacyMyeonmokSelected", "legacyFactoryCsv"
        ], StringComparer.Ordinal);
        var sources = new Dictionary<string, FrozenSource>(StringComparer.Ordinal);
        foreach (var value in scope.GetProperty("sources").EnumerateArray())
        {
            var role = Text(value, "role");
            var relative = Text(value, "repositoryRelativePath");
            var hash = Text(value, "contentHashSha256");
            var length = Long(value, "byteLength");
            var path = ResolveRepositoryPath(root, relative);
            Require(File.Exists(path) && new FileInfo(path).Length == length && HashFile(path) == hash,
                "ScopeSourceChanged");
            Require(sources.TryAdd(role, new FrozenSource(role, path, relative, hash, length)),
                "ScopeSourceRoleDuplicate");
        }
        Require(sources.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedRoles), "ScopeSourceRoleSetChanged");
        ValidateFrozenArchiveEntries(scope, sources);
        return sources;
    }

    private static void ValidateFrozenArchiveEntries(
        JsonElement scope,
        IReadOnlyDictionary<string, FrozenSource> sources)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var semasSource = scope.GetProperty("sources").EnumerateArray()
            .Single(item => Text(item, "role") == "semasNationalArchive");
        var semasEntryContract = semasSource.GetProperty("seoulEntry");
        var logicalName = Text(semasEntryContract, "logicalName");
        var cp949 = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var rawName = cp949.GetBytes(logicalName);
        Require(rawName.Length == Int(semasEntryContract, "rawNameByteLength")
                && Convert.ToBase64String(rawName) == Text(semasEntryContract, "rawNameBase64")
                && Convert.ToHexString(SHA256.HashData(rawName)) == Text(semasEntryContract, "rawNameHashSha256")
                && Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalName))) ==
                    Text(semasEntryContract, "logicalNameUtf8HashSha256")
                && Text(semasEntryContract, "rawNameEncoding") == "cp949",
            "SemasEntryNameContractChanged");
        using (var file = File.OpenRead(sources["semasNationalArchive"].Path))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Read, false, cp949))
        {
            var entry = zip.Entries.SingleOrDefault(item => item.FullName == logicalName);
            Require(entry is not null, "SemasSeoulEntryMissing");
            ValidateZipEntry(entry!, semasEntryContract, "SemasSeoulEntryChanged");
        }

        var moisSource = scope.GetProperty("sources").EnumerateArray()
            .Single(item => Text(item, "role") == "moisAdministrativeCodes");
        using var moisFile = File.OpenRead(sources["moisAdministrativeCodes"].Path);
        using var moisZip = new ZipArchive(moisFile, ZipArchiveMode.Read, false);
        foreach (var role in new[] { "administrative", "administrativeLegal" })
        {
            var contract = moisSource.GetProperty("entries").GetProperty(role);
            var name = Text(contract, "name");
            var entry = moisZip.Entries.SingleOrDefault(item => item.FullName == name);
            Require(entry is not null, "MoisEntryMissing");
            ValidateZipEntry(entry!, contract, "MoisEntryChanged");
        }
    }

    private static void ValidateZipEntry(ZipArchiveEntry entry, JsonElement contract, string code)
    {
        Require(entry.Length == Long(contract, "byteLength")
                && entry.Crc32.ToString("X8", CultureInfo.InvariantCulture) == Text(contract, "crc32"), code);
        using var stream = entry.Open();
        Require(HashStream(stream) == Text(contract, "contentHashSha256"), code);
    }

    private static object?[] BuildHeaderHashValues(
        JsonElement scope,
        string scopePath,
        string generatorPath,
        string root)
    {
        JsonElement Source(string role) => scope.GetProperty("sources").EnumerateArray()
            .Single(item => Text(item, "role") == role);
        var semas = Source("semasNationalArchive");
        var entry = semas.GetProperty("seoulEntry");
        var receipt = Source("semasAcquisitionReceipt");
        var download = Source("semasDownloadMetadata");
        var metadata = Source("semasDatasetMetadata");
        var mois = Source("moisAdministrativeCodes");
        var moisH = mois.GetProperty("entries").GetProperty("administrative");
        var moisMix = mois.GetProperty("entries").GetProperty("administrativeLegal");
        var r2Scope = Source("r2ScopeDefinition");
        var r2Manifest = Source("r2ScopeManifest");
        var boundary = Source("oa22160BoundaryArchive");
        var legacySelected = Source("legacyMyeonmokSelected");
        var legacyFactory = Source("legacyFactoryCsv");
        var contracts = scope.GetProperty("contracts");
        var values = new object?[]
        {
            "administrative-dong-business-candidate-set.v1", CandidateSchemaVersion, Revision, DesignRelative,
            DesignHash, ScopeRelative, HashFile(scopePath), GeneratorRelative, HashFile(generatorPath),
            Text(semas,"repositoryRelativePath"), Text(semas,"contentHashSha256"), Long(semas,"byteLength"),
            Text(entry,"logicalName"), Text(entry,"logicalNameUtf8HashSha256"), Text(entry,"rawNameEncoding"),
            Long(entry,"rawNameByteLength"), Text(entry,"rawNameBase64"), Text(entry,"rawNameHashSha256"),
            Long(entry,"byteLength"), Text(entry,"crc32"), Text(entry,"contentHashSha256"), Long(entry,"recordCount"),
            Text(receipt,"repositoryRelativePath"), Text(receipt,"contentHashSha256"), Long(receipt,"byteLength"),
            Text(download,"repositoryRelativePath"), Text(download,"contentHashSha256"), Long(download,"byteLength"),
            Text(metadata,"repositoryRelativePath"), Text(metadata,"contentHashSha256"), Long(metadata,"byteLength"),
            Text(mois,"repositoryRelativePath"), Text(mois,"contentHashSha256"), Long(mois,"byteLength"),
            Text(moisH,"name"), Text(moisH,"contentHashSha256"), Long(moisH,"byteLength"), Text(moisH,"crc32"),
            Text(moisMix,"name"), Text(moisMix,"contentHashSha256"), Long(moisMix,"byteLength"), Text(moisMix,"crc32"),
            Text(r2Scope,"repositoryRelativePath"), Text(r2Scope,"contentHashSha256"), Long(r2Scope,"byteLength"),
            Text(r2Manifest,"repositoryRelativePath"), Text(r2Manifest,"contentHashSha256"), Long(r2Manifest,"byteLength"),
            Text(r2Manifest,"documentContentHashSha256"), Text(boundary,"repositoryRelativePath"),
            Text(boundary,"contentHashSha256"), Long(boundary,"byteLength"),
            Text(legacySelected,"repositoryRelativePath"), Text(legacySelected,"contentHashSha256"), Long(legacySelected,"byteLength"),
            Text(legacyFactory,"repositoryRelativePath"), Text(legacyFactory,"contentHashSha256"), Long(legacyFactory,"byteLength"),
            ContractHash(contracts.GetProperty("mapping")), ContractHash(contracts.GetProperty("sourceRow")),
            ContractHash(contracts.GetProperty("historicalBoundary")), ContractHash(contracts.GetProperty("coordinate")),
            ContractHash(contracts.GetProperty("identity")), ContractHash(contracts.GetProperty("expectedCounts"))
        };
        Require(values.Length == HeaderHashFieldOrder.Length
                && Path.GetRelativePath(root, scopePath).Replace('\\','/') == ScopeRelative
                && Path.GetRelativePath(root, generatorPath).Replace('\\','/') == GeneratorRelative,
            "HeaderHashContractChanged");
        return values;
    }

    private static IReadOnlyList<Candidate> ReadCandidates(string path)
    {
        ValidateNdjsonEnvelope(path);
        var candidates = new List<Candidate>(ExpectedCandidateRows);
        using var stream = new StreamReader(path, new UTF8Encoding(false, true), false, 1 << 16);
        string? priorProvider = null;
        while (stream.ReadLine() is { } line)
        {
            Require(line.Length > 0 && line.Length <= 16_384, "CandidateLineSizeInvalid");
            using var document = JsonDocument.Parse(line);
            var value = document.RootElement;
            RequireExactPropertyOrder(value, CandidatePropertyOrder, "CandidatePropertyOrderChanged");
            Require(Text(value, "schemaVersion") == CandidateSchemaVersion
                    && Text(value, "revision") == Revision
                    && Text(value, "candidateSetHashSha256") == CandidateSetHash,
                "CandidateIdentityChanged");
            RequireExactPropertyOrder(value.GetProperty("commonEnuMillimeters"), ["x", "z"],
                "CandidateCommonEnuOrderChanged");
            RequireExactPropertyOrder(value.GetProperty("authorityFlags"), AuthorityPropertyOrder,
                "CandidateAuthorityOrderChanged");
            RequireExactPropertyOrder(value.GetProperty("protectedSource"), ProtectedSourcePropertyOrder,
                "CandidateProtectedPropertyOrderChanged");
            ValidateAuthority(value.GetProperty("authorityFlags"), "CandidateAuthorityChanged");
            var hashValues = CandidateHashValues(value);
            var candidate = CandidateFromHashValues(hashValues);
            Require(priorProvider is null
                    || StringComparer.Ordinal.Compare(priorProvider, candidate.ProviderShopIdCanonical) < 0,
                "CandidateProviderOrderChanged");
            priorProvider = candidate.ProviderShopIdCanonical;
            candidates.Add(candidate);
        }
        Require(candidates.Count == ExpectedCandidateRows, "CandidateRowCountChanged");
        return candidates;
    }

    private static object?[] CandidateHashValues(JsonElement candidate)
    {
        var values = new List<object?>(CandidateHashFieldOrder.Length + 16);
        foreach (var field in CandidateHashFieldOrder)
        {
            if (field.EndsWith("[]", StringComparison.Ordinal))
            {
                var array = Nested(candidate, field[..^2]);
                Require(array.ValueKind == JsonValueKind.Array, "CandidateHashArrayChanged");
                var items = array.EnumerateArray().ToArray();
                values.Add((long)items.Length);
                foreach (var item in items)
                {
                    Require(item.ValueKind == JsonValueKind.String, "CandidateHashArrayItemChanged");
                    values.Add(item.GetString() ?? string.Empty);
                }
            }
            else
            {
                values.Add(CanonicalScalar(Nested(candidate, field)));
            }
        }
        return values.ToArray();
    }

    private static Candidate CandidateFromHashValues(object?[] hashValues)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        var index = 0;
        foreach (var field in CandidateHashFieldOrder)
        {
            if (field.EndsWith("[]", StringComparison.Ordinal))
            {
                Require(index < hashValues.Length && hashValues[index] is long,
                    "ProtectedPayloadArrayCountInvalid");
                var count = (long)hashValues[index]!;
                Require(count is >= 0 and <= 100, "ProtectedPayloadArrayCountInvalid");
                index++;
                var items = new string[(int)count];
                for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
                {
                    Require(index < hashValues.Length && hashValues[index] is string,
                        "ProtectedPayloadArrayItemInvalid");
                    items[itemIndex] = (string)hashValues[index++]!;
                }
                fields[field[..^2]] = items;
            }
            else
            {
                Require(index < hashValues.Length, "ProtectedPayloadValueMissing");
                fields[field] = hashValues[index++];
            }
        }
        Require(index == hashValues.Length, "ProtectedPayloadValueCountChanged");

        string RequiredString(string field)
        {
            Require(fields.TryGetValue(field, out var value) && value is string, "ProtectedPayloadStringInvalid");
            return (string)value!;
        }
        bool RequiredBool(string field)
        {
            Require(fields.TryGetValue(field, out var value) && value is bool, "ProtectedPayloadBooleanInvalid");
            return (bool)value!;
        }
        long? NullableLong(string field)
        {
            Require(fields.TryGetValue(field, out var value) && (value is null or long),
                "ProtectedPayloadNullableIntegerInvalid");
            return (long?)value;
        }

        var protectedSource = ProtectedSourcePropertyOrder.ToDictionary(
            name => name,
            name => RequiredString("protectedSource." + name),
            StringComparer.Ordinal);
        var provider = protectedSource["providerShopIdCanonical"];
        var providerHash = RequiredString("providerBusinessIdSha256");
        var lowerHash = providerHash.ToLowerInvariant();
        Require(provider == provider.Trim() && ProviderIdPattern.IsMatch(provider), "ProviderIdentityInvalid");
        Require(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(provider))) == providerHash,
            "ProviderIdentityHashMismatch");
        Require(Regex.IsMatch(providerHash, "^[0-9A-F]{64}$", RegexOptions.CultureInvariant),
            "ProviderIdentityDigestInvalid");
        var sourceFeature = RequiredString("sourceFeatureKey");
        var stableId = RequiredString("stableId");
        var dimension = RequiredString("dimensionKey");
        Require(sourceFeature == "source-feature:semas:business:sha256:" + lowerHash
                && stableId == "administrative-dong-business-candidate:semas:sha256:" + lowerHash
                && dimension == "business-candidate|semas|sha256|" + providerHash
                && !sourceFeature.Contains(provider, StringComparison.Ordinal)
                && !stableId.Contains(provider, StringComparison.Ordinal)
                && !dimension.Contains(provider, StringComparison.Ordinal),
            "HashedIdentifierContractChanged");
        var area = RequiredString("administrativeAreaStableId");
        var mappedCode = RequiredString("mappedAdministrativeDongCode");
        var sourceCode = RequiredString("sourceAdministrativeDongCode");
        Require(ExpectedDistribution.ContainsKey(mappedCode)
                && area == "region:kr:hjd:" + mappedCode
                && sourceCode.Length == 8
                && mappedCode.Length == 10,
            "AdministrativeAreaContractChanged");
        Require(RequiredString("schemaVersion") == CandidateSchemaVersion
                && RequiredString("revision") == Revision
                && RequiredString("sourceCoordinateDatumStatusCode") == CoordinateStatus
                && RequiredString("historicalBoundaryOwnershipDispositionCode") ==
                    "SourceAdministrativeDongOwnershipPreserved"
                && RequiredString("identityMergeState") == "NotMerged"
                && RequiredString("qualityCode") == Quality,
            "CandidateBoundedStateChanged");
        var building = protectedSource["buildingManagementNumber"];
        Require(building.Length == 0 || BuildingManagementPattern.IsMatch(building),
            "BuildingManagementNumberInvalid");
        Require(protectedSource["roadNameAddress"].Length > 0
                && protectedSource["landLotAddress"].Length > 0
                && protectedSource["sourceLongitudeText"].Length > 0
                && protectedSource["sourceLatitudeText"].Length > 0,
            "ProtectedLocationObservationMissing");
        var food = RequiredBool("foodCategory");
        Require(food == (RequiredString("largeCategoryCode") == "I2"), "FoodCategoryChanged");
        foreach (var authority in AuthorityPropertyOrder)
        {
            var expected = authority == "privateReviewOnly";
            Require(RequiredBool("authorityFlags." + authority) == expected, "CandidateAuthorityChanged");
        }
        var identityHash = fields["identityCandidateGroupSha256"] as string;
        var identitySize = NullableLong("identityCandidateGroupSize");
        Require((identityHash is null) == (identitySize is null)
                && (identityHash is null || Regex.IsMatch(identityHash, "^[0-9A-F]{64}$", RegexOptions.CultureInvariant))
                && (identitySize is null || identitySize >= 2),
            "IdentityCandidateContractChanged");
        if (identityHash is not null)
            Require(FrameDigest([
                    "semas-business-name-road-address-identity-candidate.v1",
                    protectedSource["businessName"], protectedSource["roadNameAddress"]
                ]) == identityHash,
                "IdentityCandidateHashChanged");
        var qualityDiagnostics = (string[])fields["qualityDiagnosticCodes"]!;
        Require(qualityDiagnostics.Contains(CoordinateStatus, StringComparer.Ordinal)
                && qualityDiagnostics.Contains("CurrentOperationUnverified", StringComparer.Ordinal)
                && qualityDiagnostics.Contains("ExactBusinessDetailsPrivateLocalAndProtectedRdbOnly", StringComparer.Ordinal),
            "QualityDiagnosticContractChanged");
        return new Candidate(provider, stableId, sourceFeature, dimension, providerHash,
            RequiredString("sourceRowHashSha256"), area, food, building,
            RequiredString("historicalBoundaryDiagnosticCode"), identityHash, identitySize,
            qualityDiagnostics, protectedSource, hashValues);
    }

    private static void ValidateComputedCounts(IReadOnlyCollection<Candidate> candidates)
    {
        Require(candidates.Count == ExpectedCandidateRows
                && candidates.Select(item => item.ProviderShopIdCanonical).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(item => item.ProviderBusinessIdSha256).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(item => item.SourceFeatureKey).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == candidates.Count
                && candidates.Select(item => item.DimensionKey).Distinct(StringComparer.Ordinal).Count() == candidates.Count,
            "CandidateIdentitySetChanged");
        Require(candidates.Count(item => item.FoodCategory) == ExpectedFoodRows
                && candidates.Count(item => item.BuildingManagementNumber.Length == 0) == ExpectedMissingBuildingRows
                && candidates.Count(item => item.FoodCategory && item.BuildingManagementNumber.Length == 0) == 15
                && candidates.Where(item => item.BuildingManagementNumber.Length > 0)
                    .Select(item => item.BuildingManagementNumber).Distinct(StringComparer.Ordinal).Count() == 11_931
                && candidates.Count(item => item.HistoricalBoundaryDiagnosticCode == BoundaryConflict) == ExpectedBoundaryConflictRows
                && candidates.Count(item => item.HistoricalBoundaryDiagnosticCode == BoundaryOutside) == ExpectedBoundaryOutsideRows
                && candidates.Count(item => item.HistoricalBoundaryDiagnosticCode != BoundaryMatched) == ExpectedBoundaryDiagnosticRows
                && candidates.Count(item => item.IdentityCandidateGroupSha256 is not null) == ExpectedIdentityRows
                && candidates.Where(item => item.IdentityCandidateGroupSha256 is not null)
                    .Select(item => item.IdentityCandidateGroupSha256).Distinct(StringComparer.Ordinal).Count() == ExpectedIdentityGroups,
            "CandidateComputedCountsChanged");
        Require(candidates.Where(item => item.IdentityCandidateGroupSha256 is not null)
                .GroupBy(item => item.IdentityCandidateGroupSha256!, StringComparer.Ordinal)
                .All(group => group.All(item => item.IdentityCandidateGroupSize == group.LongCount())),
            "IdentityCandidateGroupSizeChanged");
        var distribution = candidates.GroupBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key["region:kr:hjd:".Length..],
                group => (group.Count(), group.Count(item => item.FoodCategory)), StringComparer.Ordinal);
        Require(distribution.Count == ExpectedAreaCount
                && ExpectedDistribution.All(pair => distribution.TryGetValue(pair.Key, out var actual)
                    && actual == (pair.Value.CandidateRows, pair.Value.FoodRows)),
            "CandidateDistributionChanged");
    }

    private static void ValidatePerArea(JsonElement value, IReadOnlyCollection<Candidate> candidates, string code)
    {
        Require(value.ValueKind == JsonValueKind.Array, code);
        var rows = value.EnumerateArray().ToArray();
        Require(rows.Length == ExpectedAreaCount, code);
        foreach (var row in rows)
        {
            var area = Text(row, "administrativeAreaStableId");
            var mapped = Text(row, "mappedAdministrativeDongCode");
            var selected = candidates.Where(item => item.AdministrativeAreaStableId == area).ToArray();
            Require(area == "region:kr:hjd:" + mapped
                    && ExpectedDistribution.TryGetValue(mapped, out var expected)
                    && Int(row, "candidateRows") == expected.CandidateRows
                    && Int(row, "foodRows") == expected.FoodRows
                    && selected.Length == expected.CandidateRows
                    && selected.Count(item => item.FoodCategory) == expected.FoodRows,
                code);
        }
    }

    private static void ValidateManifest(JsonElement value)
    {
        RequireExactPropertyOrder(value,
        [
            "schemaVersion", "revision", "candidateSchemaVersion", "candidateSetHashSha256", "generatedAtUtc",
            "evidenceAsOfUtc", "derivedAtUtc", "collectedAtUtc", "designDocumentRef", "designHashSha256",
            "scopeRelativePath", "scopeDefinitionSha256", "generatorRelativePath", "generatorSha256",
            "reviewStatus", "sourceCoordinateDatumStatusCode", "completionStatusCode", "privacyBoundaryCode",
            "counts", "perAdministrativeArea", "historicalBoundarySummary", "legacyPreservationSummary",
            "sourceLineage", "contractHashes", "candidateSetHashContract", "authorityFlags", "contentHashSha256"
        ], "ManifestPropertyOrderChanged");
        Require(Text(value, "schemaVersion") == "administrative-dong-business-candidate-manifest.v1"
                && Text(value, "revision") == Revision
                && Text(value, "candidateSchemaVersion") == CandidateSchemaVersion
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "evidenceAsOfUtc") == "2026-06-30T00:00:00Z"
                && Text(value, "derivedAtUtc") == "2026-09-15T00:00:00Z"
                && Text(value, "collectedAtUtc") == "2026-09-08T10:31:09.6612043Z"
                && Text(value, "designDocumentRef") == DesignRelative
                && Text(value, "designHashSha256") == DesignHash
                && Text(value, "scopeRelativePath") == ScopeRelative
                && Text(value, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(value, "generatorRelativePath") == GeneratorRelative
                && Text(value, "generatorSha256") == GeneratorHash
                && Text(value, "reviewStatus") == Quality
                && Text(value, "sourceCoordinateDatumStatusCode") == CoordinateStatus
                && Text(value, "completionStatusCode") == "G4aBusinessCandidateBundleGeneratedAndVerified"
                && Text(value, "privacyBoundaryCode") == "ExactBusinessDetailsPrivateLocalAndProtectedRdbOnly",
            "ManifestIdentityChanged");
        ValidateCounts(value.GetProperty("counts"), "ManifestCountsChanged");
        ValidateLegacyAggregate(value.GetProperty("legacyPreservationSummary"), "ManifestLegacySummaryChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "ManifestAuthorityChanged");
        var contract = value.GetProperty("candidateSetHashContract");
        Require(Text(contract, "algorithm") == "SHA-256"
                && Text(contract, "framing") == "presence-byte-then-uint32-big-endian-utf8-byte-length-then-utf8-bytes"
                && Text(contract, "booleanEncoding") == "lowercase-true-or-false"
                && Text(contract, "integerEncoding") == "invariant-decimal"
                && Text(contract, "coordinateEncoding") == "original-csv-text"
                && Text(contract, "arrayEncoding") == "framed-count-then-each-item-in-ordinal-order"
                && Text(contract, "candidateOrder") == "providerShopIdCanonical-ordinal"
                && Int(contract, "nullPresenceByte") == 0
                && Int(contract, "presentPresenceByte") == 1,
            "ManifestHashFramingChanged");
        Require(contract.GetProperty("headerFieldOrder").EnumerateArray().Select(item => item.GetString())
                    .SequenceEqual(HeaderHashFieldOrder)
                && contract.GetProperty("candidateFieldOrder").EnumerateArray().Select(item => item.GetString())
                    .SequenceEqual(CandidateHashFieldOrder),
            "ManifestHashFieldOrderChanged");
        Require(ContentHash(value) == Text(value, "contentHashSha256"), "ManifestContentHashChanged");
    }

    private static void ValidateAudit(JsonElement value)
    {
        RequireExactPropertyOrder(value,
        [
            "schemaVersion", "revision", "candidateSetHashSha256", "status", "scopeDefinitionSha256",
            "generatorSha256", "counts", "perAdministrativeArea", "historicalBoundarySummary",
            "legacyPreservationSummary", "contractHashes", "candidateFileSha256", "candidateFileByteLength",
            "manifestFileSha256", "manifestFileByteLength", "generatorSourceFileSha256",
            "generatorSourceFileByteLength", "privacyInspectionStatusCode", "authorityFlags", "contentHashSha256"
        ], "AuditPropertyOrderChanged");
        Require(Text(value, "schemaVersion") == "administrative-dong-business-candidate-audit.v1"
                && Text(value, "revision") == Revision
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "status") == "PASS"
                && Text(value, "scopeDefinitionSha256") == ScopeDefinitionHash
                && Text(value, "generatorSha256") == GeneratorHash
                && Text(value, "candidateFileSha256") == CandidatesHash
                && Text(value, "manifestFileSha256") == ManifestHash
                && Text(value, "generatorSourceFileSha256") == GeneratorSourceHash
                && Text(value, "privacyInspectionStatusCode") ==
                    "AggregateDocumentsContainNoExactBusinessDetailValues",
            "AuditIdentityChanged");
        ValidateCounts(value.GetProperty("counts"), "AuditCountsChanged");
        ValidateLegacyAggregate(value.GetProperty("legacyPreservationSummary"), "AuditLegacySummaryChanged");
        ValidateAuthority(value.GetProperty("authorityFlags"), "AuditAuthorityChanged");
        Require(ContentHash(value) == Text(value, "contentHashSha256"), "AuditContentHashChanged");
    }

    private static void ValidateComplete(JsonElement value, string generationPath)
    {
        RequireExactPropertyOrder(value,
            ["schemaVersion", "revision", "candidateSetHashSha256", "status", "fileCount", "files", "authorityFlags", "contentHashSha256"],
            "CompletePropertyOrderChanged");
        Require(Text(value, "schemaVersion") == "administrative-dong-business-candidate-complete.v1"
                && Text(value, "revision") == Revision
                && Text(value, "candidateSetHashSha256") == CandidateSetHash
                && Text(value, "status") == "Complete"
                && Int(value, "fileCount") == 4,
            "CompleteIdentityChanged");
        var expected = new[]
        {
            (Name:"manifest.json", Hash:ManifestHash),
            (Name:"candidates.ndjson", Hash:CandidatesHash),
            (Name:"audit.json", Hash:AuditHash),
            (Name:"generator-source.py", Hash:GeneratorSourceHash)
        };
        var files = value.GetProperty("files").EnumerateArray().ToArray();
        Require(files.Length == expected.Length, "CompleteFilesChanged");
        for (var index = 0; index < expected.Length; index++)
        {
            var path = Path.Combine(generationPath, expected[index].Name);
            Require(Text(files[index], "name") == expected[index].Name
                    && Text(files[index], "sha256") == expected[index].Hash
                    && Long(files[index], "byteLength") == new FileInfo(path).Length,
                "CompleteFileMetadataChanged");
        }
        ValidateAuthority(value.GetProperty("authorityFlags"), "CompleteAuthorityChanged");
        Require(ContentHash(value) == Text(value, "contentHashSha256"), "CompleteContentHashChanged");
    }

    private static void ValidateCounts(JsonElement value, string code)
    {
        var expected = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["nationalCsvEntries"] = 16, ["nationalRows"] = 2_772_484, ["seoulRows"] = 554_092,
            ["candidateRows"] = ExpectedCandidateRows, ["foodRows"] = ExpectedFoodRows,
            ["missingProviderShopIdRows"] = 0, ["duplicateProviderShopIdRows"] = 0,
            ["missingCoordinateRows"] = 0, ["missingRoadAddressRows"] = 0, ["missingLandAddressRows"] = 0,
            ["missingBuildingManagementNumberRows"] = ExpectedMissingBuildingRows,
            ["foodMissingBuildingManagementNumberRows"] = 15, ["uniqueBuildingManagementNumbers"] = 11_931,
            ["historicalBoundaryMatchedRows"] = 29_686,
            ["historicalBoundaryConflictRows"] = ExpectedBoundaryConflictRows,
            ["historicalBoundaryOutsideRows"] = ExpectedBoundaryOutsideRows,
            ["historicalBoundaryMultipleRows"] = 0,
            ["historicalBoundaryDiagnosticRows"] = ExpectedBoundaryDiagnosticRows,
            ["historicalBoundaryDiagnosticGroups"] = 13,
            ["identityCandidateGroups"] = ExpectedIdentityGroups, ["identityCandidateRows"] = ExpectedIdentityRows,
            ["legacyMyeonmokShopRows"] = ExpectedLegacyShopRows, ["legacyFactoryRows"] = ExpectedLegacyFactoryRows,
            ["legacyProviderOverlapRows"] = ExpectedLegacyOverlapRows, ["newProviderRows"] = ExpectedNewProviderRows,
            ["sourceAdministrativeNameDifferenceAreas"] = 26
        };
        RequireExactProperties(value, expected.Keys, code);
        Require(expected.All(pair => value.GetProperty(pair.Key).TryGetInt64(out var actual) && actual == pair.Value), code);
    }

    private static void ValidateLegacyAggregate(JsonElement value, string code)
    {
        Require(Int(value, "legacyMyeonmokShopRows") == ExpectedLegacyShopRows
                && Int(value, "legacyFactoryRows") == ExpectedLegacyFactoryRows
                && Int(value, "legacyProviderOverlapRows") == ExpectedLegacyOverlapRows
                && Int(value, "newProviderRows") == ExpectedNewProviderRows,
            code);
    }

    private static void ValidateAuthority(JsonElement value, string code)
    {
        RequireExactPropertyOrder(value, AuthorityPropertyOrder, code);
        foreach (var name in AuthorityPropertyOrder)
            Require(Bool(value, name) == (name == "privateReviewOnly"), code);
    }

    private static void ValidateAggregateDocument(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                Require(!AggregateBannedKeys.Contains(property.Name), "ProtectedFieldInAggregateDocument");
                ValidateAggregateDocument(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) ValidateAggregateDocument(item);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            Require(!ProviderIdLeakPattern.IsMatch(value.GetString() ?? string.Empty),
                "RawProviderIdentityInAggregateDocument");
        }
    }

    private static List<외부데이터정규화Record> BuildRows(Bundle bundle)
    {
        var rows = new List<외부데이터정규화Record>(ExpectedCandidateRows);
        foreach (var candidate in bundle.Candidates)
        {
            var payload = JsonSerializer.Serialize(new
            {
                schemaVersion = ProtectedPayloadSchemaVersion,
                candidateSetHashSha256 = CandidateSetHash,
                hashValues = candidate.HashValues
            }, CompactJson);
            Require(payload.Length <= 2_000 && Encoding.UTF8.GetByteCount(payload) <= 8_000,
                "ProtectedPayloadFieldBudgetExceeded");
            Require(candidate.DimensionKey.Length <= 500 && Limitation.Length <= 240,
                "NormalizedFieldBudgetExceeded");
            rows.Add(new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId,
                    candidate.AdministrativeAreaStableId, MetricCode, EvidenceAsOfUtc, candidate.DimensionKey),
                StableId = candidate.StableId,
                SourceId = SourceId,
                DatasetId = DatasetId,
                RegionStableId = candidate.AdministrativeAreaStableId,
                MetricCode = MetricCode,
                NumericValue = null,
                TextValue = payload,
                UnitCode = "source-business-observation",
                EvidenceAsOfUtc = EvidenceAsOfUtc,
                CollectedAtUtc = CollectedAtUtc,
                SpatialPrecisionCode = "source-address-and-lonlat-crs-not-confirmed",
                TemporalPrecisionCode = "source-date-only",
                QualityCode = Quality,
                LimitationCode = Limitation,
                DimensionKey = candidate.DimensionKey,
                SourceVersion = "source-row-sha256:" + candidate.SourceRowHashSha256.ToLowerInvariant(),
                DataRevision = Revision,
                FirstSeenAtUtc = CollectedAtUtc,
                LastSeenAtUtc = CollectedAtUtc
            });
        }
        Require(rows.Count == ExpectedCandidateRows
                && rows.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == rows.Count
                && rows.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == rows.Count
                && rows.Select(item => item.DimensionKey).Distinct(StringComparer.Ordinal).Count() == rows.Count,
            "NormalizedExactSetInvalid");
        return rows;
    }

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Bundle bundle,
        IReadOnlyList<외부데이터정규화Record> rows,
        LegacySummary legacyBefore,
        RejectedR1Summary rejectedR1Before,
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
        var primaryLocked = false;
        var rejectedR1Locked = false;
        var legacyLocked = false;
        try
        {
            primaryLocked = await AcquireLockAsync(db, LockName);
            Require(primaryLocked, "ImportBusy");
            rejectedR1Locked = await AcquireLockAsync(db, RejectedR1LockName);
            Require(rejectedR1Locked, "RejectedR1LedgerBusy");
            legacyLocked = await AcquireLockAsync(db, LegacyLockName);
            Require(legacyLocked, "LegacyLedgerBusy");

            var lockedLegacy = await ReadLegacySummaryAsync(db, bundle.Candidates);
            var lockedRejectedR1 = await ReadRejectedR1SummaryAsync(db);
            Require(lockedLegacy == legacyBefore, "LegacyMyeonmokLedgerChangedBeforeApply");
            Require(lockedRejectedR1 == rejectedR1Before, "RejectedR1LedgerChangedBeforeApply");
            var lockedExisting = await LoadScopedRowsAsync(db);
            await ValidateSourceSnapshotPreflightAsync(db, bundle);
            await ValidateExistingAsync(db, bundle, lockedExisting, rows, requireComplete: false);
            if (lockedExisting.Count == rows.Count && await AreSourceSnapshotsCompleteAsync(db, bundle))
            {
                SetNoWriteResult(result, lockedExisting.Count);
                return;
            }

            result["databaseWriteAttempted"] = true;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var registrations = new Dictionary<string, (long RawSnapshotId, bool Inserted)>(StringComparer.Ordinal);
            foreach (var input in BuildRegistrationInputs(bundle))
            {
                var stored = await db.RawSnapshots.SingleOrDefaultAsync(item =>
                    item.SourceId == SourceId && item.DatasetId == input.DatasetId);
                if (stored is not null)
                {
                    await RequireSourceSnapshotAsync(db, input);
                    registrations.Add(input.DatasetId, (stored.Id, false));
                    continue;
                }

                var runKey = ExpectedRunKey(input);
                Require(!await db.IngestionRuns.AnyAsync(item => item.RunKey == runKey),
                    "SourceRegistrationRunConflict");
                long rawSnapshotId;
                if (input.VirtualArchiveEntry)
                {
                    rawSnapshotId = await RegisterVirtualArchiveEntryAsync(db, input);
                }
                else
                {
                    var registration = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(
                        input.Path,
                        new 공공공간원본등록Request(SourceId, input.DatasetId, input.SourceVersion, Revision,
                            EvidenceAsOfUtc, input.ContentType, input.RelativePath));
                    Require(registration.Inserted
                            && registration.SourceHashSha256.Equals(input.Hash, StringComparison.OrdinalIgnoreCase),
                        "SourceRegistrationChanged");
                    rawSnapshotId = registration.RawSnapshotId;
                    await NormalizeRegisteredSourceStateAsync(db, input, rawSnapshotId);
                }
                registrations.Add(input.DatasetId, (rawSnapshotId, true));
            }
            foreach (var input in BuildRegistrationInputs(bundle))
                await RequireSourceSnapshotAsync(db, input);

            var original = registrations[OriginalInputDatasetId];
            foreach (var row in rows) row.RawSnapshotId = original.RawSnapshotId;
            var inserted = 0;
            var existing = lockedExisting.Count == rows.Count ? rows.Count : 0;
            if (lockedExisting.Count != rows.Count)
            {
                foreach (var batch in rows.Chunk(500))
                {
                    Require(batch.Length <= 500, "NormalizedChunkSizeExceeded");
                    var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch);
                    Require(saved.UpdatedCount == 0, "UnexpectedNormalizedUpdate");
                    inserted += saved.InsertedCount;
                    existing += saved.ExistingCount;
                    db.ChangeTracker.Clear();
                }
            }
            Require(inserted + existing == rows.Count, "NormalizedSaveCountMismatch");
            Require(await ReadLegacySummaryAsync(db, bundle.Candidates) == legacyBefore,
                "LegacyMyeonmokLedgerChangedDuringApply");
            Require(await ReadRejectedR1SummaryAsync(db) == rejectedR1Before,
                "RejectedR1LedgerChangedDuringApply");
            await transaction.CommitAsync();
            result["committed"] = true;
            result["inserted"] = inserted;
            result["existing"] = existing;
            result["updated"] = 0;
            result["rawSnapshotInserted"] = registrations.Values.Count(item => item.Inserted);
            result["rawSnapshotCount"] = registrations.Count;
        }
        finally
        {
            if (legacyLocked) await ReleaseLockAsync(db, LegacyLockName);
            if (rejectedR1Locked) await ReleaseLockAsync(db, RejectedR1LockName);
            if (primaryLocked) await ReleaseLockAsync(db, LockName);
        }
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
        run.FetchedCount = input.CanonicalSource ? 2_772_484 : input.VirtualArchiveEntry ? 554_092 : 1;
        run.NormalizedCount = input.CanonicalSource ? ExpectedCandidateRows : 0;
        run.RejectedCount = 0;
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

    private static async Task<long> RegisterVirtualArchiveEntryAsync(
        PublicDataIngestionDbContext db,
        RegistrationInput input)
    {
        Require(input.VirtualArchiveEntry && !input.CanonicalSource, "VirtualArchiveEntryContractInvalid");
        var run = new 외부데이터수집Run
        {
            RunKey = ExpectedRunKey(input), SourceId = SourceId, DatasetId = input.DatasetId,
            StatusCode = 외부데이터수집StatusCodes.Partial, StartedAtUtc = CollectedAtUtc,
            CompletedAtUtc = CollectedAtUtc, AttemptCount = 1, FetchedCount = 554_092,
            NormalizedCount = 0, RejectedCount = 0, InsertedCount = 1, UpdatedCount = 0, ExistingCount = 0,
            SourceVersion = input.SourceVersion, DataRevision = Revision, ErrorCode = Quality,
            ErrorSummary = ExpectedRunErrorSummary(input)
        };
        db.IngestionRuns.Add(run);
        await db.SaveChangesAsync();
        var relative = input.RelativePath.Replace('\\', '/');
        var snapshot = new 외부데이터RawSnapshot
        {
            FirstCollectionRunId = run.Id, SourceId = SourceId, DatasetId = input.DatasetId,
            SourceVersion = input.SourceVersion, CollectedAtUtc = CollectedAtUtc, EvidenceAsOfUtc = EvidenceAsOfUtc,
            ContentHashSha256 = input.Hash.ToLowerInvariant(), ContentLength = input.Length,
            ContentType = input.ContentType, OriginalFileName = input.OriginalFileName,
            StorageContainer = "local-private-public-spatial", StorageObjectName = relative,
            StorageLocation = "private-file://" + relative, FirstSeenAtUtc = CollectedAtUtc,
            LastSeenAtUtc = CollectedAtUtc
        };
        db.RawSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        return snapshot.Id;
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
            var canonical = source.Role == "semasNationalArchive";
            var dataset = canonical
                ? OriginalInputDatasetId
                : DatasetId + "-support-" + source.Role.ToLowerInvariant();
            inputs.Add(new RegistrationInput(dataset, source.Path, source.RelativePath, source.Hash,
                source.Length, Path.GetFileName(source.Path), "sha256:" + source.Hash.ToLowerInvariant(),
                ContentType(source.Path), canonical, false));
        }
        using (var scope = ReadJson(bundle.ScopePath, 1_000_000))
        {
            var semas = scope.RootElement.GetProperty("sources").EnumerateArray()
                .Single(item => Text(item, "role") == "semasNationalArchive");
            var entry = semas.GetProperty("seoulEntry");
            var archive = bundle.Sources["semasNationalArchive"];
            var logicalName = Text(entry, "logicalName");
            inputs.Add(new RegistrationInput(DatasetId + "-seoul-entry-input", archive.Path,
                archive.RelativePath + "!/" + logicalName, Text(entry, "contentHashSha256"),
                Long(entry, "byteLength"), logicalName,
                "sha256:" + Text(entry, "contentHashSha256").ToLowerInvariant(), "text/csv", false, true));
        }
        var designPath = ResolveRepositoryPath(bundle.Root, DesignRelative);
        inputs.Add(new RegistrationInput(DatasetId + "-design-input", designPath, DesignRelative, DesignHash,
            new FileInfo(designPath).Length, Path.GetFileName(designPath), "sha256:" + DesignHash.ToLowerInvariant(),
            "text/markdown", false, false));
        inputs.Add(new RegistrationInput(DatasetId + "-scope-input", bundle.ScopePath, ScopeRelative,
            ScopeDefinitionHash, new FileInfo(bundle.ScopePath).Length, Path.GetFileName(bundle.ScopePath),
            "sha256:" + ScopeDefinitionHash.ToLowerInvariant(), "application/json", false, false));
        inputs.Add(new RegistrationInput(DatasetId + "-generator-input", bundle.GeneratorPath, GeneratorRelative,
            GeneratorHash, new FileInfo(bundle.GeneratorPath).Length, Path.GetFileName(bundle.GeneratorPath),
            "sha256:" + GeneratorHash.ToLowerInvariant(), "text/x-python", false, false));
        foreach (var artifact in new[]
        {
            (Suffix:"manifest", Path:bundle.ManifestPath, Hash:ManifestHash, Type:"application/json"),
            (Suffix:"candidates", Path:bundle.CandidatesPath, Hash:CandidatesHash, Type:"application/x-ndjson"),
            (Suffix:"audit", Path:bundle.AuditPath, Hash:AuditHash, Type:"application/json"),
            (Suffix:"generator-source", Path:bundle.GeneratorSourcePath, Hash:GeneratorSourceHash, Type:"text/x-python"),
            (Suffix:"complete", Path:bundle.CompletePath, Hash:CompleteHash, Type:"application/json")
        })
        {
            inputs.Add(new RegistrationInput(DatasetId + "-artifact-" + artifact.Suffix, artifact.Path,
                GenerationRelative + "/" + Path.GetFileName(artifact.Path), artifact.Hash,
                new FileInfo(artifact.Path).Length, Path.GetFileName(artifact.Path),
                Revision + ";candidate-set:" + CandidateSetHash.ToLowerInvariant(), artifact.Type, false, false));
        }
        Require(inputs.Count == 19
                && inputs.Count(item => item.CanonicalSource) == 1
                && inputs.Count(item => item.VirtualArchiveEntry) == 1
                && inputs.Select(item => item.DatasetId).Distinct(StringComparer.Ordinal).Count() == inputs.Count
                && inputs.All(item => item.DatasetId.Length <= 160 && item.SourceVersion.Length <= 200),
            "RegistrationInputSetInvalid");
        return inputs;
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
            if (snapshots.Count == 1) await RequireSourceSnapshotAsync(db, input);
            else
            {
                Require(!await db.IngestionRuns.AsNoTracking().AnyAsync(item => item.RunKey == ExpectedRunKey(input)),
                    "SourceRunWithoutSnapshot");
            }
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
        var snapshot = snapshots[0];
        var run = await db.IngestionRuns.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == snapshot.FirstCollectionRunId);
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
               && value.OriginalFileName == input.OriginalFileName
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
           && value.FetchedCount == (input.CanonicalSource ? 2_772_484 : input.VirtualArchiveEntry ? 554_092 : 1)
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
            ? "Private G4a candidate derivation from a frozen SEMAS archive; exact details remain protected; current operation, coordinate datum, building binding, claim, distribution, runtime, gameplay and Unity authority are absent."
            : input.VirtualArchiveEntry
                ? "Verified frozen CSV entry inside the protected SEMAS archive; no extracted copy is retained; no public, operational, runtime, gameplay or Unity authority."
                : "Frozen support or generated evidence snapshot for the private G4a candidate ledger; no public, operational, runtime, gameplay or Unity authority.";

    private static string ExpectedRunKey(RegistrationInput input)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.DatasetId))).ToLowerInvariant();
        return "public-data-candidate:g4a-r2:" + digest[..24];
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
        var candidates = stored.Select(CandidateFromStoredPayload)
            .OrderBy(item => item.ProviderShopIdCanonical, StringComparer.Ordinal).ToArray();
        Require(ComputeCandidateSetHash(bundle.HeaderHashValues, candidates) == CandidateSetHash,
            "ReadbackCandidateSetHashMismatch");
        ValidateComputedCounts(candidates);
        result["verifiedRows"] = stored.Count;
        result["verifiedAdministrativeAreas"] = stored.Select(item => item.RegionStableId)
            .Distinct(StringComparer.Ordinal).Count();
        result["verifiedFoodRows"] = candidates.Count(item => item.FoodCategory);
        result["verifiedHistoricalBoundaryDiagnosticRows"] = candidates.Count(item =>
            item.HistoricalBoundaryDiagnosticCode != BoundaryMatched);
        result["verifiedMissingBuildingManagementNumberRows"] = candidates.Count(item =>
            item.BuildingManagementNumber.Length == 0);
        result["verifiedProtectedFieldsPerRow"] = ProtectedSourcePropertyOrder.Length;
        result["readbackCandidateSetHashSha256"] = CandidateSetHash.ToLowerInvariant();
        result["independentReadback"] = true;
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
    }

    private static Task<List<외부데이터정규화Record>> LoadScopedRowsAsync(PublicDataIngestionDbContext db)
        => db.NormalizedRecords.AsNoTracking().Where(item =>
            item.SourceId == SourceId && item.DatasetId == DatasetId).ToListAsync();

    private static async Task ValidateExistingAsync(
        PublicDataIngestionDbContext db,
        Bundle bundle,
        IReadOnlyCollection<외부데이터정규화Record> existing,
        IReadOnlyCollection<외부데이터정규화Record> expected,
        bool requireComplete)
    {
        var expectedByKey = expected.ToDictionary(item => item.RecordKey, StringComparer.Ordinal);
        var candidateByStableId = bundle.Candidates.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        Require(existing.Select(item => item.RecordKey).Distinct(StringComparer.Ordinal).Count() == existing.Count,
            "StoredRecordKeyDuplicate");
        foreach (var stored in existing)
        {
            Require(expectedByKey.TryGetValue(stored.RecordKey, out var wanted)
                    && EquivalentExceptStorageIds(stored, wanted!),
                "StoredRecordConflict");
            Require(candidateByStableId.TryGetValue(stored.StableId, out var sourceCandidate),
                "StoredCandidateIdentityUnknown");
            var readbackCandidate = CandidateFromStoredPayload(stored);
            Require(HashValueSequenceEqual(readbackCandidate.HashValues, sourceCandidate!.HashValues),
                "StoredProtectedPayloadChanged");
            foreach (var name in ProtectedSourcePropertyOrder)
                Require(readbackCandidate.ProtectedSource[name] == sourceCandidate.ProtectedSource[name],
                    "StoredProtectedFieldChanged");
        }
        if (requireComplete) Require(existing.Count == expected.Count, "StoredExactSetIncomplete");
        if (existing.Count == 0) return;
        var rawIds = existing.Select(item => item.RawSnapshotId).Distinct().ToArray();
        Require(rawIds.Length == 1, "StoredCanonicalSourceSnapshotChanged");
        var original = BuildRegistrationInputs(bundle).Single(item => item.CanonicalSource);
        var raw = await db.RawSnapshots.AsNoTracking().SingleOrDefaultAsync(item => item.Id == rawIds[0]);
        Require(raw is not null && SourceSnapshotMatches(raw, original), "StoredCanonicalSourceSnapshotChanged");
        if (requireComplete)
            foreach (var input in BuildRegistrationInputs(bundle)) await RequireSourceSnapshotAsync(db, input);
    }

    private static Candidate CandidateFromStoredPayload(외부데이터정규화Record row)
    {
        using var document = JsonDocument.Parse(row.TextValue);
        var value = document.RootElement;
        RequireExactPropertyOrder(value, ["schemaVersion", "candidateSetHashSha256", "hashValues"],
            "StoredPayloadPropertyOrderChanged");
        Require(Text(value, "schemaVersion") == ProtectedPayloadSchemaVersion
                && Text(value, "candidateSetHashSha256") == CandidateSetHash,
            "StoredPayloadIdentityChanged");
        var hashValuesElement = value.GetProperty("hashValues");
        Require(hashValuesElement.ValueKind == JsonValueKind.Array, "StoredPayloadValuesInvalid");
        var hashValues = hashValuesElement.EnumerateArray().Select(CanonicalScalar).ToArray();
        return CandidateFromHashValues(hashValues);
    }

    private static bool EquivalentExceptStorageIds(
        외부데이터정규화Record stored,
        외부데이터정규화Record expected)
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

    private static async Task<RejectedR1Summary> ReadRejectedR1SummaryAsync(
        DbContextOptions<PublicDataIngestionDbContext> options)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        return await ReadRejectedR1SummaryAsync(db);
    }

    private static async Task<RejectedR1Summary> ReadRejectedR1SummaryAsync(
        PublicDataIngestionDbContext db)
    {
        const string rejectedRevision = "northeast-seoul-admin-dong-business-candidate.g4a.r1";
        var rows = await db.NormalizedRecords.AsNoTracking().Where(item =>
            item.SourceId == SourceId
            && item.DatasetId == RejectedR1DatasetId
            && item.DataRevision == rejectedRevision).ToListAsync();
        Require(rows.Count == ExpectedCandidateRows, "RejectedR1LedgerCountChanged");

        var snapshotPrefix = RejectedR1DatasetId + "-";
        var snapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            item.SourceId == SourceId && item.DatasetId.StartsWith(snapshotPrefix)).ToListAsync();
        Require(snapshots.Count == 19, "RejectedR1SnapshotSetChanged");
        var runIds = snapshots.Select(item => item.FirstCollectionRunId).Distinct().ToList();
        var runs = await db.IngestionRuns.AsNoTracking().Where(item => runIds.Contains(item.Id)).ToListAsync();
        Require(runs.Count == 19 && runs.Count == runIds.Count, "RejectedR1RunSetChanged");

        return new RejectedR1Summary(rows.Count, snapshots.Count, runs.Count,
            ComputePersistedStateHash(
                "admin-dong-business-g4a-r1-full-persisted-state.v1",
                rows,
                snapshots,
                runs));
    }

    private static async Task<LegacySummary> ReadLegacySummaryAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        IReadOnlyCollection<Candidate> candidates)
    {
        await using var db = new PublicDataIngestionDbContext(options);
        return await ReadLegacySummaryAsync(db, candidates);
    }

    private static async Task<LegacySummary> ReadLegacySummaryAsync(
        PublicDataIngestionDbContext db,
        IReadOnlyCollection<Candidate> candidates)
    {
        const string legacyRevision = "myeonmok-business-20260908.r1";
        const string shopSource = "semas-commercial-listing";
        const string shopDataset = "data-go-kr-15083033";
        const string factorySource = "seoul-jungnang-open-data";
        const string factoryDataset = "jungnang-registered-factory-15034963";
        var rows = await db.NormalizedRecords.AsNoTracking().Where(item =>
            item.DataRevision == legacyRevision
            && ((item.SourceId == shopSource && item.DatasetId == shopDataset)
                || (item.SourceId == factorySource && item.DatasetId == factoryDataset))).ToListAsync();
        var shops = rows.Where(item => item.SourceId == shopSource && item.DatasetId == shopDataset).ToArray();
        var factories = rows.Where(item => item.SourceId == factorySource && item.DatasetId == factoryDataset).ToArray();
        Require(shops.Length == ExpectedLegacyShopRows && factories.Length == ExpectedLegacyFactoryRows,
            "LegacyMyeonmokLedgerCountChanged");
        var rawSnapshots = await db.RawSnapshots.AsNoTracking().Where(item =>
            (item.SourceId == shopSource && item.DatasetId == shopDataset)
            || (item.SourceId == factorySource && item.DatasetId == factoryDataset)).ToListAsync();
        var referencedRawIds = rows.Select(item => item.RawSnapshotId).Distinct().ToHashSet();
        Require(rawSnapshots.Count > 0
                && rawSnapshots.Select(item => item.Id).ToHashSet().SetEquals(referencedRawIds),
            "LegacyMyeonmokSnapshotSetChanged");
        var runIds = rawSnapshots.Select(item => item.FirstCollectionRunId).Distinct().ToList();
        var runs = await db.IngestionRuns.AsNoTracking().Where(item => runIds.Contains(item.Id)).ToListAsync();
        Require(runs.Count == runIds.Count, "LegacyMyeonmokRunSetChanged");

        var candidateProviderIds = candidates.Select(item => item.ProviderShopIdCanonical)
            .ToHashSet(StringComparer.Ordinal);
        var legacyProviderIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in shops)
        {
            using var payload = JsonDocument.Parse(row.TextValue);
            var provider = Text(payload.RootElement, "ProviderId");
            Require(ProviderIdPattern.IsMatch(provider) && legacyProviderIds.Add(provider),
                "LegacyProviderIdentityChanged");
        }
        var overlap = legacyProviderIds.Count(candidateProviderIds.Contains);
        Require(overlap == ExpectedLegacyOverlapRows
                && candidates.Count - overlap == ExpectedNewProviderRows,
            "LegacyProviderOverlapChanged");
        return new LegacySummary(shops.Length, factories.Length, rawSnapshots.Count, runs.Count,
            ComputePersistedStateHash(
                "myeonmok-business-ledger-full-persisted-state.v1",
                rows,
                rawSnapshots,
                runs));
    }

    private static string ComputePersistedStateHash(
        string domainSeparator,
        IEnumerable<외부데이터정규화Record> rows,
        IEnumerable<외부데이터RawSnapshot> snapshots,
        IEnumerable<외부데이터수집Run> runs)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendFramed(hash, domainSeparator);
        foreach (var item in rows.OrderBy(value => value.SourceId, StringComparer.Ordinal)
                     .ThenBy(value => value.DatasetId, StringComparer.Ordinal)
                     .ThenBy(value => value.RecordKey, StringComparer.Ordinal).ThenBy(value => value.Id))
        {
            AppendFramed(hash, item.Id); AppendFramed(hash, item.RawSnapshotId); AppendFramed(hash, item.RecordKey);
            AppendFramed(hash, item.StableId); AppendFramed(hash, item.SourceId); AppendFramed(hash, item.DatasetId);
            AppendFramed(hash, item.RegionStableId); AppendFramed(hash, item.MetricCode);
            AppendFramed(hash, item.NumericValue?.ToString(CultureInfo.InvariantCulture)); AppendFramed(hash, item.TextValue);
            AppendFramed(hash, item.UnitCode); AppendFramed(hash, Time(item.EvidenceAsOfUtc));
            AppendFramed(hash, Time(item.CollectedAtUtc)); AppendFramed(hash, item.SpatialPrecisionCode);
            AppendFramed(hash, item.TemporalPrecisionCode); AppendFramed(hash, item.QualityCode);
            AppendFramed(hash, item.LimitationCode); AppendFramed(hash, item.DimensionKey);
            AppendFramed(hash, item.SourceVersion); AppendFramed(hash, item.DataRevision);
            AppendFramed(hash, Time(item.FirstSeenAtUtc)); AppendFramed(hash, Time(item.LastSeenAtUtc));
        }
        foreach (var item in snapshots.OrderBy(value => value.SourceId, StringComparer.Ordinal)
                     .ThenBy(value => value.DatasetId, StringComparer.Ordinal)
                     .ThenBy(value => value.ContentHashSha256, StringComparer.Ordinal).ThenBy(value => value.Id))
        {
            AppendFramed(hash, item.Id); AppendFramed(hash, item.FirstCollectionRunId);
            AppendFramed(hash, item.SourceId); AppendFramed(hash, item.DatasetId); AppendFramed(hash, item.SourceVersion);
            AppendFramed(hash, Time(item.CollectedAtUtc));
            AppendFramed(hash, item.EvidenceAsOfUtc is null ? null : Time(item.EvidenceAsOfUtc.Value));
            AppendFramed(hash, item.ContentHashSha256); AppendFramed(hash, item.ContentLength);
            AppendFramed(hash, item.ContentType); AppendFramed(hash, item.OriginalFileName);
            AppendFramed(hash, item.StorageContainer); AppendFramed(hash, item.StorageObjectName);
            AppendFramed(hash, item.StorageLocation); AppendFramed(hash, Time(item.FirstSeenAtUtc));
            AppendFramed(hash, Time(item.LastSeenAtUtc));
        }
        foreach (var item in runs.OrderBy(value => value.SourceId, StringComparer.Ordinal)
                     .ThenBy(value => value.DatasetId, StringComparer.Ordinal)
                     .ThenBy(value => value.RunKey, StringComparer.Ordinal).ThenBy(value => value.Id))
        {
            AppendFramed(hash, item.Id); AppendFramed(hash, item.RunKey); AppendFramed(hash, item.SourceId);
            AppendFramed(hash, item.DatasetId); AppendFramed(hash, item.StatusCode);
            AppendFramed(hash, Time(item.StartedAtUtc));
            AppendFramed(hash, item.CompletedAtUtc is null ? null : Time(item.CompletedAtUtc.Value));
            AppendFramed(hash, item.AttemptCount); AppendFramed(hash, item.FetchedCount);
            AppendFramed(hash, item.NormalizedCount); AppendFramed(hash, item.RejectedCount);
            AppendFramed(hash, item.InsertedCount); AppendFramed(hash, item.UpdatedCount);
            AppendFramed(hash, item.ExistingCount); AppendFramed(hash, item.SourceVersion);
            AppendFramed(hash, item.DataRevision); AppendFramed(hash, item.ErrorCode); AppendFramed(hash, item.ErrorSummary);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void SetLegacySummary(Dictionary<string, object?> result, LegacySummary value)
    {
        result["legacyMyeonmokShopRowsPreserved"] = value.ShopRows;
        result["legacyFactoryRowsPreserved"] = value.FactoryRows;
        result["legacyRawSnapshotsPreserved"] = value.RawSnapshots;
        result["legacyIngestionRunsPreserved"] = value.IngestionRuns;
        result["legacyStateHashSha256"] = value.StateHashSha256.ToLowerInvariant();
        result["legacyProviderOverlapRows"] = ExpectedLegacyOverlapRows;
        result["newProviderRows"] = ExpectedNewProviderRows;
        result["legacyLedgerMutated"] = false;
    }

    private static void SetRejectedR1Summary(Dictionary<string, object?> result, RejectedR1Summary value)
    {
        result["rejectedR1CandidateRowsPreserved"] = value.CandidateRows;
        result["rejectedR1RawSnapshotsPreserved"] = value.RawSnapshots;
        result["rejectedR1IngestionRunsPreserved"] = value.IngestionRuns;
        result["rejectedR1StateHashSha256"] = value.StateHashSha256.ToLowerInvariant();
        result["rejectedR1LedgerMutated"] = false;
    }

    private static int SelfTest(Bundle bundle, IReadOnlyList<외부데이터정규화Record> rows)
    {
        var tests = 0;
        void Check(bool condition, string code)
        {
            Require(condition, "SelfTestFailed:" + code);
            tests++;
        }

        Check(ComputeCandidateSetHash(bundle.HeaderHashValues, bundle.Candidates) == CandidateSetHash,
            "PythonCSharpCandidateHashParity");
        Check(rows.Count == ExpectedCandidateRows, "RowCount");
        Check(rows.Max(item => item.TextValue.Length) <= 2_000, "ProtectedPayloadBudget");
        Check(rows.All(item => item.NumericValue is null && item.RawSnapshotId == 0), "PrePersistenceShape");
        Check(FrameDigest(["ab", "c"]) != FrameDigest(["a", "bc"]), "LengthFraming");
        Check(FrameDigest([null]) != FrameDigest([string.Empty]), "NullPresenceFraming");
        Check(FrameDigest([false]) != FrameDigest([true]), "BooleanFraming");

        var expectedByStable = bundle.Candidates.ToDictionary(item => item.StableId, StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var actual = CandidateFromStoredPayload(row);
            Check(expectedByStable.TryGetValue(row.StableId, out var expected), "PayloadStableIdentity");
            Check(HashValueSequenceEqual(actual.HashValues, expected!.HashValues), "PayloadExactHashValues");
            foreach (var name in ProtectedSourcePropertyOrder)
                Require(actual.ProtectedSource[name] == expected.ProtectedSource[name],
                    "SelfTestFailed:ProtectedFieldRoundTrip");
        }
        tests += 2;

        var sample = bundle.Candidates[0];
        foreach (var name in ProtectedSourcePropertyOrder)
        {
            Check(sample.ProtectedSource.ContainsKey(name), "ProtectedFieldPresent");
            var index = FindHashValueIndex(sample.HashValues, "protectedSource." + name);
            var changed = sample.HashValues.ToArray();
            changed[index] = (changed[index] as string ?? string.Empty) + "#";
            Check(!HashValueSequenceEqual(sample.HashValues, changed), "ProtectedFieldMutationDetected");
        }
        Check(BuildRegistrationInputs(bundle).Count == 19, "RegistrationInputCount");
        Check(BuildRegistrationInputs(bundle).All(item => item.VirtualArchiveEntry
            || File.Exists(item.Path) && HashFile(item.Path) == item.Hash), "RegistrationInputHash");
        Check(!ProviderIdLeakPattern.IsMatch(JsonSerializer.Serialize(new
        {
            candidateSetHashSha256 = CandidateSetHash,
            candidateRows = ExpectedCandidateRows,
            authority = "PrivateReviewOnly"
        }, CompactJson)), "SafeSummaryBoundary");
        return tests;
    }

    private static int FindHashValueIndex(object?[] values, string wantedField)
    {
        var index = 0;
        foreach (var field in CandidateHashFieldOrder)
        {
            if (field.EndsWith("[]", StringComparison.Ordinal))
            {
                Require(values[index] is long count && count >= 0 && count <= 100,
                    "HashValueArrayCountInvalid");
                index += 1 + (int)(long)values[index]!;
                continue;
            }
            if (field == wantedField) return index;
            index++;
        }
        throw new InvalidDataException("AdministrativeDongBusinessCandidate:HashFieldNotFound");
    }

    private static void SetSummary(
        Dictionary<string, object?> result,
        string mode,
        Bundle bundle,
        IReadOnlyCollection<외부데이터정규화Record> rows)
    {
        result["mode"] = mode;
        result["status"] = "Prepared";
        result["revision"] = Revision;
        result["candidateSetHashSha256"] = CandidateSetHash.ToLowerInvariant();
        result["generationRelativePath"] = GenerationRelative;
        result["candidateRows"] = bundle.Candidates.Count;
        result["foodRows"] = bundle.Candidates.Count(item => item.FoodCategory);
        result["administrativeAreas"] = ExpectedAreaCount;
        result["historicalBoundaryDiagnosticRows"] = ExpectedBoundaryDiagnosticRows;
        result["missingBuildingManagementNumberRows"] = ExpectedMissingBuildingRows;
        result["identityCandidateGroups"] = ExpectedIdentityGroups;
        result["identityCandidateRows"] = ExpectedIdentityRows;
        result["maximumProtectedPayloadCharacters"] = rows.Max(item => item.TextValue.Length);
        result["privateReviewOnly"] = true;
        result["currentAdministrativeBoundaryEstablished"] = false;
        result["sourceCoordinateDatumEstablished"] = false;
        result["exactBuildingBindingEstablished"] = false;
        result["currentOperationVerified"] = false;
        result["merchantClaimVerified"] = false;
        result["distributionApproved"] = false;
        result["publicDisplayAllowed"] = false;
        result["orderScenarioEligible"] = false;
        result["runtimeAuthorized"] = false;
        result["gameplayReady"] = false;
        result["unityApplyAllowed"] = false;
        result["mongoChanged"] = false;
        result["unityChanged"] = false;
    }

    private static string ComputeCandidateSetHash(
        IEnumerable<object?> headerValues,
        IEnumerable<Candidate> candidates)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in headerValues) AppendFramed(hash, value);
        string? prior = null;
        foreach (var candidate in candidates)
        {
            Require(prior is null
                    || StringComparer.Ordinal.Compare(prior, candidate.ProviderShopIdCanonical) < 0,
                "CandidateHashOrderChanged");
            prior = candidate.ProviderShopIdCanonical;
            foreach (var value in candidate.HashValues) AppendFramed(hash, value);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string FrameDigest(IEnumerable<object?> values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in values) AppendFramed(hash, value);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendFramed(IncrementalHash hash, object? value)
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
            _ => throw new InvalidDataException("AdministrativeDongBusinessCandidate:UnsupportedHashValue")
        };
        var bytes = Encoding.UTF8.GetBytes(text);
        header[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(header[1..], checked((uint)bytes.Length));
        hash.AppendData(header);
        hash.AppendData(bytes);
    }

    private static bool HashValueSequenceEqual(IReadOnlyList<object?> left, IReadOnlyList<object?> right)
    {
        if (left.Count != right.Count) return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (left[index] is null || right[index] is null)
            {
                if (left[index] is not null || right[index] is not null) return false;
                continue;
            }
            if (left[index]!.GetType() != right[index]!.GetType()
                || !left[index]!.Equals(right[index])) return false;
        }
        return true;
    }

    private static string ContractHash(JsonElement value)
        => Convert.ToHexString(SHA256.HashData(CanonicalJsonBytes(value, false)));

    private static string ContentHash(JsonElement value)
        => Convert.ToHexString(SHA256.HashData(CanonicalJsonBytes(value, true)));

    private static byte[] CanonicalJsonBytes(JsonElement value, bool blankRootContentHash)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false
        }))
        {
            WriteCanonical(writer, value, blankRootContentHash);
        }
        return output.ToArray();
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
                    if (blankRootContentHash && property.Name == "contentHashSha256") writer.WriteStringValue(string.Empty);
                    else WriteCanonical(writer, property.Value);
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
                writer.WriteRawValue(value.GetRawText(), true);
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
                throw new InvalidDataException("AdministrativeDongBusinessCandidate:CanonicalJsonValueInvalid");
        }
    }

    private static object? CanonicalScalar(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            _ => throw new InvalidDataException("AdministrativeDongBusinessCandidate:CanonicalScalarInvalid")
        };

    private static JsonElement Nested(JsonElement value, string path)
    {
        foreach (var component in path.Split('.', StringSplitOptions.None))
        {
            Require(value.ValueKind == JsonValueKind.Object && value.TryGetProperty(component, out value),
                "CandidateNestedFieldMissing");
        }
        return value;
    }

    private static void ValidateNdjsonEnvelope(string path)
    {
        Require(File.Exists(path) && !IsReparsePoint(path), "CandidateFileMissingOrUnsafe");
        using var stream = File.OpenRead(path);
        Require(stream.Length is > 0 and < 128_000_000, "CandidateFileSizeInvalid");
        Span<byte> prefix = stackalloc byte[3];
        var prefixRead = stream.Read(prefix);
        Require(prefixRead == 3 && !(prefix[0] == 0xEF && prefix[1] == 0xBB && prefix[2] == 0xBF),
            "CandidateUtf8BomRejected");
        stream.Position = stream.Length - 1;
        Require(stream.ReadByte() == (byte)'\n', "CandidateFinalLfMissing");
        stream.Position = 0;
        var buffer = new byte[1 << 16];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            Require(Array.IndexOf(buffer, (byte)'\r', 0, read) < 0, "CandidateCrLineEndingRejected");
    }

    private static bool FilesEqual(string left, string right)
    {
        var leftInfo = new FileInfo(left);
        var rightInfo = new FileInfo(right);
        if (leftInfo.Length != rightInfo.Length) return false;
        using var first = File.OpenRead(left);
        using var second = File.OpenRead(right);
        var leftBuffer = new byte[1 << 16];
        var rightBuffer = new byte[1 << 16];
        while (true)
        {
            var leftRead = first.Read(leftBuffer, 0, leftBuffer.Length);
            var rightRead = second.Read(rightBuffer, 0, rightBuffer.Length);
            if (leftRead != rightRead) return false;
            if (leftRead == 0) return true;
            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead))) return false;
        }
    }

    private static void RequireFrozenConstants()
    {
        foreach (var hash in new[]
        {
            DesignHash, ScopeDefinitionHash, GeneratorHash, CandidateSetHash, ManifestHash, CandidatesHash,
            AuditHash, GeneratorSourceHash, CompleteHash
        })
            Require(Regex.IsMatch(hash, "^[0-9A-F]{64}$", RegexOptions.CultureInvariant),
                "FrozenHashConstantInvalid");
        Require(GenerationRelative.EndsWith('/' + CandidateSetHash.ToLowerInvariant(), StringComparison.Ordinal),
            "FrozenGenerationPathInvalid");
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
        Require(!IsReparsePoint(normalizedRoot) && !IsReparsePoint(path), "RepositoryReparsePointRejected");
        var cursor = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(cursor))
        {
            Require(!IsReparsePoint(cursor), "RepositoryReparsePointRejected");
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
            ".csv" => "text/csv",
            ".py" => "text/x-python",
            ".ndjson" => "application/x-ndjson",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };

    private static bool IsReparsePoint(string path)
        => File.Exists(path) || Directory.Exists(path)
            ? (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
            : false;

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return HashStream(stream);
    }

    private static string HashStream(Stream stream)
        => Convert.ToHexString(SHA256.HashData(stream));

    private static string Text(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? string.Empty
            : throw new InvalidDataException("AdministrativeDongBusinessCandidate:JsonTextContract");

    private static int Int(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongBusinessCandidate:JsonIntegerContract");

    private static long Long(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.TryGetInt64(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongBusinessCandidate:JsonLongContract");

    private static bool Bool(JsonElement value, string property)
        => value.TryGetProperty(property, out var item)
           && item.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? item.GetBoolean()
            : throw new InvalidDataException("AdministrativeDongBusinessCandidate:JsonBooleanContract");

    private static void RequireExactPropertyOrder(JsonElement value, IEnumerable<string> expected, string code)
    {
        var names = expected.ToArray();
        Require(value.ValueKind == JsonValueKind.Object
                && value.EnumerateObject().Select(item => item.Name).SequenceEqual(names), code);
    }

    private static void RequireExactProperties(JsonElement value, IEnumerable<string> expected, string code)
    {
        var names = expected.ToArray();
        Require(value.ValueKind == JsonValueKind.Object
                && value.EnumerateObject().Count() == names.Length
                && value.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(names), code);
    }

    private static DateTimeOffset DbTime(DateTimeOffset value)
        => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);

    private static string Time(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException("AdministrativeDongBusinessCandidate:" + code);
    }
}
