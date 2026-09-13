using Ssalddel.Application.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Application.WorldProjection;

public sealed class 지역ExperiencePackage조회UseCaseTests
{
    [Fact]
    public async Task 사가정은_공간대장을재사용하고_미구현레이어를계획상태로분리한다()
    {
        var useCase = Create();

        var manifest = await useCase.ManifestAsync(
            RegionExperiencePackagePolicy.SagajeongRegionStableId,
            CancellationToken.None);
        Assert.NotNull(manifest);

        Assert.Equal("neighborhood-package-registry:seoul-east.v2", manifest.SpatialRegistryStableId);
        Assert.Equal("seoul-east-neighborhood-packages.r2", manifest.SpatialRegistryRevision);
        Assert.Contains("neighborhood-spatial-package:region:kr:bjd:1126010100.v2", manifest.SpatialPackageStableIds);
        Assert.Contains(AdministrativeDongDioramaPolicy.FirstAdministrativeAreaStableId, manifest.AdministrativeAreaStableIds);
        Assert.False(manifest.DistributionApproved);
        Assert.False(manifest.GameplayReady);
        Assert.False(manifest.AdvertisingEnabled);
        Assert.False(manifest.OperationalServicesEnabled);
        Assert.All(manifest.Layers, layer => Assert.False(layer.ChangesOperationalState));

        var planned = manifest.Layers.Where(layer => layer.AvailabilityCode == RegionExperienceLayerAvailabilityCodes.Planned).ToArray();
        Assert.Equal(3, planned.Length);
        Assert.All(planned, layer => Assert.Empty(layer.Endpoints));
    }

    [Fact]
    public async Task 면목역패키지는_여섯행정동과두법정동을보존하고_역범위API전에는계획상태다()
    {
        var manifest = await Create(VersionFeatureFlagKeys.AdministrativeDongDioramaObservation)
            .ManifestAsync(RegionExperiencePackagePolicy.MyeonmokStationRegionStableId, CancellationToken.None);

        Assert.NotNull(manifest);
        Assert.Equal("myeonmok-station-region-experience.r1", manifest.PackageRevision);
        Assert.Equal(
        [
            "region:kr:hjd:1126052000",
            "region:kr:hjd:1126055000",
            "region:kr:hjd:1126056500",
            "region:kr:hjd:1126057500",
            "region:kr:hjd:1126059000",
            "region:kr:hjd:1126066000"
        ], manifest.AdministrativeAreaStableIds);
        Assert.Equal(
            ["region:kr:bjd:1126010100", "region:kr:bjd:1126010200"],
            manifest.LegalAreaStableIds);
        Assert.Contains(
            "station-spatial-package:station:kr:kric:s1107:0721.private-review.r1",
            manifest.SpatialPackageStableIds);
        var geography = Assert.Single(
            manifest.Layers,
            layer => layer.LayerKindCode == RegionExperienceLayerKinds.Geography);
        Assert.Equal("ssalddel.station-spatial-snapshot.v1", geography.SchemaVersion);
        Assert.Equal(RegionExperienceLayerAvailabilityCodes.Planned, geography.AvailabilityCode);
        Assert.Equal(RegionExperienceLayerLoadModes.NotLoadable, geography.LoadModeCode);
        Assert.Equal(RegionExperienceLayerCacheModes.None, geography.CacheModeCode);
        Assert.Empty(geography.Endpoints);
        Assert.False(manifest.DistributionApproved);
        Assert.False(manifest.GameplayReady);
        Assert.False(manifest.OperationalServicesEnabled);
    }

    [Fact]
    public async Task 기능이꺼진레이어는_주소를노출하지않고_활성화된레이어만기존API를참조한다()
    {
        var closed = await Create().ManifestAsync(
            RegionExperiencePackagePolicy.SagajeongRegionStableId,
            CancellationToken.None);
        var opened = await Create(
                VersionFeatureFlagKeys.AdministrativeDongDioramaObservation,
                VersionFeatureFlagKeys.RegionMobilityObservation,
                VersionFeatureFlagKeys.OperationalWorldObservationWorkflow)
            .ManifestAsync(RegionExperiencePackagePolicy.SagajeongRegionStableId, CancellationToken.None);
        Assert.NotNull(closed);
        Assert.NotNull(opened);

        Assert.All(closed.Layers.Where(layer => layer.AvailabilityCode == RegionExperienceLayerAvailabilityCodes.Disabled),
            layer => Assert.Empty(layer.Endpoints));
        var geography = Assert.Single(opened.Layers, layer => layer.LayerKindCode == RegionExperienceLayerKinds.Geography);
        Assert.Equal(RegionExperienceLayerAvailabilityCodes.Ready, geography.AvailabilityCode);
        Assert.Contains(geography.Endpoints, endpoint => endpoint.RouteTemplate == AdministrativeDongDioramaRoutes.Manifest);
        Assert.Contains(geography.Endpoints, endpoint => endpoint.RouteTemplate == AdministrativeDongDioramaRoutes.Tile);
        var mobility = Assert.Single(opened.Layers, layer => layer.LayerKindCode == RegionExperienceLayerKinds.Mobility);
        Assert.Equal(RegionExperienceLayerAvailabilityCodes.Ready, mobility.AvailabilityCode);
        Assert.Equal(RegionExperienceLayerLoadModes.OnDemand, mobility.LoadModeCode);
        Assert.Equal(RegionExperienceLayerCacheModes.ImmutableByHash, mobility.CacheModeCode);
        Assert.Contains(mobility.Endpoints, endpoint => endpoint.RouteTemplate == RegionMobilityGraphRoutes.Manifest);
        Assert.Contains(mobility.Endpoints, endpoint => endpoint.RouteTemplate == RegionMobilityGraphRoutes.Tile);
        var operations = Assert.Single(opened.Layers, layer => layer.LayerKindCode == RegionExperienceLayerKinds.OperationalSnapshot);
        Assert.Equal(RegionExperienceLayerAvailabilityCodes.Ready, operations.AvailabilityCode);
        Assert.Contains(operations.Endpoints, endpoint => endpoint.RouteTemplate == OperationalWorldSceneRoutes.AreaSnapshot);
    }

    [Fact]
    public async Task 이동망기능이꺼져있으면_정적이동망주소와실행권위를노출하지않는다()
    {
        var manifest = await Create(VersionFeatureFlagKeys.AdministrativeDongDioramaObservation)
            .ManifestAsync(RegionExperiencePackagePolicy.SagajeongRegionStableId, CancellationToken.None);

        Assert.NotNull(manifest);
        var mobility = Assert.Single(manifest.Layers, layer => layer.LayerKindCode == RegionExperienceLayerKinds.Mobility);
        Assert.Equal(RegionExperienceLayerAvailabilityCodes.Disabled, mobility.AvailabilityCode);
        Assert.Equal(RegionExperienceLayerLoadModes.NotLoadable, mobility.LoadModeCode);
        Assert.Equal(RegionExperienceLayerCacheModes.None, mobility.CacheModeCode);
        Assert.False(mobility.RequiredForRegionOpen);
        Assert.False(mobility.ChangesOperationalState);
        Assert.Empty(mobility.Endpoints);
    }

    [Fact]
    public async Task 같은기능상태는_같은manifest와catalog_hash를만든다()
    {
        var first = Create(VersionFeatureFlagKeys.AdministrativeDongDioramaObservation);
        var second = Create(VersionFeatureFlagKeys.AdministrativeDongDioramaObservation);

        var firstManifest = await first.ManifestAsync(RegionExperiencePackagePolicy.SagajeongRegionStableId, CancellationToken.None);
        var secondManifest = await second.ManifestAsync(RegionExperiencePackagePolicy.SagajeongRegionStableId, CancellationToken.None);
        Assert.NotNull(firstManifest);
        Assert.NotNull(secondManifest);
        var firstCatalog = await first.CatalogAsync(CancellationToken.None);
        var secondCatalog = await second.CatalogAsync(CancellationToken.None);

        Assert.Equal(firstManifest.ManifestHashSha256, secondManifest.ManifestHashSha256);
        Assert.Equal(firstCatalog.CatalogRevision, secondCatalog.CatalogRevision);
        Assert.Equal(2, firstCatalog.Items.Length);
        Assert.Equal(
            firstManifest.ManifestHashSha256,
            firstCatalog.Items.Single(item => item.RegionStableId ==
                RegionExperiencePackagePolicy.SagajeongRegionStableId).ManifestHashSha256);
    }

    [Fact]
    public async Task 알수없는지역은_null이고_잘못된식별자는거절한다()
    {
        var useCase = Create();

        Assert.Null(await useCase.ManifestAsync("world-region:kr:unknown", CancellationToken.None));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => useCase.ManifestAsync("region:kr:hjd:1126057500", CancellationToken.None));
        Assert.Equal("RegionExperienceStableIdInvalid", error.Message.Split(' ')[0]);
    }

    private static 지역ExperiencePackage조회UseCase Create(params string[] enabled)
        => new(new 지역ExperiencePackageCatalog(), new StaticFeatureFlags(enabled));

    private sealed class StaticFeatureFlags(IEnumerable<string> enabled) : IVersionFeatureFlagService
    {
        private readonly HashSet<string> _enabled = enabled.ToHashSet(StringComparer.Ordinal);

        public bool IsEnabled(string featureKey) => _enabled.Contains(featureKey);

        public IReadOnlyDictionary<string, bool> GetAll()
            => _enabled.ToDictionary(item => item, _ => true, StringComparer.Ordinal);
    }
}
