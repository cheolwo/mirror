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
    private readonly List<Marker> _nativeMarkers = [];
    private readonly List<PathOverlay> _nativeRouteOverlays = [];
    private NaverMap? _naverMap;
    private MapView? _mapView;
    private FrameLayout? _container;
    private TextView? _statusView;
    private NaverMapSdk? _sdk;
    private NaverMapReadiness? _readiness;
    private CancellationTokenSource? _loadTimeout;
    private bool _disconnected;

    protected override FrameLayout CreatePlatformView()
    {
        var context = MauiContext?.Context ?? throw new InvalidOperationException("Android context is not available.");
        _disconnected = false;
        _container = new FrameLayout(context);
        _statusView = new TextView(context)
        {
            Gravity = GravityFlags.Center,
            TextSize = 14f
        };
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
        mapView.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        _container.AddView(mapView, 0);
        mapView.OnCreate((Bundle?)null);
        mapView.OnStart();
        mapView.OnResume();
        mapView.GetMapAsync(new MapReadyCallback(this));
        StartLoadTimeout();
        return _container;
    }

    protected override void DisconnectHandler(FrameLayout platformView)
    {
        _disconnected = true;
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
        }
        ClearMarkers();
        ClearRouteOverlays();
        _mapView?.OnPause();
        _mapView?.OnStop();
        _mapView?.OnDestroy();
        _mapView = null;
        _naverMap = null;
        _statusView = null;
        _container = null;
        base.DisconnectHandler(platformView);
    }

    private void OnMapReady(NaverMap naverMap)
    {
        if (_disconnected)
        {
            return;
        }

        _naverMap = naverMap;
        // The native map object being ready does not prove that background tiles loaded.
        _naverMap.Load += OnMapTilesLoaded;
        ApplyKoreanMapLocale();
        ApplyMapOptions();
        ApplyCamera();
        ApplyMarkers();
        ApplyRouteOverlays();
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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_disconnected)
            {
                return;
            }
            // Raw SDK exceptions may contain configuration information and are not displayed/logged.
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
        if (_statusView is null)
        {
            return;
        }

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
        uiSettings.LocationButtonEnabled = VirtualView.ShowLocationButton;
        uiSettings.SetLogoMargin(16, 16, 16, 120);

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
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        var target = new LatLng(VirtualView.CenterLatitude, VirtualView.CenterLongitude);
        var update = CameraUpdate.ScrollAndZoomTo(target, VirtualView.Zoom);
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
            AddMarker(item, item.PickupLatitude, item.PickupLongitude, item.PickupLabel, item.Title, PickupMarkerTintColor);
            if (item.DropoffLatitude != 0d && item.DropoffLongitude != 0d)
            {
                AddMarker(item, item.DropoffLatitude, item.DropoffLongitude, item.DropoffLabel, item.DropoffAddress, DropoffMarkerTintColor);
            }
        }
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
            if (item.Points.Count < 2)
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
                OutlineColor = ParseColor(item.OutlineColor, AndroidColor.White)
            };

            overlay.Map = _naverMap;
            _nativeRouteOverlays.Add(overlay);
        }
    }

    private void ApplyLocationOverlay()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        var overlay = _naverMap.LocationOverlay;
        var latitude = VirtualView.CurrentLocationLatitude != 0d
            ? VirtualView.CurrentLocationLatitude
            : VirtualView.CenterLatitude;
        var longitude = VirtualView.CurrentLocationLongitude != 0d
            ? VirtualView.CurrentLocationLongitude
            : VirtualView.CenterLongitude;
        overlay.Position = new LatLng(latitude, longitude);
        overlay.CircleColor = AndroidColor.Argb(40, 25, 118, 210);
        overlay.CircleOutlineColor = AndroidColor.Argb(120, 25, 118, 210);
        overlay.CircleOutlineWidth = 2;
        overlay.Visible = VirtualView.ShowCurrentLocationOverlay;

        _naverMap.LocationTrackingMode = VirtualView.ShowCurrentLocationOverlay
            ? LocationTrackingMode.NoFollow!
            : LocationTrackingMode.None!;
    }

    private void AddMarker(
        DriverMapMarkerItem item,
        double latitude,
        double longitude,
        string caption,
        string subCaption,
        int iconTintColor)
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        var marker = new Marker
        {
            Position = new LatLng(latitude, longitude),
            Icon = MarkerIcons.Black,
            IconTintColor = iconTintColor,
            CaptionText = caption,
            SubCaptionText = subCaption
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

    private sealed class MapReadyCallback(FDriverNativeMapViewHandler handler) : Java.Lang.Object, IOnMapReadyCallback
    {
        public void OnMapReady(NaverMap naverMap)
        {
            handler.OnMapReady(naverMap);
        }
    }
}
#endif
