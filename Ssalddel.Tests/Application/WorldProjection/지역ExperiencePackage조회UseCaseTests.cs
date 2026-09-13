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
    public async Task 기능이꺼진레이어는_주소를노출하지않고_활성화된레이어만기존API를참조한다()
    {
        var closed = await Create().ManifestAsync(
            RegionExperiencePackagePolicy.SagajeongRegionStableId,
            CancellationToken.None);
        var opened = await Create(
                VersionFeatureFlagKeys.AdministrativeDongDioramaObservation,
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
        var operations = Assert.Single(opened.Layers, layer => layer.LayerKindCode == RegionExperienceLayerKinds.OperationalSnapshot);
        Assert.Equal(RegionExperienceLayerAvailabilityCodes.Ready, operations.AvailabilityCode);
        Assert.Contains(operations.Endpoints, endpoint => endpoint.RouteTemplate == OperationalWorldSceneRoutes.AreaSnapshot);
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
        Assert.Equal(firstManifest.ManifestHashSha256, Assert.Single(firstCatalog.Items).ManifestHashSha256);
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
