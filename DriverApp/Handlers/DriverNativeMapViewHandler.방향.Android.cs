#if ANDROID
using Android.Hardware;
using DriverApp.Controls;
using DriverApp.Models.Driver.Samples;
using Microsoft.Maui.Devices.Sensors;

namespace DriverApp.Handlers;

public partial class DriverNativeMapViewHandler
{
    private readonly 기사위치방향State _directionState = new();
    private EventHandler<CompassChangedEventArgs>? _compassHandler;
    private bool _ownsDirectionCompass;
    private bool _directionStartFailed;
    private long _directionGeneration;

    public static void MapDirection(DriverNativeMapViewHandler handler, DriverNativeMapView view)
        => handler.RefreshDirectionPresentation();

    private bool CanTrackDirection(DateTime nowUtc)
        => _container is not null && _naverMap is not null && !UsesGoogleMaps
            && VirtualView is { IsDirectionTrackingActive: true, ShowDirectionArrow: true, ShowCurrentLocationOverlay: true }
            && DriverNativeLocationPolicy.Present(VirtualView.CurrentLocation, nowUtc).IsAvailable;

    private void RefreshDirectionPresentation()
    {
        if (!CanTrackDirection(DateTime.UtcNow))
        {
            StopDirectionCompass();
            _directionStartFailed = false;
            return;
        }

        StartDirectionCompass();
        ApplyDirectionArrow();
    }

    private void StartDirectionCompass()
    {
        if (_compassHandler is not null || _directionStartFailed) return;
        try
        {
            var compass = Compass.Default;
            if (!compass.IsSupported) { _directionStartFailed = true; return; }
            var generation = ++_directionGeneration;
            var mapGeneration = _mapLifecycleGeneration;
            _compassHandler = (_, args) =>
            {
                var magneticHeading = args.Reading.HeadingMagneticNorth;
                var observedAtUtc = DateTime.UtcNow;
                var dispatcher = VirtualView?.Dispatcher;
                if (dispatcher is null) return;
                dispatcher.Dispatch(() =>
                {
                    if (generation != _directionGeneration || mapGeneration != _mapLifecycleGeneration
                        || _compassHandler is null || !CanTrackDirection(DateTime.UtcNow)) return;
                    try
                    {
                        var point = DriverNativeLocationPolicy.Present(VirtualView.CurrentLocation, DateTime.UtcNow).Point!;
                        using var field = new GeomagneticField((float)point.Latitude, (float)point.Longitude,
                            0f, new DateTimeOffset(observedAtUtc).ToUnixTimeMilliseconds());
                        _directionState.Observe(magneticHeading, field.Declination, observedAtUtc);
                        ApplyDirectionArrow();
                    }
                    catch (Exception)
                    {
                        StopDirectionCompass();
                        _directionStartFailed = true;
                    }
                });
            };
            compass.ReadingChanged += _compassHandler;
            // 다른 화면이 시작한 센서는 소유하지 않으며 종료할 때도 중지하지 않습니다.
            if (!compass.IsMonitoring)
            {
                compass.Start(SensorSpeed.UI);
                _ownsDirectionCompass = true;
            }
        }
        catch (Exception)
        {
            StopDirectionCompass();
            _directionStartFailed = true;
        }
    }

    private void ApplyDirectionArrow()
    {
        if (_naverMap is null) return;
        var overlay = _naverMap.LocationOverlay;
        var bearing = CanTrackDirection(DateTime.UtcNow) ? _directionState.Bearing(DateTime.UtcNow) : null;
        if (bearing is not { } heading)
        {
            overlay.SubIcon = null;
            return;
        }

        overlay.Bearing = (float)heading;
        overlay.SubIcon = Com.Naver.Maps.Map.Overlay.LocationOverlay.DefaultSubIconArrow;
    }

    private void StopDirectionCompass()
    {
        ++_directionGeneration;
        var handler = _compassHandler;
        _compassHandler = null;
        var owned = _ownsDirectionCompass;
        _ownsDirectionCompass = false;
        _directionState.Clear();
        if (_naverMap is not null) _naverMap.LocationOverlay.SubIcon = null;
        if (handler is not null)
        {
            try { Compass.Default.ReadingChanged -= handler; }
            catch (Exception) { }
        }
        if (owned)
        {
            try { Compass.Default.Stop(); }
            catch (Exception) { }
        }
    }
}
#endif
