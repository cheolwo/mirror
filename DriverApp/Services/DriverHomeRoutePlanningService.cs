using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Transport;
using System.Globalization;

namespace DriverApp.Services;

public sealed class DriverHomeRoutePlanningService
{
    public DriverOrderedRouteMapState BuildWorkspaceRouteState(
        기사현재위치샘플 currentLocation, 기사운송샘플항목? currentTransport,
        기사화물운송작업공간응답? workspace, bool isCurrent)
    {
        if (!isCurrent)
            return new([], [], "현재 방문 순서를 다시 조회해 주세요. 이전 연결선은 표시하지 않습니다.", string.Empty);
        if (currentTransport is null)
            return new([], [], string.Empty, string.Empty);
        var activeTransports = workspace?.활성운송목록 ?? [];
        var heldTransport = activeTransports.FirstOrDefault(item => item.운송진행보류)
            ?? (workspace?.다음행동운송 is { 운송진행보류: true } nextHeld ? nextHeld : null);
        if (heldTransport is not null)
            return CurrentOnly(string.IsNullOrWhiteSpace(heldTransport.다음행동안내)
                ? "운송 진행이 보류되어 방문 순서를 다시 확인해 주세요." : heldTransport.다음행동안내, suppressCurrent: true);
        if (!string.IsNullOrWhiteSpace(workspace?.수락정차순서보류Code)
            || workspace?.BlockCodes?.Any(code => code?.StartsWith("FreightAcceptedStopOrder.", StringComparison.Ordinal) == true) == true)
            return CurrentOnly("저장된 방문 순서를 다시 확인해 주세요.", suppressCurrent: true);
        if (workspace is null || !workspace.수락정차순서반영됨)
            return CurrentOnly("저장된 방문 순서를 다시 확인해 주세요.");

        var transports = activeTransports.ToArray();
        var stops = (workspace.권장경로정차목록 ?? []).OrderBy(stop => stop.순서).ToArray();
        if (transports.Length == 0 || transports.Any(item => item.Id <= 0)
            || transports.GroupBy(item => item.Id).Any(group => group.Count() != 1)
            || stops.Length == 0 || stops.Any(stop => stop.운송Id is not > 0 || stop.순서 < 0
                || string.IsNullOrWhiteSpace(stop.의뢰Id))
            || stops.Select(stop => stop.순서).Distinct().Count() != stops.Length
            || stops.Select(stop => (stop.운송Id, stop.단계)).Distinct().Count() != stops.Length)
            return CurrentOnly("방문 순서의 연결 정보를 다시 확인해 주세요.", suppressCurrent: true);

        var byId = transports.ToDictionary(item => item.Id);
        if (stops.Any(stop => !byId.ContainsKey(stop.운송Id!.Value) || stop.단계 is not ("상차" or "하차"))
            || transports.Any(item => !RemainingStopsMatch(item, stops))
            || workspace.다음행동운송?.Id != stops[0].운송Id
            || currentTransport.Id != stops[0].운송Id
            || currentTransport.현재단계 == "인수완료"
            || IsCurrentTransportBeforePickupComplete(currentTransport) != (stops[0].단계 == "상차"))
            return CurrentOnly("현재 업무와 방문 순서가 달라 다시 확인해 주세요.", suppressCurrent: true);

        var markers = new List<DriverMapMarkerItem>();
        var routes = new List<DriverMapRouteOverlay>();
        var summary = new List<string>();
        var location = DriverNativeLocationPolicy.ForCurrentRoute(currentLocation, DateTime.UtcNow);
        DriverMapRoutePoint? previous = DriverNativeLocationPolicy.CoordinatesValid(location.위도, location.경도)
            ? new((double)location.위도, (double)location.경도, "최근 확인 위치") : null;
        var coordinateGap = false;
        for (var index = 0; index < stops.Length; index++)
        {
            var stop = stops[index];
            var transport = byId[stop.운송Id!.Value];
            var pickup = stop.단계 == "상차";
            var label = $"{index + 1} · {stop.단계}";
            var displayId = string.IsNullOrWhiteSpace(transport.운송번호) ? $"운송 {transport.Id}" : transport.운송번호;
            var deadline = stop.시간창근거있음 && stop.시간창종료일시.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(stop.시간창종료일시.Value, DateTimeKind.Utc))
                    .ToOffset(TimeSpan.FromHours(9)).ToString("MM.dd HH:mm", CultureInfo.InvariantCulture) + " (한국시간)"
                : "미확인";
            summary.Add($"{index + 1}. {displayId} {stop.단계} · 마감 {deadline}");
            if (transport.개인정보제공보류 || !stop.좌표근거있음
                || !DriverNativeLocationPolicy.CoordinatesValid(stop.위도, stop.경도))
            {
                previous = null;
                coordinateGap = true;
                continue;
            }
            var latitude = (double)stop.위도!.Value;
            var longitude = (double)stop.경도!.Value;
            markers.Add(new DriverMapMarkerItem(stop.의뢰Id,
                pickup ? latitude : 0d, pickup ? longitude : 0d,
                pickup ? 0d : latitude, pickup ? 0d : longitude,
                $"{displayId} {stop.단계}", index == 0 ? "다음 정차" : "이후 순서",
                pickup ? stop.주소 : string.Empty, pickup ? string.Empty : stop.주소,
                label, label) { StopOrder = index + 1, IsNextStop = index == 0 });
            var point = new DriverMapRoutePoint(latitude, longitude, label);
            if (previous is not null && (previous.Latitude != latitude || previous.Longitude != longitude))
                routes.Add(new($"accepted-stop:{index + 1}", "방문 지점 연결", [previous, point],
                    index == 0 ? pickup ? "#f97316" : "#2563eb" : "#9ca3af", "#ffffff", index == 0 ? 9 : 5));
            previous = point;
        }

        var notice = "수락한 방문 순서입니다.";
        if (coordinateGap) notice += " 일부 위치는 확인이 필요해 연결선을 끊었습니다.";
        // 회색 선을 먼저 그리고 다음 정차의 강조선을 마지막에 올려 겹친 구간에 묻히지 않게 합니다.
        return new(markers, routes.OrderBy(route => route.Width == 9 ? 1 : 0).ToArray(), notice, string.Join("\n", summary))
        { HasCoordinateGap = coordinateGap };

        DriverOrderedRouteMapState CurrentOnly(string notice, bool suppressCurrent = false)
        {
            if (suppressCurrent) return new([], [], notice, string.Empty);
            var matching = activeTransports.Where(item => item.Id == currentTransport.Id).ToArray();
            if (workspace is not null && (matching.Length != 1 || matching[0].개인정보제공보류))
                return new([], [], notice, string.Empty);
            var marker = CurrentMarker(currentTransport);
            var safeLocation = DriverNativeLocationPolicy.ForCurrentRoute(currentLocation, DateTime.UtcNow);
            return new(marker is null ? [] : [marker], BuildCurrentRouteOverlays(safeLocation, currentTransport),
                notice + " 현재 업무의 상하차 지점을 표시합니다.", string.Empty);
        }
    }

    private static bool RemainingStopsMatch(기사운송요약응답 transport, IReadOnlyList<기사화물경로정차응답> stops)
    {
        var remaining = stops.Where(stop => stop.운송Id == transport.Id).ToArray();
        return transport.상태 switch
        {
            "배차대기" or "배차확정" or "확정" or "매칭중" or "이동중" or "상차지도착"
                => remaining.Length == 2 && remaining[0].단계 == "상차" && remaining[1].단계 == "하차",
            "상차완료" or "운송중" or "하차지도착"
                => remaining.Length == 1 && remaining[0].단계 == "하차",
            _ => false
        };
    }

    /// <summary>픽업과 전달을 독립된 구간으로 구성합니다. 같은 좌표의 라벨 제거로 전달 구간이 사라지지 않습니다.</summary>
    public IReadOnlyList<DriverMapRouteOverlay> BuildRecommendationReviewRouteOverlays(
        기사현재위치샘플 currentLocation, 기사운송샘플항목? currentTransport, DriverMapMarkerItem recommendation)
    {
        // 현재 업무와 새 추천의 방문 순서는 서버의 전체 일정 검증이 소유합니다.
        // 이 표시용 좌표만으로 A 다음 B라는 순서를 만들지 않습니다. 핀은 호출자가 별도로 유지합니다.
        if (currentTransport is not null) return [];
        if (!ReviewCoordinateValid(recommendation.PickupLatitude, recommendation.PickupLongitude)) return [];
        var overlays = new List<DriverMapRouteOverlay>();
        var pickupPoints = new List<DriverMapRoutePoint>();
        if (ReviewCoordinateValid((double)currentLocation.위도, (double)currentLocation.경도))
            pickupPoints.Add(new((double)currentLocation.위도, (double)currentLocation.경도, "현재 위치"));
        var next = ResolveCurrentTransportNextCoordinate(currentTransport);
        if (next is { } point && ReviewCoordinateValid(point.Latitude, point.Longitude))
            AddDistinctPoint(pickupPoints, point.Latitude, point.Longitude, point.Label);
        AddDistinctPoint(pickupPoints, recommendation.PickupLatitude, recommendation.PickupLongitude, "추천 상차지");
        if (pickupPoints.Count >= 2)
            overlays.Add(new(recommendation.RequestId, "예상 픽업 경로", pickupPoints, "#f97316"));

        var deliveryPoints = new List<DriverMapRoutePoint>
        {
            new(recommendation.PickupLatitude, recommendation.PickupLongitude, "추천 상차지")
        };
        if (ReviewCoordinateValid(recommendation.DropoffLatitude, recommendation.DropoffLongitude))
            AddDistinctPoint(deliveryPoints, recommendation.DropoffLatitude, recommendation.DropoffLongitude, "추천 하차지");
        if (deliveryPoints.Count >= 2)
            overlays.Add(new(recommendation.RequestId, "예상 전달 경로", deliveryPoints, "#2563eb"));
        return overlays;
    }

    private static bool ReviewCoordinateValid(double latitude, double longitude)
        => double.IsFinite(latitude) && double.IsFinite(longitude)
            && latitude is >= -90 and <= 90 and not 0 && longitude is >= -180 and <= 180 and not 0;

    public IReadOnlyList<DriverMapRouteOverlay> BuildCurrentRouteOverlays(
        기사현재위치샘플 currentLocation, 기사운송샘플항목? currentTransport)
    {
        if (currentTransport is null) return [];
        var points = new List<DriverMapRoutePoint>();
        if (HasCoordinate(currentLocation.위도, currentLocation.경도))
            points.Add(new((double)currentLocation.위도, (double)currentLocation.경도, "현재 위치"));
        AddCurrentTransportNextPoint(points, currentTransport);
        return points.Count < 2 ? [] : [new(currentTransport.의뢰Id, "현재 수행 경로", points,
            IsCurrentTransportBeforePickupComplete(currentTransport) ? "#f97316" : "#2563eb")];
    }

    public DriverMapMarkerItem? CurrentMarker(기사운송샘플항목? transport)
    {
        if (transport is null) return null;
        var pickupKnown = HasCoordinate(transport.픽업위도, transport.픽업경도);
        var dropoffKnown = HasCoordinate(transport.하차위도, transport.하차경도);
        if (!pickupKnown && !dropoffKnown) return null;
        return new(transport.의뢰Id, pickupKnown ? (double)transport.픽업위도!.Value : 0d,
            pickupKnown ? (double)transport.픽업경도!.Value : 0d,
            dropoffKnown ? (double)transport.하차위도!.Value : 0d,
            dropoffKnown ? (double)transport.하차경도!.Value : 0d, transport.화물종류,
            transport.다음행동, transport.픽업지, transport.하차지, "현재 상차지", "현재 하차지");
    }

    public IReadOnlyList<DriverMapRouteOverlay> BuildLinkedRouteOverlays(
        기사현재위치샘플 currentLocation,
        기사운송샘플항목? currentTransport,
        DriverMapMarkerItem recommendation,
        string strokeColor = "#16a34a",
        string caption = "연계 추천 경로")
    {
        if (currentTransport is not null
            || !ReviewCoordinateValid(recommendation.PickupLatitude, recommendation.PickupLongitude)) return [];
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
        if (!ReviewCoordinateValid(recommendation.DropoffLatitude, recommendation.DropoffLongitude))
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
        var summary = currentTransport is not null
            ? "방문 순서는 기존 업무와 새 추천의 시간·적재 조건을 함께 검증한 결과를 확인해 주세요."
            : $"{currentLocation.위치명} 기준 추천 상하차 지점입니다.";
        var benefit = currentTransport is not null ? "방문 순서 확인" : "연계 검토";
        var dropoff = ReviewCoordinateValid(recommendation.DropoffLatitude, recommendation.DropoffLongitude)
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

        if (IsCurrentTransportBeforePickupComplete(currentTransport))
        {
            // 다음 상차 위치가 없으면 하차지로 건너뛰는 연결선을 만들지 않습니다.
            return HasCoordinate(currentTransport.픽업위도, currentTransport.픽업경도)
                ? ((double)currentTransport.픽업위도!.Value, (double)currentTransport.픽업경도!.Value, "현재 운송 상차지")
                : null;
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
        if (!ReviewCoordinateValid(latitude, longitude))
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
        => DriverNativeLocationPolicy.CoordinatesValid(latitude, longitude);

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}

public sealed record DriverLinkedRouteCardState(
    string Summary,
    string Benefit,
    string CurrentRouteLabel,
    string RecommendationPickupRouteLabel,
    string RecommendationDropoffRouteLabel);

public sealed record DriverOrderedRouteMapState(
    IReadOnlyList<DriverMapMarkerItem> Markers,
    IReadOnlyList<DriverMapRouteOverlay> RouteOverlays,
    string Notice,
    string StopSummary)
{
    public bool HasCoordinateGap { get; init; }
}
