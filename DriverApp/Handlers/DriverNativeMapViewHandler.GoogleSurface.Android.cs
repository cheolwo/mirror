#if ANDROID
using Android.OS;
using Android.Views;
using DriverApp.Controls;
using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Common.Drivers;
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
using AndroidColor = Android.Graphics.Color;

namespace DriverApp.Handlers;

public partial class DriverNativeMapViewHandler
{
    private sealed class GoogleMapSurface(DriverNativeMapViewHandler owner)
    {
        public GoogleMapView? View { get; private set; }
        public GoogleMap? Map { get; private set; }
        private GoogleMarkerClickListener? _googleMarkerClickListener;
        private readonly List<GoogleMarker> _googleMarkers = [];
        private readonly List<GooglePolyline> _googleRouteOverlays = [];
        private readonly Dictionary<string, DriverMapMarkerItem> _googleMarkerItems = new(StringComparer.Ordinal);
        private GoogleMarker? _googleCurrentLocationMarker;
        private GoogleBitmapDescriptor? _googleInactiveMarkerIcon;

        public void EnsureCreated()
        {
            if (View is not null || owner._container is null)
            {
                return;
            }

            var context = owner.MauiContext?.Context ?? throw new InvalidOperationException("Android context is not available.");
            View = new GoogleMapView(context)
            {
                LayoutParameters = new ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.MatchParent)
            };
            View.OnCreate((Bundle?)null);
            View.OnStart();
            View.OnResume();
            View.GetMapAsync(new GoogleMapReadyCallback(this, owner._mapLifecycleGeneration));
            owner._container.AddView(View);
        }

        private void OnGoogleMapReady(GoogleMap googleMap, long generation)
        {
            if (owner._container is null || generation != owner._mapLifecycleGeneration) return;
            Map = googleMap;
            _googleMarkerClickListener = new GoogleMarkerClickListener(this);
            Map.SetOnMarkerClickListener(_googleMarkerClickListener);
            ApplyGoogleMapOptions();
            ApplyGoogleCamera();
            ApplyGoogleMarkers();
            ApplyGoogleRouteOverlays();
            owner.RefreshLocationPresentation(force: true);
            owner.StartLocationFreshnessTimer();
        }

        public void ApplyGoogleMapOptions()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            Map.TrafficEnabled = owner.VirtualView.ShowTrafficLayer;
            Map.SetMinZoomPreference((float)owner.VirtualView.MinZoom);
            Map.SetMaxZoomPreference((float)owner.VirtualView.MaxZoom);
            Map.UiSettings.CompassEnabled = true;
            Map.UiSettings.ZoomControlsEnabled = true;
            Map.UiSettings.MyLocationButtonEnabled = false;
        }

        public void ApplyGoogleCamera()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            var target = new GoogleLatLng(owner.VirtualView.CenterLatitude, owner.VirtualView.CenterLongitude);
            Map.MoveCamera(GoogleCameraUpdateFactory.NewLatLngZoom(target, (float)owner.VirtualView.Zoom));
        }

        public void ApplyGoogleMarkers()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            ClearGoogleMarkers();
            foreach (var pin in owner.RecommendationMarkerPresentations())
            {
                AddGoogleMarker(pin);
            }
        }

        public void ApplyGoogleRouteOverlays()
        {
            if (Map is null || owner.VirtualView is null)
            {
                return;
            }

            ClearGoogleRouteOverlays();
            foreach (var item in DriverNativeLocationPolicy.RoutesForDisplay(owner.VirtualView.RouteOverlays, owner.VirtualView.CurrentLocation, DateTime.UtcNow))
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
                var polyline = Map.AddPolyline(options);
                if (polyline is not null)
                {
                    _googleRouteOverlays.Add(polyline);
                }
            }
        }

        public void ApplyGoogleLocationOverlay()
        {
            ClearGoogleCurrentLocationMarker();
            var point = owner._locationPresentation?.Point;
            if (Map is null || owner.VirtualView is null || !owner.VirtualView.ShowCurrentLocationOverlay || point is null)
            {
                return;
            }

            var options = new GoogleMarkerOptions()
                .SetPosition(new GoogleLatLng((double)point.Latitude, (double)point.Longitude))
                .SetTitle("최근 확인 위치")
                .SetSnippet(owner._locationPresentation!.Notice)
                .SetIcon(GoogleBitmapDescriptorFactory.DefaultMarker(210f));
            _googleCurrentLocationMarker = Map.AddMarker(options);
        }

        private void AddGoogleMarker(DriverRecommendationMarkerPresentation pin)
        {
            if (Map is null)
            {
                return;
            }

            var options = new GoogleMarkerOptions()
                .SetPosition(new GoogleLatLng(pin.Latitude, pin.Longitude))
                .SetTitle(pin.Caption)
                .SetSnippet(pin.SubCaption)
                .SetIcon(pin.IsMuted ? GetGoogleInactiveMarkerIcon() : GoogleBitmapDescriptorFactory.DefaultMarker(pin.IsPickup ? 30f : 210f));
            options.InvokeZIndex(pin.ZIndex);
            var marker = Map.AddMarker(options);
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
            var density = Math.Max(1f, owner.MauiContext?.Context?.Resources?.DisplayMetrics?.Density ?? 1f);
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
            if (owner.VirtualView is null || string.IsNullOrWhiteSpace(marker.Id) || !_googleMarkerItems.TryGetValue(marker.Id, out var item))
            {
                return false;
            }

            owner.VirtualView.SendMarkerSelected(item);
            return false;
        }

        public void ClearGoogleMarkers()
        {
            foreach (var marker in _googleMarkers)
            {
                marker.Remove();
                marker.Dispose();
            }
            _googleMarkers.Clear();
            _googleMarkerItems.Clear();
        }

        public void ClearGoogleRouteOverlays()
        {
            foreach (var overlay in _googleRouteOverlays)
            {
                overlay.Remove();
                overlay.Dispose();
            }
            _googleRouteOverlays.Clear();
        }

        public void ClearGoogleCurrentLocationMarker()
        {
            _googleCurrentLocationMarker?.Remove();
            _googleCurrentLocationMarker?.Dispose();
            _googleCurrentLocationMarker = null;
        }

        public void DisposeInactiveMarkerIcon()
        {
            _googleInactiveMarkerIcon?.Dispose();
            _googleInactiveMarkerIcon = null;
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

        public void DisposeMarkerClickListener()
        {
            _googleMarkerClickListener?.Dispose();
            _googleMarkerClickListener = null;
        }

        private sealed class GoogleMapReadyCallback(GoogleMapSurface surface, long generation)
            : Java.Lang.Object, Android.Gms.Maps.IOnMapReadyCallback
        {
            public void OnMapReady(GoogleMap googleMap) => surface.OnGoogleMapReady(googleMap, generation);
        }

        private sealed class GoogleMarkerClickListener(GoogleMapSurface surface)
            : Java.Lang.Object, GoogleMap.IOnMarkerClickListener
        {
            public bool OnMarkerClick(GoogleMarker marker) => surface.OnGoogleMarkerClicked(marker);
        }
    }
}
#endif
