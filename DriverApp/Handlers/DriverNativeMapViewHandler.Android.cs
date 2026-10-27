#if ANDROID
using Android.Views;
using Android.Widget;
using DriverApp.Controls;
using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Operations;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Dispatching;
using GoogleMap = Android.Gms.Maps.GoogleMap;
using NaverMap = Com.Naver.Maps.Map.NaverMap;
using AndroidColor = Android.Graphics.Color;

namespace DriverApp.Handlers;

public partial class DriverNativeMapViewHandler : ViewHandler<DriverNativeMapView, FrameLayout>
{
    private FrameLayout? _container;
    private NaverMapSurface? _naverSurface;
    private GoogleMapSurface? _googleSurface;
    // 방향 partial과 타이머는 제공자 자원을 소유하지 않고 현재 map 참조만 읽습니다.
    private NaverMap? _naverMap => _naverSurface?.Map;
    private GoogleMap? _googleMap => _googleSurface?.Map;
    private Android.Views.View? _naverMapView => _naverSurface?.View;
    private Android.Views.View? _googleMapView => _googleSurface?.View;
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
        => (_naverSurface ??= new NaverMapSurface(this)).EnsureCreated();

    private void EnsureGoogleMapView()
        => (_googleSurface ??= new GoogleMapSurface(this)).EnsureCreated();

    // 양쪽 overlay를 먼저 비운 뒤 뷰·참조·listener를 기존 전역 순서로 해제합니다.
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
        _googleSurface?.DisposeInactiveMarkerIcon();
        _naverSurface?.DestroyView();
        _googleSurface?.DestroyView();
        _naverSurface?.ClearMapReference();
        _googleSurface?.ClearMapReference();
        _container = null;
        _naverSurface?.ClearViewReference();
        _googleSurface?.ClearViewReference();
        _googleSurface?.DisposeMarkerClickListener();
        base.DisconnectHandler(platformView);
    }

    private bool UsesGoogleMaps
        => string.Equals(
            VirtualView?.MapProviderCode,
            OperatingMapProviderCodes.GoogleMaps,
            StringComparison.OrdinalIgnoreCase);

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

    private IReadOnlyList<DriverRecommendationMarkerPresentation> RecommendationMarkerPresentations()
        => DriverRecommendationMarkerPresentationPolicy.Build(VirtualView.Markers,
            VirtualView.HighlightSelectedRecommendation, VirtualView.SelectedRecommendationRequestId);

    private void ApplyNaverMapOptions()
        => _naverSurface?.ApplyNaverMapOptions();

    private void ApplyNaverCamera()
        => _naverSurface?.ApplyNaverCamera();

    private void ApplyNaverMarkers()
        => _naverSurface?.ApplyNaverMarkers();

    private void ApplyNaverRouteOverlays()
        => _naverSurface?.ApplyNaverRouteOverlays();

    private void ApplyNaverLocationOverlay()
        => _naverSurface?.ApplyNaverLocationOverlay();

    private void ClearNaverMarkers()
        => _naverSurface?.ClearNaverMarkers();

    private void ClearNaverRouteOverlays()
        => _naverSurface?.ClearNaverRouteOverlays();

    private void ApplyGoogleMapOptions()
        => _googleSurface?.ApplyGoogleMapOptions();

    private void ApplyGoogleCamera()
        => _googleSurface?.ApplyGoogleCamera();

    private void ApplyGoogleMarkers()
        => _googleSurface?.ApplyGoogleMarkers();

    private void ApplyGoogleRouteOverlays()
        => _googleSurface?.ApplyGoogleRouteOverlays();

    private void ApplyGoogleLocationOverlay()
        => _googleSurface?.ApplyGoogleLocationOverlay();

    private void ClearGoogleMarkers()
        => _googleSurface?.ClearGoogleMarkers();

    private void ClearGoogleRouteOverlays()
        => _googleSurface?.ClearGoogleRouteOverlays();

    private void ClearGoogleCurrentLocationMarker()
        => _googleSurface?.ClearGoogleCurrentLocationMarker();

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
}
#endif
