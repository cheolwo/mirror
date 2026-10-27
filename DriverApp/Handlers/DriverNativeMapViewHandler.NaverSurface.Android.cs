#if ANDROID
using Android.OS;
using Android.Views;
using DriverApp.Models.Driver.Samples;
using NaverLatLng = Com.Naver.Maps.Geometry.LatLng;
using NaverMap = Com.Naver.Maps.Map.NaverMap;
using NaverMapView = Com.Naver.Maps.Map.MapView;
using NaverMarker = Com.Naver.Maps.Map.Overlay.Marker;
using NaverMarkerIcons = Com.Naver.Maps.Map.Util.MarkerIcons;
using NaverPathOverlay = Com.Naver.Maps.Map.Overlay.PathOverlay;
using AndroidColor = Android.Graphics.Color;

namespace DriverApp.Handlers;

public partial class DriverNativeMapViewHandler
{
    private sealed class NaverMapSurface(DriverNativeMapViewHandler owner)
    {
        public NaverMapView? View { get; private set; }
        public NaverMap? Map { get; private set; }
        private readonly List<NaverMarker> _naverMarkers = [];
        private readonly List<NaverPathOverlay> _naverRouteOverlays = [];

        public void EnsureCreated()
        {
            if (View is not null || owner._container is null)
            {
                return;
            }

            var context = owner.MauiContext?.Context ?? throw new InvalidOperationException("Android context is not available.");
            View = new NaverMapView(context)
            {
                LayoutParameters = new ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.MatchParent)
            };
            View.OnCreate((Bundle?)null);
            View.OnStart();
            View.OnResume();
            View.GetMapAsync(new NaverMapReadyCallback(this, owner._mapLifecycleGeneration));
            owner._container.AddView(View);
        }

        private void OnNaverMapReady(NaverMap naverMap, long generation)
        {
            if (owner._container is null || generation != owner._mapLifecycleGeneration) return;
            Map = naverMap;
            Map.Locale = Java.Util.Locale.ForLanguageTag("ko-KR");
            ApplyNaverMapOptions();
            ApplyNaverCamera();
            ApplyNaverMarkers();
            ApplyNaverRouteOverlays();
            owner.RefreshLocationPresentation(force: true);
            owner.StartLocationFreshnessTimer();
        }

        public void ApplyNaverMapOptions()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            Map.MinZoom = owner.VirtualView.MinZoom;
            Map.MaxZoom = owner.VirtualView.MaxZoom;
            Map.LiteModeEnabled = false;
            Map.SetLayerGroupEnabled(NaverMap.LayerGroupTraffic, owner.VirtualView.ShowTrafficLayer);

            var uiSettings = Map.UiSettings;
            uiSettings.CompassEnabled = true;
            uiSettings.ScaleBarEnabled = true;
            uiSettings.ZoomControlEnabled = true;
            uiSettings.LocationButtonEnabled = owner.VirtualView.ShowLocationButton
                && DriverNativeLocationPolicy.Present(owner.VirtualView.CurrentLocation, DateTime.UtcNow).IsAvailable;
            uiSettings.SetLogoMargin(16, 16, 16, 120);

        }

        public void ApplyNaverCamera()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            var target = new NaverLatLng(owner.VirtualView.CenterLatitude, owner.VirtualView.CenterLongitude);
            var update = Com.Naver.Maps.Map.CameraUpdate.ScrollAndZoomTo(target, owner.VirtualView.Zoom);
            Map.MoveCamera(update);
        }

        public void ApplyNaverMarkers()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            ClearNaverMarkers();
            foreach (var pin in owner.RecommendationMarkerPresentations())
            {
                AddNaverMarker(pin);
            }
        }

        public void ApplyNaverRouteOverlays()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            ClearNaverRouteOverlays();
            foreach (var item in DriverNativeLocationPolicy.RoutesForDisplay(owner.VirtualView.RouteOverlays, owner.VirtualView.CurrentLocation, DateTime.UtcNow))
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
                    Map = Map
                };
                _naverRouteOverlays.Add(overlay);
            }
        }

        public void ApplyNaverLocationOverlay()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            var overlay = Map.LocationOverlay;
            var point = owner._locationPresentation?.Point;
            Map.LocationTrackingMode = Com.Naver.Maps.Map.LocationTrackingMode.None!;
            if (point is not null)
                overlay.Position = new NaverLatLng((double)point.Latitude, (double)point.Longitude);
            overlay.CircleColor = AndroidColor.Argb(40, 25, 118, 210);
            overlay.CircleOutlineColor = AndroidColor.Argb(120, 25, 118, 210);
            overlay.CircleOutlineWidth = 2;
            overlay.Visible = owner.VirtualView.ShowCurrentLocationOverlay && point is not null;
            owner.RefreshDirectionPresentation();
        }

        private void AddNaverMarker(DriverRecommendationMarkerPresentation pin)
        {
            if (Map is null || owner.VirtualView is null)
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
            marker.Click += (_, _) => owner.VirtualView.SendMarkerSelected(pin.Source);
            marker.Map = Map;
            _naverMarkers.Add(marker);
        }

        public void ClearNaverMarkers()
        {
            foreach (var marker in _naverMarkers)
            {
                marker.Map = null;
                marker.Dispose();
            }
            _naverMarkers.Clear();
        }

        public void ClearNaverRouteOverlays()
        {
            foreach (var overlay in _naverRouteOverlays)
            {
                overlay.Map = null;
                overlay.Dispose();
            }
            _naverRouteOverlays.Clear();
        }

        // 두 제공자의 overlay 해제가 모두 끝난 뒤 Handler가 호출합니다.
        public void DestroyView()
        {
            if (View is not null)
            {
                View.OnPause();
                View.OnStop();
                View.OnDestroy();
                View.Dispose();
            }
        }

        public void ClearMapReference() => Map = null;
        public void ClearViewReference() => View = null;

        private sealed class NaverMapReadyCallback(NaverMapSurface surface, long generation)
            : Java.Lang.Object, Com.Naver.Maps.Map.IOnMapReadyCallback
        {
            public void OnMapReady(NaverMap naverMap) => surface.OnNaverMapReady(naverMap, generation);
        }
    }
}
#endif
