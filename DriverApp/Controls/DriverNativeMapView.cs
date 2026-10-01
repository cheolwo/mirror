using Hongdal.Contracts.Common.Drivers;
using Hongdal.Contracts.Common.Operations;

namespace DriverApp.Controls;

public sealed class DriverNativeMapView : View
{
    public static readonly BindableProperty MapProviderCodeProperty = BindableProperty.Create(
        nameof(MapProviderCode),
        typeof(string),
        typeof(DriverNativeMapView),
        OperatingMapProviderCodes.NaverMaps);

    public static readonly BindableProperty CenterLatitudeProperty = BindableProperty.Create(
        nameof(CenterLatitude),
        typeof(double),
        typeof(DriverNativeMapView),
        37.5665d);

    public static readonly BindableProperty CenterLongitudeProperty = BindableProperty.Create(
        nameof(CenterLongitude),
        typeof(double),
        typeof(DriverNativeMapView),
        126.9780d);

    public static readonly BindableProperty ZoomProperty = BindableProperty.Create(
        nameof(Zoom),
        typeof(double),
        typeof(DriverNativeMapView),
        11d);

    public static readonly BindableProperty MarkersProperty = BindableProperty.Create(
        nameof(Markers),
        typeof(IReadOnlyList<DriverMapMarkerItem>),
        typeof(DriverNativeMapView),
        Array.Empty<DriverMapMarkerItem>());

    public static readonly BindableProperty RouteOverlaysProperty = BindableProperty.Create(
        nameof(RouteOverlays),
        typeof(IReadOnlyList<DriverMapRouteOverlay>),
        typeof(DriverNativeMapView),
        Array.Empty<DriverMapRouteOverlay>());

    public static readonly BindableProperty ShowTrafficLayerProperty = BindableProperty.Create(
        nameof(ShowTrafficLayer),
        typeof(bool),
        typeof(DriverNativeMapView),
        true);

    public static readonly BindableProperty ShowLocationButtonProperty = BindableProperty.Create(
        nameof(ShowLocationButton),
        typeof(bool),
        typeof(DriverNativeMapView),
        true);

    public static readonly BindableProperty ShowCurrentLocationOverlayProperty = BindableProperty.Create(
        nameof(ShowCurrentLocationOverlay),
        typeof(bool),
        typeof(DriverNativeMapView),
        true);

    public static readonly BindableProperty MinZoomProperty = BindableProperty.Create(
        nameof(MinZoom),
        typeof(double),
        typeof(DriverNativeMapView),
        6d);

    public static readonly BindableProperty MaxZoomProperty = BindableProperty.Create(
        nameof(MaxZoom),
        typeof(double),
        typeof(DriverNativeMapView),
        18d);

    public double CenterLatitude
    {
        get => (double)GetValue(CenterLatitudeProperty);
        set => SetValue(CenterLatitudeProperty, value);
    }

    public string MapProviderCode
    {
        get => (string)GetValue(MapProviderCodeProperty);
        set => SetValue(MapProviderCodeProperty, value);
    }

    public double CenterLongitude
    {
        get => (double)GetValue(CenterLongitudeProperty);
        set => SetValue(CenterLongitudeProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public IReadOnlyList<DriverMapMarkerItem> Markers
    {
        get => (IReadOnlyList<DriverMapMarkerItem>)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    public IReadOnlyList<DriverMapRouteOverlay> RouteOverlays
    {
        get => (IReadOnlyList<DriverMapRouteOverlay>)GetValue(RouteOverlaysProperty);
        set => SetValue(RouteOverlaysProperty, value);
    }

    public bool ShowTrafficLayer
    {
        get => (bool)GetValue(ShowTrafficLayerProperty);
        set => SetValue(ShowTrafficLayerProperty, value);
    }

    public bool ShowLocationButton
    {
        get => (bool)GetValue(ShowLocationButtonProperty);
        set => SetValue(ShowLocationButtonProperty, value);
    }

    public bool ShowCurrentLocationOverlay
    {
        get => (bool)GetValue(ShowCurrentLocationOverlayProperty);
        set => SetValue(ShowCurrentLocationOverlayProperty, value);
    }

    public double MinZoom
    {
        get => (double)GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => (double)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public event EventHandler<DriverMapMarkerItem>? MarkerSelected;

    public void SendMarkerSelected(DriverMapMarkerItem marker)
    {
        MarkerSelected?.Invoke(this, marker);
    }
}
