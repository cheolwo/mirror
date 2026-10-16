#if ANDROID
using Android.OS;
using Android.Views;
using Android.Widget;
using Com.Naver.Maps.Map.Util;
using DriverApp.Controls;
using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Operations;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Dispatching;
using GoogleBitmapDescriptorFactory = Android.Gms.Maps.Model.BitmapDescriptorFactory;
using GoogleBitmapDescriptor = Android.Gms.Maps.Model.BitmapDescriptor;
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
    private GoogleBitmapDescriptor? _googleInactiveMarkerIcon;
    private IDispatcherTimer? _locationFreshnessTimer;
    private DriverNativeLocationPresentation? _locationPresentation;
    private long _mapLifecycleGeneration;

    protected override FrameLayout CreatePlatformView()
    {
        ++_mapLifecycleGeneration;
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
        _naverMapView.GetMapAsync(new NaverMapReadyCallback(this, _mapLifecycleGeneration));
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
        _googleMapView.GetMapAsync(new GoogleMapReadyCallback(this, _mapLifecycleGeneration));
        _container.AddView(_googleMapView);
    }

    protected override void DisconnectHandler(FrameLayout platformView)
    {
        ++_mapLifecycleGeneration;
        StopDirectionCompass();
        StopLocationFreshnessTimer();
        _locationPresentation = null;
        ClearNaverMarkers();
        ClearNaverRouteOverlays();
        ClearGoogleMarkers();
        ClearGoogleRouteOverlays();
        ClearGoogleCurrentLocationMarker();
        _googleInactiveMarkerIcon?.Dispose();
        _googleInactiveMarkerIcon = null;

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

    private void OnNaverMapReady(NaverMap naverMap, long generation)
    {
        if (_container is null || generation != _mapLifecycleGeneration) return;
        _naverMap = naverMap;
        _naverMap.Locale = Java.Util.Locale.ForLanguageTag("ko-KR");
        ApplyNaverMapOptions();
        ApplyNaverCamera();
        ApplyNaverMarkers();
        ApplyNaverRouteOverlays();
        RefreshLocationPresentation(force: true);
        StartLocationFreshnessTimer();
    }

    private void OnGoogleMapReady(GoogleMap googleMap, long generation)
    {
        if (_container is null || generation != _mapLifecycleGeneration) return;
        _googleMap = googleMap;
        _googleMarkerClickListener = new GoogleMarkerClickListener(this);
        _googleMap.SetOnMarkerClickListener(_googleMarkerClickListener);
        ApplyGoogleMapOptions();
        ApplyGoogleCamera();
        ApplyGoogleMarkers();
        ApplyGoogleRouteOverlays();
        RefreshLocationPresentation(force: true);
        StartLocationFreshnessTimer();
    }

    public static void MapCamera(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyCamera();

    public static void MapMarkers(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyMarkers();

    public static void MapRouteOverlays(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyRouteOverlays();

    public static void MapOptions(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.ApplyMapOptions();

    public static void MapCurrentLocation(DriverNativeMapViewHandler handler, DriverNativeMapView view)
    {
        handler.RefreshLocationPresentation(force: true);
        if (view.CurrentLocation is null) handler.StopLocationFreshnessTimer();
        else handler.StartLocationFreshnessTimer();
    }

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
        RefreshLocationPresentation(force: true);
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
        uiSettings.LocationButtonEnabled = VirtualView.ShowLocationButton
            && DriverNativeLocationPolicy.Present(VirtualView.CurrentLocation, DateTime.UtcNow).IsAvailable;
        uiSettings.SetLogoMargin(16, 16, 16, 120);

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
    }

    private void ApplyGoogleCamera()
    {
        if (_googleMap is null || VirtualView is null)
        {
            return;
        }

        var target = new GoogleLatLng(VirtualView.CenterLatitude, VirtualView.CenterLongitude);
        _googleMap.MoveCamera(GoogleCameraUpdateFactory.NewLatLngZoom(target, (float)VirtualView.Zoom));
    }

    private void ApplyNaverMarkers()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        ClearNaverMarkers();
        foreach (var pin in RecommendationMarkerPresentations())
        {
            AddNaverMarker(pin);
        }
    }

    private void ApplyGoogleMarkers()
    {
        if (_googleMap is null || VirtualView is null)
        {
            return;
        }

        ClearGoogleMarkers();
        foreach (var pin in RecommendationMarkerPresentations())
        {
            AddGoogleMarker(pin);
        }
    }

    private IReadOnlyList<DriverRecommendationMarkerPresentation> RecommendationMarkerPresentations()
        => DriverRecommendationMarkerPresentationPolicy.Build(VirtualView.Markers,
            VirtualView.HighlightSelectedRecommendation, VirtualView.SelectedRecommendationRequestId);

    private void ApplyNaverRouteOverlays()
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        ClearNaverRouteOverlays();
        foreach (var item in DriverNativeLocationPolicy.RoutesForDisplay(VirtualView.RouteOverlays, VirtualView.CurrentLocation, DateTime.UtcNow))
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
        foreach (var item in DriverNativeLocationPolicy.RoutesForDisplay(VirtualView.RouteOverlays, VirtualView.CurrentLocation, DateTime.UtcNow))
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
        var point = _locationPresentation?.Point;
        _naverMap.LocationTrackingMode = Com.Naver.Maps.Map.LocationTrackingMode.None!;
        if (point is not null)
            overlay.Position = new NaverLatLng((double)point.Latitude, (double)point.Longitude);
        overlay.CircleColor = AndroidColor.Argb(40, 25, 118, 210);
        overlay.CircleOutlineColor = AndroidColor.Argb(120, 25, 118, 210);
        overlay.CircleOutlineWidth = 2;
        overlay.Visible = VirtualView.ShowCurrentLocationOverlay && point is not null;
        RefreshDirectionPresentation();
    }

    private void ApplyGoogleLocationOverlay()
    {
        ClearGoogleCurrentLocationMarker();
        var point = _locationPresentation?.Point;
        if (_googleMap is null || VirtualView is null || !VirtualView.ShowCurrentLocationOverlay || point is null)
        {
            return;
        }

        var options = new GoogleMarkerOptions()
            .SetPosition(new GoogleLatLng((double)point.Latitude, (double)point.Longitude))
            .SetTitle("최근 확인 위치")
            .SetSnippet(_locationPresentation!.Notice)
            .SetIcon(GoogleBitmapDescriptorFactory.DefaultMarker(210f));
        _googleCurrentLocationMarker = _googleMap.AddMarker(options);
    }

    private void RefreshLocationPresentation(bool force = false)
    {
        if (VirtualView is null || _container is null) return;
        var presentation = DriverNativeLocationPolicy.Present(VirtualView.CurrentLocation, DateTime.UtcNow);
        if (!force && presentation == _locationPresentation) return;
        _locationPresentation = presentation;
        ApplyNaverMapOptions();
        ApplyNaverLocationOverlay();
        ApplyGoogleLocationOverlay();
        ApplyRouteOverlays();
        VirtualView.SendLocationPresentationChanged(presentation);
    }

    private void StartLocationFreshnessTimer()
    {
        if (_locationFreshnessTimer is not null || VirtualView?.CurrentLocation is null
            || (_naverMap is null && _googleMap is null)) return;
        _locationFreshnessTimer = VirtualView.Dispatcher.CreateTimer();
        _locationFreshnessTimer.Interval = TimeSpan.FromSeconds(1);
        _locationFreshnessTimer.Tick += OnLocationFreshnessTick;
        _locationFreshnessTimer.Start();
    }

    private void OnLocationFreshnessTick(object? sender, EventArgs e)
    {
        RefreshLocationPresentation();
        // 위치가 변하지 않아도 센서 방향의 신선도는 별도로 만료됩니다.
        RefreshDirectionPresentation();
    }

    private void StopLocationFreshnessTimer()
    {
        if (_locationFreshnessTimer is null) return;
        _locationFreshnessTimer.Stop();
        _locationFreshnessTimer.Tick -= OnLocationFreshnessTick;
        _locationFreshnessTimer = null;
    }

    private void AddNaverMarker(DriverRecommendationMarkerPresentation pin)
    {
        if (_naverMap is null || VirtualView is null)
        {
            return;
        }

        var marker = new NaverMarker
        {
            Position = new NaverLatLng(pin.Latitude, pin.Longitude),
            Icon = NaverMarkerIcons.Black,
            IconTintColor = ParseColor(pin.TintColor, AndroidColor.Gray),
            CaptionText = pin.Caption,
            SubCaptionText = pin.SubCaption,
            ZIndex = pin.ZIndex
        };
        marker.Click += (_, _) => VirtualView.SendMarkerSelected(pin.Source);
        marker.Map = _naverMap;
        _naverMarkers.Add(marker);
    }

    private void AddGoogleMarker(DriverRecommendationMarkerPresentation pin)
    {
        if (_googleMap is null)
        {
            return;
        }

        var options = new GoogleMarkerOptions()
            .SetPosition(new GoogleLatLng(pin.Latitude, pin.Longitude))
            .SetTitle(pin.Caption)
            .SetSnippet(pin.SubCaption)
            .SetIcon(pin.IsMuted ? GetGoogleInactiveMarkerIcon() : GoogleBitmapDescriptorFactory.DefaultMarker(pin.IsPickup ? 30f : 210f));
        options.InvokeZIndex(pin.ZIndex);
        var marker = _googleMap.AddMarker(options);
        if (marker is null)
        {
            return;
        }

        _googleMarkers.Add(marker);
        if (!string.IsNullOrWhiteSpace(marker.Id))
        {
            _googleMarkerItems[marker.Id] = pin.Source;
        }
    }

    private GoogleBitmapDescriptor GetGoogleInactiveMarkerIcon()
    {
        if (_googleInactiveMarkerIcon is not null) return _googleInactiveMarkerIcon;

        // Google의 기본 마커 hue는 채도를 낮출 수 없어 비선택 핀만 회색으로 직접 그립니다.
        var density = Math.Max(1f, MauiContext?.Context?.Resources?.DisplayMetrics?.Density ?? 1f);
        using var bitmap = Android.Graphics.Bitmap.CreateBitmap((int)Math.Ceiling(32f * density),
            (int)Math.Ceiling(44f * density), Android.Graphics.Bitmap.Config.Argb8888!)
            ?? throw new InvalidOperationException("비선택 지도 핀을 만들지 못했습니다.");
        using var canvas = new Android.Graphics.Canvas(bitmap);
        canvas.Scale(density, density);
        using var paint = new Android.Graphics.Paint { AntiAlias = true };
        using var path = new Android.Graphics.Path();
        path.MoveTo(16f, 42f);
        path.CubicTo(12f, 36f, 3f, 24f, 3f, 16f);
        path.CubicTo(3f, 8.8f, 8.8f, 3f, 16f, 3f);
        path.CubicTo(23.2f, 3f, 29f, 8.8f, 29f, 16f);
        path.CubicTo(29f, 24f, 20f, 36f, 16f, 42f);
        path.Close();
        paint.Color = ParseColor(DriverRecommendationMarkerPresentationPolicy.InactiveTintColor, AndroidColor.Gray);
        paint.SetStyle(Android.Graphics.Paint.Style.Fill!);
        canvas.DrawPath(path, paint);
        paint.Color = AndroidColor.White;
        paint.StrokeWidth = 1.5f;
        paint.SetStyle(Android.Graphics.Paint.Style.Stroke!);
        canvas.DrawPath(path, paint);
        paint.SetStyle(Android.Graphics.Paint.Style.Fill!);
        canvas.DrawCircle(16f, 16f, 4f, paint);
        _googleInactiveMarkerIcon = GoogleBitmapDescriptorFactory.FromBitmap(bitmap)
            ?? throw new InvalidOperationException("비선택 지도 핀을 연결하지 못했습니다.");
        return _googleInactiveMarkerIcon;
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

    private sealed class NaverMapReadyCallback(DriverNativeMapViewHandler handler, long generation)
        : Java.Lang.Object, Com.Naver.Maps.Map.IOnMapReadyCallback
    {
        public void OnMapReady(NaverMap naverMap) => handler.OnNaverMapReady(naverMap, generation);
    }

    private sealed class GoogleMapReadyCallback(DriverNativeMapViewHandler handler, long generation)
        : Java.Lang.Object, Android.Gms.Maps.IOnMapReadyCallback
    {
        public void OnMapReady(GoogleMap googleMap) => handler.OnGoogleMapReady(googleMap, generation);
    }

    private sealed class GoogleMarkerClickListener(DriverNativeMapViewHandler handler)
        : Java.Lang.Object, GoogleMap.IOnMarkerClickListener
    {
        public bool OnMarkerClick(GoogleMarker marker) => handler.OnGoogleMarkerClicked(marker);
    }
}
#endif
