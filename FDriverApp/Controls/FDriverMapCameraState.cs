namespace FDriverApp.Controls;

/// <summary>SDK 카메라의 사용자 탐색과 앱 위치 따라가기를 분리합니다.</summary>
public sealed class FDriverMapCameraState
{
    private FDriverMapCameraUpdate? _camera;
    private double _lastRequestedZoom = double.NaN;
    private int _recenterRequestVersion;
    private string? _framedRouteKey;

    public FDriverMapCameraUpdate? CurrentCamera => _camera;

    /// <summary>현재 배달·단계의 첫 유효 경로만 맞추고 이후 GPS·형상 갱신은 사용자 화면을 유지합니다.</summary>
    public FDriverMapRouteFrame? ResolveRouteFrame(string? frameKey,
        IReadOnlyList<(double Latitude, double Longitude)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (string.IsNullOrWhiteSpace(frameKey) || string.Equals(_framedRouteKey, frameKey, StringComparison.Ordinal))
            return null;

        var valid = points.Where(point => IsValidCoordinate(point.Latitude, point.Longitude)).Distinct().ToArray();
        // Invalid or coincident points cannot consume the request: a later real
        // road response for the same selected delivery must still be frameable.
        if (valid.Length < 2) return null;
        var bounds = new FDriverMapRouteFrame(valid.Min(point => point.Latitude), valid.Min(point => point.Longitude),
            valid.Max(point => point.Latitude), valid.Max(point => point.Longitude));
        _framedRouteKey = frameKey;
        return bounds;
    }

    public FDriverMapCameraUpdate? ResolveUpdate(
        double centerLatitude,
        double centerLongitude,
        bool hasCurrentLocation,
        double currentLatitude,
        double currentLongitude,
        bool isFollowingCurrentLocation,
        double requestedZoom,
        int recenterRequestVersion)
    {
        var recenterRequested = recenterRequestVersion != _recenterRequestVersion;
        _recenterRequestVersion = recenterRequestVersion;
        var validLocation = hasCurrentLocation && IsValidCoordinate(currentLatitude, currentLongitude);
        var resumeFollowing = recenterRequested && validLocation;
        var follow = isFollowingCurrentLocation || resumeFollowing;
        var zoom = double.IsFinite(requestedZoom) && requestedZoom > 0d
            ? requestedZoom
            : _camera?.Zoom ?? 13d;

        var latitude = _camera?.Latitude ?? centerLatitude;
        var longitude = _camera?.Longitude ?? centerLongitude;
        if (follow && validLocation)
        {
            latitude = currentLatitude;
            longitude = currentLongitude;
        }

        if (!IsValidCoordinate(latitude, longitude))
        {
            return null;
        }

        var targetChanged = _camera is null || _camera.Latitude != latitude || _camera.Longitude != longitude;
        var zoomChanged = _lastRequestedZoom != zoom;
        _lastRequestedZoom = zoom;
        if (!targetChanged && !zoomChanged && !resumeFollowing)
        {
            return null;
        }

        _camera = new(latitude, longitude, zoom, resumeFollowing);
        return _camera;
    }

    public void ObserveNativeCamera(double latitude, double longitude, double zoom)
    {
        if (!IsValidCoordinate(latitude, longitude) || !double.IsFinite(zoom) || zoom <= 0d)
        {
            return;
        }

        _camera = new(latitude, longitude, zoom, false);
        _lastRequestedZoom = zoom;
    }

    public static bool IsValidCoordinate(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude)
           && latitude is >= -90d and <= 90d
           && longitude is >= -180d and <= 180d
           && (latitude != 0d || longitude != 0d);
}

public sealed record FDriverMapCameraUpdate(double Latitude, double Longitude, double Zoom, bool ResumeFollowing);
public sealed record FDriverMapRouteFrame(double SouthLatitude, double WestLongitude, double NorthLatitude, double EastLongitude);
