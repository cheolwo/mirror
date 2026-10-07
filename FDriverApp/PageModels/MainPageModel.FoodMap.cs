using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private FDriverMapRouteState _foodMapRoutes = null!;
    private CancellationTokenSource? _foodMapCancellation;
    private Task? _foodMapTask;
    private Task? _foodMapListenerTask;
    private DateTime _foodMapListenerRetryAtUtc;
    private long _foodMapLifetime;
    private bool _foodMapForeground = true;
    private bool _foodMapPermissionRequested;
    private FDriverLocationSnapshot? _foodMapLocation;
    private EventHandler<FDriverLocationSnapshot>? _foodMapLocationHandler;
    private EventHandler<string>? _foodMapFailureHandler;

    [ObservableProperty] private bool _isFollowingCurrentLocation = true;
    [ObservableProperty] private int _mapRecenterRequestVersion;
    [ObservableProperty] private double _mapZoom = 14d;
    public string FocusedMapRequestId => ActiveDelivery?.OfferId ?? SelectedTicket?.TicketId ?? string.Empty;
    public bool HasMapLocationWarning => IsAuthenticated && !HasCurrentLocation;

    private void InitializeFoodMap()
    {
        _foodMapRoutes = new(QueryFoodMapRouteAsync);
        _foodMapRoutes.Changed += (_, _) => QueueFoodMapSnapshot();
    }

    [RelayCommand]
    private void RecenterMap()
    {
        if (!HasCurrentLocation) return;
        IsFollowingCurrentLocation = true;
        MapRecenterRequestVersion++;
    }

    [RelayCommand]
    private async Task RetryMapLocation()
    {
        if (!_workspacePageVisible || !_workspaceActive || !_foodMapForeground || !IsAuthenticated || IsBusy
            || _foodMapCancellation is not { IsCancellationRequested: false } cancellation
            || _foodMapListenerTask is { IsCompleted: false }) return;
        _foodMapPermissionRequested = true;
        _foodMapListenerTask = SeedFoodMapLocationAsync(_foodMapLifetime, cancellation.Token, requestPermission: true);
        await _foodMapListenerTask;
    }

    // The app's foreground work lifetime owns the single device listener.
    // Only the visible workspace requests permission or computes map routes.
    public async Task ResumeFoodMapAsync()
    {
        _foodMapForeground = true;
        if (!_workspaceActive || !IsAuthenticated || _workspaceCancellation.IsCancellationRequested) return;
        if (_foodMapCancellation is { IsCancellationRequested: false } existing)
        {
            if (_workspacePageVisible && !_foodMapPermissionRequested
                && _foodMapListenerTask is not { IsCompleted: false })
            {
                _foodMapPermissionRequested = true;
                _foodMapListenerTask = SeedFoodMapLocationAsync(_foodMapLifetime, existing.Token, requestPermission: true);
                await _foodMapListenerTask;
            }
            else ScheduleFoodMapListener(_foodMapLifetime, existing.Token);
            return;
        }
        _foodMapCancellation?.Dispose();
        var lifetime = ++_foodMapLifetime;
        var cancellation = _foodMapCancellation = CancellationTokenSource.CreateLinkedTokenSource(_workspaceCancellation.Token);
        var token = cancellation.Token;
        _foodMapLocationHandler = (_, location) => QueueFoodMapLocation(location, lifetime);
        _foodMapFailureHandler = (_, message) => QueueFoodMapFailure(message, lifetime);
        _locationService.LocationChanged += _foodMapLocationHandler;
        _locationService.ListeningFailed += _foodMapFailureHandler;
        UpdateFoodMapContext();
        _foodMapTask = MonitorFoodMapAsync(lifetime, token);
        _foodMapListenerRetryAtUtc = DateTime.MinValue;
        var requestPermission = _workspacePageVisible && !_foodMapPermissionRequested;
        if (requestPermission) _foodMapPermissionRequested = true;
        _foodMapListenerTask = SeedFoodMapLocationAsync(lifetime, token, requestPermission);
        await _foodMapListenerTask;
    }

    private void ScheduleFoodMapListener(long lifetime, CancellationToken token)
    {
        if (_locationService.IsListening || _foodMapListenerTask is { IsCompleted: false }
            || DateTime.UtcNow < _foodMapListenerRetryAtUtc) return;
        _foodMapListenerTask = SeedFoodMapLocationAsync(lifetime, token);
    }

    private async Task SeedFoodMapLocationAsync(long lifetime, CancellationToken token, bool requestPermission = false)
    {
        _foodMapListenerRetryAtUtc = DateTime.UtcNow.AddSeconds(60);
        try
        {
            var started = await _locationService.StartListeningAsync(token, requestPermission);
            if (!IsFoodMapLifetimeCurrent(lifetime)) return;
            if (!started) CurrentArea = "위치 권한 또는 GPS 확인 필요";
            var location = _locationService.LatestLocation ?? (started ? await _locationService.GetCurrentAsync(token) : null);
            token.ThrowIfCancellationRequested();
            if (IsFoodMapLifetimeCurrent(lifetime) && location is not null) ApplyFoodMapLocation(location);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (IsFoodMapLifetimeCurrent(lifetime))
                QueueFoodMapFailure("위치 정보를 받지 못했습니다. GPS와 위치 권한을 확인해 주세요.", lifetime);
        }
    }

    public void PauseFoodMap()
    {
        _foodMapForeground = false;
        StopFoodMap();
    }

    private void StopFoodMap()
    {
        ++_foodMapLifetime;
        _foodMapCancellation?.Cancel();
        if (_foodMapLocationHandler is not null) _locationService.LocationChanged -= _foodMapLocationHandler;
        if (_foodMapFailureHandler is not null) _locationService.ListeningFailed -= _foodMapFailureHandler;
        _foodMapLocationHandler = null;
        _foodMapFailureHandler = null;
        _locationService.StopListening();
        _foodMapLocation = null;
        HasCurrentLocation = false;
        _foodMapRoutes.Clear();
        SelectedRouteOverlays = [];
    }

    private bool IsFoodMapLifetimeCurrent(long lifetime) => lifetime == _foodMapLifetime
        && _foodMapForeground && _workspaceActive && IsAuthenticated && !_workspaceCancellation.IsCancellationRequested;

    private async Task MonitorFoodMapAsync(long lifetime, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!IsFoodMapLifetimeCurrent(lifetime)) return;
                    if (!IsFreshFoodMapLocation(_foodMapLocation))
                    {
                        _foodMapRoutes.ForgetLocation();
                        HasCurrentLocation = false;
                        SelectedRouteOverlays = [];
                        RouteStatusText = "현재 위치 확인 후 경로를 표시합니다.";
                    }
                    if (!_workspacePageVisible) { ScheduleFoodMapListener(lifetime, token); return; }
                    UpdateFoodMapContext();
                    ScheduleFoodMapListener(lifetime, token);
                    _ = _foodMapRoutes.TickAsync();
                });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void QueueFoodMapLocation(FDriverLocationSnapshot location, long lifetime)
        => _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (IsFoodMapLifetimeCurrent(lifetime)) ApplyFoodMapLocation(location);
        });

    private void QueueFoodMapFailure(string message, long lifetime)
        => _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!IsFoodMapLifetimeCurrent(lifetime)) return;
            _foodMapLocation = null;
            _foodMapRoutes.ForgetLocation();
            _foodMapListenerRetryAtUtc = DateTime.UtcNow.AddSeconds(60);
            HasCurrentLocation = false;
            CurrentArea = message;
            SelectedRouteOverlays = [];
            RouteStatusText = "현재 위치 확인 후 경로를 표시합니다.";
        });

    private bool ApplyFoodMapLocation(FDriverLocationSnapshot location)
    {
        if (!_foodMapForeground || !IsFreshFoodMapLocation(location)) return false;
        if (_foodMapLocation is not null && location.RecordedAtUtc < _foodMapLocation.RecordedAtUtc) return false;
        _foodMapLocation = location;
        CurrentLocationLatitude = (double)location.Latitude;
        CurrentLocationLongitude = (double)location.Longitude;
        HasCurrentLocation = true;
        CurrentArea = "현재 위치 확인됨";
        if (_workspacePageVisible)
        {
            _foodMapRoutes.ObserveLocation(new(location.Latitude, location.Longitude, location.AccuracyMeters,
                new DateTimeOffset(DateTime.SpecifyKind(location.RecordedAtUtc, DateTimeKind.Utc))));
            _ = _foodMapRoutes.TickAsync();
        }
        return true;
    }

    private static bool IsFreshFoodMapLocation(FDriverLocationSnapshot? location)
    {
        if (location is null || location.Latitude is < -90 or > 90 or 0
            || location.Longitude is < -180 or > 180 or 0 || location.AccuracyMeters is < 0) return false;
        var age = DateTime.UtcNow - DateTime.SpecifyKind(location.RecordedAtUtc, DateTimeKind.Utc);
        return age >= TimeSpan.FromSeconds(-5) && age <= TimeSpan.FromSeconds(30);
    }

    private void UpdateFoodMapContext()
    {
        if (!_workspacePageVisible || !_foodMapForeground || !_workspaceActive || !IsAuthenticated || _workspaceCancellation.IsCancellationRequested) return;
        var intents = ActiveDeliveryItems.Select(ToFoodMapIntent).OfType<FDriverMapRouteIntent>().ToArray();
        if (intents.Length == 0 && ActiveDelivery is null && SelectedTicket is { } ticket)
            intents = [ToFoodMapIntent(ticket.TicketId, FoodMapRoutePhase.Pickup, ticket.Pickup, ticket.Dropoff)];
        _foodMapRoutes.UpdateContext(_authSession.UserId ?? string.Empty, _foodMapLifetime, intents, FocusedMapRequestId);
        // UpdateContext may reset the owner and its last GPS on account/resume.
        if (IsFreshFoodMapLocation(_foodMapLocation))
        {
            var location = _foodMapLocation!;
            _foodMapRoutes.ObserveLocation(new(location.Latitude, location.Longitude, location.AccuracyMeters,
                new DateTimeOffset(DateTime.SpecifyKind(location.RecordedAtUtc, DateTimeKind.Utc))));
        }
    }

    private static FDriverMapRouteIntent? ToFoodMapIntent(ActiveDeliveryPreview delivery)
        => delivery.WorkStatus switch
        {
            DriverWorkOfferStatus.Accepted or DriverWorkOfferStatus.MovingToPickup
                => ToFoodMapIntent(delivery.OfferId, FoodMapRoutePhase.Pickup, delivery.Pickup, delivery.Dropoff),
            DriverWorkOfferStatus.PickupConfirmed or DriverWorkOfferStatus.MovingToDropoff
                => ToFoodMapIntent(delivery.OfferId, FoodMapRoutePhase.Dropoff, delivery.Pickup, delivery.Dropoff),
            _ => null
        };

    private static FDriverMapRouteIntent ToFoodMapIntent(string id, FoodMapRoutePhase phase,
        DriverWorkStopDto pickup, DriverWorkStopDto dropoff)
    {
        var target = phase == FoodMapRoutePhase.Pickup ? pickup : dropoff;
        return new(id, phase, (decimal)pickup.Latitude, (decimal)pickup.Longitude,
            (decimal)dropoff.Latitude, (decimal)dropoff.Longitude,
            (decimal)target.Latitude, (decimal)target.Longitude, target.Label);
    }

    private async Task<FoodDeliveryDriverRouteResponseDto> QueryFoodMapRouteAsync(
        FoodDeliveryDriverRouteRequestDto request, CancellationToken token)
    {
        var lifetime = _foodMapLifetime;
        var account = _authSession.UserId;
        try { return await _api.GetRouteAsync(request, token); }
        catch (FDriverApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized && !token.IsCancellationRequested)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!IsFoodMapLifetimeCurrent(lifetime) || account != _authSession.UserId) return;
                CancelWorkspaceLifetime();
                await ClearAuthenticationAsync();
                StatusMessage = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
            });
            throw;
        }
    }

    private void QueueFoodMapSnapshot()
    {
        var lifetime = _foodMapLifetime;
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!IsFoodMapLifetimeCurrent(lifetime) || !_workspacePageVisible) return;
            ApplyFoodMapSnapshot(_foodMapRoutes.Snapshot);
        });
    }

    private void ApplyFoodMapSnapshot(FDriverMapRouteSnapshot snapshot)
    {
        if (!HasRouteSelection) { SelectedRouteOverlays = []; RouteStatusText = "배달을 선택해 주세요."; return; }
        if (!IsFreshFoodMapLocation(_foodMapLocation))
        {
            SelectedRouteOverlays = [];
            RouteStatusText = "현재 위치 확인 후 경로를 표시합니다.";
            return;
        }
        var overlays = snapshot.ReferenceRoutes.Where(IsRoadRoute)
            .Select(route => ToFoodMapOverlay(route, "#64748B", 5)).ToList();
        if (snapshot.CurrentRoute is { } current && IsRoadRoute(current))
            overlays.Add(ToFoodMapOverlay(current, current.Phase == FoodMapRoutePhase.Pickup ? "#F57C00" : "#2563EB", 9));
        SelectedRouteOverlays = overlays;
        RouteStatusText = snapshot.LastError is not null || snapshot.CurrentRoute?.IsEstimated == true
            ? "도로 경로를 확인하지 못했습니다. 잠시 후 다시 조회합니다."
            : snapshot.IsLoading ? "현재 목적지 경로 확인 중"
            : snapshot.CurrentRoute is { } route && IsRoadRoute(route)
                ? $"{(route.Phase == FoodMapRoutePhase.Pickup ? "픽업" : "전달")} 경로 · {route.DistanceKm:0.0}km · 약 {route.DurationMinutes}분"
                : "현재 목적지 경로 대기 중";
    }

    private static bool IsRoadRoute(FDriverStoredMapRoute route) => !route.IsEstimated && route.Points.Count > 1;
    private static DriverMapRouteOverlay ToFoodMapOverlay(FDriverStoredMapRoute route, string color, int width)
        => new(route.IsReference ? $"reference:{route.OfferId}" : route.OfferId,
            route.IsReference ? $"다른 배달 · 픽업→전달 참고 경로 · {route.ReceivedAtUtc.ToLocalTime():HH:mm} 조회" : "현재 목적지 경로",
            route.Points.Select(point => new DriverMapRoutePoint((double)point.Latitude, (double)point.Longitude, string.Empty)).ToArray(),
            StrokeColor: color, Width: width);
}
