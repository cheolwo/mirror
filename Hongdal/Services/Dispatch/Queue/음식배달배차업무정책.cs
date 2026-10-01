using Hongdal.Contracts.Common.Drivers;
using Microsoft.Extensions.Options;
using 홍달.Services.Dispatch.Recommendation;
using 홍달.Services.Storage.Local;
using 홍달.도메인.공통;
using 홍달.도메인.운송;

namespace 홍달.Services.Dispatch.Queue;

public sealed class 음식배달배차업무정책 : I배차업무정책
{
    private readonly I국내화물운송기사상태Store _기사상태Store;
    private readonly IDriverRejectedRequestStore _거절Store;
    private readonly I배차추천경로Service _경로Service;
    private readonly 배차큐정책Options _options;

    public 음식배달배차업무정책(
        I국내화물운송기사상태Store 기사상태Store,
        IDriverRejectedRequestStore 거절Store,
        I배차추천경로Service 경로Service,
        IOptions<배차큐정책Options> options)
    {
        _기사상태Store = 기사상태Store;
        _거절Store = 거절Store;
        _경로Service = 경로Service;
        _options = options.Value;
    }

    public int 배차업무유형 => 상태값.배차업무유형.음식배달;

    public async Task<배차추천후보?> 다음후보선정Async(
        운송원장 queue,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default)
    {
        if (!queue.픽업_위도.HasValue || !queue.픽업_경도.HasValue)
        {
            return null;
        }

        var pickup = new 배차경로좌표(queue.픽업_위도.Value, queue.픽업_경도.Value);
        var candidates = await _기사상태Store.위치반경조회Async(
            pickup.Latitude,
            pickup.Longitude,
            Math.Max(1m, _options.기사후보검색반경Km),
            Math.Max(1, _options.기사후보최대조회수),
            cancellationToken);
        if (candidates.Count == 0)
        {
            return null;
        }

        var rejectedDriverIds = await _거절Store.GetRejectedDriverIdsAsync(queue.의뢰Id, cancellationToken);
        var now = DateTime.UtcNow;
        배차추천후보? best = null;
        decimal bestDistance = decimal.MaxValue;

        foreach (var candidate in candidates)
        {
            if (!string.Equals(candidate.AppKey, 기사앱식별자.FoodDeliveryDriverApp, StringComparison.Ordinal)
                || !string.Equals(candidate.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase)
                || !candidate.Latitude.HasValue
                || !candidate.Longitude.HasValue
                || !기사위치신선도정책.유효한가(
                    candidate.위치수신시각Utc,
                    now,
                    _options.기사위치유효시간분)
                || rejectedDriverIds.Contains(candidate.DriverId)
                || (!string.IsNullOrWhiteSpace(제외기사Id)
                    && string.Equals(candidate.DriverId, 제외기사Id, StringComparison.Ordinal)))
            {
                continue;
            }

            var distance = _경로Service.CalculateDistanceKm(
                new 배차경로좌표(candidate.Latitude.Value, candidate.Longitude.Value),
                pickup);
            if (!distance.HasValue)
            {
                continue;
            }

            var distanceScore = Math.Max(0m, 100m - distance.Value * 12m);
            var score = distanceScore + candidate.Aging점수;
            var reason = $"음식점까지 {distance.Value:0.0}km · F드라이버 대기 보정 {candidate.Aging점수:0}";
            if (best is null
                || score > best.추천점수
                || (score == best.추천점수 && distance.Value < bestDistance))
            {
                best = new 배차추천후보(candidate.DriverId, score, reason);
                bestDistance = distance.Value;
            }
        }

        return best;
    }
}
