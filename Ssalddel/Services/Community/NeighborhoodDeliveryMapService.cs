using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Data;
using 살뜰.Services.External.Naver;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Services.Community;

public interface I생활배송지도Service
{
    Task<NeighborhoodDeliveryMapResponse?> 내지도Async(string requestId, bool includeRoute = false, CancellationToken cancellationToken = default);
}

/// <summary>본인 생활 배송의 최소 지도 투영입니다. 경로 결과는 저장·캐시하지 않고 명시 조회에만 제공합니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalLogisticsOs, SsalddelCodeLayer.Application,
    "본인 생활 배송의 핀·도로 예정 경로·현재 배정의 최근 위치를 분리해 조회한다.",
    Effects = SsalddelCodeEffect.PersistentRead,
    StepKey = "application.neighborhood-delivery-map", FlowOrder = 40,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    Boundary = "공개 추천·교류 글에 개인 좌표를 투영하지 않는다. 자동차 참고 경로는 실제 이동 이력이 아니다.")]
public sealed class 생활배송지도Service(
    SsalddelContext db, ICurrentUserAccessor currentUser, I생활배송의뢰UseCase deliveries,
    INaverCloudDirectionsService directions, TimeProvider clock, ILogger<생활배송지도Service> logger) : I생활배송지도Service
{
    public async Task<NeighborhoodDeliveryMapResponse?> 내지도Async(string requestId, bool includeRoute = false, CancellationToken cancellationToken = default)
    {
        var actor = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(requestId)) return null;
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId
            && x.주문자UserId == actor && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), cancellationToken);
        // 같은 기존 본인 상세의 운송 진행 조회 권한을 통과해야 외부 경로 API도 호출할 수 있습니다.
        if (request is null || await deliveries.내상세Async(requestId, cancellationToken) is null) return null;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        var observedAt = clock.GetUtcNow().UtcDateTime;
        var recent = await NeighborhoodDeliveryLocationPolicy.ReadAsync(db, request, queue, observedAt, cancellationToken);
        var response = new NeighborhoodDeliveryMapResponse
        {
            RequestId = requestId, RequestStatusCode = request.상태,
            StageCode = Stage(request, queue), TransportStatusCode = queue?.상태 ?? string.Empty,
            Pickup = Point(request.픽업_위도, request.픽업_경도, "픽업지"),
            Dropoff = Point(request.하차_위도, request.하차_경도, "전달지"),
            RouteRevision = Revision(request, queue, recent.AssignedAtUtc)
        };
        PopulateLocation(response, recent);
        if (includeRoute) await PopulateRouteAsync(response, cancellationToken);

        // 외부 조회를 기다리는 사이의 기사 변경·종료도 최신 원장에서 다시 판정합니다.
        queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        var currentRequest = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId
            && x.주문자UserId == actor && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), cancellationToken);
        if (currentRequest is null || !string.Equals(actor, currentUser.UserId, StringComparison.Ordinal)) return null;
        response.ObservedAtUtc = clock.GetUtcNow().UtcDateTime;
        recent = await NeighborhoodDeliveryLocationPolicy.ReadAsync(db, currentRequest, queue, response.ObservedAtUtc, cancellationToken);
        var currentRevision = Revision(currentRequest, queue, recent.AssignedAtUtc);
        if (response.RouteRevision != currentRevision
            || (response.PlannedRoute?.FromPointRoleCode == "Driver" && recent.Location is null))
        {
            response.Pickup = Point(currentRequest.픽업_위도, currentRequest.픽업_경도, "픽업지");
            response.Dropoff = Point(currentRequest.하차_위도, currentRequest.하차_경도, "전달지");
            response.RouteRevision = currentRevision;
            response.PlannedRoute = null; response.RouteStateCode = NeighborhoodDeliveryMapStates.Unavailable;
            response.RouteNotice = "배송 위치나 진행 상태가 바뀌었습니다. 예정 경로를 다시 확인해 주세요.";
        }
        response.RequestStatusCode = currentRequest.상태;
        response.TransportStatusCode = queue?.상태 ?? string.Empty;
        response.StageCode = Stage(currentRequest, queue);
        PopulateLocation(response, recent);
        return response;
    }

    private static void PopulateLocation(NeighborhoodDeliveryMapResponse response, NeighborhoodDeliveryLocationResult recent)
    {
        response.DriverLocation = null;
        response.DriverLocationStateCode = recent.StateCode;
        response.DriverLocationNotice = recent.StateCode switch
        {
            NeighborhoodDeliveryMapStates.Available => "현재 배정 이후에 확인한 최근 기사 위치입니다.",
            NeighborhoodDeliveryMapStates.Closed => "배송이 종료되어 기사 위치를 표시하지 않습니다.",
            NeighborhoodDeliveryMapStates.NotAssigned => "기사가 확정되면 최근 위치를 확인할 수 있습니다.",
            NeighborhoodDeliveryMapStates.AssignmentEvidenceMissing => "현재 배정의 위치 제공 근거를 확인하지 못해 표시하지 않습니다.",
            _ => "최근 10분 이내의 기사 위치를 확인하지 못했습니다."
        };
        if (recent.Location is { } location)
            response.DriverLocation = new NeighborhoodDeliveryRecentDriverLocation
            {
                Latitude = location.위도, Longitude = location.경도, AccuracyMeters = location.정확도_m,
                MeasuredAtUtc = DateTime.SpecifyKind(location.기록시각, DateTimeKind.Utc),
                ReceivedAtUtc = DateTime.SpecifyKind(location.CreatedAt, DateTimeKind.Utc)
            };
    }

    private async Task PopulateRouteAsync(NeighborhoodDeliveryMapResponse response, CancellationToken cancellationToken)
    {
        response.RouteStateCode = NeighborhoodDeliveryMapStates.Unavailable;
        response.RouteNotice = "도로 예정 경로를 확인하지 못했습니다. 핀 위치만 표시합니다.";
        if (response.StageCode == NeighborhoodDeliveryMapStages.Closed)
        {
            response.RouteStateCode = NeighborhoodDeliveryMapStates.Closed;
            response.RouteNotice = "배송이 종료되어 진행 경로를 표시하지 않습니다.";
            return;
        }
        if (response.Pickup is not { } pickup || response.Dropoff is not { } dropoff) return;
        var from = pickup; var to = dropoff;
        var stage = NeighborhoodDeliveryMapStages.Overview; var fromRole = "Pickup"; var toRole = "Dropoff";
        if (response.DriverLocation is { } driver && response.StageCode is NeighborhoodDeliveryMapStages.Pickup or NeighborhoodDeliveryMapStages.Delivery)
        {
            from = new() { Latitude = driver.Latitude, Longitude = driver.Longitude, Label = "최근 기사 위치" };
            stage = response.StageCode; fromRole = "Driver";
            if (stage == NeighborhoodDeliveryMapStages.Pickup) { to = pickup; toRole = "Pickup"; }
        }
        try
        {
            var route = await directions.GetDrivingRouteAsync(from.Latitude, from.Longitude, to.Latitude, to.Longitude,
                cancellationToken: cancellationToken);
            if (route is null || route.Path.Count < 2
                || route.Path.Any(x => !NeighborhoodDeliveryLocationPolicy.CoordinatesValid(x.Latitude, x.Longitude))) return;
            response.PlannedRoute = new NeighborhoodDeliveryPlannedRoute
            {
                StageCode = stage, FromPointRoleCode = fromRole, ToPointRoleCode = toRole,
                Points = route.Path.Select(x => new NeighborhoodDeliveryMapPoint { Latitude = x.Latitude, Longitude = x.Longitude }).ToArray(),
                DistanceKm = route.DistanceKm is >= 0 ? route.DistanceKm : null,
                DurationMinutes = route.DurationMilliseconds is >= 0 ? route.DurationMilliseconds / 60000m : null,
                CalculatedAtUtc = clock.GetUtcNow().UtcDateTime
            };
            response.RouteStateCode = NeighborhoodDeliveryMapStates.Available;
            response.RouteNotice = response.PlannedRoute.Notice;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or ArgumentException)
        {
            // 외부 오류의 요청 URL·개인 좌표를 사용자 응답이나 로그에 복사하지 않습니다.
            logger.LogWarning("생활 배송 예정 경로 조회 실패. ErrorType={ErrorType}", ex.GetType().Name);
        }
    }

    private static NeighborhoodDeliveryMapPoint? Point(decimal? latitude, decimal? longitude, string label)
        => NeighborhoodDeliveryLocationPolicy.CoordinatesValid(latitude, longitude)
            ? new() { Latitude = latitude!.Value, Longitude = longitude!.Value, Label = label } : null;

    private static string Stage(화주운송의뢰 request, 운송원장? queue)
        => NeighborhoodDeliveryLocationPolicy.IsClosed(request, queue) ? NeighborhoodDeliveryMapStages.Closed
            : !NeighborhoodDeliveryLocationPolicy.HasActiveAssignment(request, queue) ? NeighborhoodDeliveryMapStages.Overview
            : queue!.상태 switch
            {
                "상차완료" or "운송중" or "하차지도착" => NeighborhoodDeliveryMapStages.Delivery,
                "배차확정" or "확정" or "매칭중" or "이동중" or "상차지도착" => NeighborhoodDeliveryMapStages.Pickup,
                _ => NeighborhoodDeliveryMapStages.Overview
            };

    private static string Revision(화주운송의뢰 request, 운송원장? queue, DateTime? assignedAtUtc)
    {
        var coordinates = new[] { request.픽업_위도, request.픽업_경도, request.하차_위도, request.하차_경도 };
        var source = string.Join("|", coordinates.Select(x => x?.ToString(CultureInfo.InvariantCulture) ?? "missing"))
            + $"|{Stage(request, queue)}|{queue?.확정기사Id}|{queue?.추천라운드}|{assignedAtUtc?.Ticks}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
}
