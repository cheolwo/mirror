using System.Globalization;
using System.Text.Json;
using Hongdal.Contracts.Common.Operations;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Hongdal.Services.Operations;

public sealed class UnitedStatesAddressOptions
{
    public const string SectionName = "UnitedStatesAddress";

    public UnitedStatesCensusGeocoderOptions CensusGeocoder { get; set; } = new();

    public UnitedStatesDeliveryScopeOptions DeliveryScopes { get; set; } = new();
}

public sealed class UnitedStatesCensusGeocoderOptions
{
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://geocoding.geo.census.gov";

    public string GeographiesOneLineAddressPath { get; set; } =
        "/geocoder/geographies/onelineaddress";

    public string Benchmark { get; set; } = "Public_AR_Current";

    public string Vintage { get; set; } = "Current_Current";

    public string Layers { get; set; } = "2,28,30,80,82";

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxAddressLength { get; set; } = 500;
}

public interface IUnitedStatesAddressGeocoder
{
    Task<UnitedStatesAddressGeocodeResult> GeocodeAsync(
        string address,
        CancellationToken cancellationToken = default);
}

public sealed class UnitedStatesAddressGeocodeCandidate
{
    public string MatchedAddress { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string StateCode { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? TigerLineId { get; init; }

    public IReadOnlyList<OperatingMarketGeographicArea> GeographicAreas { get; init; } = [];
}

public sealed class UnitedStatesAddressGeocodeResult
{
    public bool Success { get; init; }

    public bool ProviderConfigured { get; init; }

    public string? ErrorMessage { get; init; }

    public string? DatasetVersion { get; init; }

    public string? GeographyVintage { get; init; }

    public IReadOnlyList<UnitedStatesAddressGeocodeCandidate> Items { get; init; } = [];
}

public sealed class UnitedStatesCensusAddressGeocoder : IUnitedStatesAddressGeocoder
{
    private readonly HttpClient _httpClient;
    private readonly UnitedStatesCensusGeocoderOptions _options;

    public UnitedStatesCensusAddressGeocoder(
        HttpClient httpClient,
        IOptions<UnitedStatesAddressOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value.CensusGeocoder;
    }

    public async Task<UnitedStatesAddressGeocodeResult> GeocodeAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return Failure(
                providerConfigured: false,
                "The United States Census Geocoder is disabled or not configured.");
        }

        var normalizedAddress = address?.Trim() ?? string.Empty;
        if (normalizedAddress.Length == 0)
        {
            return Failure(providerConfigured: true, "A United States address is required.");
        }

        if (normalizedAddress.Length > Math.Max(1, _options.MaxAddressLength))
        {
            return Failure(providerConfigured: true, "The United States address is too long.");
        }

        var query = new Dictionary<string, string?>
        {
            ["address"] = normalizedAddress,
            ["benchmark"] = _options.Benchmark,
            ["vintage"] = _options.Vintage,
            ["layers"] = _options.Layers,
            ["format"] = "json"
        };
        var relativeUri = QueryHelpers.AddQueryString(
            _options.GeographiesOneLineAddressPath.TrimStart('/'),
            query);

        try
        {
            using var response = await _httpClient.GetAsync(relativeUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Failure(
                    providerConfigured: true,
                    $"The United States Census Geocoder returned HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            return Parse(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(providerConfigured: true, "The United States Census Geocoder timed out.");
        }
        catch (HttpRequestException)
        {
            return Failure(providerConfigured: true, "The United States Census Geocoder request failed.");
        }
        catch (JsonException)
        {
            return Failure(providerConfigured: true, "The United States Census Geocoder returned an invalid response.");
        }
    }

    private UnitedStatesAddressGeocodeResult Parse(JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The Census Geocoder result object is missing.");
        }

        var datasetVersion = ReadDatasetVersion(result);
        var geographyVintage = ReadGeographyVintage(result);
        if (!result.TryGetProperty("addressMatches", out var matches) ||
            matches.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The Census Geocoder addressMatches array is missing.");
        }

        var items = matches
            .EnumerateArray()
            .Select(ParseCandidate)
            .Where(candidate => candidate is not null)
            .Cast<UnitedStatesAddressGeocodeCandidate>()
            .ToArray();

        return new UnitedStatesAddressGeocodeResult
        {
            Success = true,
            ProviderConfigured = true,
            DatasetVersion = datasetVersion,
            GeographyVintage = geographyVintage,
            Items = items
        };
    }

    private static UnitedStatesAddressGeocodeCandidate? ParseCandidate(JsonElement match)
    {
        var matchedAddress = ReadString(match, "matchedAddress");
        if (string.IsNullOrWhiteSpace(matchedAddress))
        {
            return null;
        }

        var components = ReadObject(match, "addressComponents");
        var coordinates = ReadObject(match, "coordinates");
        var tigerLine = ReadObject(match, "tigerLine");

        return new UnitedStatesAddressGeocodeCandidate
        {
            MatchedAddress = matchedAddress,
            City = ReadString(components, "city") ?? string.Empty,
            StateCode = ReadString(components, "state") ?? string.Empty,
            PostalCode = ReadString(components, "zip") ?? string.Empty,
            Longitude = ReadDouble(coordinates, "x"),
            Latitude = ReadDouble(coordinates, "y"),
            TigerLineId = ReadString(tigerLine, "tigerLineId"),
            GeographicAreas = ParseGeographicAreas(match)
        };
    }

    private static IReadOnlyList<OperatingMarketGeographicArea> ParseGeographicAreas(
        JsonElement match)
    {
        var geographies = ReadObject(match, "geographies");
        if (geographies.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var areas = new List<OperatingMarketGeographicArea>();
        foreach (var layer in geographies.EnumerateObject())
        {
            var areaTypeCode = ResolveAreaTypeCode(layer.Name);
            if (areaTypeCode is null || layer.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var value in layer.Value.EnumerateArray())
            {
                var code = ReadString(value, "GEOID") ??
                           ReadString(value, "ZCTA5");
                var name = areaTypeCode == OperatingGeographicAreaTypeCodes.ZipCodeTabulationArea
                    ? ReadString(value, "BASENAME") ?? ReadString(value, "NAME")
                    : ReadString(value, "NAME") ?? ReadString(value, "BASENAME");
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                areas.Add(new OperatingMarketGeographicArea
                {
                    AreaTypeCode = areaTypeCode,
                    Code = code,
                    Name = name
                });
            }
        }

        return areas
            .DistinctBy(area => (area.AreaTypeCode, area.Code))
            .ToArray();
    }

    private static string? ResolveAreaTypeCode(string layerName)
        => layerName switch
        {
            "States" => OperatingGeographicAreaTypeCodes.State,
            "Counties" => OperatingGeographicAreaTypeCodes.County,
            "Incorporated Places" => OperatingGeographicAreaTypeCodes.IncorporatedPlace,
            "Census Designated Places" =>
                OperatingGeographicAreaTypeCodes.CensusDesignatedPlace,
            _ when layerName.Contains(
                    "ZIP Code Tabulation Areas",
                    StringComparison.OrdinalIgnoreCase) =>
                OperatingGeographicAreaTypeCodes.ZipCodeTabulationArea,
            _ => null
        };

    private string? ReadDatasetVersion(JsonElement result)
    {
        var input = ReadObject(result, "input");
        var benchmark = ReadObject(input, "benchmark");
        return ReadString(benchmark, "benchmarkName") ?? _options.Benchmark;
    }

    private string? ReadGeographyVintage(JsonElement result)
    {
        var input = ReadObject(result, "input");
        var vintage = ReadObject(input, "vintage");
        return ReadString(vintage, "vintageName") ?? _options.Vintage;
    }

    private bool IsConfigured()
        => _options.Enabled &&
           _httpClient.BaseAddress is not null &&
           !string.IsNullOrWhiteSpace(_options.GeographiesOneLineAddressPath) &&
           !string.IsNullOrWhiteSpace(_options.Benchmark) &&
           !string.IsNullOrWhiteSpace(_options.Vintage) &&
           !string.IsNullOrWhiteSpace(_options.Layers);

    private static JsonElement ReadObject(JsonElement parent, string propertyName)
        => parent.ValueKind == JsonValueKind.Object &&
           parent.TryGetProperty(propertyName, out var value) &&
           value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static double? ReadDouble(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               double.TryParse(
                   value.GetString(),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out number)
            ? number
            : null;
    }

    private static UnitedStatesAddressGeocodeResult Failure(
        bool providerConfigured,
        string message)
        => new()
        {
            Success = false,
            ProviderConfigured = providerConfigured,
            ErrorMessage = message
        };
}
