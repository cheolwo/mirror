using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Common.Drivers;
using System.Globalization;

namespace DriverApp.Services;

public sealed class DriverHomeRoutePlanningService
{
    public IReadOnlyList<DriverMapRouteOverlay> BuildLinkedRouteOverlays(
        기사현재위치샘플 currentLocation,
        기사운송샘플항목? currentTransport,
        DriverMapMarkerItem recommendation,
        string strokeColor = "#16a34a",
        string caption = "연계 추천 경로")
    {
        var points = new List<DriverMapRoutePoint>();

        if (HasCoordinate(currentLocation.위도, currentLocation.경도))
        {
            points.Add(new DriverMapRoutePoint(
                (double)currentLocation.위도,
                (double)currentLocation.경도,
                "현재 위치"));
        }

        AddCurrentTransportNextPoint(points, currentTransport);
        AddDistinctPoint(points, recommendation.PickupLatitude, recommendation.PickupLongitude, "추천 상차지");

        if (recommendation.DropoffLatitude != 0d && recommendation.DropoffLongitude != 0d)
        {
            AddDistinctPoint(points, recommendation.DropoffLatitude, recommendation.DropoffLongitude, "추천 하차지");
        }

        return points.Count < 2
            ? []
            :
            [
                new DriverMapRouteOverlay(
                    recommendation.RequestId,
                    caption,
                    points,
                    strokeColor,
                    "#ecfdf5",
                    10)
            ];
    }

    public IReadOnlyList<DriverMapRouteOverlay> BuildAcceptedRouteOverlays(
        기사현재위치샘플 currentLocation,
        기사운송샘플항목? currentTransport,
        DriverMapMarkerItem recommendation)
    {
        if (recommendation.DropoffLatitude == 0d || recommendation.DropoffLongitude == 0d)
        {
            return [];
        }

        return BuildLinkedRouteOverlays(currentLocation, currentTransport, recommendation, "#2563eb", "수락 운송 경로");
    }

    public DriverLinkedRouteCardState BuildLinkedRouteCardState(
        기사현재위치샘플 currentLocation,
        기사운송샘플항목? currentTransport,
        DriverMapMarkerItem recommendation)
    {
        var emptyDistance = CalculateDistanceKm(currentTransport, recommendation);
        var summary = emptyDistance.HasValue
            ? $"{BuildCurrentLegLabel(currentTransport)} 이후 추천 상차지까지 약 {emptyDistance.Value.ToString("0.0", CultureInfo.CurrentCulture)}km 연계됩니다."
            : $"{currentLocation.위치명} 기준으로 추천 상차지까지 이어지는 후보 경로입니다.";
        var benefit = emptyDistance.HasValue && emptyDistance.Value <= 8d
            ? "공차 유리"
            : "연계 검토";
        var dropoff = recommendation.DropoffLatitude != 0d && recommendation.DropoffLongitude != 0d
            ? $"추천 하차지: {recommendation.Summary}"
            : "추천 하차지: 추천 상세에서 확인";

        return new DriverLinkedRouteCardState(
            summary,
            benefit,
            BuildCurrentRouteLabel(currentLocation, currentTransport),
            $"추천 상차지: {recommendation.PickupAddress}",
            dropoff);
    }

    private static double? CalculateDistanceKm(
        기사운송샘플항목? currentTransport,
        DriverMapMarkerItem recommendation)
    {
        var reference = ResolveCurrentTransportNextCoordinate(currentTransport);
        if (reference is null)
        {
            return null;
        }

        var lat1 = DegreesToRadians(reference.Value.Latitude);
        var lon1 = DegreesToRadians(reference.Value.Longitude);
        var lat2 = DegreesToRadians(recommendation.PickupLatitude);
        var lon2 = DegreesToRadians(recommendation.PickupLongitude);
        var deltaLat = lat2 - lat1;
        var deltaLon = lon2 - lon1;
        var a = Math.Pow(Math.Sin(deltaLat / 2d), 2d) +
            Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2d), 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));

        return 6371d * c;
    }

    private static void AddCurrentTransportNextPoint(
        List<DriverMapRoutePoint> points,
        기사운송샘플항목? currentTransport)
    {
        var next = ResolveCurrentTransportNextCoordinate(currentTransport);
        if (next is null)
        {
            return;
        }

        AddDistinctPoint(points, next.Value.Latitude, next.Value.Longitude, next.Value.Label);
    }

    private static (double Latitude, double Longitude, string Label)? ResolveCurrentTransportNextCoordinate(기사운송샘플항목? currentTransport)
    {
        if (currentTransport is null)
        {
            return null;
        }

        if (IsCurrentTransportBeforePickupComplete(currentTransport)
            && HasCoordinate(currentTransport.픽업위도, currentTransport.픽업경도))
        {
            return ((double)currentTransport.픽업위도!.Value, (double)currentTransport.픽업경도!.Value, "현재 운송 상차지");
        }

        if (HasCoordinate(currentTransport.하차위도, currentTransport.하차경도))
        {
            return ((double)currentTransport.하차위도!.Value, (double)currentTransport.하차경도!.Value, "현재 운송 하차지");
        }

        return null;
    }

    private static bool IsCurrentTransportBeforePickupComplete(기사운송샘플항목 currentTransport)
    {
        var stage = currentTransport.현재단계 ?? string.Empty;
        var nextAction = currentTransport.다음행동 ?? string.Empty;
        return nextAction.Contains("상차", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, "확정", StringComparison.Ordinal)
            || stage.Contains("상차 대기", StringComparison.OrdinalIgnoreCase)
            || stage.Contains("상차지 도착", StringComparison.OrdinalIgnoreCase)
            || stage.Contains("배차확정", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCurrentLegLabel(기사운송샘플항목? currentTransport)
    {
        if (currentTransport is null)
        {
            return "현재 위치";
        }

        return IsCurrentTransportBeforePickupComplete(currentTransport)
            ? "현재 운송 상차지"
            : "현재 운송 하차지";
    }

    private static string BuildCurrentRouteLabel(
        기사현재위치샘플 currentLocation,
        기사운송샘플항목? currentTransport)
    {
        if (currentTransport is null)
        {
            return $"현재 위치: {currentLocation.위치명}";
        }

        return IsCurrentTransportBeforePickupComplete(currentTransport)
            ? $"현재 이동: {currentLocation.위치명} → 상차지 {currentTransport.픽업지}"
            : $"현재 이동: {currentLocation.위치명} → 하차지 {currentTransport.하차지}";
    }

    private static void AddDistinctPoint(List<DriverMapRoutePoint> points, double latitude, double longitude, string label)
    {
        if (latitude == 0d || longitude == 0d)
        {
            return;
        }

        var last = points.LastOrDefault();
        if (last is not null
            && Math.Abs(last.Latitude - latitude) < 0.000001d
            && Math.Abs(last.Longitude - longitude) < 0.000001d)
        {
            return;
        }

        points.Add(new DriverMapRoutePoint(latitude, longitude, label));
    }

    private static bool HasCoordinate(decimal? latitude, decimal? longitude)
        => latitude is not null
            && longitude is not null
            && latitude != 0m
            && longitude != 0m;

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}

public sealed record DriverLinkedRouteCardState(
    string Summary,
    string Benefit,
    string CurrentRouteLabel,
    string RecommendationPickupRouteLabel,
    string RecommendationDropoffRouteLabel);
