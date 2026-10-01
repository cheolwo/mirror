#if ANDROID
using Android.OS;
using Android.Views;
using Android.Widget;
using Com.Naver.Maps.Map.Util;
using DriverApp.Controls;
using Hongdal.Contracts.Common.Drivers;
using Hongdal.Contracts.Common.Operations;
using Microsoft.Maui.Handlers;
using GoogleBitmapDescriptorFactory = Android.Gms.Maps.Model.BitmapDescriptorFactory;
using GoogleCameraUpdateFactory = Android.Gms.Maps.CameraUpdateFactory;
using GoogleLatLng = Android.Gms.Maps.Model.LatLng;
using GoogleMap = Android.Gms.Maps.GoogleMap;
using GoogleMapView = Android.Gms.Maps.MapView;
using GoogleMarker = Android.Gms.Maps.Model.Marker;
using GoogleMarkerOptions = Android.Gms.Maps.Model.MarkerOptions;
using GooglePolyline = Android.Gms.Maps.Model.Polyline;
using GooglePolylineOptions = Android.Gms.Maps.Model.PolylineOptions;
using NaverLatLng = Com.Naver.Maps.Geometry.LatLng;
using NaverMap = Com.Naver.Maps.Map.NaverMap;
using NaverMapView = Com.Naver.Maps.Map.MapView;
using NaverMarker = Com.Naver.Maps.Map.Overlay.Marker;
using NaverMarkerIcons = Com.Naver.Maps.Map.Util.MarkerIcons;
using NaverPathOverlay = Com.Naver.Maps.Map.Overlay.PathOverlay;
using AndroidColor = Android.Graphics.Color;

namespace DriverApp.Handlers;

public partial class DriverNativeMapViewHandler : ViewHandler<DriverNativeMapView, FrameLayout>
{
    private FrameLayout? _container;
    private NaverMapView? _naverMapView;
    private GoogleMapView? _googleMapView;
    private NaverMap? _naverMap;
    private GoogleMap? _googleMap;
    private GoogleMarkerClickListener? _googleMarkerClickListener;
    private readonly List<NaverMarker> _naverMarkers = [];
    private readonly List<NaverPathOverlay> _naverRouteOverlays = [];
    private readonly List<GoogleMarker> _googleMarkers = [];
    private readonly List<GooglePolyline> _googleRouteOverlays = [];
    private readonly Dictionary<string, DriverMapMarkerItem> _googleMarkerItems = new(StringComparer.Ordinal);
    private GoogleMarker? _googleCurrentLocationMarker;
    private static readonly int PickupMarkerTintColor = AndroidColor.Rgb(245, 124, 0);
    private static readonly int DropoffMarkerTintColor = AndroidColor.Rgb(37, 99, 235);

    protected override FrameLayout CreatePlatformView()
    {
        var context = MauiContext?.Context ?? throw new InvalidOperationException("Android context is not available.");
        var layoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);

        _container = new FrameLayout(context)
        {
            LayoutParameters = layoutParameters
        };
        ApplyProviderVisibility();
        return _container;
    }

    private void EnsureNaverMapView()
    {
        if (_naverMapView is not null || _container is null)
        {
            return;
        }

        var context = MauiContext?.Context ?? throw new InvalidOperationException("Android context is not available.");
        _naverMapView = new NaverMapView(context)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        _naverMapView.OnCreate((Bundle?)null);
        _naverMapView.OnStart();
        _naverMapView.OnResume();
        _naverMapView.GetMapAsync(new NaverMapReadyCallback(this));
        _container.AddView(_naverMapView);
    }

    private void EnsureGoogleMapView()
    {
        if (_googleMapView is not null || _container is null)
        {
            return;
        }

        var context = MauiContext?.Context ?? throw new InvalidOperationException("Android context is not available.");
        _googleMapView = new GoogleMapView(context)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        _googleMapView.OnCreate((Bundle?)null);
        _googleMapView.OnStart();
        _googleMapView.OnResume();
        _googleMapView.GetMapAsync(new GoogleMapReadyCallback(this));
        _container.AddView(_googleMapView);
    }

    protected override void DisconnectHandler(FrameLayout platformView)
    {
        ClearNaverMarkers();
        ClearNaverRouteOverlays();
        ClearGoogleMarkers();
        ClearGoogleRouteOverlays();
        ClearGoogleCurrentLocationMarker();

        if (_naverMapView is not null)
        {
            _naverMapView.OnPause();
            _naverMapView.OnStop();
            _naverMapView.OnDestroy();
            _naverMapView.Dispose();
        }

        if (_googleMapView is not null)
        {
            _googleMapView.OnPause();
            _googleMapView.OnStop();
            _googleMapView.OnDestroy();
            _googleMapView.Dispose();
        }

        _naverMap = null;
        _googleMap = null;
        _container = null;
        _naverMapView = null;
        _googleMapView = null;
        _googleMarkerClickListener?.Dispose();
        _googleMarkerClickListener = null;
        base.DisconnectHandler(platformView);
    }

    private bool UsesGoogleMaps
        => string.Equals(
            VirtualView?.MapProviderCode,
            OperatingMapProviderCodes.GoogleMaps,
            StringComparison.OrdinalIgnoreCase);

    private void OnNaverMapReady(NaverMap naverMap)
    {
        _naverMap = naverMap;
        _naverMap.Locale = Java.Util.Locale.ForLanguageTag("ko-KR");
        ApplyNaverMapOptions();
        ApplyNaverCamera();
        ApplyNaverMarkers();
        ApplyNaverRouteOverlays();
    }

    private void OnGoogleMapReady(GoogleMap googleMap)
    {
        _googleMap = googleMap;
        _googleMarkerClickListener = new GoogleMarkerClickListener(this);
        _googleMap.SetOnMarkerClickListener(_googleMarkerClickListener);
        ApplyGoogleMapOptions();
        ApplyGoogleCamera();
        ApplyGoogleMarkers();
        ApplyGoogleRouteOverlays();
    }

    public static void MapCamera(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyCamera();

    public static void MapMarkers(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyMarkers();

    public static void MapRouteOverlays(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyRouteOverlays();

    public static void MapOptions(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyMapOptions();

    private void ApplyProviderVisibility()
    {
        if (UsesGoogleMaps)
        {
            EnsureGoogleMapView();
        }
        else
        {
            EnsureNaverMapView();
        }

        if (_naverMapView is not null)
        {
            _naverMapView.Visibility = UsesGoogleMaps ? ViewStates.Gone : ViewStates.Visible;
        }

        if (_googleMapView is not null)
        {
            _googleMapView.Visibility = UsesGoogleMaps ? ViewStates.Visible : ViewStates.Gone;
        }
    }

    private void ApplyMapOptions()
    {
        ApplyProviderVisibility();
        ApplyNaverMapOptions();
        ApplyGoogleMapOptions();
        ApplyCamera();
        ApplyMarkers();
        ApplyRouteOverlays();
    }

    private void ApplyCamera()
    {
        ApplyNaverCamera();
        ApplyGoogleCamera();
    }

    private void ApplyMarkers()
    {
        ApplyNaverMarkers();
        ApplyGoogleMarkers();
    }

    private void ApplyRouteOverlays()
    {
        ApplyNaverRouteOverlays();
        ApplyGoogleRouteOverlays();
    }

    private void ApplyNaverMapOptions()
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

        ApplyNaverLocationOverlay();
    }

    private void ApplyGoogleMapOptions()
    {
        if (_googleMap is null || VirtualView is null)
        {
            return;
        }

        _googleMap.TrafficEnabled = VirtualView.ShowTrafficLayer;
        _googleMap.SetMinZoomPreference((float)VirtualView.MinZoom);
        _googleMap.SetMaxZoomPreference((float)VirtualView.MaxZoom);
        _googleMap.UiSettings.CompassEnabled = true;
        _googleMap.UiSettings.ZoomControlsEnabled = true;
        _googleMap.UiSettings.MyLocationButtonEnabled = false;
        ApplyGoogleLocationOverlay();
    }

    private void ApplyNaverCamera()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        var target = new NaverLatLng(VirtualView.CenterLatitude, VirtualView.CenterLongitude);
        var update = Com.Naver.Maps.Map.CameraUpdate.ScrollAndZoomTo(target, VirtualView.Zoom);
        _naverMap.MoveCamera(update);
        ApplyNaverLocationOverlay();
    }

    private void ApplyGoogleCamera()
    {
        if (_googleMap is null || VirtualView is null)
        {
            return;
        }

        var target = new GoogleLatLng(VirtualView.CenterLatitude, VirtualView.CenterLongitude);
        _googleMap.MoveCamera(GoogleCameraUpdateFactory.NewLatLngZoom(target, (float)VirtualView.Zoom));
        ApplyGoogleLocationOverlay();
    }

    private void ApplyNaverMarkers()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        ClearNaverMarkers();
        foreach (var item in VirtualView.Markers)
        {
            AddNaverMarker(item, item.PickupLatitude, item.PickupLongitude, item.PickupLabel, item.PickupAddress, PickupMarkerTintColor);
            if (item.DropoffLatitude != 0d && item.DropoffLongitude != 0d)
            {
                AddNaverMarker(item, item.DropoffLatitude, item.DropoffLongitude, item.DropoffLabel, item.DropoffAddress, DropoffMarkerTintColor);
            }
        }
    }

    private void ApplyGoogleMarkers()
    {
        if (_googleMap is null || VirtualView is null)
        {
            return;
        }

        ClearGoogleMarkers();
        foreach (var item in VirtualView.Markers)
        {
            AddGoogleMarker(item, item.PickupLatitude, item.PickupLongitude, item.PickupLabel, item.PickupAddress, 30f);
            if (item.DropoffLatitude != 0d && item.DropoffLongitude != 0d)
            {
                AddGoogleMarker(item, item.DropoffLatitude, item.DropoffLongitude, item.DropoffLabel, item.DropoffAddress, 210f);
            }
        }
    }

    private void ApplyNaverRouteOverlays()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        ClearNaverRouteOverlays();
        foreach (var item in VirtualView.RouteOverlays)
        {
            if (item.Points.Count < 2)
            {
                continue;
            }

            var overlay = new NaverPathOverlay
            {
                Coords = item.Points.Select(x => new NaverLatLng(x.Latitude, x.Longitude)).ToList(),
                Width = item.Width,
                Color = ParseColor(item.StrokeColor, AndroidColor.Rgb(37, 99, 235)),
                OutlineColor = ParseColor(item.OutlineColor, AndroidColor.White),
                Map = _naverMap
            };
            _naverRouteOverlays.Add(overlay);
        }
    }

    private void ApplyGoogleRouteOverlays()
    {
        if (_googleMap is null || VirtualView is null)
        {
            return;
        }

        ClearGoogleRouteOverlays();
        foreach (var item in VirtualView.RouteOverlays)
        {
            if (item.Points.Count < 2)
            {
                continue;
            }

            var options = new GooglePolylineOptions();
            foreach (var point in item.Points)
            {
                options.Add(new GoogleLatLng(point.Latitude, point.Longitude));
            }

            options.InvokeWidth(item.Width);
            options.InvokeColor(ParseColor(item.StrokeColor, AndroidColor.Rgb(37, 99, 235)));
            var polyline = _googleMap.AddPolyline(options);
            if (polyline is not null)
            {
                _googleRouteOverlays.Add(polyline);
            }
        }
    }

    private void ApplyNaverLocationOverlay()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        var overlay = _naverMap.LocationOverlay;
        overlay.Position = new NaverLatLng(VirtualView.CenterLatitude, VirtualView.CenterLongitude);
        overlay.CircleColor = AndroidColor.Argb(40, 25, 118, 210);
        overlay.CircleOutlineColor = AndroidColor.Argb(120, 25, 118, 210);
        overlay.CircleOutlineWidth = 2;
        overlay.Visible = VirtualView.ShowCurrentLocationOverlay;
        _naverMap.LocationTrackingMode = VirtualView.ShowCurrentLocationOverlay
            ? Com.Naver.Maps.Map.LocationTrackingMode.NoFollow!
            : Com.Naver.Maps.Map.LocationTrackingMode.None!;
    }

    private void ApplyGoogleLocationOverlay()
    {
        ClearGoogleCurrentLocationMarker();
        if (_googleMap is null || VirtualView is null || !VirtualView.ShowCurrentLocationOverlay)
        {
            return;
        }

        var options = new GoogleMarkerOptions()
            .SetPosition(new GoogleLatLng(VirtualView.CenterLatitude, VirtualView.CenterLongitude))
            .SetTitle("현재 위치")
            .SetIcon(GoogleBitmapDescriptorFactory.DefaultMarker(210f));
        _googleCurrentLocationMarker = _googleMap.AddMarker(options);
    }

    private void AddNaverMarker(
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

        var marker = new NaverMarker
        {
            Position = new NaverLatLng(latitude, longitude),
            Icon = NaverMarkerIcons.Black,
            IconTintColor = iconTintColor,
            CaptionText = caption,
            SubCaptionText = subCaption
        };
        marker.Click += (_, _) => VirtualView.SendMarkerSelected(item);
        marker.Map = _naverMap;
        _naverMarkers.Add(marker);
    }

    private void AddGoogleMarker(
        DriverMapMarkerItem item,
        double latitude,
        double longitude,
        string caption,
        string subCaption,
        float hue)
    {
        if (_googleMap is null)
        {
            return;
        }

        var options = new GoogleMarkerOptions()
            .SetPosition(new GoogleLatLng(latitude, longitude))
            .SetTitle(caption)
            .SetSnippet(subCaption)
            .SetIcon(GoogleBitmapDescriptorFactory.DefaultMarker(hue));
        var marker = _googleMap.AddMarker(options);
        if (marker is null)
        {
            return;
        }

        _googleMarkers.Add(marker);
        if (!string.IsNullOrWhiteSpace(marker.Id))
        {
            _googleMarkerItems[marker.Id] = item;
        }
    }

    private bool OnGoogleMarkerClicked(GoogleMarker marker)
    {
        if (VirtualView is null || string.IsNullOrWhiteSpace(marker.Id) || !_googleMarkerItems.TryGetValue(marker.Id, out var item))
        {
            return false;
        }

        VirtualView.SendMarkerSelected(item);
        return false;
    }

    private void ClearNaverMarkers()
    {
        foreach (var marker in _naverMarkers)
        {
            marker.Map = null;
            marker.Dispose();
        }
        _naverMarkers.Clear();
    }

    private void ClearNaverRouteOverlays()
    {
        foreach (var overlay in _naverRouteOverlays)
        {
            overlay.Map = null;
            overlay.Dispose();
        }
        _naverRouteOverlays.Clear();
    }

    private void ClearGoogleMarkers()
    {
        foreach (var marker in _googleMarkers)
        {
            marker.Remove();
            marker.Dispose();
        }
        _googleMarkers.Clear();
        _googleMarkerItems.Clear();
    }

    private void ClearGoogleRouteOverlays()
    {
        foreach (var overlay in _googleRouteOverlays)
        {
            overlay.Remove();
            overlay.Dispose();
        }
        _googleRouteOverlays.Clear();
    }

    private void ClearGoogleCurrentLocationMarker()
    {
        _googleCurrentLocationMarker?.Remove();
        _googleCurrentLocationMarker?.Dispose();
        _googleCurrentLocationMarker = null;
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

    private sealed class NaverMapReadyCallback(DriverNativeMapViewHandler handler)
        : Java.Lang.Object, Com.Naver.Maps.Map.IOnMapReadyCallback
    {
        public void OnMapReady(NaverMap naverMap) => handler.OnNaverMapReady(naverMap);
    }

    private sealed class GoogleMapReadyCallback(DriverNativeMapViewHandler handler)
        : Java.Lang.Object, Android.Gms.Maps.IOnMapReadyCallback
    {
        public void OnMapReady(GoogleMap googleMap) => handler.OnGoogleMapReady(googleMap);
    }

    private sealed class GoogleMarkerClickListener(DriverNativeMapViewHandler handler)
        : Java.Lang.Object, GoogleMap.IOnMarkerClickListener
    {
        public bool OnMarkerClick(GoogleMarker marker) => handler.OnGoogleMarkerClicked(marker);
    }
}
#endif
