using System.Text.Json;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Fulfillment;

namespace Ssalddel.Tests.Services.Community;

public sealed class DeliveryCostSharingCalculatorTests
{
    [Theory]
    [InlineData(DeliveryCostSharingModes.CustomerPays, null, null, null, 6000, 0, 6000)]
    [InlineData(DeliveryCostSharingModes.FixedContribution, 1500, null, null, 6000, 1500, 4500)]
    [InlineData(DeliveryCostSharingModes.FixedContribution, 9000, null, null, 6000, 6000, 0)]
    [InlineData(DeliveryCostSharingModes.PercentageShare, null, 50, null, 6001, 3000, 3001)]
    [InlineData(DeliveryCostSharingModes.PercentageShare, null, 50, 2000, 6001, 2000, 4001)]
    [InlineData(DeliveryCostSharingModes.PercentageShare, null, 0, null, 6000, 0, 6000)]
    [InlineData(DeliveryCostSharingModes.PercentageShare, null, 100, null, 6000, 6000, 0)]
    [InlineData(DeliveryCostSharingModes.RestaurantPays, null, null, null, 6000, 6000, 0)]
    [InlineData(DeliveryCostSharingModes.RestaurantPays, null, null, 6000, 6000, 6000, 0)]
    public void 주문전체비용을분담하고원미만은식당내림고객잔액(string mode, int? fixedAmount, int? percent, int? cap,
        int total, int restaurant, int customer)
    {
        var result = DeliveryCostSharingCalculator.Calculate(total, new()
        { Mode = mode, FixedAmountKrw = fixedAmount, RestaurantSharePercent = percent, MaximumRestaurantChargeKrw = cap });
        Assert.True(result.IsAllowed); Assert.Equal((decimal)restaurant, result.RestaurantChargeKrw);
        Assert.Equal((decimal)customer, result.CustomerChargeKrw); Assert.Equal(total, result.RestaurantChargeKrw + result.CustomerChargeKrw);
    }

    [Fact]
    public void 음식점전액의한도초과는고객에게전가하지않고양쪽확정을멈춘다()
    {
        var result = DeliveryCostSharingCalculator.Calculate(6001, new() { Mode = DeliveryCostSharingModes.RestaurantPays, MaximumRestaurantChargeKrw = 6000 });
        Assert.False(result.IsAllowed); Assert.Equal("RestaurantChargeLimitExceeded", result.ReasonCode);
        Assert.Null(result.RestaurantChargeKrw); Assert.Null(result.CustomerChargeKrw);
    }

    [Theory]
    [MemberData(nameof(InvalidPolicies))]
    public void 모드에맞지않는누락혼합범위를거부한다(DeliveryCostSharingPolicy policy)
    {
        Assert.NotNull(DeliveryCostSharingCalculator.ValidationError(policy));
        Assert.False(DeliveryCostSharingCalculator.Calculate(6000, policy).IsAllowed);
    }

    public static IEnumerable<object[]> InvalidPolicies()
    {
        yield return [new DeliveryCostSharingPolicy()];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.FixedContribution }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = -1 }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = 1.5m }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = 1000001 }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = 1000, MaximumRestaurantChargeKrw = 2000 }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.CustomerPays, FixedAmountKrw = 0 }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.PercentageShare }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 100.01m }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 0.001m }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 25, FixedAmountKrw = 10 }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.RestaurantPays, MaximumRestaurantChargeKrw = -1 }];
        yield return [new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.RestaurantPays, RestaurantSharePercent = 100 }];
    }

    [Fact]
    public void 비율의두자리와정규동등성복사및누적반올림을검증한다()
    {
        var original = new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 33.33m };
        var copy = DeliveryCostSharingCalculator.Clone(original); original.RestaurantSharePercent = 50;
        Assert.Equal(33.33m, copy.RestaurantSharePercent); Assert.Null(DeliveryCostSharingCalculator.ValidationError(copy));
        var cumulative = DeliveryCostSharingCalculator.Calculate(6003, copy);
        Assert.Equal(2000m, cumulative.RestaurantChargeKrw); Assert.Equal(4003m, cumulative.CustomerChargeKrw);
        Assert.True(DeliveryCostSharingCalculator.Equivalent(DeliveryCostSharingCalculator.Clone(new() { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 33.3300m }), copy));
    }

    [Fact]
    public void null정책만기존정액이고손상JSON은의미를만들지않는다()
    {
        Assert.Equal(DeliveryCostSharingModes.FixedContribution, DeliveryCostSharingStorage.Read(null, 1500).Mode);
        Assert.Equal(1500, DeliveryCostSharingCalculator.Calculate(6000, DeliveryCostSharingStorage.Read(null, 1500)).RestaurantChargeKrw);
        Assert.False(DeliveryCostSharingCalculator.Calculate(6000, DeliveryCostSharingStorage.Read("broken-json", 1500)).IsAllowed);
        var legacy = JsonSerializer.Serialize(new AutoFulfillmentConsent());
        Assert.DoesNotContain("ConfirmedCostSharingPolicy", legacy);
    }
}
