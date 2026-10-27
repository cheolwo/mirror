using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Services.Community;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Common.Transport;

internal sealed record 현재배정기사위치(long? 운송Id, string 상태Code, 기사위치기록? 위치);

/// <summary>호출자가 조회 권한을 검사한 운송 의뢰에만 사용합니다. 공개 기사 목록이나 전체 경로 조회가 아닙니다.</summary>
internal static class 현재배정기사위치조회
{
    private static readonly TimeSpan 유효기간 = TimeSpan.FromMinutes(10);

    internal static async Task<IReadOnlyDictionary<string, 현재배정기사위치>> 조회Async(
        SsalddelContext db, IReadOnlyCollection<string> allowedRequestIds, CancellationToken ct)
    {
        var ids = allowedRequestIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return new Dictionary<string, 현재배정기사위치>();
        var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id)).ToDictionaryAsync(x => x.의뢰Id, ct);
        var ledgers = await 원장Async(db, ids, ct);
        var driverIds = ledgers.Values.Select(x => x.확정기사Id).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToArray();
        var drivers = await db.용달기사.AsNoTracking().Where(x => Enumerable.Contains(driverIds, x.기사Id)).ToDictionaryAsync(x => x.기사Id, ct);
        var shifts = await 근무Async(db, driverIds, ct);
        var acceptances = await 수락Async(db, ids, ct);
        // 한 최신 행을 읽습니다. 최신 근거가 손상되었다고 과거 GPS로 되돌아가지 않습니다.
        var locations = await db.기사위치기록.AsNoTracking().Where(x => Enumerable.Contains(driverIds, x.기사Id))
            .GroupBy(x => x.기사Id).Select(g => g.OrderByDescending(x => x.기록시각).ThenByDescending(x => x.Id).First())
            .ToDictionaryAsync(x => x.기사Id, ct);
        var currentRequests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id)).ToDictionaryAsync(x => x.의뢰Id, ct);
        var currentLedgers = await 원장Async(db, ids, ct);
        var currentAcceptances = await 수락Async(db, ids, ct);
        var currentDrivers = await db.용달기사.AsNoTracking().Where(x => Enumerable.Contains(driverIds, x.기사Id)).ToDictionaryAsync(x => x.기사Id, ct);
        var currentShifts = await 근무Async(db, driverIds, ct);
        var now = DateTime.UtcNow;
        return ids.ToDictionary(id => id, id =>
        {
            requests.TryGetValue(id, out var source); currentRequests.TryGetValue(id, out var current);
            ledgers.TryGetValue(id, out var ledger); currentLedgers.TryGetValue(id, out var currentLedger);
            현재배정기사위치 Result(string code, 기사위치기록? point = null) => new(ledger?.Id, code, point);
            if ((source is not null && NeighborhoodDeliveryLocationPolicy.IsClosed(source, ledger))
                || (current is not null && NeighborhoodDeliveryLocationPolicy.IsClosed(current, currentLedger)))
                return Result(기사위치상태Codes.Closed);
            // 생활 배송은 별도 동의·배정 정책을 유지합니다. 전문 화물 근무로 가장하지 않습니다.
            if (source is null || NeighborhoodDeliveryLocationPolicy.IsNeighborhoodDelivery(source)
                || !현재배정유효(source, ledger) || current is null || !현재배정유효(current, currentLedger)
                || source.상태 != current.상태 || source.배차상태 != current.배차상태
                || source.화주Id != current.화주Id || source.주문자UserId != current.주문자UserId || source.UpdatedAt != current.UpdatedAt
                || !같은배정(ledger!, currentLedger!)) return Result(기사위치상태Codes.Unavailable);
            var driverId = ledger!.확정기사Id!;
            acceptances.TryGetValue(id, out var accepted); currentAcceptances.TryGetValue(id, out var currentAccepted);
            var assignedAt = 배정시각(ledger, accepted, now);
            drivers.TryGetValue(driverId, out var driver); currentDrivers.TryGetValue(driverId, out var currentDriver);
            shifts.TryGetValue(driverId, out var shift); currentShifts.TryGetValue(driverId, out var currentShift);
            if (!assignedAt.HasValue || accepted?.Id != currentAccepted?.Id || 배정시각(currentLedger!, currentAccepted, now) != assignedAt
                || driver?.운행상태 != 상태값.기사운행상태.운행중 || currentDriver?.운행상태 != 상태값.기사운행상태.운행중
                || !현재근무(shift, driverId, now) || !현재근무(currentShift, driverId, now)
                || shift!.Id != currentShift!.Id || shift.시작시각 != currentShift.시작시각
                || shift.StoppedAtUtc != currentShift.StoppedAtUtc || shift.ActiveDriverId != currentShift.ActiveDriverId)
                return Result(기사위치상태Codes.Unavailable);
            var start = Utc(shift.시작시각!.Value) > assignedAt.Value ? Utc(shift.시작시각.Value) : assignedAt.Value;
            locations.TryGetValue(driverId, out var point);
            if (!위치유효(point, driverId, start, now)) return Result(기사위치상태Codes.Unavailable);
            if (now - Utc(point!.기록시각) >= 유효기간 || now - Utc(point.CreatedAt) >= 유효기간)
                return Result(기사위치상태Codes.Delayed);
            point.기록시각 = Utc(point.기록시각);
            return Result(기사위치상태Codes.Available, point);
        }, StringComparer.Ordinal);
    }

    private static async Task<Dictionary<string, 운송원장>> 원장Async(SsalddelContext db, string[] ids, CancellationToken ct)
        => (await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id)).OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).ToListAsync(ct))
            .GroupBy(x => x.의뢰Id).ToDictionary(g => g.Key, g => g.First());
    private static async Task<Dictionary<string, 기사근무>> 근무Async(SsalddelContext db, string[] ids, CancellationToken ct)
        => (await db.기사근무.AsNoTracking().Where(x => Enumerable.Contains(ids, x.기사Id)).OrderByDescending(x => x.Id).ToListAsync(ct))
            .GroupBy(x => x.기사Id).ToDictionary(g => g.Key, g => g.First());
    private static async Task<Dictionary<string, 운송이벤트>> 수락Async(SsalddelContext db, string[] ids, CancellationToken ct)
        => (await db.운송이벤트.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id) && x.이벤트타입 == CargoAcceptanceRecoveryMarker.EventType)
            .OrderByDescending(x => x.Id).ToListAsync(ct)).GroupBy(x => x.의뢰Id).ToDictionary(g => g.Key, g => g.First());
    private static bool 현재근무(기사근무? shift, string driverId, DateTime now)
        => shift?.ActiveDriverId == driverId && shift.StoppedAtUtc is null && shift.시작시각.HasValue
           && Utc(shift.시작시각.Value) <= now && shift.운송실행유형 == 운송실행유형코드.화물운송;
    private static bool 현재배정유효(화주운송의뢰 source, 운송원장? ledger)
        => source.상태 == 상태값.의뢰상태.생성됨 && ledger is not null && ledger.의뢰Id == source.의뢰Id
           && ledger.배차업무유형 == 상태값.배차업무유형.용달운송 && ledger.배차큐단계 == 상태값.배차큐단계.확정
           && !string.IsNullOrWhiteSpace(ledger.확정기사Id)
           && ledger.상태 is (상태값.배차대기상태.확정 or 상태값.배차상태.배차확정 or "이동중" or "상차지도착"
               or 상태값.배차상태.상차중 or 상태값.배차상태.상차완료 or 상태값.배차상태.운송중 or "하차지도착");
    private static bool 같은배정(운송원장 before, 운송원장 current)
        => before.Id == current.Id && before.확정기사Id == current.확정기사Id && before.추천라운드 == current.추천라운드
           && before.상태 == current.상태 && before.배차큐단계 == current.배차큐단계 && before.UpdatedAt == current.UpdatedAt && before.RowVersion == current.RowVersion;
    private static DateTime? 배정시각(운송원장 ledger, 운송이벤트? accepted, DateTime now)
    {
        if (accepted?.의뢰Id != ledger.의뢰Id || accepted.이벤트시각 == default || Utc(accepted.이벤트시각) > now) return null;
        var evidence = CargoAcceptanceRecoveryMarker.Read(accepted.메타데이터);
        return evidence is not null && evidence.TransportId == ledger.Id && evidence.DriverId == ledger.확정기사Id
            && evidence.RecommendationRound == ledger.추천라운드 ? Utc(accepted.이벤트시각) : null;
    }
    private static bool 위치유효(기사위치기록? point, string driverId, DateTime start, DateTime now)
        => point is not null && point.기사Id == driverId && point.위도 is >= -90 and <= 90 && point.경도 is >= -180 and <= 180
           && (point.위도 != 0 || point.경도 != 0) && point.정확도_m is null or >= 0
           && Utc(point.기록시각) >= start && Utc(point.CreatedAt) >= start && Utc(point.기록시각) <= Utc(point.CreatedAt)
           && Utc(point.기록시각) <= now && Utc(point.CreatedAt) <= now;
    internal static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
