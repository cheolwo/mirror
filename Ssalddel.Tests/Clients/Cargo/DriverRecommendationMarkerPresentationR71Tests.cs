using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Common.Drivers;

namespace Ssalddel.Tests.Clients.Cargo;

public sealed class DriverRecommendationMarkerPresentationR71Tests
{
    [Fact]
    public void DefaultMode_PreservesExistingColorsLabelsAndOrder_WhenSelectionIsProvided()
    {
        var first = Marker("A"); var second = Marker("B");
        var pins = DriverRecommendationMarkerPresentationPolicy.Build([first, second], selectedRecommendationRequestId: "B");

        Assert.Equal(["A", "A", "B", "B"], pins.Select(x => x.Source.RequestId));
        Assert.All(pins, pin => { Assert.False(pin.IsMuted); Assert.Equal(0, pin.ZIndex); });
        Assert.Equal(first.PickupLabel, pins[0].Caption);
        Assert.Equal(first.DropoffLabel, pins[1].Caption);
        Assert.Equal(first.PickupAddress, pins[0].SubCaption);
        Assert.Equal(first.DropoffAddress, pins[1].SubCaption);
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.PickupTintColor, pins[0].TintColor);
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.DropoffTintColor, pins[1].TintColor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void HighlightMode_WithoutAMatchingSelection_KeepsEveryKnownPointGray(string? selectedId)
    {
        var pins = DriverRecommendationMarkerPresentationPolicy.Build([Marker("A"), Marker("B")], true, selectedId);

        Assert.Equal(4, pins.Count);
        Assert.All(pins, pin =>
        {
            Assert.True(pin.IsMuted);
            Assert.Equal(DriverRecommendationMarkerPresentationPolicy.InactiveTintColor, pin.TintColor);
            Assert.Equal(0, pin.ZIndex);
        });
    }

    [Fact]
    public void SelectedSharedPickup_IsDrawnAboveAllInactivePins_WithoutRemovingOtherOffers()
    {
        var first = Marker("A"); var selected = Marker("B"); var third = Marker("C");
        var pins = DriverRecommendationMarkerPresentationPolicy.Build([first, selected, third], true, "B");

        Assert.Equal(["A", "A", "C", "C", "B", "B"], pins.Select(x => x.Source.RequestId));
        Assert.All(pins.Take(4), pin => Assert.True(pin.IsMuted));
        Assert.All(pins.TakeLast(2), pin => { Assert.False(pin.IsMuted); Assert.Equal(1, pin.ZIndex); });
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.PickupTintColor, pins[^2].TintColor);
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.DropoffTintColor, pins[^1].TintColor);
        Assert.Same(selected, pins[^2].Source);
        Assert.Equal(first.PickupLatitude, pins[^2].Latitude);
    }

    [Fact]
    public void MissingPickup_DoesNotHideAConfirmedDropoff_AndDoesNotCreateAZeroPoint()
    {
        var marker = Marker("dropoff-only") with { PickupLatitude = 0, PickupLongitude = 0 };
        var pin = Assert.Single(DriverRecommendationMarkerPresentationPolicy.Build([marker], true, marker.RequestId));

        Assert.False(pin.IsPickup);
        Assert.Equal(marker.DropoffLatitude, pin.Latitude);
        Assert.Equal(marker.DropoffLabel, pin.Caption);
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.DropoffTintColor, pin.TintColor);
    }

    [Theory]
    [InlineData(double.NaN, 127.0)]
    [InlineData(double.PositiveInfinity, 127.0)]
    [InlineData(37.0, double.NegativeInfinity)]
    [InlineData(91.0, 127.0)]
    [InlineData(-91.0, 127.0)]
    [InlineData(37.0, 181.0)]
    [InlineData(0.0, 127.0)]
    [InlineData(37.0, 0.0)]
    public void InvalidPickup_IsOmitted_WhileTheValidDropoffRemains(double latitude, double longitude)
    {
        var marker = Marker("A") with { PickupLatitude = latitude, PickupLongitude = longitude };
        var pin = Assert.Single(DriverRecommendationMarkerPresentationPolicy.Build([marker]));

        Assert.False(pin.IsPickup);
        Assert.Equal(marker.DropoffLatitude, pin.Latitude);
        Assert.Equal(marker.DropoffLongitude, pin.Longitude);
    }

    [Fact]
    public void InvalidDropoff_IsOmitted_AndPageProvidedCardOrdinalIsPreserved()
    {
        var marker = Marker("C") with
        {
            PickupLabel = "추천 3 · 픽업", DropoffLabel = "추천 3 · 전달",
            DropoffLatitude = double.NaN, DropoffLongitude = 127
        };
        var pin = Assert.Single(DriverRecommendationMarkerPresentationPolicy.Build([marker], true, "C"));

        Assert.True(pin.IsPickup);
        Assert.Equal("추천 3 · 픽업", pin.Caption);
    }

    [Fact]
    public void NoKnownCoordinates_ProducesNoPins_WithoutFallingBackToAnAddressOrMapCenter()
    {
        var missing = Marker("missing") with { PickupLatitude = 0, PickupLongitude = 0, DropoffLatitude = 0, DropoffLongitude = 0 };
        Assert.Empty(DriverRecommendationMarkerPresentationPolicy.Build([missing], true, "missing"));
        Assert.Empty(DriverRecommendationMarkerPresentationPolicy.Build([], true));
    }

    private static DriverMapMarkerItem Marker(string id) => new(
        id, 37.588, 127.085, 37.590, 127.087, id, "조건", "픽업 장소", "전달 장소", "기존 상차지", "기존 하차지");
}
