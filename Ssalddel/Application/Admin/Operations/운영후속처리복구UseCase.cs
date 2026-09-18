using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Services.Community;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Services.Operations;
using 살뜰.도메인.설정;
using 살뜰.도메인.운영;

namespace Ssalddel.Application.Admin.Operations;

public interface I운영후속처리복구UseCase
{
    Task<운영후속처리복구목록Dto> 목록조회Async(
        bool 완료포함 = false,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<운영후속처리재시도응답Dto?> 재시도예약Async(
        string 원천Code,
        long 원천항목Id,
        운영후속처리재시도요청Dto 요청,
        CancellationToken cancellationToken = default);
}

public sealed class 운영후속처리복구UseCase(
    SsalddelContext db,
    I음식마트원장동기화복구Service 음식마트원장동기화Outbox,
    I운영체제업무인계OutboxService 운영체제업무인계Outbox)
    : I운영후속처리복구UseCase
{
    public async Task<운영후속처리복구목록Dto> 목록조회Async(
        bool 완료포함 = false,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var boundedTake = Math.Clamp(take, 1, 200);

        var ledgerQuery = db.음식마트원장동기화Outbox.AsNoTracking();
        if (!완료포함)
        {
            ledgerQuery = ledgerQuery.Where(x => x.처리상태 != OutboxProcessingStatuses.Succeeded);
        }

        var ledgerItems = await ledgerQuery
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);

        var handoffQuery =
            from outbox in db.운영체제업무인계Outbox.AsNoTracking()
            join handoff in db.운영체제업무인계.AsNoTracking()
                on outbox.인계StableId equals handoff.인계StableId into handoffs
            from handoff in handoffs.DefaultIfEmpty()
            select new { Outbox = outbox, Handoff = handoff };
        if (!완료포함)
        {
            handoffQuery = handoffQuery.Where(x =>
                x.Outbox.처리상태Code != 운영체제업무인계Outbox상태Codes.완료);
        }

        var handoffItems = await handoffQuery
            .OrderByDescending(x => x.Outbox.UpdatedAt)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);

        var items = ledgerItems
            .Select(x => MapLedger(x, now))
            .Concat(handoffItems.Select(x => MapHandoff(x.Outbox, x.Handoff, now)))
            .OrderByDescending(x => x.운영자확인필요)
            .ThenBy(x => x.다음처리예정시각Utc ?? DateTime.MaxValue)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Take(boundedTake)
            .ToArray();

        return new 운영후속처리복구목록Dto
        {
            조회시각Utc = now,
            전체항목수 = items.Length,
            자동재시도대기수 = items.Count(x =>
                x.상태Code == 운영후속처리복구상태Codes.자동재시도대기),
            처리중수 = items.Count(x =>
                x.상태Code == 운영후속처리복구상태Codes.처리중),
            운영자확인필요수 = items.Count(x => x.운영자확인필요),
            항목 = items
        };
    }

    public async Task<운영후속처리재시도응답Dto?> 재시도예약Async(
        string 원천Code,
        long 원천항목Id,
        운영후속처리재시도요청Dto 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        if (원천항목Id <= 0)
        {
            throw new ArgumentException("원천 항목 ID가 필요합니다.", nameof(원천항목Id));
        }

        var sourceCode = 원천Code?.Trim();
        if (string.Equals(
                sourceCode,
                운영후속처리복구원천Codes.운영체제업무인계,
                StringComparison.Ordinal))
        {
            var handoffScheduled = await 운영체제업무인계Outbox.실패항목재시도예약Async(
                원천항목Id,
                요청.예상처리시도수,
                cancellationToken);
            if (!handoffScheduled)
            {
                return null;
            }

            var handoffItem = await db.운영체제업무인계Outbox
                .AsNoTracking()
                .SingleAsync(x => x.Id == 원천항목Id, cancellationToken);
            return new 운영후속처리재시도응답Dto
            {
                복구StableId = BuildStableId(운영후속처리복구원천Codes.운영체제업무인계, handoffItem.Id),
                상태Code = 운영후속처리복구상태Codes.자동재시도대기,
                처리시도수 = handoffItem.처리시도수,
                재시도예약시각Utc = handoffItem.다음처리시각Utc ?? handoffItem.UpdatedAt
            };
        }

        if (!string.Equals(
                sourceCode,
                운영후속처리복구원천Codes.음식마트원장동기화,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("지원하지 않는 후속 처리 원천입니다.");
        }

        var scheduled = await 음식마트원장동기화Outbox.실패항목재시도예약Async(
            원천항목Id,
            요청.예상처리시도수,
            cancellationToken);
        if (!scheduled)
        {
            return null;
        }

        var item = await db.음식마트원장동기화Outbox
            .AsNoTracking()
            .SingleAsync(x => x.Id == 원천항목Id, cancellationToken);
        return new 운영후속처리재시도응답Dto
        {
            복구StableId = BuildStableId(운영후속처리복구원천Codes.음식마트원장동기화, item.Id),
            상태Code = 운영후속처리복구상태Codes.자동재시도대기,
            처리시도수 = item.시도횟수,
            재시도예약시각Utc = item.UpdatedAtUtc + OutboxProcessingPolicy.RetryDelay
        };
    }

    private static 운영후속처리복구항목Dto MapLedger(
        음식마트원장동기화Outbox item,
        DateTime now)
    {
        var staleProcessing = item.처리상태 == OutboxProcessingStatuses.Processing
                              && item.UpdatedAtUtc <= now - OutboxProcessingPolicy.LeaseTimeout;
        var operatorReview = item.처리상태 == OutboxProcessingStatuses.Failed || staleProcessing;
        var status = item.처리상태 switch
        {
            OutboxProcessingStatuses.Succeeded => 운영후속처리복구상태Codes.정상완료,
            OutboxProcessingStatuses.Processing when !staleProcessing => 운영후속처리복구상태Codes.처리중,
            OutboxProcessingStatuses.Pending => 운영후속처리복구상태Codes.자동재시도대기,
            _ => 운영후속처리복구상태Codes.운영자확인필요
        };

        return new 운영후속처리복구항목Dto
        {
            복구StableId = BuildStableId(운영후속처리복구원천Codes.음식마트원장동기화, item.Id),
            원천Code = 운영후속처리복구원천Codes.음식마트원장동기화,
            원천항목Id = item.Id,
            업무유형Code = item.동기화유형,
            상태Code = status,
            현재책임운영체제Id = ResolveLedgerOperatingSystem(item.동기화유형),
            처리시도수 = item.시도횟수,
            최대자동시도수 = OutboxProcessingPolicy.MaximumAttempts,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            다음처리예정시각Utc = item.처리상태 == OutboxProcessingStatuses.Pending
                ? item.UpdatedAtUtc + OutboxProcessingPolicy.RetryDelay
                : null,
            운영자확인필요 = operatorReview,
            재시도예약가능 = item.처리상태 == OutboxProcessingStatuses.Failed,
            실패분류Code = operatorReview ? 업무실패분류Codes.최종수동검토 : null,
            재시도정책Code = item.처리상태 == OutboxProcessingStatuses.Failed
                ? 업무재시도정책Codes.운영자안전재시도
                : 업무재시도정책Codes.자동재시도안함,
            상태전체재조회필요 = false,
            복구가능행동 = item.처리상태 == OutboxProcessingStatuses.Failed
                ? [업무복구행동Ids.운영자안전재시도예약]
                : [],
            안전요약 = ResolveLedgerSummary(item.동기화유형)
        };
    }

    private static 운영후속처리복구항목Dto MapHandoff(
        운영체제업무인계Outbox item,
        운영체제업무인계? handoff,
        DateTime now)
    {
        var completed = item.처리상태Code == 운영체제업무인계Outbox상태Codes.완료;
        var processing = item.처리상태Code == 운영체제업무인계Outbox상태Codes.처리중;
        var retryPending = item.처리상태Code == 운영체제업무인계Outbox상태Codes.재시도대기
                           || item.처리상태Code == 운영체제업무인계Outbox상태Codes.대기;
        var failed = item.처리상태Code == 운영체제업무인계Outbox상태Codes.실패;
        var stale = processing && item.UpdatedAt <= now - OutboxProcessingPolicy.LeaseTimeout;
        var operatorReview = failed;

        return new 운영후속처리복구항목Dto
        {
            복구StableId = BuildStableId(운영후속처리복구원천Codes.운영체제업무인계, item.Id),
            원천Code = 운영후속처리복구원천Codes.운영체제업무인계,
            원천항목Id = item.Id,
            업무유형Code = item.이벤트Type,
            상태Code = completed
                ? 운영후속처리복구상태Codes.정상완료
                : processing && !stale
                    ? 운영후속처리복구상태Codes.처리중
                    : retryPending || stale
                        ? 운영후속처리복구상태Codes.자동재시도대기
                        : 운영후속처리복구상태Codes.운영자확인필요,
            현재책임운영체제Id = handoff?.현재책임운영체제Id
                ?? OperatingSystemIds.PlatformOperations,
            처리시도수 = item.처리시도수,
            최대자동시도수 = OutboxProcessingPolicy.MaximumAttempts,
            CreatedAtUtc = item.CreatedAt,
            UpdatedAtUtc = item.UpdatedAt,
            다음처리예정시각Utc = retryPending
                ? item.다음처리시각Utc ?? item.UpdatedAt
                : stale
                    ? item.UpdatedAt + OutboxProcessingPolicy.LeaseTimeout
                    : null,
            운영자확인필요 = operatorReview,
            재시도예약가능 = failed,
            실패분류Code = operatorReview ? 업무실패분류Codes.최종수동검토 : null,
            재시도정책Code = failed
                ? 업무재시도정책Codes.운영자안전재시도
                : 업무재시도정책Codes.자동재시도안함,
            상태전체재조회필요 = false,
            복구가능행동 = failed
                ? [업무복구행동Ids.운영자안전재시도예약]
                : [],
            안전요약 = completed
                ? "운영체제 사이 업무 인계 후속 처리가 완료되었습니다."
                : failed
                    ? "운영체제 사이 업무 인계 후속 처리가 반복 실패해 운영자 확인이 필요합니다."
                    : "운영체제 사이 업무 인계 후속 처리가 자동으로 진행 중입니다."
        };
    }

    private static string ResolveLedgerOperatingSystem(string syncType)
        => syncType == 음식마트원장동기화유형코드.창고출고
            ? OperatingSystemIds.WarehouseCommerceFulfillment
            : OperatingSystemIds.FoodDelivery;

    private static string ResolveLedgerSummary(string syncType)
        => syncType switch
        {
            음식마트원장동기화유형코드.음식주문 => "완료된 음식 주문을 공동 원장에 반영하는 후속 처리입니다.",
            음식마트원장동기화유형코드.창고출고 => "완료된 창고 출고를 공동 원장에 반영하는 후속 처리입니다.",
            음식마트원장동기화유형코드.음식배차요청 => "음식 주문의 배차 요청을 후속 원장에 반영하는 처리입니다.",
            음식마트원장동기화유형코드.음식배달완료WorldProjection => "완료된 음식 배달을 관찰용 상태 사본에 반영하는 처리입니다.",
            _ => "완료된 원 업무의 후속 원장 반영 처리입니다."
        };

    private static string BuildStableId(string sourceCode, long id)
        => $"follow-up-recovery:{sourceCode}:{id}";
}
