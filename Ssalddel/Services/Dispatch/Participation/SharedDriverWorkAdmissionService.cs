using System.Collections.Concurrent;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Dispatch.Participation;

public sealed class SharedDriverWorkOptions
{
    public const string SectionName = "SharedDriverWork";
    public bool Enabled { get; set; }
    public int OperationalMaxConcurrentJobs { get; set; } = 4;
}

public interface ISharedDriverWorkAdmissionService
{
    bool Enabled { get; }
    Task<IDisposable> EnterAsync(string driverId, CancellationToken ct = default);
    Task LockAsync(string driverId, CancellationToken ct = default);
    Task<Result> CheckAsync(string driverId, IReadOnlyList<string> candidateRequestIds, CancellationToken ct = default);
    Task<Result> CheckRecommendationAsync(string driverId, 운송원장 queue, CancellationToken ct = default);
    Task<Result> CheckParticipationAsync(string driverId, string workKind, bool requiresDirectPicking, CancellationToken ct = default);
}

/// <summary>모든 수락 경로가 같은 기사 SQL 행을 먼저 잠그고 전문 원장만 읽습니다. 프로세스 잠금만으로 다중 서버 보호를 주장하지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.Application,
    "기사 희망 업무와 음식·화물·분산 미완료 약속을 공통 수락 직전에 검사한다.",
    StepKey = "application.shared-driver-admission", FlowOrder = 60,
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    Effects = SsalddelCodeEffect.PersistentWrite,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "SQL 잠금 행만 갱신한다. 전문 원장·차량 적합성·완료·정산·지급 권위를 대체하지 않는다.")]
public sealed class SharedDriverWorkAdmissionService(SsalddelContext db, IOptions<SharedDriverWorkOptions> options)
    : ISharedDriverWorkAdmissionService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    public bool Enabled => options.Value.Enabled;

    public async Task<IDisposable> EnterAsync(string driverId, CancellationToken ct = default)
    {
        if (!Enabled) return NoopLease.Instance;
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        var gate = Gates.GetOrAdd(driverId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new GateLease(gate);
    }

    public async Task<Result> CheckParticipationAsync(string driverId, string workKind, bool requiresDirectPicking, CancellationToken ct = default)
    {
        if (!Enabled) return Result.Ok();
        var profile = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct);
        var participation = Convert(SharedDriverWorkAdmissionPolicy.Participation(profile, workKind, requiresDirectPicking));
        if (participation.IsFailed) return participation;
        var activeCount = await db.운송원장.AsNoTracking().CountAsync(x => x.배차큐단계 == 상태값.배차큐단계.확정
            && x.상태 != 상태값.배차상태.인수완료 && (x.확정기사Id == driverId || x.기사_운송자 == driverId), ct);
        if (activeCount > 0 && !profile!.ReceiveAdditionalOffers)
            return Fail("DriverWork.AdditionalOffersOff", "추가 배차 수신이 꺼져 있습니다. 현재 업무는 계속 진행할 수 있습니다.");
        if (profile!.MaxConcurrentJobs is < 1 or > 10 || options.Value.OperationalMaxConcurrentJobs is < 1 or > 10)
            return Fail("DriverWork.ConcurrentLimitInvalid", "동시 수행 건수 설정을 다시 확인해 주세요.");
        if (activeCount >= Math.Min(profile.MaxConcurrentJobs, options.Value.OperationalMaxConcurrentJobs))
            return Fail("DriverWork.ConcurrentLimitExceeded", "현재 업무가 설정한 동시 수행 건수에 도달했습니다.");
        return Result.Ok();
    }

    /// <summary>추천 저장·푸시와 조회가 같은 업무 분류·직접 피킹 선택을 사용합니다. 현재 업무 조회를 숨기는 검사가 아닙니다.</summary>
    public async Task<Result> CheckRecommendationAsync(string driverId, 운송원장 queue, CancellationToken ct = default)
    {
        if (!Enabled) return Result.Ok();
        if (string.IsNullOrWhiteSpace(driverId) || string.IsNullOrWhiteSpace(queue.의뢰Id))
            return Fail("DriverWork.CandidatesInvalid", "제안할 업무와 기사 정보를 확인해 주세요.", 400);
        var profile = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct);
        if (profile is null) return Convert(SharedDriverWorkAdmissionPolicy.Participation(null, "", false));
        // 조회에는 행 잠금이나 UPDATE를 적용하지 않습니다. 추천 직후 바뀐 조건은 수락 transaction에서 다시 검사합니다.
        return await EvaluateSnapshotAsync(driverId, profile, [queue], ct);
    }

    /// <summary>전문 업무의 완료 영수증 재조회 전에 호출하는 공통 SQL 잠금입니다. 잠금 자체는 수락 허가가 아닙니다.</summary>
    public async Task LockAsync(string driverId, CancellationToken ct = default)
    {
        if (!Enabled) return;
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Shared driver admission requires the caller's relational transaction.");
        await db.Set<DriverWorkParticipationRecord>().Where(x => x.UserId == driverId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.WorkLockRevision, x => x.WorkLockRevision + 1), ct);
    }

    /// <summary>기존 수락 transaction 안에서 최신 조건을 검사합니다. 이미 확정한 이력은 새 희망 조건으로 차단하지 않습니다.</summary>
    public async Task<Result> CheckAsync(string driverId, IReadOnlyList<string> candidateRequestIds, CancellationToken ct = default)
    {
        if (!Enabled) return Result.Ok();
        if (string.IsNullOrWhiteSpace(driverId)) return Fail("DriverWork.DriverRequired", "기사 정보를 확인해 주세요.", 400);
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is null)
            return Fail("DriverWork.AcceptanceTransactionRequired", "배차 수락을 위한 저장소 연결을 확인해 주세요.", 503);
        var ids = candidateRequestIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (ids.Length is 0 or > 10) return Fail("DriverWork.CandidatesInvalid", "수락할 업무를 다시 확인해 주세요.", 400);

        // 모든 경로의 첫 SQL write. Revision은 사용자 조건의 CAS 판본이며 수락 잠금과 별개입니다.
        await LockAsync(driverId, ct);
        var profile = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct);
        // VehicleKind는 기사 희망 정보이며 실제 차량 식별자·제원·배차 자격이 아닙니다. 기존 전문 적합성 검사를 유지합니다.
        var candidates = await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id)).ToArrayAsync(ct);
        if (candidates.Length != ids.Length || candidates.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).Count() != ids.Length)
            return Fail("DriverWork.CandidatesChanged", "수락할 업무의 현재 정보를 다시 확인해 주세요.");
        if (candidates.All(x => x.배차큐단계 == 상태값.배차큐단계.확정 && (x.확정기사Id == driverId || x.기사_운송자 == driverId)))
            return Result.Ok();
        if (profile is null) return Convert(SharedDriverWorkAdmissionPolicy.Participation(null, "", false));
        return await EvaluateSnapshotAsync(driverId, profile, candidates, ct);
    }

    private async Task<Result> EvaluateSnapshotAsync(string driverId, DriverWorkParticipationRecord profile,
        IReadOnlyList<운송원장> candidates, CancellationToken ct)
    {
        var active = await db.운송원장.AsNoTracking().Where(x => x.배차큐단계 == 상태값.배차큐단계.확정
                && x.상태 != 상태값.배차상태.인수완료 && (x.확정기사Id == driverId || x.기사_운송자 == driverId))
            .ToArrayAsync(ct);
        // 취소되어 운송 큐가 종료됐어도 실제 물품을 보유한 분산 주문의 책임은 남습니다.
        var held = await (from wave in db.Set<분산배송WaveRecord>().AsNoTracking()
                          join order in db.Set<분산배송주문Record>().AsNoTracking() on wave.WaveId equals order.WaveId
                          join reservation in db.Set<분산재고예약Record>().AsNoTracking() on order.OrderId equals reservation.OrderId
                          where wave.DriverId == driverId && reservation.PickedQuantity > reservation.DeliveredQuantity + reservation.ReturnedQuantity
                          select new { order.CargoRequestId, order.StatusCode }).Distinct().ToArrayAsync(ct);
        var heldIds = held.Select(x => x.CargoRequestId).Distinct(StringComparer.Ordinal).ToArray();
        var heldQueues = await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains(heldIds, x.의뢰Id)).ToArrayAsync(ct);
        var activeQueues = active.Concat(heldQueues).DistinctBy(x => x.의뢰Id, StringComparer.Ordinal).ToArray();
        var allQueues = activeQueues.Concat(candidates).DistinctBy(x => x.의뢰Id, StringComparer.Ordinal).ToArray();
        var allIds = allQueues.Select(x => x.의뢰Id).Concat(heldIds).Distinct(StringComparer.Ordinal).ToArray();
        var cargo = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(allIds, x.의뢰Id)).ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        var directPickingIds = await db.Set<분산배송주문Record>().AsNoTracking().Where(x => Enumerable.Contains(allIds, x.CargoRequestId))
            .Select(x => x.CargoRequestId).Distinct().ToArrayAsync(ct);
        var direct = directPickingIds.ToHashSet(StringComparer.Ordinal);
        var returns = held.Where(x => x.StatusCode is "ReturnRequired" or "Cancelled").Select(x => x.CargoRequestId).ToHashSet(StringComparer.Ordinal);
        SharedDriverWorkDemand Demand(운송원장 q)
        {
            cargo.TryGetValue(q.의뢰Id, out var source);
            return new(q.의뢰Id, ResolveWorkKind(q), direct.Contains(q.의뢰Id), q.배차업무유형 == 상태값.배차업무유형.용달운송,
                source?.화물중량Kg, source?.화물부피Cbm is { } cbm ? cbm * 1000m : null, returns.Contains(q.의뢰Id));
        }
        var demands = activeQueues.Select(Demand).ToList();
        // 원천 큐가 유실됐다고 현재 보유 물품을 0건으로 처리하지 않습니다.
        foreach (var id in heldIds.Except(activeQueues.Select(x => x.의뢰Id), StringComparer.Ordinal))
            demands.Add(new(id, DriverWorkKinds.CargoTransport, true, true, null, null, true));
        return Convert(SharedDriverWorkAdmissionPolicy.Evaluate(profile, demands, candidates.Select(Demand).ToArray(), options.Value.OperationalMaxConcurrentJobs));
    }

    public static string ResolveWorkKind(운송원장 queue)
        => queue.배차업무유형 == 상태값.배차업무유형.음식배달
            ? queue.원본의뢰유형 == "SsalddelMartPackedOrder" ? DriverWorkKinds.QuickDelivery : DriverWorkKinds.FoodDelivery
            : queue.배차업무유형 == 상태값.배차업무유형.용달운송
                ? queue.생활배송배차방식 is not null ? DriverWorkKinds.QuickDelivery : DriverWorkKinds.CargoTransport
                : string.Empty;

    private static Result Convert(SharedDriverWorkDecision decision) => decision.CanAccept ? Result.Ok() : Fail(decision.ErrorCode!, decision.Message!);
    private static Result Fail(string code, string message, int status = 409)
        => Result.Fail(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
    private sealed class GateLease(SemaphoreSlim gate) : IDisposable
    {
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) gate.Release(); }
    }
    private sealed class NoopLease : IDisposable
    {
        public static NoopLease Instance { get; } = new();
        public void Dispose() { }
    }
}
