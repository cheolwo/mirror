using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Work;
using DriverApp.Models.Driver;

namespace DriverApp.Models.Driver.Samples;

public static class DriverNativeLocationSources
{
    public const string Sample = "sample";
    public const string ServerLocationReceived = "server-location-received";
    public const string Unknown = "unknown";
}

public sealed record DriverNativeLocationPoint(decimal Latitude, decimal Longitude);

public sealed record DriverNativeLocationPresentation(
    string StateCode,
    DriverNativeLocationPoint? Point,
    DateTime? ObservedAtUtc,
    string SourceCode,
    string Notice)
{
    public bool IsAvailable => Point is not null;
}

/// <summary>서버가 실제 위치를 받은 시각만 사용한다. 지도 중심과 복귀지는 현재 위치 근거가 아니다.</summary>
public static class DriverNativeLocationPolicy
{
    public static readonly TimeSpan MaximumLocationAge = TimeSpan.FromMinutes(10);
    public static readonly DriverNativeLocationPoint DefaultCameraCenter = new(37.5665m, 126.9780m);

    public static 기사현재위치샘플 FromServer(기사운행상태응답? status, 기사현재근무응답? work)
    {
        var observedAt = NormalizeUtc(status?.최근위치수신시각);
        var hasEvidence = CoordinatesValid(status?.현재위도, status?.현재경도) && observedAt.HasValue;
        return new 기사현재위치샘플(
            hasEvidence ? "최근 확인 위치" : "위치 미확인",
            status?.현재위도 ?? 0m,
            status?.현재경도 ?? 0m,
            observedAt ?? DateTime.MinValue)
        {
            SourceCode = hasEvidence ? DriverNativeLocationSources.ServerLocationReceived : DriverNativeLocationSources.Unknown,
            관측시각Utc = observedAt,
            복귀지위도 = work?.오늘의복귀지위도,
            복귀지경도 = work?.오늘의복귀지경도,
            복귀지명 = work?.오늘의복귀지주소 ?? work?.복귀지
        };
    }

    public static DriverNativeLocationPresentation Present(기사현재위치샘플? location, DateTime nowUtc)
    {
        if (location?.SourceCode == DriverNativeLocationSources.Sample)
            return new("sample", null, null, DriverNativeLocationSources.Sample, "예시 위치 · 실제 위치 미확인");

        var observedAt = NormalizeUtc(location?.관측시각Utc);
        var currentTime = NormalizeUtc(nowUtc);
        if (location is null || location.SourceCode != DriverNativeLocationSources.ServerLocationReceived
            || !CoordinatesValid(location.위도, location.경도) || !observedAt.HasValue
            || !currentTime.HasValue || observedAt.Value > currentTime.Value)
        {
            var notice = CoordinatesValid(location?.복귀지위도, location?.복귀지경도)
                ? "위치 미확인 · 복귀지 주변 지도"
                : "위치 미확인 · 새로고침으로 다시 확인해 주세요.";
            return new("unknown", null, observedAt, location?.SourceCode ?? DriverNativeLocationSources.Unknown, notice);
        }

        var receivedTime = new DateTimeOffset(observedAt.Value).ToOffset(TimeSpan.FromHours(9));
        if (currentTime.Value - observedAt.Value > MaximumLocationAge)
            return new("stale", null, observedAt, location.SourceCode,
                $"위치 확인 지연 · 마지막 수신 {receivedTime:HH:mm:ss} (한국시간)");

        return new("available", new(location.위도, location.경도), observedAt, location.SourceCode,
            $"최근 확인 위치 · 서버 수신 {receivedTime:HH:mm:ss} (한국시간)");
    }

    public static DriverNativeLocationPoint CameraCenter(기사현재위치샘플? location, DateTime nowUtc)
        => Present(location, nowUtc).Point
            ?? (CoordinatesValid(location?.복귀지위도, location?.복귀지경도)
                ? new(location!.복귀지위도!.Value, location.복귀지경도!.Value)
                : DefaultCameraCenter);

    public static 기사현재위치샘플 ForCurrentRoute(기사현재위치샘플 location, DateTime nowUtc)
        => Present(location, nowUtc).IsAvailable
            ? location with { 위치명 = "최근 확인 위치" }
            : location with { 위치명 = "위치 미확인", 위도 = 0m, 경도 = 0m };

    public static IReadOnlyList<DriverMapRouteOverlay> RoutesForDisplay(
        IReadOnlyList<DriverMapRouteOverlay> routes, 기사현재위치샘플? location, DateTime nowUtc)
    {
        var available = Present(location, nowUtc).IsAvailable;
        return routes.Select(route => route with
            {
                Points = route.Points
                    .Where(point => available || !IsObservedLocationPoint(point))
                    .Select(point => IsObservedLocationPoint(point) ? point with { Label = "최근 확인 위치" } : point)
                    .ToArray()
            })
            .Where(route => route.Points.Count >= 2)
            .ToArray();
    }

    public static bool CoordinatesValid(decimal? latitude, decimal? longitude)
        => latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m
            && latitude != 0m && longitude != 0m;

    public static IReadOnlyList<추천의뢰표시항목> RecommendationsWithDistance(
        IEnumerable<DriverRequestItem> requests,
        기사현재위치샘플? location,
        DateTime nowUtc,
        Func<decimal, decimal, decimal, decimal, decimal> calculateDistance)
    {
        var point = Present(location, nowUtc).Point;
        return requests.Select(request => new
            {
                Request = request,
                Distance = point is not null && CoordinatesValid(request.픽업_위도, request.픽업_경도)
                    ? (decimal?)calculateDistance(point.Latitude, point.Longitude, request.픽업_위도!.Value, request.픽업_경도!.Value)
                    : null
            })
            .OrderBy(item => !item.Distance.HasValue)
            .ThenBy(item => item.Distance)
            .Select((item, index) => new 추천의뢰표시항목(item.Request, item.Distance, item.Distance.HasValue ? index + 1 : null))
            .ToArray();
    }

    public static IReadOnlyList<추천의뢰표시항목> SelectRecommendations(
        IEnumerable<추천의뢰표시항목> recommendations,
        기사현재위치샘플? location,
        DateTime nowUtc,
        decimal? radiusKm,
        bool sortByIncome)
    {
        var available = Present(location, nowUtc).IsAvailable;
        var items = available ? recommendations : recommendations.Select(item => item with
        {
            상차지까지거리Km = null,
            가까운순위 = null
        });
        if (available && radiusKm is { } radius)
            items = items.Where(item => item.상차지까지거리Km is { } distance && distance <= radius);

        return (sortByIncome
                ? items.OrderByDescending(item => item.의뢰.예상수익 ?? 0m)
                    .ThenBy(item => !item.상차지까지거리Km.HasValue)
                    .ThenBy(item => item.상차지까지거리Km)
                : items.OrderBy(item => !item.상차지까지거리Km.HasValue)
                    .ThenBy(item => item.상차지까지거리Km))
            .ToArray();
    }

    private static bool IsObservedLocationPoint(DriverMapRoutePoint point)
        => point.Label is "현재 위치" or "최근 확인 위치";

    private static DateTime? NormalizeUtc(DateTime? value)
        => value is null || value == DateTime.MinValue ? null
            : value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime()
            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}
