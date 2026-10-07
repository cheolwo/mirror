using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodExchangeMapViewModelTests
{
    [Fact]
    public async Task 늦은_초기저장설정은_최신_URL의_none선택을_덮지않는다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodMapPreferences?>(); var loads = 0;
        var saved = new Preferences { Load = () => ++loads == 1 ? delayed.Task : Task.FromResult<NeighborhoodMapPreferences?>(null) };
        using var vm = new NeighborhoodExchangeMapViewModel(new Client(), saved, new User());
        var initial = vm.InitializeAsync(null, null, null);
        await vm.InitializeAsync("none", "list", null);
        delayed.SetResult(new([NeighborhoodMapNavigation.Offer], "map", "a")); await initial;
        Assert.Empty(vm.SelectedLayers); Assert.Equal("list", vm.ViewMode);
    }
    [Fact]
    public async Task 명시한_none은_저장설정과_기본레이어보다_우선하고_재방문에도_유지한다()
    {
        var saved = new Preferences { Value = new([NeighborhoodMapNavigation.Offer], "list", "a") };
        var client = new Client(); using var vm = new NeighborhoodExchangeMapViewModel(client, saved, new User());
        await vm.InitializeAsync("none", "map", null);
        Assert.Empty(vm.SelectedLayers); Assert.Equal("map", vm.ViewMode); Assert.Equal(0, client.MapCalls);
        Assert.Contains("layers=none", vm.HomeHref);
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.Offer);
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.Offer);
        Assert.Empty(saved.Value!.Layers);
        using var next = new NeighborhoodExchangeMapViewModel(client, saved, new User());
        await next.InitializeAsync(null, null, null); Assert.Empty(next.SelectedLayers);
    }

    [Fact]
    public async Task 지역선택은_동네중심으로_이동하고_레이어변경은_현재시점을_보존한다()
    {
        var client = new Client(); using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User());
        await vm.InitializeAsync(null, null, null); await vm.SelectRegionAsync("a");
        Assert.Equal(new NeighborhoodMapViewport(37.5, 127.1, 13), vm.RenderState.Viewport); Assert.False(vm.RenderState.PreserveViewport);
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.Need);
        Assert.True(vm.RenderState.PreserveViewport); Assert.Equal("a", vm.RegionKey);
        Assert.Contains("region=a", vm.WriteHref); Assert.Contains("returnUrl=", vm.WriteHref);
    }

    [Fact]
    public async Task 검증지역에_없는_핀과_동네미선택글은_물품좌표로_변환하지않는다()
    {
        var client = new Client { PublicMap = new() { Items = [new() { Region = Region("a"), OfferCount = 2 }, new() { Region = Region("invented"), NeedCount = 1 }], UnlocatedPostCount = 3 } };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User());
        await vm.InitializeAsync(null, null, null);
        var pin = Assert.Single(vm.RenderState.Markers); Assert.Equal("region:a", pin.Id); Assert.Equal(2, pin.Count);
        Assert.Equal(3, vm.UnlocatedCount);
        await vm.SelectRegionAsync("invented"); Assert.Null(vm.RegionKey);
    }

    [Fact]
    public async Task 계정변경후_늦게도착한_이전배송응답은_개인좌표를_복구하지않는다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodDeliveryMapResponse?>();
        var user = new User("old"); var client = new Client { Read = (_, _) => delayed.Task };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), user);
        await vm.InitializeAsync("mine-active", null, null);
        var pending = vm.SelectDeliveryAsync("one");
        user.Current = new("next", "다른 이웃", []); vm.SynchronizeOwner();
        delayed.SetResult(Delivery("one")); await pending;
        Assert.Null(vm.Delivery); Assert.Null(vm.SelectedRequestId); Assert.Empty(vm.Deliveries); Assert.Empty(vm.RenderState.Markers);
        user.Current = 현재사용자Snapshot.익명; vm.SynchronizeOwner();
        Assert.False(vm.IsAuthenticated); Assert.Empty(vm.RenderState.Routes);
    }

    [Fact]
    public async Task 로그인만료후_같은계정의_재로그인은_개인조회실패를_해제하고_다시불러온다()
    {
        var client = new Client { Read = (_, _) => Task.FromException<NeighborhoodDeliveryMapResponse?>(new SsalddelApiException("private diagnostics", 401, "test", "", null)) };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User("owner"));
        await vm.InitializeAsync("mine-active", null, null); await vm.SelectDeliveryAsync("one");
        Assert.True(vm.RequiresLogin); Assert.False(vm.IsAuthenticated); Assert.Empty(vm.Deliveries);
        client.Read = (id, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(Delivery(id));
        vm.ResetAuthentication(); await vm.InitializeAsync("mine-active", null, null);
        Assert.False(vm.RequiresLogin); Assert.True(vm.IsAuthenticated);
        await vm.SelectDeliveryAsync("one"); Assert.Equal("one", vm.Delivery!.RequestId);
    }

    [Fact]
    public async Task 이전배송의_늦은오류는_새선택을_덮지않는다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodDeliveryMapResponse?>();
        var client = new Client { Read = (id, _) => id == "one" ? delayed.Task : Task.FromResult<NeighborhoodDeliveryMapResponse?>(Delivery(id)) };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User("owner"));
        await vm.InitializeAsync("mine-active", null, null);
        var first = vm.SelectDeliveryAsync("one"); await vm.SelectDeliveryAsync("two");
        delayed.SetException(new InvalidOperationException("private diagnostics")); await first;
        Assert.Equal("two", vm.Delivery!.RequestId); Assert.Null(vm.DeliveryError);
        Assert.Single(vm.RenderState.Routes); Assert.Contains(vm.RenderState.Markers, pin => pin.Id == "delivery:one:pickup" && pin.Kind == NeighborhoodMapMarkerKinds.Inactive);
    }

    [Fact]
    public async Task 위치갱신은_동일원장의_예정경로만_이어가고_빈기사위치는_즉시지운다()
    {
        var now = new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);
        var selected = Delivery("one"); selected.DriverLocationStateCode = NeighborhoodDeliveryMapStates.Available;
        selected.DriverLocation = new() { Latitude = 37.51m, Longitude = 127.12m, MeasuredAtUtc = now, ReceivedAtUtc = now };
        var client = new Client { Read = (_, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(selected) };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User("owner"), new Clock(now));
        await vm.InitializeAsync("mine-active", null, null); await vm.SelectDeliveryAsync("one");
        Assert.True(vm.HasRecentDriverLocation);
        client.Read = (_, includeRoute) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(new() { RequestId = "one", RouteRevision = "v1", Pickup = Point(37.5m, 127.1m), Dropoff = Point(37.6m, 127.2m) });
        await vm.RefreshDeliveryAsync(false);
        Assert.False(vm.HasRecentDriverLocation); Assert.DoesNotContain(vm.RenderState.Markers, pin => pin.Kind == NeighborhoodMapMarkerKinds.Driver);
        Assert.Single(vm.RenderState.Routes); Assert.False(client.RouteRequests.Last());
        client.Read = (_, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(new() { RequestId = "one", RouteRevision = "v2", Pickup = Point(37.51m, 127.1m), Dropoff = Point(37.6m, 127.2m) });
        await vm.RefreshDeliveryAsync(false); Assert.Empty(vm.RenderState.Routes);
    }

    [Fact]
    public async Task 기사위치가_사라지거나_오래되면_같은원장이어도_기사출발_이전경로를_지운다()
    {
        var now = new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);
        var selected = Delivery("one"); selected.PlannedRoute!.FromPointRoleCode = "Driver";
        selected.DriverLocationStateCode = NeighborhoodDeliveryMapStates.Available;
        selected.DriverLocation = new() { Latitude = 37.51m, Longitude = 127.12m, MeasuredAtUtc = now, ReceivedAtUtc = now };
        NeighborhoodDeliveryMapResponse Poll(NeighborhoodDeliveryRecentDriverLocation? location) => new()
        {
            RequestId = "one", RouteRevision = "v1", Pickup = Point(37.5m, 127.1m), Dropoff = Point(37.6m, 127.2m),
            DriverLocationStateCode = location is null ? NeighborhoodDeliveryMapStates.NoRecentLocation : NeighborhoodDeliveryMapStates.Available,
            DriverLocation = location
        };
        var client = new Client { Read = (_, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(selected) };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User("owner"), new Clock(now));
        await vm.InitializeAsync("mine-active", null, null);
        foreach (var location in new NeighborhoodDeliveryRecentDriverLocation?[]
        {
            null,
            new() { Latitude = 37.51m, Longitude = 127.12m, MeasuredAtUtc = now.AddMinutes(-11), ReceivedAtUtc = now },
            new() { Latitude = 37.51m, Longitude = 127.12m, MeasuredAtUtc = now, ReceivedAtUtc = now.AddMinutes(-11) },
            new() { Latitude = 37.51m, Longitude = 127.12m, MeasuredAtUtc = now.AddMinutes(1), ReceivedAtUtc = now }
        })
        {
            client.Read = (_, includeRoute) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(includeRoute ? selected : Poll(location));
            await vm.SelectDeliveryAsync("one"); Assert.Single(vm.RenderState.Routes);
            await vm.RefreshDeliveryAsync(false);
            Assert.Null(vm.Delivery!.PlannedRoute); Assert.Empty(vm.RenderState.Routes);
            Assert.DoesNotContain(vm.RenderState.Markers, pin => pin.Kind == NeighborhoodMapMarkerKinds.Driver);
        }
    }

    [Theory]
    [InlineData(11, 0)] [InlineData(0, 11)] [InlineData(-1, 0)]
    public async Task 측정시각과_서버수신시각_둘다_최근이어야_기사위치를_표시한다(int measuredAgo, int receivedAgo)
    {
        var now = new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);
        var delivery = Delivery("one"); delivery.DriverLocationStateCode = NeighborhoodDeliveryMapStates.Available;
        delivery.DriverLocation = new() { Latitude = 37.5m, Longitude = 127.1m, MeasuredAtUtc = now.AddMinutes(-measuredAgo), ReceivedAtUtc = now.AddMinutes(-receivedAgo) };
        var client = new Client { Read = (_, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(delivery) };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User("owner"), new Clock(now));
        await vm.InitializeAsync("mine-active", null, null); await vm.SelectDeliveryAsync("one");
        Assert.False(vm.HasRecentDriverLocation); Assert.DoesNotContain(vm.RenderState.Markers, pin => pin.Kind == NeighborhoodMapMarkerKinds.Driver);
    }

    [Fact]
    public async Task 종료와_레이어해제는_선택배송의_개인지도를_지운다()
    {
        var client = new Client(); using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User("owner"));
        await vm.InitializeAsync("mine-active", null, null); await vm.SelectDeliveryAsync("one");
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.Mine); Assert.Null(vm.Delivery); Assert.Empty(vm.RenderState.Markers);
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.Mine); await vm.SelectDeliveryAsync("one");
        client.Read = (_, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(new() { RequestId = "one", DriverLocationStateCode = NeighborhoodDeliveryMapStates.Closed });
        await vm.RefreshDeliveryAsync(false); Assert.Null(vm.Delivery); Assert.Empty(vm.RenderState.Routes);
        Assert.DoesNotContain(vm.Deliveries, item => item.RequestId == "one");
    }

    [Fact]
    public async Task 공공자료_연결실패는_가상의핀없이_기존글과_별도화면안내를_유지한다()
    {
        var client = new Client { PublicMap = new() { Items = [new() { Region = Region("a"), OfferCount = 1 }] } };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User());
        await vm.InitializeAsync("offer,public-data", null, null);
        Assert.NotNull(vm.PublicDataError); Assert.Empty(vm.PublicDataMarkers);
        Assert.Equal("region:a", Assert.Single(vm.RenderState.Markers).Id);
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.PublicData); Assert.Null(vm.PublicDataError);
    }

    [Fact]
    public async Task 배송선택은_전체동네가_아닌_선택배송_좌표로_화면중심을_정한다()
    {
        var client = new Client { PublicMap = new() { Items = [new() { Region = new() { RegionKey = "a", Latitude = 35, Longitude = 129 }, OfferCount = 1 }] } };
        using var vm = new NeighborhoodExchangeMapViewModel(client, new Preferences(), new User("owner"));
        await vm.InitializeAsync(null, null, null); await vm.SelectDeliveryAsync("one");
        Assert.Equal(37.55, vm.RenderState.Viewport!.Latitude, precision: 5);
        Assert.Equal(127.15, vm.RenderState.Viewport.Longitude, precision: 5);
        Assert.False(vm.RenderState.PreserveViewport);
    }

    [Theory]
    [InlineData("https://example.com/community/map")][InlineData("//example.com/community/map")][InlineData("/community/map-other")][InlineData("/community/map%2Fother")]
    public void 작성후_외부주소나_유사경로로_복귀하지않는다(string href) => Assert.Null(NeighborhoodMapNavigation.SafeReturn(href));

    private static NeighborhoodPublicRegionDto Region(string key) => new() { RegionKey = key, DisplayName = "시험 동네", Latitude = 37.5, Longitude = 127.1 };
    private static NeighborhoodDeliveryMapPoint Point(decimal latitude, decimal longitude) => new() { Latitude = latitude, Longitude = longitude };
    private static NeighborhoodDeliveryMapResponse Delivery(string id) => new()
    {
        RequestId = id, RouteRevision = "v1", Pickup = Point(37.5m, 127.1m), Dropoff = Point(37.6m, 127.2m), RouteStateCode = NeighborhoodDeliveryMapStates.Available,
        PlannedRoute = new() { StageCode = NeighborhoodDeliveryMapStages.Pickup, Points = [Point(37.5m, 127.1m), Point(37.6m, 127.2m)] }
    };
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class User(string? id = null) : ISsalddel현재사용자Context
    { public 현재사용자Snapshot Current { get; set; } = id is null ? 현재사용자Snapshot.익명 : new(id, id, []); public 현재사용자Snapshot 현재사용자 => Current; }
    private sealed class Preferences : INeighborhoodMapPreferenceStore
    {
        public NeighborhoodMapPreferences? Value { get; set; }
        public Func<Task<NeighborhoodMapPreferences?>>? Load { get; init; }
        public Task<NeighborhoodMapPreferences?> LoadAsync(string? ownerId, CancellationToken cancellationToken = default) => Load?.Invoke() ?? Task.FromResult(Value);
        public Task SaveAsync(string? ownerId, NeighborhoodMapPreferences preferences, CancellationToken cancellationToken = default) { Value = preferences; return Task.CompletedTask; }
    }
    private sealed class Client : INeighborhoodExchangeMapClient
    {
        public NeighborhoodExchangeMapResponse PublicMap { get; set; } = new();
        public int MapCalls;
        public List<bool> RouteRequests { get; } = [];
        public Func<string, bool, Task<NeighborhoodDeliveryMapResponse?>> Read { get; set; } = (id, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(Delivery(id));
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeRegionListResponse { Items = [Region("a")] });
        public Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct) { MapCalls++; return Task.FromResult(PublicMap); }
        public Task<PlatformCommunityPostListResponse> PostsAsync(string? regionKey, string? intent, int page, CancellationToken ct) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct) => Task.FromResult(new NeighborhoodMapDeliveryPage([
            new("one", "첫 배송", "진행 중", DateTime.UtcNow, new(37.5,127.1), new(37.6,127.2)), new("two", "둘째 배송", "진행 중", DateTime.UtcNow)], false));
        public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string requestId, bool includeRoute, CancellationToken ct) { RouteRequests.Add(includeRoute); return Read(requestId, includeRoute); }
    }
}
