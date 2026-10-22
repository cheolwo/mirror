#if ANDROID
using Android.OS;
using Android.Views;
using Android.Widget;
using Com.Naver.Maps.Map;
using Microsoft.Maui.Handlers;
using SsalddelApp.Controls;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace SsalddelApp.Handlers;

/// <summary>권한이 확인된 지도 상태만 네이버 SDK에 표시합니다. 위치 수집·배차 권한은 소유하지 않습니다.</summary>
public sealed partial class NeighborhoodNativeMapViewHandler : ViewHandler<NeighborhoodNativeMapView, FrameLayout>
{
    public static readonly IPropertyMapper<NeighborhoodNativeMapView, NeighborhoodNativeMapViewHandler> Mapper =
        new PropertyMapper<NeighborhoodNativeMapView, NeighborhoodNativeMapViewHandler>(ViewHandler.ViewMapper)
        { [nameof(NeighborhoodNativeMapView.State)] = (handler, _) => handler.Render() };
    private NaverMap? _map;
    private MapView? _nativeMapView;
    private NaverMapSdk? _sdk;
    private MapCallback? _callback;
    private NeighborhoodNativeMapView? _view;
    private CancellationTokenSource? _expiryCancellation;
    private bool _cameraInitialized, _connected, _authenticationFailed;
    private long _generation;
    private long? _renderRevision;
    public NeighborhoodNativeMapViewHandler() : base(Mapper) { }

    internal static bool IsConfigured(Android.Content.Context context)
    {
        var resourceId = context.Resources?.GetIdentifier("naver_map_sdk_ncp_key_id", "string", context.PackageName) ?? 0;
        var key = resourceId == 0 ? null : context.GetString(resourceId)?.Trim();
        return !string.IsNullOrWhiteSpace(key)
            && !key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
            && !key.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("NOT_CONFIGURED", StringComparison.OrdinalIgnoreCase);
    }

    protected override FrameLayout CreatePlatformView()
    {
        VirtualView.ResetTiles();
        var context = MauiContext?.Context ?? throw new InvalidOperationException("지도 표시 컨텍스트를 확인할 수 없습니다.");
        return new FrameLayout(context);
    }

    protected override void ConnectHandler(FrameLayout platformView)
    {
        base.ConnectHandler(platformView);
        _connected = true; _view = VirtualView; _authenticationFailed = false;
        var generation = ++_generation;
        if (!IsConfigured(platformView.Context!)) { Fail(); return; }
        try
        {
            _sdk = NaverMapSdk.GetInstance(platformView.Context!);
            _sdk.AuthFailed += OnAuthenticationFailed;
            var mapView = new MapView(platformView.Context!)
            {
                LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
            };
            _nativeMapView = mapView;
            platformView.AddView(mapView);
            mapView.OnCreate((Bundle?)null); mapView.OnStart(); mapView.OnResume();
            mapView.LayoutChange += OnLayoutChanged;
            _callback = new MapCallback(this, generation);
            mapView.GetMapAsync(_callback);
        }
        catch (Exception) { Fail(); }
    }

    private bool Current(long generation) => _connected && generation == _generation && _view is not null;

    private void Ready(NaverMap map, long generation)
    {
        if (!Current(generation) || _authenticationFailed) return;
        _map = map;
        try
        {
            // SDK 객체 준비와 실제 배경 타일 로딩은 별개의 결과입니다.
            map.Load += OnTilesLoaded;
            map.CameraIdle += OnCameraIdle;
            map.Locale = Java.Util.Locale.ForLanguageTag("ko-KR");
            map.MinZoom = 3; map.MaxZoom = 20; map.LiteModeEnabled = false;
            map.SetLayerGroupEnabled(NaverMap.LayerGroupTraffic, false);
            map.SetLayerGroupEnabled(NaverMap.LayerGroupTransit, false);
            map.LocationTrackingMode = LocationTrackingMode.None!;
            map.LocationOverlay.Visible = false;
            var settings = map.UiSettings;
            settings.LocationButtonEnabled = false;
            settings.ZoomControlEnabled = true;
            settings.CompassEnabled = true;
            settings.ScaleBarEnabled = true;
            var margin = (int)(8 * (PlatformView.Resources?.DisplayMetrics?.Density ?? 1f));
            settings.SetLogoMargin(margin, margin, margin, margin);
            Render();
        }
        catch (Exception) { Fail(); }
    }

    private void OnTilesLoaded(object? sender, EventArgs args)
    {
        if (_connected && !_authenticationFailed && ReferenceEquals(sender, _map)) _view?.CompleteTiles(true);
    }

    private void OnAuthenticationFailed(object? sender, NaverMapSdk.AuthFailedEventArgs args)
    {
        var generation = _generation;
#if DEBUG
        var code = args.P0.ErrorCode is "401" or "429" or "800" ? args.P0.ErrorCode : "unknown";
#endif
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!Current(generation)) return;
            // 원본 SDK 예외에는 설정값이 포함될 수 있으므로 정해진 코드만 기록합니다.
#if DEBUG
            Android.Util.Log.Warn("MemberMap", $"Naver SDK authentication failed: {code}");
#endif
            _authenticationFailed = true;
            Fail();
        });
    }

    private void OnCameraIdle(object? sender, EventArgs args)
    {
        if (!_connected || _authenticationFailed || _map?.CameraPosition is not { } camera) return;
        var point = camera.Target;
        if (point is not null && Valid(point.Latitude, point.Longitude) && double.IsFinite(camera.Zoom))
            _view?.ChangeViewport(new(point.Latitude, point.Longitude, Math.Clamp(camera.Zoom, 3, 20)));
    }

    private void OnLayoutChanged(object? sender, Android.Views.View.LayoutChangeEventArgs args)
    {
        if (!_connected || _authenticationFailed || _cameraInitialized || _view?.State is not { } state) return;
        try { MoveCamera(state); }
        catch (Exception) { Fail(); }
    }

    private void Fail()
    {
        _authenticationFailed = true;
        CancelExpiry(); ClearOverlays();
        Release(() => { if (_nativeMapView is not null) _nativeMapView.Visibility = ViewStates.Gone; });
        _view?.CompleteTiles(false);
    }

    protected override void DisconnectHandler(FrameLayout platformView)
    {
        _connected = false; ++_generation;
        CancelExpiry();
        if (_sdk is not null) Release(() => _sdk.AuthFailed -= OnAuthenticationFailed);
        _sdk = null;
        if (_map is not null)
        {
            Release(() => _map.Load -= OnTilesLoaded);
            Release(() => _map.CameraIdle -= OnCameraIdle);
        }
        ClearOverlays(); _map = null; _cameraInitialized = false; _renderRevision = null;
        _view?.CompleteTiles(false); _view = null;
        var mapView = _nativeMapView; _nativeMapView = null;
        if (mapView is not null)
        {
            Release(() => mapView.LayoutChange -= OnLayoutChanged);
            Release(mapView.OnPause); Release(mapView.OnStop); Release(mapView.OnDestroy);
            Release(() => platformView.RemoveView(mapView)); Release(mapView.Dispose);
        }
        var callback = _callback; _callback = null;
        if (callback is not null) Release(callback.Dispose);
        base.DisconnectHandler(platformView);
    }

    private static void Release(Action release) { try { release(); } catch (Exception) { } }

    private sealed class MapCallback(NeighborhoodNativeMapViewHandler owner, long generation)
        : Java.Lang.Object, IOnMapReadyCallback
    {
        public void OnMapReady(NaverMap map) => owner.Ready(map, generation);
    }
}
#endif
