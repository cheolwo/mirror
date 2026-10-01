using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Tests.Contracts.Common.Operations;

public sealed class OperatingMarketProfileCatalogTests
{
    [Fact]
    public void Get_Korea_ReturnsKoreaOperationalDefaults()
    {
        var profile = OperatingMarketProfileCatalog.Get(OperatingMarketCodes.Korea);

        Assert.Equal("KRW", profile.CurrencyCode);
        Assert.Equal(OperatingDistanceUnitCodes.Kilometer, profile.DistanceUnitCode);
        Assert.Equal(OperatingWeightUnitCodes.Kilogram, profile.WeightUnitCode);
        Assert.Equal(OperatingAddressProviderCodes.KoreaRoadNameAddress, profile.AddressProviderCode);
        Assert.Contains("SmartStore", profile.PreferredCommerceChannelCodes);
    }

    [Fact]
    public void Get_UnitedStates_ReturnsUsOperationalDefaults()
    {
        var profile = OperatingMarketProfileCatalog.Get(OperatingMarketCodes.UnitedStates);

        Assert.Equal("USD", profile.CurrencyCode);
        Assert.Equal(OperatingDistanceUnitCodes.Mile, profile.DistanceUnitCode);
        Assert.Equal(OperatingWeightUnitCodes.Pound, profile.WeightUnitCode);
        Assert.Equal(
            OperatingAddressProviderCodes.UnitedStatesCensusGeocoder,
            profile.AddressProviderCode);
        Assert.Equal(OperatingMapProviderCodes.GoogleMaps, profile.MapProviderCode);
        Assert.Equal(
            FreightArrangementModeCodes.UnitedStatesLicensedBrokerPartner,
            profile.FreightArrangementModeCode);
        Assert.Contains("Amazon", profile.PreferredCommerceChannelCodes);
        Assert.DoesNotContain("Coupang", profile.PreferredCommerceChannelCodes);
    }

    [Theory]
    [InlineData("Domestic", OperatingMarketCodes.Korea)]
    [InlineData("Overseas", OperatingMarketCodes.UnitedStates)]
    [InlineData("usa", OperatingMarketCodes.UnitedStates)]
    [InlineData("KOR", OperatingMarketCodes.Korea)]
    public void Normalize_MigratesLegacyAndAliasValues(string source, string expected)
    {
        Assert.Equal(expected, OperatingMarketCodes.Normalize(source));
    }

    [Fact]
    public void TryGet_UnknownMarket_DoesNotSilentlyReportSuccess()
    {
        var found = OperatingMarketProfileCatalog.TryGet("JP", out var profile);

        Assert.False(found);
        Assert.Equal(OperatingMarketCodes.Korea, profile.MarketCode);
    }

    [Fact]
    public void TryNormalize_UnknownMarket_ReturnsFalse()
    {
        var found = OperatingMarketCodes.TryNormalize("JP", out var marketCode);

        Assert.False(found);
        Assert.Equal(OperatingMarketCodes.Korea, marketCode);
    }
}
