using Ssalddel.Contracts.Common.Metadata;

using System.Text.Json;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Outbox;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.설정;
using 살뜰.도메인.창고;
using Ssalddel.Services.PrivacyRetention;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Services.Food;
using MediatR;
using Ssalddel.Application.Food.Events;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using Ssalddel.Contracts.Common.Community;

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

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Infrastructure,
    "기사 후속 의도를 기존 SQL Outbox에서 멱등 예약하고 최신 원천으로 처리한다.",
    ContractType = typeof(I음식기사후속OutboxService),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation,
    FlowOrder = 160,
    StepKey = "infrastructure.driver-followup-outbox",
    DependsOnStepKeys = ["contract.driver-followup-port", "application.driver-followups"],
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "예약은 호출자 transaction에 결속하고 처리 시 최신 정본·개인정보 파기 장벽을 다시 확인한다. SQL 전문 상태·Mongo 관계 투영·외부 알림은 별개이며 수령·실지급을 확정하지 않는다.")]
public sealed class 음식마트원장동기화OutboxService(
    SsalddelContext db,
    I음식마트원장Mongo동기화Service ledgerSync,
    ILogger<음식마트원장동기화OutboxService> logger,
    I개인정보복원차단Service? privacyBarrier = null,
    ISsalddelFoodOrderStore? foodOrderStore = null,
    I운송원장Mongo동기화Service? transportLedgerSync = null,
    I음식점주문실시간알림Service? restaurantNotification = null,
    IPublisher? publisher = null)
    : I음식마트원장동기화OutboxService, I음식마트원장동기화복구Service, I음식기사후속OutboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task 예약Async(음식기사후속의도 intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (string.IsNullOrWhiteSpace(intent.주문번호) || string.IsNullOrWhiteSpace(intent.배차의뢰Id)
            || string.IsNullOrWhiteSpace(intent.처리기사Id) || string.IsNullOrWhiteSpace(intent.상태))
            throw new ArgumentException("음식 기사 후속 의도의 원천과 상태가 필요합니다.", nameof(intent));
        if (privacyBarrier is not null && !await privacyBarrier.복원허용Async(
                개인정보파기원천Codes.FoodOrder, intent.주문번호, cancellationToken))
            throw new InvalidOperationException("PrivacyDeletionRestoreBlocked");
        foreach (var kind in 음식기사후속Kinds.For(intent))
        {
            var key = 음식기사후속Kinds.Key(intent, kind);
            if (db.음식마트원장동기화Outbox.Local.Any(x => x.멱등키 == key)
                || await db.음식마트원장동기화Outbox.AsNoTracking().AnyAsync(x => x.멱등키 == key, cancellationToken)) continue;
            db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
            {
                멱등키 = key, 동기화유형 = 음식마트원장동기화유형코드.음식주문,
                원천Id = intent.주문번호, 변경자 = intent.처리기사Id,
                PayloadJson = JsonSerializer.Serialize(new { DriverFollowup = new 음식기사후속Payload(kind, intent) }, JsonOptions),
                처리상태 = OutboxProcessingStatuses.Pending,
                CreatedAtUtc = intent.발생시각Utc, UpdatedAtUtc = DateTime.UtcNow
            });
        }
    }

    public async Task 즉시처리Async(음식기사후속의도 intent, CancellationToken cancellationToken = default)
    {
        var keys = 음식기사후속Kinds.For(intent).Select(kind => 음식기사후속Kinds.Key(intent, kind)).ToArray();
        var ids = await db.음식마트원장동기화Outbox.AsNoTracking().Where(x => keys.Contains(x.멱등키))
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
        if (ids.Length != 0) await ProcessItemsAsync(ids, ids.Length, cancellationToken);
    }

    public async Task 음식주문예약후즉시처리Async(
        음식주문응답 order,
        string updatedBy,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (privacyBarrier is not null && !await privacyBarrier.복원허용Async(개인정보파기원천Codes.FoodOrder, order.주문번호, cancellationToken))
            throw new InvalidOperationException("PrivacyDeletionRestoreBlocked");
        var item = await EnqueueAsync(
            음식마트원장동기화유형코드.음식주문,
            order.주문번호,
            updatedBy,
            idempotencyKey,
            JsonSerializer.Serialize(음식개인정보OutboxPolicy.Minimized(order), JsonOptions),
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
        if (item.동기화유형 == 음식마트원장동기화유형코드.음식주문 && privacyBarrier is not null
            && !await privacyBarrier.복원허용Async(개인정보파기원천Codes.FoodOrder, item.원천Id, cancellationToken))
            throw new InvalidOperationException("PrivacyDeletionRestoreBlocked");

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
                if (item.처리상태 != 음식개인정보OutboxPolicy.PrivacyExpired) item.처리상태 = OutboxProcessingStatuses.Succeeded;
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
            if (privacyBarrier is not null && !await privacyBarrier.복원허용Async(개인정보파기원천Codes.FoodOrder, item.원천Id, cancellationToken))
            {
                item.PayloadJson = "{}"; item.처리상태 = 음식개인정보OutboxPolicy.PrivacyExpired;
                return;
            }
            using var payloadDocument = JsonDocument.Parse(item.PayloadJson);
            if (payloadDocument.RootElement.TryGetProperty("driverFollowup", out var followupNode))
            {
                var followup = followupNode.Deserialize<음식기사후속Payload>(JsonOptions)
                    ?? throw new InvalidOperationException("FoodDriverFollowupUnavailable");
                await ProcessDriverFollowupAsync(item, followup, cancellationToken);
                return;
            }
            var snapshot = JsonSerializer.Deserialize<음식주문응답>(item.PayloadJson, JsonOptions)
                        ?? throw new InvalidOperationException("음식 주문 원장 동기화 payload가 비어 있습니다.");
            if (!string.Equals(snapshot.주문번호, item.원천Id, StringComparison.Ordinal))
                throw new InvalidOperationException("FoodOutboxSourceMismatch");
            // Operational DI supplies the authoritative store. The outbox only retains a minimal intent copy.
            var order = foodOrderStore is null ? snapshot : foodOrderStore.GetOrder(item.원천Id)
                ?? throw new InvalidOperationException("FoodOutboxSourceUnavailable");
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
        var outbounds = await db.출고예정.AsNoTracking()
            .Where(x => outboundIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (outbounds.Count == 0)
        {
            throw new InvalidOperationException("원장 동기화 대상 출고예정을 찾을 수 없습니다.");
        }

        var inboundIds = payload.InboundIds.ToList();
        var inbounds = inboundIds.Count == 0
            ? []
            : await db.입고요청.AsNoTracking()
                .Where(x => inboundIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        var driverAttempt = await db.Set<마트배송시도>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.주문참조번호 == item.원천Id, cancellationToken);
        var stage = payload.CurrentStageKey;
        var template = payload.LedgerTemplateKey;
        if (driverAttempt is not null)
        {
            if (outbounds.Any(x => x.주문참조번호 != item.원천Id || x.운송의뢰Id != driverAttempt.배차의뢰Id))
                throw new InvalidOperationException("MartDriverLedgerSourceMismatch");
            // 예전 Pending child의 포장 단계/추정 템플릿을 최신 기사 업무에 덮어쓰지 않습니다.
            stage = driverAttempt.상태Code switch
            {
                "Accepted" => "픽업대기", "PickedUp" => "배송중", "Delivered" => "전달완료",
                _ => throw new InvalidOperationException("MartDriverLedgerStageUnavailable")
            };
            template = CommunityLedgerTemplateKeys.SsalddelMart;
        }
        var ledgerResult = await ledgerSync.출고원장동기화Async(
            outbounds,
            inbounds,
            item.변경자,
            stage,
            template,
            cancellationToken);
        if (ledgerResult is null)
        {
            throw new InvalidOperationException("마트/창고 Mongo 원장 동기화 결과가 없습니다.");
        }
    }

    private async Task ProcessDriverFollowupAsync(음식마트원장동기화Outbox item,
        음식기사후속Payload payload, CancellationToken cancellationToken)
    {
        var intent = payload.Intent;
        if (intent.주문번호 != item.원천Id || intent.처리기사Id != item.변경자
            || 음식기사후속Kinds.Key(intent, payload.Kind) != item.멱등키
            || !음식기사후속Kinds.For(intent).Contains(payload.Kind))
            throw new InvalidOperationException("FoodDriverFollowupSourceMismatch");
        // 저장 당시의 고객 정보나 상태 사본으로 후속 업무를 확정하지 않습니다.
        var order = foodOrderStore?.GetOrder(item.원천Id)
            ?? throw new InvalidOperationException("FoodOutboxSourceUnavailable");
        switch (payload.Kind)
        {
            case 음식기사후속Kinds.FoodLedger:
                if (await ledgerSync.음식주문동기화Async(order, item.변경자, cancellationToken) is null)
                    throw new InvalidOperationException("FoodDriverFollowupLedgerUnavailable");
                break;
            case 음식기사후속Kinds.TransportLedger:
                var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == intent.운송Id, cancellationToken);
                if (queue is null || queue.의뢰Id != intent.배차의뢰Id || queue.원본의뢰Id != intent.주문번호
                    || queue.배차업무유형 != 상태값.배차업무유형.음식배달
                    || !운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형))
                    throw new InvalidOperationException("FoodDriverFollowupTransportMismatch");
                if (transportLedgerSync is null || await transportLedgerSync.운송실행투영동기화Async(queue, item.변경자, cancellationToken) is null)
                    throw new InvalidOperationException("FoodDriverFollowupTransportUnavailable");
                break;
            case 음식기사후속Kinds.RestaurantNotification:
                if (restaurantNotification is null) throw new InvalidOperationException("FoodDriverFollowupNotificationUnavailable");
                await restaurantNotification.주문상태변경알림발송Async(order, intent.음식점알림사유!, cancellationToken);
                break;
            case 음식기사후속Kinds.CommunityActivity:
                if (publisher is null) throw new InvalidOperationException("FoodDriverFollowupPublisherUnavailable");
                await publisher.Publish(new 음식배달인계상태변경됨Event(intent.배차의뢰Id, intent.주문번호,
                    intent.상태, intent.발생시각Utc, intent.EventId), cancellationToken);
                break;
            default:
                throw new InvalidOperationException("FoodDriverFollowupKindUnsupported");
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
