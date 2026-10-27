using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace 살뜰.Services.Dispatch.Recommendation;

public interface I화물후속픽업기준Service
{
    Task<화물후속픽업기준결과> 조회Async(string driverId, 배차경로좌표 current, DateTime nowUtc, CancellationToken ct = default);
    Task<bool> 현재근거확인Async(string driverId, 화물후속픽업기준결과 basis, CancellationToken ct = default)
        => Task.FromResult(false);
}

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "현재 전체 화물 업무의 끝점과 실제 경로로 확인된 완료 예상시각을 다음 픽업의 기준으로 읽는다.",
    StepKey = "application.cargo-next-pickup-anchor", FlowOrder = 45,
    ExecutionStage = SsalddelCodeExecutionStage.Preview, Effects = SsalddelCodeEffect.PersistentRead,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "새 배차 판단용 근거이며 운행·업무 순서·수락·완료를 변경하지 않는다. 미확인을 현재 GPS로 대체하지 않는다.")]
public sealed class 화물후속픽업기준Service(SsalddelContext db, I기사운송일정구성Service schedules,
    I운송일정삽입평가Service evaluations) : I화물후속픽업기준Service
{
    public async Task<화물후속픽업기준결과> 조회Async(string driverId, 배차경로좌표 current, DateTime nowUtc, CancellationToken ct = default)
    {
        var active = await 현재업무Async(driverId, ct);
        if (await 반납책임있음Async(driverId, ct)) return new(true, false, null, null, null, "ReturnCustodyPending");
        if (active.Length == 0) return new(false, true, current, nowUtc, null, "CurrentLocationEstimate") { 원장판본 = 판본(active, null) };
        if (active.Length > 10 || active.Any(x => x.배차업무유형 != 상태값.배차업무유형.용달운송))
            return new(true, false, null, null, null, "WholeActiveWorkUnverified");
        var plan = await schedules.구성Async(driverId, current, ct);
        var plannedIds = plan.항목목록.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (active.Any(x => !plannedIds.Contains(x.의뢰Id))) return new(true, false, null, null, null, "ActiveScheduleSourceMismatch");
        plan = plan with { 기준시각 = nowUtc };
        try
        {
            var result = await evaluations.기존일정평가Async(plan, ct);
            var basis = result.확인됨 && result.종료좌표 is not null && result.완료예상시각Utc >= nowUtc
                ? new 화물후속픽업기준결과(true, true, result.종료좌표, result.완료예상시각Utc, result.RouteOption, result.사유Code, plan)
                    { 원장판본 = 판본(active, plan) }
                : new(true, false, null, null, null, result.사유Code);
            return basis.확인됨 && !await 현재근거확인Async(driverId, basis, ct)
                ? new(true, false, null, null, null, "ActiveScheduleChanged") : basis;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return new(true, false, null, null, null, "ActiveScheduleLookupFailed"); }
    }

    public async Task<bool> 현재근거확인Async(string driverId, 화물후속픽업기준결과 basis, CancellationToken ct = default)
    {
        if (!basis.확인됨 || string.IsNullOrWhiteSpace(basis.원장판본) || await 반납책임있음Async(driverId, ct)) return false;
        var active = await 현재업무Async(driverId, ct);
        var plan = basis.활성업무있음 && basis.기존계획 is not null
            ? await schedules.구성Async(driverId, basis.기존계획.시작좌표, ct) : null;
        return basis.원장판본 == 판본(active, plan);
    }

    private Task<살뜰.도메인.운송.운송원장[]> 현재업무Async(string id, CancellationToken ct)
        => db.운송원장.AsNoTracking().Where(x => (x.확정기사Id == id || x.기사_운송자 == id)
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료)
            .OrderBy(x => x.의뢰Id).Take(11).ToArrayAsync(ct);

    private Task<bool> 반납책임있음Async(string id, CancellationToken ct)
        => 반납책임있음Async(db, id, ct);

    internal static Task<bool> 반납책임있음Async(SsalddelContext db, string id, CancellationToken ct)
        => (from wave in db.Set<분산배송WaveRecord>().AsNoTracking()
            join order in db.Set<분산배송주문Record>().AsNoTracking() on wave.WaveId equals order.WaveId
            join reservation in db.Set<분산재고예약Record>().AsNoTracking() on order.OrderId equals reservation.OrderId
            where wave.DriverId == id && (order.StatusCode == "ReturnRequired" || order.StatusCode == "Cancelled")
                && reservation.PickedQuantity > reservation.DeliveredQuantity + reservation.ReturnedQuantity
            select order.OrderId).AnyAsync(ct);

    private static string 판본(IEnumerable<살뜰.도메인.운송.운송원장> active, 기사운송일정계획? plan)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            업무 = active.Select(x => new { x.의뢰Id, x.운송번호, x.배차업무유형, x.상태, x.확정기사Id, x.기사_운송자,
                x.RowVersion, x.UpdatedAt, x.출발_픽업, x.도착 }),
            정차 = plan?.항목목록
        })));
}
