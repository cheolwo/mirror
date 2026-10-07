using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Transport;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using Ssalddel.Services.PrivacyRetention;
using Ssalddel.Contracts.Common.PrivacyRetention;

namespace Ssalddel.Services.Food;

public interface I음식배차요청OutboxService
{
    Task 예약Async(음식주문응답 order, string actorUserId, string eventId, CancellationToken cancellationToken = default);
    Task<bool> 즉시처리Async(string eventId, CancellationToken cancellationToken = default);
    Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default);
}

/// <summary>
/// 음식점 수락과 같은 RDB 트랜잭션에 배차 요청을 남기고, 실제 운송 원장 생성은 재시도 가능한 후속 작업으로 수행합니다.
/// 기존 Outbox 테이블을 재사용하지만 원장 투영 작업과 처리 유형은 분리합니다.
/// </summary>
public sealed class 음식배차요청OutboxService(
    SsalddelContext db,
    I운송의뢰배차대기Service dispatchQueueService,
    I운송원장Mongo동기화Service transportLedgerSync,
    I음식마트원장동기화OutboxService foodLedgerOutbox,
    ITransportRequestLedgerRealtimeService transportLedgerRealtime,
    I음식점주문실시간알림Service restaurantNotification,
    ISsalddelFoodOrderStore orderStore,
    IKakao좌표변환Service kakaoGeoService,
    ILogger<음식배차요청OutboxService> logger,
    I개인정보복원차단Service? privacyBarrier = null) : I음식배차요청OutboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task 예약Async(
        음식주문응답 order,
        string actorUserId,
        string eventId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (privacyBarrier is not null && !await privacyBarrier.복원허용Async(개인정보파기원천Codes.FoodOrder, order.주문번호, cancellationToken))
            throw new InvalidOperationException("PrivacyDeletionRestoreBlocked");
        var idempotencyKey = Key(eventId);
        if (await db.음식마트원장동기화Outbox.AnyAsync(x => x.멱등키 == idempotencyKey, cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
        {
            멱등키 = idempotencyKey,
            동기화유형 = 음식마트원장동기화유형코드.음식배차요청,
            원천Id = order.주문번호,
            변경자 = string.IsNullOrWhiteSpace(actorUserId) ? $"restaurant:{order.음식점Id}" : actorUserId.Trim(),
            PayloadJson = JsonSerializer.Serialize(new 음식배차요청Payload
            {
                EventId = eventId,
                Order = 음식개인정보OutboxPolicy.Minimized(order)
            }, JsonOptions),
            처리상태 = OutboxProcessingStatuses.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (db.Database.CurrentTransaction is null)
        {
            db.ChangeTracker.Clear();
            if (!await db.음식마트원장동기화Outbox.AsNoTracking()
                    .AnyAsync(x => x.멱등키 == idempotencyKey, cancellationToken))
            {
                throw;
            }
        }
    }

    public async Task<bool> 즉시처리Async(string eventId, CancellationToken cancellationToken = default)
    {
        var key = Key(eventId);
        return await ProcessItemsAsync([key], 1, cancellationToken) > 0;
    }

    public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default)
        => ProcessItemsAsync(null, take, cancellationToken);

    private async Task<int> ProcessItemsAsync(
        IReadOnlyCollection<string>? requestedKeys,
        int take,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var retryCutoff = now - OutboxProcessingPolicy.RetryDelay;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;
        var query = db.음식마트원장동기화Outbox.Where(x =>
            x.동기화유형 == 음식마트원장동기화유형코드.음식배차요청
            && ((x.처리상태 == OutboxProcessingStatuses.Pending
                 && (x.시도횟수 == 0 || x.UpdatedAtUtc <= retryCutoff))
                || (x.처리상태 == OutboxProcessingStatuses.Processing && x.UpdatedAtUtc <= leaseCutoff)));
        if (requestedKeys is not null)
        {
            query = query.Where(x => requestedKeys.Contains(x.멱등키));
        }

        var items = await query.OrderBy(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.처리상태 = OutboxProcessingStatuses.Processing;
            item.시도횟수 += 1;
            item.마지막시도시각Utc = now;
            item.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                await ProcessItemAsync(item, cancellationToken);
                if (item.처리상태 != 음식개인정보OutboxPolicy.PrivacyExpired) item.처리상태 = OutboxProcessingStatuses.Succeeded;
                item.마지막오류 = string.Empty;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                item.처리상태 = OutboxProcessingPolicy.CanRetry(item.시도횟수)
                    ? OutboxProcessingStatuses.Pending
                    : OutboxProcessingStatuses.Failed;
                item.마지막오류 = ex.GetBaseException().GetType().Name;
                logger.LogWarning(
                    "음식 배차 요청 Outbox 처리 실패. OutboxId={OutboxId}, OrderNo={OrderNo}, Attempt={Attempt}",
                    item.Id, item.원천Id, item.시도횟수);
            }

            item.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return items.Count;
    }

    private async Task ProcessItemAsync(음식마트원장동기화Outbox item, CancellationToken cancellationToken)
    {
        if (privacyBarrier is not null && !await privacyBarrier.복원허용Async(개인정보파기원천Codes.FoodOrder, item.원천Id, cancellationToken))
        {
            item.PayloadJson = "{}"; item.처리상태 = 음식개인정보OutboxPolicy.PrivacyExpired;
            return;
        }
        var payload = JsonSerializer.Deserialize<음식배차요청Payload>(item.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("음식 배차 요청 payload가 비어 있습니다.");
        var snapshot = payload.Order ?? throw new InvalidOperationException("음식 배차 요청에 주문 사본이 없습니다.");
        if (!string.Equals(snapshot.주문번호, item.원천Id, StringComparison.Ordinal))
            throw new InvalidOperationException("음식 배차 요청의 원천 주문이 일치하지 않습니다.");
        // payload는 수락 의도의 과거 사본입니다. 재시도는 현재 주문·현재 큐를 투영합니다.
        var order = orderStore.GetOrder(snapshot.주문번호)
                    ?? throw new InvalidOperationException("배차대기를 연결할 음식 주문을 찾을 수 없습니다.");
        var current = 음식배달업무상태전이Guard.정본상태확인(order.상태);
        운송원장 queue;
        if (order.배차대기Id is { } dispatchWaitId)
        {
            queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == dispatchWaitId, cancellationToken)
                    ?? throw new InvalidOperationException("음식 주문에 연결된 배차대기를 찾을 수 없습니다.");
        }
        else
        {
            if (current is 음식주문상태코드.주문대기 or 음식주문상태코드.거절 or 음식주문상태코드.취소)
                throw new InvalidOperationException("음식점 수락 뒤의 주문에만 배차대기를 연결할 수 있습니다.");
            queue = await CreateDispatchQueueAsync(order, cancellationToken);
        }
        if (queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || queue.원본의뢰유형 != 운송의뢰배차원천유형.음식점주문
            || !string.Equals(queue.원본의뢰Id, order.주문번호, StringComparison.Ordinal))
            throw new InvalidOperationException("음식 주문과 배차대기의 업무 출처가 일치하지 않습니다.");

        var updated = orderStore.배차대기반영(order.주문번호, queue.Id, DateTime.UtcNow)
                      ?? throw new InvalidOperationException("배차대기를 연결할 음식 주문을 찾을 수 없습니다.");
        queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == queue.Id, cancellationToken) ?? queue;
        await transportLedgerSync.운송실행투영동기화Async(queue, item.변경자, cancellationToken);
        await foodLedgerOutbox.음식주문예약후즉시처리Async(
            updated,
            item.변경자,
            $"food-dispatch-ledger:{payload.EventId}",
            cancellationToken);
        await BestEffortAsync(
            () => transportLedgerRealtime.PublishAsync(order.주문번호, "FoodDispatchRequested", cancellationToken),
            "운송 실시간 투영",
            order.주문번호,
            cancellationToken);
        await BestEffortAsync(
            () => restaurantNotification.주문상태변경알림발송Async(updated, "음식점 수락 후 기사 배차를 시작했습니다.", cancellationToken),
            "음식점 알림",
            order.주문번호,
            cancellationToken);
    }

    private async Task<운송원장> CreateDispatchQueueAsync(음식주문응답 order, CancellationToken cancellationToken)
    {
        var pickupAddress = JoinAddress(order.음식점주소, order.음식점상세주소);
        var dropoffAddress = order.수령인정보.주소;
        var pickup = await ResolveCoordinateAsync(order.음식점위도, order.음식점경도, pickupAddress, cancellationToken);
        var dropoff = await ResolveCoordinateAsync(null, null, dropoffAddress, cancellationToken);
        var target = new 출고예정운송대상
        {
            원천유형 = 출고예정운송대상원천유형.음식주문,
            원천참조번호 = order.주문번호,
            운송의뢰Id = order.주문번호,
            표시명 = FoodOrderSampleData.BuildMenuSummary(order.상품목록),
            판매자UserId = $"restaurant:{order.음식점Id}",
            주문자UserId = order.주문자UserId,
            상차주소 = pickupAddress,
            상차위도 = pickup?.위도,
            상차경도 = pickup?.경도,
            하차주소 = dropoffAddress,
            하차위도 = dropoff?.위도,
            하차경도 = dropoff?.경도,
            온도조건 = "음식",
            파손주의 = true,
            Lines = order.상품목록.Select((line, index) => new 출고예정운송대상라인
            {
                LineKey = $"{order.주문번호}-{index + 1}",
                Sku = line.상품명,
                ProductName = line.상품명,
                Quantity = line.수량
            }).ToArray()
        };
        var queue = await dispatchQueueService.생성또는조회Async(target, new 운송의뢰배차대기생성옵션
        {
            의뢰Id = order.주문번호,
            화주Id = $"restaurant:{order.음식점Id}",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            원본의뢰Id = order.주문번호,
            픽업상세주소 = order.음식점상세주소,
            하차상세주소 = order.수령인정보.상세주소,
            상태 = 상태값.배차대기상태.대기
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return queue;
    }

    private async Task<(decimal 위도, decimal 경도)?> ResolveCoordinateAsync(
        decimal? latitude,
        decimal? longitude,
        string address,
        CancellationToken cancellationToken)
    {
        if (latitude.HasValue && longitude.HasValue) return (latitude.Value, longitude.Value);
        try
        {
            var result = await kakaoGeoService.주소정보조회Async(address, cancellationToken);
            return result?.위도 is { } lat && result.경도 is { } lng ? (lat, lng) : null;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("음식 배차 주소 좌표 조회 실패. ErrorType={ErrorType}", ex.GetBaseException().GetType().Name);
            return null;
        }
    }

    private async Task BestEffortAsync(
        Func<Task> action,
        string actionName,
        string orderNo,
        CancellationToken cancellationToken)
    {
        try { await action(); }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{ActionName} 실패. OrderNo={OrderNo}", actionName, orderNo);
        }
    }

    private static string Key(string eventId)
        => $"food-dispatch:{(string.IsNullOrWhiteSpace(eventId) ? throw new ArgumentException("Event ID가 필요합니다.") : eventId.Trim())}";
    private static string JoinAddress(string primary, string detail)
        => string.IsNullOrWhiteSpace(detail) ? primary : $"{primary} {detail}";

    private sealed class 음식배차요청Payload
    {
        public string EventId { get; set; } = string.Empty;
        public 음식주문응답? Order { get; set; }
    }
}
