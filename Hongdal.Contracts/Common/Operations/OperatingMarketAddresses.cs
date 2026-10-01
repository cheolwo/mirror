namespace Hongdal.Contracts.Common.Operations;

public static class OperatingMarketAddressErrorCodes
{
    public const string QueryRequired = "QueryRequired";
    public const string UnsupportedMarket = "UnsupportedMarket";
    public const string MarketNotAvailableInDeployment = "MarketNotAvailableInDeployment";
    public const string ProviderNotConfigured = "ProviderNotConfigured";
    public const string ProviderRequestFailed = "ProviderRequestFailed";
}

public static class OperatingAddressMatchPrecisionCodes
{
    public const string AddressRange = "AddressRange";
    public const string PostalStandardized = "PostalStandardized";
}

public sealed class OperatingMarketAddressSearchRequest
{
    public string? MarketCode { get; init; }

    public string Query { get; init; } = string.Empty;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

public sealed class OperatingMarketAddressCandidate
{
    public string MarketCode { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;

    public string FormattedAddress { get; init; } = string.Empty;

    public string? AlternateAddress { get; init; }

    public string PostalCode { get; init; } = string.Empty;

    public string? AdministrativeAreaCode { get; init; }

    public string? Locality { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? MatchPrecisionCode { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string? ProviderReference { get; init; }

    public string? ProviderDatasetVersion { get; init; }

    public string? ProviderGeographyVintage { get; init; }

    public IReadOnlyList<OperatingMarketGeographicArea> GeographicAreas { get; init; } = [];
}

public sealed class OperatingMarketAddressSearchResult
{
    public bool Success { get; init; }

    public bool ProviderConfigured { get; init; }

    public string MarketCode { get; init; } = string.Empty;

    public string ProviderCode { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int? TotalCount { get; init; }

    public IReadOnlyList<OperatingMarketAddressCandidate> Items { get; init; } = [];
}
