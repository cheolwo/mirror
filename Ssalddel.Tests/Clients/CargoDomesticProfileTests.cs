using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Operations;

namespace Ssalddel.Tests.Clients;

public sealed class CargoDomesticProfileTests
{
    [Theory]
    [InlineData("US")]
    [InlineData("unknown")]
    public void 과거저장지역도_국내지도와운행계약으로복원한다(string previousMarket)
    {
        const string key = "driver.operating_profile.market_code";
        Preferences.Default.Set(key, previousMarket);
        var service = new DriverOperatingProfileService();
        Assert.Equal(DriverOperatingProfileCatalog.Korea, service.Current);
        Assert.Equal(OperatingMarketCodes.Korea, Preferences.Default.Get(key, ""));
        service.SetMarket(OperatingMarketCodes.UnitedStates);
        Assert.True(service.Current.IsKorea);
        Assert.Equal(OperatingMarketCodes.Korea, Preferences.Default.Get(key, ""));
    }
}
