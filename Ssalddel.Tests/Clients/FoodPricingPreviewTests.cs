using Ssalddel.Contracts.Admin.Restaurants;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class FoodPricingPreviewTests
{
    [Theory]
    [InlineData(-1, 3000, 2500, 500)]
    [InlineData(0, 3000, 2500, 500)]
    [InlineData(1000, 3000, 2500, 500)]
    [InlineData(1001, 3120, 2590, 530)]
    [InlineData(1099, 3120, 2590, 530)]
    [InlineData(1100, 3120, 2590, 530)]
    [InlineData(1101, 3240, 2680, 560)]
    [InlineData(2300, 4560, 3670, 890)]
    public void 기본정책의_포함거리와단위올림및차액을유지한다(int distance, int platform, int driver, int margin)
    {
        var result = 음식요금Preview.Calculate(new(), distance);
        Assert.Equal((decimal)platform, result.PlatformFee);
        Assert.Equal((decimal)driver, result.DriverPayout);
        Assert.Equal((decimal)margin, result.Margin);
    }

    [Theory]
    [InlineData(-100, 0, -1, 0)]
    [InlineData(-100, 0, 1, 1)]
    [InlineData(-100, -5, 2, 2)]
    [InlineData(0, 3, 10, 4)]
    public void 음수포함거리와잘못된단위는_기존보정과올림을유지한다(int included, int unit, int distance, int expectedUnits)
    {
        var policy = new 음식배달요금정책응답
        {
            IncludedDistanceMeters = included, DistanceUnitMeters = unit,
            BaseFee = 0m, MinimumFee = 0m, DistanceUnitFee = 1m,
            DriverBasePayout = 0m, DriverMinimumPayout = 0m, DriverDistanceUnitPayout = 2m
        };
        var result = 음식요금Preview.Calculate(policy, distance);
        Assert.Equal((decimal)expectedUnits, result.PlatformFee);
        Assert.Equal(expectedUnits * 2m, result.DriverPayout);
    }

    [Fact]
    public void 플랫폼과기사최소액은각각적용하고_날씨할증과픽업분리는견적에추가하지않는다()
    {
        var policy = new 음식배달요금정책응답
        {
            BaseFee = 500m, MinimumFee = 4000m,
            DriverBasePayout = 1000m, DriverPickupPayout = 600m, DriverMinimumPayout = 5000m,
            DriverWeatherSurchargeEnabled = true, DriverWeatherSurcharge = 9000m
        };
        var result = 음식요금Preview.Calculate(policy, 1000);
        Assert.Equal(4000m, result.PlatformFee);
        Assert.Equal(5000m, result.DriverPayout);
        Assert.Equal(-1000m, result.Margin);
        Assert.Equal(1000m, policy.DriverBasePayout);
        Assert.Equal(600m, policy.DriverPickupPayout);
        Assert.Equal(9000m, policy.DriverWeatherSurcharge);
    }
}
