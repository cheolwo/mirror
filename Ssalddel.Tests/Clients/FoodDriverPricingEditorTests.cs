using System.Text.Json;
using Ssalddel.Contracts.Admin.Restaurants;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class FoodDriverPricingEditorTests
{
    [Fact]
    public void LoadingLegacySettingsDoesNotInventPickupOrDropoffAmounts()
    {
        var policy = new 음식배달요금정책응답 { DriverBasePayout = 2500m };
        var editor = new FoodDriverPricingEditor(policy);
        Assert.False(editor.SplitEnabled);
        Assert.Null(policy.DriverPickupPayout);
        Assert.Equal(2500m, policy.DriverBasePayout);
        Assert.Throws<InvalidOperationException>(() => editor.DropoffFee);
    }

    [Fact]
    public void IndependentAmountsSurviveSerializationWithoutResettingOtherPolicies()
    {
        var policy = new 음식배달요금정책응답
        {
            DriverBasePayout = 1400m, DriverPickupPayout = 700m,
            DriverWeatherSurcharge = 1200m, DriverWeatherSurchargePolicyRevision = "existing-r1"
        };
        var editor = new FoodDriverPricingEditor(policy);
        editor.PickupFee = 1000m;
        Assert.Equal(700m, editor.DropoffFee);
        editor.DropoffFee = 1000m;
        policy.DistanceUnitMeters = 100;
        policy.DriverDistanceUnitPayout = 100m;
        var received = JsonSerializer.Deserialize<음식배달요금정책응답>(JsonSerializer.Serialize(policy))!;
        var reloaded = new FoodDriverPricingEditor(received);
        Assert.Equal(1000m, reloaded.PickupFee);
        Assert.Equal(1000m, reloaded.DropoffFee);
        Assert.Equal(2000m, received.DriverBasePayout);
        Assert.Equal(100m, received.DriverDistanceUnitPayout);
        Assert.Equal(1200m, received.DriverWeatherSurcharge);
        Assert.Equal("existing-r1", received.DriverWeatherSurchargePolicyRevision);
    }

    [Fact]
    public void ExplicitSplitTogglePreservesTotalAndDisablingRestoresUnsplitMeaning()
    {
        var policy = new 음식배달요금정책응답 { DriverBasePayout = 2500m };
        var editor = new FoodDriverPricingEditor(policy);
        editor.SplitEnabled = true;
        Assert.Equal(0m, editor.PickupFee);
        Assert.Equal(2500m, editor.DropoffFee);
        editor.DropoffFee = 1000m;
        editor.PickupFee = 1000m;
        editor.SplitEnabled = false;
        Assert.Null(policy.DriverPickupPayout);
        Assert.Equal(2000m, policy.DriverBasePayout);
    }
}
