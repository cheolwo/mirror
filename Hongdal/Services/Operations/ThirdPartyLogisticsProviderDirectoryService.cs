using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public interface IThirdPartyLogisticsProviderDirectoryService
{
    ThirdPartyLogisticsProviderDirectoryResponse Search(
        ThirdPartyLogisticsProviderDirectoryQuery query);

    CollectivePurchaseLogisticsDirectoryResponse SearchForCollectivePurchase(
        CollectivePurchaseLogisticsDirectoryQuery query);

    BondedToDoorLogisticsDirectoryResponse SearchBondedToDoor(
        BondedToDoorLogisticsDirectoryQuery query);
}

public sealed class UnitedStatesThirdPartyLogisticsProviderDirectoryService
    : IThirdPartyLogisticsProviderDirectoryService
{
    public ThirdPartyLogisticsProviderDirectoryResponse Search(
        ThirdPartyLogisticsProviderDirectoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var searchText = NullIfWhiteSpace(query.SearchText);
        var capabilityCode = NullIfWhiteSpace(query.CapabilityCode);
        var segmentCode = NullIfWhiteSpace(query.SegmentCode);

        var filtered = UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
            .Where(item => MatchesSearch(item, searchText))
            .Where(item => HasCode(item.CapabilityCodes, capabilityCode))
            .Where(item => HasCode(item.SegmentCodes, segmentCode))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var skip = (long)(page - 1) * pageSize;
        var items = skip >= filtered.LongLength
            ? Array.Empty<ThirdPartyLogisticsProviderDirectoryItem>()
            : filtered.Skip((int)skip).Take(pageSize).ToArray();

        return new ThirdPartyLogisticsProviderDirectoryResponse
        {
            Success = true,
            MarketCode = OperatingMarketCodes.UnitedStates,
            CatalogVersion = UnitedStatesThirdPartyLogisticsProviderCatalog.CatalogVersion,
            SnapshotReviewedOn =
                UnitedStatesThirdPartyLogisticsProviderCatalog.SnapshotReviewedOn,
            Page = page,
            PageSize = pageSize,
            TotalCount = filtered.Length,
            AvailableCapabilityCodes = DistinctCodes(
                UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
                    .SelectMany(item => item.CapabilityCodes)),
            AvailableSegmentCodes = DistinctCodes(
                UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
                    .SelectMany(item => item.SegmentCodes)),
            RegulatoryVerificationResources =
                UnitedStatesThirdPartyLogisticsProviderCatalog
                    .RegulatoryVerificationResources,
            Items = Array.AsReadOnly(items)
        };
    }

    public CollectivePurchaseLogisticsDirectoryResponse SearchForCollectivePurchase(
        CollectivePurchaseLogisticsDirectoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var searchText = NullIfWhiteSpace(query.SearchText);
        var stageCode = NullIfWhiteSpace(query.StageCode);
        var productHandlingCode = NullIfWhiteSpace(query.ProductHandlingCode);
        var engagementSignalCode = NullIfWhiteSpace(query.EngagementSignalCode);
        var providersByKey = UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
            .ToDictionary(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase);

        var filtered = UnitedStatesCollectivePurchaseLogisticsCatalog.Profiles
            .Where(profile => providersByKey.ContainsKey(profile.ProviderKey))
            .Select(profile => new CollectivePurchaseLogisticsProviderCandidate
            {
                Provider = providersByKey[profile.ProviderKey],
                CollectivePurchaseProfile = profile
            })
            .Where(item => MatchesCollectivePurchaseSearch(item, searchText))
            .Where(item => HasCode(
                item.CollectivePurchaseProfile.StageCodes,
                stageCode))
            .Where(item => HasCode(
                item.CollectivePurchaseProfile.ProductHandlingCodes,
                productHandlingCode))
            .Where(item => HasCode(
                item.CollectivePurchaseProfile.EngagementSignalCodes,
                engagementSignalCode))
            .OrderBy(item => item.Provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var skip = (long)(page - 1) * pageSize;
        var items = skip >= filtered.LongLength
            ? Array.Empty<CollectivePurchaseLogisticsProviderCandidate>()
            : filtered.Skip((int)skip).Take(pageSize).ToArray();

        return new CollectivePurchaseLogisticsDirectoryResponse
        {
            Success = true,
            MarketCode = OperatingMarketCodes.UnitedStates,
            CatalogVersion = UnitedStatesCollectivePurchaseLogisticsCatalog.CatalogVersion,
            SnapshotReviewedOn =
                UnitedStatesCollectivePurchaseLogisticsCatalog.SnapshotReviewedOn,
            Page = page,
            PageSize = pageSize,
            TotalCount = filtered.Length,
            AvailableStageCodes = DistinctCodes(
                UnitedStatesCollectivePurchaseLogisticsCatalog.Profiles
                    .SelectMany(item => item.StageCodes)),
            AvailableProductHandlingCodes = DistinctCodes(
                UnitedStatesCollectivePurchaseLogisticsCatalog.Profiles
                    .SelectMany(item => item.ProductHandlingCodes)),
            AvailableEngagementSignalCodes = DistinctCodes(
                UnitedStatesCollectivePurchaseLogisticsCatalog.Profiles
                    .SelectMany(item => item.EngagementSignalCodes)),
            RequiredQuoteInputCodes =
                UnitedStatesCollectivePurchaseLogisticsCatalog.RequiredQuoteInputCodes,
            RegulatoryVerificationResources =
                UnitedStatesThirdPartyLogisticsProviderCatalog
                    .RegulatoryVerificationResources,
            Items = Array.AsReadOnly(items)
        };
    }

    public BondedToDoorLogisticsDirectoryResponse SearchBondedToDoor(
        BondedToDoorLogisticsDirectoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var searchText = NullIfWhiteSpace(query.SearchText);
        var stageCode = NullIfWhiteSpace(query.StageCode);
        var storageModelCode = NullIfWhiteSpace(query.StorageModelCode);
        var stateCode = NullIfWhiteSpace(query.StateCode);
        var providersByKey = UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
            .ToDictionary(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase);

        var filtered = UnitedStatesBondedToDoorLogisticsCatalog.Profiles
            .Where(profile => providersByKey.ContainsKey(profile.ProviderKey))
            .Select(profile => new BondedToDoorLogisticsProviderCandidate
            {
                Provider = providersByKey[profile.ProviderKey],
                BondedToDoorProfile = profile
            })
            .Where(item => MatchesBondedToDoorSearch(item, searchText))
            .Where(item => HasCode(item.BondedToDoorProfile.StageCodes, stageCode))
            .Where(item => HasCode(
                item.BondedToDoorProfile.StorageModelCodes,
                storageModelCode))
            .Where(item => stateCode is null || item.BondedToDoorProfile
                .FacilityClaims.Any(facility => string.Equals(
                    facility.StateCode,
                    stateCode,
                    StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var skip = (long)(page - 1) * pageSize;
        var items = skip >= filtered.LongLength
            ? Array.Empty<BondedToDoorLogisticsProviderCandidate>()
            : filtered.Skip((int)skip).Take(pageSize).ToArray();

        return new BondedToDoorLogisticsDirectoryResponse
        {
            Success = true,
            MarketCode = OperatingMarketCodes.UnitedStates,
            CatalogVersion = UnitedStatesBondedToDoorLogisticsCatalog.CatalogVersion,
            SnapshotReviewedOn =
                UnitedStatesBondedToDoorLogisticsCatalog.SnapshotReviewedOn,
            Page = page,
            PageSize = pageSize,
            TotalCount = filtered.Length,
            AvailableStageCodes = DistinctCodes(
                UnitedStatesBondedToDoorLogisticsCatalog.Profiles
                    .SelectMany(item => item.StageCodes)),
            AvailableStorageModelCodes = DistinctCodes(
                UnitedStatesBondedToDoorLogisticsCatalog.Profiles
                    .SelectMany(item => item.StorageModelCodes)),
            AvailableStateCodes = DistinctCodes(
                UnitedStatesBondedToDoorLogisticsCatalog.Profiles
                    .SelectMany(item => item.FacilityClaims)
                    .Select(item => item.StateCode)),
            UniversalRoleRequirementCodes =
                UnitedStatesBondedToDoorLogisticsCatalog
                    .UniversalRoleRequirementCodes,
            RegulatoryVerificationResources =
                UnitedStatesThirdPartyLogisticsProviderCatalog
                    .RegulatoryVerificationResources,
            Items = Array.AsReadOnly(items)
        };
    }

    private static bool MatchesSearch(
        ThirdPartyLogisticsProviderDirectoryItem item,
        string? searchText)
        => searchText is null
           || item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || item.ProviderKey.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || item.OfficialWebsiteUrl.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || item.CapabilityCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
           || item.SegmentCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesCollectivePurchaseSearch(
        CollectivePurchaseLogisticsProviderCandidate item,
        string? searchText)
        => searchText is null
           || MatchesSearch(item.Provider, searchText)
           || item.CollectivePurchaseProfile.StageCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
           || item.CollectivePurchaseProfile.ProductHandlingCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
           || item.CollectivePurchaseProfile.EngagementSignalCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
           || item.CollectivePurchaseProfile.ExplicitRestrictionCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesBondedToDoorSearch(
        BondedToDoorLogisticsProviderCandidate item,
        string? searchText)
        => searchText is null
           || MatchesSearch(item.Provider, searchText)
           || item.BondedToDoorProfile.StageCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
           || item.BondedToDoorProfile.StorageModelCodes.Any(code =>
               code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
           || item.BondedToDoorProfile.FacilityClaims.Any(facility =>
               facility.DisplayName.Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase)
               || facility.City.Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase)
               || facility.StateCode.Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase)
               || (facility.FirmsCode?.Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase) ?? false));

    private static bool HasCode(IReadOnlyList<string> codes, string? requiredCode)
        => requiredCode is null
           || codes.Contains(requiredCode, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> DistinctCodes(IEnumerable<string> codes)
        => Array.AsReadOnly(codes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UnavailableThirdPartyLogisticsProviderDirectoryService
    : IThirdPartyLogisticsProviderDirectoryService
{
    private readonly IOperatingMarketDeployment _deployment;

    public UnavailableThirdPartyLogisticsProviderDirectoryService(
        IOperatingMarketDeployment deployment)
    {
        _deployment = deployment;
    }

    public ThirdPartyLogisticsProviderDirectoryResponse Search(
        ThirdPartyLogisticsProviderDirectoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new ThirdPartyLogisticsProviderDirectoryResponse
        {
            Success = false,
            MarketCode = _deployment.MarketCode,
            ErrorCode = ThirdPartyLogisticsProviderDirectoryErrorCodes
                .MarketNotAvailableInDeployment,
            ErrorMessage =
                "The United States 3PL candidate directory is not available in this deployment market.",
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };
    }

    public CollectivePurchaseLogisticsDirectoryResponse SearchForCollectivePurchase(
        CollectivePurchaseLogisticsDirectoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectivePurchaseLogisticsDirectoryResponse
        {
            Success = false,
            MarketCode = _deployment.MarketCode,
            ErrorCode = ThirdPartyLogisticsProviderDirectoryErrorCodes
                .MarketNotAvailableInDeployment,
            ErrorMessage =
                "The United States collective-purchase logistics candidate directory is not available in this deployment market.",
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };
    }

    public BondedToDoorLogisticsDirectoryResponse SearchBondedToDoor(
        BondedToDoorLogisticsDirectoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new BondedToDoorLogisticsDirectoryResponse
        {
            Success = false,
            MarketCode = _deployment.MarketCode,
            ErrorCode = ThirdPartyLogisticsProviderDirectoryErrorCodes
                .MarketNotAvailableInDeployment,
            ErrorMessage =
                "The United States bonded-to-door logistics candidate directory is not available in this deployment market.",
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };
    }
}
