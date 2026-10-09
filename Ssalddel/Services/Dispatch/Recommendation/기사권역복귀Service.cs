using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Driver.Settings;

namespace 살뜰.Services.Dispatch.Recommendation;

public interface I기사권역복귀Service
{
    Task<기사권역복귀상태> 상태조회Async(string driverId, 배차경로좌표? 현재좌표,
        DateTime? 위치시각Utc, decimal? 정확도M = null, CancellationToken cancellationToken = default);
    Task<기사권역복귀평가> 평가Async(기사권역복귀상태 state, 배차경로좌표? baselineEnd,
        배차경로좌표? candidateEnd, bool fullRouteVerified, bool eligible,
        string? routeOption = null, CancellationToken cancellationToken = default);
}

// 기준점은 서버 내부 평가용이다. 고객/매장 응답이나 추천 감사 로그에 직렬화하지 않는다.
public sealed record 기사권역복귀상태(bool 정책선택됨, bool 활성여부, decimal? 기준점직선거리Km,
    배차경로좌표? 기준점, long 설정Revision, string 사유)
{
    public string? DriverId { get; init; }
}

public sealed record 기사권역복귀평가(decimal 가점, bool 확인됨, decimal? 기존복귀거리Km,
    decimal? 후보복귀거리Km, string 사유)
{
    public static 기사권역복귀평가 보류(string reason) => new(0m, false, null, null, reason);
}

public sealed class 기사권역복귀정책Options
{
    public decimal 최대가점 { get; set; } = 20m;
    public decimal 목표개선거리Km { get; set; } = 3m;
    public int 위치유효시간초 { get; set; } = 300;
    public decimal 최대위치오차M { get; set; } = 100m;
}

public sealed class 기사권역복귀Service(
    I기사권역복귀설정Store settings,
    I배차추천경로Service routes,
    IOptions<기사권역복귀정책Options> options,
    TimeProvider timeProvider) : I기사권역복귀Service
{
    public async Task<기사권역복귀상태> 상태조회Async(string driverId, 배차경로좌표? 현재좌표,
        DateTime? 위치시각Utc, decimal? 정확도M = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        // 조회 실패를 OFF로 바꾸지 않는다. 저장소 오류는 기존 재시도 경계로 전달한다.
        var snapshot = await settings.조회Async(driverId, cancellationToken);
        return await 상태계산Async(driverId, snapshot, 현재좌표, 위치시각Utc, 정확도M, true, cancellationToken);
    }

    private async Task<기사권역복귀상태> 상태계산Async(string driverId, 기사권역복귀설정Snapshot snapshot,
        배차경로좌표? position, DateTime? recordedUtc, decimal? accuracyM, bool retry,
        CancellationToken cancellationToken)
    {
        var config = snapshot.설정;
        if (config.Mode == "off") return new(false, false, null, null, config.Revision, "권역 복귀 우대 꺼짐");
        if (config.Mode is not ("automatic" or "on") || !좌표유효(position)
            || !config.기준위도.HasValue || !config.기준경도.HasValue)
            return 미확인(config.Revision, "권역 복귀 기준 위치 또는 현재 위치를 확인해 주세요.");
        var anchor = new 배차경로좌표(config.기준위도.Value, config.기준경도.Value);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!좌표유효(anchor) || !recordedUtc.HasValue
            || recordedUtc.Value > now.AddSeconds(30)
            || recordedUtc.Value < now.AddSeconds(-options.Value.위치유효시간초)
            || accuracyM is null or < 0 || accuracyM > options.Value.최대위치오차M
            || config.해제반경Km < 0m || config.진입반경Km <= config.해제반경Km)
            return 미확인(config.Revision, "신선한 위치와 유효한 복귀 반경을 확인할 수 없습니다.");

        // 늦게 도착한 GPS로 이전 활성 상태를 되돌리지 않는다.
        if (snapshot.위치판정시각Utc.HasValue && recordedUtc.Value < snapshot.위치판정시각Utc.Value)
            return 미확인(config.Revision, "최신 위치를 기다리고 있습니다.");
        var distance = routes.CalculateDistanceKm(position!, anchor);
        if (!distance.HasValue || distance < 0m)
            return 미확인(config.Revision, "기준 위치까지의 반경을 계산할 수 없습니다.");
        var errorKm = accuracyM!.Value / 1000m;
        var active = config.Mode == "on" || (snapshot.활성여부
            ? distance.Value + errorKm > config.해제반경Km
            : distance.Value - errorKm >= config.진입반경Km);
        var saved = await settings.활성상태갱신Async(driverId, config.Revision, active, recordedUtc.Value, cancellationToken);
        if (!saved)
        {
            var latest = await settings.조회Async(driverId, cancellationToken);
            if (retry) return await 상태계산Async(driverId, latest, position, recordedUtc, accuracyM, false, cancellationToken);
            return 미확인(latest.설정.Revision, "복귀 설정 또는 위치가 변경돼 다시 확인합니다.");
        }
        return new(true, active, Math.Round(distance.Value, 3), anchor, config.Revision,
            active ? "권역 복귀 우대 적용 중" : "설정한 활동 반경 안에 있습니다.") { DriverId = driverId };
    }

    public async Task<기사권역복귀평가> 평가Async(기사권역복귀상태 state,
        배차경로좌표? baselineEnd, 배차경로좌표? candidateEnd,
        bool fullRouteVerified, bool eligible, string? routeOption = null,
        CancellationToken cancellationToken = default)
    {
        if (!state.정책선택됨 || !state.활성여부) return 기사권역복귀평가.보류(state.사유);
        if (string.IsNullOrWhiteSpace(state.DriverId) || !eligible || !fullRouteVerified || !좌표유효(baselineEnd) || !좌표유효(candidateEnd)
            || !좌표유효(state.기준점))
            return 기사권역복귀평가.보류("전체 수행 경로와 시간·적재 조건 확인 후 복귀 우대를 계산합니다.");

        // 같은 차량 경로 옵션으로 두 끝점을 비교한다. 공차/시간 비용은 원 추천 항목이 소유한다.
        var before = await 복귀경로Async(baselineEnd!, state.기준점!, routeOption, cancellationToken);
        var after = await 복귀경로Async(candidateEnd!, state.기준점!, routeOption, cancellationToken);
        if (!실경로유효(before) || !실경로유효(after)
            || (routeOption is not null && (before!.RouteOption != routeOption || after!.RouteOption != routeOption)))
            return 기사권역복귀평가.보류("도로 복귀 경로를 확인할 수 없어 복귀 가점을 보류합니다.");
        // 도로 응답을 기다리는 동안 바뀐 설정이나 해제된 GPS 상태로 예전 가점을 확정하지 않는다.
        var latest = await settings.조회Async(state.DriverId!, cancellationToken);
        var checkedAt = timeProvider.GetUtcNow().UtcDateTime;
        if (latest.설정.Revision != state.설정Revision || latest.설정.Mode is not ("automatic" or "on")
            || latest.설정.기준위도 != state.기준점!.Latitude || latest.설정.기준경도 != state.기준점.Longitude
            || !latest.활성여부 || latest.위치판정시각Utc is not { } lastPosition
            || lastPosition < checkedAt.AddSeconds(-options.Value.위치유효시간초) || lastPosition > checkedAt.AddSeconds(30))
            return 기사권역복귀평가.보류("복귀 설정 또는 현재 위치가 변경돼 가점을 다시 확인합니다.");
        var improvement = before!.DistanceKm!.Value - after!.DistanceKm!.Value;
        var score = Math.Round(options.Value.최대가점
            * Math.Clamp(improvement / options.Value.목표개선거리Km, 0m, 1m), 2);
        var beforeKm = Math.Round(before.DistanceKm.Value, 3);
        var afterKm = Math.Round(after.DistanceKm.Value, 3);
        return new(score, true, beforeKm, afterKm, score > 0m
            ? $"권역 복귀 방향 · 전체 수행 후 기준점까지 {beforeKm:0.###}km → {afterKm:0.###}km"
            : "기존 전체 수행 계획보다 복귀 거리가 줄지 않습니다.");
    }

    private Task<배차경로예상결과?> 복귀경로Async(배차경로좌표 endpoint, 배차경로좌표 anchor,
        string? option, CancellationToken cancellationToken)
        => endpoint == anchor
            ? Task.FromResult<배차경로예상결과?>(new 배차경로예상결과(0m, TimeSpan.Zero, 0m, "동일좌표", true) { RouteOption = option })
            : option is null ? routes.EstimateRouteAsync(endpoint, anchor, cancellationToken)
            : routes.EstimateRouteAsync(endpoint, anchor, option, cancellationToken);

    private static bool 실경로유효(배차경로예상결과? route)
        => route is { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
            && route.Duration.Value >= TimeSpan.Zero;
    private static bool 좌표유효(배차경로좌표? point)
        => point is { Latitude: >= -90m and <= 90m, Longitude: >= -180m and <= 180m };
    private static 기사권역복귀상태 미확인(long revision, string reason)
        => new(true, false, null, null, revision, reason);
}
