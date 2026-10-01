using Hongdal.Contracts.Common.Sales;

namespace Hongdal.Contracts.Common.Operations;

public static class OperatingMarketCodes
{
    public const string Korea = "KR";
    public const string UnitedStates = "US";

    public static readonly IReadOnlyList<string> All = [Korea, UnitedStates];

    public static string Normalize(string? marketCode, string fallback = Korea)
    {
        var normalizedFallback = TryNormalize(fallback, out var fallbackCode) ? fallbackCode : Korea;
        return TryNormalize(marketCode, out var normalizedCode) ? normalizedCode : normalizedFallback;
    }

    public static bool TryNormalize(string? marketCode, out string normalizedCode)
    {
        normalizedCode = Korea;
        if (string.IsNullOrWhiteSpace(marketCode))
        {
            return false;
        }

        normalizedCode = marketCode.Trim().ToUpperInvariant() switch
        {
            "KR" or "KOR" or "KOREA" or "SOUTHKOREA" or "DOMESTIC" => Korea,
            "US" or "USA" or "UNITEDSTATES" or "UNITEDSTATESOFAMERICA" or "OVERSEAS" => UnitedStates,
            _ => string.Empty
        };

        if (normalizedCode.Length > 0)
        {
            return true;
        }

        normalizedCode = Korea;
        return false;
    }

    public static bool IsSupported(string? marketCode)
        => TryNormalize(marketCode, out _);
}

public static class OperatingDistanceUnitCodes
{
    public const string Kilometer = "km";
    public const string Mile = "mi";
}

public static class OperatingWeightUnitCodes
{
    public const string Kilogram = "kg";
    public const string Pound = "lb";
}

public static class OperatingTimeZoneIds
{
    public const string Korea = "Asia/Seoul";
    public const string CoordinatedUniversal = "UTC";
}

public static class OperatingAddressFormatCodes
{
    public const string KoreaRoadName = "KoreaRoadName";
    public const string UnitedStatesStreet = "UnitedStatesStreet";
}

public static class OperatingAddressProviderCodes
{
    public const string KoreaRoadNameAddress = "KoreaRoadNameAddress";
    public const string UnitedStatesCensusGeocoder = "UnitedStatesCensusGeocoder";
    public const string UnitedStatesPostalService = "UnitedStatesPostalService";
    public const string GoogleAddressValidation = "GoogleAddressValidation";
}

public static class OperatingMapProviderCodes
{
    public const string NaverMaps = "NaverMaps";
    public const string GoogleMaps = "GoogleMaps";
}

public static class FreightArrangementModeCodes
{
    public const string KoreaDomesticTransport = "KoreaDomesticTransport";
    public const string UnitedStatesLicensedBrokerPartner = "UnitedStatesLicensedBrokerPartner";
}

public sealed record OperatingMarketProfile(
    string MarketCode,
    string CountryCode,
    string CurrencyCode,
    string FormattingCultureName,
    string DistanceUnitCode,
    string WeightUnitCode,
    string AddressFormatCode,
    string AddressProviderCode,
    string MapProviderCode,
    string FreightArrangementModeCode,
    IReadOnlyList<string> PreferredCommerceChannelCodes)
{
    // Retained for callers compiled against the first operating-market contract.
    public string CultureName => FormattingCultureName;
}

public static class OperatingMarketProfileCatalog
{
    private static readonly IReadOnlyDictionary<string, OperatingMarketProfile> Profiles =
        new Dictionary<string, OperatingMarketProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [OperatingMarketCodes.Korea] = new(
                OperatingMarketCodes.Korea,
                "KR",
                "KRW",
                "ko-KR",
                OperatingDistanceUnitCodes.Kilometer,
                OperatingWeightUnitCodes.Kilogram,
                OperatingAddressFormatCodes.KoreaRoadName,
                OperatingAddressProviderCodes.KoreaRoadNameAddress,
                OperatingMapProviderCodes.NaverMaps,
                FreightArrangementModeCodes.KoreaDomesticTransport,
                [CommerceChannelKeys.SmartStore, CommerceChannelKeys.Coupang, CommerceChannelKeys.ElevenStreet]),
            [OperatingMarketCodes.UnitedStates] = new(
                OperatingMarketCodes.UnitedStates,
                "US",
                "USD",
                "en-US",
                OperatingDistanceUnitCodes.Mile,
                OperatingWeightUnitCodes.Pound,
                OperatingAddressFormatCodes.UnitedStatesStreet,
                OperatingAddressProviderCodes.UnitedStatesCensusGeocoder,
                OperatingMapProviderCodes.GoogleMaps,
                FreightArrangementModeCodes.UnitedStatesLicensedBrokerPartner,
                [
                    CommerceChannelKeys.Amazon,
                    CommerceChannelKeys.Ebay,
                    CommerceChannelKeys.Shopify,
                    CommerceChannelKeys.Walmart,
                    CommerceChannelKeys.Etsy,
                    CommerceChannelKeys.TikTokShop
                ])
        };

    public static IReadOnlyList<OperatingMarketProfile> All { get; } = OperatingMarketCodes.All
        .Select(code => Profiles[code])
        .ToArray();

    public static OperatingMarketProfile Get(string? marketCode)
        => Profiles[OperatingMarketCodes.Normalize(marketCode)];

    public static bool TryGet(string? marketCode, out OperatingMarketProfile profile)
    {
        if (!OperatingMarketCodes.TryNormalize(marketCode, out var normalizedCode))
        {
            profile = Profiles[OperatingMarketCodes.Korea];
            return false;
        }

        profile = Profiles[normalizedCode];
        return true;
    }
}
