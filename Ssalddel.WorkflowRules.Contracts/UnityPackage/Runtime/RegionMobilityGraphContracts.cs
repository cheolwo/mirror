using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class RegionMobilityGraphPolicy
    {
        public const string ManifestSchemaVersion = "region-mobility-graph-manifest.v1";
        public const string TileSchemaVersion = "region-mobility-graph-tile.v1";
        public const string PendingHumanReview = "PendingHumanReview";
        public const string SourceExplicitlyRestricted = "SourceExplicitlyRestricted";
        public const string SagajeongOneKilometerWindow = "SagajeongOneKilometerWindow";
        public const int TileSizeMeters = 500;
        public const string FirstGraphStableId = "mobility-graph:kr:seoul:jungnang:sagajeong.r1";
        public const string FirstRegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1";
        public const double FirstWorldOffsetX = 550d;
        public const double FirstWorldOffsetZ = 8d;
        public const double FirstMetersPerUnit = 1d;

        public static bool IsRegionStableId(string value)
            => !string.IsNullOrWhiteSpace(value)
                && value.StartsWith("world-region:", StringComparison.Ordinal);
    }

    public static class RegionMobilityGraphRoutes
    {
        public const string Manifest =
            "api/v1/world/regions/{regionStableId}/mobility-graph-manifest";
        public const string Tile =
            "api/v1/world/regions/{regionStableId}/mobility-graph-tiles/{tileStableId}";
    }

    public static class RegionMobilityGraphModeCodes
    {
        public const string Vehicle = "Vehicle";
        public const string Pedestrian = "Pedestrian";
        public const string Motorcycle = "Motorcycle";
    }

    public static class RegionMobilityGraphDirectionCodes
    {
        public const string Unknown = "Unknown";
        public const string Forward = "Forward";
        public const string Reverse = "Reverse";
        public const string Both = "Both";
    }

    public static class RegionMobilityGraphStitchKindCodes
    {
        public const string TileStitch = "TileStitch";
        public const string OpenBoundaryPortal = "OpenBoundaryPortal";
    }

    public sealed class RegionMobilityGraphPoint
    {
        public double X { get; set; }
        public double Z { get; set; }
    }

    public sealed class RegionMobilityGraphTag
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public sealed class RegionMobilityGraphProvenance
    {
        public string SourceId { get; set; } = string.Empty;
        public string DatasetId { get; set; } = string.Empty;
        public string SourceVersion { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string RawContentHashSha256 { get; set; } = string.Empty;
        public string LicenseCode { get; set; } = string.Empty;
        public string Attribution { get; set; } = string.Empty;
        public string CoordinateMethod { get; set; } = string.Empty;
        public string OriginOsmNodeId { get; set; } = string.Empty;
        public double OriginLatitude { get; set; }
        public double OriginLongitude { get; set; }
    }

    public sealed class RegionMobilityGraphCoordinates
    {
        public string Unit { get; set; } = "m";
        public string AxisOrder { get; set; } = "EastingNorthing";
        public double[] Bounds { get; set; } = Array.Empty<double>();
        public int TileSizeMeters { get; set; } = RegionMobilityGraphPolicy.TileSizeMeters;
        public double WorldOffsetX { get; set; }
        public double WorldOffsetZ { get; set; }
        public double MetersPerUnit { get; set; } = 1d;
    }

    public sealed class RegionMobilityGraphTileSummary
    {
        public string TileStableId { get; set; } = string.Empty;
        public int TileIndexX { get; set; }
        public int TileIndexZ { get; set; }
        public double[] Bounds { get; set; } = Array.Empty<double>();
        public string FileName { get; set; } = string.Empty;
        public string ContentHashSha256 { get; set; } = string.Empty;
        public string FileHashSha256 { get; set; } = string.Empty;
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public int PortalCount { get; set; }
    }

    public sealed class RegionMobilityGraphPortalReference
    {
        public string TileStableId { get; set; } = string.Empty;
        public string NodeStableId { get; set; } = string.Empty;
    }

    public sealed class RegionMobilityGraphStitch
    {
        public string StitchStableId { get; set; } = string.Empty;
        public string KindCode { get; set; } = string.Empty;
        public RegionMobilityGraphPoint Position { get; set; } = new RegionMobilityGraphPoint();
        public RegionMobilityGraphPortalReference[] Portals { get; set; } =
            Array.Empty<RegionMobilityGraphPortalReference>();
        public bool RuntimeAuthorized { get; set; }
    }

    public sealed class RegionMobilityConnectorCandidate
    {
        public string CandidateStableId { get; set; } = string.Empty;
        public string AnchorCode { get; set; } = string.Empty;
        public string SourceEntranceOsmNodeId { get; set; } = string.Empty;
        public RegionMobilityGraphPoint From { get; set; } = new RegionMobilityGraphPoint();
        public RegionMobilityGraphPoint To { get; set; } = new RegionMobilityGraphPoint();
        public double StraightDistanceMeters { get; set; }
        public string SourceRoadOsmWayId { get; set; } = string.Empty;
        public int SourceRoadSegmentIndex { get; set; }
        public string VisualRoadStableId { get; set; } = string.Empty;
        public string CandidateModeCode { get; set; } = string.Empty;
        public string ReviewStatusCode { get; set; } = RegionMobilityGraphPolicy.PendingHumanReview;
        public bool GeneratedAsGraphEdge { get; set; }
        public bool RuntimeAuthorized { get; set; }
    }

    public sealed class RegionMobilityVerticalSliceCandidate
    {
        public string SemanticPlaceStableId { get; set; } = string.Empty;
        public string SourceBuildingOsmWayId { get; set; } = string.Empty;
        public bool BuildingFound { get; set; }
        public string EntranceEvidenceCode { get; set; } = string.Empty;
        public string[] TaggedEntranceOsmNodeIds { get; set; } = Array.Empty<string>();
        public RegionMobilityConnectorCandidate[] ConnectorCandidates { get; set; } =
            Array.Empty<RegionMobilityConnectorCandidate>();
        public string BindingStatusCode { get; set; } = string.Empty;
        public bool RuntimeAuthorized { get; set; }
    }

    public sealed class RegionMobilityGraphQuality
    {
        public string AccessReviewCode { get; set; } = RegionMobilityGraphPolicy.PendingHumanReview;
        public string ConnectorPolicyCode { get; set; } = string.Empty;
        public string DirectionPolicyCode { get; set; } = string.Empty;
        public string ParkingAislePolicyCode { get; set; } = string.Empty;
        public string AdministrativeCoveragePolicyCode { get; set; } = string.Empty;
        public int OmittedMissingNodeSegmentCount { get; set; }
        public int ClippedSourceSegmentCount { get; set; }
        public int DetachedEntranceCount { get; set; }
        public int CrossingCount { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
    }

    public sealed class RegionMobilityGraphManifest
    {
        public string SchemaVersion { get; set; } = RegionMobilityGraphPolicy.ManifestSchemaVersion;
        public string GraphStableId { get; set; } = string.Empty;
        public string RegionStableId { get; set; } = string.Empty;
        public string[] AdministrativeAreaStableIds { get; set; } = Array.Empty<string>();
        public string Revision { get; set; } = string.Empty;
        public string CoverageCode { get; set; } = string.Empty;
        public bool AdministrativeBoundaryClipped { get; set; }
        public string ReadinessCode { get; set; } = RegionMobilityGraphPolicy.PendingHumanReview;
        public string ProjectionHashSha256 { get; set; } = string.Empty;
        public RegionMobilityGraphProvenance Provenance { get; set; } = new RegionMobilityGraphProvenance();
        public RegionMobilityGraphCoordinates Coordinates { get; set; } = new RegionMobilityGraphCoordinates();
        public RegionMobilityGraphTileSummary[] Tiles { get; set; } = Array.Empty<RegionMobilityGraphTileSummary>();
        public RegionMobilityGraphStitch[] Stitches { get; set; } = Array.Empty<RegionMobilityGraphStitch>();
        public RegionMobilityVerticalSliceCandidate[] VerticalSliceCandidates { get; set; } =
            Array.Empty<RegionMobilityVerticalSliceCandidate>();
        public RegionMobilityGraphQuality Quality { get; set; } = new RegionMobilityGraphQuality();
        public bool DistributionApproved { get; set; }
        public bool TraversalReady { get; set; }
        public bool RuntimeAuthorized { get; set; }
        public string ContentHashSha256 { get; set; } = string.Empty;
    }

    public sealed class RegionMobilityGraphNode
    {
        public string NodeStableId { get; set; } = string.Empty;
        public string SourceOsmNodeId { get; set; } = string.Empty;
        public RegionMobilityGraphPoint Position { get; set; } = new RegionMobilityGraphPoint();
        public string[] RoleCodes { get; set; } = Array.Empty<string>();
        public RegionMobilityGraphTag[] SourceTags { get; set; } = Array.Empty<RegionMobilityGraphTag>();
        public string[] SourceBuildingOsmWayIds { get; set; } = Array.Empty<string>();
        public bool IsPortal { get; set; }
        public string StitchStableId { get; set; } = string.Empty;
        public string ConnectionStatusCode { get; set; } = string.Empty;
        public bool RuntimeAuthorized { get; set; }
    }

    public sealed class RegionMobilityGraphEdge
    {
        public string EdgeStableId { get; set; } = string.Empty;
        public string EdgeKindCode { get; set; } = string.Empty;
        public string SourceOsmWayId { get; set; } = string.Empty;
        public int SourceSegmentIndex { get; set; }
        public string[] SourceGeometryNodeIds { get; set; } = Array.Empty<string>();
        public string FromNodeStableId { get; set; } = string.Empty;
        public string ToNodeStableId { get; set; } = string.Empty;
        public RegionMobilityGraphPoint[] Geometry { get; set; } = Array.Empty<RegionMobilityGraphPoint>();
        public double LengthMeters { get; set; }
        public string DirectionCode { get; set; } = RegionMobilityGraphDirectionCodes.Unknown;
        public string[] CandidateModeCodes { get; set; } = Array.Empty<string>();
        public string AccessReviewCode { get; set; } = RegionMobilityGraphPolicy.PendingHumanReview;
        public string SourceOneway { get; set; } = string.Empty;
        public string SourceAccess { get; set; } = string.Empty;
        public string SourceVehicle { get; set; } = string.Empty;
        public string SourceMotorVehicle { get; set; } = string.Empty;
        public string SourceMotorcycle { get; set; } = string.Empty;
        public string SourceFoot { get; set; } = string.Empty;
        public RegionMobilityGraphTag[] SourceTags { get; set; } = Array.Empty<RegionMobilityGraphTag>();
        public string VisualRoadStableId { get; set; } = string.Empty;
        public bool ThroughRouteCandidate { get; set; }
        public bool BoundaryClipped { get; set; }
        public bool RuntimeAuthorized { get; set; }
    }

    public sealed class RegionMobilityGraphTile
    {
        public string SchemaVersion { get; set; } = RegionMobilityGraphPolicy.TileSchemaVersion;
        public string GraphStableId { get; set; } = string.Empty;
        public string RegionStableId { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string CoverageCode { get; set; } = string.Empty;
        public bool AdministrativeBoundaryClipped { get; set; }
        public string TileStableId { get; set; } = string.Empty;
        public int TileIndexX { get; set; }
        public int TileIndexZ { get; set; }
        public double[] Bounds { get; set; } = Array.Empty<double>();
        public string SourceRawContentHashSha256 { get; set; } = string.Empty;
        public RegionMobilityGraphNode[] Nodes { get; set; } = Array.Empty<RegionMobilityGraphNode>();
        public RegionMobilityGraphEdge[] Edges { get; set; } = Array.Empty<RegionMobilityGraphEdge>();
        public bool DistributionApproved { get; set; }
        public bool TraversalReady { get; set; }
        public bool RuntimeAuthorized { get; set; }
        public string ContentHashSha256 { get; set; } = string.Empty;
    }
}
