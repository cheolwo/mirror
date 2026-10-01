using Hongdal.Contracts.Common.Operations;
using Hongdal.Contracts.Common.PublicData;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Services.Operations;

public interface IOperatingMarketAddressLookupAdapter
{
    string MarketCode { get; }

    string ProviderCode { get; }

    Task<OperatingMarketAddressSearchResult> SearchAsync(
        OperatingMarketAddressSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IOperatingMarketAddressLookupService
{
    Task<OperatingMarketAddressSearchResult> SearchAsync(
        OperatingMarketAddressSearchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class OperatingMarketAddressLookupService : IOperatingMarketAddressLookupService
{
    private readonly IOperatingMarketDeployment _deployment;
    private readonly IOperatingMarketAddressLookupAdapter _adapter;

    public OperatingMarketAddressLookupService(
        IOperatingMarketDeployment deployment,
        IEnumerable<IOperatingMarketAddressLookupAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(adapters);

        _deployment = deployment;
        var registeredAdapters = adapters.ToArray();
        if (registeredAdapters.Length != 1)
        {
            throw new InvalidOperationException(
                $"Operating market {deployment.MarketCode} requires exactly one address adapter; " +
                $"{registeredAdapters.Length} were registered.");
        }

        _adapter = registeredAdapters[0];
        var adapterMarketCode = ResolveAdapterMarketCode(_adapter);
        if (!string.Equals(
                adapterMarketCode,
                deployment.MarketCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Address adapter {_adapter.GetType().Name} targets {adapterMarketCode}, " +
                $"but this server is configured for {deployment.MarketCode}.");
        }
    }

    public Task<OperatingMarketAddressSearchResult> SearchAsync(
        OperatingMarketAddressSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestedMarketCode = string.IsNullOrWhiteSpace(request.MarketCode)
            ? _deployment.MarketCode
            : request.MarketCode;

        if (!OperatingMarketCodes.TryNormalize(requestedMarketCode, out var marketCode))
        {
            return Task.FromResult(Failure(
                requestedMarketCode?.Trim() ?? string.Empty,
                string.Empty,
                OperatingMarketAddressErrorCodes.UnsupportedMarket,
                $"Unsupported operating market: {requestedMarketCode}."));
        }

        if (!string.Equals(
                marketCode,
                _deployment.MarketCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Failure(
                marketCode,
                OperatingMarketProfileCatalog.Get(marketCode).AddressProviderCode,
                OperatingMarketAddressErrorCodes.MarketNotAvailableInDeployment,
                $"Operating market {marketCode} is not available in the " +
                $"{_deployment.MarketCode} deployment."));
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Task.FromResult(Failure(
                marketCode,
                _adapter.ProviderCode,
                OperatingMarketAddressErrorCodes.QueryRequired,
                "An address query is required."));
        }

        return _adapter.SearchAsync(new OperatingMarketAddressSearchRequest
        {
            MarketCode = marketCode,
            Query = request.Query.Trim(),
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 30)
        }, cancellationToken);
    }

    private static OperatingMarketAddressSearchResult Failure(
        string marketCode,
        string providerCode,
        string errorCode,
        string message)
        => new()
        {
            Success = false,
            ProviderConfigured = false,
            MarketCode = marketCode,
            ProviderCode = providerCode,
            ErrorCode = errorCode,
            ErrorMessage = message
        };

    private static string ResolveAdapterMarketCode(IOperatingMarketAddressLookupAdapter adapter)
    {
        if (OperatingMarketCodes.TryNormalize(adapter.MarketCode, out var marketCode))
        {
            return marketCode;
        }

        throw new InvalidOperationException(
            $"Address adapter {adapter.GetType().Name} declares unsupported market {adapter.MarketCode}.");
    }
}

public sealed class KoreaRoadAddressLookupAdapter : IOperatingMarketAddressLookupAdapter
{
    private readonly IRoadAddressLookupService _roadAddressLookupService;
    private readonly bool _providerConfigured;

    public KoreaRoadAddressLookupAdapter(
        IRoadAddressLookupService roadAddressLookupService,
        IOptions<PublicDataOptions> options)
    {
        _roadAddressLookupService = roadAddressLookupService;
        var value = options.Value;
        _providerConfigured = !string.IsNullOrWhiteSpace(value.RoadAddress.ConfirmKey) ||
                              !string.IsNullOrWhiteSpace(value.ServiceKey);
    }

    public string MarketCode => OperatingMarketCodes.Korea;

    public string ProviderCode => OperatingAddressProviderCodes.KoreaRoadNameAddress;

    public async Task<OperatingMarketAddressSearchResult> SearchAsync(
        OperatingMarketAddressSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_providerConfigured)
        {
            return NotConfigured(request);
        }

        var response = await _roadAddressLookupService.SearchAsync(new RoadAddressSearchRequest
        {
            Keyword = request.Query,
            Page = request.Page,
            PageSize = request.PageSize
        }, cancellationToken);

        return new OperatingMarketAddressSearchResult
        {
            Success = response.Success,
            ProviderConfigured = true,
            MarketCode = MarketCode,
            ProviderCode = ProviderCode,
            ErrorCode = response.Success ? null : OperatingMarketAddressErrorCodes.ProviderRequestFailed,
            ErrorMessage = response.ErrorMessage,
            Page = response.Page,
            PageSize = response.PageSize,
            TotalCount = response.TotalCount,
            Items = response.Items.Select(ToCandidate).ToArray()
        };
    }

    private OperatingMarketAddressSearchResult NotConfigured(OperatingMarketAddressSearchRequest request)
        => new()
        {
            Success = false,
            ProviderConfigured = false,
            MarketCode = MarketCode,
            ProviderCode = ProviderCode,
            ErrorCode = OperatingMarketAddressErrorCodes.ProviderNotConfigured,
            ErrorMessage = "The Korea road-name address provider is not configured.",
            Page = request.Page,
            PageSize = request.PageSize
        };

    private OperatingMarketAddressCandidate ToCandidate(RoadAddressItem item)
        => new()
        {
            MarketCode = MarketCode,
            CountryCode = OperatingMarketCodes.Korea,
            FormattedAddress = item.RoadAddress,
            AlternateAddress = string.IsNullOrWhiteSpace(item.EnglishAddress)
                ? item.JibunAddress
                : item.EnglishAddress,
            PostalCode = item.ZipCode,
            AdministrativeAreaCode = item.AdministrativeCode,
            ProviderCode = ProviderCode,
            ProviderReference = FirstNotEmpty(item.BuildingManagementNo, item.RoadNameManagementNo)
        };

    private static string? FirstNotEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

public sealed class UnitedStatesAddressLookupAdapter : IOperatingMarketAddressLookupAdapter
{
    private readonly IUnitedStatesAddressGeocoder _addressGeocoder;

    public UnitedStatesAddressLookupAdapter(IUnitedStatesAddressGeocoder addressGeocoder)
    {
        ArgumentNullException.ThrowIfNull(addressGeocoder);
        _addressGeocoder = addressGeocoder;
    }

    public string MarketCode => OperatingMarketCodes.UnitedStates;

    public string ProviderCode => OperatingAddressProviderCodes.UnitedStatesCensusGeocoder;

    public async Task<OperatingMarketAddressSearchResult> SearchAsync(
        OperatingMarketAddressSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _addressGeocoder.GeocodeAsync(
            request.Query,
            cancellationToken);
        var allItems = response.Items.Select(item => new OperatingMarketAddressCandidate
        {
            MarketCode = MarketCode,
            CountryCode = OperatingMarketCodes.UnitedStates,
            FormattedAddress = item.MatchedAddress,
            PostalCode = item.PostalCode,
            AdministrativeAreaCode = item.StateCode,
            Locality = item.City,
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            MatchPrecisionCode = OperatingAddressMatchPrecisionCodes.AddressRange,
            ProviderCode = ProviderCode,
            ProviderReference = item.TigerLineId,
            ProviderDatasetVersion = response.DatasetVersion,
            ProviderGeographyVintage = response.GeographyVintage,
            GeographicAreas = item.GeographicAreas
        }).ToArray();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 30);
        var skip = (long)(page - 1) * pageSize;
        OperatingMarketAddressCandidate[] items = skip >= allItems.Length
            ? []
            : allItems.Skip((int)skip).Take(pageSize).ToArray();

        return new OperatingMarketAddressSearchResult
        {
            Success = response.Success,
            ProviderConfigured = response.ProviderConfigured,
            MarketCode = MarketCode,
            ProviderCode = ProviderCode,
            ErrorCode = response.Success
                ? null
                : response.ProviderConfigured
                    ? OperatingMarketAddressErrorCodes.ProviderRequestFailed
                    : OperatingMarketAddressErrorCodes.ProviderNotConfigured,
            ErrorMessage = response.ErrorMessage,
            Page = page,
            PageSize = pageSize,
            TotalCount = allItems.Length,
            Items = items
        };
    }
}
