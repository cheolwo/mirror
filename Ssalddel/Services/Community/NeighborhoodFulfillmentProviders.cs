using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Data;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Community;

public sealed class NeighborhoodFulfillmentOptions
{
    public const string SectionName = "NeighborhoodFulfillment";
    public bool Enabled { get; set; }
    public int QuoteLifetimeMinutes { get; set; } = 5;
}

public sealed record FulfillmentQuoteInput(decimal WeightKg, decimal DistanceKm, decimal InternalFareKrw, DateTime EarliestPickupUtc, DateTime? DesiredCompletionUtc,
    string VehicleType = "오토바이", int Quantity = 1);
public sealed record FulfillmentProviderCapabilities(decimal MaxWeightKg, decimal MaxDistanceKm, int MaxQuantity, IReadOnlyList<string> VehicleTypes);
public sealed record FulfillmentProviderQuote(bool IsAvailable, decimal? CostKrw, DateTime? CompletionUtc, string Reason = "");
public sealed record FulfillmentProviderOperation(string AttemptId, string RequestId, DateTime CreatedAtUtc, DateTime? DesiredCompletionUtc, string StateCode, string? ExternalRequestId,
    DateTime EstimatedCompletionUtc);
public sealed record FulfillmentProviderResult(string StateCode, string? ExternalRequestId = null);

/// <summary>실제 업체 API를 가장하지 않는 최소 연결 계약. 접수 ID는 시도마다 고정하며 조회·취소도 같은 시도를 사용합니다.</summary>
public interface IFulfillmentProvider
{
    string Id { get; }
    string Name { get; }
    bool IsSimulation { get; }
    FulfillmentProviderCapabilities Capabilities { get; }
    Task<FulfillmentProviderQuote> QuoteAsync(FulfillmentQuoteInput input, CancellationToken ct);
    Task<FulfillmentProviderResult> SubmitAsync(FulfillmentProviderOperation operation, CancellationToken ct);
    Task<FulfillmentProviderResult> ReadAsync(FulfillmentProviderOperation operation, CancellationToken ct);
    Task<FulfillmentProviderResult> CancelAsync(FulfillmentProviderOperation operation, CancellationToken ct);
}

/// <summary>기존 서버 Command→현장 지급 큐를 재사용합니다. 기사 확정 후에는 자동 취소·재위탁하지 않습니다.</summary>
public sealed class InternalNeighborhoodFulfillmentProvider(SsalddelContext db, I화주운송의뢰UseCase transport, TimeProvider clock) : IFulfillmentProvider
{
    public string Id => "internal";
    public string Name => "자체 배차";
    public bool IsSimulation => true; // v1 전체가 Simulation 검증 환경입니다.
    public FulfillmentProviderCapabilities Capabilities => new(20, 30, 20, ["오토바이"]);
    public Task<FulfillmentProviderQuote> QuoteAsync(FulfillmentQuoteInput input, CancellationToken ct)
    {
        var eta = (input.EarliestPickupUtc > clock.GetUtcNow().UtcDateTime ? input.EarliestPickupUtc : clock.GetUtcNow().UtcDateTime).AddMinutes(45);
        var available = input.WeightKg <= 20 && input.DistanceKm <= 30 && input.Quantity <= 20 && input.VehicleType == "오토바이"
            && (input.DesiredCompletionUtc is null || eta <= input.DesiredCompletionUtc);
        return Task.FromResult(new FulfillmentProviderQuote(available, input.InternalFareKrw, eta,
            available ? "" : "생활 물품 20kg·30km 및 희망 완료 시간 조건을 충족하지 않습니다."));
    }
    public async Task<FulfillmentProviderResult> SubmitAsync(FulfillmentProviderOperation operation, CancellationToken ct)
    {
        var result = await transport.현장지급처리Async(operation.RequestId, new 화주운송의뢰현장지급처리요청
        { 현장지급메모 = "생활 배송 처리처 확정: Simulation 검증. 실제 지급 완료를 뜻하지 않음." }, ct);
        if (result.IsFailed) return new(NeighborhoodFulfillmentStates.ResultUnknown);
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == operation.RequestId, ct);
        if (queue is not null && queue.확정기사Id is null)
        {
            // 이 경로는 이전 수행 시도의 취소 확인 후 새 선택으로만 진입합니다.
            queue.배차큐단계 = 상태값.배차큐단계.계획배차;
            queue.생활배송수락준비완료 = true; queue.생활배송선택판본++;
            생활배송배차Policy.대기적용(queue);
            await db.SaveChangesAsync(ct);
        }
        return await ReadAsync(operation, ct);
    }
    public async Task<FulfillmentProviderResult> ReadAsync(FulfillmentProviderOperation operation, CancellationToken ct)
    {
        var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == operation.RequestId, ct);
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == operation.RequestId, ct);
        if (source is null || queue is null) return new(NeighborhoodFulfillmentStates.ResultUnknown);
        if (source.상태 == 상태값.의뢰상태.취소 || queue.배차큐단계 == 상태값.배차큐단계.종료 && queue.확정기사Id is null)
            return new(NeighborhoodFulfillmentStates.Cancelled);
        return new(source.배차상태 is 상태값.배차상태.인수완료 or 상태값.배차상태.하차완료 ? NeighborhoodFulfillmentStates.Completed
            : source.배차상태 is 상태값.배차상태.상차완료 or 상태값.배차상태.운송중 ? NeighborhoodFulfillmentStates.InTransit
            : NeighborhoodFulfillmentStates.Accepted);
    }
    public async Task<FulfillmentProviderResult> CancelAsync(FulfillmentProviderOperation operation, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var source = await db.화주운송의뢰.SingleAsync(x => x.의뢰Id == operation.RequestId, ct);
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == operation.RequestId, ct);
        if (queue?.확정기사Id is not null || source.배차상태 is not (상태값.배차상태.미시작 or 상태값.배차상태.매칭중))
            return new(NeighborhoodFulfillmentStates.Accepted);
        if (queue is not null)
        {
            queue.생활배송수락준비완료 = false; queue.생활배송선택판본++; queue.추천라운드++;
            queue.배차큐단계 = 상태값.배차큐단계.종료; queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null; queue.추천만료시각 = null;
        }
        source.배차상태 = 상태값.배차상태.미시작;
        db.운송이벤트.Add(new() { 의뢰Id = operation.RequestId, 이벤트타입 = "NeighborhoodFulfillmentInternalCancelled",
            메타데이터 = System.Text.Json.JsonSerializer.Serialize(new { operation.AttemptId }) });
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        return new(NeighborhoodFulfillmentStates.Cancelled);
    }
}

/// <summary>외부 업체 이름·계정·API를 사용하지 않는 결정적 모의 서비스. 상태는 영속 시도 시각에 기반하므로 재시작 후에도 같은 결과입니다.</summary>
public sealed class MockNeighborhoodFulfillmentProvider(string id, string name, decimal maxWeight, decimal maxDistance,
    decimal costFactor, int minutes, TimeProvider clock) : IFulfillmentProvider
{
    public string Id => id;
    public string Name => name;
    public bool IsSimulation => true;
    public FulfillmentProviderCapabilities Capabilities => new(maxWeight, maxDistance, 20, ["오토바이"]);
    public Task<FulfillmentProviderQuote> QuoteAsync(FulfillmentQuoteInput input, CancellationToken ct)
    {
        var eta = (input.EarliestPickupUtc > clock.GetUtcNow().UtcDateTime ? input.EarliestPickupUtc : clock.GetUtcNow().UtcDateTime).AddMinutes(minutes);
        var available = input.WeightKg <= maxWeight && input.DistanceKm <= maxDistance && input.Quantity <= 20 && input.VehicleType == "오토바이"
            && (input.DesiredCompletionUtc is null || eta <= input.DesiredCompletionUtc);
        return Task.FromResult(new FulfillmentProviderQuote(available, decimal.Ceiling(input.InternalFareKrw * costFactor), eta,
            available ? "" : "물품·거리 또는 희망 완료 시간 조건을 충족하지 않습니다."));
    }
    public Task<FulfillmentProviderResult> SubmitAsync(FulfillmentProviderOperation operation, CancellationToken ct)
        => Task.FromResult(new FulfillmentProviderResult(NeighborhoodFulfillmentStates.Accepted, id + ":" + operation.AttemptId));
    public Task<FulfillmentProviderResult> ReadAsync(FulfillmentProviderOperation operation, CancellationToken ct)
    {
        if (operation.StateCode is NeighborhoodFulfillmentStates.CancelPending or NeighborhoodFulfillmentStates.Cancelled)
            return Task.FromResult(new FulfillmentProviderResult(NeighborhoodFulfillmentStates.Cancelled, id + ":" + operation.AttemptId));
        var now = clock.GetUtcNow().UtcDateTime;
        var start = operation.EstimatedCompletionUtc.AddMinutes(-minutes);
        return Task.FromResult(new FulfillmentProviderResult(now >= operation.EstimatedCompletionUtc ? NeighborhoodFulfillmentStates.Completed
            : now >= start.AddMinutes(1) ? NeighborhoodFulfillmentStates.InTransit : NeighborhoodFulfillmentStates.Accepted, id + ":" + operation.AttemptId));
    }
    public Task<FulfillmentProviderResult> CancelAsync(FulfillmentProviderOperation operation, CancellationToken ct)
        => Task.FromResult(new FulfillmentProviderResult(NeighborhoodFulfillmentStates.Cancelled, id + ":" + operation.AttemptId));
}
