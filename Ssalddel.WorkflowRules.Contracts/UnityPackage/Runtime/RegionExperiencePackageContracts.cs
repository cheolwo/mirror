using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class RegionExperiencePackagePolicy
    {
        public const string SchemaVersion = "region-experience-package.v1";
        public const string CatalogSchemaVersion = "region-experience-package-catalog.v1";
        public const string MyeonmokStationRegionStableId =
            "world-region:kr:seoul:jungnang:myeonmok-station.r1";
        public const string SagajeongRegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1";

        public static bool IsRegionStableId(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.StartsWith("world-region:", StringComparison.Ordinal)
               && value.Length > "world-region:".Length;
    }

    public static class RegionExperiencePackageRoutes
    {
        public const string Catalog = "api/v1/world/regions";
        public const string Manifest = Catalog + "/{regionStableId}/experience-manifest";
    }

    public static class RegionExperiencePackageReleaseChannels
    {
        public const string SteamFreeBaseGame = "SteamFreeBaseGame";
    }

    public static class RegionExperiencePackageAvailabilityCodes
    {
        public const string PrivatePreview = "PrivatePreview";
        public const string Published = "Published";
    }

    public static class RegionExperienceLayerKinds
    {
        public const string Geography = "Geography";
        public const string Mobility = "Mobility";
        public const string LifeContext = "LifeContext";
        public const string RecoveryScenario = "RecoveryScenario";
        public const string Gameplay = "Gameplay";
        public const string CommercialDisplay = "CommercialDisplay";
        public const string OperationalSnapshot = "OperationalSnapshot";
    }

    public static class RegionExperienceLayerAvailabilityCodes
    {
        public const string Ready = "Ready";
        public const string Disabled = "Disabled";
        public const string Planned = "Planned";
    }

    public static class RegionExperienceLayerLoadModes
    {
        public const string Bootstrap = "Bootstrap";
        public const string OnDemand = "OnDemand";
        public const string LiveRefresh = "LiveRefresh";
        public const string NotLoadable = "NotLoadable";
    }

    public static class RegionExperienceLayerCacheModes
    {
        public const string ImmutableByHash = "ImmutableByHash";
        public const string Revalidate = "Revalidate";
        public const string None = "None";
    }

    public sealed class RegionExperienceLayerEndpoint
    {
        public string PurposeCode { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = "GET";
        public string RouteTemplate { get; set; } = string.Empty;
    }

    public sealed class RegionExperienceLayerReference
    {
        public string LayerStableId { get; set; } = string.Empty;
        public string LayerKindCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string AvailabilityCode { get; set; } = RegionExperienceLayerAvailabilityCodes.Planned;
        public string LoadModeCode { get; set; } = RegionExperienceLayerLoadModes.NotLoadable;
        public string CacheModeCode { get; set; } = RegionExperienceLayerCacheModes.None;
        public string RequiredFeatureKey { get; set; } = string.Empty;
        public bool RequiredForRegionOpen { get; set; }
        public bool ChangesOperationalState { get; set; }
        public RegionExperienceLayerEndpoint[] Endpoints { get; set; } =
            Array.Empty<RegionExperienceLayerEndpoint>();
    }

    public sealed class RegionExperiencePackageSummary
    {
        public string RegionStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string PackageRevision { get; set; } = string.Empty;
        public string ManifestHashSha256 { get; set; } = string.Empty;
        public string AvailabilityCode { get; set; } = RegionExperiencePackageAvailabilityCodes.PrivatePreview;
        public string ReleaseChannelCode { get; set; } = RegionExperiencePackageReleaseChannels.SteamFreeBaseGame;
        public bool IsFree { get; set; } = true;
        public bool DistributionApproved { get; set; }
    }

    public sealed class RegionExperiencePackageCatalogResponse
    {
        public string SchemaVersion { get; set; } = RegionExperiencePackagePolicy.CatalogSchemaVersion;
        public string CatalogRevision { get; set; } = string.Empty;
        public RegionExperiencePackageSummary[] Items { get; set; } =
            Array.Empty<RegionExperiencePackageSummary>();
    }

    public sealed class RegionExperiencePackageManifest
    {
        public string SchemaVersion { get; set; } = RegionExperiencePackagePolicy.SchemaVersion;
        public string RegionStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string PackageRevision { get; set; } = string.Empty;
        public string ManifestHashSha256 { get; set; } = string.Empty;
        public string AvailabilityCode { get; set; } = RegionExperiencePackageAvailabilityCodes.PrivatePreview;
        public string ReleaseChannelCode { get; set; } = RegionExperiencePackageReleaseChannels.SteamFreeBaseGame;
        public bool IsFree { get; set; } = true;
        public bool DistributionApproved { get; set; }
        public bool GameplayReady { get; set; }
        public bool AdvertisingEnabled { get; set; }
        public bool OperationalServicesEnabled { get; set; }
        public string SpatialRegistryStableId { get; set; } = string.Empty;
        public string SpatialRegistryRevision { get; set; } = string.Empty;
        public string[] SpatialPackageStableIds { get; set; } = Array.Empty<string>();
        public string[] AdministrativeAreaStableIds { get; set; } = Array.Empty<string>();
        public string[] LegalAreaStableIds { get; set; } = Array.Empty<string>();
        public RegionExperienceLayerReference[] Layers { get; set; } =
            Array.Empty<RegionExperienceLayerReference>();
    }
}
