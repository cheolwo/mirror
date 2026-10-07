using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMapWorkspaceViewModelTests
{
    [Fact]
    public async Task 입력페이지를_다녀온_같은계정은_카메라와_목록과_협업문맥을_복원한다()
    {
        var session = new NeighborhoodMapWorkspaceSession();
        var user = new User("owner"); var client = new Client(); var preferences = new Preferences();
        var camera = new NeighborhoodMapViewport(37.49, 127.09, 15);
        using (var first = Vm(client, preferences, user, session))
        {
            await first.InitializeAsync("offer,storage", "map", "a");
            await first.LoadPostsAsync(3);
            first.SelectPanel("space", "space-1", "work-1"); first.SetPanelScope("space");
            first.RecordListContext("work", 4, "undertaken");
            first.RecordListContext("delivery", 2); first.RecordListContext("space", 5, "mine");
            first.SetViewport(camera);
        }
        using var next = Vm(client, preferences, user, session);
        await next.InitializeAsync(null, null, null);
        Assert.Equal(camera, next.RenderState.Viewport); Assert.True(next.RenderState.PreserveViewport);
        Assert.Equal(3, next.Page); Assert.Equal("a", next.RegionKey); Assert.Equal("space", next.PanelKind);
        Assert.Equal("space-1", next.PanelTarget); Assert.Equal("work-1", next.PanelRelated); Assert.Equal("space", next.PanelScope);
        Assert.Equal(4, next.WorkListPage); Assert.Equal("undertaken", next.WorkListScope);
        Assert.Equal(2, next.DeliveryListPage); Assert.Equal(5, next.SpaceListPage); Assert.True(next.SpaceListMine);
        Assert.DoesNotContain("37.49", next.HomeHref); Assert.DoesNotContain("127.09", next.HomeHref);
        Assert.DoesNotContain("work-1", preferences.Saved?.ToString() ?? "");
    }

    [Fact]
    public async Task 공개패널과_목록페이지만_바꿀때는_지도장면을_다시만들지않는다()
    {
        using var vm = Vm(new(), new(), new(), new());
        await vm.InitializeAsync("offer", null, "a");
        vm.SetViewport(new(37.51, 127.11, 15));
        var revision = vm.RenderState.Revision;
        vm.SelectPanel("post", "42"); vm.SelectPanel("work", "work-1");
        vm.SetPanelScope("delivery"); vm.RecordListContext("delivery", 3);
        await vm.LoadPostsAsync(2);
        Assert.Equal(revision, vm.RenderState.Revision);
        Assert.Equal(new NeighborhoodMapViewport(37.51, 127.11, 15), vm.RenderState.Viewport);
        vm.InvalidateMap(); Assert.True(vm.RenderState.Revision > revision);
    }

    [Fact]
    public async Task 지역이_검증되면_다른레이어는_지도응답을_기다리지않고_각각_조회한다()
    {
        var map = new TaskCompletionSource<NeighborhoodExchangeMapResponse>();
        var posts = new TaskCompletionSource<PlatformCommunityPostListResponse>();
        var publicData = new TaskCompletionSource<RegionalAgriculturalMapMarkerListResponse?>();
        var storage = new TaskCompletionSource<NeighborhoodStorageMapResponse?>();
        var mine = new TaskCompletionSource<NeighborhoodMapDeliveryPage>();
        var client = new Client
        {
            MapRead = _ => map.Task, PostRead = (_, _, _) => posts.Task,
            PublicDataRead = _ => publicData.Task, StorageRead = _ => storage.Task, MineRead = (_, _) => mine.Task
        };
        using var vm = Vm(client, new(), new("owner"), new());
        var pending = vm.InitializeAsync("offer,mine-active,public-data,storage", null, "a");
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, client.MapCalls); Assert.Equal(1, client.PostCalls); Assert.Equal(1, client.PublicDataCalls);
        Assert.Equal(1, client.StorageCalls); Assert.Equal(1, client.MineCalls);
        posts.SetResult(new()); publicData.SetResult(null); storage.SetResult(null); mine.SetResult(new([], false));
        Assert.False(pending.IsCompleted);
        map.SetResult(new()); await pending;
        Assert.False(vm.IsLoading); Assert.NotNull(vm.PublicDataError); Assert.NotNull(vm.StorageError);
    }

    [Fact]
    public async Task 레이어해제는_진행중_조회토큰을_취소하고_늦은핀을_차단한다()
    {
        var delayed = new TaskCompletionSource<NeighborhoodStorageMapResponse?>();
        CancellationToken token = default;
        var client = new Client { StorageRead = value => { token = value; return delayed.Task; } };
        using var vm = Vm(client, new(), new(), new());
        var initial = vm.InitializeAsync("storage", null, "a");
        await vm.ToggleLayerAsync(NeighborhoodMapNavigation.Storage);
        Assert.True(token.IsCancellationRequested);
        delayed.SetResult(new() { Items = [new() { Region = Region(), SpaceCount = 9 }] }); await initial;
        Assert.Empty(vm.StorageMarkers); Assert.Empty(vm.RenderState.Markers); Assert.False(vm.StorageLoading);
    }

    [Fact]
    public async Task 개인선택복귀는_새_원장권한확인전에는_기존정확카메라를_사용하지않는다()
    {
        var session = new NeighborhoodMapWorkspaceSession(); var user = new User("owner"); var client = new Client();
        var privateCamera = new NeighborhoodMapViewport(37.55, 127.15, 16);
        using (var first = Vm(client, new(), user, session))
        {
            await first.InitializeAsync("mine-active", null, null); await first.SelectDeliveryAsync("one");
            first.SetViewport(privateCamera);
        }
        var read = new TaskCompletionSource<NeighborhoodDeliveryMapResponse?>();
        client.DeliveryRead = (_, _) => read.Task;
        using var next = Vm(client, new(), user, session);
        var pending = next.InitializeAsync(null, null, null);
        Assert.Null(next.Delivery); Assert.Null(next.RenderState.Viewport); Assert.Empty(next.RenderState.Routes);
        read.SetResult(Delivery()); await pending;
        Assert.Equal("one", next.Delivery!.RequestId); Assert.Equal(privateCamera, next.RenderState.Viewport);
        Assert.True(next.RenderState.PreserveViewport);
    }

    [Fact]
    public async Task 개인권한조회실패와_다른계정에서는_정확카메라를_복원하지않는다()
    {
        var session = new NeighborhoodMapWorkspaceSession(); var user = new User("owner"); var client = new Client();
        using (var first = Vm(client, new(), user, session))
        {
            await first.InitializeAsync("mine-active", null, null); await first.SelectDeliveryAsync("one"); first.SetViewport(new(37.55, 127.15, 16));
        }
        client.DeliveryRead = (_, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(null);
        using (var denied = Vm(client, new(), user, session))
        {
            await denied.InitializeAsync(null, null, null);
            Assert.Null(denied.Delivery); Assert.Null(denied.RenderState.Viewport); Assert.Empty(denied.RenderState.Routes);
        }
        user.Current = new("other", "다른 계정", []);
        using var next = Vm(client, new(), user, session);
        await next.InitializeAsync("none", null, null);
        Assert.Null(next.SelectedRequestId); Assert.Null(next.PanelTarget); Assert.Null(next.RenderState.Viewport);
    }

    [Fact]
    public async Task 앱정지는_개인지도응답을_지우고_재개할_고유번호만_보존한다()
    {
        var session = new NeighborhoodMapWorkspaceSession(); var client = new Client();
        using var vm = Vm(client, new(), new("owner"), session);
        await vm.InitializeAsync("mine-active", null, null); await vm.SelectDeliveryAsync("one"); vm.SetViewport(new(37.55, 127.15, 16));
        vm.SuspendPrivate();
        Assert.Equal("one", vm.SelectedRequestId); Assert.Equal("delivery", vm.PanelKind);
        Assert.Null(vm.Delivery); Assert.Empty(vm.Deliveries); Assert.Empty(vm.RenderState.Markers); Assert.Null(vm.RenderState.Viewport);
        Assert.Equal("one", session.Read("owner")!.SelectedRequestId); Assert.Null(session.Read("owner")!.Viewport);
        vm.InvalidateMap();
        await vm.LoadMineAsync(); await vm.SelectDeliveryAsync("one");
        Assert.NotNull(vm.Delivery); Assert.Single(vm.RenderState.Routes);
    }

    [Fact]
    public async Task 앱중단중_주기조회와_늦은카메라callback은_개인응답을_다시채우지않는다()
    {
        var session = new NeighborhoodMapWorkspaceSession(); var client = new Client();
        using var vm = Vm(client, new(), new("owner"), session);
        await vm.InitializeAsync("mine-active", null, null); await vm.SelectDeliveryAsync("one"); vm.SetViewport(new(37.55, 127.15, 16));
        var mineCalls = client.MineCalls; var deliveryCalls = 0;
        client.DeliveryRead = (_, _) => { ++deliveryCalls; return Task.FromResult<NeighborhoodDeliveryMapResponse?>(Delivery()); };
        vm.SuspendPrivate();
        await vm.RefreshDeliveryAsync(false); await vm.LoadMineAsync(); await vm.SelectDeliveryAsync("one", requireKnown: false);
        await vm.RefreshPublicAsync();
        vm.SetViewport(new(37.55, 127.15, 16));
        Assert.Equal(0, deliveryCalls); Assert.Equal(mineCalls, client.MineCalls);
        Assert.Null(vm.Delivery); Assert.Empty(vm.Deliveries); Assert.Empty(vm.RenderState.Routes);
        Assert.Null(vm.RenderState.Viewport); Assert.Null(session.Read("owner")!.Viewport);
        vm.InvalidateMap(); await vm.LoadMineAsync(); await vm.SelectDeliveryAsync("one");
        Assert.Equal(1, deliveryCalls); Assert.NotNull(vm.Delivery);
    }

    [Fact]
    public async Task 배송을_선택하지않아도_개인배송핀을_본_카메라는_앱정지때_제거한다()
    {
        var client = new Client { MineRead = (_, _) => Task.FromResult(new NeighborhoodMapDeliveryPage(
            [new("one", "물품", "진행 중", DateTime.UtcNow, new(37.55, 127.15))], false)) };
        var session = new NeighborhoodMapWorkspaceSession();
        using var vm = Vm(client, new(), new("owner"), session);
        await vm.InitializeAsync("mine-active", null, null); vm.SetViewport(new(37.55, 127.15, 16));
        Assert.True(session.Read("owner")!.PrivateViewport);
        vm.SuspendPrivate();
        Assert.Empty(vm.RenderState.Markers); Assert.Null(vm.RenderState.Viewport); Assert.Null(session.Read("owner")!.Viewport);
    }

    [Fact]
    public async Task 계속_권한있는_회색배송핀_카메라는_글과_협업패널만_전환할때_유지한다()
    {
        var client = new Client { MineRead = (_, _) => Task.FromResult(new NeighborhoodMapDeliveryPage(
            [new("one", "물품", "진행 중", DateTime.UtcNow, new(37.55, 127.15))], false)) };
        var session = new NeighborhoodMapWorkspaceSession(); var camera = new NeighborhoodMapViewport(37.55, 127.15, 16);
        using var vm = Vm(client, new(), new("owner"), session);
        await vm.InitializeAsync("mine-active", null, null); vm.SetViewport(camera);
        var revision = vm.RenderState.Revision;
        vm.SelectPanel("post", "42"); vm.SelectPanel("work", "work-1");
        Assert.Equal(revision, vm.RenderState.Revision); Assert.Equal(camera, vm.RenderState.Viewport);
        Assert.Equal(camera, session.Read("owner")!.Viewport); Assert.True(session.Read("owner")!.PrivateViewport);
        Assert.Contains(vm.RenderState.Markers, item => item.Kind == NeighborhoodMapMarkerKinds.Inactive);
        vm.SuspendPrivate(); Assert.Null(vm.RenderState.Viewport); Assert.Null(session.Read("owner")!.Viewport);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 본인배송_조회권한을_잃으면_회색배송핀과_그_카메라도_제거한다(int status)
    {
        var client = new Client { MineRead = (_, _) => Task.FromResult(new NeighborhoodMapDeliveryPage(
            [new("one", "물품", "진행 중", DateTime.UtcNow, new(37.55, 127.15))], false)) };
        var session = new NeighborhoodMapWorkspaceSession();
        using var vm = Vm(client, new(), new("owner"), session);
        await vm.InitializeAsync("mine-active", null, null); vm.SetViewport(new(37.55, 127.15, 16)); vm.SelectPanel("post", "42");
        client.MineRead = (_, _) => Task.FromException<NeighborhoodMapDeliveryPage>(new SsalddelApiException("private", status, "test", "", null));
        await vm.LoadMineAsync();
        Assert.Empty(vm.Deliveries); Assert.Empty(vm.RenderState.Markers); Assert.Null(vm.RenderState.Viewport);
        Assert.Null(session.Read("owner")!.Viewport); Assert.False(session.Read("owner")!.PrivateViewport);
        Assert.NotNull(vm.MineError);
    }

    [Fact]
    public async Task 새_URL의_패널과_지역이_이전작업공간보다_우선한다()
    {
        var session = new NeighborhoodMapWorkspaceSession();
        using (var first = Vm(new(), new(), new("owner"), session))
        {
            await first.InitializeAsync("offer", null, "a", "space", "space-old", "work-old"); first.SetViewport(new(37.49, 127.09, 15));
        }
        using var next = Vm(new(), new(), new("owner"), session);
        await next.InitializeAsync("none", "list", "invalid", "post", "52", "ignored");
        Assert.Empty(next.SelectedLayers); Assert.Equal("list", next.ViewMode); Assert.Null(next.RegionKey);
        Assert.Equal("post", next.PanelKind); Assert.Equal("52", next.PanelTarget); Assert.Null(next.PanelRelated);
        Assert.False(next.RenderState.PreserveViewport);
    }

    [Fact]
    public async Task 명시한_새공간과_빠진대상은_이전대상이나_협업번호를_이어받지않는다()
    {
        using var vm = Vm(new(), new(), new("owner"), new());
        await vm.InitializeAsync("offer", "map", "a", "space", "space-old", "work-old");
        await vm.InitializeAsync("offer", "map", "a", "space", "space-new");
        Assert.Equal("space-new", vm.PanelTarget); Assert.Null(vm.PanelRelated);
        await vm.InitializeAsync("offer", "map", "a", "space");
        Assert.Null(vm.PanelTarget); Assert.Null(vm.PanelRelated);
        await vm.InitializeAsync("offer", "map", "a", "work", "work-new");
        await vm.InitializeAsync("offer", "map", "a", "post");
        Assert.Null(vm.PanelTarget); Assert.Null(vm.PanelRelated);
    }

    [Fact]
    public async Task 패널쿼리를_제거한_지도URL은_현재상세를_닫는다()
    {
        using var vm = Vm(new(), new(), new("owner"), new());
        await vm.InitializeAsync("offer", "map", "a", "work", "work-old");
        await vm.InitializeAsync("offer", "map", "a");
        Assert.Null(vm.PanelKind); Assert.Null(vm.PanelTarget); Assert.DoesNotContain("panel=", vm.HomeHref);
    }

    [Fact]
    public async Task 배송목록의_다른페이지에서_연_패널은_서버권한을_새로확인하여_지도를_조회한다()
    {
        var reads = new List<string>();
        var client = new Client { DeliveryRead = (id, _) => { reads.Add(id); return Task.FromResult<NeighborhoodDeliveryMapResponse?>(Delivery(id)); } };
        using var vm = Vm(client, new(), new("owner"), new());
        await vm.InitializeAsync("mine-active", null, null, "delivery", "page-three");
        Assert.Equal("page-three", vm.Delivery!.RequestId); Assert.Single(vm.RenderState.Routes);
        Assert.Contains(vm.RenderState.Markers, item => item.Id == "delivery:page-three:pickup");
        await vm.InitializeAsync("mine-active", null, null, "delivery", "page-four");
        Assert.Equal("page-four", vm.Delivery!.RequestId); Assert.Equal(new[] { "page-three", "page-four" }, reads);
        await vm.SelectDeliveryAsync("unknown-marker");
        Assert.Equal(2, reads.Count); Assert.Equal("page-four", vm.Delivery.RequestId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("different")]
    [InlineData("closed")]
    [InlineData("forbidden")]
    public async Task 다른배송_패널의_실패응답은_기존배송의_좌표와_카메라를_남기지않는다(string failure)
    {
        var client = new Client(); using var vm = Vm(client, new(), new("owner"), new());
        await vm.InitializeAsync("mine-active", null, null); await vm.SelectDeliveryAsync("one"); vm.SetViewport(new(37.55, 127.15, 16));
        client.DeliveryRead = (_, _) => failure switch
        {
            "missing" => Task.FromResult<NeighborhoodDeliveryMapResponse?>(null),
            "different" => Task.FromResult<NeighborhoodDeliveryMapResponse?>(Delivery("someone-else")),
            "closed" => Task.FromResult<NeighborhoodDeliveryMapResponse?>(new() { RequestId = "new-id", RouteStateCode = NeighborhoodDeliveryMapStates.Closed }),
            _ => Task.FromException<NeighborhoodDeliveryMapResponse?>(new SsalddelApiException("private", 403, "test", "", null))
        };
        await vm.InitializeAsync("mine-active", null, null, "delivery", "new-id");
        Assert.Null(vm.Delivery); Assert.Null(vm.RenderState.Viewport); Assert.Empty(vm.RenderState.Routes);
        Assert.DoesNotContain(vm.RenderState.Markers, item => item.Kind == NeighborhoodMapMarkerKinds.Pickup);
        Assert.NotNull(vm.DeliveryError);
    }

    private static NeighborhoodExchangeMapViewModel Vm(Client client, Preferences preferences, User user, NeighborhoodMapWorkspaceSession session)
        => new(client, preferences, user, session: session);
    private static NeighborhoodPublicRegionDto Region() => new() { RegionKey = "a", DisplayName = "동네", Latitude = 37.5, Longitude = 127.1 };
    private static NeighborhoodDeliveryMapResponse Delivery(string id = "one") => new()
    {
        RequestId = id, RouteRevision = "1", RouteStateCode = NeighborhoodDeliveryMapStates.Available,
        Pickup = new() { Latitude = 37.5m, Longitude = 127.1m }, Dropoff = new() { Latitude = 37.6m, Longitude = 127.2m },
        PlannedRoute = new() { StageCode = NeighborhoodDeliveryMapStages.Pickup,
            Points = [new() { Latitude = 37.5m, Longitude = 127.1m }, new() { Latitude = 37.6m, Longitude = 127.2m }] }
    };
    private sealed class User(string? id = null) : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot Current { get; set; } = id is null ? 현재사용자Snapshot.익명 : new(id, id, []);
        public 현재사용자Snapshot 현재사용자 => Current;
    }
    private sealed class Preferences : INeighborhoodMapPreferenceStore
    {
        public NeighborhoodMapPreferences? Saved { get; private set; }
        public Task<NeighborhoodMapPreferences?> LoadAsync(string? ownerId, CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public Task SaveAsync(string? ownerId, NeighborhoodMapPreferences preferences, CancellationToken cancellationToken = default) { Saved = preferences; return Task.CompletedTask; }
    }
    private sealed class Client : INeighborhoodExchangeMapClient
    {
        public int MapCalls, PostCalls, MineCalls, StorageCalls, PublicDataCalls;
        public Func<CancellationToken, Task<NeighborhoodExchangeMapResponse>> MapRead { get; set; } = _ => Task.FromResult(new NeighborhoodExchangeMapResponse());
        public Func<string?, int, CancellationToken, Task<PlatformCommunityPostListResponse>> PostRead { get; set; } = (_, _, _) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Func<int, CancellationToken, Task<NeighborhoodMapDeliveryPage>> MineRead { get; set; } = (_, _) => Task.FromResult(new NeighborhoodMapDeliveryPage([new("one", "물품", "진행 중", DateTime.UtcNow)], false));
        public Func<string, bool, Task<NeighborhoodDeliveryMapResponse?>> DeliveryRead { get; set; } = (_, _) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(Delivery());
        public Func<CancellationToken, Task<RegionalAgriculturalMapMarkerListResponse?>> PublicDataRead { get; set; } = _ => Task.FromResult<RegionalAgriculturalMapMarkerListResponse?>(null);
        public Func<CancellationToken, Task<NeighborhoodStorageMapResponse?>> StorageRead { get; set; } = _ => Task.FromResult<NeighborhoodStorageMapResponse?>(null);
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeRegionListResponse { Items = [Region()] });
        public Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct) { MapCalls++; return MapRead(ct); }
        public Task<PlatformCommunityPostListResponse> PostsAsync(string? regionKey, string? intent, int page, CancellationToken ct) { PostCalls++; return PostRead(regionKey, page, ct); }
        public Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct) { MineCalls++; return MineRead(page, ct); }
        public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string requestId, bool includeRoute, CancellationToken ct) => DeliveryRead(requestId, includeRoute);
        public Task<RegionalAgriculturalMapMarkerListResponse?> PublicDataAsync(CancellationToken ct) { PublicDataCalls++; return PublicDataRead(ct); }
        public Task<NeighborhoodStorageMapResponse?> StorageAsync(CancellationToken ct) { StorageCalls++; return StorageRead(ct); }
    }
}
