using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.배차;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Coordination;

public sealed partial class 국내화물배차조율입력Factory
{
    private async Task<List<운송원장>> LoadQueuesAsync(
        국내화물배차조율입력요청 request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // 배차대기는 업무 상태의 DB 원장이다. 서버 메모리 큐가 사라져도
        // 이 조건으로 미처리 운송 의뢰를 다시 읽어 실행 큐를 재구성한다.
        var query = _db.운송원장
            .AsNoTracking()
            .미처리운송의뢰쿼리(now);

        if (request.의뢰Ids is { Count: > 0 })
        {
            var ids = request.의뢰Ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
            query = query.Where(x => Enumerable.Contains(ids, x.의뢰Id));
        }

        return await query
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Max(1, request.최대운송의뢰수))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<string, 화주운송의뢰>> LoadRequestMapAsync(IEnumerable<string> requestIds, CancellationToken cancellationToken)
    {
        var ids = requestIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, 화주운송의뢰>(StringComparer.Ordinal);
        }

        return await _db.화주운송의뢰
            .AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);
    }

    private async Task<Dictionary<string, 화물요구조건>> LoadCargoMapAsync(IEnumerable<string> requestIds, CancellationToken cancellationToken)
    {
        var ids = requestIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, 화물요구조건>(StringComparer.Ordinal);
        }

        return await _db.화물요구조건
            .AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);
    }

    private async Task<IReadOnlyList<국내화물운송기사상태Snapshot>> LoadDriverStatesAsync(
        국내화물배차조율입력요청 request,
        CancellationToken cancellationToken)
    {
        // 기사 상태 Store는 배차 판단을 빠르게 하기 위한 실행 인덱스다.
        // 업무 확정은 배차대기, 화주운송의뢰, 운송원장 DB 상태로만 판단한다.
        if (request.기사Ids is { Count: > 0 })
        {
            var result = new List<국내화물운송기사상태Snapshot>();
            foreach (var driverId in request.기사Ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
            {
                var state = await _기사상태Store.GetAsync(driverId, cancellationToken);
                if (state is not null)
                {
                    result.Add(state);
                }
            }

            return result;
        }

        return await _기사상태Store.활성기사조회Async(Math.Max(1, request.최대기사수), cancellationToken);
    }

    private async Task<Dictionary<string, 용달기사>> LoadDriverMapAsync(
        IReadOnlyList<국내화물운송기사상태Snapshot> states,
        국내화물배차조율입력요청 request,
        CancellationToken cancellationToken)
    {
        var ids = states
            .Select(x => x.DriverId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, 용달기사>(StringComparer.Ordinal);
        }

        return await _db.용달기사
            .AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.기사Id) && x.상태 == "활동중")
            .Take(Math.Max(1, request.최대기사수))
            .ToDictionaryAsync(x => x.기사Id, StringComparer.Ordinal, cancellationToken);
    }

    private async Task<Dictionary<string, 차량제원>> LoadVehicleSpecMapAsync(
        IEnumerable<용달기사> drivers,
        CancellationToken cancellationToken)
    {
        var vehicles = drivers
            .Select(x => x.차량)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (vehicles.Length == 0)
        {
            return new Dictionary<string, 차량제원>(StringComparer.Ordinal);
        }

        var specs = await _db.차량제원
            .AsNoTracking()
            .Where(x => Enumerable.Contains(vehicles, x.차량코드) || Enumerable.Contains(vehicles, x.차량명))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, 차량제원>(StringComparer.Ordinal);
        foreach (var spec in specs)
        {
            if (!string.IsNullOrWhiteSpace(spec.차량코드))
            {
                result.TryAdd(spec.차량코드, spec);
            }

            if (!string.IsNullOrWhiteSpace(spec.차량명))
            {
                result.TryAdd(spec.차량명, spec);
            }
        }

        return result;
    }

    private async Task<Dictionary<string, int>> LoadAcceptedTransportCountsAsync(
        IEnumerable<string> driverIds,
        CancellationToken cancellationToken)
    {
        var ids = driverIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return await _db.운송원장
            .AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.기사_운송자) && x.상태 != "인수완료")
            .GroupBy(x => x.기사_운송자)
            .Select(x => new { 기사Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.기사Id, x => x.Count, StringComparer.Ordinal, cancellationToken);
    }

    private async Task<Dictionary<string, 기사근무>> LoadCurrentShiftMapAsync(
        IEnumerable<string> driverIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var ids = driverIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, 기사근무>(StringComparer.Ordinal);
        }

        var shifts = await _db.기사근무
            .AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.기사Id))
            .Where(x => !x.시작시각.HasValue || x.시작시각 <= now)
            .OrderByDescending(x => x.시작시각 ?? x.CreatedAt)
            .ToListAsync(cancellationToken);

        return shifts
            .GroupBy(x => x.기사Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
    }
}
