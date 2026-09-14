using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.DeliveryZones;

// 현행 경계 정본이 아닌 역사적 공식 경계로 30개 행정동 투영을 재현한다.
// 결과는 API current 컬렉션에 절대 게시하지 않고 불변 candidate 컬렉션에만 저장한다.
internal static class 행정동디오라마Batch
{
    private const string ArtifactRelative =
        "artifacts/local/public-data/admin-dong-diorama-northeast-seoul-20260914-r2";
    private const string BoundaryRelative =
        "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip";
    private const string BuildingRelative =
        "artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip";
    private const string NodeLinkRelative =
        "artifacts/local/public-data/sagajeong-nodelink-20260912-r1/nodelink.zip";
    private const string ScopeDefinitionRelative =
        "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json";
    private const string ProjectionBuilderRelative =
        "Ssalddel/Services/WorldProjection/AdministrativeDongDiorama/행정동디오라마ProjectionBuilder.cs";
    private const string UnityReviewExporterRelative =
        "eng/Ssalddel.PublicDataPortalImport/행정동디오라마Batch.cs";
    private const string UnityReviewExportRelative =
        "artifacts/local/validation/admin-dong-diorama-base-review/r1/input";

    private const string ScopeSchemaVersion = "administrative-dong-diorama-batch-scope.v1";
    private const string ModuleSchemaVersion = "administrative-dong-diorama-batch-input.v1";
    private const string AuditSchemaVersion = "administrative-dong-diorama-batch-audit.v1";
    private const string UnityReviewIndexSchemaVersion =
        "administrative-dong-diorama-unity-review-index.v1";
    private const string UnityReviewBundleSchemaVersion =
        "administrative-dong-diorama-unity-review-bundle.v1";
    private const string UnityReviewCompletionSchemaVersion =
        "administrative-dong-diorama-unity-review-completion.v1";
    private const string UnityReviewExporterSemanticRevision =
        "administrative-dong-diorama-unity-review-exporter.r2";
    private const string ScopeStableId = "scope:administrative-dong-diorama:northeast-seoul-rider:r2";
    private const string Revision = "northeast-seoul-administrative-dong-dioramas.r2";
    private const string SourceVintage =
        "oa22160-file-20231031+al-d010-20260809+nodelink-20260812+mois-jscode-20260301+projection-r2";
    private const string DesiredReadiness = AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary;
    private const string AssignmentMethod = "SourceGeometryPointOnSurfaceWithinUniqueAdministrativeBoundary";
    private const string AssignmentConfidence = "UniqueHistoricalBoundaryMatch";
    private const string AssignmentBoundaryRevision =
        "seoul-oa22160:file-modified:20231031:retrieved:20260912";
    private const string ProjectionBuilderSemanticRevision =
        "administrative-dong-diorama-projection-builder.r2";
    private const double IndependentBoundaryComparisonToleranceMeters = 0.002d;

    private const string BoundaryHash =
        "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68";
    private const string BuildingHash =
        "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755";
    private const string NodeLinkHash =
        "5BBF5A01D677B6DCB941CC5954FCC256D6DED60D96F95336A23FC2B53B39D4E4";
    private const string CrosswalkHash =
        "3EC5188469881A9EC9BB23913F553BFBC3488DF72B8BD0FC378A3C1CACC3B905";

    private const string BatchCollection = "administrative_dong_diorama_candidate_batches";
    private const string ManifestCollection = "administrative_dong_diorama_candidate_manifests";
    private const string TileCollection = "administrative_dong_diorama_candidate_tiles";
    private const string OverlayCollection = "administrative_dong_diorama_candidate_overlays";
    private const string CurrentCollection = "administrative_dong_diorama_current";
    private const int MongoMaximumDocumentBytes = 16 * 1024 * 1024;

    private static readonly DateTime GeneratedAtUtc =
        new(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] RequiredDiagnosticCodes =
    [
        "CurrentAuthoritativeBoundaryUnavailable",
        "HistoricalBoundaryBootstrapOnly",
        "SupersedesGeometryUnsafeR1Candidate",
        "DistributionNotApproved",
        "TraversalNotReady",
        "GameplayNotReady"
    ];

    private static readonly string[] MissingLifeLayerCodes =
    [
        "CurrentAdministrativeBoundary",
        "RoadAddress",
        "ParcelIdentifierExport",
        "ParcelGeometry",
        "BuildingEntrance",
        "RoadWidth",
        "AlleyNetwork",
        "SidewalkNetwork",
        "LaneDirection",
        "IntersectionControl",
        "TrafficSignal",
        "PublicBusinessSpatialBinding",
        "LivingPopulationBinding",
        "NpcMovementBinding",
        "OperationalLifecycleBinding",
        "HRoleAreaSetBinding"
    ];

    private static readonly HashSet<string> ForbiddenUnityReviewPropertyNames = new(
        [
            "publicBusinesses",
            "businessName",
            "roadAddress",
            "jibunAddress",
            "lotAddress",
            "detailAddress",
            "detailedAddress",
            "phoneNumber",
            "contactName",
            "contactPhone",
            "emailAddress",
            "applicantName",
            "ownerName",
            "representativeName",
            "claim",
            "campaign"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly AreaDefinition[] Areas =
    [
        new("region:kr:hjd:1121574000", "서울특별시 광진구 중곡제1동", "region:kr:bjd:1121510100"),
        new("region:kr:hjd:1121575000", "서울특별시 광진구 중곡제2동", "region:kr:bjd:1121510100"),
        new("region:kr:hjd:1121576000", "서울특별시 광진구 중곡제3동", "region:kr:bjd:1121510100"),
        new("region:kr:hjd:1121577000", "서울특별시 광진구 중곡제4동", "region:kr:bjd:1121510100"),
        new("region:kr:hjd:1123056000", "서울특별시 동대문구 전농제1동", "region:kr:bjd:1123010400"),
        new("region:kr:hjd:1123057000", "서울특별시 동대문구 전농제2동", "region:kr:bjd:1123010400"),
        new("region:kr:hjd:1123060000", "서울특별시 동대문구 답십리제1동", "region:kr:bjd:1123010500"),
        new("region:kr:hjd:1123061000", "서울특별시 동대문구 답십리제2동", "region:kr:bjd:1123010500"),
        new("region:kr:hjd:1123065000", "서울특별시 동대문구 장안제1동", "region:kr:bjd:1123010600"),
        new("region:kr:hjd:1123066000", "서울특별시 동대문구 장안제2동", "region:kr:bjd:1123010600"),
        new("region:kr:hjd:1123072000", "서울특별시 동대문구 휘경제1동", "region:kr:bjd:1123010900"),
        new("region:kr:hjd:1123073000", "서울특별시 동대문구 휘경제2동", "region:kr:bjd:1123010900"),
        new("region:kr:hjd:1123074000", "서울특별시 동대문구 이문제1동", "region:kr:bjd:1123011000"),
        new("region:kr:hjd:1123075000", "서울특별시 동대문구 이문제2동", "region:kr:bjd:1123011000"),
        new("region:kr:hjd:1126052000", "서울특별시 중랑구 면목제2동", "region:kr:bjd:1126010100"),
        new("region:kr:hjd:1126054000", "서울특별시 중랑구 면목제4동", "region:kr:bjd:1126010100"),
        new("region:kr:hjd:1126055000", "서울특별시 중랑구 면목제5동", "region:kr:bjd:1126010100"),
        new("region:kr:hjd:1126056500", "서울특별시 중랑구 면목본동", "region:kr:bjd:1126010100"),
        new("region:kr:hjd:1126057000", "서울특별시 중랑구 면목제7동", "region:kr:bjd:1126010100"),
        new("region:kr:hjd:1126057500", "서울특별시 중랑구 면목제3.8동", "region:kr:bjd:1126010100"),
        new("region:kr:hjd:1126058000", "서울특별시 중랑구 상봉제1동", "region:kr:bjd:1126010200"),
        new("region:kr:hjd:1126059000", "서울특별시 중랑구 상봉제2동", "region:kr:bjd:1126010200"),
        new("region:kr:hjd:1126060000", "서울특별시 중랑구 중화제1동", "region:kr:bjd:1126010300"),
        new("region:kr:hjd:1126061000", "서울특별시 중랑구 중화제2동", "region:kr:bjd:1126010300"),
        new("region:kr:hjd:1126062000", "서울특별시 중랑구 묵제1동", "region:kr:bjd:1126010400"),
        new("region:kr:hjd:1126063000", "서울특별시 중랑구 묵제2동", "region:kr:bjd:1126010400"),
        new("region:kr:hjd:1126065500", "서울특별시 중랑구 망우본동", "region:kr:bjd:1126010500"),
        new("region:kr:hjd:1126066000", "서울특별시 중랑구 망우제3동", "region:kr:bjd:1126010500"),
        new("region:kr:hjd:1126068000", "서울특별시 중랑구 신내1동", "region:kr:bjd:1126010600"),
        new("region:kr:hjd:1126069000", "서울특별시 중랑구 신내2동", "region:kr:bjd:1126010600")
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    internal static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "preview" or "apply" or "verify" or "export-unity-review",
            "AdministrativeDongDioramaBatchModeInvalid");
        result["stage"] = "ValidateCandidatePackage";
        result["publishBlocked"] = true;
        result["currentPointerUpdated"] = false;
        result["desiredReadinessCode"] = DesiredReadiness;
        result["diagnosticCodes"] = RequiredDiagnosticCodes;
        result["distributionApproved"] = false;
        result["traversalReady"] = false;
        result["gameplayReady"] = false;

        var package = await ReadAndBuildAsync(root);
        var candidateDocuments = BuildCandidateDocuments(package);
        var candidateDocumentSizes = candidateDocuments.All.Select(document => document.ToBson().Length).ToArray();
        Require(candidateDocumentSizes.All(length => length < MongoMaximumDocumentBytes),
            "AdministrativeDongDioramaCandidateDocumentTooLarge");
        result["scopeStableId"] = ScopeStableId;
        result["revision"] = Revision;
        result["batchContentHashSha256"] = package.BatchContentHash;
        result["scopeContentHashSha256"] = package.ScopeContentHash;
        result["projectionSetHashSha256"] = package.ProjectionSetHash;
        result["projectionBuilderSemanticRevision"] = ProjectionBuilderSemanticRevision;
        result["projectionBuilderSourceHashSha256"] = package.ProjectionBuilderSourceHash;
        result["independentBoundaryComparisonToleranceMeters"] = IndependentBoundaryComparisonToleranceMeters;
        result["administrativeAreaCount"] = package.Projections.Length;
        result["legalAreaCount"] = package.LegalAreaCount;
        result["buildingCount"] = package.BuildingCount;
        result["projectedBuildingCount"] = package.ProjectedBuildingCount;
        result["builderRejectedBuildingCount"] = package.BuildingCount - package.ProjectedBuildingCount;
        result["roadInputSegmentCount"] = package.RoadInputCount;
        result["projectedRoadSegmentCount"] = package.ProjectedRoadCount;
        result["tileCount"] = package.TileCount;
        result["unresolvedBuildingCount"] = package.UnresolvedBuildingCount;
        result["unresolvedRepresentativePointCount"] = package.Audit.UnresolvedRepresentativePointCount;
        result["quarantinedMalformedBuildingCount"] = package.Audit.QuarantinedMalformedBuildingCount;
        result["quarantinedRoundedFootprintBuildingCount"] =
            package.Audit.QuarantinedRoundedFootprintBuildingCount;
        result["interiorRingBuildingCount"] = package.Audit.InteriorRingBuildingCount;
        result["interiorRingOmittedCount"] = package.Audit.InteriorRingOmittedCount;
        result["multipartOmittedBuildingCount"] = package.Audit.MultipartOmittedBuildingCount;
        result["buildingLegalAreaMismatchCount"] = package.Audit.BuildingLegalAreaMismatchCount;
        result["duplicateSourceBuildingIdOccurrenceCount"] = package.Audit.DuplicateSourceBuildingIdOccurrenceCount;
        result["v1ContractAssignmentAnchorOverrideCount"] = package.Audit.V1ContractAssignmentAnchorOverrideCount;
        result["directedRoadLinkCount"] = package.Audit.DirectedRoadLinkCount;
        result["administrativeDongRoadLinkOccurrenceCount"] = package.Audit.AdministrativeDongRoadLinkOccurrenceCount;
        result["crossBoundaryDirectedRoadLinkCount"] = package.Audit.CrossBoundaryDirectedRoadLinkCount;
        result["exactRoadBoundaryContainmentFailureSegmentCount"] =
            package.Audit.ExactRoadBoundaryContainmentFailureSegmentCount;
        result["roadBoundaryNumericToleranceMeters"] = package.Audit.RoadBoundaryNumericToleranceMeters;
        result["duplicateBuildingAcrossAdministrativeAreas"] = 0;
        result["publicBusinessCount"] = 0;
        result["semanticPlaceBindingCount"] = 0;
        result["operationalAreaBindingCount"] = 0;
        result["candidateBatchCount"] = 1;
        result["candidateManifestCount"] = package.Projections.Length;
        result["candidateTileCount"] = package.TileCount;
        result["candidateOverlayCount"] = package.Projections.Length;
        result["candidateDocumentCount"] = 1 + package.Projections.Length * 2 + package.TileCount;
        result["areas"] = package.Projections.OrderBy(item => item.Definition.Id, StringComparer.Ordinal)
            .Select(item => new
            {
                administrativeAreaStableId = item.Definition.Id,
                displayName = item.Definition.DisplayName,
                legalAreaStableId = item.Definition.LegalId,
                projectionHashSha256 = item.Projection.Manifest.ProjectionHashSha256,
                buildingCount = item.Input.Buildings.Length,
                projectedBuildingCount = item.Projection.Tiles.Sum(tile => tile.Buildings.Length),
                roadInputSegmentCount = item.Input.Roads.Length,
                projectedRoadSegmentCount = item.Projection.Tiles.Sum(tile => tile.Roads.Length),
                tileCount = item.Projection.Tiles.Length,
                unresolvedBuildingCount = item.Input.UnresolvedBuildingCount
            }).ToArray();
        result["historicalBoundaryProjectionReadinessCode"] =
            AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly;
        result["candidateEnvelopeIsPublicationAuthority"] = false;
        result["candidateEnvelopeReadinessGovernsPromotion"] = true;
        result["embeddedManifestReadinessIsInformational"] = true;
        result["maxCandidateDocumentBytes"] = candidateDocumentSizes.Max();
        result["mongoMaximumDocumentBytesExclusive"] = MongoMaximumDocumentBytes;
        result["sourceHashesVerified"] = true;
        result["localMoisLedgerVerified"] = true;
        result["administrativeCodeLedgerReadbackVerified"] = true;
        result["moisLedgerSourceVersion"] = package.Ledger.SourceVersion;
        result["moisLedgerDataRevision"] = package.Ledger.DataRevision;
        result["administrativeCodeSourceVersion"] = package.Ledger.SourceVersion;
        result["administrativeCodeDataRevision"] = package.Ledger.DataRevision;
        result["administrativeCodeEvidenceAsOfUtc"] = package.Ledger.EvidenceAsOfUtc;
        result["moisCrosswalkContentHashSha256"] = package.Ledger.CrosswalkHash;
        result["moduleHashesVerified"] = true;
        result["projectionInternalConsistencyVerified"] = true;

        if (mode == "export-unity-review")
        {
            result["stage"] = "ExportUnityPrivateReviewBundle";
            var export = await ExportUnityReviewAsync(root, package);
            result["candidateDatabaseWriteAttempted"] = false;
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            result["independentReadbackVerified"] = export.IndependentReadbackVerified;
            result["unityReviewIndexPath"] = export.IndexPath;
            result["unityReviewIndexSha256"] = export.IndexHash;
            result["unityReviewIndexContentHashSha256"] = export.IndexContentHash;
            result["unityReviewBundleSetHashSha256"] = export.BundleSetHash;
            result["unityReviewGenerationHashSha256"] = export.GenerationHash;
            result["unityReviewCompletionMarkerPath"] = export.CompletionMarkerPath;
            result["unityReviewCompletionMarkerSha256"] = export.CompletionMarkerHash;
            result["unityReviewExporterSemanticRevision"] = export.ExporterSemanticRevision;
            result["unityReviewExporterSourceHashSha256"] = export.ExporterSourceHash;
            result["unityReviewBundleCount"] = export.BundleCount;
            result["unityReviewChangedFileCount"] = export.ChangedFileCount;
            result["unityReviewByteLength"] = export.ByteLength;
            result["baseGeometryCaptureReadyCount"] = export.BundleCount;
            result["lifeDioramaCaptureReadyCount"] = 0;
            result["currentPointerUsed"] = false;
            result["personalDataIncluded"] = false;
            result["stage"] = "Complete";
            return;
        }

        if (mode == "preview")
        {
            result["candidateDatabaseWriteAttempted"] = false;
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            result["independentReadbackVerified"] = false;
            result["stage"] = "Complete";
            return;
        }

        if (mode == "apply")
        {
            result["stage"] = "ConnectCandidateDatabase";
            var connection = await 공간자료CatalogImport.ConnectAsync(root);
            var before = await ReadCurrentPointerSnapshotAsync(connection.Client, package.AreaIds);
            var documents = candidateDocuments;

            result["stage"] = "InsertOrVerifyCandidateDocuments";
            var writes = await InsertOrVerifyAllAsync(connection.Client, documents);
            var after = await ReadCurrentPointerSnapshotAsync(connection.Client, package.AreaIds);
            Require(before.Hash == after.Hash && before.Count == after.Count,
                "AdministrativeDongDioramaCurrentPointerChangedDuringCandidateApply");
            await RequireNoCandidateProjectionIsCurrentAsync(connection.Client, package);

            result["candidateDatabaseWriteAttempted"] = true;
            result["databaseWriteAttempted"] = true;
            result["committed"] = true;
            result["candidateDocumentsInserted"] = writes.Inserted;
            result["candidateDocumentsVerifiedExisting"] = writes.Verified;
            result["currentPointerCount"] = after.Count;
            result["currentPointerSnapshotHashSha256"] = after.Hash;

            result["stage"] = "IndependentReadback";
            await VerifyReadbackAsync(root, package);
            result["independentReadbackVerified"] = true;
        }
        else
        {
            result["candidateDatabaseWriteAttempted"] = false;
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            result["stage"] = "IndependentReadback";
            var connection = await 공간자료CatalogImport.ConnectAsync(root);
            var current = await ReadCurrentPointerSnapshotAsync(connection.Client, package.AreaIds);
            await VerifyCandidateDocumentsAsync(connection.Client, package);
            await RequireNoCandidateProjectionIsCurrentAsync(connection.Client, package);
            result["currentPointerCount"] = current.Count;
            result["currentPointerSnapshotHashSha256"] = current.Hash;
            result["independentReadbackVerified"] = true;
        }

        result["currentPointerUpdated"] = false;
        result["stage"] = "Complete";
    }

    private static async Task<UnityReviewExportResult> ExportUnityReviewAsync(
        string root,
        CandidatePackage package)
    {
        Require(package.Projections.Length == Areas.Length,
            "AdministrativeDongDioramaUnityReviewAreaCountMismatch");
        Require(package.Projections.All(item =>
                item.Projection.Manifest.PublicBusinessMarkerCount == 0
                && item.Projection.Manifest.OperationalAreaStableIds.Length == 0
                && item.Projection.Manifest.SemanticPlaceStableIds.Length == 0
                && item.Projection.DisplayOverlays.Items.Length == 0),
            "AdministrativeDongDioramaUnityReviewPrivateOrBusinessDataDetected");

        var exporterPath = Path.GetFullPath(Path.Combine(root, UnityReviewExporterRelative));
        SafeRead(exporterPath, root, 4 * 1024 * 1024);
        var exporterSourceHash = HashFile(exporterPath);
        Require(IsSha256(exporterSourceHash),
            "AdministrativeDongDioramaUnityReviewExporterSourceHashInvalid");

        var candidateBatchId = BatchId(package.BatchContentHash);
        var artifacts = new List<UnityReviewBundleArtifact>(package.Projections.Length);
        foreach (var item in package.Projections.OrderBy(value => value.Definition.Id, StringComparer.Ordinal))
        {
            var code = AdministrativeAreaCode(item.Definition.Id);
            var relativePath = $"bundles/{code}.json";
            var tiles = item.Projection.Tiles
                .OrderBy(tile => tile.TileStableId, StringComparer.Ordinal)
                .ToArray();
            var bundle = new UnityReviewBundle
            {
                ScopeStableId = ScopeStableId,
                Revision = Revision,
                SourceVintage = SourceVintage,
                GeneratedAtUtc = GeneratedAtUtc,
                CandidateBatchId = candidateBatchId,
                BatchContentHashSha256 = package.BatchContentHash,
                ScopeContentHashSha256 = package.ScopeContentHash,
                ProjectionSetHashSha256 = package.ProjectionSetHash,
                ProjectionBuilderSemanticRevision = ProjectionBuilderSemanticRevision,
                ProjectionBuilderSourceHashSha256 = package.ProjectionBuilderSourceHash,
                ExporterSemanticRevision = UnityReviewExporterSemanticRevision,
                ExporterSourceHashSha256 = exporterSourceHash,
                AdministrativeAreaStableId = item.Definition.Id,
                DisplayName = item.Definition.DisplayName,
                LegalAreaStableId = item.Definition.LegalId,
                ModuleFileHashSha256 = item.FileHash,
                ModuleContentHashSha256 = item.ContentHash,
                ProjectionHashSha256 = item.Projection.Manifest.ProjectionHashSha256,
                DesiredReadinessCode = DesiredReadiness,
                DiagnosticCodes = [.. RequiredDiagnosticCodes],
                EmbeddedManifestReadinessCode = item.Projection.Manifest.ReadinessCode,
                MissingLayerCodes = [.. MissingLifeLayerCodes],
                BuildingCount = tiles.Sum(tile => tile.Buildings.Length),
                RoadSegmentCount = tiles.Sum(tile => tile.Roads.Length),
                TileCount = tiles.Length,
                UnresolvedBuildingCount = item.Input.UnresolvedBuildingCount,
                Manifest = item.Projection.Manifest,
                Tiles = tiles,
                DisplayOverlays = item.Projection.DisplayOverlays
            };
            var contentHash = HashCanonicalWithBlankContentHash(
                JsonSerializer.SerializeToElement(bundle, JsonOptions));
            bundle = bundle with { ContentHashSha256 = contentHash };
            var bytes = SerializeUnityReviewDocument(bundle, contentHash);
            var entry = new UnityReviewIndexEntry
            {
                AdministrativeAreaStableId = item.Definition.Id,
                DisplayName = item.Definition.DisplayName,
                LegalAreaStableId = item.Definition.LegalId,
                RelativePath = relativePath,
                Sha256 = Hash(bytes),
                ByteLength = bytes.LongLength,
                ContentHashSha256 = contentHash,
                ModuleFileHashSha256 = item.FileHash,
                ModuleContentHashSha256 = item.ContentHash,
                ProjectionHashSha256 = item.Projection.Manifest.ProjectionHashSha256,
                BuildingCount = bundle.BuildingCount,
                RoadSegmentCount = bundle.RoadSegmentCount,
                TileCount = bundle.TileCount,
                UnresolvedBuildingCount = bundle.UnresolvedBuildingCount,
                BaseGeometryCaptureReady = true,
                LifeDioramaCaptureReady = false,
                MissingLayerCodes = [.. MissingLifeLayerCodes]
            };
            artifacts.Add(new UnityReviewBundleArtifact(entry, bundle, bytes));
        }

        var entries = artifacts.Select(item => item.Entry).ToArray();
        var bundleSetHash = HashCanonical(JsonSerializer.SerializeToElement(entries, JsonOptions));
        var index = new UnityReviewIndex
        {
            ScopeStableId = ScopeStableId,
            Revision = Revision,
            SourceVintage = SourceVintage,
            GeneratedAtUtc = GeneratedAtUtc,
            CandidateBatchId = candidateBatchId,
            BatchContentHashSha256 = package.BatchContentHash,
            ScopeContentHashSha256 = package.ScopeContentHash,
            ProjectionSetHashSha256 = package.ProjectionSetHash,
            ProjectionBuilderSemanticRevision = ProjectionBuilderSemanticRevision,
            ProjectionBuilderSourceHashSha256 = package.ProjectionBuilderSourceHash,
            ExporterSemanticRevision = UnityReviewExporterSemanticRevision,
            ExporterSourceHashSha256 = exporterSourceHash,
            GenerationHashSha256 = bundleSetHash,
            BundleSetHashSha256 = bundleSetHash,
            DesiredReadinessCode = DesiredReadiness,
            DiagnosticCodes = [.. RequiredDiagnosticCodes],
            MissingLayerCodes = [.. MissingLifeLayerCodes],
            AdministrativeAreaCount = package.Projections.Length,
            LegalAreaCount = package.LegalAreaCount,
            BundleCount = artifacts.Count,
            BuildingCount = package.ProjectedBuildingCount,
            RoadSegmentCount = package.ProjectedRoadCount,
            TileCount = package.TileCount,
            UnresolvedBuildingCount = package.UnresolvedBuildingCount,
            PublicBusinessCount = 0,
            DisplayOverlayItemCount = 0,
            Bundles = entries
        };
        var indexContentHash = HashCanonicalWithBlankContentHash(
            JsonSerializer.SerializeToElement(index, JsonOptions));
        index = index with { ContentHashSha256 = indexContentHash };
        var indexBytes = SerializeUnityReviewDocument(index, indexContentHash);

        var completion = new UnityReviewCompletion
        {
            ScopeStableId = ScopeStableId,
            Revision = Revision,
            GeneratedAtUtc = GeneratedAtUtc,
            GenerationHashSha256 = bundleSetHash,
            IndexRelativePath = "index.json",
            IndexSha256 = Hash(indexBytes),
            IndexContentHashSha256 = indexContentHash,
            BundleSetHashSha256 = bundleSetHash,
            BundleCount = artifacts.Count,
            ExporterSemanticRevision = UnityReviewExporterSemanticRevision,
            ExporterSourceHashSha256 = exporterSourceHash
        };
        var completionContentHash = HashCanonicalWithBlankContentHash(
            JsonSerializer.SerializeToElement(completion, JsonOptions));
        completion = completion with { ContentHashSha256 = completionContentHash };
        var completionBytes = SerializeUnityReviewDocument(completion, completionContentHash);

        var exportRoot = PrepareSafeOutputDirectory(root, UnityReviewExportRelative);
        var generationsRoot = PrepareSafeOutputDirectory(exportRoot, "generations");
        var generationDirectoryName = bundleSetHash.ToLowerInvariant();
        var generationRoot = ResolveSafeOutputDirectoryPath(generationsRoot, generationDirectoryName);
        var dataPaths = entries.Select(item => item.RelativePath)
            .Append("index.json")
            .ToHashSet(StringComparer.Ordinal);
        var completedPaths = dataPaths.Append("complete.json").ToHashSet(StringComparer.Ordinal);
        var changedFileCount = 0;

        if (!Directory.Exists(generationRoot))
        {
            var stagingName = $".staging-{generationDirectoryName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
            var stagingRoot = PrepareSafeOutputDirectory(generationsRoot, stagingName);
            _ = PrepareSafeOutputDirectory(stagingRoot, "bundles");
            RequireOnlyExpectedUnityReviewFiles(stagingRoot, dataPaths, allowMissing: true);

            foreach (var artifact in artifacts)
            {
                var path = ResolveSafeOutputPath(stagingRoot, artifact.Entry.RelativePath);
                if (await WriteAtomicIfChangedAsync(path, artifact.Bytes, stagingRoot)) changedFileCount++;
            }
            var stagingIndexPath = ResolveSafeOutputPath(stagingRoot, "index.json");
            if (await WriteAtomicIfChangedAsync(stagingIndexPath, indexBytes, stagingRoot)) changedFileCount++;

            RequireOnlyExpectedUnityReviewFiles(stagingRoot, dataPaths, allowMissing: false);
            await VerifyUnityReviewReadbackAsync(stagingRoot, index, indexBytes, artifacts, package);

            var stagingCompletionPath = ResolveSafeOutputPath(stagingRoot, "complete.json");
            if (await WriteAtomicIfChangedAsync(stagingCompletionPath, completionBytes, stagingRoot))
                changedFileCount++;
            RequireOnlyExpectedUnityReviewFiles(stagingRoot, completedPaths, allowMissing: false);
            await VerifyUnityReviewCompletionAsync(
                stagingRoot,
                completion,
                completionBytes,
                indexBytes,
                artifacts.Count,
                exporterSourceHash);

            Require(!Directory.Exists(generationRoot),
                "AdministrativeDongDioramaUnityReviewGenerationAlreadyExists");
            Directory.Move(stagingRoot, generationRoot);
        }

        Require(string.Equals(
                Path.GetFileName(Path.TrimEndingDirectorySeparator(generationRoot)),
                generationDirectoryName,
                StringComparison.Ordinal),
            "AdministrativeDongDioramaUnityReviewGenerationDirectoryHashMismatch");
        RequireOnlyExpectedUnityReviewFiles(generationRoot, completedPaths, allowMissing: false);
        await VerifyUnityReviewReadbackAsync(generationRoot, index, indexBytes, artifacts, package);
        await VerifyUnityReviewCompletionAsync(
            generationRoot,
            completion,
            completionBytes,
            indexBytes,
            artifacts.Count,
            exporterSourceHash);
        SafeRead(exporterPath, root, 4 * 1024 * 1024);
        Require(HashFile(exporterPath) == exporterSourceHash,
            "AdministrativeDongDioramaUnityReviewExporterSourceChangedDuringExport");
        var indexPath = ResolveSafeOutputPath(generationRoot, "index.json");
        var completionPath = ResolveSafeOutputPath(generationRoot, "complete.json");
        return new UnityReviewExportResult(
            true,
            Path.GetRelativePath(root, indexPath).Replace('\\', '/'),
            Hash(indexBytes),
            indexContentHash,
            bundleSetHash,
            bundleSetHash,
            Path.GetRelativePath(root, completionPath).Replace('\\', '/'),
            Hash(completionBytes),
            UnityReviewExporterSemanticRevision,
            exporterSourceHash,
            artifacts.Count,
            changedFileCount,
            artifacts.Sum(item => item.Bytes.LongLength) + indexBytes.LongLength + completionBytes.LongLength);
    }

    private static async Task VerifyUnityReviewReadbackAsync(
        string exportRoot,
        UnityReviewIndex expectedIndex,
        byte[] expectedIndexBytes,
        IReadOnlyList<UnityReviewBundleArtifact> expectedArtifacts,
        CandidatePackage package)
    {
        var indexPath = ResolveSafeOutputPath(exportRoot, "index.json");
        SafeRead(indexPath, exportRoot, 8 * 1024 * 1024);
        var indexBytes = await File.ReadAllBytesAsync(indexPath);
        Require(indexBytes.AsSpan().SequenceEqual(expectedIndexBytes),
            "AdministrativeDongDioramaUnityReviewIndexBytesChanged");
        using var indexDocument = JsonDocument.Parse(indexBytes);
        var index = indexDocument.RootElement;
        Require(HashCanonical(index)
                == HashCanonical(JsonSerializer.SerializeToElement(expectedIndex, JsonOptions)),
            "AdministrativeDongDioramaUnityReviewIndexRereadMismatch");
        Require(String(index, "schemaVersion") == UnityReviewIndexSchemaVersion
                && String(index, "scopeStableId") == ScopeStableId
                && String(index, "revision") == Revision
                && String(index, "candidateBatchId") == BatchId(package.BatchContentHash),
            "AdministrativeDongDioramaUnityReviewIndexIdentityMismatch");
        Require(String(index, "exporterSemanticRevision") == UnityReviewExporterSemanticRevision
                && String(index, "exporterSourceHashSha256") == expectedIndex.ExporterSourceHashSha256
                && IsSha256(String(index, "exporterSourceHashSha256"))
                && String(index, "generationHashSha256") == expectedIndex.BundleSetHashSha256,
            "AdministrativeDongDioramaUnityReviewIndexExporterReceiptMismatch");
        Require(String(index, "contentHashSha256") == expectedIndex.ContentHashSha256
                && HashCanonicalWithBlankContentHash(index) == expectedIndex.ContentHashSha256,
            "AdministrativeDongDioramaUnityReviewIndexContentHashMismatch");
        RequireUnityReviewEnvelope(index);
        Require(ArraySet(index, "missingLayerCodes", MissingLifeLayerCodes),
            "AdministrativeDongDioramaUnityReviewIndexMissingLayersChanged");
        Require(Int(index, "administrativeAreaCount") == Areas.Length
                && Int(index, "bundleCount") == Areas.Length
                && Int(index, "buildingCount") == package.ProjectedBuildingCount
                && Int(index, "roadSegmentCount") == package.ProjectedRoadCount
                && Int(index, "tileCount") == package.TileCount
                && Int(index, "unresolvedBuildingCount") == package.UnresolvedBuildingCount
                && Int(index, "publicBusinessCount") == 0
                && Int(index, "displayOverlayItemCount") == 0,
            "AdministrativeDongDioramaUnityReviewIndexCountMismatch");
        Require(Boolean(index, "baseGeometryCaptureReady")
                && !Boolean(index, "lifeDioramaCaptureReady")
                && !Boolean(index, "personalDataIncluded"),
            "AdministrativeDongDioramaUnityReviewIndexCapabilityMismatch");
        RequireNoForbiddenUnityReviewProperties(index);

        var indexedBundles = index.GetProperty("bundles").EnumerateArray().ToArray();
        Require(indexedBundles.Length == expectedArtifacts.Count
                && indexedBundles.Select(item => String(item, "administrativeAreaStableId"))
                    .Distinct(StringComparer.Ordinal).Count() == expectedArtifacts.Count,
            "AdministrativeDongDioramaUnityReviewIndexBundleSetMismatch");
        Require(String(index, "bundleSetHashSha256")
                == HashCanonical(index.GetProperty("bundles")),
            "AdministrativeDongDioramaUnityReviewBundleSetHashMismatch");

        foreach (var artifact in expectedArtifacts)
        {
            var indexed = indexedBundles.Single(item =>
                String(item, "administrativeAreaStableId") == artifact.Entry.AdministrativeAreaStableId);
            Require(String(indexed, "relativePath") == artifact.Entry.RelativePath
                    && String(indexed, "sha256") == artifact.Entry.Sha256
                    && String(indexed, "contentHashSha256") == artifact.Entry.ContentHashSha256
                    && indexed.GetProperty("byteLength").GetInt64() == artifact.Entry.ByteLength,
                "AdministrativeDongDioramaUnityReviewIndexEntryMismatch:"
                + artifact.Entry.AdministrativeAreaStableId);
            var bundlePath = ResolveSafeOutputPath(exportRoot, artifact.Entry.RelativePath);
            SafeRead(bundlePath, exportRoot, 128 * 1024 * 1024);
            var bundleBytes = await File.ReadAllBytesAsync(bundlePath);
            Require(bundleBytes.AsSpan().SequenceEqual(artifact.Bytes)
                    && Hash(bundleBytes) == artifact.Entry.Sha256
                    && bundleBytes.LongLength == artifact.Entry.ByteLength,
                "AdministrativeDongDioramaUnityReviewBundleFileMismatch:"
                + artifact.Entry.AdministrativeAreaStableId);
            using var bundleDocument = JsonDocument.Parse(bundleBytes);
            VerifyUnityReviewBundle(
                bundleDocument.RootElement,
                artifact,
                package.Projections.Single(item =>
                    item.Definition.Id == artifact.Entry.AdministrativeAreaStableId));
        }
    }

    private static void VerifyUnityReviewBundle(
        JsonElement bundle,
        UnityReviewBundleArtifact expected,
        ModuleProjection module)
    {
        Require(HashCanonical(bundle)
                == HashCanonical(JsonSerializer.SerializeToElement(expected.Bundle, JsonOptions)),
            "AdministrativeDongDioramaUnityReviewBundleRereadMismatch:"
            + expected.Entry.AdministrativeAreaStableId);
        Require(String(bundle, "schemaVersion") == UnityReviewBundleSchemaVersion
                && String(bundle, "scopeStableId") == ScopeStableId
                && String(bundle, "revision") == Revision,
            "AdministrativeDongDioramaUnityReviewBundleIdentityMismatch:"
            + expected.Entry.AdministrativeAreaStableId);
        Require(String(bundle, "candidateBatchId") == expected.Bundle.CandidateBatchId
                && String(bundle, "administrativeAreaStableId") == module.Definition.Id
                && String(bundle, "projectionHashSha256")
                == module.Projection.Manifest.ProjectionHashSha256,
            "AdministrativeDongDioramaUnityReviewBundleBindingMismatch:"
            + expected.Entry.AdministrativeAreaStableId);
        Require(String(bundle, "exporterSemanticRevision") == UnityReviewExporterSemanticRevision
                && String(bundle, "exporterSourceHashSha256") == expected.Bundle.ExporterSourceHashSha256
                && IsSha256(String(bundle, "exporterSourceHashSha256")),
            "AdministrativeDongDioramaUnityReviewBundleExporterReceiptMismatch:"
            + expected.Entry.AdministrativeAreaStableId);
        Require(String(bundle, "contentHashSha256") == expected.Entry.ContentHashSha256
                && HashCanonicalWithBlankContentHash(bundle) == expected.Entry.ContentHashSha256,
            "AdministrativeDongDioramaUnityReviewBundleContentHashMismatch:"
            + expected.Entry.AdministrativeAreaStableId);
        RequireUnityReviewEnvelope(bundle);
        Require(ArraySet(bundle, "missingLayerCodes", MissingLifeLayerCodes),
            "AdministrativeDongDioramaUnityReviewBundleMissingLayersChanged:"
            + expected.Entry.AdministrativeAreaStableId);
        Require(Boolean(bundle, "baseGeometryCaptureReady")
                && !Boolean(bundle, "lifeDioramaCaptureReady")
                && !Boolean(bundle, "personalDataIncluded")
                && Int(bundle, "publicBusinessCount") == 0,
            "AdministrativeDongDioramaUnityReviewBundleCapabilityMismatch:"
            + expected.Entry.AdministrativeAreaStableId);

        var manifest = bundle.GetProperty("manifest");
        Require(String(manifest, "administrativeAreaStableId") == module.Definition.Id
                && String(manifest, "projectionHashSha256")
                == module.Projection.Manifest.ProjectionHashSha256
                && String(manifest, "readinessCode")
                == AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly
                && Boolean(manifest, "observationPresentationOnly")
                && !Boolean(manifest, "traversalReady")
                && !Boolean(manifest, "gameplayReady")
                && !Boolean(manifest, "distributionApproved")
                && Int(manifest, "publicBusinessMarkerCount") == 0
                && manifest.GetProperty("operationalAreaStableIds").GetArrayLength() == 0
                && manifest.GetProperty("semanticPlaceStableIds").GetArrayLength() == 0,
            "AdministrativeDongDioramaUnityReviewManifestEnvelopeMismatch:"
            + expected.Entry.AdministrativeAreaStableId);

        var tiles = bundle.GetProperty("tiles").EnumerateArray().ToArray();
        var overlays = bundle.GetProperty("displayOverlays");
        Require(tiles.Length == module.Projection.Tiles.Length
                && tiles.Sum(tile => tile.GetProperty("buildings").GetArrayLength())
                == module.Projection.Tiles.Sum(tile => tile.Buildings.Length)
                && tiles.Sum(tile => tile.GetProperty("roads").GetArrayLength())
                == module.Projection.Tiles.Sum(tile => tile.Roads.Length)
                && String(overlays, "administrativeAreaStableId") == module.Definition.Id
                && overlays.GetProperty("items").GetArrayLength() == 0,
            "AdministrativeDongDioramaUnityReviewPayloadCountMismatch:"
            + expected.Entry.AdministrativeAreaStableId);
        RequireNoForbiddenUnityReviewProperties(bundle);
    }

    private static async Task VerifyUnityReviewCompletionAsync(
        string generationRoot,
        UnityReviewCompletion expected,
        byte[] expectedBytes,
        byte[] expectedIndexBytes,
        int expectedBundleCount,
        string expectedExporterSourceHash)
    {
        var completionPath = ResolveSafeOutputPath(generationRoot, "complete.json");
        SafeRead(completionPath, generationRoot, 1024 * 1024);
        var bytes = await File.ReadAllBytesAsync(completionPath);
        Require(bytes.AsSpan().SequenceEqual(expectedBytes),
            "AdministrativeDongDioramaUnityReviewCompletionBytesChanged");
        using var document = JsonDocument.Parse(bytes);
        var completion = document.RootElement;
        Require(HashCanonical(completion)
                == HashCanonical(JsonSerializer.SerializeToElement(expected, JsonOptions)),
            "AdministrativeDongDioramaUnityReviewCompletionRereadMismatch");
        Require(String(completion, "schemaVersion") == UnityReviewCompletionSchemaVersion
                && String(completion, "scopeStableId") == ScopeStableId
                && String(completion, "revision") == Revision
                && String(completion, "generationHashSha256") == expected.GenerationHashSha256
                && String(completion, "bundleSetHashSha256") == expected.GenerationHashSha256,
            "AdministrativeDongDioramaUnityReviewCompletionIdentityMismatch");
        Require(String(completion, "indexRelativePath") == "index.json"
                && String(completion, "indexSha256") == Hash(expectedIndexBytes)
                && String(completion, "indexContentHashSha256") == expected.IndexContentHashSha256
                && Int(completion, "bundleCount") == expectedBundleCount,
            "AdministrativeDongDioramaUnityReviewCompletionIndexReceiptMismatch");
        Require(String(completion, "exporterSemanticRevision") == UnityReviewExporterSemanticRevision
                && String(completion, "exporterSourceHashSha256") == expectedExporterSourceHash
                && IsSha256(String(completion, "exporterSourceHashSha256")),
            "AdministrativeDongDioramaUnityReviewCompletionExporterReceiptMismatch");
        Require(String(completion, "contentHashSha256") == expected.ContentHashSha256
                && HashCanonicalWithBlankContentHash(completion) == expected.ContentHashSha256,
            "AdministrativeDongDioramaUnityReviewCompletionContentHashMismatch");
        RequireUnityReviewEnvelope(completion);
        Require(ArraySet(completion, "missingLayerCodes", MissingLifeLayerCodes)
                && Boolean(completion, "baseGeometryCaptureReady")
                && !Boolean(completion, "lifeDioramaCaptureReady")
                && !Boolean(completion, "personalDataIncluded")
                && Int(completion, "publicBusinessCount") == 0
                && Int(completion, "displayOverlayItemCount") == 0,
            "AdministrativeDongDioramaUnityReviewCompletionCapabilityMismatch");
        RequireNoForbiddenUnityReviewProperties(completion);
    }

    private static void RequireUnityReviewEnvelope(JsonElement element)
    {
        Require(String(element, "desiredReadinessCode") == DesiredReadiness
                && ArraySet(element, "diagnosticCodes", RequiredDiagnosticCodes)
                && !Boolean(element, "candidateEnvelopeIsPublicationAuthority")
                && Boolean(element, "candidateEnvelopeReadinessGovernsPromotion")
                && Boolean(element, "embeddedManifestReadinessIsInformational")
                && Boolean(element, "publishBlocked")
                && !Boolean(element, "currentPointerUsed")
                && !Boolean(element, "currentPointerUpdated")
                && Boolean(element, "observationPresentationOnly")
                && !Boolean(element, "distributionApproved")
                && !Boolean(element, "traversalReady")
                && !Boolean(element, "gameplayReady"),
            "AdministrativeDongDioramaUnityReviewCandidateEnvelopeMismatch");
    }

    private static void RequireNoForbiddenUnityReviewProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Require(!ForbiddenUnityReviewPropertyNames.Contains(property.Name),
                    "AdministrativeDongDioramaUnityReviewForbiddenProperty:" + property.Name);
                RequireNoForbiddenUnityReviewProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) RequireNoForbiddenUnityReviewProperties(item);
        }
    }

    private static byte[] SerializeUnityReviewDocument<T>(T value, string contentHash)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        using var document = JsonDocument.Parse(bytes);
        Require(String(document.RootElement, "contentHashSha256") == contentHash
                && HashCanonicalWithBlankContentHash(document.RootElement) == contentHash,
            "AdministrativeDongDioramaUnityReviewSerializationHashMismatch");
        return bytes;
    }

    private static async Task<bool> WriteAtomicIfChangedAsync(
        string path,
        byte[] bytes,
        string allowedRoot)
    {
        EnsureSafeOutputPath(path, allowedRoot);
        if (File.Exists(path))
        {
            SafeRead(path, allowedRoot, 128 * 1024 * 1024);
            var existing = await File.ReadAllBytesAsync(path);
            if (existing.AsSpan().SequenceEqual(bytes)) return false;
        }

        var temporary = path + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N") + ".tmp";
        EnsureSafeOutputPath(temporary, allowedRoot);
        try
        {
            await using (var stream = new FileStream(temporary, new FileStreamOptions
                         {
                             Mode = FileMode.CreateNew,
                             Access = FileAccess.Write,
                             Share = FileShare.None,
                             Options = FileOptions.Asynchronous | FileOptions.WriteThrough
                         }))
            {
                await stream.WriteAsync(bytes);
                await stream.FlushAsync();
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null, ignoreMetadataErrors: true);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        SafeRead(path, allowedRoot, 128 * 1024 * 1024);
        Require(HashFile(path) == Hash(bytes), "AdministrativeDongDioramaUnityReviewAtomicWriteMismatch");
        return true;
    }

    private static string PrepareSafeOutputDirectory(string root, string relativePath)
    {
        var safeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var output = Path.GetFullPath(Path.Combine(safeRoot, relativePath));
        Require(IsChildPath(output, safeRoot), "AdministrativeDongDioramaUnityReviewOutputOutsideRoot");
        var existingAncestor = new DirectoryInfo(output);
        while (!existingAncestor.Exists)
            existingAncestor = existingAncestor.Parent
                               ?? throw new InvalidDataException(
                                   "AdministrativeDongDioramaUnityReviewDirectoryOutsideRoot");
        EnsureSafeDirectoryChain(existingAncestor.FullName, safeRoot);
        Directory.CreateDirectory(output);
        EnsureSafeDirectoryChain(output, safeRoot);
        return output;
    }

    private static string ResolveSafeOutputPath(string outputRoot, string relativePath)
    {
        Require(!Path.IsPathRooted(relativePath)
                && !relativePath.Split('/', '\\').Any(part => part is "" or "." or ".."),
            "AdministrativeDongDioramaUnityReviewRelativePathInvalid");
        var result = Path.GetFullPath(Path.Combine(outputRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureSafeOutputPath(result, outputRoot);
        return result;
    }

    private static string ResolveSafeOutputDirectoryPath(string outputRoot, string relativePath)
    {
        Require(!Path.IsPathRooted(relativePath)
                && !relativePath.Split('/', '\\').Any(part => part is "" or "." or ".."),
            "AdministrativeDongDioramaUnityReviewRelativeDirectoryInvalid");
        var safeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputRoot));
        var result = Path.GetFullPath(Path.Combine(
            safeRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Require(IsChildPath(result, safeRoot),
            "AdministrativeDongDioramaUnityReviewDirectoryOutsideRoot");
        if (Directory.Exists(result)) EnsureSafeDirectoryChain(result, safeRoot);
        else
        {
            var parent = Path.GetDirectoryName(result)
                         ?? throw new InvalidDataException(
                             "AdministrativeDongDioramaUnityReviewDirectoryMissing");
            Require(Directory.Exists(parent),
                "AdministrativeDongDioramaUnityReviewDirectoryMissing");
            EnsureSafeDirectoryChain(parent, safeRoot);
        }
        return result;
    }

    private static void EnsureSafeOutputPath(string path, string outputRoot)
    {
        var safeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputRoot));
        var full = Path.GetFullPath(path);
        Require(IsChildPath(full, safeRoot), "AdministrativeDongDioramaUnityReviewPathOutsideRoot");
        var directory = Path.GetDirectoryName(full)
                        ?? throw new InvalidDataException("AdministrativeDongDioramaUnityReviewDirectoryMissing");
        Require(Directory.Exists(directory), "AdministrativeDongDioramaUnityReviewDirectoryMissing");
        EnsureSafeDirectoryChain(directory, safeRoot);
        if (File.Exists(full))
            Require(!new FileInfo(full).Attributes.HasFlag(FileAttributes.ReparsePoint),
                "AdministrativeDongDioramaUnityReviewFileReparseRejected");
    }

    private static void EnsureSafeDirectoryChain(string directory, string root)
    {
        var safeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var reachedRoot = false;
        for (var current = new DirectoryInfo(Path.GetFullPath(directory)); current is not null; current = current.Parent)
        {
            Require(!current.Attributes.HasFlag(FileAttributes.ReparsePoint),
                "AdministrativeDongDioramaUnityReviewDirectoryReparseRejected");
            if (!string.Equals(Path.TrimEndingDirectorySeparator(current.FullName), safeRoot,
                    StringComparison.OrdinalIgnoreCase)) continue;
            reachedRoot = true;
            break;
        }
        Require(reachedRoot, "AdministrativeDongDioramaUnityReviewDirectoryOutsideRoot");
    }

    private static bool IsChildPath(string path, string root)
        => Path.GetFullPath(path).StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static void RequireOnlyExpectedUnityReviewFiles(
        string exportRoot,
        IReadOnlySet<string> expectedPaths,
        bool allowMissing)
    {
        var bundleRoot = Path.GetFullPath(Path.Combine(exportRoot, "bundles"));
        Require(IsChildPath(bundleRoot, exportRoot) && Directory.Exists(bundleRoot),
            "AdministrativeDongDioramaUnityReviewBundleDirectoryMissing");
        EnsureSafeDirectoryChain(bundleRoot, exportRoot);
        var topLevelDirectories = Directory.EnumerateDirectories(exportRoot, "*", SearchOption.TopDirectoryOnly)
            .ToArray();
        Require(topLevelDirectories.Length == 1
                && string.Equals(Path.GetFullPath(topLevelDirectories[0]), Path.GetFullPath(bundleRoot),
                    StringComparison.OrdinalIgnoreCase)
                && !new DirectoryInfo(bundleRoot).Attributes.HasFlag(FileAttributes.ReparsePoint)
                && !Directory.EnumerateDirectories(bundleRoot, "*", SearchOption.TopDirectoryOnly).Any(),
            "AdministrativeDongDioramaUnityReviewUnexpectedOutputDirectory");
        var actual = Directory.EnumerateFiles(exportRoot, "*", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(bundleRoot, "*", SearchOption.TopDirectoryOnly))
            .Select(path => Path.GetRelativePath(exportRoot, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.All(expectedPaths.Contains),
            "AdministrativeDongDioramaUnityReviewUnexpectedOutputFile");
        Require(allowMissing || actual.SetEquals(expectedPaths),
            "AdministrativeDongDioramaUnityReviewOutputFileSetMismatch");
    }

    private static bool ArraySet(JsonElement element, string name, IReadOnlyCollection<string> expected)
    {
        var values = element.GetProperty(name).EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        return values.Length == expected.Count
               && values.ToHashSet(StringComparer.Ordinal).SetEquals(expected);
    }

    private static string AdministrativeAreaCode(string stableId)
    {
        const string prefix = "region:kr:hjd:";
        Require(stableId.StartsWith(prefix, StringComparison.Ordinal),
            "AdministrativeDongDioramaUnityReviewAreaIdInvalid");
        var code = stableId[prefix.Length..];
        Require(code.Length == 10 && code.All(char.IsAsciiDigit),
            "AdministrativeDongDioramaUnityReviewAreaCodeInvalid");
        return code;
    }

    private static async Task<CandidatePackage> ReadAndBuildAsync(string root)
    {
        var artifactRoot = Path.GetFullPath(Path.Combine(root, ArtifactRelative));
        var scopePath = Path.Combine(artifactRoot, "scope-manifest.json");
        var boundaryPath = Path.Combine(root, BoundaryRelative);
        var buildingPath = Path.Combine(root, BuildingRelative);
        var nodeLinkPath = Path.Combine(root, NodeLinkRelative);
        var scopeDefinitionPath = Path.Combine(root, ScopeDefinitionRelative);
        var projectionBuilderPath = Path.Combine(root, ProjectionBuilderRelative);
        SafeRead(scopePath, artifactRoot, 8 * 1024 * 1024);
        SafeRead(boundaryPath, root, 8 * 1024 * 1024);
        SafeRead(buildingPath, root, 256 * 1024 * 1024);
        SafeRead(nodeLinkPath, root, 384 * 1024 * 1024);
        SafeRead(scopeDefinitionPath, root, 1024 * 1024);
        SafeRead(projectionBuilderPath, root, 1024 * 1024);
        Require(HashFile(boundaryPath) == BoundaryHash, "AdministrativeBoundaryInputHashChanged");
        Require(HashFile(buildingPath) == BuildingHash, "AdministrativeBuildingInputHashChanged");
        Require(HashFile(nodeLinkPath) == NodeLinkHash, "AdministrativeRoadInputHashChanged");
        var projectionBuilderSourceHash = HashFile(projectionBuilderPath);

        var scopeBytes = await File.ReadAllBytesAsync(scopePath);
        using var scopeDocument = JsonDocument.Parse(scopeBytes);
        var scope = scopeDocument.RootElement;
        Require(String(scope, "schemaVersion") == ScopeSchemaVersion, "AdministrativeDongDioramaBatchScopeSchemaChanged");
        Require(String(scope, "scopeStableId") == ScopeStableId, "AdministrativeDongDioramaBatchScopeIdChanged");
        Require(String(scope, "revision") == Revision, "AdministrativeDongDioramaBatchRevisionChanged");
        Require(String(scope, "sourceVintage") == SourceVintage, "AdministrativeDongDioramaBatchSourceVintageChanged");
        Require(String(scope, "desiredReadinessCode") == DesiredReadiness,
            "AdministrativeDongDioramaBatchReadinessChanged");
        Require(Date(scope, "generatedAtUtc") == GeneratedAtUtc, "AdministrativeDongDioramaBatchGeneratedAtChanged");
        Require(!Boolean(scope, "distributionApproved") && !Boolean(scope, "traversalReady")
                                                        && !Boolean(scope, "gameplayReady"),
            "AdministrativeDongDioramaBatchUnsafeCapabilityEnabled");
        Require(Set(scope, "diagnosticCodes").SetEquals(RequiredDiagnosticCodes),
            "AdministrativeDongDioramaBatchDiagnosticsChanged");
        var scopeContentHash = String(scope, "contentHashSha256");
        Require(IsSha256(scopeContentHash), "AdministrativeDongDioramaBatchContentHashInvalid");
        Require(HashCanonicalWithBlankContentHash(scope) == scopeContentHash.ToUpperInvariant(),
            "AdministrativeDongDioramaBatchContentHashMismatch");
        var scopeDefinitionBytes = await File.ReadAllBytesAsync(scopeDefinitionPath);
        using var scopeDefinitionDocument = JsonDocument.Parse(scopeDefinitionBytes);
        ValidateScopeDefinitionAndToolchain(
            root,
            scopeDefinitionDocument.RootElement,
            scope,
            Hash(scopeDefinitionBytes));
        ValidateSources(scope.GetProperty("sources"));
        var frozenSourcesHash = HashCanonicalWithBlankContentHash(scope.GetProperty("sources"));

        var auditReference = scope.GetProperty("audit");
        var auditRelativePath = String(auditReference, "relativePath").Replace('/', Path.DirectorySeparatorChar);
        var auditPath = Path.GetFullPath(Path.Combine(artifactRoot, auditRelativePath));
        SafeRead(auditPath, artifactRoot, 8 * 1024 * 1024);
        var auditBytes = await File.ReadAllBytesAsync(auditPath);
        Require(Hash(auditBytes) == String(auditReference, "sha256").ToUpperInvariant(),
            "AdministrativeDongDioramaAuditFileHashMismatch");
        using var auditDocument = JsonDocument.Parse(auditBytes);
        var auditRoot = auditDocument.RootElement;
        Require(String(auditRoot, "schemaVersion") == AuditSchemaVersion,
            "AdministrativeDongDioramaAuditSchemaChanged");
        Require(String(auditRoot, "scopeStableId") == ScopeStableId
                && String(auditRoot, "revision") == Revision
                && String(auditRoot, "sourceVintage") == SourceVintage,
            "AdministrativeDongDioramaAuditIdentityChanged");
        var auditContentHash = String(auditRoot, "contentHashSha256");
        Require(IsSha256(auditContentHash)
                && HashCanonicalWithBlankContentHash(auditRoot) == auditContentHash.ToUpperInvariant(),
            "AdministrativeDongDioramaAuditContentHashMismatch");
        Require(HashCanonicalWithBlankContentHash(auditRoot.GetProperty("toolchain"))
                == HashCanonicalWithBlankContentHash(scope.GetProperty("toolchain")),
            "AdministrativeDongDioramaAuditToolchainReceiptMismatch");

        var frame = JsonSerializer.Deserialize<AdministrativeDongDioramaCoordinateFrame>(
                        scope.GetProperty("coordinateFrame").GetRawText(), JsonOptions)
                    ?? throw new InvalidDataException("AdministrativeDongDioramaCoordinateFrameMissing");
        ValidateCoordinateFrame(frame);
        var ledger = await ReadOfficialCrosswalkAsync(root);

        var moduleEntries = scope.GetProperty("modules").EnumerateArray().ToArray();
        Require(moduleEntries.Length == Areas.Length, "AdministrativeDongDioramaBatchModuleCountMismatch");
        var entryIds = moduleEntries.Select(item => String(item, "administrativeAreaStableId")).ToArray();
        Require(entryIds.Distinct(StringComparer.Ordinal).Count() == Areas.Length
                && entryIds.ToHashSet(StringComparer.Ordinal).SetEquals(Areas.Select(item => item.Id)),
            "AdministrativeDongDioramaBatchAreaSetMismatch");

        var boundaryBytes = await File.ReadAllBytesAsync(boundaryPath);
        var snapshots = 행정동경계ShapefileZipReader.Read(
            boundaryBytes, Areas.Select(item => item.Id), SourceVintage, frame);
        Require(snapshots.Count == Areas.Length, "AdministrativeDongDioramaBoundaryCountMismatch");
        var boundaryByArea = snapshots.ToDictionary(item => item.AdministrativeAreaStableId, StringComparer.Ordinal);

        var modules = new List<ModuleProjection>(Areas.Length);
        foreach (var entry in moduleEntries.OrderBy(item => String(item, "administrativeAreaStableId"), StringComparer.Ordinal))
        {
            var areaId = String(entry, "administrativeAreaStableId");
            var definition = Areas.Single(item => item.Id == areaId);
            Require(String(entry, "displayName") == definition.DisplayName,
                "AdministrativeDongDioramaBatchEntryDisplayNameMismatch:" + areaId);
            var relativePath = String(entry, "relativePath").Replace('/', Path.DirectorySeparatorChar);
            var modulePath = Path.GetFullPath(Path.Combine(artifactRoot, relativePath));
            SafeRead(modulePath, artifactRoot, 64 * 1024 * 1024);
            var moduleBytes = await File.ReadAllBytesAsync(modulePath);
            Require(Hash(moduleBytes) == String(entry, "sha256").ToUpperInvariant(),
                "AdministrativeDongDioramaModuleFileHashMismatch:" + areaId);

            using var moduleDocument = JsonDocument.Parse(moduleBytes);
            var module = moduleDocument.RootElement;
            Require(String(module, "schemaVersion") == ModuleSchemaVersion,
                "AdministrativeDongDioramaModuleSchemaChanged:" + areaId);
            Require(String(module, "administrativeAreaStableId") == areaId,
                "AdministrativeDongDioramaModuleAreaMismatch:" + areaId);
            Require(String(module, "displayName") == definition.DisplayName,
                "AdministrativeDongDioramaModuleDisplayNameMismatch:" + areaId);
            Require(String(module, "sourceVintage") == SourceVintage,
                "AdministrativeDongDioramaModuleSourceVintageMismatch:" + areaId);
            Require(Date(module, "generatedAtUtc") == GeneratedAtUtc,
                "AdministrativeDongDioramaModuleGeneratedAtMismatch:" + areaId);
            Require(String(module, "desiredReadinessCode") == DesiredReadiness,
                "AdministrativeDongDioramaModuleReadinessMismatch:" + areaId);
            Require(Set(module, "diagnosticCodes").SetEquals(RequiredDiagnosticCodes),
                "AdministrativeDongDioramaModuleDiagnosticsMismatch:" + areaId);
            var contentHash = String(module, "contentHashSha256");
            Require(IsSha256(contentHash)
                    && contentHash.Equals(String(entry, "contentHashSha256"), StringComparison.OrdinalIgnoreCase)
                    && HashCanonicalWithBlankContentHash(module) == contentHash.ToUpperInvariant(),
                "AdministrativeDongDioramaModuleContentHashMismatch:" + areaId);
            ValidateSources(module.GetProperty("sources"));
            Require(HashCanonicalWithBlankContentHash(module.GetProperty("sources")) == frozenSourcesHash,
                "AdministrativeDongDioramaModuleSourceSetMismatch:" + areaId);

            var input = JsonSerializer.Deserialize<행정동디오라마ProjectionBuildInput>(module.GetRawText(), JsonOptions)
                        ?? throw new InvalidDataException("AdministrativeDongDioramaModuleDeserializeFailed:" + areaId);
            ValidateModuleInput(input, definition, frame);
            var snapshot = boundaryByArea[areaId];
            Require(snapshot.DisplayName == HistoricalBoundaryDisplayName(definition.DisplayName),
                "AdministrativeDongDioramaHistoricalBoundaryNameMismatch:" + areaId);

            // Python/GEOS가 내보낸 1mm 경계를 그대로 신뢰하지 않는다. 동일 hash의 OA ZIP을
            // C# Adapter가 독립 재판독한 뒤 같은 공개 좌표 규칙으로 정규화해 정확히 대조한다.
            // 대조를 통과한 생성 경계를 같은 1mm 공개 규칙으로 정규화해 건물·도로 입력과 한 판본을 유지한다.
            var independentlyPublishedBoundary = CanonicalOneMillimeterBoundary(snapshot.Boundary);
            var boundaryMismatch = BoundaryMismatch(input.Boundary, independentlyPublishedBoundary);
            Require(boundaryMismatch is null,
                "AdministrativeDongDioramaDerivedBoundaryMismatch:" + areaId + ":" + boundaryMismatch);
            input.Boundary = NormalizeGeneratedBoundary(input.Boundary);
            var projection = 행정동디오라마ProjectionBuilder.Build(input);
            ValidateProjection(input, projection);
            Require(projection.Manifest.ReadinessCode == AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                "AdministrativeDongDioramaHistoricalProjectionUnexpectedReadiness:" + areaId);

            Require(Int(entry, "buildingCount") == input.Buildings.Length,
                "AdministrativeDongDioramaModuleBuildingCountMismatch:" + areaId);
            Require(Int(entry, "roadSegmentCount") == input.Roads.Length,
                "AdministrativeDongDioramaModuleRoadCountMismatch:" + areaId);
            Require(Int(entry, "unresolvedBuildingCount") == input.UnresolvedBuildingCount,
                "AdministrativeDongDioramaModuleUnresolvedCountMismatch:" + areaId);
            modules.Add(new ModuleProjection(
                definition,
                String(entry, "sha256").ToUpperInvariant(),
                contentHash.ToUpperInvariant(),
                input,
                projection));
        }

        var buildingIds = modules.SelectMany(item => item.Input.Buildings.Select(building => building.BuildingStableId)).ToArray();
        Require(buildingIds.Distinct(StringComparer.Ordinal).Count() == buildingIds.Length,
            "AdministrativeDongDioramaBuildingOwnedByMultipleAreas");
        Require(modules.Select(item => item.Definition.LegalId).Distinct(StringComparer.Ordinal).Count() == 12,
            "AdministrativeDongDioramaLegalAreaCountMismatch");
        var audit = ReadScopeAudit(auditRoot, modules);
        ValidateScopeCounts(scope, modules, audit);

        var orderedModules = modules.ToArray();
        var projectionSetHash = ComputeProjectionSetHash(orderedModules);
        var batchContentHash = ComputeBatchContentHash(
            scopeContentHash.ToUpperInvariant(),
            projectionSetHash,
            projectionBuilderSourceHash);
        return new CandidatePackage(
            batchContentHash,
            scopeContentHash.ToUpperInvariant(),
            projectionSetHash,
            projectionBuilderSourceHash,
            orderedModules,
            audit,
            ledger);
    }

    private static async Task<OfficialLedger> ReadOfficialCrosswalkAsync(string root)
    {
        await using var database = new PublicDataIngestionDbContext(await 로컬공공자료Db.OptionsAsync(root));
        var source = new Official행정동배달운영권역Source(database);
        var rows = await source.조회Async(배달운영권역SourceScopes.NortheastSeoulRiderR1);
        Require(rows.Count == Areas.Length, "AdministrativeDongDioramaMoisLedgerCountMismatch");
        var byArea = rows.ToDictionary(item => item.AdministrativeAreaStableId, StringComparer.Ordinal);
        Require(byArea.Count == Areas.Length, "AdministrativeDongDioramaMoisLedgerAreaDuplicate");
        foreach (var definition in Areas)
        {
            Require(byArea.TryGetValue(definition.Id, out var row)
                    && row.DisplayName == definition.DisplayName
                    && row.LegalAreas.Count == 1
                    && row.LegalAreas[0].LegalAreaStableId == definition.LegalId,
                "AdministrativeDongDioramaMoisLedgerBindingMismatch:" + definition.Id);
        }
        var sourceVersions = rows.Select(item => item.SourceVersion).Distinct(StringComparer.Ordinal).ToArray();
        var revisions = rows.Select(item => item.DataRevision).Distinct(StringComparer.Ordinal).ToArray();
        Require(sourceVersions is [행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1SourceVersion]
                && revisions is [행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1DataRevision],
            "AdministrativeDongDioramaMoisLedgerVintageMismatch");
        var crosswalk = rows.OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
            .Select(item => new
            {
                administrativeAreaStableId = item.AdministrativeAreaStableId,
                displayName = item.DisplayName,
                legalAreaStableId = item.LegalAreas.Single().LegalAreaStableId
            }).ToArray();
        var crosswalkHash = HashCanonicalWithBlankContentHash(JsonSerializer.SerializeToElement(crosswalk, JsonOptions));
        Require(crosswalkHash == CrosswalkHash, "AdministrativeDongDioramaMoisLedgerHashMismatch");
        return new OfficialLedger(sourceVersions[0], revisions[0], crosswalkHash,
            rows.Max(item => item.EvidenceAsOfUtc).ToUniversalTime());
    }

    private static void ValidateModuleInput(
        행정동디오라마ProjectionBuildInput input,
        AreaDefinition definition,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        Require(input.AdministrativeAreaStableId == definition.Id, "AdministrativeDongDioramaModuleAreaChanged");
        Require(input.DisplayName == definition.DisplayName, "AdministrativeDongDioramaModuleNameChanged");
        Require(input.SourceVintage == SourceVintage, "AdministrativeDongDioramaModuleVintageChanged");
        Require(input.GeneratedAtUtc.ToUniversalTime() == GeneratedAtUtc, "AdministrativeDongDioramaModuleTimeChanged");
        ValidateCoordinateFrame(input.CoordinateFrame);
        Require(SameFrame(input.CoordinateFrame, frame), "AdministrativeDongDioramaModuleFrameMismatch");
        Require(input.LegalAreaStableIds is { Length: 1 } && input.LegalAreaStableIds[0] == definition.LegalId,
            "AdministrativeDongDioramaModuleLegalAreaMismatch:" + definition.Id);
        Require(input.OperationalAreaStableIds.Length == 0, "AdministrativeDongDioramaOperationalBindingForbidden");
        Require(input.SemanticPlaceStableIds.Length == 0, "AdministrativeDongDioramaSemanticBindingForbidden");
        Require(input.PublicBusinesses.Length == 0, "AdministrativeDongDioramaPublicBusinessForbidden");
        Require(input.Buildings.Length > 0, "AdministrativeDongDioramaBuildingsRequired:" + definition.Id);
        Require(input.Buildings.All(building => building.AssignmentPoint is not null
                                               && building.AssignmentMethodCode == AssignmentMethod
                                               && building.AssignmentConfidenceCode == AssignmentConfidence
                                               && building.AssignmentBoundarySourceRevision == AssignmentBoundaryRevision),
            "AdministrativeDongDioramaBuildingAssignmentEvidenceMismatch:" + definition.Id);
        Require(input.Roads.Length > 0, "AdministrativeDongDioramaRoadsRequired:" + definition.Id);
        Require(input.Boundary.Length >= 3, "AdministrativeDongDioramaDerivedBoundaryMissing:" + definition.Id);
        Require(input.UnresolvedBuildingCount >= 0, "AdministrativeDongDioramaUnresolvedCountInvalid");
        ValidateSources(JsonSerializer.SerializeToElement(input.Sources, JsonOptions));
    }

    private static void ValidateProjection(
        행정동디오라마ProjectionBuildInput input,
        행정동디오라마ProjectionBuildResult projection)
    {
        var manifest = projection.Manifest;
        Require(manifest.AdministrativeAreaStableId == input.AdministrativeAreaStableId,
            "AdministrativeDongDioramaProjectionAreaMismatch");
        Require(manifest.ObservationPresentationOnly && !manifest.DistributionApproved
                                                     && !manifest.TraversalReady && !manifest.GameplayReady,
            "AdministrativeDongDioramaProjectionUnsafeCapabilityEnabled");
        Require(manifest.OperationalAreaStableIds.Length == 0 && manifest.SemanticPlaceStableIds.Length == 0
                                                               && manifest.PublicBusinessMarkerCount == 0,
            "AdministrativeDongDioramaProjectionForbiddenBinding");
        Require(projection.DisplayOverlays.AdministrativeAreaStableId == manifest.AdministrativeAreaStableId
                && projection.DisplayOverlays.Items.Length == 0,
            "AdministrativeDongDioramaProjectionOverlayMismatch");
        Require(projection.Tiles.Length > 0 && manifest.Tiles.Length == projection.Tiles.Length,
            "AdministrativeDongDioramaProjectionTileSetEmpty");
        var summaries = manifest.Tiles.ToDictionary(item => item.TileStableId, StringComparer.Ordinal);
        Require(summaries.Count == manifest.Tiles.Length, "AdministrativeDongDioramaProjectionTileDuplicate");
        foreach (var tile in projection.Tiles)
        {
            Require(tile.AdministrativeAreaStableId == manifest.AdministrativeAreaStableId
                    && tile.ProjectionHashSha256 == manifest.ProjectionHashSha256
                    && summaries.TryGetValue(tile.TileStableId, out var summary)
                    && summary.TileHashSha256 == tile.TileHashSha256
                    && summary.BuildingCount == tile.Buildings.Length
                    && summary.RoadSegmentCount == tile.Roads.Length,
                "AdministrativeDongDioramaProjectionTileVersionMismatch");
        }
        Require(projection.Tiles.Sum(tile => tile.Buildings.Length) == input.Buildings.Length,
            "AdministrativeDongDioramaProjectionBuildingLoss:" + input.AdministrativeAreaStableId);
        var projectedRoadCount = projection.Tiles.Sum(tile => tile.Roads.Length);
        Require(projectedRoadCount == input.Roads.Length,
            "AdministrativeDongDioramaProjectionRoadLoss:" + input.AdministrativeAreaStableId
            + ":input=" + input.Roads.Length + ":projected=" + projectedRoadCount);
    }

    private static void ValidateSources(JsonElement sourcesElement)
    {
        var sources = sourcesElement.EnumerateArray().Select(item => JsonSerializer.Deserialize<AdministrativeDongDioramaSourceAttribution>(
                item.GetRawText(), JsonOptions) ?? throw new InvalidDataException("AdministrativeDongDioramaSourceInvalid"))
            .ToArray();
        Require(sources.Length == 4, "AdministrativeDongDioramaSourceCountMismatch");
        var keyed = sources.ToDictionary(item => item.DatasetId, StringComparer.Ordinal);
        Require(keyed.Count == 4, "AdministrativeDongDioramaSourceDuplicate");
        RequireSource(keyed, "OA-22160", "source:seoul-open-data",
            "seoul-oa22160:file-modified:20231031:retrieved:20260912", BoundaryHash, "KOGL-Type1",
            "HistoricalBootstrapBoundary;EPSG5181;CurrentAuthoritativeBoundaryUnavailable;DistributionNotApproved");
        RequireSource(keyed, "data-go-kr-15083092", "source:data-go-kr", "AL_D010:Seoul:20260809",
            BuildingHash, "RightsConflictUnresolved",
            "OfficialGisBuildingGeometry;EPSG5186;LargestExteriorOnly;DistributionNotApproved");
        RequireSource(keyed, "data-go-kr-15025526", "source:data-go-kr", "NodeLink:release:20260812",
            NodeLinkHash, "UseScopeUnrestricted",
            "DirectedLinkPresentationOnly;NoWidthInference;NoTraversalAuthority;DistributionNotApproved");
        Require(keyed.TryGetValue("korea-administrative-legal-crosswalk", out var crosswalk)
                && crosswalk.SourceId == "source:mois-standard-codes"
                && crosswalk.SourceRevision == "mois-jscode:20260301"
                && crosswalk.ContentHashSha256.Equals(CrosswalkHash, StringComparison.OrdinalIgnoreCase)
                && crosswalk.LicenseCode == "PublicOpenData"
                && crosswalk.LimitationCode == "CodeAndJurisdictionOnly;NoBoundary;FrozenAllowList",
            "AdministrativeDongDioramaCrosswalkSourceMismatch");
    }

    private static void RequireSource(
        IReadOnlyDictionary<string, AdministrativeDongDioramaSourceAttribution> sources,
        string datasetId,
        string sourceId,
        string revision,
        string hash,
        string license,
        string limitation)
    {
        Require(sources.TryGetValue(datasetId, out var source)
                && source.SourceId == sourceId && source.SourceRevision == revision
                && source.ContentHashSha256.Equals(hash, StringComparison.OrdinalIgnoreCase)
                && source.LicenseCode == license && source.LimitationCode == limitation,
            "AdministrativeDongDioramaSourceMismatch:" + datasetId);
    }

    private static void ValidateScopeCounts(
        JsonElement scope,
        IReadOnlyList<ModuleProjection> modules,
        ScopeAudit audit)
    {
        Require(scope.TryGetProperty("counts", out var counts), "AdministrativeDongDioramaScopeCountsMissing");
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["administrativeAreaCount"] = modules.Count,
            ["buildingCount"] = modules.Sum(item => item.Input.Buildings.Length),
            ["roadSegmentCount"] = modules.Sum(item => item.Input.Roads.Length),
            ["scopeUnresolvedBuildingCount"] = audit.ScopeUnresolvedCount
        };
        foreach (var pair in expected)
            Require(counts.TryGetProperty(pair.Key, out var value) && value.GetInt32() == pair.Value,
                "AdministrativeDongDioramaScopeCountMismatch:" + pair.Key);
        Require(Int(counts, "unresolvedRepresentativePointCount") == 13,
            "AdministrativeDongDioramaUnresolvedRepresentativePointCountChanged");
        Require(Int(counts, "quarantinedMalformedBuildingCount") == 11,
            "AdministrativeDongDioramaMalformedQuarantineCountChanged");
        Require(Int(counts, "quarantinedRoundedFootprintBuildingCount") == 3,
            "AdministrativeDongDioramaRoundedFootprintQuarantineCountChanged");
        Require(Int(counts, "ambiguousBuildingAssignmentCount") == 0
                && Int(counts, "buildingLegalAreaMismatchCount") == audit.BuildingLegalAreaMismatchCount
                && Int(counts, "interiorRingOmittedBuildingCount") == audit.InteriorRingBuildingCount
                && Int(counts, "interiorRingsOmitted") == audit.InteriorRingOmittedCount
                && Int(counts, "multipartOmittedBuildingCount") == audit.MultipartOmittedBuildingCount
                && Int(counts, "v1ContractAssignmentAnchorOverrideCount") == audit.V1ContractAssignmentAnchorOverrideCount
                && Int(counts, "directedRoadLinkCount") == audit.DirectedRoadLinkCount
                && Int(counts, "crossBoundaryDirectedRoadLinkCount") == audit.CrossBoundaryDirectedRoadLinkCount
                && Int(counts, "exactRoadBoundaryContainmentFailureSegmentCount")
                == audit.ExactRoadBoundaryContainmentFailureSegmentCount
                && Nearly(counts.GetProperty("roadBoundaryNumericToleranceMeters").GetDouble(),
                    audit.RoadBoundaryNumericToleranceMeters),
            "AdministrativeDongDioramaScopeAuditCountMismatch");
    }

    private static ScopeAudit ReadScopeAudit(JsonElement auditElement, IReadOnlyList<ModuleProjection> modules)
    {
        var buildings = auditElement.GetProperty("buildings");
        var roads = auditElement.GetProperty("roads");
        var result = new ScopeAudit(
            Int(buildings, "outsideAfterIntersection"),
            Int(buildings, "malformedQuarantine"),
            Int(buildings, "roundedFootprintQuarantine"),
            Int(buildings, "interiorRingOmittedBuildings"),
            Int(buildings, "interiorRingsOmitted"),
            Int(buildings, "multipartOmittedBuildings"),
            Int(buildings, "legalAreaMismatch"),
            Int(buildings, "duplicateSourceFeatureIdOccurrences"),
            Int(buildings, "v1ContractAssignmentAnchorOverrides"),
            Int(buildings, "scopeUnresolvedCount"),
            Int(roads, "directedLinksIntersectingUnion"),
            Int(roads, "administrativeDongLinkOccurrences"),
            Int(roads, "crossBoundaryDirectedLinks"),
            Int(roads, "exportedTwoPointSegments"),
            Int(roads, "exactBoundaryContainmentFailureSegments"),
            roads.GetProperty("boundaryContainmentToleranceMeters").GetDouble(),
            BsonDocument.Parse(auditElement.GetRawText()));
        Require(result.UnresolvedRepresentativePointCount == 13,
            "AdministrativeDongDioramaUnresolvedRepresentativePointCountChanged");
        Require(result.QuarantinedMalformedBuildingCount == 11,
            "AdministrativeDongDioramaMalformedQuarantineCountChanged");
        Require(result.QuarantinedRoundedFootprintBuildingCount == 3,
            "AdministrativeDongDioramaRoundedFootprintQuarantineCountChanged");
        Require(result.InteriorRingBuildingCount == 16 && result.InteriorRingOmittedCount == 18,
            "AdministrativeDongDioramaInteriorRingAuditChanged");
        Require(result.V1ContractAssignmentAnchorOverrideCount == 2,
            "AdministrativeDongDioramaAssignmentAnchorOverrideCountChanged");
        Require(result.ScopeUnresolvedCount
                == result.UnresolvedRepresentativePointCount
                   + result.QuarantinedMalformedBuildingCount
                   + result.QuarantinedRoundedFootprintBuildingCount,
            "AdministrativeDongDioramaScopeUnresolvedCountMismatch");
        Require(Int(buildings, "assigned") == modules.Sum(item => item.Input.Buildings.Length),
            "AdministrativeDongDioramaAssignedBuildingAuditMismatch");
        Require(Int(buildings, "assigned") == 61_897,
            "AdministrativeDongDioramaAssignedBuildingCountChanged");
        Require(Int(buildings, "ambiguous") == 0, "AdministrativeDongDioramaAmbiguousBuildingDetected");
        Require(result.DirectedRoadLinkCount == 4209
                && result.AdministrativeDongRoadLinkOccurrenceCount == 4620
                && result.CrossBoundaryDirectedRoadLinkCount == 397,
            "AdministrativeDongDioramaRoadAuditChanged");
        Require(result.ExportedRoadSegmentCount == modules.Sum(item => item.Input.Roads.Length),
            "AdministrativeDongDioramaExportedRoadAuditMismatch");
        Require(result.ExportedRoadSegmentCount == 11_769,
            "AdministrativeDongDioramaExportedRoadCountChanged");
        Require(result.ExactRoadBoundaryContainmentFailureSegmentCount == 454
                && Nearly(result.RoadBoundaryNumericToleranceMeters, 0.000001d),
            "AdministrativeDongDioramaRoadBoundaryToleranceAuditChanged");
        var completion = auditElement.GetProperty("completionGate");
        Require(Boolean(completion, "allAdministrativeAreasHaveBoundary")
                && Boolean(completion, "allAdministrativeAreasHaveBuildings")
                && Boolean(completion, "allAdministrativeAreasHaveRoadSegments")
                && !Boolean(completion, "currentAuthoritativeBoundaryAvailable")
                && !Boolean(completion, "publicDistributionApproved"),
            "AdministrativeDongDioramaAuditCompletionGateMismatch");
        return result;
    }

    private static CandidateDocuments BuildCandidateDocuments(CandidatePackage package)
    {
        var batchId = BatchId(package.BatchContentHash);
        var manifests = new List<BsonDocument>(package.Projections.Length);
        var tiles = new List<BsonDocument>(package.TileCount);
        var overlays = new List<BsonDocument>(package.Projections.Length);
        foreach (var item in package.Projections)
        {
            var hash = item.Projection.Manifest.ProjectionHashSha256;
            manifests.Add(new BsonDocument
            {
                ["_id"] = VersionedId("candidate-manifest", batchId, item.Definition.Id, hash),
                ["batchId"] = batchId,
                ["administrativeAreaStableId"] = item.Definition.Id,
                ["moduleFileHashSha256"] = item.FileHash,
                ["moduleContentHashSha256"] = item.ContentHash,
                ["projectionHashSha256"] = hash,
                ["desiredReadinessCode"] = DesiredReadiness,
                ["diagnosticCodes"] = new BsonArray(RequiredDiagnosticCodes),
                ["candidateEnvelopeIsPublicationAuthority"] = false,
                ["candidateEnvelopeReadinessGovernsPromotion"] = true,
                ["embeddedManifestReadinessIsInformational"] = true,
                ["embeddedManifestReadinessCode"] = item.Projection.Manifest.ReadinessCode,
                ["publishBlocked"] = true,
                ["currentPointerUpdated"] = false,
                ["manifest"] = ToBson(item.Projection.Manifest)
            });
            foreach (var tile in item.Projection.Tiles)
                tiles.Add(new BsonDocument
                {
                    ["_id"] = VersionedId("candidate-tile", batchId, item.Definition.Id, hash, tile.TileStableId),
                    ["batchId"] = batchId,
                    ["administrativeAreaStableId"] = item.Definition.Id,
                    ["projectionHashSha256"] = hash,
                    ["tileStableId"] = tile.TileStableId,
                    ["tileHashSha256"] = tile.TileHashSha256,
                    ["tile"] = ToBson(tile)
                });
            overlays.Add(new BsonDocument
            {
                ["_id"] = VersionedId("candidate-overlay", batchId, item.Definition.Id, hash),
                ["batchId"] = batchId,
                ["administrativeAreaStableId"] = item.Definition.Id,
                ["projectionHashSha256"] = hash,
                ["overlayRevision"] = item.Projection.DisplayOverlays.OverlayRevision,
                ["overlays"] = ToBson(item.Projection.DisplayOverlays)
            });
        }

        var areaSummaries = package.Projections.OrderBy(item => item.Definition.Id, StringComparer.Ordinal)
            .Select(item => new BsonDocument
            {
                ["administrativeAreaStableId"] = item.Definition.Id,
                ["displayName"] = item.Definition.DisplayName,
                ["legalAreaStableId"] = item.Definition.LegalId,
                ["moduleFileHashSha256"] = item.FileHash,
                ["moduleContentHashSha256"] = item.ContentHash,
                ["projectionHashSha256"] = item.Projection.Manifest.ProjectionHashSha256,
                ["buildingCount"] = item.Input.Buildings.Length,
                ["projectedBuildingCount"] = item.Projection.Tiles.Sum(tile => tile.Buildings.Length),
                ["roadInputSegmentCount"] = item.Input.Roads.Length,
                ["projectedRoadSegmentCount"] = item.Projection.Tiles.Sum(tile => tile.Roads.Length),
                ["tileCount"] = item.Projection.Tiles.Length,
                ["unresolvedBuildingCount"] = item.Input.UnresolvedBuildingCount
            });
        var batch = new BsonDocument
        {
            ["_id"] = batchId,
            ["schemaVersion"] = ScopeSchemaVersion,
            ["scopeStableId"] = ScopeStableId,
            ["revision"] = Revision,
            ["batchContentHashSha256"] = package.BatchContentHash,
            ["scopeContentHashSha256"] = package.ScopeContentHash,
            ["projectionSetHashSha256"] = package.ProjectionSetHash,
            ["projectionBuilderSemanticRevision"] = ProjectionBuilderSemanticRevision,
            ["projectionBuilderSourceHashSha256"] = package.ProjectionBuilderSourceHash,
            ["independentBoundaryComparisonToleranceMeters"] = IndependentBoundaryComparisonToleranceMeters,
            ["sourceVintage"] = SourceVintage,
            ["moisLedgerSourceVersion"] = package.Ledger.SourceVersion,
            ["moisLedgerDataRevision"] = package.Ledger.DataRevision,
            ["moisCrosswalkContentHashSha256"] = package.Ledger.CrosswalkHash,
            ["desiredReadinessCode"] = DesiredReadiness,
            ["diagnosticCodes"] = new BsonArray(RequiredDiagnosticCodes),
            ["candidateEnvelopeIsPublicationAuthority"] = false,
            ["candidateEnvelopeReadinessGovernsPromotion"] = true,
            ["embeddedManifestReadinessIsInformational"] = true,
            ["publishBlocked"] = true,
            ["currentPointerUpdated"] = false,
            ["distributionApproved"] = false,
            ["traversalReady"] = false,
            ["gameplayReady"] = false,
            ["administrativeAreaCount"] = package.Projections.Length,
            ["legalAreaCount"] = package.LegalAreaCount,
            ["buildingCount"] = package.BuildingCount,
            ["projectedBuildingCount"] = package.ProjectedBuildingCount,
            ["builderRejectedBuildingCount"] = package.BuildingCount - package.ProjectedBuildingCount,
            ["roadInputSegmentCount"] = package.RoadInputCount,
            ["projectedRoadSegmentCount"] = package.ProjectedRoadCount,
            ["tileCount"] = package.TileCount,
            ["unresolvedBuildingCount"] = package.UnresolvedBuildingCount,
            ["sourceAudit"] = package.Audit.Document.DeepClone().AsBsonDocument,
            ["areas"] = new BsonArray(areaSummaries)
        };
        return new CandidateDocuments(batch, manifests.ToArray(), tiles.ToArray(), overlays.ToArray());
    }

    private static async Task<(int Inserted, int Verified)> InsertOrVerifyAllAsync(
        MongoClient client,
        CandidateDocuments documents)
    {
        var database = client.GetDatabase("ssalddel_dev");
        await RequireBatchMarkerCompatibleBeforeChildWritesAsync(
            database.GetCollection<BsonDocument>(BatchCollection),
            documents.Batch);
        var inserted = 0;
        var verified = 0;
        foreach (var document in documents.Tiles)
            Count(await InsertOrVerifyAsync(database.GetCollection<BsonDocument>(TileCollection), document));
        foreach (var document in documents.Manifests)
            Count(await InsertOrVerifyAsync(database.GetCollection<BsonDocument>(ManifestCollection), document));
        foreach (var document in documents.Overlays)
            Count(await InsertOrVerifyAsync(database.GetCollection<BsonDocument>(OverlayCollection), document));
        // batch marker를 마지막에 써서 중간 종료를 완료 batch로 보이지 않게 한다.
        Count(await InsertOrVerifyAsync(database.GetCollection<BsonDocument>(BatchCollection), documents.Batch));
        return (inserted, verified);

        void Count(bool wasInserted)
        {
            if (wasInserted) inserted++;
            else verified++;
        }
    }

    private static async Task RequireBatchMarkerCompatibleBeforeChildWritesAsync(
        IMongoCollection<BsonDocument> collection,
        BsonDocument expected)
    {
        var existing = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", expected["_id"]))
            .SingleOrDefaultAsync();
        Require(existing is null || existing.Equals(expected),
            "AdministrativeDongDioramaCandidateBatchPreflightConflict:" + expected["_id"].AsString);
    }

    private static async Task<bool> InsertOrVerifyAsync(
        IMongoCollection<BsonDocument> collection,
        BsonDocument document)
    {
        try
        {
            await collection.InsertOneAsync(document);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", document["_id"]))
                .SingleOrDefaultAsync();
            Require(existing is not null && existing.Equals(document),
                "AdministrativeDongDioramaCandidateImmutableConflict:" + document["_id"].AsString);
            return false;
        }
    }

    private static async Task VerifyReadbackAsync(
        string root,
        CandidatePackage package)
    {
        var independent = await 공간자료CatalogImport.ConnectAsync(root);
        await VerifyCandidateDocumentsAsync(independent.Client, package);
        await RequireNoCandidateProjectionIsCurrentAsync(independent.Client, package);
    }

    private static async Task VerifyCandidateDocumentsAsync(
        MongoClient client,
        CandidatePackage package)
    {
        var expected = BuildCandidateDocuments(package);
        var database = client.GetDatabase("ssalddel_dev");
        await RequireExactAsync(database.GetCollection<BsonDocument>(BatchCollection), expected.Batch);
        var batchId = expected.Batch["_id"].AsString;

        foreach (var document in expected.Manifests)
            await RequireExactAsync(database.GetCollection<BsonDocument>(ManifestCollection), document);
        foreach (var document in expected.Tiles)
            await RequireExactAsync(database.GetCollection<BsonDocument>(TileCollection), document);
        foreach (var document in expected.Overlays)
            await RequireExactAsync(database.GetCollection<BsonDocument>(OverlayCollection), document);

        var batchFilter = Builders<BsonDocument>.Filter.Eq("batchId", batchId);
        Require(await database.GetCollection<BsonDocument>(ManifestCollection).CountDocumentsAsync(batchFilter)
                == expected.Manifests.Length, "AdministrativeDongDioramaCandidateManifestReadbackCountMismatch");
        Require(await database.GetCollection<BsonDocument>(TileCollection).CountDocumentsAsync(batchFilter)
                == expected.Tiles.Length, "AdministrativeDongDioramaCandidateTileReadbackCountMismatch");
        Require(await database.GetCollection<BsonDocument>(OverlayCollection).CountDocumentsAsync(batchFilter)
                == expected.Overlays.Length, "AdministrativeDongDioramaCandidateOverlayReadbackCountMismatch");
    }

    private static async Task RequireNoCandidateProjectionIsCurrentAsync(
        MongoClient client,
        CandidatePackage package)
    {
        var candidateHashes = package.Projections.Select(item => item.Projection.Manifest.ProjectionHashSha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var current = await client.GetDatabase("ssalddel_dev").GetCollection<BsonDocument>(CurrentCollection)
            .Find(Builders<BsonDocument>.Filter.In("_id", package.AreaIds)).ToListAsync();
        foreach (var document in current)
        {
            var hash = document.TryGetValue("ProjectionHashSha256", out var pascal)
                ? pascal.AsString
                : document.TryGetValue("projectionHashSha256", out var camel) ? camel.AsString : string.Empty;
            Require(string.IsNullOrWhiteSpace(hash) || !candidateHashes.Contains(hash),
                "AdministrativeDongDioramaCandidateProjectionPublishedToCurrent:" + document["_id"].AsString);
        }
    }

    private static async Task RequireExactAsync(IMongoCollection<BsonDocument> collection, BsonDocument expected)
    {
        var actual = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", expected["_id"]))
            .SingleOrDefaultAsync();
        Require(actual is not null && actual.Equals(expected),
            "AdministrativeDongDioramaCandidateReadbackMismatch:" + expected["_id"].AsString);
    }

    private static async Task<CurrentPointerSnapshot> ReadCurrentPointerSnapshotAsync(
        MongoClient client,
        IReadOnlyCollection<string> areaIds)
    {
        var collection = client.GetDatabase("ssalddel_dev").GetCollection<BsonDocument>(CurrentCollection);
        var documents = await collection.Find(Builders<BsonDocument>.Filter.In("_id", areaIds))
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .ToListAsync();
        var canonical = string.Join("\n", documents.Select(document => document.ToJson()));
        return new CurrentPointerSnapshot(documents.Count, Hash(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void ValidateScopeDefinitionAndToolchain(
        string root,
        JsonElement definition,
        JsonElement generatedScope,
        string definitionHash)
    {
        Require(String(definition, "scopeStableId") == ScopeStableId
                && String(definition, "revision") == Revision
                && String(definition, "sourceVintage") == SourceVintage,
            "AdministrativeDongDioramaScopeDefinitionIdentityChanged");
        var output = definition.GetProperty("output");
        Require(String(output, "repositoryRelativeDirectory")
                    .Replace('\\', '/') == ArtifactRelative.Replace('\\', '/'),
            "AdministrativeDongDioramaScopeOutputDirectoryChanged");
        var configured = output.GetProperty("toolchain");
        var receipt = generatedScope.GetProperty("toolchain");
        var configuredProperties = configured.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        var receiptProperties = receipt.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        Require(receiptProperties.Count == configuredProperties.Count + 1,
            "AdministrativeDongDioramaToolchainReceiptShapeChanged");
        foreach (var pair in configuredProperties)
            Require(receiptProperties.TryGetValue(pair.Key, out var actual)
                    && SameJsonScalar(actual, pair.Value),
                "AdministrativeDongDioramaToolchainReceiptMismatch:" + pair.Key);
        Require(receiptProperties.TryGetValue("scopeDefinitionSha256", out var scopeHash)
                && scopeHash.ValueKind == JsonValueKind.String
                && string.Equals(scopeHash.GetString(), definitionHash, StringComparison.OrdinalIgnoreCase),
            "AdministrativeDongDioramaScopeDefinitionReceiptMismatch");

        var generatorRelative = String(configured, "generatorRelativePath")
            .Replace('/', Path.DirectorySeparatorChar);
        var generatorPath = Path.GetFullPath(Path.Combine(root, generatorRelative));
        SafeRead(generatorPath, root, 2 * 1024 * 1024);
        Require(HashFile(generatorPath) == String(configured, "generatorSha256").ToUpperInvariant(),
            "AdministrativeDongDioramaGeneratorSourceHashChanged");
    }

    private static bool SameJsonScalar(JsonElement first, JsonElement second)
    {
        if (first.ValueKind == JsonValueKind.Number && second.ValueKind == JsonValueKind.Number)
            return first.TryGetDecimal(out var firstDecimal) && second.TryGetDecimal(out var secondDecimal)
                ? firstDecimal == secondDecimal
                : Nearly(first.GetDouble(), second.GetDouble());
        return first.ValueKind == second.ValueKind && first.GetRawText() == second.GetRawText();
    }

    private static string ComputeProjectionSetHash(IReadOnlyList<ModuleProjection> modules)
    {
        var receipt = modules.OrderBy(item => item.Definition.Id, StringComparer.Ordinal)
            .Select(item => new
            {
                administrativeAreaStableId = item.Definition.Id,
                moduleContentHashSha256 = item.ContentHash,
                manifest = item.Projection.Manifest,
                tiles = item.Projection.Tiles.OrderBy(tile => tile.TileStableId, StringComparer.Ordinal).ToArray(),
                displayOverlays = item.Projection.DisplayOverlays
            }).ToArray();
        return HashCanonicalWithBlankContentHash(JsonSerializer.SerializeToElement(receipt, JsonOptions));
    }

    private static string ComputeBatchContentHash(
        string scopeContentHash,
        string projectionSetHash,
        string projectionBuilderSourceHash)
        => HashCanonicalWithBlankContentHash(JsonSerializer.SerializeToElement(new
        {
            scopeContentHashSha256 = scopeContentHash,
            projectionSetHashSha256 = projectionSetHash,
            projectionBuilderSemanticRevision = ProjectionBuilderSemanticRevision,
            projectionBuilderSourceHashSha256 = projectionBuilderSourceHash
        }, JsonOptions));

    private static AdministrativeDongDioramaPoint[] CanonicalOneMillimeterBoundary(
        IReadOnlyList<AdministrativeDongDioramaPoint> source)
    {
        var compact = new List<AdministrativeDongDioramaPoint>(source.Count);
        foreach (var point in source)
        {
            var rounded = new AdministrativeDongDioramaPoint
            {
                X = RoundMillimeter(point.X),
                Z = RoundMillimeter(point.Z)
            };
            if (compact.Count == 0 || !SamePoint(compact[^1], rounded)) compact.Add(rounded);
        }
        if (compact.Count > 1 && SamePoint(compact[0], compact[^1])) compact.RemoveAt(compact.Count - 1);
        Require(compact.Count >= 3 && compact.All(point => double.IsFinite(point.X) && double.IsFinite(point.Z)),
            "AdministrativeDongDioramaIndependentBoundaryInvalid");
        var distinct = compact.Select(point => (point.X, point.Z)).Distinct().Count();
        Require(distinct == compact.Count, "AdministrativeDongDioramaIndependentBoundaryRepeatedVertex");
        if (SignedDoubleArea(compact) > 0d) compact.Reverse();

        var minimumIndex = 0;
        for (var index = 1; index < compact.Count; index++)
            if (compact[index].X < compact[minimumIndex].X
                || Nearly(compact[index].X, compact[minimumIndex].X)
                && compact[index].Z < compact[minimumIndex].Z)
                minimumIndex = index;
        return Enumerable.Range(0, compact.Count)
            .Select(offset => compact[(minimumIndex + offset) % compact.Count])
            .ToArray();
    }

    private static string? BoundaryMismatch(
        IReadOnlyList<AdministrativeDongDioramaPoint> generated,
        IReadOnlyList<AdministrativeDongDioramaPoint> independent)
    {
        var normalized = NormalizeGeneratedBoundary(generated);
        if (normalized.Length != independent.Count)
            return "count=" + normalized.Length + "/" + independent.Count;
        for (var index = 0; index < normalized.Length; index++)
            if (Distance(normalized[index], independent[index]) > IndependentBoundaryComparisonToleranceMeters)
                return "index=" + index
                       + ":generated=" + normalized[index].X.ToString("R") + "," + normalized[index].Z.ToString("R")
                       + ":independent=" + independent[index].X.ToString("R") + "," + independent[index].Z.ToString("R");
        return null;
    }

    private static AdministrativeDongDioramaPoint[] NormalizeGeneratedBoundary(
        IReadOnlyList<AdministrativeDongDioramaPoint> generated)
    {
        var normalized = generated.Select(point => new AdministrativeDongDioramaPoint { X = point.X, Z = point.Z })
            .ToArray();
        if (normalized.Length > 1 && SamePoint(normalized[0], normalized[^1])) normalized = normalized[..^1];
        return normalized;
    }

    private static double SignedDoubleArea(IReadOnlyList<AdministrativeDongDioramaPoint> polygon)
    {
        var area = 0d;
        for (var index = 0; index < polygon.Count; index++)
        {
            var next = (index + 1) % polygon.Count;
            area += polygon[index].X * polygon[next].Z - polygon[next].X * polygon[index].Z;
        }
        return area;
    }

    private static double RoundMillimeter(double value)
    {
        var rounded = Math.Round(value, 3, MidpointRounding.ToEven);
        return rounded == 0d ? 0d : rounded;
    }

    private static bool SamePoint(AdministrativeDongDioramaPoint first, AdministrativeDongDioramaPoint second)
        => Nearly(first.X, second.X) && Nearly(first.Z, second.Z);
    private static double Distance(AdministrativeDongDioramaPoint first, AdministrativeDongDioramaPoint second)
    {
        var x = first.X - second.X;
        var z = first.Z - second.Z;
        return Math.Sqrt(x * x + z * z);
    }

    private static void ValidateCoordinateFrame(AdministrativeDongDioramaCoordinateFrame frame)
    {
        Require(frame.Method == "WGS84-ECEF-ENU-at-zero-altitude"
                && Nearly(frame.OriginLatitude, 37.580912d)
                && Nearly(frame.OriginLongitude, 127.088502d)
                && Nearly(frame.WorldOffsetX, 0d) && Nearly(frame.WorldOffsetZ, 0d)
                && Nearly(frame.MetersPerUnit, 1d),
            "AdministrativeDongDioramaCoordinateFrameChanged");
    }

    private static bool SameFrame(
        AdministrativeDongDioramaCoordinateFrame first,
        AdministrativeDongDioramaCoordinateFrame second)
        => first.Method == second.Method && Nearly(first.OriginLatitude, second.OriginLatitude)
                                         && Nearly(first.OriginLongitude, second.OriginLongitude)
                                         && Nearly(first.WorldOffsetX, second.WorldOffsetX)
                                         && Nearly(first.WorldOffsetZ, second.WorldOffsetZ)
                                         && Nearly(first.MetersPerUnit, second.MetersPerUnit);

    private static string HashCanonicalWithBlankContentHash(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                   Indented = false
               }))
            WriteCanonical(writer, element, isRoot: true);
        return Hash(stream.ToArray());
    }

    private static string HashCanonical(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                   Indented = false
               }))
            WriteCanonical(writer, element);
        return Hash(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element, bool isRoot = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    if (isRoot && property.NameEquals("contentHashSha256")) writer.WriteStringValue(string.Empty);
                    else WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
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
                throw new InvalidDataException("AdministrativeDongDioramaCanonicalJsonUnsupported");
        }
    }

    private static BsonDocument ToBson<T>(T value)
        => BsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));

    private static void SafeRead(string path, string allowedRoot, long maxBytes)
    {
        var full = Path.GetFullPath(path);
        var safeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot));
        Require(full.StartsWith(safeRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "AdministrativeDongDioramaBatchPathOutsideRoot");
        for (var item = new FileInfo(full).Directory; item is not null; item = item.Parent)
            if (item.Exists)
                Require(!item.Attributes.HasFlag(FileAttributes.ReparsePoint),
                    "AdministrativeDongDioramaBatchReparseRejected");
        var file = new FileInfo(full);
        Require(file.Exists && file.Length is > 0 && file.Length <= maxBytes
                && !file.Attributes.HasFlag(FileAttributes.ReparsePoint),
            "AdministrativeDongDioramaBatchInputMissingOrInvalid:" + full);
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string BatchId(string hash) => "candidate-batch:" + hash.ToLowerInvariant();
    private static string VersionedId(params string[] parts)
        => string.Join("|", parts.Select(part => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(part)))));
    private static string String(JsonElement element, string name)
        => element.GetProperty(name).GetString() ?? throw new InvalidDataException("AdministrativeDongDioramaJsonStringMissing:" + name);
    private static bool Boolean(JsonElement element, string name) => element.GetProperty(name).GetBoolean();
    private static int Int(JsonElement element, string name) => element.GetProperty(name).GetInt32();
    private static DateTime Date(JsonElement element, string name)
        => element.GetProperty(name).GetDateTime().ToUniversalTime();
    private static HashSet<string> Set(JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(item => item.GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character));
    private static bool Nearly(double first, double second) => Math.Abs(first - second) <= 0.000000001d;
    private static string HistoricalBoundaryDisplayName(string officialDisplayName)
        => officialDisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1]
            .Replace("제", string.Empty, StringComparison.Ordinal)
            .Replace('.', '·');
    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed record AreaDefinition(string Id, string DisplayName, string LegalId);
    private sealed record ModuleProjection(
        AreaDefinition Definition,
        string FileHash,
        string ContentHash,
        행정동디오라마ProjectionBuildInput Input,
        행정동디오라마ProjectionBuildResult Projection);
    private sealed record CurrentPointerSnapshot(int Count, string Hash);
    private sealed record OfficialLedger(
        string SourceVersion,
        string DataRevision,
        string CrosswalkHash,
        DateTimeOffset EvidenceAsOfUtc);
    private sealed record ScopeAudit(
        int UnresolvedRepresentativePointCount,
        int QuarantinedMalformedBuildingCount,
        int QuarantinedRoundedFootprintBuildingCount,
        int InteriorRingBuildingCount,
        int InteriorRingOmittedCount,
        int MultipartOmittedBuildingCount,
        int BuildingLegalAreaMismatchCount,
        int DuplicateSourceBuildingIdOccurrenceCount,
        int V1ContractAssignmentAnchorOverrideCount,
        int ScopeUnresolvedCount,
        int DirectedRoadLinkCount,
        int AdministrativeDongRoadLinkOccurrenceCount,
        int CrossBoundaryDirectedRoadLinkCount,
        int ExportedRoadSegmentCount,
        int ExactRoadBoundaryContainmentFailureSegmentCount,
        double RoadBoundaryNumericToleranceMeters,
        BsonDocument Document);
    private sealed record CandidateDocuments(
        BsonDocument Batch,
        BsonDocument[] Manifests,
        BsonDocument[] Tiles,
        BsonDocument[] Overlays)
    {
        public IEnumerable<BsonDocument> All => Tiles.Concat(Manifests).Concat(Overlays).Append(Batch);
    }

    private sealed record UnityReviewExportResult(
        bool IndependentReadbackVerified,
        string IndexPath,
        string IndexHash,
        string IndexContentHash,
        string BundleSetHash,
        string GenerationHash,
        string CompletionMarkerPath,
        string CompletionMarkerHash,
        string ExporterSemanticRevision,
        string ExporterSourceHash,
        int BundleCount,
        int ChangedFileCount,
        long ByteLength);

    private sealed record UnityReviewBundleArtifact(
        UnityReviewIndexEntry Entry,
        UnityReviewBundle Bundle,
        byte[] Bytes);

    private sealed record UnityReviewIndex
    {
        public string SchemaVersion { get; init; } = UnityReviewIndexSchemaVersion;
        public string ScopeStableId { get; init; } = string.Empty;
        public string Revision { get; init; } = string.Empty;
        public string SourceVintage { get; init; } = string.Empty;
        public DateTime GeneratedAtUtc { get; init; }
        public string CandidateBatchId { get; init; } = string.Empty;
        public string BatchContentHashSha256 { get; init; } = string.Empty;
        public string ScopeContentHashSha256 { get; init; } = string.Empty;
        public string ProjectionSetHashSha256 { get; init; } = string.Empty;
        public string ProjectionBuilderSemanticRevision { get; init; } = string.Empty;
        public string ProjectionBuilderSourceHashSha256 { get; init; } = string.Empty;
        public string ExporterSemanticRevision { get; init; } = string.Empty;
        public string ExporterSourceHashSha256 { get; init; } = string.Empty;
        public string GenerationHashSha256 { get; init; } = string.Empty;
        public string BundleSetHashSha256 { get; init; } = string.Empty;
        public string DesiredReadinessCode { get; init; } = string.Empty;
        public string[] DiagnosticCodes { get; init; } = [];
        public bool CandidateEnvelopeIsPublicationAuthority { get; init; }
        public bool CandidateEnvelopeReadinessGovernsPromotion { get; init; } = true;
        public bool EmbeddedManifestReadinessIsInformational { get; init; } = true;
        public bool PublishBlocked { get; init; } = true;
        public bool CurrentPointerUsed { get; init; }
        public bool CurrentPointerUpdated { get; init; }
        public bool ObservationPresentationOnly { get; init; } = true;
        public bool DistributionApproved { get; init; }
        public bool TraversalReady { get; init; }
        public bool GameplayReady { get; init; }
        public bool BaseGeometryCaptureReady { get; init; } = true;
        public bool LifeDioramaCaptureReady { get; init; }
        public bool PersonalDataIncluded { get; init; }
        public string[] MissingLayerCodes { get; init; } = [];
        public int AdministrativeAreaCount { get; init; }
        public int LegalAreaCount { get; init; }
        public int BundleCount { get; init; }
        public int BuildingCount { get; init; }
        public int RoadSegmentCount { get; init; }
        public int TileCount { get; init; }
        public int UnresolvedBuildingCount { get; init; }
        public int PublicBusinessCount { get; init; }
        public int DisplayOverlayItemCount { get; init; }
        public UnityReviewIndexEntry[] Bundles { get; init; } = [];
        public string ContentHashSha256 { get; init; } = string.Empty;
    }

    private sealed record UnityReviewIndexEntry
    {
        public string AdministrativeAreaStableId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string LegalAreaStableId { get; init; } = string.Empty;
        public string RelativePath { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
        public long ByteLength { get; init; }
        public string ContentHashSha256 { get; init; } = string.Empty;
        public string ModuleFileHashSha256 { get; init; } = string.Empty;
        public string ModuleContentHashSha256 { get; init; } = string.Empty;
        public string ProjectionHashSha256 { get; init; } = string.Empty;
        public int BuildingCount { get; init; }
        public int RoadSegmentCount { get; init; }
        public int TileCount { get; init; }
        public int UnresolvedBuildingCount { get; init; }
        public bool BaseGeometryCaptureReady { get; init; }
        public bool LifeDioramaCaptureReady { get; init; }
        public string[] MissingLayerCodes { get; init; } = [];
    }

    private sealed record UnityReviewBundle
    {
        public string SchemaVersion { get; init; } = UnityReviewBundleSchemaVersion;
        public string ScopeStableId { get; init; } = string.Empty;
        public string Revision { get; init; } = string.Empty;
        public string SourceVintage { get; init; } = string.Empty;
        public DateTime GeneratedAtUtc { get; init; }
        public string CandidateBatchId { get; init; } = string.Empty;
        public string BatchContentHashSha256 { get; init; } = string.Empty;
        public string ScopeContentHashSha256 { get; init; } = string.Empty;
        public string ProjectionSetHashSha256 { get; init; } = string.Empty;
        public string ProjectionBuilderSemanticRevision { get; init; } = string.Empty;
        public string ProjectionBuilderSourceHashSha256 { get; init; } = string.Empty;
        public string ExporterSemanticRevision { get; init; } = string.Empty;
        public string ExporterSourceHashSha256 { get; init; } = string.Empty;
        public string AdministrativeAreaStableId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string LegalAreaStableId { get; init; } = string.Empty;
        public string ModuleFileHashSha256 { get; init; } = string.Empty;
        public string ModuleContentHashSha256 { get; init; } = string.Empty;
        public string ProjectionHashSha256 { get; init; } = string.Empty;
        public string DesiredReadinessCode { get; init; } = string.Empty;
        public string[] DiagnosticCodes { get; init; } = [];
        public bool CandidateEnvelopeIsPublicationAuthority { get; init; }
        public bool CandidateEnvelopeReadinessGovernsPromotion { get; init; } = true;
        public bool EmbeddedManifestReadinessIsInformational { get; init; } = true;
        public string EmbeddedManifestReadinessCode { get; init; } = string.Empty;
        public bool PublishBlocked { get; init; } = true;
        public bool CurrentPointerUsed { get; init; }
        public bool CurrentPointerUpdated { get; init; }
        public bool ObservationPresentationOnly { get; init; } = true;
        public bool DistributionApproved { get; init; }
        public bool TraversalReady { get; init; }
        public bool GameplayReady { get; init; }
        public bool BaseGeometryCaptureReady { get; init; } = true;
        public bool LifeDioramaCaptureReady { get; init; }
        public bool PersonalDataIncluded { get; init; }
        public int PublicBusinessCount { get; init; }
        public string[] MissingLayerCodes { get; init; } = [];
        public int BuildingCount { get; init; }
        public int RoadSegmentCount { get; init; }
        public int TileCount { get; init; }
        public int UnresolvedBuildingCount { get; init; }
        public AdministrativeDongDioramaManifest Manifest { get; init; } = new();
        public AdministrativeDongDioramaTile[] Tiles { get; init; } = [];
        public AdministrativeDongDisplayOverlayResponse DisplayOverlays { get; init; } = new();
        public string ContentHashSha256 { get; init; } = string.Empty;
    }

    private sealed record UnityReviewCompletion
    {
        public string SchemaVersion { get; init; } = UnityReviewCompletionSchemaVersion;
        public string ScopeStableId { get; init; } = string.Empty;
        public string Revision { get; init; } = string.Empty;
        public DateTime GeneratedAtUtc { get; init; }
        public string GenerationHashSha256 { get; init; } = string.Empty;
        public string IndexRelativePath { get; init; } = string.Empty;
        public string IndexSha256 { get; init; } = string.Empty;
        public string IndexContentHashSha256 { get; init; } = string.Empty;
        public string BundleSetHashSha256 { get; init; } = string.Empty;
        public int BundleCount { get; init; }
        public string ExporterSemanticRevision { get; init; } = string.Empty;
        public string ExporterSourceHashSha256 { get; init; } = string.Empty;
        public string DesiredReadinessCode { get; init; } = DesiredReadiness;
        public string[] DiagnosticCodes { get; init; } = [.. RequiredDiagnosticCodes];
        public bool CandidateEnvelopeIsPublicationAuthority { get; init; }
        public bool CandidateEnvelopeReadinessGovernsPromotion { get; init; } = true;
        public bool EmbeddedManifestReadinessIsInformational { get; init; } = true;
        public bool PublishBlocked { get; init; } = true;
        public bool CurrentPointerUsed { get; init; }
        public bool CurrentPointerUpdated { get; init; }
        public bool ObservationPresentationOnly { get; init; } = true;
        public bool DistributionApproved { get; init; }
        public bool TraversalReady { get; init; }
        public bool GameplayReady { get; init; }
        public bool BaseGeometryCaptureReady { get; init; } = true;
        public bool LifeDioramaCaptureReady { get; init; }
        public bool PersonalDataIncluded { get; init; }
        public int PublicBusinessCount { get; init; }
        public int DisplayOverlayItemCount { get; init; }
        public string[] MissingLayerCodes { get; init; } = [.. MissingLifeLayerCodes];
        public string ContentHashSha256 { get; init; } = string.Empty;
    }

    private sealed class CandidatePackage
    {
        public CandidatePackage(
            string batchContentHash,
            string scopeContentHash,
            string projectionSetHash,
            string projectionBuilderSourceHash,
            ModuleProjection[] projections,
            ScopeAudit audit,
            OfficialLedger ledger)
        {
            BatchContentHash = batchContentHash;
            ScopeContentHash = scopeContentHash;
            ProjectionSetHash = projectionSetHash;
            ProjectionBuilderSourceHash = projectionBuilderSourceHash;
            Projections = projections;
            Audit = audit;
            Ledger = ledger;
        }

        public string BatchContentHash { get; }
        public string ScopeContentHash { get; }
        public string ProjectionSetHash { get; }
        public string ProjectionBuilderSourceHash { get; }
        public ModuleProjection[] Projections { get; }
        public ScopeAudit Audit { get; }
        public OfficialLedger Ledger { get; }
        public string[] AreaIds => Projections.Select(item => item.Definition.Id).ToArray();
        public int LegalAreaCount => Projections.Select(item => item.Definition.LegalId).Distinct(StringComparer.Ordinal).Count();
        public int BuildingCount => Projections.Sum(item => item.Input.Buildings.Length);
        public int ProjectedBuildingCount => Projections.Sum(item => item.Projection.Tiles.Sum(tile => tile.Buildings.Length));
        public int RoadInputCount => Projections.Sum(item => item.Input.Roads.Length);
        public int ProjectedRoadCount => Projections.Sum(item => item.Projection.Tiles.Sum(tile => tile.Roads.Length));
        public int TileCount => Projections.Sum(item => item.Projection.Tiles.Length);
        public int UnresolvedBuildingCount => Audit.ScopeUnresolvedCount;
    }
}
