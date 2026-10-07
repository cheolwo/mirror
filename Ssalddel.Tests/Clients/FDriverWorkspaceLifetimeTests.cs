using System.Net;
using System.Net.Http.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverWorkspaceLifetimeTests
{
    [Fact]
    public async Task HistoryRoute_KeepsForegroundWorkLocationAndRecommendations_WithoutHiddenRoutes()
    {
        var gps = new ForegroundLocationFake();
        var api = new FDriverTestWorkspaceApi
        {
            WorkStatus = "운행중",
            Workspace = _ => Task.FromResult(ActiveWorkspace(DriverWorkOfferStatus.MovingToPickup)),
            Route = _ => Task.FromResult(Road()),
            Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On })
        };
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync(); await model.StartMonitoringAsync();
            var owner = FDriverLifecycleTestSupport.WorkspaceToken(model);
            var monitor = FDriverLifecycleTestSupport.MonitorTask(model);
            model.SetWorkspacePageVisible(false);
            var routeCount = api.RouteRequests.Count;
            var locationCount = api.LocationCalls;
            gps.Emit(37.5005m, 127.0005m);
            model.IsOnDuty = true;
            // The server heartbeat interval belongs to the work lifetime, not a page.
            typeof(MainPageModel).GetField("_lastLocationSentAtUtc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(model, null);
            api.Workspace = _ => Task.FromResult(ActiveWorkspace(DriverWorkOfferStatus.MovingToDropoff, "fresh"));
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            await Task.Delay(1100); // Exercises the independent GPS/map tick while the page is hidden.

            Assert.False(owner.IsCancellationRequested);
            Assert.Same(monitor, FDriverLifecycleTestSupport.MonitorTask(model));
            Assert.Equal(DriverWorkOfferStatus.MovingToDropoff, model.ActiveDelivery!.WorkStatus);
            Assert.Equal("active", model.FocusedMapRequestId);
            Assert.Equal("fresh", Assert.Single(model.RecommendedTicketItems).TicketId);
            Assert.True(model.HasNewRecommendations);
            Assert.Equal(37.5005d, model.CurrentLocationLatitude);
            Assert.True(api.LocationCalls > locationCount);
            Assert.Equal(routeCount, api.RouteRequests.Count);
            Assert.Empty(model.SelectedRouteOverlays);
            Assert.Equal(1, gps.ListenerSubscribers);
            Assert.Equal([true], gps.PermissionRequests);
            Assert.Null(model.FoodNotificationFocus);

            model.SetWorkspacePageVisible(true);
            await model.InitializeAsync(); await model.StartMonitoringAsync();
            Assert.Same(monitor, FDriverLifecycleTestSupport.MonitorTask(model));
            Assert.Equal(1, gps.StartCalls);
            Assert.Equal(1, gps.ListenerSubscribers);
            Assert.Equal("active", model.FocusedMapRequestId);
            Assert.Contains(model.SelectedRouteOverlays, line => line.StrokeColor == "#2563EB");
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task HistoryRoute_DoesNotApplyRouteStartedBeforePageExit()
    {
        var late = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FDriverTestWorkspaceApi
        {
            Workspace = _ => Task.FromResult(ActiveWorkspace(DriverWorkOfferStatus.Accepted)),
            Route = _ => late.Task
        };
        var gps = new ForegroundLocationFake();
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync(); await model.StartMonitoringAsync();
            Assert.NotEmpty(api.RouteRequests);
            model.SetWorkspacePageVisible(false);
            late.SetResult(Road());
            await Task.Delay(30);
            Assert.Empty(model.SelectedRouteOverlays);
            Assert.True(gps.IsListening);
            Assert.False(FDriverLifecycleTestSupport.WorkspaceToken(model).IsCancellationRequested);
        }
        finally { late.TrySetResult(Road()); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task AppPauseWhileHistoryVisible_CancelsLateRefreshAndGps_ResumeRequeriesOneMonitor()
    {
        var gps = new ForegroundLocationFake(); var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync(); await model.StartMonitoringAsync(); model.SetWorkspacePageVisible(false);
            var previousMonitor = FDriverLifecycleTestSupport.MonitorTask(model);
            var late = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            api.Workspace = _ => late.Task;
            var refresh = FDriverLifecycleTestSupport.RefreshBackground(model);
            model.PauseFoodWorkspace();
            Assert.True(FDriverLifecycleTestSupport.WorkspaceToken(model).IsCancellationRequested);
            Assert.False(gps.IsListening); Assert.Equal(0, gps.ListenerSubscribers);
            gps.Emit(38m, 128m);
            late.SetResult(FDriverTestWorkspaceApi.Data("late")); await refresh;
            Assert.Equal("before", Assert.Single(model.RecommendedTicketItems).TicketId);
            Assert.False(model.HasCurrentLocation);
            api.Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data("current"));
            await model.ResumeFoodWorkspaceAsync();
            Assert.Equal("current", Assert.Single(model.RecommendedTicketItems).TicketId);
            Assert.NotSame(previousMonitor, FDriverLifecycleTestSupport.MonitorTask(model));
            Assert.True(gps.IsListening); Assert.Equal(1, gps.ListenerSubscribers);
            Assert.Equal([true, false], gps.PermissionRequests);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task OverlappingAppResume_SharesServerReloadAndDoesNotDuplicateGpsOrMonitor()
    {
        var gps = new ForegroundLocationFake(); var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync(); await model.StartMonitoringAsync(); model.SetWorkspacePageVisible(false); model.PauseFoodWorkspace();
            var calls = 0;
            var late = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            api.Workspace = _ => { calls++; return late.Task; };
            var first = model.ResumeFoodWorkspaceAsync(); var second = model.ResumeFoodWorkspaceAsync();
            Assert.Same(first, second); Assert.Equal(1, calls);
            late.SetResult(FDriverTestWorkspaceApi.Data("resumed"));
            await Task.WhenAll(first, second);
            Assert.Equal(1, gps.ListenerSubscribers); Assert.Equal(2, gps.StartCalls);
            Assert.NotNull(FDriverLifecycleTestSupport.MonitorTask(model));
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task ResumePauseResumeBeforeOldResponse_CompletesFreshOwnerInsteadOfSharingCanceledResume()
    {
        var gps = new ForegroundLocationFake(); var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync(); await model.StartMonitoringAsync();
            model.SetWorkspacePageVisible(false); model.PauseFoodWorkspace();
            var old = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            api.Workspace = _ => ++calls == 1 ? old.Task : Task.FromResult(FDriverTestWorkspaceApi.Data("fresh-owner"));
            var oldResume = model.ResumeFoodWorkspaceAsync();
            var oldLifetime = FDriverLifecycleTestSupport.WorkspaceToken(model);
            model.PauseFoodWorkspace();
            var freshResume = model.ResumeFoodWorkspaceAsync();
            Assert.NotSame(oldResume, freshResume);
            Assert.True(oldLifetime.IsCancellationRequested);
            Assert.False(freshResume.IsCompleted);
            old.SetResult(FDriverTestWorkspaceApi.Data("stale-owner"));
            await Task.WhenAll(oldResume, freshResume).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(2, calls);
            Assert.Equal("fresh-owner", Assert.Single(model.RecommendedTicketItems).TicketId);
            Assert.False(FDriverLifecycleTestSupport.WorkspaceToken(model).IsCancellationRequested);
            Assert.Equal(2, gps.StartCalls); Assert.Equal(1, gps.ListenerSubscribers);
            Assert.NotNull(FDriverLifecycleTestSupport.MonitorTask(model));
            Assert.Equal([true, false], gps.PermissionRequests);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task PageActivationCompletingAfterPause_DoesNotStartNewOwnerBeforeResumeRequery()
    {
        var gps = new ForegroundLocationFake(); var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        var old = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fresh = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        api.Workspace = _ => ++calls == 1 ? old.Task : fresh.Task;
        try
        {
            var appearing = model.ActivateWorkspacePageAsync();
            model.PauseFoodWorkspace();
            model.SetWorkspacePageVisible(false);
            var resumed = model.ResumeFoodWorkspaceAsync();
            old.SetResult(FDriverTestWorkspaceApi.Data("old"));
            await appearing.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Null(FDriverLifecycleTestSupport.MonitorTask(model)); Assert.Equal(0, gps.StartCalls);
            fresh.SetResult(FDriverTestWorkspaceApi.Data("resumed"));
            await resumed.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("resumed", Assert.Single(model.RecommendedTicketItems).TicketId);
            Assert.Equal(1, gps.StartCalls); Assert.Equal([false], gps.PermissionRequests);
            Assert.NotNull(FDriverLifecycleTestSupport.MonitorTask(model));
        }
        finally { old.TrySetResult(new()); fresh.TrySetResult(new()); await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData("test-driver", "Orderer")]
    [InlineData("other-driver", "Driver")]
    public async Task HiddenWorkspace_RoleRemovalOrAccountChange_StopsAndClearsOldOwner(string owner, string role)
    {
        var session = new FDriverTestSession(); var gps = new ForegroundLocationFake();
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(ActiveWorkspace(DriverWorkOfferStatus.Accepted)) };
        var model = FDriverLifecycleTestSupport.Model(session, api, gps);
        try
        {
            await model.InitializeAsync(); await model.StartMonitoringAsync(); model.SetWorkspacePageVisible(false);
            var lifetime = FDriverLifecycleTestSupport.WorkspaceToken(model);
            var late = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            api.Workspace = _ => late.Task;
            var refresh = FDriverLifecycleTestSupport.RefreshBackground(model);
            await session.ApplyAsync(Session(owner, role));
            Assert.True(lifetime.IsCancellationRequested);
            Assert.False(gps.IsListening); Assert.Equal(0, gps.ListenerSubscribers);
            Assert.Empty(model.ActiveDeliveryItems); Assert.Empty(model.RecommendedTicketItems);
            Assert.Null(model.ActiveDelivery); Assert.Empty(model.MapMarkers); Assert.False(model.HasCurrentLocation);
            late.SetResult(ActiveWorkspace(DriverWorkOfferStatus.MovingToDropoff, "late")); await refresh;
            Assert.Empty(model.ActiveDeliveryItems); Assert.Empty(model.RecommendedTicketItems);
            Assert.Equal(role == "Driver", model.IsAuthenticated);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task HiddenWorkspace_SessionClearStopsForegroundOwner()
    {
        var session = new FDriverTestSession(); var gps = new ForegroundLocationFake();
        var model = FDriverLifecycleTestSupport.Model(session, new(), gps);
        await model.InitializeAsync(); await model.StartMonitoringAsync(); model.SetWorkspacePageVisible(false);
        await session.ClearAsync();
        Assert.False(model.IsAuthenticated); Assert.False(gps.IsListening);
        Assert.True(FDriverLifecycleTestSupport.WorkspaceToken(model).IsCancellationRequested);
        Assert.Empty(model.RecommendedTicketItems);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task SameAccountTokenRefresh_PreservesForegroundOwnerAndOneGpsListener()
    {
        var session = new FDriverTestSession(); var gps = new ForegroundLocationFake();
        var model = FDriverLifecycleTestSupport.Model(session, new(), gps);
        try
        {
            await model.InitializeAsync(); await model.StartMonitoringAsync(); model.SetWorkspacePageVisible(false);
            var lifetime = FDriverLifecycleTestSupport.WorkspaceToken(model);
            var monitor = FDriverLifecycleTestSupport.MonitorTask(model);
            await session.ApplyAsync(Session("test-driver", "Driver"));
            Assert.False(lifetime.IsCancellationRequested);
            Assert.Same(monitor, FDriverLifecycleTestSupport.MonitorTask(model));
            Assert.Equal(1, gps.StartCalls); Assert.Equal(1, gps.ListenerSubscribers);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task InitiallyHiddenResume_DoesNotPromptForLocationPermission()
    {
        var gps = new ForegroundLocationFake();
        var model = FDriverLifecycleTestSupport.Model(new(), new(), gps);
        try
        {
            model.SetWorkspacePageVisible(false);
            await model.InitializeAsync(); await model.StartMonitoringAsync();
            Assert.Equal([false], gps.PermissionRequests);
            model.SetWorkspacePageVisible(true); await model.StartMonitoringAsync();
            Assert.Equal([false, true], gps.PermissionRequests);
            Assert.Equal(1, gps.ListenerSubscribers);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task NonDriverSession_DoesNotStartWorkspaceRequestsOrGps()
    {
        var session = new FDriverTestSession(); await session.ApplyAsync(Session("test-driver", "Orderer"));
        var gps = new ForegroundLocationFake(); var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api, gps);
        await model.InitializeAsync(); await model.StartMonitoringAsync();
        Assert.False(model.IsAuthenticated); Assert.Equal(0, api.WorkStatusCalls); Assert.Equal(0, gps.StartCalls);
        Assert.Null(FDriverLifecycleTestSupport.MonitorTask(model));
        await model.StopMonitoringAsync();
    }

    private static ClientAuthTokenSnapshot Session(string owner, string role) => new(
        "new-access", DateTime.UtcNow.AddMinutes(30), "new-refresh", DateTime.UtcNow.AddHours(1), owner, "기사", [role]);
    private static FoodDeliveryDriverWorkspaceDto ActiveWorkspace(string status, string recommendation = "before")
    {
        var workspace = FDriverTestWorkspaceApi.Data(recommendation, coordinates: true);
        workspace.Recommendations[0].ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
        workspace.Recommendations[0].AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사제안수락 }];
        workspace.ActiveDeliveries = [new()
        {
            OfferId = "active", RestaurantName = "동네식당", WorkStatus = status, DriverPayout = 4300,
            Pickup = new() { Latitude = 37.51m, Longitude = 127.01m, Label = "음식점", Address = "음식점 주소" },
            Dropoff = new() { Latitude = 37.52m, Longitude = 127.02m, Label = "전달지", Address = "전달 주소" }
        }];
        return workspace;
    }
    private static FoodDeliveryDriverRouteResponseDto Road() => new()
    {
        Source = "NaverDirections5", DistanceKm = 1, DurationMinutes = 3,
        Points = [new() { Latitude = 37.5m, Longitude = 127m }, new() { Latitude = 37.51m, Longitude = 127.01m }]
    };
    private sealed class ForegroundLocationFake : IFDriverLocationService
    {
        private EventHandler<FDriverLocationSnapshot>? _changed;
        public int ListenerSubscribers { get; private set; }
        public int StartCalls { get; private set; }
        public List<bool> PermissionRequests { get; } = [];
        public bool IsListening { get; private set; }
        public FDriverLocationSnapshot? LatestLocation { get; private set; } = new(37.5m, 127m, 5, DateTime.UtcNow);
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged
        {
            add { _changed += value; ListenerSubscribers++; }
            remove { _changed -= value; ListenerSubscribers--; }
        }
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
        {
            PermissionRequests.Add(requestPermission);
            if (!IsListening) { StartCalls++; IsListening = true; LatestLocation = new(37.5m, 127m, 5, DateTime.UtcNow); }
            return Task.FromResult(true);
        }
        public void StopListening() => IsListening = false;
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(LatestLocation);
        public void Emit(decimal latitude, decimal longitude)
        { LatestLocation = new(latitude, longitude, 5, DateTime.UtcNow); _changed?.Invoke(this, LatestLocation); }
    }

    [Fact]
    public async Task ExpiredAccessToken_TransientRefreshFailure_RemainsRecoverableInMainModel()
    {
        var session = new FDriverTestSession(expired: true);
        var recovered = false;
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("auth/refresh", StringComparison.Ordinal))
            {
                return recovered
                    ? Task.FromResult(FDriverTestHttpHandler.TokenResponse())
                    : Task.FromException<HttpResponseMessage>(new HttpRequestException("test connection failure"));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri.AbsolutePath.EndsWith("workspace", StringComparison.Ordinal)
                    ? JsonContent.Create(FDriverTestWorkspaceApi.Data("recovered"))
                    : JsonContent.Create(new 기사운행상태응답 { Status = "운행종료" })
            });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var auth = new FDriverAuthApiService(http, session);
        var model = new MainPageModel(new(), session, auth, new FoodDeliveryDriverApiService(http, session, auth),
            new FDriverTestLocationService(), new());

        await model.InitializeAsync();

        Assert.True(model.IsAuthenticated);
        Assert.Equal(ClientAuthSessionRestoreState.RefreshRequired, session.CurrentState);
        Assert.Equal(0, session.ClearCount);
        recovered = true;
        await FDriverLifecycleTestSupport.RefreshBackground(model);
        Assert.True(model.IsAuthenticated);
        Assert.True(session.IsAuthenticated);
        Assert.Equal("recovered", Assert.Single(model.RecommendedTicketItems).TicketId);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task BackgroundTransientFailure_PreservesLoginAndPreviousWorkspace_ThenRecovers()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        api.Workspace = _ => Task.FromException<FoodDeliveryDriverWorkspaceDto>(new FDriverApiException("test connection failure", null));

        await FDriverLifecycleTestSupport.RefreshBackground(model);

        Assert.True(model.IsAuthenticated);
        Assert.Equal(0, session.ClearCount);
        Assert.Equal("before", Assert.Single(model.RecommendedTicketItems).TicketId);
        Assert.True(model.HasWorkspaceWarning);
        api.Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data("recovered"));

        await FDriverLifecycleTestSupport.RefreshBackground(model);

        Assert.Equal("recovered", Assert.Single(model.RecommendedTicketItems).TicketId);
        Assert.False(model.HasWorkspaceWarning);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task StopMonitoring_CancelsAndJoinsInFlightRefresh_IgnoresLateWorkspace_AndAllowsFreshEntry()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        var late = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestedToken = default;
        api.Workspace = token => { requestedToken = token; return late.Task; };
        var refresh = FDriverLifecycleTestSupport.RefreshBackground(model);
        // Represents the monitor awaiting its production refresh callback; avoids a 10-second timer delay.
        FDriverLifecycleTestSupport.MonitorTask(model, refresh);

        var stop = model.StopMonitoringAsync();

        Assert.True(requestedToken.IsCancellationRequested);
        Assert.False(stop.IsCompleted);
        late.SetResult(FDriverTestWorkspaceApi.Data("late"));
        await stop.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("before", Assert.Single(model.RecommendedTicketItems).TicketId);
        Assert.Null(FDriverLifecycleTestSupport.MonitorTask(model));

        api.Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data("fresh"));
        await model.InitializeAsync();
        Assert.Equal("fresh", Assert.Single(model.RecommendedTicketItems).TicketId);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task Logout_JoinsInFlightRefresh_BeforeClearingSession_AndDoesNotRepopulateWorkspace()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        var late = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Workspace = _ => late.Task;
        var refresh = FDriverLifecycleTestSupport.RefreshBackground(model);
        FDriverLifecycleTestSupport.MonitorTask(model, refresh);

        var logout = model.LogoutCommand.ExecuteAsync(null);

        Assert.False(logout.IsCompleted);
        Assert.Equal(0, session.ClearCount);
        late.SetResult(FDriverTestWorkspaceApi.Data("late"));
        await logout.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, session.ClearCount);
        Assert.False(model.IsAuthenticated);
        Assert.Empty(model.RecommendedTicketItems);
        Assert.Null(model.SelectedTicket);

        api.Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data("new-login"));
        model.LoginId = "test-driver";
        model.Password = "test-password";
        await model.LoginCommand.ExecuteAsync(null);
        Assert.True(model.IsAuthenticated);
        Assert.Equal("new-login", Assert.Single(model.RecommendedTicketItems).TicketId);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task BackgroundUnauthorized_ClearsWorkspaceWithoutAwaitingItsOwnMonitorTask()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        var response = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Workspace = _ => response.Task;
        var refresh = FDriverLifecycleTestSupport.RefreshBackground(model);
        FDriverLifecycleTestSupport.MonitorTask(model, refresh);

        response.SetException(new FDriverApiException("test unauthorized", HttpStatusCode.Unauthorized));
        await refresh.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, session.ClearCount);
        Assert.False(model.IsAuthenticated);
        Assert.Empty(model.RecommendedTicketItems);
        Assert.True(FDriverLifecycleTestSupport.WorkspaceToken(model).IsCancellationRequested);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task Unauthorized_WhenDeviceRemovalFailsAfterMemoryClear_StillClearsPrivateUi()
    {
        var session = new FDriverTestSession { ClearFailure = new InvalidOperationException("test storage failure") };
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        model.HasCurrentLocation = true;
        model.CurrentLocationLatitude = 37.5d;
        model.CurrentLocationLongitude = 127d;
        api.Workspace = _ => Task.FromException<FoodDeliveryDriverWorkspaceDto>(new FDriverApiException("test unauthorized", HttpStatusCode.Unauthorized));

        await FDriverLifecycleTestSupport.RefreshBackground(model);

        Assert.False(model.IsAuthenticated);
        Assert.Empty(model.RecommendedTicketItems);
        Assert.Empty(model.MapMarkers);
        Assert.False(model.HasCurrentLocation);
        Assert.Equal(0d, model.CurrentLocationLatitude);
        Assert.Equal(0d, model.CurrentLocationLongitude);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task InitializationCompletingAfterPageExit_DoesNotAuthenticateOrStartHiddenMonitor()
    {
        var restore = new TaskCompletionSource<ClientAuthSessionRestoreState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FDriverTestSession { Restore = () => restore.Task };
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        var initialize = model.InitializeAsync();

        await model.StopMonitoringAsync();
        restore.SetResult(ClientAuthSessionRestoreState.Authenticated);
        await initialize.WaitAsync(TimeSpan.FromSeconds(2));
        await model.StartMonitoringAsync();

        Assert.False(model.IsAuthenticated);
        Assert.Equal(0, api.WorkStatusCalls);
        Assert.Null(FDriverLifecycleTestSupport.MonitorTask(model));
    }

    [Fact]
    public async Task ManualRefreshCompletingAfterPageExit_DoesNotReplaceWorkspace_AndReleasesBusyState()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        var late = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Workspace = _ => late.Task;
        var refresh = model.RefreshCommand.ExecuteAsync(null);

        await model.StopMonitoringAsync();
        late.SetResult(FDriverTestWorkspaceApi.Data("late"));
        await refresh.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("before", Assert.Single(model.RecommendedTicketItems).TicketId);
        Assert.False(model.IsBusy);
        Assert.False(model.IsRefreshing);
    }

    [Fact]
    public async Task RouteCompletingAfterPageExit_DoesNotApplyLateOverlay()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data("before", coordinates: true)) };
        var model = FDriverLifecycleTestSupport.Model(session, api, new FDriverFixedTestLocationService());
        await model.InitializeAsync();
        await model.ResumeFoodMapAsync();
        var late = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Route = _ => late.Task;
        var select = model.SelectTicketCommand.ExecuteAsync(Assert.Single(model.RecommendedTicketItems));
        Assert.True(api.RouteRequests.Count > 1);
        var previousRouteText = model.RouteStatusText;

        await model.StopMonitoringAsync();
        late.SetResult(new()
        {
            DistanceKm = 999, DurationMinutes = 999,
            Points = [new() { Latitude = 37.5m, Longitude = 127m }, new() { Latitude = 37.6m, Longitude = 127.1m }]
        });
        await select.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(model.SelectedRouteOverlays);
        Assert.Equal(previousRouteText, model.RouteStatusText);
    }
}
