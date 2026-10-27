using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.AI;

internal sealed record FoodDispatchTimingPoolSnapshot(운송원장[] Regional, 운송원장[] Pending);

/// <summary>이미 관측한 주문과 수요의 통계만 구성하며 조회·시각 취득·AI 호출은 하지 않습니다.</summary>
internal static class FoodDispatchTimingSnapshotBuilder
{
    internal static FoodDispatchTimingPoolSnapshot BuildPool(운송원장[] rows,
        IReadOnlySet<string> keys, DateTimeOffset now)
    {
        var regional = rows.Where(x => Coordinate(x.픽업_위도, x.픽업_경도)
                && keys.Contains(음식배달권정책.판정(new(x.픽업_위도!.Value, x.픽업_경도!.Value), null).배달권키))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.원본의뢰Id) ? x.의뢰Id : x.원본의뢰Id, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(y => y.Id).First()).ToArray();
        var pending = regional.Where(x => x.상태 == 상태값.배차대기상태.대기
            && x.배차큐단계 is 상태값.배차큐단계.계획배차 or 상태값.배차큐단계.배차추천
            && string.IsNullOrWhiteSpace(x.현재추천대상기사Id)
            && string.IsNullOrWhiteSpace(x.확정기사Id) && string.IsNullOrWhiteSpace(x.기사_운송자)
            && !배차실행인덱스재구성정책.유효한추천중잠금인가(x, now.UtcDateTime)).ToArray();
        return new(regional, pending);
    }

    internal static FoodDispatchTimingInput WithStatistics(FoodDispatchTimingInput input,
        운송원장 queue, FoodDispatchTimingPoolSnapshot pool, 음식배달수요참여결과? demand,
        DateTimeOffset observedAt, DateTimeOffset now, DateTime from,
        FoodDispatchTimingSettings timingSettings, int observationSeconds)
    {
        var regional = pool.Regional;
        var pending = pool.Pending;
        var current = Current(demand, observedAt);
        var compatible = pending.Count(x => Compatible(queue, x));
        var arrivals = regional.Count(x => x.의뢰Id != queue.의뢰Id && x.CreatedAt >= from
            && x.CreatedAt <= now.UtcDateTime);
        return input with
        {
            NowUtc = observedAt, SnapshotAtUtc = current && demand!.판정시각Utc < now ? demand.판정시각Utc : current ? now : null,
            DemandKnown = current,
            DriverCapacityKnown = current && demand!.기존기사가용처리건수.HasValue,
            AvailableDrivers = current ? demand!.기사판정목록.Count(x => x.신규제안허용) : 0,
            WaitingOrders = pending.Length, CompatibleOrders = compatible,
            PromisingPlanAvailable = compatible >= timingSettings.MinimumCompatibleOrders,
            ExpectedNewOrders = arrivals * (decimal)timingSettings.CollectionSeconds / observationSeconds,
            DelayRisk = pending.Any(x => x.CreatedAt < now.UtcDateTime.AddMinutes(-10))
        };
    }

    private static bool Current(음식배달수요참여결과? value, DateTimeOffset now)
        => value is { 데이터상태Code: 음식배달수요자료상태Code.최신, 예상업무량건수: >= 0m,
            기존기사가용처리건수: >= 0m, 보호활동기사수: >= 0, 추가참여인원: >= 0 }
            && value.정책판본 == 음식배달수요참여Policy.현재정책판본 && value.판정시각Utc <= now
            && now - value.판정시각Utc <= TimeSpan.FromMinutes(5) && value.기사판정목록 is not null
            && value.기사판정목록.All(x => x is not null && !string.IsNullOrWhiteSpace(x.기사Id) && x.우선순위 > 0)
            && value.기사판정목록.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() == value.기사판정목록.Count
            && value.기사판정목록.Select(x => x.우선순위).Distinct().Count() == value.기사판정목록.Count;

    internal static bool Coordinate(decimal? latitude, decimal? longitude)
        => latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m;

    // 동일·인접 셀의 가까운 두 지점은 조합 검토 기회일 뿐 시간·적재 약속 통과 근거가 아닙니다.
    private static bool Compatible(운송원장 source, 운송원장 candidate)
        => Coordinate(source.하차_위도, source.하차_경도) && Coordinate(candidate.하차_위도, candidate.하차_경도)
            && Distance(source.픽업_위도!.Value, source.픽업_경도!.Value, candidate.픽업_위도!.Value, candidate.픽업_경도!.Value) <= 1m
            && Distance(source.하차_위도!.Value, source.하차_경도!.Value, candidate.하차_위도!.Value, candidate.하차_경도!.Value) <= 2m;

    private static decimal Distance(decimal latitudeA, decimal longitudeA, decimal latitudeB, decimal longitudeB)
    {
        const double radians = Math.PI / 180d;
        var latitudeDelta = (double)(latitudeB - latitudeA) * radians;
        var longitudeDelta = (double)(longitudeB - longitudeA) * radians;
        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) + Math.Cos((double)latitudeA * radians)
            * Math.Cos((double)latitudeB * radians) * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return (decimal)(6371d * 2d * Math.Asin(Math.Sqrt(Math.Clamp(a, 0d, 1d))));
    }
}
