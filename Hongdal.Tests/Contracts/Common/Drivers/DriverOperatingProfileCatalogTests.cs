using Hongdal.Contracts.Common.Drivers;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Tests.Contracts.Common.Drivers;

public sealed class DriverOperatingProfileCatalogTests
{
    [Fact]
    public void KoreaProfile_UsesNaverDriverStack()
    {
        var profile = DriverOperatingProfileCatalog.Get(OperatingMarketCodes.Korea);

        Assert.Equal("한국 기사용", profile.DisplayName);
        Assert.True(profile.IsKorea);
        Assert.Equal(OperatingMapProviderCodes.NaverMaps, profile.MapProviderCode);
        Assert.Equal(DriverRoutingProviderCodes.NaverDirections, profile.RoutingProviderCode);
        Assert.Equal(DriverPlaceProviderCodes.KoreaRoadNameAddress, profile.PlaceProviderCode);
    }

    [Fact]
    public void UnitedStatesProfile_UsesGoogleDriverStack()
    {
        var profile = DriverOperatingProfileCatalog.Get(OperatingMarketCodes.UnitedStates);

        Assert.Equal("미국 기사용", profile.DisplayName);
        Assert.True(profile.IsUnitedStates);
        Assert.Equal(OperatingMapProviderCodes.GoogleMaps, profile.MapProviderCode);
        Assert.Equal(DriverRoutingProviderCodes.GoogleRoutes, profile.RoutingProviderCode);
        Assert.Equal(DriverPlaceProviderCodes.GooglePlaces, profile.PlaceProviderCode);
        Assert.Equal(
            OperatingAddressProviderCodes.UnitedStatesCensusGeocoder,
            profile.AddressProviderCode);
    }

    [Theory]
    [InlineData("KR", OperatingMarketCodes.Korea)]
    [InlineData("Korea", OperatingMarketCodes.Korea)]
    [InlineData("US", OperatingMarketCodes.UnitedStates)]
    [InlineData("USA", OperatingMarketCodes.UnitedStates)]
    public void Get_NormalizesSupportedCountryAliases(string source, string expectedMarketCode)
    {
        var profile = DriverOperatingProfileCatalog.Get(source);

        Assert.Equal(expectedMarketCode, profile.MarketCode);
    }
}
