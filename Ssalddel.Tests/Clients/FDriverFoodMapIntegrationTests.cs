using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverFoodMapIntegrationTests
{
    [Theory]
    [InlineData(DriverWorkOfferStatus.MovingToDropoff)]
    [InlineData(DriverWorkOfferStatus.PickupConfirmed)]
    public async Task StageAndFocus_UseOneTargetAndGrayReferencesBelowCurrentRoute(string deliveryStatus)
    {
        var stage = DriverWorkOfferStatus.MovingToPickup;
        var gps = new LocationFake();
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(Workspace(stage)), Route = _ => Task.FromResult(Road()) };
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync();
            await model.ResumeFoodMapAsync();
            await Until(() => model.SelectedRouteOverlays.Count == 2);
            Assert.Equal("first", model.FocusedMapRequestId);
            Assert.Equal("#64748B", model.SelectedRouteOverlays[0].StrokeColor);
            Assert.Equal("#F57C00", model.SelectedRouteOverlays[1].StrokeColor);
            Assert.All(api.RouteRequests, request => Assert.Single(request.Stops));
            var focusedRequest = Assert.Single(api.RouteRequests.Where(request => request.Stops[0].Latitude == 37.51m));
            Assert.Equal(gps.LatestLocation!.Latitude, focusedRequest.StartLatitude);
            stage = deliveryStatus;
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            await Until(() => model.SelectedRouteOverlays.LastOrDefault()?.StrokeColor == "#2563EB");
            Assert.Equal(37.52m, api.RouteRequests.Last().Stops[0].Latitude);
            Assert.Contains("전달", model.RouteStatusText);
            var second = model.ActiveDeliveryItems.Single(x => x.OfferId == "second");
            await model.SelectActiveDeliveryCommand.ExecuteAsync(second);
            await Until(() => model.SelectedRouteOverlays.LastOrDefault()?.RouteId == "second");
            Assert.Equal("second", model.FocusedMapRequestId);
            Assert.Equal("#F57C00", model.SelectedRouteOverlays.Last().StrokeColor);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task SlowRoute_DoesNotBlockGpsOrApplyAfterForegroundExit()
    {
        var late = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gps = new LocationFake();
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(Workspace()), Route = _ => late.Task };
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync(); await model.ResumeFoodMapAsync();
            await Until(() => api.RouteRequests.Count > 0);
            gps.Emit(37.5001m, 127.0001m);
            Assert.Equal(37.5001d, model.CurrentLocationLatitude);
            Assert.False(model.IsBusy);
            model.IsFollowingCurrentLocation = false; model.MapZoom = 17;
            model.PauseFoodMap();
            Assert.False(gps.IsListening);
            gps.Emit(38m, 128m);
            late.SetResult(Road());
            await Task.Delay(30);
            Assert.False(model.HasCurrentLocation);
            Assert.Empty(model.SelectedRouteOverlays);
            Assert.Equal(37.5001d, model.CurrentLocationLatitude);
            Assert.Equal(17, model.MapZoom);
        }
        finally { late.TrySetResult(Road()); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task EstimatedResponse_ShowsPinsAndFailureWithoutFakeSolidRoute()
    {
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(Workspace()), Route = _ => Task.FromResult(Road(estimated: true)) };
        var model = FDriverLifecycleTestSupport.Model(new(), api, new LocationFake());
        try
        {
            await model.InitializeAsync(); await model.ResumeFoodMapAsync();
            await Until(() => model.RouteStatusText.Contains("다시 조회"));
            Assert.Equal(2, model.MapMarkers.Count);
            Assert.Empty(model.SelectedRouteOverlays);
            Assert.False(model.IsBusy);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task Resume_RestartsOneListenerAndRecenterPreservesZoom()
    {
        var gps = new LocationFake();
        var model = FDriverLifecycleTestSupport.Model(new(), new(), gps);
        try
        {
            await model.InitializeAsync(); await model.ResumeFoodMapAsync(); await model.ResumeFoodMapAsync();
            Assert.Equal(1, gps.StartCalls);
            model.MapZoom = 16; model.IsFollowingCurrentLocation = false;
            model.RecenterMapCommand.Execute(null);
            Assert.True(model.IsFollowingCurrentLocation);
            Assert.Equal(16, model.MapZoom);
            model.PauseFoodMap(); await model.ResumeFoodMapAsync();
            Assert.Equal(2, gps.StartCalls);
            Assert.True(model.HasCurrentLocation);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task PermissionDenied_ResumeDoesNotPromptAgainAndUserCanRetryExplicitly()
    {
        var gps = new DeniedLocationFake();
        var model = FDriverLifecycleTestSupport.Model(new(), new(), gps);
        try
        {
            await model.InitializeAsync(); await model.ResumeFoodMapAsync();
            Assert.Equal([true], gps.PermissionRequests);
            Assert.True(model.HasMapLocationWarning);
            Assert.Equal(0, gps.CurrentCalls);
            model.PauseFoodMap(); await model.ResumeFoodMapAsync();
            Assert.Equal([true, false], gps.PermissionRequests);
            await model.RetryMapLocationCommand.ExecuteAsync(null);
            Assert.Equal([true, false, true], gps.PermissionRequests);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    private sealed class DeniedLocationFake : IFDriverLocationService
    {
        public List<bool> PermissionRequests { get; } = [];
        public int CurrentCalls { get; private set; }
        public bool IsListening => false;
        public FDriverLocationSnapshot? LatestLocation => null;
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged { add { } remove { } }
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
        { PermissionRequests.Add(requestPermission); return Task.FromResult(false); }
        public void StopListening() { }
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
        { CurrentCalls++; return Task.FromResult<FDriverLocationSnapshot?>(null); }
    }

    private static FoodDeliveryDriverWorkspaceDto Workspace(string stage = DriverWorkOfferStatus.MovingToPickup) => new()
    {
        DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
        ActiveDeliveries = [Active("first", stage, 37.51m), Active("second", DriverWorkOfferStatus.Accepted, 37.53m)]
    };
    private static FoodDeliveryDriverActiveDeliveryDto Active(string id, string stage, decimal latitude) => new()
    {
        OfferId = id, RestaurantName = id, WorkStatus = stage, DriverPayout = 4300,
        Pickup = new() { Latitude = latitude, Longitude = 127.01m, Label = "음식점", Address = "검증 주소" },
        Dropoff = new() { Latitude = latitude + 0.01m, Longitude = 127.02m, Label = "전달지", Address = "검증 전달 주소" }
    };
    private static FoodDeliveryDriverRouteResponseDto Road(bool estimated = false) => new()
    {
        Source = estimated ? "CoordinateEstimate" : "NaverDirections5", IsEstimated = estimated, DistanceKm = 1, DurationMinutes = 3,
        Points = [new() { Latitude = 37.5m, Longitude = 127m }, new() { Latitude = 37.51m, Longitude = 127.01m }]
    };
    private static async Task Until(Func<bool> condition)
    {
        var end = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < end) await Task.Delay(10);
        Assert.True(condition());
    }
    private sealed class LocationFake : IFDriverLocationService
    {
        public bool IsListening { get; private set; }
        public int StartCalls { get; private set; }
        public FDriverLocationSnapshot? LatestLocation { get; private set; } = new(37.5m, 127m, 5, DateTime.UtcNow);
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged;
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
        { StartCalls++; IsListening = true; LatestLocation = new(37.5m, 127m, 5, DateTime.UtcNow); return Task.FromResult(true); }
        public void StopListening() => IsListening = false;
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(LatestLocation);
        public void Emit(decimal latitude, decimal longitude)
        { LatestLocation = new(latitude, longitude, 5, DateTime.UtcNow); LocationChanged?.Invoke(this, LatestLocation); }
    }
}
