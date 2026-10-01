using Hongdal.Contracts.Common.Operations;
using Microsoft.Extensions.Options;

namespace Hongdal.Services.Operations;

public sealed class UnitedStatesDeliveryScopeOptions
{
    public int StateMinimumParticipantsForPublicDisplay { get; set; } = 1;

    public int CountyMinimumParticipantsForPublicDisplay { get; set; } = 3;

    public int PlaceMinimumParticipantsForPublicDisplay { get; set; } = 5;

    public int ZctaMinimumParticipantsForPublicDisplay { get; set; } = 10;
}

public interface IUnitedStatesDeliveryScopePlanner
{
    OperatingMarketDeliveryScopePlan Build(
        OperatingMarketAddressCandidate address,
        int? participantCount = null);
}

public interface IUnitedStatesDeliveryScopeService
{
    Task<OperatingMarketDeliveryScopePlan> ResolveAsync(
        string address,
        int? participantCount = null,
        CancellationToken cancellationToken = default);
}

public sealed class UnitedStatesDeliveryScopePlanner : IUnitedStatesDeliveryScopePlanner
{
    private readonly UnitedStatesDeliveryScopeOptions _options;

    public UnitedStatesDeliveryScopePlanner(IOptions<UnitedStatesAddressOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.DeliveryScopes;
    }

    public OperatingMarketDeliveryScopePlan Build(
        OperatingMarketAddressCandidate address,
        int? participantCount = null)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (!OperatingMarketCodes.TryNormalize(address.MarketCode, out var marketCode) ||
            marketCode != OperatingMarketCodes.UnitedStates)
        {
            return Failure("A United States operating-market address is required.");
        }

        var state = Find(address, OperatingGeographicAreaTypeCodes.State);
        if (state is null)
        {
            return Failure("The Census state geography is required to create delivery scopes.");
        }

        var county = Find(address, OperatingGeographicAreaTypeCodes.County);
        var place = Find(address, OperatingGeographicAreaTypeCodes.IncorporatedPlace) ??
                    Find(address, OperatingGeographicAreaTypeCodes.CensusDesignatedPlace);
        var zcta = Find(address, OperatingGeographicAreaTypeCodes.ZipCodeTabulationArea);

        var stateScopeKey = BuildScopeKey("state", state.Code);
        var recommendedScopeKey = place is not null
            ? BuildScopeKey("place", place.Code)
            : county is not null
                ? BuildScopeKey("county", county.Code)
                : stateScopeKey;
        var candidates = new List<OperatingMarketDeliveryScopeCandidate>();

        if (place is not null)
        {
            candidates.Add(CreateCandidate(
                place,
                BuildScopeKey("place", place.Code),
                OperatingDeliveryScopeTypeCodes.PlaceRecruitment,
                stateScopeKey,
                recommendedScopeKey,
                _options.PlaceMinimumParticipantsForPublicDisplay,
                participantCount,
                isFineGrained: false,
                OperatingDeliveryScopeLogisticsRoleCodes.UrbanHubConsolidation,
                supportsLastMileBatching: true));
        }

        if (county is not null)
        {
            candidates.Add(CreateCandidate(
                county,
                BuildScopeKey("county", county.Code),
                OperatingDeliveryScopeTypeCodes.CountyRecruitment,
                stateScopeKey,
                recommendedScopeKey,
                _options.CountyMinimumParticipantsForPublicDisplay,
                participantCount,
                isFineGrained: false,
                OperatingDeliveryScopeLogisticsRoleCodes.RuralRouteConsolidation,
                supportsLastMileBatching: true));
        }

        if (zcta is not null)
        {
            candidates.Add(CreateCandidate(
                zcta,
                BuildScopeKey("zcta", zcta.Code),
                OperatingDeliveryScopeTypeCodes.ZctaRecruitment,
                parentScopeKey: null,
                recommendedScopeKey,
                _options.ZctaMinimumParticipantsForPublicDisplay,
                participantCount,
                isFineGrained: true,
                OperatingDeliveryScopeLogisticsRoleCodes.LastMileStopConsolidation,
                supportsLastMileBatching: true));
        }

        candidates.Add(CreateCandidate(
            state,
            stateScopeKey,
            OperatingDeliveryScopeTypeCodes.StateDiscovery,
            parentScopeKey: null,
            recommendedScopeKey,
            _options.StateMinimumParticipantsForPublicDisplay,
            participantCount,
            isFineGrained: false,
            OperatingDeliveryScopeLogisticsRoleCodes.RegionalInboundConsolidation,
            supportsLastMileBatching: false));

        return new OperatingMarketDeliveryScopePlan
        {
            Success = true,
            MarketCode = OperatingMarketCodes.UnitedStates,
            RecommendedScopeKey = recommendedScopeKey,
            RecommendedDemandConsolidationScopeKey = recommendedScopeKey,
            ProviderCode = address.ProviderCode,
            ProviderDatasetVersion = address.ProviderDatasetVersion,
            ProviderGeographyVintage = address.ProviderGeographyVintage,
            Items = candidates
                .OrderByDescending(candidate => candidate.IsRecommendedRecruitmentScope)
                .ToArray()
        };
    }

    private static OperatingMarketDeliveryScopeCandidate CreateCandidate(
        OperatingMarketGeographicArea area,
        string scopeKey,
        string scopeTypeCode,
        string? parentScopeKey,
        string recommendedScopeKey,
        int minimumParticipants,
        int? participantCount,
        bool isFineGrained,
        string logisticsRoleCode,
        bool supportsLastMileBatching)
    {
        var normalizedMinimum = Math.Max(1, minimumParticipants);
        return new OperatingMarketDeliveryScopeCandidate
        {
            MarketCode = OperatingMarketCodes.UnitedStates,
            ScopeKey = scopeKey,
            ScopeTypeCode = scopeTypeCode,
            LogisticsRoleCode = logisticsRoleCode,
            GeographicAreaTypeCode = area.AreaTypeCode,
            GeographicAreaCode = area.Code,
            DisplayName = area.Name,
            ParentScopeKey = parentScopeKey,
            IsRecommendedRecruitmentScope = string.Equals(
                scopeKey,
                recommendedScopeKey,
                StringComparison.OrdinalIgnoreCase),
            IsRecommendedDemandConsolidationScope = string.Equals(
                scopeKey,
                recommendedScopeKey,
                StringComparison.OrdinalIgnoreCase),
            IsFineGrained = isFineGrained,
            MinimumParticipantsForPublicDisplay = normalizedMinimum,
            CanPublishForParticipantCount = participantCount >= normalizedMinimum,
            SupportsLastMileBatching = supportsLastMileBatching,
            RequiresLogisticsFeasibilityValidation = true,
            RequiresOperationalRouteValidation = true
        };
    }

    private static OperatingMarketGeographicArea? Find(
        OperatingMarketAddressCandidate address,
        string areaTypeCode)
        => address.GeographicAreas.FirstOrDefault(area => string.Equals(
            area.AreaTypeCode,
            areaTypeCode,
            StringComparison.OrdinalIgnoreCase));

    private static string BuildScopeKey(string scopeType, string geographicCode)
        => $"us-{scopeType}:{geographicCode.Trim().ToLowerInvariant()}";

    private static OperatingMarketDeliveryScopePlan Failure(string message)
        => new()
        {
            Success = false,
            MarketCode = OperatingMarketCodes.UnitedStates,
            ErrorMessage = message
        };
}

public sealed class UnitedStatesDeliveryScopeService : IUnitedStatesDeliveryScopeService
{
    private readonly IOperatingMarketAddressLookupService _addressLookupService;
    private readonly IUnitedStatesDeliveryScopePlanner _scopePlanner;

    public UnitedStatesDeliveryScopeService(
        IOperatingMarketAddressLookupService addressLookupService,
        IUnitedStatesDeliveryScopePlanner scopePlanner)
    {
        ArgumentNullException.ThrowIfNull(addressLookupService);
        ArgumentNullException.ThrowIfNull(scopePlanner);
        _addressLookupService = addressLookupService;
        _scopePlanner = scopePlanner;
    }

    public async Task<OperatingMarketDeliveryScopePlan> ResolveAsync(
        string address,
        int? participantCount = null,
        CancellationToken cancellationToken = default)
    {
        var lookup = await _addressLookupService.SearchAsync(
            new OperatingMarketAddressSearchRequest
            {
                MarketCode = OperatingMarketCodes.UnitedStates,
                Query = address,
                Page = 1,
                PageSize = 1
            },
            cancellationToken);
        if (!lookup.Success)
        {
            return Failure("The United States delivery-scope address lookup failed.");
        }

        var candidate = lookup.Items.FirstOrDefault();
        return candidate is null
            ? Failure("No Census address match was found for delivery-scope planning.")
            : _scopePlanner.Build(candidate, participantCount);
    }

    private static OperatingMarketDeliveryScopePlan Failure(string message)
        => new()
        {
            Success = false,
            MarketCode = OperatingMarketCodes.UnitedStates,
            ErrorMessage = message
        };
}
