using System.Globalization;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;

namespace Ssalddel.Tests.Clients;

public sealed class CargoFallbackMapCoordinatesR29Tests
{
    private readonly DriverHomeRoutePlanningService _maps = new();

    [Theory]
    [InlineData("91", "127.1")]
    [InlineData("-91", "127.1")]
    [InlineData("37.6", "181")]
    [InlineData("37.6", "-181")]
    [InlineData(null, "127.1")]
    [InlineData("37.6", null)]
    [InlineData("0", "127.1")]
    [InlineData("37.6", "0")]
    public void 무효상차좌표는_쌍으로감추고_확인된하차핀을보존한다(string? latitude, string? longitude)
    {
        var transport = Transport(Parse(latitude), Parse(longitude));
        var marker = Assert.IsType<DriverMapMarkerItem>(_maps.CurrentMarker(transport));
        Assert.Equal(0d, marker.PickupLatitude);
        Assert.Equal(0d, marker.PickupLongitude);
        Assert.Equal(37.61d, marker.DropoffLatitude);
        Assert.Equal(127.11d, marker.DropoffLongitude);
        Assert.Empty(_maps.BuildCurrentRouteOverlays(Location(), transport));
    }

    [Fact]
    public void 양쪽지점이무효면_핀과현재업무연결선을그리지않는다()
    {
        var transport = Transport(91m, 127m) with { 하차위도 = 37m, 하차경도 = 181m };
        Assert.Null(_maps.CurrentMarker(transport));
        Assert.Empty(_maps.BuildCurrentRouteOverlays(Location(), transport));
    }

    [Fact]
    public void 무효하차좌표는_쌍으로감추고_확인된상차핀을보존한다()
    {
        var transport = Transport(37.6m, 127.1m) with { 하차위도 = null };
        var marker = Assert.IsType<DriverMapMarkerItem>(_maps.CurrentMarker(transport));
        Assert.Equal(37.6d, marker.PickupLatitude);
        Assert.Equal(127.1d, marker.PickupLongitude);
        Assert.Equal(0d, marker.DropoffLatitude);
        Assert.Equal(0d, marker.DropoffLongitude);
        Assert.Equal("현재 운송 상차지", Assert.Single(_maps.BuildCurrentRouteOverlays(Location(), transport)).Points[^1].Label);
    }

    [Fact]
    public void 상차완료뒤에는_확인된하차지로연결한다()
    {
        var transport = Transport(null, null) with { 현재단계 = "상차완료", 다음행동 = "하차 확인" };
        var route = Assert.Single(_maps.BuildCurrentRouteOverlays(Location(), transport));
        Assert.Equal("현재 운송 하차지", route.Points[^1].Label);
        Assert.Equal("#2563eb", route.StrokeColor);
    }

    [Fact]
    public void 상차완료뒤하차위치가무효면_상차지로되돌려연결하지않는다()
    {
        var transport = Transport(37.6m, 127.1m) with
        { 현재단계 = "상차완료", 다음행동 = "하차 확인", 하차경도 = 181m };
        Assert.Empty(_maps.BuildCurrentRouteOverlays(Location(), transport));
    }

    [Fact]
    public void 현재위치가범위밖이면_업무지점까지의선을만들지않는다()
    {
        Assert.Empty(_maps.BuildCurrentRouteOverlays(Location() with { 위도 = 91m }, Transport(37.6m, 127.1m)));
    }

    [Fact]
    public void 순서미확인fallback도_상차지공백을건너하차지로이어그리지않는다()
    {
        var result = _maps.BuildWorkspaceRouteState(Location(), Transport(null, 127.1m), null, true);
        var marker = Assert.Single(result.Markers);
        Assert.Equal(0d, marker.PickupLatitude);
        Assert.Equal(0d, marker.PickupLongitude);
        Assert.Equal(37.61d, marker.DropoffLatitude);
        Assert.Empty(result.RouteOverlays);
    }

    [Fact]
    public void 정상fallback은_현재위치와다음상차위치를그대로보존한다()
    {
        var result = _maps.BuildWorkspaceRouteState(Location(), Transport(37.6m, 127.1m), null, true);
        var marker = Assert.Single(result.Markers);
        Assert.Equal(37.6d, marker.PickupLatitude);
        Assert.Equal(127.11d, marker.DropoffLongitude);
        Assert.Equal(2, Assert.Single(result.RouteOverlays).Points.Count);
    }

    [Theory]
    [InlineData(double.NaN, 127.1d)]
    [InlineData(double.PositiveInfinity, 127.1d)]
    [InlineData(37.6d, double.NegativeInfinity)]
    [InlineData(91d, 127.1d)]
    [InlineData(37.6d, 181d)]
    public void 추천상차위치가무효면_하차지로건너뛰는선을그리지않는다(double latitude, double longitude)
    {
        var marker = Recommendation() with { PickupLatitude = latitude, PickupLongitude = longitude };
        Assert.Empty(_maps.BuildLinkedRouteOverlays(Location(), null, marker));
        Assert.Empty(_maps.BuildAcceptedRouteOverlays(Location(), null, marker));
        Assert.Empty(_maps.BuildRecommendationReviewRouteOverlays(Location(), null, marker));
    }

    [Fact]
    public void 추천하차위치가무효면_정상상차선만남기고수락연결선은보류한다()
    {
        var marker = Recommendation() with { DropoffLatitude = double.NaN };
        var linked = Assert.Single(_maps.BuildLinkedRouteOverlays(Location(), null, marker));
        Assert.Equal(2, linked.Points.Count);
        Assert.Equal("추천 상차지", linked.Points[^1].Label);
        Assert.Empty(_maps.BuildAcceptedRouteOverlays(Location(), null, marker));
        Assert.Equal("추천 하차지: 추천 상세에서 확인", _maps.BuildLinkedRouteCardState(Location(), null, marker).RecommendationDropoffRouteLabel);
    }

    private static decimal? Parse(string? value)
        => value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture);

    private static DriverMapMarkerItem Recommendation()
        => new("request-r29", 37.6d, 127.1d, 37.61d, 127.11d, "운송 1", "상차 확인", "상차 장소");

    private static 기사운송샘플항목 Transport(decimal? pickupLatitude, decimal? pickupLongitude)
        => new(1, "request-r29", "화물", "상차 장소", "하차 장소", pickupLatitude, pickupLongitude,
            37.61m, 127.11m, "확정", DateTime.UtcNow, 3, 8000, false, false, "별도정산", "상차 확인");

    private static 기사현재위치샘플 Location()
        => new("최근 확인 위치", 37.59m, 127.09m, DateTime.UtcNow)
        { SourceCode = DriverNativeLocationSources.ServerLocationReceived, 관측시각Utc = DateTime.UtcNow.AddSeconds(-1) };
}
