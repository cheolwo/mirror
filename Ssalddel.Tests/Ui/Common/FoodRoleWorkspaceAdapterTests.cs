using Ssalddel.Contracts.Admin.Dispatch;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodRoleWorkspaceAdapterTests
{
    [Fact]
    public async Task DriverOfferSummaryUsesActualFeeDistanceAndPickupBeforeAcceptance()
    {
        var api = DriverApi(new());
        api.Reads["api/v1/driver/food-deliveries/workspace"] = new FoodDeliveryDriverWorkspaceDto
        {
            Recommendations = [new() { OfferId = "offer-a", RestaurantName = "음식점", DriverPayout = 4720,
                DistanceKm = 3.414m, OrderSummary = "긴 메뉴 정보", Pickup = new() { Address = "픽업 주소" },
                Dropoff = new() { Address = "수락 전 전달 주소" } }]
        };
        var item = Assert.Single((await new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(null)).LoadAsync(null, default)).Items);
        var summary = Assert.IsType<RoleWorkspaceSummary>(item.Summary);
        Assert.Equal("픽업지", summary.DestinationLabel);
        Assert.Equal("픽업 주소", summary.Destination);
        Assert.Equal(new[] { new RoleWorkspaceField("배달료", "4,720원"), new("제안 거리", "3.414 km") }, summary.Metrics);
        Assert.Null(summary.Request);
        Assert.DoesNotContain(summary.Metrics!, field => field.Value.Contains("긴 메뉴", StringComparison.Ordinal));
        Assert.Equal("긴 메뉴 정보", item.Subtitle);
        Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(null, "미확인")]
    [InlineData("-1", "미확인")]
    [InlineData("0", "0 km")]
    public async Task DriverOfferSummaryDoesNotInventMissingMetricsAndKeepsConfirmedZeroDistance(string? distance, string expected)
    {
        var api = DriverApi(new());
        api.Reads["api/v1/driver/food-deliveries/workspace"] = new FoodDeliveryDriverWorkspaceDto
        {
            Recommendations = [new() { OfferId = "offer-a", DriverPayout = 0,
                DistanceKm = distance is null ? null : decimal.Parse(distance, System.Globalization.CultureInfo.InvariantCulture) }]
        };
        var item = Assert.Single((await new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(null)).LoadAsync(null, default)).Items);
        var summary = Assert.IsType<RoleWorkspaceSummary>(item.Summary);
        Assert.Equal("미확인", summary.Destination);
        Assert.Equal("미확인", summary.Metrics!.Single(field => field.Label == "배달료").Value);
        Assert.Equal(expected, summary.Metrics!.Single(field => field.Label == "제안 거리").Value);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DriverPickupSummaryKeepsNextPickupAndPreparationWithoutCustomerFields(bool arrived, bool recooking)
    {
        var active = new FoodDeliveryDriverActiveDeliveryDto
        {
            OfferId = "offer-a", DriverPayout = 4180, WorkStatus = DriverWorkOfferStatus.Accepted,
            RestaurantArrivedAtUtc = arrived ? new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc) : null,
            DisplayedPreparationReadyAtUtc = new(2026, 10, 5, 3, 10, 0, DateTimeKind.Utc), CurrentPreparationRound = recooking ? 2 : 1,
            Pickup = new() { Address = "픽업 주소" }, Dropoff = new() { Address = "고객 전달 주소" },
            Recipient = new() { DisplayName = "수령인 이름", ContactPhone = "010-1234-5678", DeliveryInstructions = "고객 전달 요청" }
        };
        var summary = Assert.IsType<RoleWorkspaceSummary>(Assert.Single(
            (await new FoodDriverRoleWorkspaceAdapter(DriverApi(active), new LocationProvider(null)).LoadAsync(null, default)).Items).Summary);
        Assert.Equal("픽업지", summary.DestinationLabel);
        Assert.Equal("픽업 주소", summary.Destination);
        Assert.Equal(new[] { new RoleWorkspaceField("배달료", "4,180원"), new("준비 예정", "10/5 12:10 (한국)") }, summary.Metrics);
        Assert.DoesNotContain(summary.Metrics!, field => field.Label.Contains("거리", StringComparison.Ordinal));
        if (arrived) Assert.Equal(new RoleWorkspaceField("픽업 확인", recooking
            ? "이번 재조리 음식이 아직 준비되지 않았습니다. 준비 완료를 기다려 주세요."
            : "음식이 아직 준비되지 않았습니다. 준비 완료를 기다려 주세요."), summary.Request);
        else Assert.Null(summary.Request);
    }

    [Fact]
    public async Task DriverDeliverySummaryShowsDestinationAndRequestWithoutNameOrContact()
    {
        var active = new FoodDeliveryDriverActiveDeliveryDto
        {
            OfferId = "offer-a", DriverPayout = 4180, WorkStatus = DriverWorkOfferStatus.MovingToDropoff,
            Pickup = new() { Address = "픽업 주소" }, Dropoff = new() { Address = "고객 전달 주소" },
            Recipient = new() { DisplayName = "수령인 이름", ContactPhone = "010-1234-5678", DeliveryInstructions = "초인종 없이 문 앞에 놓아 주세요." }
        };
        var item = Assert.Single((await new FoodDriverRoleWorkspaceAdapter(DriverApi(active), new LocationProvider(null)).LoadAsync(null, default)).Items);
        var summary = Assert.IsType<RoleWorkspaceSummary>(item.Summary);
        Assert.Equal("전달지", summary.DestinationLabel);
        Assert.Equal("고객 전달 주소", summary.Destination);
        Assert.Equal(new RoleWorkspaceField("전달 요청", "초인종 없이 문 앞에 놓아 주세요."), summary.Request);
        Assert.Equal(new RoleWorkspaceField("배달료", "4,180원"), Assert.Single(summary.Metrics!));
        Assert.DoesNotContain(summary.Metrics!, field => field.Value.Contains("수령인", StringComparison.Ordinal) || field.Value.Contains("010-1234", StringComparison.Ordinal));
        Assert.Contains(item.Sections!.SelectMany(section => section.Fields), field => field.Label == "연락처" && field.Value == "•••• 5678");
    }

    [Theory]
    [InlineData(null, "/workspace/orderer")]
    [InlineData("/workspace/orderer", "/workspace/orderer")]
    [InlineData("/workspace/orderer?selected=order-a", "/workspace/orderer?selected=order-a")]
    [InlineData("/workspace/orderer?selected=%EC%A3%BC%EB%AC%B8-a", "/workspace/orderer?selected=%EC%A3%BC%EB%AC%B8-a")]
    [InlineData("https://example.com/workspace/orderer", "/workspace/orderer")]
    [InlineData("//example.com/workspace/orderer", "/workspace/orderer")]
    [InlineData("/workspace/orderer/new", "/workspace/orderer")]
    [InlineData("/workspace/orderer/action?actionKey=order.cancel&itemId=order-a", "/workspace/orderer")]
    [InlineData("/workspace/orderer?selected=order-a&returnUrl=https://example.com", "/workspace/orderer")]
    [InlineData("/workspace/orderer?selected=order-a#other", "/workspace/orderer")]
    public void NewOrderReturnPreservesOnlyOrdererWorkspaceSelection(string? href, string expected)
        => Assert.Equal(expected, FoodWorkspacePresentation.OrdererReturnHref(href));

    [Theory]
    [InlineData("restaurant", "restaurant.accept", "order-a", "/workspace/restaurant/action?actionKey=restaurant.accept&itemId=order-a")]
    [InlineData("food-driver", "driver.interrupt", "offer:a", "/workspace/food-driver/action?actionKey=driver.interrupt&itemId=offer%3Aa")]
    [InlineData("operator", "food-operator.interruption-review", "order-a", "/workspace/operator/action?actionKey=food-operator.interruption-review&itemId=order-a")]
    public void ActionUrlKeepsStableActionInQueryAndFileFreePath(string role, string action, string item, string expected)
    {
        var route = FoodWorkspacePresentation.ActionRoute(role, action, item);
        Assert.Equal(expected, route);
        Assert.Equal($"/workspace/{role}/action", route.Split('?', 2)[0]);
    }

    [Theory]
    [InlineData(false, false, false, false, false, FoodDriverStage.Waiting)]
    [InlineData(true, false, false, false, true, FoodDriverStage.PickupTravel)]
    [InlineData(true, false, true, false, false, FoodDriverStage.PickupWaiting)]
    [InlineData(true, false, false, true, false, FoodDriverStage.PickupWaiting)]
    [InlineData(true, true, true, true, true, FoodDriverStage.Delivery)]
    public void NativeAndSharedDriverStageUsesCurrentSnapshot(bool active, bool dropoff, bool arrived, bool pickup, bool arrival, FoodDriverStage expected)
        => Assert.Equal(expected, FoodDriverStagePresentation.Resolve(active, dropoff, arrived, pickup, arrival));

    [Fact]
    public async Task DriverDeliveryKeepsThreeStageCardsAndDisabledPrimaryWithoutInventingPermission()
    {
        var api = DriverApi(new() { OfferId = "offer-a", RestaurantName = "음식점", WorkStatus = DriverWorkOfferStatus.MovingToDropoff,
            Recipient = new() { DeliveryInstructions = "문 앞에서 연락해 주세요.", ContactPhone = "010-1234-5678" } });
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(null));
        var result = await adapter.LoadAsync(null, default);
        var item = Assert.Single(result.Items);
        Assert.Equal(new[] { "전달지", "고객 요청 사항", "주문 정보" }, item.Sections!.Select(value => value.Title));
        Assert.Contains(item.Sections![1].Fields, value => value.Value == "문 앞에서 연락해 주세요.");
        Assert.DoesNotContain(item.Sections.SelectMany(value => value.Fields), value => value.Value.Contains("010-1234-5678", StringComparison.Ordinal));
        var primary = Assert.Single(item.Actions!, value => value.IsPrimary);
        Assert.Equal(음식배달가능행동Ids.기사전달완료, primary.Key);
        Assert.False(primary.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(item.Id, primary.Key, Guid.NewGuid(), default));
        Assert.Empty(api.Writes);
        Assert.Empty(item.Markers!);
        Assert.Contains("현재 위치", result.Message);
    }

    [Fact]
    public async Task DriverRecordsActualLocationAndOnlyAcceptsActualDirections()
    {
        var active = new FoodDeliveryDriverActiveDeliveryDto { OfferId = "offer-a", RestaurantName = "음식점", WorkStatus = DriverWorkOfferStatus.Accepted,
            Pickup = new() { Latitude = 37.57m, Longitude = 127.08m }, Dropoff = new() { Latitude = 37.58m, Longitude = 127.09m } };
        var api = DriverApi(active);
        var location = new RoleWorkspaceLocation(37.56, 127.07, 5, DateTimeOffset.UtcNow);
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
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(location));
        await adapter.LoadAsync(null, default);
        Assert.Empty(api.Writes);
        await adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.UpdateLocation, Guid.NewGuid(), default);
        var result = await adapter.LoadAsync(null, default);
        var item = Assert.Single(result.Items);
        Assert.Contains(item.Markers!, marker => marker.Kind == "driver" && marker.Latitude == location.Latitude && marker.Longitude == location.Longitude);
        Assert.Equal(new[] { "#f97316", "#6b7280" }, item.Routes!.Select(route => route.Color));
        Assert.Contains(item.Markers!, marker => marker.Kind == "dropoff");
        Assert.All(item.Routes!, route => Assert.Equal("NaverDirections5", route.SourceCode));
        var publish = Assert.IsType<기사위치갱신요청>(api.Writes[0].Body);
        Assert.Equal(기사앱식별자.FoodDeliveryDriverApp, publish.AppKey);
        Assert.Equal((decimal)location.Latitude, publish.위도);
        active.WorkStatus = DriverWorkOfferStatus.MovingToDropoff;
        var delivery = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Equal("#2563eb", Assert.Single(delivery.Routes!).Color);
        Assert.Contains(delivery.Markers!, marker => marker.Kind == "dropoff");
        var deliveryRoute = Assert.IsType<FoodDeliveryDriverRouteRequestDto>(api.Writes[^1].Body);
        Assert.Equal((decimal)location.Latitude, deliveryRoute.StartLatitude);
        Assert.Equal(active.Dropoff.Latitude, Assert.Single(deliveryRoute.Stops).Latitude);
        adapter.Clear();
        var cleared = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.DoesNotContain(cleared.Markers!, marker => marker.Kind == "driver");
        Assert.Empty(cleared.Routes ?? []);
    }

    [Fact]
    public async Task DriverEstimatedOrFailedDirectionsKeepActualStopsWithoutDrawingStraightLine()
    {
        var api = DriverApi(new() { OfferId = "offer-a", Pickup = new() { Latitude = 37.57m, Longitude = 127.08m },
            Dropoff = new() { Latitude = 37.58m, Longitude = 127.09m } });
        api.Post = (path, body) => path.EndsWith("work/location", StringComparison.Ordinal)
            ? new 기사위치갱신응답 { 현재위도 = 37.56m, 현재경도 = 127.07m }
            : new FoodDeliveryDriverRouteResponseDto { Source = "CoordinateEstimate", IsEstimated = true,
                Points = [new() { Latitude = 37.56m, Longitude = 127.07m }, new() { Latitude = 37.57m, Longitude = 127.08m }] };
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(new(37.56, 127.07, 5, DateTimeOffset.UtcNow)));
        await adapter.LoadAsync(null, default);
        await adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.UpdateLocation, Guid.NewGuid(), default);
        var snapshot = await adapter.LoadAsync(null, default);
        Assert.Empty(Assert.Single(snapshot.Items).Routes!);
        Assert.Equal(3, Assert.Single(snapshot.Items).Markers!.Count);
        Assert.Contains("실제 이동 경로를 확인하지 못했습니다", snapshot.Message);
        api.Post = (path, body) => throw new HttpRequestException("unavailable");
        var failed = await adapter.LoadAsync(null, default);
        Assert.Empty(Assert.Single(failed.Items).Routes!);
        Assert.Equal(3, Assert.Single(failed.Items).Markers!.Count);
    }

    [Fact]
    public async Task DriverStaleGpsBlocksCommandBeforeLocationOrActionPost()
    {
        var api = DriverApi(new() { OfferId = "offer-a", DeliveryAttemptId = "attempt-a", AttemptRevision = 4,
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사가게도착, ExpectedRevision = 4 }] });
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(new(37.56, 127.07, 5, DateTimeOffset.UtcNow.AddMinutes(-2))));
        await adapter.LoadAsync(null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync("offer-a", 음식배달가능행동Ids.기사가게도착, Guid.NewGuid(), default));
        Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DriverArrivalRequiresCurrentAttemptAndUnrecordedArrival(bool hasAttempt, bool alreadyArrived)
    {
        var api = DriverApi(new() { OfferId = "offer-a", DeliveryAttemptId = hasAttempt ? "attempt-a" : null,
            RestaurantArrivedAtUtc = alreadyArrived ? DateTime.UtcNow : null,
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사가게도착, ExpectedRevision = 4 }] });
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(new(37.56, 127.07, 5, DateTimeOffset.UtcNow)));
        var item = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.DoesNotContain(item.Actions!, action => action.Key == 음식배달가능행동Ids.기사가게도착 && action.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync("offer-a", 음식배달가능행동Ids.기사가게도착, Guid.NewGuid(), default));
        Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task DriverDoesNotCacheLocationWhenServerDoesNotConfirmPostedCoordinates()
    {
        var api = DriverApi(new() { OfferId = "offer-a", Pickup = new() { Latitude = 37.57m, Longitude = 127.08m } });
        api.Post = (path, body) => new 기사위치갱신응답 { 현재위도 = 37.6m, 현재경도 = 127.1m };
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new LocationProvider(new(37.56, 127.07, 5, DateTimeOffset.UtcNow)));
        await adapter.LoadAsync(null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.UpdateLocation, Guid.NewGuid(), default));
        var item = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.DoesNotContain(item.Markers!, marker => marker.Kind == "driver");
        Assert.Empty(item.Routes ?? []);
        Assert.Single(api.Writes);
    }

    [Fact]
    public async Task OrdererOnlyTracksCurrentOwnerSnapshotAndRequiresReceiptPermission()
    {
        var api = new Api();
        api.Reads["api/v1/food-orders?page=1&pageSize=50"] = new 주문자음식주문목록응답 { Items = [new() { 주문번호 = "order-a", 상태 = 음식주문상태코드.전달완료 }] };
        var detail = new 주문자음식주문상세응답 { 주문 = new() { 주문번호 = "order-a", 상태 = 음식주문상태코드.전달완료 },
            배달진행 = new() { 수령확인가능 = true }, 기사위치 = new() { 상태 = 음식배달위치추적상태코드.종료, 위도 = 37.5m, 경도 = 127m },
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.주문수령확인 }] };
        api.Reads["api/v1/food-orders/order-a"] = detail;
        var adapter = new OrdererRoleWorkspaceAdapter(api);
        var item = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Empty(item.Markers!);
        var requestId = Guid.NewGuid();
        await adapter.PerformAsync(item.Id, 음식배달가능행동Ids.주문수령확인, requestId, default);
        var write = Assert.Single(api.Writes);
        Assert.Equal("orderer", write.Role);
        Assert.Equal(requestId, Assert.IsType<주문자음식주문수령확인요청>(write.Body).클라이언트요청Id);
        adapter.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(item.Id, 음식배달가능행동Ids.주문수령확인, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RestaurantUsesServerPreparationGateAndExpectedRevision()
    {
        var api = new Api();
        var order = new 음식주문응답 { 주문번호 = "order-a", Revision = 12, 조리시작가능 = false,
            CurrentPreparationRound = 2, AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점조리시작, ExpectedRevision = 12 }] };
        api.Reads["api/v1/food-orders/restaurant/inbox?처리상태=" + Uri.EscapeDataString("미처리") + "&Page=1&PageSize=50"] = new 음식점주문수신함응답 { Items = [order] };
        api.Reads["api/v1/food-orders/restaurant/inbox/order-a"] = order;
        var adapter = new RestaurantRoleWorkspaceAdapter(api);
        var item = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.False(Assert.Single(item.Actions!).Enabled);
        Assert.Empty(item.Markers!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(item.Id, 음식배달가능행동Ids.음식점조리시작, Guid.NewGuid(), default));
        Assert.Empty(api.Writes);
        order.조리시작가능 = true;
        await adapter.LoadAsync(null, default);
        await adapter.PerformAsync(item.Id, 음식배달가능행동Ids.음식점조리시작, Guid.NewGuid(), default);
        Assert.Equal(12, Assert.IsType<음식점주문진행변경요청>(Assert.Single(api.Writes).Body).예상Revision);
    }

    [Theory]
    [InlineData("jungnang-scope-sample", 0)]
    [InlineData("actual-with-sample-drivers", 0)]
    [InlineData("actual", 2)]
    public async Task OperatorUsesSeparateAdminReadAndNeverMapsSampleCoordinates(string source, int markerCount)
    {
        var api = new Api();
        api.Reads["api/v1/admin/food-orders/operations?page=1&pageSize=50"] = new AdminFoodOrderListDto { Items = [new() { OrderNo = "order-a" }] };
        api.Reads["api/v1/admin/dispatch/food-delivery-ai-review"] = new FoodDeliveryDispatchAIReviewWorkspaceDto { Source = source,
            Orders = [new() { OrderNo = "order-a", RestaurantLatitude = 37.57m, RestaurantLongitude = 127.08m, CustomerLatitude = 37.58m, CustomerLongitude = 127.09m }] };
        api.Reads["api/v1/admin/food-orders/order-a/operations-trace"] = new 음식주문운영추적응답 { 주문번호 = "order-a" };
        var adapter = new OperatorRoleWorkspaceAdapter(api);
        Assert.Equal(markerCount, Assert.Single((await adapter.LoadAsync(null, default)).Items).Markers!.Count);
        Assert.All(api.ReadRoles, role => Assert.Equal("operator", role));
        Assert.Empty(api.Writes);
    }

    private static Api DriverApi(FoodDeliveryDriverActiveDeliveryDto delivery)
    {
        var api = new Api();
        api.Reads["api/v1/driver/food-deliveries/workspace"] = new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [delivery] };
        api.Reads["api/v1/driver/food-deliveries/work/status"] = new 기사운행상태응답 { Status = "운행중" };
        api.Reads["api/v1/driver/operational-dispatch/availability"] = new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능 };
        return api;
    }

    internal sealed class Api : IRoleWorkspaceApi
    {
        public Dictionary<string, object> Reads { get; } = new(StringComparer.Ordinal);
        public List<string> ReadRoles { get; } = [];
        public List<(string Role, string Path, object Body)> Writes { get; } = [];
        public Func<string, object, object?>? Post { get; set; }
        public Func<string, object, Task<object?>>? AsyncPost { get; set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); ReadRoles.Add(roleKey);
            return Task.FromResult((T)Reads[path]);
        }
        public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        {
            Writes.Add((roleKey, path, body));
            var response = AsyncPost is null ? Post?.Invoke(path, body) : await AsyncPost(path, body);
            return response is null ? default : (T)response;
        }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class LocationProvider(RoleWorkspaceLocation? location) : IRoleWorkspaceLocationProvider
    { public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(location); }
}
