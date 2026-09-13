using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class StationDioramaPolicy
    {
        public const string SchemaVersion = "station-diorama.v1";
        public const string CatalogSchemaVersion = "station-diorama-catalog.v1";
        public const string SourceRowHashCanonicalVersion = "station-diorama-source-row.v1";
        public const int StandardWindowMeters = 1000;
        public const string MyeonmokTransitStationStableId = "station:kr:kric:s1107:0721";
        public const string SagajeongTransitStationStableId = "station:kr:kric:s1107:0722";
        public const string YongmasanTransitStationStableId = "station:kr:kric:s1107:0723";

        public static bool IsTransitStationStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim();
            const string prefix = "station:kr:kric:";
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var parts = normalized.Split(':');
            if (parts.Length != 5 || parts[3].Length == 0 || parts[4].Length == 0) return false;
            for (var partIndex = 3; partIndex < parts.Length; partIndex++)
            for (var index = 0; index < parts[partIndex].Length; index++)
            {
                var character = parts[partIndex][index];
                if ((character < 'a' || character > 'z')
                    && (character < '0' || character > '9')
                    && character != '-')
                    return false;
            }
            return true;
        }
    }

    public static class StationDioramaRoutes
    {
        public const string Catalog = "api/v1/world/stations";
        public const string Manifest = Catalog + "/{transitStationStableId}/diorama-manifest";
    }

    public static class StationDioramaAvailabilityCodes
    {
        public const string WaitingForSpatialCoverage = "WaitingForSpatialCoverage";
        public const string PartialCoverage = "PartialCoverage";
        public const string Ready = "Ready";
    }

    public static class StationDioramaStationTypeCodes
    {
        public const string Normal = "Normal";
        public const string MajorInterchange = "MajorInterchange";
    }

    public static class StationDioramaNameConfirmationStatusCodes
    {
        public const string SourceNameConfirmed = "SourceNameConfirmed";
        public const string UserConfirmedDisplayName = "UserConfirmedDisplayName";
        public const string PendingUserNameConfirmation = "PendingUserNameConfirmation";
    }

    public static class StationDioramaWindowPolicyCodes
    {
        public const string FixedStationCentered = "FixedStationCentered";
        public const string MajorInterchangeVariable = "MajorInterchangeVariable";
    }

    public static class StationDioramaQualityCodes
    {
        public const string OfficialSourcePendingHumanReview = "OfficialSourcePendingHumanReview";
        public const string SourceReportedWgs84Point = "SourceReportedWgs84Point";
        public const string NotPlatformFieldSurvey = "NotPlatformFieldSurvey";
        public const string SpatialCoverageNotPublished = "SpatialCoverageNotPublished";
        public const string KnownPartialSpatialCoverage = "KnownPartialSpatialCoverage";
    }

    public static class StationDioramaRightsCodes
    {
        public const string PrivatePreviewOnly = "PrivatePreviewOnly";
    }

    public static class StationDioramaSpatialSnapshotStatusCodes
    {
        public const string LocalPrivateReview = "LocalPrivateReview";
    }

    public sealed class StationDioramaServiceIdentifier
    {
        public string OperatorName { get; set; } = string.Empty;
        public string LineCode { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
    }

    public sealed class StationDioramaAnchor
    {
        public string CoordinateReferenceSystemCode { get; set; } = "EPSG:4326";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string QualityCode { get; set; } = StationDioramaQualityCodes.SourceReportedWgs84Point;
        public string QualityNote { get; set; } = string.Empty;
    }

    public sealed class StationDioramaWindow
    {
        public string PolicyCode { get; set; } = StationDioramaWindowPolicyCodes.FixedStationCentered;
        public int WidthMeters { get; set; } = StationDioramaPolicy.StandardWindowMeters;
        public int DepthMeters { get; set; } = StationDioramaPolicy.StandardWindowMeters;
        public bool MajorInterchangeOverrideApplied { get; set; }
    }

    public sealed class StationDioramaSourceEvidence
    {
        public string SourceId { get; set; } = string.Empty;
        public string DatasetId { get; set; } = string.Empty;
        public string OfficialSourcePageUrl { get; set; } = string.Empty;
        public string ProviderSourcePageUrl { get; set; } = string.Empty;
        public string SourceRevision { get; set; } = string.Empty;
        public string DataRevision { get; set; } = string.Empty;
        public string TargetRowReferenceDate { get; set; } = string.Empty;
        public string RawContentHashSha256 { get; set; } = string.Empty;
        public DateTime CollectedAtUtc { get; set; }
        public string SourceRowReference { get; set; } = string.Empty;
        public string SourceRowHashCanonicalVersion { get; set; } =
            StationDioramaPolicy.SourceRowHashCanonicalVersion;
        public string SourceRowHashSha256 { get; set; } = string.Empty;
        public string LicenseObserved { get; set; } = string.Empty;
        public string QualityCode { get; set; } = StationDioramaQualityCodes.OfficialSourcePendingHumanReview;
        public string ReviewStatusCode { get; set; } = string.Empty;
        public string LimitationCode { get; set; } = string.Empty;
        public string RightsCode { get; set; } = StationDioramaRightsCodes.PrivatePreviewOnly;
        public bool DistributionApproved { get; set; }
    }

    public sealed class StationDioramaAreaProjectionReference
    {
        public string AdministrativeAreaStableId { get; set; } = string.Empty;
        public string ProjectionHashSha256 { get; set; } = string.Empty;
        public string ReadinessCode { get; set; } = string.Empty;
        public string ManifestRouteTemplate { get; set; } = AdministrativeDongDioramaRoutes.Manifest;
        public string TileRouteTemplate { get; set; } = AdministrativeDongDioramaRoutes.Tile;
        public AdministrativeDongDioramaCoordinateFrame CoordinateFrame { get; set; } =
            new AdministrativeDongDioramaCoordinateFrame();
        public AdministrativeDongDioramaBounds WindowBounds { get; set; } =
            new AdministrativeDongDioramaBounds();
        public AdministrativeDongDioramaTileSummary[] SelectedTiles { get; set; } =
            Array.Empty<AdministrativeDongDioramaTileSummary>();
        public AdministrativeDongDioramaSourceAttribution[] Sources { get; set; } =
            Array.Empty<AdministrativeDongDioramaSourceAttribution>();
    }

    /// <summary>
    /// 역 중심 공간 사본이 존재한다는 계보만 공개합니다.
    /// ServerLoadable이 false인 후보는 API 제공·배포·gameplay 준비 완료를 의미하지 않습니다.
    /// </summary>
    public sealed class StationDioramaSpatialSnapshotCandidate
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string ContentHashSha256 { get; set; } = string.Empty;
        public string PayloadHashSha256 { get; set; } = string.Empty;
        public long PayloadByteLength { get; set; }
        public int BuildingCount { get; set; }
        public int RoadCount { get; set; }
        public int SurfaceCount { get; set; }
        public int AdministrativeAreaCount { get; set; }
        public int CoverageCellCount { get; set; }
        public int MissingCoverageCellCount { get; set; }
        public string[] MissingCoverageCodes { get; set; } = Array.Empty<string>();
        public string ReviewStatusCode { get; set; } =
            StationDioramaSpatialSnapshotStatusCodes.LocalPrivateReview;
        public bool ServerLoadable { get; set; }
        public bool DistributionApproved { get; set; }
        public bool TraversalReady { get; set; }
        public bool GameplayReady { get; set; }
    }

    public sealed class StationDioramaSummary
    {
        public string TransitStationStableId { get; set; } = string.Empty;
        public string OfficialStationName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string NameConfirmationStatusCode { get; set; } =
            StationDioramaNameConfirmationStatusCodes.SourceNameConfirmed;
        public StationDioramaServiceIdentifier Service { get; set; } = new StationDioramaServiceIdentifier();
        public string StationTypeCode { get; set; } = StationDioramaStationTypeCodes.Normal;
        public int WindowWidthMeters { get; set; } = StationDioramaPolicy.StandardWindowMeters;
        public int WindowDepthMeters { get; set; } = StationDioramaPolicy.StandardWindowMeters;
        public string ManifestRevision { get; set; } = string.Empty;
        public string ManifestHashSha256 { get; set; } = string.Empty;
        public string AvailabilityCode { get; set; } = StationDioramaAvailabilityCodes.WaitingForSpatialCoverage;
        public bool DistributionApproved { get; set; }
    }

    public sealed class StationDioramaCatalogResponse
    {
        public string SchemaVersion { get; set; } = StationDioramaPolicy.CatalogSchemaVersion;
        public string CatalogRevision { get; set; } = string.Empty;
        public StationDioramaSummary[] Items { get; set; } = Array.Empty<StationDioramaSummary>();
    }

    public sealed class StationDioramaManifest
    {
        public string SchemaVersion { get; set; } = StationDioramaPolicy.SchemaVersion;
        public string TransitStationStableId { get; set; } = string.Empty;
        public string OfficialStationName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string NameConfirmationStatusCode { get; set; } =
            StationDioramaNameConfirmationStatusCodes.SourceNameConfirmed;
        public string[] LegacyAliases { get; set; } = Array.Empty<string>();
        public StationDioramaServiceIdentifier Service { get; set; } = new StationDioramaServiceIdentifier();
        public string StationTypeCode { get; set; } = StationDioramaStationTypeCodes.Normal;
        public StationDioramaAnchor Anchor { get; set; } = new StationDioramaAnchor();
        public StationDioramaWindow Window { get; set; } = new StationDioramaWindow();
        public string ManifestRevision { get; set; } = string.Empty;
        public string ManifestHashSha256 { get; set; } = string.Empty;
        public string AvailabilityCode { get; set; } = StationDioramaAvailabilityCodes.WaitingForSpatialCoverage;
        public string RegionStableId { get; set; } = string.Empty;
        public string RegionManifestHashSha256 { get; set; } = string.Empty;
        public string SpatialRegistryStableId { get; set; } = string.Empty;
        public string SpatialRegistryRevision { get; set; } = string.Empty;
        public string[] SpatialPackageStableIds { get; set; } = Array.Empty<string>();
        public string[] AdministrativeAreaStableIds { get; set; } = Array.Empty<string>();
        public string[] LegalAreaStableIds { get; set; } = Array.Empty<string>();
        public RegionExperienceLayerEndpoint[] GeographyEndpoints { get; set; } =
            Array.Empty<RegionExperienceLayerEndpoint>();
        public StationDioramaAreaProjectionReference[] AreaProjections { get; set; } =
            Array.Empty<StationDioramaAreaProjectionReference>();
        public StationDioramaSpatialSnapshotCandidate? SpatialSnapshotCandidate { get; set; }
        public StationDioramaSourceEvidence[] StationSources { get; set; } =
            Array.Empty<StationDioramaSourceEvidence>();
        public string[] QualityCodes { get; set; } = Array.Empty<string>();
        public bool ObservationPresentationOnly { get; set; } = true;
        public bool TraversalReady { get; set; }
        public bool GameplayReady { get; set; }
        public bool DistributionApproved { get; set; }
    }
}
