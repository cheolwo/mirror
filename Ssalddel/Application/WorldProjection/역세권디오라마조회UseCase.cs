using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Application.WorldProjection;

public interface I역세권디오라마조회UseCase
{
    Task<StationDioramaCatalogResponse> CatalogAsync(CancellationToken cancellationToken);
    Task<StationDioramaManifest?> ManifestAsync(string transitStationStableId, CancellationToken cancellationToken);
}

public interface I역세권디오라마Catalog
{
    IReadOnlyList<역세권디오라마Definition> All { get; }
}

public sealed record 역세권디오라마공간사본Definition(
    string TransitStationStableId,
    string SchemaVersion,
    string Revision,
    string ContentHashSha256,
    string PayloadHashSha256,
    long PayloadByteLength,
    int BuildingCount,
    int RoadCount,
    int SurfaceCount,
    int AdministrativeAreaCount,
    int CoverageCellCount,
    int MissingCoverageCellCount,
    IReadOnlyList<string> MissingCoverageCodes);

public sealed record 역세권디오라마Definition(
    string TransitStationStableId,
    string OfficialStationName,
    string DisplayName,
    string NameConfirmationStatusCode,
    IReadOnlyList<string> LegacyAliases,
    string OperatorName,
    string LineCode,
    string StationCode,
    double Latitude,
    double Longitude,
    string StationTypeCode,
    int WindowWidthMeters,
    int WindowDepthMeters,
    string ManifestRevision,
    string RegionStableId,
    IReadOnlyList<string> AdministrativeAreaStableIds,
    bool SpatialCoverageCompleteApproved,
    역세권디오라마공간사본Definition? SpatialSnapshot);

/// <summary>
/// KRIC 원천 행을 역 고유 식별자와 표준 1km 창에 결속합니다.
/// 좌표는 원천 제공 WGS84 지점이며 플랫폼 현장측량값으로 승격하지 않습니다.
/// </summary>
public sealed class 역세권디오라마Catalog : I역세권디오라마Catalog
{
    internal const string SpatialSnapshotSchemaVersion = "ssalddel.station-spatial-snapshot.v1";
    public const string SourceId = "kric-urban-rail-stations";
    public const string DatasetId = "data-go-kr-15093755";
    public const string OfficialSourcePageUrl = "https://www.data.go.kr/data/15093755/fileData.do";
    public const string ProviderSourcePageUrl = "https://data.kric.go.kr/rips/M_01_01/detail.do?id=32";
    public const string DataRevision = "jungnang-line7-station-reference.r1";
    public const string TargetRowReferenceDate = "2024-12-31";
    public const string RawContentHashSha256 =
        "cdf1d84a7e5c898b2aacd622783ba8ba9af35c40bee0561dc97d55ce8e063f94";
    public const string SourceRevision =
        "file:20260630;target-row-reference:2024-12-31;sha256:" + RawContentHashSha256;
    public const string LicenseObserved = "이용허락범위 제한 없음 (공공데이터포털 dataset metadata)";
    public const string ReviewStatusCode = "PendingHumanReview";
    public const string LimitationCode =
        "PrivateReviewOnly;NoPublicDistribution;NoOperationalAuthority;" +
        "DatasetLicenseObserved;CurrentFileVersionAlignmentPending;" +
        "SourceReportedPointNotPlatformSurvey;NoAreaCoverageInference";
    public static readonly DateTime CollectedAtUtc =
        new(2026, 9, 13, 2, 2, 58, 672, DateTimeKind.Utc);

    public IReadOnlyList<역세권디오라마Definition> All { get; } =
    [
        new(
            StationDioramaPolicy.MyeonmokTransitStationStableId,
            "면목",
            "면목역",
            StationDioramaNameConfirmationStatusCodes.SourceNameConfirmed,
            [],
            "서울교통공사",
            "S1107",
            "0721",
            37.588671d,
            127.087503d,
            StationDioramaStationTypeCodes.Normal,
            StationDioramaPolicy.StandardWindowMeters,
            StationDioramaPolicy.StandardWindowMeters,
            "station-diorama:kric:20260630:0721.r2",
            RegionExperiencePackagePolicy.MyeonmokStationRegionStableId,
            [
                "region:kr:hjd:1126052000",
                "region:kr:hjd:1126055000",
                "region:kr:hjd:1126056500",
                "region:kr:hjd:1126057500",
                "region:kr:hjd:1126059000",
                "region:kr:hjd:1126066000"
            ],
            false,
            MyeonmokSpatialSnapshot()),
        new(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            "사가정",
            "사가정역",
            StationDioramaNameConfirmationStatusCodes.SourceNameConfirmed,
            [],
            "서울교통공사",
            "S1107",
            "0722",
            37.580912d,
            127.088502d,
            StationDioramaStationTypeCodes.Normal,
            StationDioramaPolicy.StandardWindowMeters,
            StationDioramaPolicy.StandardWindowMeters,
            "station-diorama:kric:20260630:0722.r1",
            RegionExperiencePackagePolicy.SagajeongRegionStableId,
            [AdministrativeDongDioramaPolicy.FirstAdministrativeAreaStableId],
            false,
            null),
        new(
            StationDioramaPolicy.YongmasanTransitStationStableId,
            "용마산(용마폭포공원)",
            "용마산역",
            StationDioramaNameConfirmationStatusCodes.UserConfirmedDisplayName,
            [],
            "서울교통공사",
            "S1107",
            "0723",
            37.573752d,
            127.086802d,
            StationDioramaStationTypeCodes.Normal,
            StationDioramaPolicy.StandardWindowMeters,
            StationDioramaPolicy.StandardWindowMeters,
            "station-diorama:kric:20260630:0723.r3",
            string.Empty,
            [],
            false,
            YongmasanSpatialSnapshot())
    ];

    private static 역세권디오라마공간사본Definition MyeonmokSpatialSnapshot()
        => new(
            StationDioramaPolicy.MyeonmokTransitStationStableId,
            SpatialSnapshotSchemaVersion,
            "myeonmok-station-spatial-snapshot.private-review.r1",
            "48FED5BFA9B06075B27D7DCF22EFDF3AE3F98222D337D0298C01C098A62B913A",
            "4B8D45AE3414218228FEB1436AE8E99AB3999C83A30607292AC594C0099530B1",
            10_111_746,
            4_165,
            124,
            48,
            6,
            100,
            2,
            [
                "BuildingRightsConflictUnresolved",
                "LegalDongBoundaryCoverageUnverified",
                "RoadWidthUnavailable",
                "SurfaceCoverageIncomplete",
                "TraversalAuthorityUnavailable"
            ]);

    private static 역세권디오라마공간사본Definition YongmasanSpatialSnapshot()
        => new(
            StationDioramaPolicy.YongmasanTransitStationStableId,
            SpatialSnapshotSchemaVersion,
            "yongmasan-station-spatial-snapshot.private-review.r1",
            "43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D",
            "6AC2E8595B913B6AE80D6426476F334225491E89555CEB9A5C1A17BD568120B6",
            7_069_044,
            2_859,
            116,
            41,
            4,
            100,
            10,
            [
                "BuildingRightsConflictUnresolved",
                "LegalDongBoundaryCoverageUnverified",
                "OsmRelationMembersMissing",
                "RoadWidthUnavailable",
                "SurfaceCoverageIncomplete",
                "TraversalAuthorityUnavailable"
            ]);
}

/// <summary>
/// 역 정보는 정적 공식 원천에서, 공간 범위는 기존 지역·행정동 읽기 투영에서 조립합니다.
/// 저장소에 없는 도형을 보완하거나 준비 완료로 추정하지 않습니다.
/// </summary>
public sealed class 역세권디오라마조회UseCase(
    I역세권디오라마Catalog catalog,
    I지역ExperiencePackage조회UseCase regionPackages,
    I행정동디오라마조회UseCase administrativeDongDioramas) : I역세권디오라마조회UseCase
{
    private const string SupportedCoordinateMethod = "WGS84-ECEF-ENU-at-zero-altitude";
    private const double CoordinateTolerance = 0.000001d;

    public async Task<StationDioramaCatalogResponse> CatalogAsync(CancellationToken cancellationToken)
    {
        ValidateDefinitions(catalog.All);
        var manifests = new List<StationDioramaManifest>();
        foreach (var definition in catalog.All.OrderBy(item => item.TransitStationStableId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            manifests.Add(await BuildManifestAsync(definition, cancellationToken));
        }
        var items = manifests.Select(ToSummary).ToArray();
        return new StationDioramaCatalogResponse
        {
            CatalogRevision = Hash(string.Join("\n", items.Select(item =>
                item.TransitStationStableId + "|" + item.ManifestHashSha256))),
            Items = items
        };
    }

    public async Task<StationDioramaManifest?> ManifestAsync(
        string transitStationStableId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateDefinitions(catalog.All);
        if (!StationDioramaPolicy.IsTransitStationStableId(transitStationStableId))
            throw new ArgumentException("TransitStationDioramaStableIdInvalid", nameof(transitStationStableId));
        var normalized = transitStationStableId.Trim();
        var definition = catalog.All.SingleOrDefault(item => string.Equals(
            item.TransitStationStableId,
            normalized,
            StringComparison.Ordinal));
        return definition is null
            ? null
            : await BuildManifestAsync(definition, cancellationToken);
    }

    private async Task<StationDioramaManifest> BuildManifestAsync(
        역세권디오라마Definition definition,
        CancellationToken cancellationToken)
    {
        var manifest = new StationDioramaManifest
        {
            TransitStationStableId = definition.TransitStationStableId,
            OfficialStationName = definition.OfficialStationName,
            DisplayName = definition.DisplayName,
            NameConfirmationStatusCode = definition.NameConfirmationStatusCode,
            LegacyAliases = Sorted(definition.LegacyAliases),
            Service = new StationDioramaServiceIdentifier
            {
                OperatorName = definition.OperatorName,
                LineCode = definition.LineCode,
                StationCode = definition.StationCode
            },
            StationTypeCode = definition.StationTypeCode,
            Anchor = new StationDioramaAnchor
            {
                Latitude = definition.Latitude,
                Longitude = definition.Longitude,
                QualityCode = StationDioramaQualityCodes.SourceReportedWgs84Point,
                QualityNote = "원천 제공 WGS84 지점이며 플랫폼 현장측량값이 아님"
            },
            Window = new StationDioramaWindow
            {
                PolicyCode = string.Equals(
                    definition.StationTypeCode,
                    StationDioramaStationTypeCodes.MajorInterchange,
                    StringComparison.Ordinal)
                    ? StationDioramaWindowPolicyCodes.MajorInterchangeVariable
                    : StationDioramaWindowPolicyCodes.FixedStationCentered,
                WidthMeters = definition.WindowWidthMeters,
                DepthMeters = definition.WindowDepthMeters,
                MajorInterchangeOverrideApplied = string.Equals(
                    definition.StationTypeCode,
                    StationDioramaStationTypeCodes.MajorInterchange,
                    StringComparison.Ordinal)
            },
            ManifestRevision = definition.ManifestRevision,
            RegionStableId = definition.RegionStableId,
            AdministrativeAreaStableIds = Sorted(definition.AdministrativeAreaStableIds),
            StationSources = [StationSource(definition)],
            SpatialSnapshotCandidate = SpatialSnapshotCandidate(definition.SpatialSnapshot),
            ObservationPresentationOnly = true,
            TraversalReady = false,
            GameplayReady = false,
            DistributionApproved = false
        };

        if (!string.IsNullOrWhiteSpace(definition.RegionStableId))
            await ConnectExistingSpatialCoverageAsync(manifest, definition, cancellationToken);

        manifest.AvailabilityCode = manifest.AreaProjections.Length == 0
            ? StationDioramaAvailabilityCodes.WaitingForSpatialCoverage
            : definition.SpatialCoverageCompleteApproved
                ? StationDioramaAvailabilityCodes.Ready
                : StationDioramaAvailabilityCodes.PartialCoverage;
        manifest.QualityCodes = Sorted(
        [
            StationDioramaQualityCodes.OfficialSourcePendingHumanReview,
            StationDioramaQualityCodes.SourceReportedWgs84Point,
            StationDioramaQualityCodes.NotPlatformFieldSurvey,
            manifest.AreaProjections.Length == 0
                ? StationDioramaQualityCodes.SpatialCoverageNotPublished
                : StationDioramaQualityCodes.KnownPartialSpatialCoverage
        ]);
        manifest.ManifestHashSha256 = ComputeManifestHash(manifest);
        return manifest;
    }

    private async Task ConnectExistingSpatialCoverageAsync(
        StationDioramaManifest station,
        역세권디오라마Definition definition,
        CancellationToken cancellationToken)
    {
        var region = await regionPackages.ManifestAsync(definition.RegionStableId, cancellationToken);
        if (region is null
            || !string.Equals(region.SchemaVersion, RegionExperiencePackagePolicy.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(region.RegionStableId, definition.RegionStableId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(region.ManifestHashSha256))
            return;

        station.RegionManifestHashSha256 = region.ManifestHashSha256;
        station.SpatialRegistryStableId = region.SpatialRegistryStableId;
        station.SpatialRegistryRevision = region.SpatialRegistryRevision;
        station.SpatialPackageStableIds = Sorted(region.SpatialPackageStableIds ?? []);
        var configuredAdministrativeAreas = Sorted(definition.AdministrativeAreaStableIds);
        var regionAdministrativeAreas = Sorted(region.AdministrativeAreaStableIds ?? []);
        station.AdministrativeAreaStableIds = configuredAdministrativeAreas;
        station.LegalAreaStableIds = Sorted(region.LegalAreaStableIds ?? []);
        if (!configuredAdministrativeAreas.SequenceEqual(regionAdministrativeAreas, StringComparer.Ordinal))
            return;

        var geographyLayers = (region.Layers ?? [])
            .Where(layer => layer is not null && string.Equals(
                layer.LayerKindCode,
                RegionExperienceLayerKinds.Geography,
                StringComparison.Ordinal))
            .ToArray();
        if (geographyLayers.Length != 1) return;
        var geography = geographyLayers[0];
        if (!string.Equals(
                geography.AvailabilityCode,
                RegionExperienceLayerAvailabilityCodes.Ready,
                StringComparison.Ordinal)
            || geography.ChangesOperationalState)
            return;

        var rawEndpoints = geography.Endpoints ?? [];
        if (rawEndpoints.Any(endpoint => endpoint is null)) return;
        var endpoints = rawEndpoints
            .OrderBy(item => item.PurposeCode, StringComparer.Ordinal)
            .ThenBy(item => item.RouteTemplate, StringComparer.Ordinal)
            .ToArray();
        if (endpoints.Length != 2
            || !SafeGeographyEndpoint(endpoints[0], "Manifest", AdministrativeDongDioramaRoutes.Manifest)
            || !SafeGeographyEndpoint(endpoints[1], "Tile", AdministrativeDongDioramaRoutes.Tile))
            return;
        station.GeographyEndpoints = endpoints.Select(Copy).ToArray();

        var projections = new List<StationDioramaAreaProjectionReference>();
        foreach (var administrativeAreaStableId in station.AdministrativeAreaStableIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = await administrativeDongDioramas.ManifestAsync(
                administrativeAreaStableId,
                cancellationToken);
            var projection = source is null
                ? null
                : await BuildProjectionReferenceAsync(
                    station,
                    administrativeAreaStableId,
                    source,
                    cancellationToken);
            if (projection is not null) projections.Add(projection);
        }
        station.AreaProjections = projections
            .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<StationDioramaAreaProjectionReference?> BuildProjectionReferenceAsync(
        StationDioramaManifest station,
        string requestedAdministrativeAreaStableId,
        AdministrativeDongDioramaManifest source,
        CancellationToken cancellationToken)
    {
        var sourceTiles = source.Tiles ?? [];
        var sourceBoundary = source.Boundary ?? [];
        if (!string.Equals(
                source.AdministrativeAreaStableId,
                requestedAdministrativeAreaStableId,
                StringComparison.Ordinal)
            || !string.Equals(source.SchemaVersion, AdministrativeDongDioramaPolicy.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(
                source.DataPolicyCode,
                AdministrativeDongDioramaPolicy.ObservationPresentationOnly,
                StringComparison.Ordinal)
            || !source.ObservationPresentationOnly
            || source.TraversalReady
            || source.GameplayReady
            || source.DistributionApproved
            || !string.Equals(
                source.ReadinessCode,
                AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(source.ProjectionHashSha256)
            || !Usable(source.CoordinateFrame)
            || !Usable(source.Bounds)
            || !UsableBoundary(sourceBoundary, source.Bounds))
            return null;
        if (sourceTiles.Any(tile => tile is null
                                    || string.IsNullOrWhiteSpace(tile.TileStableId)
                                    || string.IsNullOrWhiteSpace(tile.TileHashSha256)
                                    || tile.BuildingCount < 0
                                    || tile.RoadSegmentCount < 0)
            || sourceTiles.GroupBy(tile => tile.TileStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || sourceTiles.GroupBy(tile => (tile.TileIndexX, tile.TileIndexZ)).Any(group => group.Count() != 1))
            return null;

        var center = 행정동경계GeoJsonReader.ProjectWgs84(
            station.Anchor.Latitude,
            station.Anchor.Longitude,
            source.CoordinateFrame);
        var halfWidth = station.Window.WidthMeters / 2d;
        var halfDepth = station.Window.DepthMeters / 2d;
        var window = new AdministrativeDongDioramaBounds
        {
            MinX = center.X - halfWidth,
            MinZ = center.Z - halfDepth,
            MaxX = center.X + halfWidth,
            MaxZ = center.Z + halfDepth
        };
        if (!PolygonIntersectsBounds(sourceBoundary, window)) return null;

        var candidateTiles = sourceTiles
            .Where(tile => Intersects(window, tile, source.CoordinateFrame))
            .OrderBy(tile => tile.TileIndexX)
            .ThenBy(tile => tile.TileIndexZ)
            .ThenBy(tile => tile.TileStableId, StringComparer.Ordinal)
            .ToArray();
        if (candidateTiles.Length == 0) return null;

        var tiles = new List<AdministrativeDongDioramaTileSummary>();
        foreach (var summary in candidateTiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = await administrativeDongDioramas.TileAsync(
                requestedAdministrativeAreaStableId,
                summary.TileStableId,
                cancellationToken);
            if (!ValidTilePayload(source, summary, payload)) return null;
            if (PayloadIntersectsWindow(payload!, window, sourceBoundary)) tiles.Add(Copy(summary));
        }
        if (tiles.Count == 0) return null;

        return new StationDioramaAreaProjectionReference
        {
            AdministrativeAreaStableId = source.AdministrativeAreaStableId,
            ProjectionHashSha256 = source.ProjectionHashSha256,
            ReadinessCode = source.ReadinessCode,
            CoordinateFrame = Copy(source.CoordinateFrame),
            WindowBounds = window,
            SelectedTiles = tiles.ToArray(),
            Sources = (source.Sources ?? [])
                .OrderBy(item => item.SourceId, StringComparer.Ordinal)
                .ThenBy(item => item.DatasetId, StringComparer.Ordinal)
                .ThenBy(item => item.SourceRevision, StringComparer.Ordinal)
                .Select(Copy)
                .ToArray()
        };
    }

    private static bool SafeGeographyEndpoint(
        RegionExperienceLayerEndpoint? endpoint,
        string purposeCode,
        string routeTemplate)
        => endpoint is not null
           && string.Equals(endpoint.PurposeCode, purposeCode, StringComparison.Ordinal)
           && string.Equals(endpoint.HttpMethod, "GET", StringComparison.Ordinal)
           && string.Equals(endpoint.RouteTemplate, routeTemplate, StringComparison.Ordinal);

    private static bool Usable(AdministrativeDongDioramaCoordinateFrame frame)
        => frame is not null
           && string.Equals(frame.Method, SupportedCoordinateMethod, StringComparison.Ordinal)
           && double.IsFinite(frame.OriginLatitude)
           && frame.OriginLatitude is >= -90d and <= 90d
           && double.IsFinite(frame.OriginLongitude)
           && frame.OriginLongitude is >= -180d and <= 180d
           && double.IsFinite(frame.WorldOffsetX)
           && double.IsFinite(frame.WorldOffsetZ)
           && Math.Abs(frame.MetersPerUnit - 1d) < CoordinateTolerance;

    private static bool Usable(AdministrativeDongDioramaBounds? bounds)
        => bounds is not null
           && double.IsFinite(bounds.MinX)
           && double.IsFinite(bounds.MinZ)
           && double.IsFinite(bounds.MaxX)
           && double.IsFinite(bounds.MaxZ)
           && bounds.MaxX > bounds.MinX
           && bounds.MaxZ > bounds.MinZ;

    private static bool UsableBoundary(
        IReadOnlyList<AdministrativeDongDioramaPoint> boundary,
        AdministrativeDongDioramaBounds bounds)
        => boundary.Count >= 3
           && boundary.All(point => Usable(point)
                                    && point.X >= bounds.MinX - CoordinateTolerance
                                    && point.X <= bounds.MaxX + CoordinateTolerance
                                    && point.Z >= bounds.MinZ - CoordinateTolerance
                                    && point.Z <= bounds.MaxZ + CoordinateTolerance)
           && boundary.Where(point => point is not null)
               .Select(point => (point.X, point.Z))
               .Distinct()
               .Take(3)
               .Count() == 3
           && Math.Abs(DoubleArea(boundary)) > CoordinateTolerance;

    private static double DoubleArea(IReadOnlyList<AdministrativeDongDioramaPoint> polygon)
    {
        var area = 0d;
        for (var index = 0; index < polygon.Count; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Count];
            area += current.X * next.Z - next.X * current.Z;
        }
        return area;
    }

    private static bool ValidTilePayload(
        AdministrativeDongDioramaManifest manifest,
        AdministrativeDongDioramaTileSummary summary,
        AdministrativeDongDioramaTile? payload)
    {
        if (payload is null
            || !string.Equals(payload.SchemaVersion, AdministrativeDongDioramaPolicy.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(payload.AdministrativeAreaStableId, manifest.AdministrativeAreaStableId, StringComparison.Ordinal)
            || !string.Equals(payload.ProjectionHashSha256, manifest.ProjectionHashSha256, StringComparison.Ordinal)
            || !string.Equals(payload.TileStableId, summary.TileStableId, StringComparison.Ordinal)
            || !string.Equals(payload.TileHashSha256, summary.TileHashSha256, StringComparison.Ordinal)
            || payload.TileIndexX != summary.TileIndexX
            || payload.TileIndexZ != summary.TileIndexZ
            || payload.Buildings is null
            || payload.Roads is null
            || payload.Buildings.Length != summary.BuildingCount
            || payload.Roads.Length != summary.RoadSegmentCount
            || !Usable(payload.Bounds)
            || !ExpectedTileBounds(payload.Bounds, summary, manifest.CoordinateFrame))
            return false;

        if (payload.Buildings.Any(building => building is null
                                              || string.IsNullOrWhiteSpace(building.BuildingStableId)
                                              || building.Footprint is null
                                              || building.Footprint.Length < 3
                                              || building.Footprint.Any(point => !Usable(point))
                                              || building.Footprint
                                                  .Select(point => (point.X, point.Z))
                                                  .Distinct()
                                                  .Take(3)
                                                  .Count() != 3)
            || payload.Buildings.GroupBy(building => building.BuildingStableId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            return false;

        return !payload.Roads.Any(road => road is null
                                          || string.IsNullOrWhiteSpace(road.RoadStableId)
                                          || !Usable(road.From)
                                          || !Usable(road.To))
               && !payload.Roads.GroupBy(road => road.RoadStableId, StringComparer.Ordinal)
                   .Any(group => group.Count() != 1);
    }

    private static bool ExpectedTileBounds(
        AdministrativeDongDioramaBounds bounds,
        AdministrativeDongDioramaTileSummary summary,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        var minX = frame.WorldOffsetX + summary.TileIndexX * AdministrativeDongDioramaPolicy.TileSizeMeters;
        var minZ = frame.WorldOffsetZ + summary.TileIndexZ * AdministrativeDongDioramaPolicy.TileSizeMeters;
        return NearlyEqual(bounds.MinX, minX)
               && NearlyEqual(bounds.MinZ, minZ)
               && NearlyEqual(bounds.MaxX, minX + AdministrativeDongDioramaPolicy.TileSizeMeters)
               && NearlyEqual(bounds.MaxZ, minZ + AdministrativeDongDioramaPolicy.TileSizeMeters);
    }

    private static bool PayloadIntersectsWindow(
        AdministrativeDongDioramaTile payload,
        AdministrativeDongDioramaBounds window,
        IReadOnlyList<AdministrativeDongDioramaPoint> administrativeBoundary)
        => payload.Buildings.Any(building => PolygonIntersectsWindowAndBoundary(
               building.Footprint,
               window,
               administrativeBoundary))
           || payload.Roads.Any(road => SegmentIntersectsWindowAndBoundary(
               road.From,
               road.To,
               window,
               administrativeBoundary));

    private static bool PolygonIntersectsWindowAndBoundary(
        IReadOnlyList<AdministrativeDongDioramaPoint> polygon,
        AdministrativeDongDioramaBounds window,
        IReadOnlyList<AdministrativeDongDioramaPoint> administrativeBoundary)
    {
        if (polygon.Any(point => PointInsideBounds(point, window)
                                 && PointInsideOrOnPolygon(point, administrativeBoundary)))
            return true;
        if (administrativeBoundary.Any(point => PointInsideBounds(point, window)
                                                && PointInsideOrOnPolygon(point, polygon)))
            return true;

        var corners = BoundsCorners(window);
        if (corners.Any(corner => PointInsideOrOnPolygon(corner, polygon)
                                  && PointInsideOrOnPolygon(corner, administrativeBoundary)))
            return true;

        for (var index = 0; index < polygon.Count; index++)
            if (TryClipSegmentToBounds(
                    polygon[index],
                    polygon[(index + 1) % polygon.Count],
                    window,
                    out var clippedFrom,
                    out var clippedTo)
                && SegmentIntersectsPolygon(clippedFrom, clippedTo, administrativeBoundary))
                return true;
        return false;
    }

    private static bool SegmentIntersectsWindowAndBoundary(
        AdministrativeDongDioramaPoint from,
        AdministrativeDongDioramaPoint to,
        AdministrativeDongDioramaBounds window,
        IReadOnlyList<AdministrativeDongDioramaPoint> administrativeBoundary)
        => TryClipSegmentToBounds(from, to, window, out var clippedFrom, out var clippedTo)
           && SegmentIntersectsPolygon(clippedFrom, clippedTo, administrativeBoundary);

    private static bool PolygonIntersectsBounds(
        IReadOnlyList<AdministrativeDongDioramaPoint> polygon,
        AdministrativeDongDioramaBounds bounds)
    {
        if (polygon.Any(point => PointInsideBounds(point, bounds))) return true;
        var corners = BoundsCorners(bounds);
        if (corners.Any(corner => PointInsidePolygon(corner, polygon))) return true;
        for (var index = 0; index < polygon.Count; index++)
            if (SegmentIntersectsBounds(polygon[index], polygon[(index + 1) % polygon.Count], bounds))
                return true;
        return false;
    }

    private static AdministrativeDongDioramaPoint[] BoundsCorners(AdministrativeDongDioramaBounds bounds)
        =>
        [
            new AdministrativeDongDioramaPoint { X = bounds.MinX, Z = bounds.MinZ },
            new AdministrativeDongDioramaPoint { X = bounds.MinX, Z = bounds.MaxZ },
            new AdministrativeDongDioramaPoint { X = bounds.MaxX, Z = bounds.MinZ },
            new AdministrativeDongDioramaPoint { X = bounds.MaxX, Z = bounds.MaxZ }
        ];

    private static bool PointInsideBounds(
        AdministrativeDongDioramaPoint point,
        AdministrativeDongDioramaBounds bounds)
        => point.X >= bounds.MinX - CoordinateTolerance
           && point.X <= bounds.MaxX + CoordinateTolerance
           && point.Z >= bounds.MinZ - CoordinateTolerance
           && point.Z <= bounds.MaxZ + CoordinateTolerance;

    private static bool PointInsidePolygon(
        AdministrativeDongDioramaPoint point,
        IReadOnlyList<AdministrativeDongDioramaPoint> polygon)
    {
        var inside = false;
        for (var index = 0; index < polygon.Count; index++)
        {
            var previous = polygon[(index + polygon.Count - 1) % polygon.Count];
            var current = polygon[index];
            if ((current.Z > point.Z) == (previous.Z > point.Z)) continue;
            var intersectionX = (previous.X - current.X) * (point.Z - current.Z)
                                / (previous.Z - current.Z) + current.X;
            if (point.X < intersectionX) inside = !inside;
        }
        return inside;
    }

    private static bool PointInsideOrOnPolygon(
        AdministrativeDongDioramaPoint point,
        IReadOnlyList<AdministrativeDongDioramaPoint> polygon)
    {
        for (var index = 0; index < polygon.Count; index++)
            if (PointOnSegment(point, polygon[index], polygon[(index + 1) % polygon.Count]))
                return true;
        return PointInsidePolygon(point, polygon);
    }

    private static bool SegmentIntersectsPolygon(
        AdministrativeDongDioramaPoint from,
        AdministrativeDongDioramaPoint to,
        IReadOnlyList<AdministrativeDongDioramaPoint> polygon)
    {
        if (PointInsideOrOnPolygon(from, polygon) || PointInsideOrOnPolygon(to, polygon)) return true;
        for (var index = 0; index < polygon.Count; index++)
            if (SegmentsIntersect(from, to, polygon[index], polygon[(index + 1) % polygon.Count]))
                return true;
        return false;
    }

    private static bool SegmentsIntersect(
        AdministrativeDongDioramaPoint firstFrom,
        AdministrativeDongDioramaPoint firstTo,
        AdministrativeDongDioramaPoint secondFrom,
        AdministrativeDongDioramaPoint secondTo)
    {
        var firstSecondFrom = Cross(firstFrom, firstTo, secondFrom);
        var firstSecondTo = Cross(firstFrom, firstTo, secondTo);
        var secondFirstFrom = Cross(secondFrom, secondTo, firstFrom);
        var secondFirstTo = Cross(secondFrom, secondTo, firstTo);
        if (((firstSecondFrom > CoordinateTolerance && firstSecondTo < -CoordinateTolerance)
             || (firstSecondFrom < -CoordinateTolerance && firstSecondTo > CoordinateTolerance))
            && ((secondFirstFrom > CoordinateTolerance && secondFirstTo < -CoordinateTolerance)
                || (secondFirstFrom < -CoordinateTolerance && secondFirstTo > CoordinateTolerance)))
            return true;
        return Math.Abs(firstSecondFrom) <= CoordinateTolerance && PointOnSegment(secondFrom, firstFrom, firstTo)
               || Math.Abs(firstSecondTo) <= CoordinateTolerance && PointOnSegment(secondTo, firstFrom, firstTo)
               || Math.Abs(secondFirstFrom) <= CoordinateTolerance && PointOnSegment(firstFrom, secondFrom, secondTo)
               || Math.Abs(secondFirstTo) <= CoordinateTolerance && PointOnSegment(firstTo, secondFrom, secondTo);
    }

    private static bool PointOnSegment(
        AdministrativeDongDioramaPoint point,
        AdministrativeDongDioramaPoint from,
        AdministrativeDongDioramaPoint to)
        => Math.Abs(Cross(from, to, point)) <= CoordinateTolerance
           && point.X >= Math.Min(from.X, to.X) - CoordinateTolerance
           && point.X <= Math.Max(from.X, to.X) + CoordinateTolerance
           && point.Z >= Math.Min(from.Z, to.Z) - CoordinateTolerance
           && point.Z <= Math.Max(from.Z, to.Z) + CoordinateTolerance;

    private static double Cross(
        AdministrativeDongDioramaPoint from,
        AdministrativeDongDioramaPoint to,
        AdministrativeDongDioramaPoint point)
        => (to.X - from.X) * (point.Z - from.Z) - (to.Z - from.Z) * (point.X - from.X);

    private static bool SegmentIntersectsBounds(
        AdministrativeDongDioramaPoint from,
        AdministrativeDongDioramaPoint to,
        AdministrativeDongDioramaBounds bounds)
        => TryClipSegmentToBounds(from, to, bounds, out _, out _);

    private static bool TryClipSegmentToBounds(
        AdministrativeDongDioramaPoint from,
        AdministrativeDongDioramaPoint to,
        AdministrativeDongDioramaBounds bounds,
        out AdministrativeDongDioramaPoint clippedFrom,
        out AdministrativeDongDioramaPoint clippedTo)
    {
        var minimum = 0d;
        var maximum = 1d;
        var deltaX = to.X - from.X;
        var deltaZ = to.Z - from.Z;
        var intersects = Clip(-deltaX, from.X - bounds.MinX, ref minimum, ref maximum)
                         && Clip(deltaX, bounds.MaxX - from.X, ref minimum, ref maximum)
                         && Clip(-deltaZ, from.Z - bounds.MinZ, ref minimum, ref maximum)
                         && Clip(deltaZ, bounds.MaxZ - from.Z, ref minimum, ref maximum);
        clippedFrom = new AdministrativeDongDioramaPoint
        {
            X = from.X + minimum * deltaX,
            Z = from.Z + minimum * deltaZ
        };
        clippedTo = new AdministrativeDongDioramaPoint
        {
            X = from.X + maximum * deltaX,
            Z = from.Z + maximum * deltaZ
        };
        return intersects;
    }

    private static bool Clip(double direction, double distance, ref double minimum, ref double maximum)
    {
        if (Math.Abs(direction) < CoordinateTolerance) return distance >= -CoordinateTolerance;
        var ratio = distance / direction;
        if (direction < 0d)
        {
            if (ratio > maximum) return false;
            if (ratio > minimum) minimum = ratio;
        }
        else
        {
            if (ratio < minimum) return false;
            if (ratio < maximum) maximum = ratio;
        }
        return true;
    }

    private static bool Usable(AdministrativeDongDioramaPoint? point)
        => point is not null && double.IsFinite(point.X) && double.IsFinite(point.Z);

    private static bool NearlyEqual(double left, double right)
        => Math.Abs(left - right) < CoordinateTolerance;

    private static bool Intersects(
        AdministrativeDongDioramaBounds window,
        AdministrativeDongDioramaTileSummary tile,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        var tileMinX = frame.WorldOffsetX + tile.TileIndexX * AdministrativeDongDioramaPolicy.TileSizeMeters;
        var tileMinZ = frame.WorldOffsetZ + tile.TileIndexZ * AdministrativeDongDioramaPolicy.TileSizeMeters;
        var tileMaxX = tileMinX + AdministrativeDongDioramaPolicy.TileSizeMeters;
        var tileMaxZ = tileMinZ + AdministrativeDongDioramaPolicy.TileSizeMeters;
        return tileMaxX > window.MinX
               && tileMinX < window.MaxX
               && tileMaxZ > window.MinZ
               && tileMinZ < window.MaxZ;
    }

    private static StationDioramaSummary ToSummary(StationDioramaManifest manifest)
        => new()
        {
            TransitStationStableId = manifest.TransitStationStableId,
            OfficialStationName = manifest.OfficialStationName,
            DisplayName = manifest.DisplayName,
            NameConfirmationStatusCode = manifest.NameConfirmationStatusCode,
            Service = Copy(manifest.Service),
            StationTypeCode = manifest.StationTypeCode,
            WindowWidthMeters = manifest.Window.WidthMeters,
            WindowDepthMeters = manifest.Window.DepthMeters,
            ManifestRevision = manifest.ManifestRevision,
            ManifestHashSha256 = manifest.ManifestHashSha256,
            AvailabilityCode = manifest.AvailabilityCode,
            DistributionApproved = manifest.DistributionApproved
        };

    private static StationDioramaSourceEvidence StationSource(역세권디오라마Definition definition)
    {
        var rowReference = "line=" + definition.LineCode + ";station=" + definition.StationCode;
        var rowCanonical = string.Join("|",
            StationDioramaPolicy.SourceRowHashCanonicalVersion,
            definition.TransitStationStableId,
            definition.OfficialStationName,
            definition.LineCode,
            definition.StationCode,
            Number(definition.Latitude),
            Number(definition.Longitude),
            definition.OperatorName,
            역세권디오라마Catalog.TargetRowReferenceDate);
        return new StationDioramaSourceEvidence
        {
            SourceId = 역세권디오라마Catalog.SourceId,
            DatasetId = 역세권디오라마Catalog.DatasetId,
            OfficialSourcePageUrl = 역세권디오라마Catalog.OfficialSourcePageUrl,
            ProviderSourcePageUrl = 역세권디오라마Catalog.ProviderSourcePageUrl,
            SourceRevision = 역세권디오라마Catalog.SourceRevision,
            DataRevision = 역세권디오라마Catalog.DataRevision,
            TargetRowReferenceDate = 역세권디오라마Catalog.TargetRowReferenceDate,
            RawContentHashSha256 = 역세권디오라마Catalog.RawContentHashSha256,
            CollectedAtUtc = 역세권디오라마Catalog.CollectedAtUtc,
            SourceRowReference = rowReference,
            SourceRowHashCanonicalVersion = StationDioramaPolicy.SourceRowHashCanonicalVersion,
            SourceRowHashSha256 = Hash(rowCanonical),
            LicenseObserved = 역세권디오라마Catalog.LicenseObserved,
            QualityCode = StationDioramaQualityCodes.OfficialSourcePendingHumanReview,
            ReviewStatusCode = 역세권디오라마Catalog.ReviewStatusCode,
            LimitationCode = 역세권디오라마Catalog.LimitationCode,
            RightsCode = StationDioramaRightsCodes.PrivatePreviewOnly,
            DistributionApproved = false
        };
    }

    private static StationDioramaSpatialSnapshotCandidate? SpatialSnapshotCandidate(
        역세권디오라마공간사본Definition? definition)
        => definition is null
            ? null
            : new StationDioramaSpatialSnapshotCandidate
            {
                SchemaVersion = definition.SchemaVersion,
                Revision = definition.Revision,
                ContentHashSha256 = definition.ContentHashSha256,
                PayloadHashSha256 = definition.PayloadHashSha256,
                PayloadByteLength = definition.PayloadByteLength,
                BuildingCount = definition.BuildingCount,
                RoadCount = definition.RoadCount,
                SurfaceCount = definition.SurfaceCount,
                AdministrativeAreaCount = definition.AdministrativeAreaCount,
                CoverageCellCount = definition.CoverageCellCount,
                MissingCoverageCellCount = definition.MissingCoverageCellCount,
                MissingCoverageCodes = Sorted(definition.MissingCoverageCodes),
                ReviewStatusCode = StationDioramaSpatialSnapshotStatusCodes.LocalPrivateReview,
                ServerLoadable = false,
                DistributionApproved = false,
                TraversalReady = false,
                GameplayReady = false
            };

    private static RegionExperienceLayerEndpoint Copy(RegionExperienceLayerEndpoint value)
        => new()
        {
            PurposeCode = value.PurposeCode,
            HttpMethod = value.HttpMethod,
            RouteTemplate = value.RouteTemplate
        };

    private static StationDioramaServiceIdentifier Copy(StationDioramaServiceIdentifier value)
        => new()
        {
            OperatorName = value.OperatorName,
            LineCode = value.LineCode,
            StationCode = value.StationCode
        };

    private static AdministrativeDongDioramaCoordinateFrame Copy(AdministrativeDongDioramaCoordinateFrame value)
        => new()
        {
            Method = value.Method,
            OriginLatitude = value.OriginLatitude,
            OriginLongitude = value.OriginLongitude,
            WorldOffsetX = value.WorldOffsetX,
            WorldOffsetZ = value.WorldOffsetZ,
            MetersPerUnit = value.MetersPerUnit
        };

    private static AdministrativeDongDioramaTileSummary Copy(AdministrativeDongDioramaTileSummary value)
        => new()
        {
            TileStableId = value.TileStableId,
            TileIndexX = value.TileIndexX,
            TileIndexZ = value.TileIndexZ,
            TileHashSha256 = value.TileHashSha256,
            BuildingCount = value.BuildingCount,
            RoadSegmentCount = value.RoadSegmentCount
        };

    private static AdministrativeDongDioramaSourceAttribution Copy(AdministrativeDongDioramaSourceAttribution value)
        => new()
        {
            SourceId = value.SourceId,
            DatasetId = value.DatasetId,
            SourceRevision = value.SourceRevision,
            ContentHashSha256 = value.ContentHashSha256,
            LicenseCode = value.LicenseCode,
            LimitationCode = value.LimitationCode
        };

    private static string Canonical(StationDioramaManifest manifest)
        => string.Join("\n",
            manifest.SchemaVersion,
            manifest.TransitStationStableId,
            manifest.OfficialStationName,
            manifest.DisplayName,
            manifest.NameConfirmationStatusCode,
            string.Join(",", manifest.LegacyAliases),
            string.Join("|", manifest.Service.OperatorName, manifest.Service.LineCode, manifest.Service.StationCode),
            manifest.StationTypeCode,
            string.Join("|", manifest.Anchor.CoordinateReferenceSystemCode,
                Number(manifest.Anchor.Latitude), Number(manifest.Anchor.Longitude),
                manifest.Anchor.QualityCode, manifest.Anchor.QualityNote),
            string.Join("|", manifest.Window.PolicyCode, manifest.Window.WidthMeters,
                manifest.Window.DepthMeters, manifest.Window.MajorInterchangeOverrideApplied),
            manifest.ManifestRevision,
            manifest.AvailabilityCode,
            manifest.RegionStableId,
            manifest.RegionManifestHashSha256,
            manifest.SpatialRegistryStableId,
            manifest.SpatialRegistryRevision,
            string.Join(",", manifest.SpatialPackageStableIds),
            string.Join(",", manifest.AdministrativeAreaStableIds),
            string.Join(",", manifest.LegalAreaStableIds),
            string.Join("\n", manifest.GeographyEndpoints.Select(item => string.Join("|",
                item.PurposeCode, item.HttpMethod, item.RouteTemplate))),
            string.Join("\n", manifest.AreaProjections.Select(ProjectionCanonical)),
            SpatialSnapshotCandidateCanonical(manifest.SpatialSnapshotCandidate),
            string.Join("\n", manifest.StationSources.Select(item => string.Join("|",
                item.SourceId, item.DatasetId, item.OfficialSourcePageUrl, item.ProviderSourcePageUrl,
                item.SourceRevision, item.DataRevision, item.TargetRowReferenceDate,
                item.RawContentHashSha256, item.CollectedAtUtc.ToUniversalTime().ToString("O"),
                item.SourceRowReference, item.SourceRowHashCanonicalVersion, item.SourceRowHashSha256,
                item.LicenseObserved, item.QualityCode, item.ReviewStatusCode, item.LimitationCode,
                item.RightsCode, item.DistributionApproved))),
            string.Join(",", manifest.QualityCodes),
            manifest.ObservationPresentationOnly,
            manifest.TraversalReady,
            manifest.GameplayReady,
            manifest.DistributionApproved);

    internal static string ComputeManifestHash(StationDioramaManifest manifest)
        => Hash(Canonical(manifest));

    private static string ProjectionCanonical(StationDioramaAreaProjectionReference item)
        => string.Join("|",
            item.AdministrativeAreaStableId,
            item.ProjectionHashSha256,
            item.ReadinessCode,
            item.ManifestRouteTemplate,
            item.TileRouteTemplate,
            item.CoordinateFrame.Method,
            Number(item.CoordinateFrame.OriginLatitude),
            Number(item.CoordinateFrame.OriginLongitude),
            Number(item.CoordinateFrame.WorldOffsetX),
            Number(item.CoordinateFrame.WorldOffsetZ),
            Number(item.CoordinateFrame.MetersPerUnit),
            Number(item.WindowBounds.MinX),
            Number(item.WindowBounds.MinZ),
            Number(item.WindowBounds.MaxX),
            Number(item.WindowBounds.MaxZ),
            string.Join(",", item.SelectedTiles.Select(tile => string.Join(":",
                tile.TileStableId, tile.TileIndexX, tile.TileIndexZ, tile.TileHashSha256,
                tile.BuildingCount, tile.RoadSegmentCount))),
            string.Join(",", item.Sources.Select(source => string.Join(":",
                source.SourceId, source.DatasetId, source.SourceRevision,
                source.ContentHashSha256, source.LicenseCode, source.LimitationCode))));

    private static string SpatialSnapshotCandidateCanonical(
        StationDioramaSpatialSnapshotCandidate? item)
        => item is null
            ? string.Empty
            : string.Join("|",
                item.SchemaVersion,
                item.Revision,
                item.ContentHashSha256,
                item.PayloadHashSha256,
                item.PayloadByteLength,
                item.BuildingCount,
                item.RoadCount,
                item.SurfaceCount,
                item.AdministrativeAreaCount,
                item.CoverageCellCount,
                item.MissingCoverageCellCount,
                string.Join(",", item.MissingCoverageCodes),
                item.ReviewStatusCode,
                item.ServerLoadable,
                item.DistributionApproved,
                item.TraversalReady,
                item.GameplayReady);

    private static void ValidateDefinitions(IReadOnlyList<역세권디오라마Definition> definitions)
    {
        if (definitions is null || definitions.Count == 0)
            throw new InvalidOperationException("StationDioramaCatalogEmpty");
        if (definitions.GroupBy(item => item.TransitStationStableId, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
            throw new InvalidOperationException("TransitStationStableIdDuplicate");

        foreach (var definition in definitions)
        {
            if (!StationDioramaPolicy.IsTransitStationStableId(definition.TransitStationStableId)
                || !string.Equals(
                    definition.TransitStationStableId,
                    definition.TransitStationStableId.Trim(),
                    StringComparison.Ordinal))
                throw new InvalidOperationException("TransitStationStableIdInvalid");
            var idParts = definition.TransitStationStableId.Split(':');
            if (string.IsNullOrWhiteSpace(definition.LineCode)
                || string.IsNullOrWhiteSpace(definition.StationCode)
                || !string.Equals(idParts[3], definition.LineCode.ToLowerInvariant(), StringComparison.Ordinal)
                || !string.Equals(idParts[4], definition.StationCode, StringComparison.Ordinal))
                throw new InvalidOperationException("TransitStationStableIdServiceMismatch");
            if (string.IsNullOrWhiteSpace(definition.OfficialStationName)
                || string.IsNullOrWhiteSpace(definition.DisplayName)
                || string.IsNullOrWhiteSpace(definition.OperatorName)
                || string.IsNullOrWhiteSpace(definition.ManifestRevision))
                throw new InvalidOperationException("TransitStationIdentityRequired");
            if (!string.IsNullOrWhiteSpace(definition.RegionStableId)
                && !RegionExperiencePackagePolicy.IsRegionStableId(definition.RegionStableId))
                throw new InvalidOperationException("TransitStationRegionStableIdInvalid");
            if (definition.AdministrativeAreaStableIds.Any(area =>
                    !AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(area))
                || definition.AdministrativeAreaStableIds
                    .GroupBy(area => area, StringComparer.Ordinal)
                    .Any(group => group.Count() != 1))
                throw new InvalidOperationException("TransitStationAdministrativeAreaReferencesInvalid");
            if (definition.LegacyAliases.Any(alias =>
                    alias.StartsWith("reference:", StringComparison.Ordinal)
                    || alias.StartsWith("anchor:", StringComparison.Ordinal)))
                throw new InvalidOperationException("TransitStationSpatialReferenceCannotBeLegacyAlias");
            if (!double.IsFinite(definition.Latitude)
                || definition.Latitude is < -90d or > 90d
                || !double.IsFinite(definition.Longitude)
                || definition.Longitude is < -180d or > 180d)
                throw new InvalidOperationException("TransitStationCoordinateInvalid");
            if (!string.Equals(
                    definition.NameConfirmationStatusCode,
                    StationDioramaNameConfirmationStatusCodes.SourceNameConfirmed,
                    StringComparison.Ordinal)
                && !string.Equals(
                    definition.NameConfirmationStatusCode,
                    StationDioramaNameConfirmationStatusCodes.UserConfirmedDisplayName,
                    StringComparison.Ordinal)
                && !string.Equals(
                    definition.NameConfirmationStatusCode,
                    StationDioramaNameConfirmationStatusCodes.PendingUserNameConfirmation,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("TransitStationNameConfirmationStatusInvalid");
            if (string.Equals(definition.StationTypeCode, StationDioramaStationTypeCodes.Normal, StringComparison.Ordinal))
            {
                if (definition.WindowWidthMeters != StationDioramaPolicy.StandardWindowMeters
                    || definition.WindowDepthMeters != StationDioramaPolicy.StandardWindowMeters)
                    throw new InvalidOperationException("NormalTransitStationWindowMustBe1000Meters");
            }
            else if (!string.Equals(
                         definition.StationTypeCode,
                         StationDioramaStationTypeCodes.MajorInterchange,
                         StringComparison.Ordinal)
                     || definition.WindowWidthMeters <= 0
                     || definition.WindowDepthMeters <= 0)
            {
                throw new InvalidOperationException("TransitStationWindowPolicyInvalid");
            }

            ValidateSpatialSnapshot(definition);
        }
    }

    private static void ValidateSpatialSnapshot(역세권디오라마Definition definition)
    {
        var snapshot = definition.SpatialSnapshot;
        if (snapshot is null) return;
        if (!string.Equals(
                snapshot.TransitStationStableId,
                definition.TransitStationStableId,
                StringComparison.Ordinal))
            throw new InvalidOperationException("TransitStationSpatialSnapshotIdentityMismatch");
        if (!string.Equals(
                snapshot.SchemaVersion,
                역세권디오라마Catalog.SpatialSnapshotSchemaVersion,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(snapshot.Revision))
            throw new InvalidOperationException("TransitStationSpatialSnapshotSchemaInvalid");
        if (!IsSha256(snapshot.ContentHashSha256)
            || !IsSha256(snapshot.PayloadHashSha256)
            || snapshot.PayloadByteLength <= 0)
            throw new InvalidOperationException("TransitStationSpatialSnapshotFingerprintInvalid");
        if (snapshot.BuildingCount < 0
            || snapshot.RoadCount < 0
            || snapshot.SurfaceCount < 0
            || snapshot.AdministrativeAreaCount < 0
            || snapshot.CoverageCellCount <= 0
            || snapshot.MissingCoverageCellCount < 0
            || snapshot.MissingCoverageCellCount > snapshot.CoverageCellCount)
            throw new InvalidOperationException("TransitStationSpatialSnapshotCountsInvalid");
        if (snapshot.MissingCoverageCodes is null
            || snapshot.MissingCoverageCodes.Any(string.IsNullOrWhiteSpace)
            || snapshot.MissingCoverageCodes.GroupBy(code => code, StringComparer.Ordinal)
                .Any(group => group.Count() != 1)
            || snapshot.MissingCoverageCellCount > 0 && snapshot.MissingCoverageCodes.Count == 0)
            throw new InvalidOperationException("TransitStationSpatialSnapshotMissingCoverageInvalid");
    }

    private static bool IsSha256(string value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string[] Sorted(IEnumerable<string> values)
        => values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string Number(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
