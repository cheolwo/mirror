using System.Security.Cryptography;
using System.Text;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Application.WorldProjection;

public interface I지역ExperiencePackage조회UseCase
{
    Task<RegionExperiencePackageCatalogResponse> CatalogAsync(CancellationToken cancellationToken);
    Task<RegionExperiencePackageManifest?> ManifestAsync(string regionStableId, CancellationToken cancellationToken);
}

public interface I지역ExperiencePackageCatalog
{
    IReadOnlyList<지역ExperiencePackageDefinition> All { get; }
}

public sealed record 지역ExperienceLayerDefinition(
    string LayerStableId,
    string LayerKindCode,
    string DisplayName,
    string SchemaVersion,
    string LoadModeCode,
    string CacheModeCode,
    string RequiredFeatureKey,
    bool RequiredForRegionOpen,
    bool Implemented,
    IReadOnlyList<RegionExperienceLayerEndpoint> Endpoints);

public sealed record 지역ExperiencePackageDefinition(
    string RegionStableId,
    string DisplayName,
    string ShortDescription,
    string PackageRevision,
    string SpatialRegistryStableId,
    string SpatialRegistryRevision,
    IReadOnlyList<string> SpatialPackageStableIds,
    IReadOnlyList<string> AdministrativeAreaStableIds,
    IReadOnlyList<string> LegalAreaStableIds,
    IReadOnlyList<지역ExperienceLayerDefinition> Layers);

/// <summary>
/// 지역 하나를 Steam 무료 본편에서 독립적으로 발견·갱신할 수 있는 콘텐츠 묶음으로 정의합니다.
/// 기존 공간·광고·운영 API의 권위를 합치지 않고 읽기 경로와 판본만 한 manifest로 묶습니다.
/// </summary>
public sealed class 지역ExperiencePackageCatalog : I지역ExperiencePackageCatalog
{
    public IReadOnlyList<지역ExperiencePackageDefinition> All { get; } =
    [
        new(
            RegionExperiencePackagePolicy.MyeonmokStationRegionStableId,
            "면목역 1km 관찰 디오라마",
            "면목역 중심 1km × 1km 창에 실제 근거를 사본으로 조립하고 자료 결손을 그대로 드러내는 비공개 관찰 패키지",
            "myeonmok-station-region-experience.r1",
            "station-area-package-registry:seoul-east.v1",
            "seoul-east-station-area-packages.r1",
            ["station-spatial-package:station:kr:kric:s1107:0721.private-review.r1"],
            [
                "region:kr:hjd:1126052000",
                "region:kr:hjd:1126055000",
                "region:kr:hjd:1126056500",
                "region:kr:hjd:1126057500",
                "region:kr:hjd:1126059000",
                "region:kr:hjd:1126066000"
            ],
            ["region:kr:bjd:1126010100", "region:kr:bjd:1126010200"],
            [
                new(
                    "region-layer:myeonmok-station:geography.r1",
                    RegionExperienceLayerKinds.Geography,
                    "면목역 1km 행정동 경계·건물·도로",
                    "ssalddel.station-spatial-snapshot.v1",
                    RegionExperienceLayerLoadModes.NotLoadable,
                    RegionExperienceLayerCacheModes.None,
                    string.Empty,
                    true,
                    false,
                    []),
                new(
                    "region-layer:myeonmok-station:life-context.r1",
                    RegionExperienceLayerKinds.LifeContext,
                    "면목역 생활 맥락",
                    "station-life-context.v1",
                    RegionExperienceLayerLoadModes.NotLoadable,
                    RegionExperienceLayerCacheModes.None,
                    string.Empty,
                    false,
                    false,
                    []),
                new(
                    "region-layer:myeonmok-station:gameplay.r1",
                    RegionExperienceLayerKinds.Gameplay,
                    "면목역 방어 gameplay",
                    "region-gameplay-module.v1",
                    RegionExperienceLayerLoadModes.NotLoadable,
                    RegionExperienceLayerCacheModes.None,
                    string.Empty,
                    false,
                    false,
                    [])
            ]),
        new(
            RegionExperiencePackagePolicy.SagajeongRegionStableId,
            "사가정 생활 복구 디오라마",
            "사가정역 1km의 실제 공간 근거 위에 생활 밀도와 초기 복구 정착기 시나리오를 층별로 조립하는 첫 지역 패키지",
            "sagajeong-region-experience.r2",
            "neighborhood-package-registry:seoul-east.v2",
            "seoul-east-neighborhood-packages.r2",
            ["neighborhood-spatial-package:region:kr:bjd:1126010100.v2"],
            [AdministrativeDongDioramaPolicy.FirstAdministrativeAreaStableId],
            ["region:kr:bjd:1126010100"],
            [
                new(
                    "region-layer:sagajeong:geography.r1",
                    RegionExperienceLayerKinds.Geography,
                    "행정동 경계·건물·도로",
                    AdministrativeDongDioramaPolicy.SchemaVersion,
                    RegionExperienceLayerLoadModes.Bootstrap,
                    RegionExperienceLayerCacheModes.ImmutableByHash,
                    VersionFeatureFlagKeys.AdministrativeDongDioramaObservation,
                    true,
                    true,
                    [
                        Endpoint("Manifest", AdministrativeDongDioramaRoutes.Manifest),
                        Endpoint("Tile", AdministrativeDongDioramaRoutes.Tile)
                    ]),
                new(
                    "region-layer:sagajeong:mobility.r1",
                    RegionExperienceLayerKinds.Mobility,
                    "사가정 1km 정적 이동망 검토 후보",
                    RegionMobilityGraphPolicy.ManifestSchemaVersion,
                    RegionExperienceLayerLoadModes.OnDemand,
                    RegionExperienceLayerCacheModes.ImmutableByHash,
                    VersionFeatureFlagKeys.RegionMobilityObservation,
                    false,
                    true,
                    [
                        Endpoint("Manifest", RegionMobilityGraphRoutes.Manifest),
                        Endpoint("Tile", RegionMobilityGraphRoutes.Tile)
                    ]),
                new(
                    "region-layer:sagajeong:life-context.r1",
                    RegionExperienceLayerKinds.LifeContext,
                    "익명 생활 밀도",
                    "administrative-dong-life-context.v1",
                    RegionExperienceLayerLoadModes.NotLoadable,
                    RegionExperienceLayerCacheModes.None,
                    string.Empty,
                    false,
                    false,
                    []),
                new(
                    "region-layer:sagajeong:recovery-scenario.r1",
                    RegionExperienceLayerKinds.RecoveryScenario,
                    "복합 생태 이변 초기 복구 정착기",
                    "administrative-dong-recovery-scenario.v1",
                    RegionExperienceLayerLoadModes.NotLoadable,
                    RegionExperienceLayerCacheModes.None,
                    string.Empty,
                    false,
                    false,
                    []),
                new(
                    "region-layer:sagajeong:gameplay.r1",
                    RegionExperienceLayerKinds.Gameplay,
                    "지역 복구 gameplay",
                    "region-gameplay-module.v1",
                    RegionExperienceLayerLoadModes.NotLoadable,
                    RegionExperienceLayerCacheModes.None,
                    string.Empty,
                    false,
                    false,
                    []),
                new(
                    "region-layer:sagajeong:commercial-display.r1",
                    RegionExperienceLayerKinds.CommercialDisplay,
                    "검증 상호·광고·후원 표시",
                    AdministrativeDongDioramaPolicy.DisplayOverlaySchemaVersion,
                    RegionExperienceLayerLoadModes.LiveRefresh,
                    RegionExperienceLayerCacheModes.Revalidate,
                    VersionFeatureFlagKeys.AdministrativeDongDioramaObservation,
                    false,
                    true,
                    [Endpoint("DisplayOverlays", AdministrativeDongDioramaRoutes.DisplayOverlays)]),
                new(
                    "region-layer:sagajeong:operational-snapshot.r1",
                    RegionExperienceLayerKinds.OperationalSnapshot,
                    "선택형 운영 상태 사본",
                    OperationalWorldScenePolicy.SchemaVersionV2,
                    RegionExperienceLayerLoadModes.LiveRefresh,
                    RegionExperienceLayerCacheModes.Revalidate,
                    VersionFeatureFlagKeys.OperationalWorldObservationWorkflow,
                    false,
                    true,
                    [Endpoint("SceneSnapshots", OperationalWorldSceneRoutes.AreaSnapshot)])
            ])
    ];

    private static RegionExperienceLayerEndpoint Endpoint(string purposeCode, string routeTemplate)
        => new()
        {
            PurposeCode = purposeCode,
            HttpMethod = "GET",
            RouteTemplate = routeTemplate
        };
}

public sealed class 지역ExperiencePackage조회UseCase(
    I지역ExperiencePackageCatalog catalog,
    IVersionFeatureFlagService featureFlags) : I지역ExperiencePackage조회UseCase
{
    public Task<RegionExperiencePackageCatalogResponse> CatalogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifests = catalog.All
            .OrderBy(item => item.RegionStableId, StringComparer.Ordinal)
            .Select(BuildManifest)
            .ToArray();
        var items = manifests.Select(ToSummary).ToArray();
        return Task.FromResult(new RegionExperiencePackageCatalogResponse
        {
            CatalogRevision = Hash(string.Join("\n", items.Select(item =>
                item.RegionStableId + "|" + item.ManifestHashSha256))),
            Items = items
        });
    }

    public Task<RegionExperiencePackageManifest?> ManifestAsync(
        string regionStableId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!RegionExperiencePackagePolicy.IsRegionStableId(regionStableId))
            throw new ArgumentException("RegionExperienceStableIdInvalid", nameof(regionStableId));
        var definition = catalog.All.SingleOrDefault(item => string.Equals(
            item.RegionStableId,
            regionStableId.Trim(),
            StringComparison.Ordinal));
        return Task.FromResult(definition is null ? null : BuildManifest(definition));
    }

    private RegionExperiencePackageManifest BuildManifest(지역ExperiencePackageDefinition definition)
    {
        var layers = definition.Layers
            .OrderBy(item => item.LayerStableId, StringComparer.Ordinal)
            .Select(BuildLayer)
            .ToArray();
        var manifest = new RegionExperiencePackageManifest
        {
            RegionStableId = definition.RegionStableId,
            DisplayName = definition.DisplayName,
            ShortDescription = definition.ShortDescription,
            PackageRevision = definition.PackageRevision,
            AvailabilityCode = RegionExperiencePackageAvailabilityCodes.PrivatePreview,
            ReleaseChannelCode = RegionExperiencePackageReleaseChannels.SteamFreeBaseGame,
            IsFree = true,
            DistributionApproved = false,
            GameplayReady = false,
            AdvertisingEnabled = false,
            OperationalServicesEnabled = false,
            SpatialRegistryStableId = definition.SpatialRegistryStableId,
            SpatialRegistryRevision = definition.SpatialRegistryRevision,
            SpatialPackageStableIds = Sorted(definition.SpatialPackageStableIds),
            AdministrativeAreaStableIds = Sorted(definition.AdministrativeAreaStableIds),
            LegalAreaStableIds = Sorted(definition.LegalAreaStableIds),
            Layers = layers
        };
        manifest.ManifestHashSha256 = Hash(Canonical(manifest));
        return manifest;
    }

    private RegionExperienceLayerReference BuildLayer(지역ExperienceLayerDefinition definition)
    {
        var enabled = definition.Implemented
                      && (string.IsNullOrWhiteSpace(definition.RequiredFeatureKey)
                          || featureFlags.IsEnabled(definition.RequiredFeatureKey));
        return new RegionExperienceLayerReference
        {
            LayerStableId = definition.LayerStableId,
            LayerKindCode = definition.LayerKindCode,
            DisplayName = definition.DisplayName,
            SchemaVersion = definition.SchemaVersion,
            AvailabilityCode = !definition.Implemented
                ? RegionExperienceLayerAvailabilityCodes.Planned
                : enabled
                    ? RegionExperienceLayerAvailabilityCodes.Ready
                    : RegionExperienceLayerAvailabilityCodes.Disabled,
            LoadModeCode = enabled
                ? definition.LoadModeCode
                : RegionExperienceLayerLoadModes.NotLoadable,
            CacheModeCode = enabled
                ? definition.CacheModeCode
                : RegionExperienceLayerCacheModes.None,
            RequiredFeatureKey = definition.RequiredFeatureKey,
            RequiredForRegionOpen = definition.RequiredForRegionOpen,
            ChangesOperationalState = false,
            Endpoints = enabled
                ? definition.Endpoints
                    .OrderBy(item => item.PurposeCode, StringComparer.Ordinal)
                    .Select(item => new RegionExperienceLayerEndpoint
                    {
                        PurposeCode = item.PurposeCode,
                        HttpMethod = item.HttpMethod,
                        RouteTemplate = item.RouteTemplate
                    })
                    .ToArray()
                : []
        };
    }

    private static RegionExperiencePackageSummary ToSummary(RegionExperiencePackageManifest manifest)
        => new()
        {
            RegionStableId = manifest.RegionStableId,
            DisplayName = manifest.DisplayName,
            ShortDescription = manifest.ShortDescription,
            PackageRevision = manifest.PackageRevision,
            ManifestHashSha256 = manifest.ManifestHashSha256,
            AvailabilityCode = manifest.AvailabilityCode,
            ReleaseChannelCode = manifest.ReleaseChannelCode,
            IsFree = manifest.IsFree,
            DistributionApproved = manifest.DistributionApproved
        };

    private static string Canonical(RegionExperiencePackageManifest manifest)
        => string.Join("\n",
            manifest.SchemaVersion,
            manifest.RegionStableId,
            manifest.DisplayName,
            manifest.ShortDescription,
            manifest.PackageRevision,
            manifest.AvailabilityCode,
            manifest.ReleaseChannelCode,
            manifest.IsFree,
            manifest.DistributionApproved,
            manifest.GameplayReady,
            manifest.AdvertisingEnabled,
            manifest.OperationalServicesEnabled,
            manifest.SpatialRegistryStableId,
            manifest.SpatialRegistryRevision,
            string.Join(",", manifest.SpatialPackageStableIds),
            string.Join(",", manifest.AdministrativeAreaStableIds),
            string.Join(",", manifest.LegalAreaStableIds),
            string.Join("\n", manifest.Layers.Select(layer => string.Join("|",
                layer.LayerStableId,
                layer.LayerKindCode,
                layer.DisplayName,
                layer.SchemaVersion,
                layer.AvailabilityCode,
                layer.LoadModeCode,
                layer.CacheModeCode,
                layer.RequiredFeatureKey,
                layer.RequiredForRegionOpen,
                layer.ChangesOperationalState,
                string.Join(",", layer.Endpoints.Select(endpoint =>
                    endpoint.PurposeCode + ":" + endpoint.HttpMethod + ":" + endpoint.RouteTemplate))))));

    private static string[] Sorted(IEnumerable<string> values)
        => values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
