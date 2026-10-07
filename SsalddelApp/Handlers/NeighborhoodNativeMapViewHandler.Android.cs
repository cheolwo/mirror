#if ANDROID
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.OS;
using Microsoft.Maui.Handlers;
using SsalddelApp.Controls;
using Ssalddel.Ui.Common.Areas.App.Models;
using AColor = Android.Graphics.Color;

namespace SsalddelApp.Handlers;

// 표시 알고리즘은 DriverNativeMapViewHandler의 Google 분기를 재사용하되 기사·미국 운영 모드와 분리합니다.
public sealed class NeighborhoodNativeMapViewHandler : ViewHandler<NeighborhoodNativeMapView, MapView>
{
    public static readonly IPropertyMapper<NeighborhoodNativeMapView, NeighborhoodNativeMapViewHandler> Mapper =
        new PropertyMapper<NeighborhoodNativeMapView, NeighborhoodNativeMapViewHandler>(ViewHandler.ViewMapper)
        { [nameof(NeighborhoodNativeMapView.State)] = (handler, _) => handler.Render() };
    private GoogleMap? _map;
    private MapCallback? _callback;
    private NeighborhoodNativeMapView? _view;
    private readonly Dictionary<string, string> _markerIds = new(StringComparer.Ordinal);
    private bool _cameraInitialized, _connected;
    private long _generation;
    private long? _renderRevision;
    public NeighborhoodNativeMapViewHandler() : base(Mapper) { }

    protected override MapView CreatePlatformView()
    {
        VirtualView.ResetTiles();
        var context = MauiContext?.Context ?? throw new InvalidOperationException("지도 표시 컨텍스트를 확인할 수 없습니다.");
        var view = new MapView(context);
        view.OnCreate((Bundle?)null);
        return view;
    }
    protected override void ConnectHandler(MapView platformView)
    {
        base.ConnectHandler(platformView);
        _connected = true; _view = VirtualView;
        platformView.OnStart(); platformView.OnResume();
        platformView.LayoutChange += OnLayoutChanged;
        _callback = new MapCallback(this, ++_generation);
        platformView.GetMapAsync(_callback);
    }
    private bool Current(long generation) => _connected && generation == _generation && _view is not null;
    private void Ready(GoogleMap map, long generation)
    {
        if (!Current(generation)) return;
        _map = map;
        try
        {
            map.UiSettings.MyLocationButtonEnabled = false;
            map.UiSettings.ZoomControlsEnabled = true;
            map.UiSettings.CompassEnabled = true;
            map.SetMinZoomPreference(3f);
            map.SetMaxZoomPreference(20f);
            map.SetOnMarkerClickListener(_callback);
            map.SetOnMapLoadedCallback(_callback);
            map.SetOnCameraIdleListener(_callback);
            Render();
        }
        catch (Exception) { ClearMap(); _view?.CompleteTiles(false); }
    }
    private void Render()
    {
        if (!_connected || _map is null || _view is null) return;
        if (_view.State is { } unchanged && _renderRevision == unchanged.Revision) return;
        _map.Clear(); _markerIds.Clear();
        if (_view.State is not { } state) { _cameraInitialized = false; _renderRevision = null; return; }
        foreach (var pin in state.Markers.Where(p => Valid(p.Latitude, p.Longitude)))
        {
            using var options = new MarkerOptions().SetPosition(new LatLng(pin.Latitude, pin.Longitude))
                .SetTitle(pin.Label).SetIcon(MarkerIcon(pin.Kind));
            options.InvokeZIndex(pin.Id == state.SelectedMarkerId ? 300f
                : pin.Kind == NeighborhoodMapMarkerKinds.Inactive ? 10f
                : pin.Kind == NeighborhoodMapMarkerKinds.Region ? 100f : 200f);
            var marker = _map.AddMarker(options);
            if (marker is not null) _markerIds[marker.Id] = pin.Id;
        }
        foreach (var route in state.Routes)
        {
            var points = route.Points.Where(p => Valid(p.Latitude, p.Longitude)).ToArray();
            if (points.Length < 2) continue;
            using var options = new PolylineOptions();
            foreach (var point in points) options.Add(new LatLng(point.Latitude, point.Longitude));
            options.InvokeWidth(8f);
            if (route.SourceCode == "cargo-straight-candidate")
                options.InvokePattern(new List<PatternItem> { new Dash(16f), new Gap(12f) });
            try { options.InvokeColor(AColor.ParseColor(route.Color)); }
            catch (Java.Lang.IllegalArgumentException) { options.InvokeColor(AColor.Rgb(107, 114, 128)); }
            _map.AddPolyline(options);
        }
        MoveCamera(state);
        _renderRevision = state.Revision;
    }
    private void MoveCamera(NeighborhoodMapRenderState state)
    {
        if (_map is null || (_cameraInitialized && state.PreserveViewport)) return;
        if (state.Viewport is { } viewport && Valid(viewport.Latitude, viewport.Longitude))
        {
            _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(new LatLng(viewport.Latitude, viewport.Longitude), (float)Math.Clamp(viewport.Zoom, 3, 20)));
        }
        else
        {
            var points = FocusPoints(state);
            if (points.Count > 0)
            {
                if (points.Count == 1)
                    _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(new LatLng(points[0].Latitude, points[0].Longitude), 13f));
                else
                {
                    // Android 실제 표시 크기가 정해진 다음 맞춥니다. 고정 640x480은 작은 휴대폰을 벗어납니다.
                    var width = PlatformView.Width; var height = PlatformView.Height;
                    if (width <= 0 || height <= 0) return;
                    var bounds = new LatLngBounds.Builder();
                    foreach (var point in points) bounds.Include(new LatLng(point.Latitude, point.Longitude));
                    var density = PlatformView.Resources?.DisplayMetrics?.Density ?? 1f;
                    var padding = Math.Max(0, Math.Min((int)(32 * density), (Math.Min(width, height) - 1) / 4));
                    _map.MoveCamera(CameraUpdateFactory.NewLatLngBounds(bounds.Build(), width, height, padding));
                }
            }
            else _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(new LatLng(36.4, 127.8), 7f));
        }
        _cameraInitialized = true;
    }
    private static List<NeighborhoodMapPoint> FocusPoints(NeighborhoodMapRenderState state)
    {
        var selected = state.SelectedMarkerId;
        if (selected?.StartsWith("delivery:", StringComparison.Ordinal) == true && selected.LastIndexOf(':') > 9)
        {
            var requestId = selected[9..selected.LastIndexOf(':')];
            var prefix = "delivery:" + requestId + ":";
            return state.Markers.Where(pin => pin.Id.StartsWith(prefix, StringComparison.Ordinal))
                .Select(pin => new NeighborhoodMapPoint(pin.Latitude, pin.Longitude))
                .Concat(state.Routes.Where(route => route.Id == requestId).SelectMany(route => route.Points))
                .Where(point => Valid(point.Latitude, point.Longitude)).Distinct().ToList();
        }
        if (selected is not null && state.Markers.FirstOrDefault(pin => pin.Id == selected && Valid(pin.Latitude, pin.Longitude)) is { } marker)
            return [new(marker.Latitude, marker.Longitude)];
        return state.Markers.Where(pin => Valid(pin.Latitude, pin.Longitude))
            .Select(pin => new NeighborhoodMapPoint(pin.Latitude, pin.Longitude)).Distinct().ToList();
    }
    private void OnLayoutChanged(object? sender, Android.Views.View.LayoutChangeEventArgs args)
    {
        if (!_connected || _view?.State is not { } state || _cameraInitialized) return;
        try { MoveCamera(state); }
        catch (Exception) { ClearMap(); _view?.CompleteTiles(false); }
    }
    protected override void DisconnectHandler(MapView platformView)
    {
        _connected = false; ++_generation;
        platformView.LayoutChange -= OnLayoutChanged;
        ClearMap(); _map = null; _markerIds.Clear(); _cameraInitialized = false; _renderRevision = null;
        _view?.CompleteTiles(false); _view = null;
        try { platformView.OnPause(); } catch (Exception) { }
        try { platformView.OnStop(); } catch (Exception) { }
        try { platformView.OnDestroy(); } catch (Exception) { }
        try { _callback?.Dispose(); }
        finally { _callback = null; base.DisconnectHandler(platformView); }
    }
    private void ClearMap()
    {
        try { _map?.SetOnMarkerClickListener(null); _map?.SetOnMapLoadedCallback(null); _map?.SetOnCameraIdleListener(null); _map?.Clear(); }
        catch (Exception) { }
        _markerIds.Clear();
    }
    private static bool Valid(double lat, double lng) => double.IsFinite(lat) && double.IsFinite(lng) && lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
    private static BitmapDescriptor MarkerIcon(string kind)
    {
        if (kind is NeighborhoodMapMarkerKinds.Region or NeighborhoodMapMarkerKinds.Pickup or NeighborhoodMapMarkerKinds.Dropoff or NeighborhoodMapMarkerKinds.Driver or NeighborhoodMapMarkerKinds.PublicData)
            return BitmapDescriptorFactory.DefaultMarker(kind switch
            { NeighborhoodMapMarkerKinds.Pickup => 30f, NeighborhoodMapMarkerKinds.Dropoff => 210f,
                NeighborhoodMapMarkerKinds.Driver or NeighborhoodMapMarkerKinds.PublicData => 270f, _ => 160f });
        using var bitmap = Android.Graphics.Bitmap.CreateBitmap(36, 36, Android.Graphics.Bitmap.Config.Argb8888!);
        using var canvas = new Android.Graphics.Canvas(bitmap!);
        using var paint = new Android.Graphics.Paint { AntiAlias = true, Color = AColor.White };
        canvas.DrawCircle(18, 18, 17, paint);
        paint.Color = AColor.Rgb(107, 114, 128);
        canvas.DrawCircle(18, 18, 14, paint);
        return BitmapDescriptorFactory.FromBitmap(bitmap!);
    }
    private sealed class MapCallback(NeighborhoodNativeMapViewHandler owner, long generation) : Java.Lang.Object,
        IOnMapReadyCallback, GoogleMap.IOnMarkerClickListener, GoogleMap.IOnMapLoadedCallback, GoogleMap.IOnCameraIdleListener
    {
        public void OnMapReady(GoogleMap map) => owner.Ready(map, generation);
        public void OnMapLoaded() { if (owner.Current(generation)) owner._view?.CompleteTiles(true); }
        public void OnCameraIdle()
        {
            if (!owner.Current(generation) || owner._map?.CameraPosition is not { } camera) return;
            var target = camera.Target;
            if (target is not null && Valid(target.Latitude, target.Longitude) && float.IsFinite(camera.Zoom))
                owner._view?.ChangeViewport(new(target.Latitude, target.Longitude, Math.Clamp(camera.Zoom, 3, 20)));
        }
        public bool OnMarkerClick(Marker marker)
        {
            if (owner.Current(generation) && owner._markerIds.TryGetValue(marker.Id, out var id)) owner._view?.SelectMarker(id);
            return true;
        }
    }
}
#endif
