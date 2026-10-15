using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Settings;

namespace Ssalddel.Tests.Clients;

public sealed class FoodPricingContinuityR75Tests
{
    [Theory]
    [InlineData(DriverWorkOfferStatus.MovingToPickup)]
    [InlineData(DriverWorkOfferStatus.MovingToDropoff)]
    public void AcceptedWork_PreservesFrozenFeeEvidenceAndAssignedActivities(string status)
    {
        var price = Price();
        var recommendation = DeliveryTicketPreview.From(new FoodDeliveryDriverOfferDto
        {
            OfferId = "r75-food", WorkKind = DriverWorkKinds.FoodDelivery,
            AssignedActivities = [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver],
            DriverPayout = 3935, DistanceKm = 6.5m, Pricing = price
        });
        var active = ActiveDeliveryPreview.From(new FoodDeliveryDriverActiveDeliveryDto
        {
            OfferId = recommendation.TicketId, WorkKind = recommendation.WorkKind,
            AssignedActivities = recommendation.AssignedActivities, DriverPayout = recommendation.DriverPayout,
            Pricing = price, WorkStatus = status
        });
        var common = active.ToDriverWorkOffer(new FDriverAppProfile());
        Assert.Equal(recommendation.DriverPayout, active.DriverPayout);
        Assert.Same(price, active.Pricing);
        Assert.Equal(recommendation.PricingDistanceText, active.PricingDistanceText);
        Assert.Equal(recommendation.PricingDistanceFeeText, active.PricingDistanceFeeText);
        Assert.Equal(recommendation.PricingRateText, active.PricingRateText);
        Assert.Contains("1.234568", active.PricingDistanceText);
        Assert.DoesNotContain("6.5", active.PricingDistanceText);
        Assert.Equal(DriverWorkKinds.FoodDelivery, common.WorkType);
        Assert.Equal(recommendation.AssignedActivities, common.AssignedActivities);
        Assert.Equal(price, common.Pricing);
        Assert.Equal(3935m, common.DriverPayout);
    }

    [Fact]
    public void LegacyWorkWithoutPricing_PreservesFeeAndShowsUnknownEvidence()
    {
        var active = ActiveDeliveryPreview.From(new FoodDeliveryDriverActiveDeliveryDto { DriverPayout = 4200 });
        Assert.Equal(4200m, active.DriverPayout);
        Assert.Null(active.Pricing);
        Assert.False(active.OfferPricing.HasEvidence);
        Assert.Contains("미정", active.PricingDistanceText);
        Assert.Empty(active.AssignedActivities);
        Assert.Null(active.ToDriverWorkOffer(new()).Pricing);
    }

    [Fact]
    public void ChangedServerSnapshot_ReplacesEvidenceWithoutMutatingPriorWork()
    {
        var first = ActiveDeliveryPreview.From(new FoodDeliveryDriverActiveDeliveryDto
        { OfferId = "first", DriverPayout = 3935, Pricing = Price() });
        var second = ActiveDeliveryPreview.From(new FoodDeliveryDriverActiveDeliveryDto
        { OfferId = "second", DriverPayout = 5000, Pricing = Price() with { TotalPayoutKrw = 5000, DistanceFeeKrw = 2300 } });
        Assert.Equal(3935m, first.ToDriverWorkOffer(new()).DriverPayout);
        Assert.Equal(1235m, first.Pricing!.DistanceFeeKrw);
        Assert.Equal(5000m, second.ToDriverWorkOffer(new()).DriverPayout);
        Assert.Equal(2300m, second.Pricing!.DistanceFeeKrw);
    }

    private static DriverOfferPricingDto Price() => new()
    {
        DistanceBasisCode = "StraightLine", RawDistanceMeters = 1234.56789m, RatePerKmKrw = 1000,
        IncludedDistanceMeters = 0, DistanceFeeKrw = 1235, BaseFeeKrw = 2000, SurchargeKrw = 700,
        TotalPayoutKrw = 3935, PolicyRevision = "r75-fixture-frozen"
    };
}
