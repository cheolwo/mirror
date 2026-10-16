using 살뜰.도메인.화물;
using Microsoft.EntityFrameworkCore;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Queue
{
    public sealed partial class 용달운송배차업무정책
    {
        private async Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 원거리지원후보병합Async(
            IReadOnlyList<국내화물운송기사상태Snapshot> geoCandidates,
            배차경로좌표 pickupPoint,
            화주운송의뢰 request,
            CancellationToken cancellationToken)
        {
            var result = geoCandidates
                .GroupBy(x => x.DriverId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            var activeCandidates = await _국내화물운송기사상태Store.활성기사조회Async(
                Math.Clamp(_options.원거리지원후보최대조회수, 1, 500),
                cancellationToken);
            var activeIds = activeCandidates.Select(x => x.DriverId).Distinct(StringComparer.Ordinal).ToArray();
            var performing = _options.화물수행종료기준사용
                ? (await _db.운송원장.AsNoTracking().Where(x => x.배차큐단계 == 상태값.배차큐단계.확정
                    && x.상태 != 상태값.배차상태.인수완료
                    && (Enumerable.Contains(activeIds, x.확정기사Id!) || Enumerable.Contains(activeIds, x.기사_운송자)))
                    .Select(x => x.확정기사Id ?? x.기사_운송자).Distinct().ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            foreach (var state in activeCandidates)
            {
                // GPS 반경 밖이어도 현재 업무의 끝점이 새 픽업 근처일 수 있다. 제한된 active-index만 합친다.
                if (performing.Contains(state.DriverId)) { result.TryAdd(state.DriverId, state); continue; }
                if (result.ContainsKey(state.DriverId)
                    || !state.상차접근허용반경Km.HasValue
                    || !state.Latitude.HasValue
                    || !state.Longitude.HasValue)
                {
                    continue;
                }

                var distanceKm = _routeService.CalculateDistanceKm(
                    new 배차경로좌표(state.Latitude.Value, state.Longitude.Value),
                    pickupPoint);
                if (!distanceKm.HasValue || !상차접근허용(state, distanceKm.Value, request))
                {
                    continue;
                }

                result[state.DriverId] = state;
            }

            return result.Values.ToList();
        }

        private bool 상차접근허용(국내화물운송기사상태Snapshot state, decimal distanceKm, 화주운송의뢰 request)
        {
            var baseRadius = Math.Max(1m, _options.기사후보검색반경Km);
            if (distanceKm <= baseRadius)
            {
                return true;
            }

            var driverAllowedRadius = state.상차접근허용반경Km ?? baseRadius;
            var maxRadius = Math.Max(baseRadius, _options.원거리상차접근최대반경Km);
            var allowedRadius = Math.Min(driverAllowedRadius, maxRadius);
            if (distanceKm > allowedRadius)
            {
                return false;
            }

            return 상차시간창도착가능(distanceKm, request);
        }

        private bool 상차시간창도착가능(decimal distanceKm, 화주운송의뢰 request)
        {
            var pickupWindowEnd = request.픽업_시간창_종료일시;
            if (pickupWindowEnd <= DateTime.UtcNow)
            {
                return false;
            }

            var speed = Math.Max(1m, _options.원거리상차평균속도KmH);
            var estimatedMinutes = distanceKm / speed * 60m;
            var remainingMinutes = (decimal)(pickupWindowEnd - DateTime.UtcNow).TotalMinutes;
            return remainingMinutes >= estimatedMinutes + Math.Max(0m, _options.원거리상차도착여유분);
        }
    }
}
