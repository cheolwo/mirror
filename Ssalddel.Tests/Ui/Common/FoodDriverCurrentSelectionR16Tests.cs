using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodDriverCurrentSelectionR16Tests
{
    [Fact]
    public async Task 완료된_이전ID로_복귀해도_첫조회부터_다음업무의_카드_핀_경로를_함께_표시한다()
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        var active = new FoodDeliveryDriverActiveDeliveryDto
        {
            OfferId = "next-offer", RestaurantName = "음식점 2", WorkStatus = DriverWorkOfferStatus.Accepted,
            Pickup = new() { Address = "픽업 장소 2", Latitude = 37.57m, Longitude = 127.08m },
            Dropoff = new() { Address = "전달 장소 2", Latitude = 37.58m, Longitude = 127.09m }
        };
        api.Reads["api/v1/driver/food-deliveries/workspace"] = new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [active] };
        api.Reads["api/v1/driver/food-deliveries/work/status"] = new 기사운행상태응답 { Status = "운행중" };
        api.Reads["api/v1/driver/operational-dispatch/availability"] = new 운영배차수신상태Dto
        { 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능 };
        var position = new RoleWorkspaceLocation(37.56, 127.07, 5, DateTimeOffset.UtcNow);
        api.Post = (path, body) =>
        {
            if (path.EndsWith("work/location", StringComparison.Ordinal))
                return new 기사위치갱신응답 { 현재위도 = 37.56m, 현재경도 = 127.07m };
            var request = Assert.IsType<FoodDeliveryDriverRouteRequestDto>(body);
            var stop = Assert.Single(request.Stops);
            return new FoodDeliveryDriverRouteResponseDto { Source = "NaverDirections5", IsEstimated = false,
                Points = [new() { Latitude = request.StartLatitude, Longitude = request.StartLongitude },
                    new() { Latitude = stop.Latitude, Longitude = stop.Longitude }] };
        };
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location(position));
        await adapter.LoadAsync(null, default);
        await adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.UpdateLocation, Guid.NewGuid(), default);
        var snapshot = await adapter.LoadAsync("completed-offer", default);
        var selected = Assert.Single(snapshot.Items);
        Assert.Equal(active.OfferId, snapshot.SelectedId);
        Assert.Equal(active.Pickup.Address, selected.Summary?.Destination);
        var driver = Assert.Single(selected.Markers!.Where(marker => marker.Kind == NeighborhoodMapMarkerKinds.Driver));
        Assert.Equal(position.MeasuredAt.AddSeconds(30), driver.ExpiresAt);
        Assert.NotEmpty(selected.Routes!);
        Assert.All(selected.Routes!, route =>
        {
            Assert.StartsWith(active.OfferId + ":", route.Id);
            Assert.Equal(position.MeasuredAt.AddSeconds(30), route.ExpiresAt);
        });
    }

    private sealed class Location(RoleWorkspaceLocation position) : IRoleWorkspaceLocationProvider
    {
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult<RoleWorkspaceLocation?>(position);
    }
}
