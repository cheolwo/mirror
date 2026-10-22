#if ANDROID
using Com.Naver.Maps.Geometry;
using Com.Naver.Maps.Map;
using Com.Naver.Maps.Map.Overlay;
using Com.Naver.Maps.Map.Util;
using Ssalddel.Ui.Common.Areas.App.Models;
using AColor = Android.Graphics.Color;

namespace SsalddelApp.Handlers;

public sealed partial class NeighborhoodNativeMapViewHandler
{
    private sealed record MarkerBinding(Marker Marker, EventHandler<Overlay.ClickEventArgs> Selected);
    private readonly List<MarkerBinding> _markers = [];
    private readonly List<PolylineOverlay> _routes = [];

    private void Render(bool force = false)
    {
        if (!_connected || _authenticationFailed || _map is null || _view is null) return;
        if (!force && _view.State is { } unchanged && _renderRevision == unchanged.Revision) return;
        CancelExpiry(); ClearOverlays();
        if (_view.State is not { } state) { _cameraInitialized = false; _renderRevision = null; return; }
        try
        {
            var now = DateTimeOffset.UtcNow;
            var generation = _generation;
            foreach (var pin in state.Markers.Where(p => Valid(p.Latitude, p.Longitude) && CurrentAt(p.ExpiresAt, now)))
            {
                var marker = new Marker
                {
                    Position = new LatLng(pin.Latitude, pin.Longitude),
                    CaptionText = pin.Label,
                    Icon = MarkerIcons.Black,
                    IconTintColor = MarkerColor(pin.Kind),
                    ZIndex = pin.Id == state.SelectedMarkerId ? 300
                        : pin.Kind == NeighborhoodMapMarkerKinds.Inactive ? 10
                        : pin.Kind == NeighborhoodMapMarkerKinds.Region ? 100 : 200
                };
                EventHandler<Overlay.ClickEventArgs> selected = (_, args) =>
                {
                    args.Handled = true;
                    if (Current(generation) && !_authenticationFailed && _view?.State?.Revision == state.Revision
                        && CurrentAt(pin.ExpiresAt, DateTimeOffset.UtcNow))
                        _view.SelectMarker(pin.Id);
                };
                _markers.Add(new(marker, selected));
                marker.Click += selected;
                marker.Map = _map;
            }
            foreach (var route in state.Routes.Where(route => CurrentAt(route.ExpiresAt, now)))
            {
                var points = route.Points.Where(p => Valid(p.Latitude, p.Longitude)).ToArray();
                if (points.Length < 2) continue;
                var overlay = new PolylineOverlay
                {
                    Coords = points.Select(p => new LatLng(p.Latitude, p.Longitude)).ToList(),
                    Width = 8,
                    Color = ParseColor(route.Color)
                };
                _routes.Add(overlay);
                // 직선 후보를 검증된 도로 경로와 같은 실선으로 표현하지 않습니다.
                if (route.SourceCode == "cargo-straight-candidate") overlay.SetPattern(16, 12);
                overlay.Map = _map;
            }
            MoveCamera(state);
            _renderRevision = state.Revision;
            ScheduleExpiry(state, generation, now);
        }
        catch (Exception) { Fail(); }
    }

    private void MoveCamera(NeighborhoodMapRenderState state)
    {
        if (_map is null || (_cameraInitialized && state.PreserveViewport)) return;
        if (state.Viewport is { } viewport && Valid(viewport.Latitude, viewport.Longitude) && double.IsFinite(viewport.Zoom))
        {
            _map.MoveCamera(CameraUpdate.ScrollAndZoomTo(new LatLng(viewport.Latitude, viewport.Longitude), Math.Clamp(viewport.Zoom, 3, 20)));
        }
        else
        {
            var points = FocusPoints(state);
            if (points.Count == 1)
                _map.MoveCamera(CameraUpdate.ScrollAndZoomTo(new LatLng(points[0].Latitude, points[0].Longitude), 13));
            else if (points.Count > 1)
            {
                var width = PlatformView.Width; var height = PlatformView.Height;
                if (width <= 0 || height <= 0) return;
                var density = PlatformView.Resources?.DisplayMetrics?.Density ?? 1f;
                var padding = Math.Max(0, Math.Min((int)(32 * density), (Math.Min(width, height) - 1) / 4));
                var bounds = LatLngBounds.From(points.Select(p => new LatLng(p.Latitude, p.Longitude)).ToList());
                _map.MoveCamera(CameraUpdate.FitBounds(bounds, padding));
            }
            else _map.MoveCamera(CameraUpdate.ScrollAndZoomTo(new LatLng(36.4, 127.8), 7));
        }
        _cameraInitialized = true;
    }

    private static List<NeighborhoodMapPoint> FocusPoints(NeighborhoodMapRenderState state)
    {
        var now = DateTimeOffset.UtcNow;
        var markers = state.Markers.Where(pin => CurrentAt(pin.ExpiresAt, now) && Valid(pin.Latitude, pin.Longitude)).ToArray();
        var selected = state.SelectedMarkerId;
        if (selected?.StartsWith("delivery:", StringComparison.Ordinal) == true && selected.LastIndexOf(':') > 9)
        {
            var requestId = selected[9..selected.LastIndexOf(':')];
            var prefix = "delivery:" + requestId + ":";
            return markers.Where(pin => pin.Id.StartsWith(prefix, StringComparison.Ordinal))
                .Select(pin => new NeighborhoodMapPoint(pin.Latitude, pin.Longitude))
                .Concat(state.Routes.Where(route => route.Id == requestId && CurrentAt(route.ExpiresAt, now)).SelectMany(route => route.Points))
                .Where(point => Valid(point.Latitude, point.Longitude)).Distinct().ToList();
        }
        if (selected is not null && markers.FirstOrDefault(pin => pin.Id == selected) is { } marker)
            return [new(marker.Latitude, marker.Longitude)];
        return markers.Select(pin => new NeighborhoodMapPoint(pin.Latitude, pin.Longitude)).Distinct().ToList();
    }

    private void ClearOverlays()
    {
        foreach (var binding in _markers)
        {
            Release(() => binding.Marker.Click -= binding.Selected);
            Release(() => binding.Marker.Map = null);
            Release(binding.Marker.Dispose);
        }
        _markers.Clear();
        foreach (var route in _routes) { Release(() => route.Map = null); Release(route.Dispose); }
        _routes.Clear();
    }

    private void ScheduleExpiry(NeighborhoodMapRenderState state, long generation, DateTimeOffset now)
    {
        var expiry = state.Markers.Select(pin => pin.ExpiresAt).Concat(state.Routes.Select(route => route.ExpiresAt))
            .Where(value => value is { } date && date > now).Min();
        if (expiry is null) return;
        _expiryCancellation = new();
        _ = RemoveExpiredAsync(expiry.Value - now, generation, state.Revision, _expiryCancellation.Token);
    }

    private async Task RemoveExpiredAsync(TimeSpan delay, long generation, long revision, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(delay.TotalMilliseconds, 1, TimeSpan.FromHours(1).TotalMilliseconds)), cancellationToken);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested && Current(generation) && _view?.State?.Revision == revision)
                    Render(force: true);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            try { await MainThread.InvokeOnMainThreadAsync(() => { if (Current(generation)) Fail(); }); }
            catch (Exception) { }
        }
    }

    private void CancelExpiry()
    {
        var cancellation = _expiryCancellation; _expiryCancellation = null;
        if (cancellation is null) return;
        try { cancellation.Cancel(); } catch (ObjectDisposedException) { }
        cancellation.Dispose();
    }

    private static bool CurrentAt(DateTimeOffset? expiry, DateTimeOffset now) => expiry is null || expiry > now;
    private static bool Valid(double lat, double lng) => double.IsFinite(lat) && double.IsFinite(lng) && lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
    private static int ParseColor(string color)
    {
        try { return AColor.ParseColor(color); }
        catch (Java.Lang.IllegalArgumentException) { return AColor.Rgb(107, 114, 128); }
    }
    private static int MarkerColor(string kind) => kind switch
    {
        NeighborhoodMapMarkerKinds.Pickup => AColor.Rgb(245, 124, 0),
        NeighborhoodMapMarkerKinds.Dropoff => AColor.Rgb(37, 99, 235),
        NeighborhoodMapMarkerKinds.Driver or NeighborhoodMapMarkerKinds.PublicData => AColor.Rgb(126, 34, 206),
        NeighborhoodMapMarkerKinds.Region => AColor.Rgb(16, 185, 129),
        _ => AColor.Rgb(107, 114, 128)
    };
}
#endif
