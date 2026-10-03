using System.Net;
using System.Net.Http.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverWorkspaceLifetimeTests
{
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
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        var late = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Route = _ => late.Task;
        var select = model.SelectTicketCommand.ExecuteAsync(Assert.Single(model.RecommendedTicketItems));
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
