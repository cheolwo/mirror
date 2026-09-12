using System.Text.Json;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Outbox;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.설정;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Community;

public interface I음식마트원장동기화OutboxService
{
    Task 음식주문예약후즉시처리Async(
        음식주문응답 order,
        string updatedBy,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task 출고원장예약후즉시처리Async(
        IReadOnlyList<출고예정> outbounds,
        IReadOnlyList<입고요청> inbounds,
        string updatedBy,
        string idempotencyKey,
        string? currentStageKey = null,
        string? ledgerTemplateKey = null,
        CancellationToken cancellationToken = default);

    Task<int> 대기항목처리Async(
        int take = 100,
        CancellationToken cancellationToken = default);

}

public interface I음식마트원장동기화복구Service
{
    Task<bool> 실패항목재시도예약Async(
        long outboxId,
        int expectedAttemptCount,
        CancellationToken cancellationToken = default);
}

public sealed class 음식마트원장동기화OutboxService(
    SsalddelContext db,
    I음식마트원장Mongo동기화Service ledgerSync,
    ILogger<음식마트원장동기화OutboxService> logger)
    : I음식마트원장동기화OutboxService, I음식마트원장동기화복구Service
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task 음식주문예약후즉시처리Async(
        음식주문응답 order,
        string updatedBy,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        var item = await EnqueueAsync(
            음식마트원장동기화유형코드.음식주문,
            order.주문번호,
            updatedBy,
            idempotencyKey,
            JsonSerializer.Serialize(order, JsonOptions),
            cancellationToken);
        await ProcessItemsAsync([item.Id], 1, cancellationToken);
    }

    public async Task 출고원장예약후즉시처리Async(
        IReadOnlyList<출고예정> outbounds,
        IReadOnlyList<입고요청> inbounds,
        string updatedBy,
        string idempotencyKey,
        string? currentStageKey = null,
        string? ledgerTemplateKey = null,
        CancellationToken cancellationToken = default)
    {
        if (outbounds.Count == 0)
        {
            return;
        }

        var payload = new WarehouseOutboundPayload
        {
            OutboundIds = outbounds.Select(x => x.Id).Distinct().ToArray(),
            InboundIds = inbounds.Select(x => x.Id).Distinct().ToArray(),
            CurrentStageKey = currentStageKey,
            LedgerTemplateKey = ledgerTemplateKey
        };
        var sourceId = outbounds
            .Select(x => x.주문참조번호)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? outbounds[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var item = await EnqueueAsync(
            음식마트원장동기화유형코드.창고출고,
            sourceId,
            updatedBy,
            idempotencyKey,
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);
        await ProcessItemsAsync([item.Id], 1, cancellationToken);
    }

    public Task<int> 대기항목처리Async(
        int take = 100,
        CancellationToken cancellationToken = default)
        => ProcessItemsAsync(null, take, cancellationToken);

    public async Task<bool> 실패항목재시도예약Async(
        long outboxId,
        int expectedAttemptCount,
        CancellationToken cancellationToken = default)
    {
        if (outboxId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outboxId));
        }

        var item = await db.음식마트원장동기화Outbox
            .SingleOrDefaultAsync(x => x.Id == outboxId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (item.시도횟수 != expectedAttemptCount)
        {
            throw new DbUpdateConcurrencyException("후속 처리 상태가 이미 변경되었습니다. 목록을 새로 조회해 주세요.");
        }

        if (item.처리상태 != OutboxProcessingStatuses.Failed)
        {
            throw new InvalidOperationException("실패가 확정되어 운영자 확인이 필요한 항목만 재시도할 수 있습니다.");
        }

        item.처리상태 = OutboxProcessingStatuses.Pending;
        item.UpdatedAtUtc = DateTime.UtcNow - OutboxProcessingPolicy.RetryDelay;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<음식마트원장동기화Outbox> EnqueueAsync(
        string syncType,
        string sourceId,
        string updatedBy,
        string idempotencyKey,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var normalizedKey = Require(idempotencyKey, nameof(idempotencyKey));
        var existing = await db.음식마트원장동기화Outbox
            .SingleOrDefaultAsync(x => x.멱등키 == normalizedKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTime.UtcNow;
        var item = new 음식마트원장동기화Outbox
        {
            멱등키 = normalizedKey,
            동기화유형 = Require(syncType, nameof(syncType)),
            원천Id = Require(sourceId, nameof(sourceId)),
            변경자 = Require(updatedBy, nameof(updatedBy)),
            PayloadJson = payloadJson,
            처리상태 = OutboxProcessingStatuses.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.음식마트원장동기화Outbox.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return item;
        }
        catch (DbUpdateException)
        {
            db.Entry(item).State = EntityState.Detached;
            return await db.음식마트원장동기화Outbox
                .SingleAsync(x => x.멱등키 == normalizedKey, cancellationToken);
        }
    }

    private async Task<int> ProcessItemsAsync(
        IReadOnlyCollection<long>? requestedIds,
        int take,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var retryCutoff = now - OutboxProcessingPolicy.RetryDelay;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;
        var query = db.음식마트원장동기화Outbox.Where(x =>
            (x.동기화유형 == 음식마트원장동기화유형코드.음식주문
             || x.동기화유형 == 음식마트원장동기화유형코드.창고출고)
            && ((x.처리상태 == OutboxProcessingStatuses.Pending
             && (x.시도횟수 == 0 || x.UpdatedAtUtc <= retryCutoff))
            || (x.처리상태 == OutboxProcessingStatuses.Processing
                && x.UpdatedAtUtc <= leaseCutoff)));
        if (requestedIds is not null)
        {
            query = query.Where(x => requestedIds.Contains(x.Id));
        }

        var items = await query
            .OrderBy(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            item.처리상태 = OutboxProcessingStatuses.Processing;
            item.시도횟수 += 1;
            item.마지막시도시각Utc = now;
            item.UpdatedAtUtc = now;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.Entry(item).State = EntityState.Detached;
                continue;
            }

            processed++;
            try
            {
                await ProcessItemAsync(item, cancellationToken);
                item.처리상태 = OutboxProcessingStatuses.Succeeded;
                item.마지막오류 = string.Empty;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var retry = OutboxProcessingPolicy.CanRetry(item.시도횟수);
                item.처리상태 = retry
                    ? OutboxProcessingStatuses.Pending
                    : OutboxProcessingStatuses.Failed;
                var errorMessage = ex.GetBaseException().Message;
                item.마지막오류 = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
                logger.LogWarning(
                    ex,
                    "음식/마트 원장 동기화 Outbox 처리 실패. OutboxId={OutboxId}, SyncType={SyncType}, SourceId={SourceId}, Attempt={Attempt}, WillRetry={WillRetry}",
                    item.Id,
                    item.동기화유형,
                    item.원천Id,
                    item.시도횟수,
                    retry);
            }

            item.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    private async Task ProcessItemAsync(
        음식마트원장동기화Outbox item,
        CancellationToken cancellationToken)
    {
        if (item.동기화유형 == 음식마트원장동기화유형코드.음식주문)
        {
            var order = JsonSerializer.Deserialize<음식주문응답>(item.PayloadJson, JsonOptions)
                        ?? throw new InvalidOperationException("음식 주문 원장 동기화 payload가 비어 있습니다.");
            var ledger = await ledgerSync.음식주문동기화Async(order, item.변경자, cancellationToken);
            if (ledger is null)
            {
                throw new InvalidOperationException("음식 주문 Mongo 원장 동기화 결과가 없습니다.");
            }

            return;
        }

        if (item.동기화유형 != 음식마트원장동기화유형코드.창고출고)
        {
            throw new InvalidOperationException($"지원하지 않는 원장 동기화 유형입니다: {item.동기화유형}");
        }

        var payload = JsonSerializer.Deserialize<WarehouseOutboundPayload>(item.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("창고 출고 원장 동기화 payload가 비어 있습니다.");
        var outboundIds = payload.OutboundIds.ToList();
        var outbounds = await db.출고예정
            .Where(x => outboundIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (outbounds.Count == 0)
        {
            throw new InvalidOperationException("원장 동기화 대상 출고예정을 찾을 수 없습니다.");
        }

        var inboundIds = payload.InboundIds.ToList();
        var inbounds = inboundIds.Count == 0
            ? []
            : await db.입고요청
                .Where(x => inboundIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        var ledgerResult = await ledgerSync.출고원장동기화Async(
            outbounds,
            inbounds,
            item.변경자,
            payload.CurrentStageKey,
            payload.LedgerTemplateKey,
            cancellationToken);
        if (ledgerResult is null)
        {
            throw new InvalidOperationException("마트/창고 Mongo 원장 동기화 결과가 없습니다.");
        }
    }

    private static string Require(string? value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("값이 필요합니다.", name)
            : value.Trim();

    private sealed class WarehouseOutboundPayload
    {
        public WarehouseOutboundPayload()
        {
        }

        public long[] OutboundIds { get; set; } = [];

        public long[] InboundIds { get; set; } = [];

        public string? CurrentStageKey { get; set; }

        public string? LedgerTemplateKey { get; set; }
    }
}
