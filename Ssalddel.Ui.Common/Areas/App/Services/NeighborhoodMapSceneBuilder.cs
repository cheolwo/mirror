using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>검증된 공개 대표점과 현재 권한 조회를 지도 어댑터용 장면으로만 조립합니다.</summary>
internal static class NeighborhoodMapSceneBuilder
{
    public static NeighborhoodMapRenderState Build(long revision,
        IReadOnlyList<NeighborhoodPublicRegionDto> regions, NeighborhoodExchangeMapResponse map,
        IReadOnlyList<RegionalAgriculturalMapMarkerDto> publicData,
        IReadOnlyList<NeighborhoodStorageMapMarkerDto> storage,
        IReadOnlyList<NeighborhoodMapDeliverySummary> deliveries, NeighborhoodDeliveryMapResponse? delivery,
        Func<string, bool> hasLayer, bool privateAuthorized, bool recentDriver,
        string? selectedRequestId, string? selectedMarkerId, NeighborhoodPublicRegionDto? selectedRegion,
        RegionalAgriculturalMapMarkerDto? selectedPublicData, NeighborhoodMapViewport? rememberedViewport, bool preserveViewport)
    {
        var markers = regions.Select(region =>
        {
            var count = map.Items.Where(item => item.Region.RegionKey == region.RegionKey).Sum(item =>
                (hasLayer(NeighborhoodMapNavigation.Offer) ? item.OfferCount : 0)
                + (hasLayer(NeighborhoodMapNavigation.Need) ? item.NeedCount : 0));
            if (hasLayer(NeighborhoodMapNavigation.Storage))
                count += storage.Where(item => item.Region.RegionKey == region.RegionKey).Sum(item => item.SpaceCount);
            return new NeighborhoodMapMarker("region:" + region.RegionKey, region.DisplayName, NeighborhoodMapMarkerKinds.Region,
                region.Latitude, region.Longitude, count);
        }).Where(item => item.Count > 0).ToList();
        var routes = new List<NeighborhoodMapRoute>();
        if (hasLayer(NeighborhoodMapNavigation.PublicData))
            markers.AddRange(publicData.Select(item => new NeighborhoodMapMarker("public-data:" + item.MarkerKey,
                item.DisplayNameKo, NeighborhoodMapMarkerKinds.PublicData, (double)item.Latitude, (double)item.Longitude, item.ObservationCount)));
        if (privateAuthorized && hasLayer(NeighborhoodMapNavigation.Mine))
        {
            foreach (var summary in deliveries.Where(item => item.RequestId != selectedRequestId))
            {
                AddInactive(summary, summary.Pickup, "pickup");
                AddInactive(summary, summary.Dropoff, "dropoff");
            }
            if (delivery is { } value)
            {
                AddPoint(value, value.Pickup, NeighborhoodMapMarkerKinds.Pickup);
                AddPoint(value, value.Dropoff, NeighborhoodMapMarkerKinds.Dropoff);
                if (recentDriver && value.DriverLocation is { } driver)
                    markers.Add(new("delivery:" + value.RequestId + ":driver", "기사 최근 위치", NeighborhoodMapMarkerKinds.Driver,
                        (double)driver.Latitude, (double)driver.Longitude));
                if (value.PlannedRoute is { } route && route.Points.Count >= 2 && route.Points.All(point => ValidPoint((double)point.Latitude, (double)point.Longitude)))
                    routes.Add(new(value.RequestId, route.Points.Select(point => new NeighborhoodMapPoint((double)point.Latitude, (double)point.Longitude)).ToArray(),
                        route.StageCode == "Pickup" ? "#f97316" : route.StageCode == "Delivery" ? "#2563eb" : "#6b7280", route.Notice, route.SourceCode));
            }
        }
        var viewport = preserveViewport && rememberedViewport is not null ? rememberedViewport
            : delivery is { } selected && privateAuthorized ? DeliveryViewport(selected, recentDriver)
            : selectedPublicData is { } selectedPublic ? new NeighborhoodMapViewport((double)selectedPublic.Latitude, (double)selectedPublic.Longitude, 7)
            : selectedRequestId is null && selectedRegion is { } region ? new NeighborhoodMapViewport(region.Latitude, region.Longitude, 13) : rememberedViewport;
        return new(revision, markers, routes, selectedMarkerId, viewport, preserveViewport);

        void AddInactive(NeighborhoodMapDeliverySummary summary, NeighborhoodMapPoint? point, string role)
        {
            if (point is not null && ValidPoint(point.Latitude, point.Longitude))
                markers.Add(new("delivery:" + summary.RequestId + ":" + role, "다른 진행 배송", NeighborhoodMapMarkerKinds.Inactive, point.Latitude, point.Longitude));
        }
        void AddPoint(NeighborhoodDeliveryMapResponse value, NeighborhoodDeliveryMapPoint? point, string kind)
        {
            if (point is not null && ValidPoint((double)point.Latitude, (double)point.Longitude))
                markers.Add(new("delivery:" + value.RequestId + ":" + kind, kind == NeighborhoodMapMarkerKinds.Pickup ? "픽업지" : "전달지", kind,
                    (double)point.Latitude, (double)point.Longitude));
        }
    }

    private static NeighborhoodMapViewport? DeliveryViewport(NeighborhoodDeliveryMapResponse value, bool recentDriver)
    {
        var points = new List<NeighborhoodMapPoint>();
        foreach (var point in new[] { value.Pickup, value.Dropoff })
            if (point is not null && ValidPoint((double)point.Latitude, (double)point.Longitude)) points.Add(new((double)point.Latitude, (double)point.Longitude));
        if (value.PlannedRoute is { } route)
            points.AddRange(route.Points.Where(point => ValidPoint((double)point.Latitude, (double)point.Longitude)).Select(point => new NeighborhoodMapPoint((double)point.Latitude, (double)point.Longitude)));
        if (recentDriver && value.DriverLocation is { } driver) points.Add(new((double)driver.Latitude, (double)driver.Longitude));
        if (points.Count == 0) return null;
        var latitude = (points.Min(point => point.Latitude) + points.Max(point => point.Latitude)) / 2;
        var longitude = (points.Min(point => point.Longitude) + points.Max(point => point.Longitude)) / 2;
        var span = Math.Max(points.Max(point => point.Longitude) - points.Min(point => point.Longitude),
            (points.Max(point => point.Latitude) - points.Min(point => point.Latitude)) / Math.Max(0.1, Math.Cos(latitude * Math.PI / 180)));
        return new(latitude, longitude, Math.Clamp(Math.Log2(360 / Math.Max(0.004, span)) - 1, 3, 16));
    }

    private static bool ValidPoint(double latitude, double longitude) => double.IsFinite(latitude) && double.IsFinite(longitude)
        && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
}
