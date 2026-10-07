using FDriverApp.Controls;
using Microsoft.Maui.Handlers;

namespace FDriverApp.Handlers;

public partial class FDriverNativeMapViewHandler
{
    public static readonly IPropertyMapper<FDriverNativeMapView, FDriverNativeMapViewHandler> Mapper =
        new PropertyMapper<FDriverNativeMapView, FDriverNativeMapViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(FDriverNativeMapView.CenterLatitude)] = MapCamera,
            [nameof(FDriverNativeMapView.CenterLongitude)] = MapCamera,
            [nameof(FDriverNativeMapView.CurrentLocationLatitude)] = MapLocation,
            [nameof(FDriverNativeMapView.CurrentLocationLongitude)] = MapLocation,
            [nameof(FDriverNativeMapView.HasCurrentLocation)] = MapLocation,
            [nameof(FDriverNativeMapView.IsFollowingCurrentLocation)] = MapCamera,
            [nameof(FDriverNativeMapView.RecenterRequestVersion)] = MapCamera,
            [nameof(FDriverNativeMapView.Zoom)] = MapCamera,
            [nameof(FDriverNativeMapView.Markers)] = MapMarkers,
            [nameof(FDriverNativeMapView.SelectedRequestId)] = MapMarkers,
            [nameof(FDriverNativeMapView.RouteOverlays)] = MapRouteOverlays,
            [nameof(FDriverNativeMapView.ShowTrafficLayer)] = MapOptions,
            [nameof(FDriverNativeMapView.ShowLocationButton)] = MapOptions,
            [nameof(FDriverNativeMapView.ShowCurrentLocationOverlay)] = MapOptions,
            [nameof(FDriverNativeMapView.MinZoom)] = MapOptions,
            [nameof(FDriverNativeMapView.MaxZoom)] = MapOptions
        };

    public FDriverNativeMapViewHandler() : base(Mapper)
    {
    }

    public static void MapLocation(FDriverNativeMapViewHandler handler, FDriverNativeMapView view)
    {
        MapOptions(handler, view);
        MapCamera(handler, view);
    }
}
