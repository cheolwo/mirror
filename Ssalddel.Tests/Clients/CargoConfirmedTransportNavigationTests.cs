using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

public sealed class CargoConfirmedTransportNavigationTests
{
    [Theory]
    [InlineData("확정", true)]
    [InlineData("배차확정", true)]
    [InlineData("상차완료", false)]
    [InlineData("운송중", false)]
    [InlineData("하차지도착", false)]
    public void 다음행동문구가없어도_서버상태로_상하차_이동좌표와_설명을결정한다(
        string status, bool beforePickup)
    {
        var currentLocation = new 기사현재위치샘플("합성 현재 위치", 37.5m, 127m, DateTime.UtcNow);
        var transport = 기사운송표시Mapper.Map(new 기사운송요약응답
        {
            Id = 42, 운송번호 = "synthetic-current-request", 상태 = status,
            출발지 = "합성 상차지", 도착지 = "합성 하차지"
        }) with
        {
            픽업위도 = 37.51m, 픽업경도 = 127.01m,
            하차위도 = 37.58m, 하차경도 = 127.08m,
            다음행동 = "상태 갱신"
        };
        var recommendation = new DriverMapMarkerItem(
            "synthetic-recommendation", 37.6, 127.1, 37.7, 127.2,
            "합성 추천", "합성 추천 하차지", "합성 추천 상차지");
        var service = new DriverHomeRoutePlanningService();

        var overlay = Assert.Single(service.BuildLinkedRouteOverlays(currentLocation, transport, recommendation));
        var card = service.BuildLinkedRouteCardState(currentLocation, transport, recommendation);

        var nextPoint = overlay.Points[1];
        Assert.Equal(beforePickup ? "현재 운송 상차지" : "현재 운송 하차지", nextPoint.Label);
        Assert.Equal(beforePickup ? 37.51d : 37.58d, nextPoint.Latitude);
        Assert.Equal(beforePickup ? 127.01d : 127.08d, nextPoint.Longitude);
        Assert.Equal(beforePickup
            ? "현재 이동: 합성 현재 위치 → 상차지 합성 상차지"
            : "현재 이동: 합성 현재 위치 → 하차지 합성 하차지", card.CurrentRouteLabel);
        Assert.StartsWith(beforePickup ? "현재 운송 상차지 이후" : "현재 운송 하차지 이후", card.Summary);
    }
}
