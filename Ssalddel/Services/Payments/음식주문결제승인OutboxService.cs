using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;

namespace 살뜰.Services.Payments;

public sealed class 음식주문결제승인Options
{
    public const string SectionName = "FoodOrderPaymentApproval";
    public bool Enabled { get; set; }
}

/// <summary>
/// 이미 승인된 결제 정본을 음식 주문에 연결한다. PG 호출·환불·정산·업무 상태 전이는 하지 않는다.
/// 전용 scoped DbContext에서 실행하며 쓰기는 조건부 SQL과 트랜잭션으로만 수행한다.
/// </summary>
public sealed class 음식주문결제승인OutboxService(
    SsalddelContext db,
    IOptions<음식주문결제승인Options> options,
    TimeProvider clock,
    ILogger<음식주문결제승인OutboxService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> 대기승인반영Async(int take = 100, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return 0;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var retryCutoff = now - OutboxProcessingPolicy.RetryDelay;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;
        var items = await 대상의도()
            .AsNoTracking()
            .Where(x => (x.Status == OutboxProcessingStatuses.Pending
                        && (x.RetryCount == 0 || x.UpdatedAt <= retryCutoff))
                    || (x.Status == OutboxProcessingStatuses.Processing && x.UpdatedAt <= leaseCutoff))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = item.RetryCount + 1;
            // 조회 이후 다른 worker가 선점/재선점했다면 건드리지 않는다.
            var claimed = await 대상의도()
                .Where(x => x.Id == item.Id && x.Status == item.Status
                    && x.RetryCount == item.RetryCount && x.UpdatedAt == item.UpdatedAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, OutboxProcessingStatuses.Processing)
                    .SetProperty(x => x.RetryCount, attempt)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            processed++;
            try
            {
                var payload = JsonSerializer.Deserialize<승인의도>(item.PayloadJson, JsonOptions);
                if (payload is null || payload.의도 != "FoodOrderPaymentApproved"
                    || string.IsNullOrWhiteSpace(payload.결제Id) || payload.결제Id.Length > 100
                    || string.IsNullOrWhiteSpace(payload.대상Id) || payload.금액 <= 0
                    || payload.통화 != "KRW" || payload.승인시각Utc == default)
                {
                    throw new 승인거절Exception("InvalidApprovalIntent");
                }

                await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                    await 승인반영Async(payload, cancellationToken);
                    if (await 처리상태저장Async(item.Id, attempt, OutboxProcessingStatuses.Succeeded, cancellationToken) != 1)
                    {
                        throw new DbUpdateConcurrencyException("ApprovalOutboxLeaseLost");
                    }

                    await tx.CommitAsync(cancellationToken);
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 종료 뒤 같은 의도가 lease 만료 시 재처리된다. 성공으로 가장하지 않는다.
                throw;
            }
            catch (Exception ex)
            {
                var permanent = ex is 승인거절Exception or JsonException;
                var retry = !permanent && OutboxProcessingPolicy.CanRetry(attempt);
                await 처리상태저장Async(item.Id, attempt,
                    retry ? OutboxProcessingStatuses.Pending : OutboxProcessingStatuses.Failed,
                    cancellationToken);
                // 원문 payload·업무 ID·예외 원문은 로그에 넣지 않는다.
                logger.LogWarning("Food approval intent blocked. OutboxId={OutboxId} Code={Code} Attempt={Attempt} Retry={Retry}",
                    item.Id, ex is 승인거절Exception rejected ? rejected.Code
                        : ex is JsonException ? "InvalidJson" : "ApprovalPersistenceFailure", attempt, retry);
            }
        }

        return processed;
    }

    private IQueryable<Command알림Outbox> 대상의도()
        => db.Command알림Outbox.Where(x => x.FeatureName == "FoodOrderPayment"
            && x.Target == "SsalddelFoodOrder" && x.EventName == nameof(결제승인완료Event));

    private Task<int> 처리상태저장Async(long id, int attempt, string status, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return 대상의도().Where(x => x.Id == id && x.Status == OutboxProcessingStatuses.Processing && x.RetryCount == attempt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, status).SetProperty(x => x.UpdatedAt, now), ct);
    }

    private async Task 승인반영Async(승인의도 intent, CancellationToken ct)
    {
        var payment = await db.결제.AsNoTracking().SingleOrDefaultAsync(x => x.결제Id == intent.결제Id, ct);
        if (payment is null || payment.결제Id != intent.결제Id
            || payment.결제대상유형 != 결제공통정의.결제대상유형.음식주문
            || payment.대상Id != intent.대상Id
            || payment.공통결제상태 != 결제공통정의.결제상태.승인완료
            || payment.결제상태 != 상태값.결제상태.결제완료 || payment.취소일시 is not null
            || payment.결제금액 != intent.금액 || payment.통화 != intent.통화
            || payment.승인일시 is null || 승인시각정규화(payment.승인일시.Value) != 승인시각정규화(intent.승인시각Utc))
        {
            throw new 승인거절Exception("PaymentApprovalMismatch");
        }

        var order = await db.음식주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문번호 == intent.대상Id, ct);
        if (order is null || order.주문번호 != intent.대상Id
            || string.IsNullOrWhiteSpace(order.주문자UserId) || order.주문자UserId != payment.화주Id
            || order.총주문금액 != payment.결제금액)
        {
            throw new 승인거절Exception("FoodOrderApprovalMismatch");
        }

        // 이미 적용한 승인 의도의 재전달은 주문이 이후 취소되어도 과거 사실만 유지한다.
        if (order.결제승인Id is not null)
        {
            if (order.결제승인Id == payment.결제Id && order.결제승인금액 == payment.결제금액
                && order.결제승인통화 == payment.통화
                && order.결제승인시각Utc == payment.승인일시)
            {
                return;
            }

            throw new 승인거절Exception("FoodOrderApprovalConflict");
        }

        if (order.상태 is 음식주문상태코드.취소 or 음식주문상태코드.거절)
        {
            throw new 승인거절Exception("FoodOrderNoLongerPayable");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var updated = await db.음식주문
            .Where(x => x.Id == order.Id && x.결제승인Id == null && x.상태 == order.상태)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.결제승인Id, payment.결제Id)
                .SetProperty(x => x.결제승인금액, (decimal?)payment.결제금액)
                .SetProperty(x => x.결제승인통화, payment.통화)
                .SetProperty(x => x.결제승인시각Utc, payment.승인일시)
                .SetProperty(x => x.UpdatedAt, now), ct);
        if (updated != 1)
        {
            throw new DbUpdateConcurrencyException("FoodOrderApprovalChanged");
        }
    }

    // MySQL datetime(6)은 100ns 자리를 보존하지 않는다. UTC 승인 시각을 마이크로초 단위로 대조한다.
    private static long 승인시각정규화(DateTime value) => value.Ticks / 10;
    private sealed record 승인의도(string 결제Id, string 대상Id, int 금액, string 통화, DateTime 승인시각Utc, string 의도);
    private sealed class 승인거절Exception(string code) : Exception(code)
    {
        public string Code { get; } = code;
    }
}
