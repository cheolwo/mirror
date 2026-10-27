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
    private readonly 음식주문원장동기화PayloadHandler _음식주문PayloadHandler = new(ledgerSync, foodOrderStore);
    private readonly 창고출고원장동기화PayloadHandler _창고출고PayloadHandler = new(db, ledgerSync);
    private readonly 음식합의취소투영PayloadHandler _합의취소PayloadHandler = new(db, ledgerSync, foodOrderStore, transportLedgerSync);
    private readonly 음식기사후속PayloadHandler _기사후속PayloadHandler = new(db, ledgerSync, foodOrderStore,
        transportLedgerSync, restaurantNotification, publisher);

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

        var payload = new 창고출고원장동기화Payload
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
            if (payloadDocument.RootElement.TryGetProperty("agreedCancellation", out var cancellationNode))
            {
                var intent = cancellationNode.Deserialize<음식합의취소투영의도>(JsonOptions)
                    ?? throw new InvalidOperationException("FoodAgreedCancellationIntentUnavailable");
                await _합의취소PayloadHandler.처리Async(item, intent, cancellationToken);
                return;
            }
            if (payloadDocument.RootElement.TryGetProperty("driverFollowup", out var followupNode))
            {
                var followup = followupNode.Deserialize<음식기사후속Payload>(JsonOptions)
                    ?? throw new InvalidOperationException("FoodDriverFollowupUnavailable");
                await _기사후속PayloadHandler.처리Async(item, followup, cancellationToken);
                return;
            }
            await _음식주문PayloadHandler.처리Async(item, JsonOptions, cancellationToken);
            return;
        }

        if (item.동기화유형 != 음식마트원장동기화유형코드.창고출고)
        {
            throw new InvalidOperationException($"지원하지 않는 원장 동기화 유형입니다: {item.동기화유형}");
        }

        await _창고출고PayloadHandler.처리Async(item, JsonOptions, cancellationToken);
    }

    private static string Require(string? value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("값이 필요합니다.", name)
            : value.Trim();
}

/// <summary>같은 SQL 취소 transaction에 저장하는 최소 투영 의도이며 고객 정보·비용 사본을 보관하지 않는다.</summary>
internal sealed record 음식합의취소투영의도(string OrderNo, string AgreementId, long? QueueId);
