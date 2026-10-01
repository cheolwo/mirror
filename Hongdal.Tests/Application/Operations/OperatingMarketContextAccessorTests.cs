using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;

namespace Hongdal.Tests.Application.Operations;

public sealed class OperatingMarketContextAccessorTests
{
    [Theory]
    [InlineData(OperatingMarketCodes.Korea)]
    [InlineData(OperatingMarketCodes.UnitedStates)]
    public void Current_UsesDeploymentMarket(string marketCode)
    {
        var accessor = new DeploymentOperatingMarketContextAccessor(
            new OperatingMarketDeployment(marketCode));

        Assert.Equal(marketCode, accessor.Current.MarketCode);
        Assert.Equal(
            OperatingMarketContextSourceCodes.Deployment,
            accessor.Current.SourceCode);
        Assert.Equal(marketCode, accessor.Current.Profile.MarketCode);
        Assert.Equal(
            marketCode == OperatingMarketCodes.Korea
                ? OperatingTimeZoneIds.Korea
                : OperatingTimeZoneIds.CoordinatedUniversal,
            accessor.Current.TimeZoneId);
    }
}
