using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class AdministrativeDongDioramaPolicy
    {
        public const string SchemaVersion = "administrative-dong-diorama.v1";
        public const string DisplayOverlaySchemaVersion = "administrative-dong-display-overlays.v1";
        public const string ObservationPresentationOnly = "ObservationPresentationOnly";
        public const int TileSizeMeters = 500;
        public const string FirstAdministrativeAreaStableId = "region:kr:hjd:1126057500";

        public static bool IsAdministrativeAreaStableId(string value)
        {
            const string prefix = "region:kr:hjd:";
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            var code = value.Substring(prefix.Length);
            if (code.Length != 10) return false;
            for (var index = 0; index < code.Length; index++)
                if (code[index] < '0' || code[index] > '9') return false;
            return true;
        }
    }

    public static class AdministrativeDongDioramaRoutes
    {
        public const string Manifest =
            "api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-manifest";
        public const string Tile =
            "api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-tiles/{tileStableId}";
        public const string DisplayOverlays =
            "api/v1/world/administrative-areas/{administrativeAreaStableId}/display-overlays";
    }

    public static class AdministrativeDongDioramaReadinessCodes
    {
        public const string Ready = "Ready";
        public const string WaitingForAdministrativeBoundary = "WaitingForAdministrativeBoundary";
        public const string WaitingForSpatialLinkage = "WaitingForSpatialLinkage";
        public const string PrivateReviewOnly = "PrivateReviewOnly";
    }

    public static class AdministrativeDongDisplayOverlayKinds
    {
        public const string PublicBusiness = "PublicBusiness";
        public const string Sponsorship = "Sponsorship";
        public const string UnresolvedOperationalCount = "UnresolvedOperationalCount";
    }

    public sealed class AdministrativeDongDioramaPoint
    {
        public double X { get; set; }
        public double Z { get; set; }
    }

    public sealed class AdministrativeDongDioramaBounds
    {
        public double MinX { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxZ { get; set; }
    }

    public sealed class AdministrativeDongDioramaCoordinateFrame
    {
        public string Method { get; set; } = string.Empty;
        public double OriginLatitude { get; set; }
        public double OriginLongitude { get; set; }
        public double WorldOffsetX { get; set; }
        public double WorldOffsetZ { get; set; }
        public double MetersPerUnit { get; set; } = 1d;
    }

    public sealed class AdministrativeDongDioramaSourceAttribution
    {
        public string SourceId { get; set; } = string.Empty;
        public string DatasetId { get; set; } = string.Empty;
        public string SourceRevision { get; set; } = string.Empty;
        public string ContentHashSha256 { get; set; } = string.Empty;
        public string LicenseCode { get; set; } = string.Empty;
        public string LimitationCode { get; set; } = string.Empty;
    }

    public sealed class AdministrativeDongDioramaTileSummary
    {
        public string TileStableId { get; set; } = string.Empty;
        public int TileIndexX { get; set; }
        public int TileIndexZ { get; set; }
        public string TileHashSha256 { get; set; } = string.Empty;
        public int BuildingCount { get; set; }
        public int RoadSegmentCount { get; set; }
    }

    public sealed class AdministrativeDongDioramaBuildingSummary
    {
        public string CategoryCode { get; set; } = string.Empty;
        public int BuildingCount { get; set; }
        public double BuildingAreaSquareMeters { get; set; }
        public double TotalFloorAreaSquareMeters { get; set; }
    }

    public sealed class AdministrativeDongDioramaManifest
    {
        public string SchemaVersion { get; set; } = AdministrativeDongDioramaPolicy.SchemaVersion;
        public string AdministrativeAreaStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SourceVintage { get; set; } = string.Empty;
        public string ProjectionHashSha256 { get; set; } = string.Empty;
        public string ReadinessCode { get; set; } = AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary;
        public string DataPolicyCode { get; set; } = AdministrativeDongDioramaPolicy.ObservationPresentationOnly;
        public bool ObservationPresentationOnly { get; set; } = true;
        public bool TraversalReady { get; set; }
        public bool GameplayReady { get; set; }
        public bool DistributionApproved { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
        public string[] LegalAreaStableIds { get; set; } = Array.Empty<string>();
        public string[] OperationalAreaStableIds { get; set; } = Array.Empty<string>();
        public string[] SemanticPlaceStableIds { get; set; } = Array.Empty<string>();
        public AdministrativeDongDioramaCoordinateFrame CoordinateFrame { get; set; } =
            new AdministrativeDongDioramaCoordinateFrame();
        public AdministrativeDongDioramaBounds Bounds { get; set; } =
            new AdministrativeDongDioramaBounds();
        public AdministrativeDongDioramaPoint[] Boundary { get; set; } =
            Array.Empty<AdministrativeDongDioramaPoint>();
        public AdministrativeDongDioramaTileSummary[] Tiles { get; set; } =
            Array.Empty<AdministrativeDongDioramaTileSummary>();
        public AdministrativeDongDioramaBuildingSummary[] BuildingSummaries { get; set; } =
            Array.Empty<AdministrativeDongDioramaBuildingSummary>();
        public int UnresolvedBuildingCount { get; set; }
        public int PublicBusinessMarkerCount { get; set; }
        public AdministrativeDongDioramaSourceAttribution[] Sources { get; set; } =
            Array.Empty<AdministrativeDongDioramaSourceAttribution>();
    }

    public sealed class AdministrativeDongDioramaBuilding
    {
        public string BuildingStableId { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string EvidenceKindCode { get; set; } = string.Empty;
        public int? AboveGroundFloorCount { get; set; }
        public double? HeightMeters { get; set; }
        public AdministrativeDongDioramaPoint[] Footprint { get; set; } =
            Array.Empty<AdministrativeDongDioramaPoint>();
    }

    public sealed class AdministrativeDongDioramaRoadSegment
    {
        public string RoadStableId { get; set; } = string.Empty;
        public AdministrativeDongDioramaPoint From { get; set; } = new AdministrativeDongDioramaPoint();
        public AdministrativeDongDioramaPoint To { get; set; } = new AdministrativeDongDioramaPoint();
        public string EvidenceKindCode { get; set; } = string.Empty;
    }

    public sealed class AdministrativeDongDioramaTile
    {
        public string SchemaVersion { get; set; } = AdministrativeDongDioramaPolicy.SchemaVersion;
        public string AdministrativeAreaStableId { get; set; } = string.Empty;
        public string ProjectionHashSha256 { get; set; } = string.Empty;
        public string TileStableId { get; set; } = string.Empty;
        public string TileHashSha256 { get; set; } = string.Empty;
        public int TileIndexX { get; set; }
        public int TileIndexZ { get; set; }
        public AdministrativeDongDioramaBounds Bounds { get; set; } =
            new AdministrativeDongDioramaBounds();
        public AdministrativeDongDioramaBuilding[] Buildings { get; set; } =
            Array.Empty<AdministrativeDongDioramaBuilding>();
        public AdministrativeDongDioramaRoadSegment[] Roads { get; set; } =
            Array.Empty<AdministrativeDongDioramaRoadSegment>();
    }

    public sealed class AdministrativeDongDisplayOverlay
    {
        public string OverlayStableId { get; set; } = string.Empty;
        public string OverlayKindCode { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string SemanticPlaceStableId { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string DetailCardText { get; set; } = string.Empty;
        public bool AdvertisementDisclosureRequired { get; set; }
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }
    }

    public sealed class AdministrativeDongDisplayOverlayResponse
    {
        public string SchemaVersion { get; set; } = AdministrativeDongDioramaPolicy.DisplayOverlaySchemaVersion;
        public string AdministrativeAreaStableId { get; set; } = string.Empty;
        public string OverlayRevision { get; set; } = string.Empty;
        public DateTime AsOfUtc { get; set; }
        public AdministrativeDongDisplayOverlay[] Items { get; set; } =
            Array.Empty<AdministrativeDongDisplayOverlay>();
    }
}
