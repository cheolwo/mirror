#if ANDROID
using Android.OS;
using Android.Views;
using Android.Widget;
using Com.Naver.Maps.Geometry;
using Com.Naver.Maps.Map;
using Com.Naver.Maps.Map.Overlay;
using Com.Naver.Maps.Map.Util;
using FDriverApp.Controls;
using Ssalddel.Contracts.Common.Drivers;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Networking;
using AndroidColor = Android.Graphics.Color;

namespace FDriverApp.Handlers;

public partial class FDriverNativeMapViewHandler : ViewHandler<FDriverNativeMapView, FrameLayout>
{
    private static readonly int PickupMarkerTintColor = AndroidColor.Rgb(245, 124, 0);
    private static readonly int DropoffMarkerTintColor = AndroidColor.Rgb(37, 99, 235);
    private static readonly int UnselectedMarkerTintColor = AndroidColor.Rgb(100, 116, 139);
    private readonly List<Marker> _nativeMarkers = [];
    private readonly List<PathOverlay> _nativeRouteOverlays = [];
    private NaverMap? _naverMap;
    private MapView? _mapView;
    private FrameLayout? _container;
    private TextView? _statusView;
    private NaverMapSdk? _sdk;
    private NaverMapReadiness? _readiness;
    private CancellationTokenSource? _loadTimeout;
    private FDriverMapCameraState CameraState => VirtualView.CameraState;
    private bool _updatingCameraBindings;
    private bool _disconnected;
    private int _mapGeneration;

    protected override FrameLayout CreatePlatformView()
    {
        var context = MauiContext?.Context ?? throw new InvalidOperationException("Android context is not available.");
        _disconnected = false;
        var generation = ++_mapGeneration;
        _container = new FrameLayout(context);
        _statusView = new TextView(context)
        {
            Gravity = GravityFlags.Center
        };
        _statusView.SetTextSize(Android.Util.ComplexUnitType.Dip, 14f);
        _statusView.SetPadding(24, 24, 24, 24);
        _statusView.SetTextColor(AndroidColor.Black);
        _statusView.SetBackgroundColor(AndroidColor.Argb(235, 255, 255, 255));
        _container.AddView(_statusView, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Center));

        var keyResource = context.Resources?.GetIdentifier("naver_map_sdk_ncp_key_id", "string", context.PackageName) ?? 0;
        var keyId = keyResource == 0 ? null : context.GetString(keyResource);
        _readiness = new NaverMapReadiness(keyId);
        UpdateStatus();
        if (!_readiness.IsConfigured)
        {
            // Do not initialize the SDK or request tiles using a missing/example credential.
            return _container;
        }

        _sdk = NaverMapSdk.GetInstance(context);
        _sdk.AuthFailed += OnAuthenticationFailed;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        _readiness.SetInternetAvailable(Connectivity.Current.NetworkAccess == NetworkAccess.Internet);
        UpdateStatus();

        var mapView = new MapView(context);
        _mapView = mapView;
        mapView.LayoutChange += OnMapLayoutChanged;
        mapView.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        _container.AddView(mapView, 0);
        mapView.OnCreate((Bundle?)null);
        mapView.OnStart();
        mapView.OnResume();
        mapView.GetMapAsync(new MapReadyCallback(this, generation));
        StartLoadTimeout();
        return _container;
    }

    protected override void DisconnectHandler(FrameLayout platformView)
    {
        _disconnected = true;
        _mapGeneration++;
        VirtualView?.SetMapReady(false);
        _loadTimeout?.Cancel();
        _loadTimeout?.Dispose();
        _loadTimeout = null;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        if (_sdk is not null)
        {
            _sdk.AuthFailed -= OnAuthenticationFailed;
            _sdk = null;
        }
        if (_naverMap is not null)
        {
            _naverMap.Load -= OnMapTilesLoaded;
            _naverMap.CameraChange -= OnCameraChanged;
        }
        ClearMarkers();
        ClearRouteOverlays();
        if (_mapView is not null)
        {
            _mapView.LayoutChange -= OnMapLayoutChanged;
        }
        _mapView?.OnPause();
        _mapView?.OnStop();
        _mapView?.OnDestroy();
        _mapView = null;
        _naverMap = null;
        _statusView = null;
        _container = null;
        base.DisconnectHandler(platformView);
    }

    private void OnMapReady(NaverMap naverMap, int generation)
    {
        if (_disconnected || generation != _mapGeneration)
        {
            return;
        }

        _naverMap = naverMap;
        // The native map object being ready does not prove that background tiles loaded.
        _naverMap.Load += OnMapTilesLoaded;
        _naverMap.CameraChange += OnCameraChanged;
        ApplyKoreanMapLocale();
        ApplyMapOptions();
        if (CameraState.CurrentCamera is { } previous)
        {
            _naverMap.MoveCamera(CameraUpdate.ScrollAndZoomTo(
                new LatLng(previous.Latitude, previous.Longitude), previous.Zoom));
        }
        ApplyCamera();
        ApplyMarkers();
        ApplyRouteOverlays();
    }

    private void OnCameraChanged(object? sender, NaverMap.CameraChangeEventArgs args)
    {
        if (_disconnected || _naverMap is null || VirtualView is null)
        {
            return;
        }

        var camera = _naverMap.CameraPosition;
        CameraState.ObserveNativeCamera(camera.Target.Latitude, camera.Target.Longitude, camera.Zoom);
        _updatingCameraBindings = true;
        try
        {
            // SDK 카메라 값을 되돌려 보내면서 같은 카메라를 다시 이동하지 않습니다.
            VirtualView.Zoom = camera.Zoom;
            if (args.P0 == CameraUpdate.ReasonGesture)
            {
                VirtualView.IsFollowingCurrentLocation = false;
            }
        }
        finally
        {
            _updatingCameraBindings = false;
        }
    }

    private void OnMapTilesLoaded(object? sender, EventArgs args)
    {
        if (_disconnected)
        {
            return;
        }
        _readiness?.OnTilesLoaded();
        _loadTimeout?.Cancel();
        UpdateStatus();
    }

    private void OnAuthenticationFailed(object? sender, NaverMapSdk.AuthFailedEventArgs args)
    {
        var generation = _mapGeneration;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_disconnected || generation != _mapGeneration)
            {
                return;
            }
            // Raw SDK exceptions may contain configuration information and are not displayed/logged.
#if DEBUG
            var safeCode = args.P0.ErrorCode is "401" or "429" or "800" ? args.P0.ErrorCode : "unknown";
            Android.Util.Log.Warn("FDriverMap", $"Naver SDK authentication failed: {safeCode}");
#endif
            _readiness?.OnAuthenticationFailed(args.P0.ErrorCode);
            _loadTimeout?.Cancel();
            UpdateStatus();
        });
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_disconnected)
            {
                return;
            }
            _readiness?.SetInternetAvailable(args.NetworkAccess == NetworkAccess.Internet);
            UpdateStatus();
            if (_readiness?.Status == NaverMapReadinessStatus.Loading)
            {
                StartLoadTimeout();
            }
        });
    }

    private void StartLoadTimeout()
    {
        _loadTimeout?.Cancel();
        _loadTimeout?.Dispose();
        _loadTimeout = new CancellationTokenSource();
        _ = CheckLoadTimeoutAsync(_loadTimeout.Token);
    }

    private async Task CheckLoadTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!_disconnected && !cancellationToken.IsCancellationRequested)
                {
                    _readiness?.OnLoadTimeout();
                    UpdateStatus();
                }
            });
        }
        catch (System.OperationCanceledException)
        {
            // Map loaded or handler was disconnected.
        }
    }

    private void UpdateStatus()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(UpdateStatus);
            return;
        }
        if (_disconnected || _statusView is null)
        {
            return;
        }

        VirtualView?.SetMapReady(_readiness?.Status == NaverMapReadinessStatus.Ready);
        var message = _readiness?.Message;
        _statusView.Text = message;
        _statusView.Visibility = message is null ? ViewStates.Gone : ViewStates.Visible;
    }

    public static void MapCamera(FDriverNativeMapViewHandler handler, FDriverNativeMapView view)
    {
        handler.ApplyCamera();
    }

    public static void MapMarkers(FDriverNativeMapViewHandler handler, FDriverNativeMapView view)
    {
        handler.ApplyMarkers();
    }

    public static void MapRouteOverlays(FDriverNativeMapViewHandler handler, FDriverNativeMapView view)
    {
        handler.ApplyRouteOverlays();
    }

    public static void MapOptions(FDriverNativeMapViewHandler handler, FDriverNativeMapView view)
    {
        handler.ApplyMapOptions();
    }

    private void ApplyMapOptions()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        _naverMap.MinZoom = VirtualView.MinZoom;
        _naverMap.MaxZoom = VirtualView.MaxZoom;
        _naverMap.LiteModeEnabled = false;
        _naverMap.SetLayerGroupEnabled(NaverMap.LayerGroupTraffic, VirtualView.ShowTrafficLayer);

        var uiSettings = _naverMap.UiSettings;
        uiSettings.CompassEnabled = true;
        uiSettings.ScaleBarEnabled = true;
        uiSettings.ZoomControlEnabled = true;
        // 앱의 GPS 소유자와 재중앙 버튼을 사용합니다. SDK LocationSource는 연결하지 않습니다.
        uiSettings.LocationButtonEnabled = false;
        uiSettings.SetLogoMargin(ToPixels(8), ToPixels(8), ToPixels(8), ToPixels(8));

        ApplyLocationOverlay();
    }

    private void ApplyKoreanMapLocale()
    {
        if (_naverMap is not null)
        {
            _naverMap.Locale = Java.Util.Locale.ForLanguageTag("ko-KR");
        }
    }

    private void ApplyCamera()
    {
        if (_naverMap is null || VirtualView is null || _updatingCameraBindings)
        {
            return;
        }

        var camera = CameraState.ResolveUpdate(
            VirtualView.CenterLatitude,
            VirtualView.CenterLongitude,
            VirtualView.HasCurrentLocation,
            VirtualView.CurrentLocationLatitude,
            VirtualView.CurrentLocationLongitude,
            VirtualView.IsFollowingCurrentLocation,
            VirtualView.Zoom,
            VirtualView.RecenterRequestVersion);
        if (camera is null)
        {
            return;
        }

        if (camera.ResumeFollowing)
        {
            _updatingCameraBindings = true;
            try
            {
                VirtualView.IsFollowingCurrentLocation = true;
            }
            finally
            {
                _updatingCameraBindings = false;
            }
        }
        var target = new LatLng(camera.Latitude, camera.Longitude);
        var update = CameraUpdate.ScrollAndZoomTo(target, camera.Zoom);
        _naverMap.MoveCamera(update);
        ApplyLocationOverlay();
    }

    private void ApplyMarkers()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        ClearMarkers();
        foreach (var item in VirtualView.Markers)
        {
            var selected = !string.IsNullOrWhiteSpace(VirtualView.SelectedRequestId)
                && string.Equals(item.RequestId, VirtualView.SelectedRequestId, StringComparison.Ordinal);
            AddMarker(item, item.PickupLatitude, item.PickupLongitude, item.PickupLabel, item.Title,
                selected ? PickupMarkerTintColor : UnselectedMarkerTintColor, selected);
            AddMarker(item, item.DropoffLatitude, item.DropoffLongitude, item.DropoffLabel, item.DropoffAddress,
                selected ? DropoffMarkerTintColor : UnselectedMarkerTintColor, selected);
        }
        ApplyRouteFrame();
    }

    private void ApplyRouteOverlays()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        ClearRouteOverlays();
        foreach (var item in VirtualView.RouteOverlays)
        {
            if (item.Points.Count < 2 || item.Points.Any(x => !FDriverMapCameraState.IsValidCoordinate(x.Latitude, x.Longitude)))
            {
                continue;
            }

            var coords = item.Points
                .Select(x => new LatLng(x.Latitude, x.Longitude))
                .ToList();
            var overlay = new PathOverlay
            {
                Coords = coords,
                Width = item.Width,
                Color = ParseColor(item.StrokeColor, AndroidColor.Rgb(37, 99, 235)),
                OutlineColor = ParseColor(item.OutlineColor, AndroidColor.White),
                ZIndex = _nativeRouteOverlays.Count
            };

            overlay.Map = _naverMap;
            _nativeRouteOverlays.Add(overlay);
        }
        ApplyRouteFrame();
    }

    private void OnMapLayoutChanged(object? sender, Android.Views.View.LayoutChangeEventArgs args)
        => ApplyRouteFrame();

    private int ToPixels(double dip)
        => (int)Math.Ceiling(dip * (_mapView?.Resources?.DisplayMetrics?.Density ?? 1f));

    private void ApplyRouteFrame()
    {
        if (_disconnected || _naverMap is null || VirtualView is null || _mapView is null
            || _mapView.Width <= 0 || _mapView.Height <= 0)
        {
            return;
        }

        var route = VirtualView.RouteOverlays.FirstOrDefault(x =>
            string.Equals(x.RouteId, VirtualView.SelectedRequestId, StringComparison.Ordinal));
        if (route is null || route.Points.Count < 2
            || route.Points.Any(x => !FDriverMapCameraState.IsValidCoordinate(x.Latitude, x.Longitude)))
        {
            return;
        }

        var points = route.Points.Select(x => (x.Latitude, x.Longitude)).ToList();
        var selected = VirtualView.Markers.FirstOrDefault(x =>
            string.Equals(x.RequestId, VirtualView.SelectedRequestId, StringComparison.Ordinal));
        if (selected is null)
        {
            // Do not consume the first frame before the selected pins arrive.
            return;
        }
        points.Add((selected.PickupLatitude, selected.PickupLongitude));
        points.Add((selected.DropoffLatitude, selected.DropoffLongitude));
        if (VirtualView.HasCurrentLocation)
        {
            points.Add((VirtualView.CurrentLocationLatitude, VirtualView.CurrentLocationLongitude));
        }

        // Only the current route and its pickup/dropoff pins determine the initial frame.
        // GPS updates and refreshed geometry for the same step preserve user exploration.
        var frame = CameraState.ResolveRouteFrame($"{route.RouteId}:{route.StrokeColor}", points);
        if (frame is null)
        {
            return;
        }

        var bounds = LatLngBounds.From(
            new LatLng(frame.SouthLatitude, frame.WestLongitude),
            new LatLng(frame.NorthLatitude, frame.EastLongitude));
        var horizontal = Math.Min(ToPixels(32), _mapView.Width / 5);
        var top = Math.Min(ToPixels(72), _mapView.Height / 4);
        var bottom = Math.Min(ToPixels(64), _mapView.Height / 4);
        _updatingCameraBindings = true;
        try
        {
            VirtualView.IsFollowingCurrentLocation = false;
        }
        finally
        {
            _updatingCameraBindings = false;
        }
        _naverMap.MoveCamera(CameraUpdate.FitBounds(bounds, horizontal, top, horizontal, bottom));
        // Also record synchronously so an intervening binding update cannot restore GPS-only framing.
        var camera = _naverMap.CameraPosition;
        CameraState.ObserveNativeCamera(camera.Target.Latitude, camera.Target.Longitude, camera.Zoom);
        _updatingCameraBindings = true;
        try
        {
            VirtualView.Zoom = camera.Zoom;
        }
        finally
        {
            _updatingCameraBindings = false;
        }
    }

    private void ApplyLocationOverlay()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        var overlay = _naverMap.LocationOverlay;
        var hasLocation = VirtualView.HasCurrentLocation
            && FDriverMapCameraState.IsValidCoordinate(VirtualView.CurrentLocationLatitude, VirtualView.CurrentLocationLongitude);
        if (hasLocation)
        {
            overlay.Position = new LatLng(VirtualView.CurrentLocationLatitude, VirtualView.CurrentLocationLongitude);
        }
        overlay.CircleColor = AndroidColor.Argb(40, 25, 118, 210);
        overlay.CircleOutlineColor = AndroidColor.Argb(120, 25, 118, 210);
        overlay.CircleOutlineWidth = 2;
        overlay.Visible = VirtualView.ShowCurrentLocationOverlay && hasLocation;

        _naverMap.LocationTrackingMode = VirtualView.ShowCurrentLocationOverlay && hasLocation
            ? LocationTrackingMode.NoFollow!
            : LocationTrackingMode.None!;
    }

    private void AddMarker(
        DriverMapMarkerItem item,
        double latitude,
        double longitude,
        string caption,
        string subCaption,
        int iconTintColor,
        bool selected)
    {
        if (_naverMap is null || VirtualView is null || !FDriverMapCameraState.IsValidCoordinate(latitude, longitude))
        {
            return;
        }

        var marker = new Marker
        {
            Position = new LatLng(latitude, longitude),
            Icon = MarkerIcons.Black,
            IconTintColor = iconTintColor,
            CaptionText = caption,
            SubCaptionText = subCaption,
            ZIndex = selected ? 1000 : 0
        };

        marker.Click += (_, _) =>
        {
            VirtualView.SendMarkerSelected(item);
        };
        marker.Map = _naverMap;
        _nativeMarkers.Add(marker);
    }

    private void ClearMarkers()
    {
        foreach (var marker in _nativeMarkers)
        {
            marker.Map = null;
            marker.Dispose();
        }

        _nativeMarkers.Clear();
    }

    private void ClearRouteOverlays()
    {
        foreach (var overlay in _nativeRouteOverlays)
        {
            overlay.Map = null;
            overlay.Dispose();
        }

        _nativeRouteOverlays.Clear();
    }

    private static AndroidColor ParseColor(string value, AndroidColor fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return AndroidColor.ParseColor(value);
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private sealed class MapReadyCallback(FDriverNativeMapViewHandler handler, int generation) : Java.Lang.Object, IOnMapReadyCallback
    {
        public void OnMapReady(NaverMap naverMap)
        {
            handler.OnMapReady(naverMap, generation);
        }
    }
}
#endif
