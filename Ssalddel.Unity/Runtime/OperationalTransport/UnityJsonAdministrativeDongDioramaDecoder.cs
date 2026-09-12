#if UNITY_5_3_OR_NEWER
using System;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.OperationalTransport
{
    public sealed class UnityJsonAdministrativeDongDioramaDecoder : IAdministrativeDongDioramaDecoder
    {
        public AdministrativeDongDioramaManifest DecodeManifest(string json)
            => Parse<ManifestWire>(json, "AdministrativeDongDioramaManifestJsonInvalid").ToContract();

        public AdministrativeDongDioramaTile DecodeTile(string json)
            => Parse<TileWire>(json, "AdministrativeDongDioramaTileJsonInvalid").ToContract();

        public AdministrativeDongDisplayOverlayResponse DecodeDisplayOverlays(string json)
            => Parse<OverlayResponseWire>(json, "AdministrativeDongDioramaOverlayJsonInvalid").ToContract();

        private static T Parse<T>(string json, string error) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException(error, nameof(json));
            return JsonUtility.FromJson<T>(json) ?? throw new FormatException(error);
        }
    }

    [Serializable]
    internal sealed class PointWire
    {
        public double x;
        public double z;
        public AdministrativeDongDioramaPoint ToContract() => new AdministrativeDongDioramaPoint { X = x, Z = z };
    }

    [Serializable]
    internal sealed class BoundsWire
    {
        public double minX;
        public double minZ;
        public double maxX;
        public double maxZ;
        public AdministrativeDongDioramaBounds ToContract() => new AdministrativeDongDioramaBounds
            { MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ };
    }

    [Serializable]
    internal sealed class FrameWire
    {
        public string method = string.Empty;
        public double originLatitude;
        public double originLongitude;
        public double worldOffsetX;
        public double worldOffsetZ;
        public double metersPerUnit = 1d;
        public AdministrativeDongDioramaCoordinateFrame ToContract() => new AdministrativeDongDioramaCoordinateFrame
        {
            Method = method,
            OriginLatitude = originLatitude,
            OriginLongitude = originLongitude,
            WorldOffsetX = worldOffsetX,
            WorldOffsetZ = worldOffsetZ,
            MetersPerUnit = metersPerUnit
        };
    }

    [Serializable]
    internal sealed class SourceWire
    {
        public string sourceId = string.Empty;
        public string datasetId = string.Empty;
        public string sourceRevision = string.Empty;
        public string contentHashSha256 = string.Empty;
        public string licenseCode = string.Empty;
        public string limitationCode = string.Empty;
        public AdministrativeDongDioramaSourceAttribution ToContract() => new AdministrativeDongDioramaSourceAttribution
        {
            SourceId = sourceId, DatasetId = datasetId, SourceRevision = sourceRevision,
            ContentHashSha256 = contentHashSha256, LicenseCode = licenseCode, LimitationCode = limitationCode
        };
    }

    [Serializable]
    internal sealed class TileSummaryWire
    {
        public string tileStableId = string.Empty;
        public int tileIndexX;
        public int tileIndexZ;
        public string tileHashSha256 = string.Empty;
        public int buildingCount;
        public int roadSegmentCount;
        public AdministrativeDongDioramaTileSummary ToContract() => new AdministrativeDongDioramaTileSummary
        {
            TileStableId = tileStableId, TileIndexX = tileIndexX, TileIndexZ = tileIndexZ,
            TileHashSha256 = tileHashSha256, BuildingCount = buildingCount, RoadSegmentCount = roadSegmentCount
        };
    }

    [Serializable]
    internal sealed class BuildingSummaryWire
    {
        public string categoryCode = string.Empty;
        public int buildingCount;
        public double buildingAreaSquareMeters;
        public double totalFloorAreaSquareMeters;
        public AdministrativeDongDioramaBuildingSummary ToContract() => new AdministrativeDongDioramaBuildingSummary
        {
            CategoryCode = categoryCode, BuildingCount = buildingCount,
            BuildingAreaSquareMeters = buildingAreaSquareMeters,
            TotalFloorAreaSquareMeters = totalFloorAreaSquareMeters
        };
    }

    [Serializable]
    internal sealed class ManifestWire
    {
        public string schemaVersion = string.Empty;
        public string administrativeAreaStableId = string.Empty;
        public string displayName = string.Empty;
        public string sourceVintage = string.Empty;
        public string projectionHashSha256 = string.Empty;
        public string readinessCode = string.Empty;
        public string dataPolicyCode = string.Empty;
        public bool observationPresentationOnly;
        public bool traversalReady;
        public bool gameplayReady;
        public bool distributionApproved;
        public string generatedAtUtc = string.Empty;
        public string[] legalAreaStableIds = Array.Empty<string>();
        public string[] operationalAreaStableIds = Array.Empty<string>();
        public string[] semanticPlaceStableIds = Array.Empty<string>();
        public FrameWire coordinateFrame = new FrameWire();
        public BoundsWire bounds = new BoundsWire();
        public PointWire[] boundary = Array.Empty<PointWire>();
        public TileSummaryWire[] tiles = Array.Empty<TileSummaryWire>();
        public BuildingSummaryWire[] buildingSummaries = Array.Empty<BuildingSummaryWire>();
        public int unresolvedBuildingCount;
        public int publicBusinessMarkerCount;
        public SourceWire[] sources = Array.Empty<SourceWire>();

        public AdministrativeDongDioramaManifest ToContract() => new AdministrativeDongDioramaManifest
        {
            SchemaVersion = schemaVersion, AdministrativeAreaStableId = administrativeAreaStableId,
            DisplayName = displayName, SourceVintage = sourceVintage, ProjectionHashSha256 = projectionHashSha256,
            ReadinessCode = readinessCode, DataPolicyCode = dataPolicyCode,
            ObservationPresentationOnly = observationPresentationOnly, TraversalReady = traversalReady,
            GameplayReady = gameplayReady, DistributionApproved = distributionApproved,
            GeneratedAtUtc = WireTime.Parse(generatedAtUtc),
            LegalAreaStableIds = legalAreaStableIds ?? Array.Empty<string>(),
            OperationalAreaStableIds = operationalAreaStableIds ?? Array.Empty<string>(),
            SemanticPlaceStableIds = semanticPlaceStableIds ?? Array.Empty<string>(),
            CoordinateFrame = coordinateFrame.ToContract(), Bounds = bounds.ToContract(),
            Boundary = Array.ConvertAll(boundary ?? Array.Empty<PointWire>(), value => value.ToContract()),
            Tiles = Array.ConvertAll(tiles ?? Array.Empty<TileSummaryWire>(), value => value.ToContract()),
            BuildingSummaries = Array.ConvertAll(buildingSummaries ?? Array.Empty<BuildingSummaryWire>(), value => value.ToContract()),
            UnresolvedBuildingCount = unresolvedBuildingCount, PublicBusinessMarkerCount = publicBusinessMarkerCount,
            Sources = Array.ConvertAll(sources ?? Array.Empty<SourceWire>(), value => value.ToContract())
        };
    }

    [Serializable]
    internal sealed class BuildingWire
    {
        public string buildingStableId = string.Empty;
        public string categoryCode = string.Empty;
        public string evidenceKindCode = string.Empty;
        public int aboveGroundFloorCount;
        public double heightMeters;
        public PointWire[] footprint = Array.Empty<PointWire>();
        public AdministrativeDongDioramaBuilding ToContract() => new AdministrativeDongDioramaBuilding
        {
            BuildingStableId = buildingStableId, CategoryCode = categoryCode, EvidenceKindCode = evidenceKindCode,
            AboveGroundFloorCount = aboveGroundFloorCount > 0 ? aboveGroundFloorCount : (int?)null,
            HeightMeters = heightMeters > 0d ? heightMeters : (double?)null,
            Footprint = Array.ConvertAll(footprint ?? Array.Empty<PointWire>(), value => value.ToContract())
        };
    }

    [Serializable]
    internal sealed class RoadWire
    {
        public string roadStableId = string.Empty;
        public PointWire from = new PointWire();
        public PointWire to = new PointWire();
        public string evidenceKindCode = string.Empty;
        public AdministrativeDongDioramaRoadSegment ToContract() => new AdministrativeDongDioramaRoadSegment
            { RoadStableId = roadStableId, From = from.ToContract(), To = to.ToContract(), EvidenceKindCode = evidenceKindCode };
    }

    [Serializable]
    internal sealed class TileWire
    {
        public string schemaVersion = string.Empty;
        public string administrativeAreaStableId = string.Empty;
        public string projectionHashSha256 = string.Empty;
        public string tileStableId = string.Empty;
        public string tileHashSha256 = string.Empty;
        public int tileIndexX;
        public int tileIndexZ;
        public BoundsWire bounds = new BoundsWire();
        public BuildingWire[] buildings = Array.Empty<BuildingWire>();
        public RoadWire[] roads = Array.Empty<RoadWire>();
        public AdministrativeDongDioramaTile ToContract() => new AdministrativeDongDioramaTile
        {
            SchemaVersion = schemaVersion, AdministrativeAreaStableId = administrativeAreaStableId,
            ProjectionHashSha256 = projectionHashSha256, TileStableId = tileStableId, TileHashSha256 = tileHashSha256,
            TileIndexX = tileIndexX, TileIndexZ = tileIndexZ, Bounds = bounds.ToContract(),
            Buildings = Array.ConvertAll(buildings ?? Array.Empty<BuildingWire>(), value => value.ToContract()),
            Roads = Array.ConvertAll(roads ?? Array.Empty<RoadWire>(), value => value.ToContract())
        };
    }

    [Serializable]
    internal sealed class OverlayWire
    {
        public string overlayStableId = string.Empty;
        public string overlayKindCode = string.Empty;
        public string displayLabel = string.Empty;
        public string categoryCode = string.Empty;
        public string semanticPlaceStableId = string.Empty;
        public string badgeText = string.Empty;
        public string detailCardText = string.Empty;
        public bool advertisementDisclosureRequired;
        public string startsAtUtc = string.Empty;
        public string endsAtUtc = string.Empty;
        public AdministrativeDongDisplayOverlay ToContract() => new AdministrativeDongDisplayOverlay
        {
            OverlayStableId = overlayStableId, OverlayKindCode = overlayKindCode, DisplayLabel = displayLabel,
            CategoryCode = categoryCode, SemanticPlaceStableId = semanticPlaceStableId, BadgeText = badgeText,
            DetailCardText = detailCardText, AdvertisementDisclosureRequired = advertisementDisclosureRequired,
            StartsAtUtc = WireTime.ParseNullable(startsAtUtc), EndsAtUtc = WireTime.ParseNullable(endsAtUtc)
        };
    }

    [Serializable]
    internal sealed class OverlayResponseWire
    {
        public string schemaVersion = string.Empty;
        public string administrativeAreaStableId = string.Empty;
        public string overlayRevision = string.Empty;
        public string asOfUtc = string.Empty;
        public OverlayWire[] items = Array.Empty<OverlayWire>();
        public AdministrativeDongDisplayOverlayResponse ToContract() => new AdministrativeDongDisplayOverlayResponse
        {
            SchemaVersion = schemaVersion, AdministrativeAreaStableId = administrativeAreaStableId,
            OverlayRevision = overlayRevision, AsOfUtc = WireTime.Parse(asOfUtc),
            Items = Array.ConvertAll(items ?? Array.Empty<OverlayWire>(), value => value.ToContract())
        };
    }

    internal static class WireTime
    {
        public static DateTime Parse(string value)
            => DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed.ToUniversalTime() : default;
        public static DateTime? ParseNullable(string value)
            => string.IsNullOrWhiteSpace(value) ? (DateTime?)null : Parse(value);
    }
}
#endif
