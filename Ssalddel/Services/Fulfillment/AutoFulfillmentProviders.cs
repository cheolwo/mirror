using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Domain.Community;
using 살뜰.Data;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Fulfillment;

public sealed record AutoFulfillmentQuote(string ProviderId, string ProviderName, bool Available,
    decimal? TotalCostKrw, DateTime? CompletionUtc, string? Reason = null);

public interface I자동수행Provider
{
    string Id { get; }
    string Name { get; }
    bool 원천지원(string sourceKind) => FulfillmentSourceKinds.IsKnown(sourceKind);
    Task<AutoFulfillmentQuote> 견적Async(FulfillmentSourceSnapshot source, DateTime? deadline, CancellationToken ct);
    Task<FulfillmentExecutionResult> 접수Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct);
    Task<FulfillmentExecutionResult> 조회Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct);
    Task<FulfillmentExecutionResult> 취소Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct);
}

/// <summary>기존 SQL 모의 접수를 재사용하며 실제 endpoint·인증·요금을 만들지 않는 가정 연결입니다.</summary>
public sealed class 가정자동수행Provider : I자동수행Provider
{
    private readonly 모의자동수행Provider inner;
    private readonly IOptions<FulfillmentRoutingOptions> options;
    private readonly ISsalddelExecutionModePolicy execution;

    public 가정자동수행Provider(SsalddelContext db, TimeProvider clock, AutoFulfillmentProviderDescriptor descriptor,
        IOptions<FulfillmentRoutingOptions> options, ISsalddelExecutionModePolicy execution)
    {
        if (!AssumedFulfillmentProviderCatalog.IsAssumed(descriptor.ProviderId))
            throw new ArgumentException("등록된 가정 수행처를 확인해야 합니다.", nameof(descriptor));
        this.options = options; this.execution = execution;
        // 모두 기존 퀵 검증 fixture의 합성 조건입니다. 실제 업체별 가격·시간·지원 범위가 아닙니다.
        inner = new(db, clock, descriptor.ProviderId, descriptor.ProviderName, 1.15m, 25, 25);
    }

    public string Id => inner.Id;
    public string Name => inner.Name;
    public bool 원천지원(string sourceKind) => options.Value.Enabled && options.Value.AssumedProvidersEnabled
        && execution.IsSimulation && sourceKind == FulfillmentSourceKinds.NeighborhoodDelivery;

    public Task<AutoFulfillmentQuote> 견적Async(FulfillmentSourceSnapshot source, DateTime? deadline, CancellationToken ct)
    {
        if (!원천지원(source.Key.SourceKind))
            return Task.FromResult(new AutoFulfillmentQuote(Id, Name, false, null, null, "가정 수행처는 별도 동의한 Simulation 생활 배송에서만 사용합니다."));
        if (!NeighborhoodPackageSizes.IsKnown(source.PackageSizeCode))
            return Task.FromResult(new AutoFulfillmentQuote(Id, Name, false, null, null, "포장 크기를 명시해야 합니다. 누락된 크기는 추정하지 않습니다."));
        return inner.견적Async(source, deadline, ct);
    }

    public async Task<FulfillmentExecutionResult> 접수Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
    {
        if (attempt.ProviderId != Id || !원천지원(attempt.Key.SourceKind)) return new(AutoFulfillmentStates.Failed, ConfirmedIncurredCostKrw: 0);
        var input = await source.읽기Async(attempt.Key, ct);
        if (input is null || !원천지원(attempt.Key.SourceKind) || !NeighborhoodPackageSizes.IsKnown(input.PackageSizeCode))
            return new(AutoFulfillmentStates.Failed, ConfirmedIncurredCostKrw: 0);
        return await inner.접수Async(attempt, source, ct);
    }

    // opt-in 철회 후에도 이미 접수한 같은 시도의 결과·취소를 확인합니다. 새 접수는 위에서 차단합니다.
    private bool 원시도확인가능(FulfillmentAttemptContext attempt) => execution.IsSimulation
        && attempt.Key.SourceKind == FulfillmentSourceKinds.NeighborhoodDelivery && attempt.ProviderId == Id;
    public Task<FulfillmentExecutionResult> 조회Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
        => 원시도확인가능(attempt) ? inner.조회Async(attempt, source, ct)
            : Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.ResultUnknown));
    public Task<FulfillmentExecutionResult> 취소Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
        => 원시도확인가능(attempt) ? inner.취소Async(attempt, source, ct)
            : Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.CancelPending));
}

/// <summary>후보 계산만 수행하고 저장·접수는 Coordinator가 담당합니다.</summary>
public static class 수행처선정Engine
{
    public static AutoFulfillmentQuote? 선정(IEnumerable<AutoFulfillmentQuote> quotes, DateTime now,
        DateTime? deadline, decimal incurred, decimal support, decimal cap)
        => 선정(quotes, now, deadline, incurred, DeliveryCostSharingCalculator.Effective(null, support), cap);

    public static AutoFulfillmentQuote? 선정(IEnumerable<AutoFulfillmentQuote> quotes, DateTime now,
        DateTime? deadline, decimal incurred, DeliveryCostSharingPolicy policy, decimal cap)
        => quotes.Where(x => x.Available && x.TotalCostKrw is >= 0 && x.CompletionUtc is not null
                && x.CompletionUtc >= now && (deadline is null || x.CompletionUtc <= deadline)
                && DeliveryCostSharingCalculator.Calculate(incurred + x.TotalCostKrw.Value, policy) is { IsAllowed: true, CustomerChargeKrw: { } customer } && customer <= cap)
            .OrderBy(x => incurred + x.TotalCostKrw!.Value).ThenBy(x => x.CompletionUtc)
            .ThenBy(x => x.ProviderId, StringComparer.Ordinal).FirstOrDefault();
}

/// <summary>모의 provider의 접수/진행을 SQL에 보존합니다. 실제 API를 호출하지 않습니다.</summary>
public sealed class 모의자동수행Provider(SsalddelContext db, TimeProvider clock, string id, string name,
    decimal costFactor, int minutes, decimal maxDistanceKm) : I자동수행Provider
{
    public string Id => id;
    public string Name => name;
    public Task<AutoFulfillmentQuote> 견적Async(FulfillmentSourceSnapshot source, DateTime? deadline, CancellationToken ct)
    {
        var start = source.EarliestPickupUtc > clock.GetUtcNow().UtcDateTime ? source.EarliestPickupUtc : clock.GetUtcNow().UtcDateTime;
        var eta = start.AddMinutes(minutes + Math.Max(0, source.PreparationMinutes) + (double)Math.Max(0, source.TravelMinutes ?? 0));
        // 음식 주문은 실측 무게가 없습니다. 음식 역량·수량·거리로만 판정하며 1kg을 만들어 넣지 않습니다.
        var supported = source.Key.SourceKind == FulfillmentSourceKinds.FoodOrder
            || source.WeightKg is > 0 and <= 20;
        var available = supported && source.TravelMinutes is >= 0 && source.DistanceKm is >= 0 && source.DistanceKm <= maxDistanceKm
            && source.InternalCostKrw is >= 0 && source.Quantity is > 0 and <= 20 && source.VehicleType == "오토바이"
            && (deadline is null || eta <= deadline);
        return Task.FromResult(new AutoFulfillmentQuote(Id, Name, available,
            source.InternalCostKrw is { } cost ? decimal.Ceiling(cost * costFactor) : null, eta,
            available ? null : "거리·요금 근거·물품 조건 또는 희망 완료 시각을 확인해야 합니다."));
    }
    public async Task<FulfillmentExecutionResult> 접수Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var job = await db.FulfillmentMockJobs.SingleOrDefaultAsync(x => x.AttemptId == attempt.AttemptId, ct);
        if (job is null)
        {
            // 모의 접수의 영속 side effect와 철회는 같은 SQL 직렬화 경계를 공유합니다.
            var c = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == attempt.Key.SourceKind && x.RequestId == attempt.Key.SourceId, ct);
            var permission = await db.AutoFulfillmentConsents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == attempt.Key.SourceKind && x.RequestId == attempt.Key.SourceId, ct);
            var a = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync(x => x.AttemptId == attempt.AttemptId, ct);
            var input = await source.읽기Async(attempt.Key, ct);
            var quote = await db.NeighborhoodFulfillmentQuotes.AsNoTracking().SingleAsync(x => x.QuoteId == a.QuoteId, ct);
            if (c?.AttemptId != attempt.AttemptId || c.Revision != attempt.Revision || permission?.Revoked != false
                || permission.Revision != attempt.ConsentRevision || input is null || input.IsClosed || input.HasCookingStarted
                || input.InputDigest != permission.InputDigest || input.SourceRevision != a.SourceRevision || input.Leg != a.Leg
                || quote.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
                return new(AutoFulfillmentStates.Failed, ConfirmedIncurredCostKrw: 0);
            var now = clock.GetUtcNow().UtcDateTime;
            job = new() { AttemptId = attempt.AttemptId, SourceKind = attempt.Key.SourceKind, RequestId = attempt.Key.SourceId,
                ProviderId = Id, ExternalRequestId = Id + ":" + attempt.AttemptId, Leg = attempt.Leg,
                StateCode = AutoFulfillmentStates.Accepted, Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now };
            db.FulfillmentMockJobs.Add(job);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); job = await db.FulfillmentMockJobs.SingleAsync(x => x.AttemptId == attempt.AttemptId, ct); }
        }
        if (tx is not null) await tx.CommitAsync(ct);
        return new(job.StateCode, job.ExternalRequestId, job.ConfirmedIncurredCostKrw);
    }
    public async Task<FulfillmentExecutionResult> 조회Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
    {
        var job = await db.FulfillmentMockJobs.AsNoTracking().SingleOrDefaultAsync(x => x.AttemptId == attempt.AttemptId, ct);
        return job is null ? new(AutoFulfillmentStates.ResultUnknown) : new(job.StateCode, job.ExternalRequestId, job.ConfirmedIncurredCostKrw);
    }
    public async Task<FulfillmentExecutionResult> 취소Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == attempt.Key.SourceKind && x.RequestId == attempt.Key.SourceId, ct);
        if (current?.AttemptId != attempt.AttemptId || current.Revision != attempt.Revision) return new(AutoFulfillmentStates.CancelPending);
        var job = await db.FulfillmentMockJobs.SingleOrDefaultAsync(x => x.AttemptId == attempt.AttemptId, ct);
        if (job is null) return new(AutoFulfillmentStates.CancelPending);
        var input = await source.읽기Async(attempt.Key, ct);
        if (input is null || !input.CanCancel || input.HasCookingStarted) return new(job.StateCode, job.ExternalRequestId, job.ConfirmedIncurredCostKrw);
        if (job.StateCode is AutoFulfillmentStates.InTransit or AutoFulfillmentStates.Completed)
            return new(job.StateCode, job.ExternalRequestId, job.ConfirmedIncurredCostKrw);
        job.StateCode = AutoFulfillmentStates.Cancelled;
        job.ConfirmedIncurredCostKrw = 0; job.ExternalDriverReference = null; job.AssignmentRevision++;
        job.Revision++; job.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(job.StateCode, job.ExternalRequestId, 0);
    }
}

public sealed class 자체자동수행Provider(TimeProvider clock) : I자동수행Provider
{
    public string Id => "internal";
    public string Name => "자체 배차";
    public Task<AutoFulfillmentQuote> 견적Async(FulfillmentSourceSnapshot source, DateTime? deadline, CancellationToken ct)
    {
        var start = source.EarliestPickupUtc > clock.GetUtcNow().UtcDateTime ? source.EarliestPickupUtc : clock.GetUtcNow().UtcDateTime;
        var eta = start.AddMinutes(45 + Math.Max(0, source.PreparationMinutes) + (double)Math.Max(0, source.TravelMinutes ?? 0));
        var supported = source.Key.SourceKind == FulfillmentSourceKinds.FoodOrder || source.WeightKg is > 0 and <= 20;
        var available = supported && source.TravelMinutes is >= 0 && source.DistanceKm is >= 0 and <= 30 && source.InternalCostKrw is >= 0
            && source.Quantity is > 0 and <= 20 && source.VehicleType == "오토바이" && (deadline is null || eta <= deadline);
        return Task.FromResult(new AutoFulfillmentQuote(Id, Name, available, source.InternalCostKrw, eta,
            available ? null : "자체 수행 비용·거리·희망 시간을 확인해야 합니다."));
    }
    public Task<FulfillmentExecutionResult> 접수Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct) => source.내부접수Async(attempt, ct);
    public Task<FulfillmentExecutionResult> 조회Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct) => source.내부조회Async(attempt, ct);
    public Task<FulfillmentExecutionResult> 취소Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct) => source.내부취소Async(attempt, ct);
}
