using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Admin.Food;

public interface I음식주문운영추적UseCase
{
    Task<음식주문운영추적응답?> 조회Async(
        string 주문번호,
        CancellationToken cancellationToken = default);
}

public sealed class 음식주문운영추적UseCase(
    SsalddelContext db,
    살뜰.Services.Options.ISsalddelExecutionModePolicy? executionMode = null) : I음식주문운영추적UseCase
{
    public async Task<음식주문운영추적응답?> 조회Async(
        string 주문번호,
        CancellationToken cancellationToken = default)
    {
        var normalizedOrderNo = 주문번호?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOrderNo))
        {
            throw new ArgumentException("주문번호가 필요합니다.", nameof(주문번호));
        }

        var order = await db.음식주문
            .AsNoTracking()
            .Include(x => x.상태이력)
            .SingleOrDefaultAsync(x => x.주문번호 == normalizedOrderNo, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var queue = await ResolveQueueAsync(order.배차대기Id, normalizedOrderNo, cancellationToken);
        var ledgerOutboxes = await db.음식마트원장동기화Outbox
            .AsNoTracking()
            .Where(x => x.동기화유형 == 음식마트원장동기화유형코드.음식주문
                        && x.원천Id == normalizedOrderNo)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);
        var queueId = queue?.Id;
        var recommendationOutboxes = await db.배차추천알림Outbox
            .AsNoTracking()
            .Where(x => x.의뢰Id == normalizedOrderNo
                        || (queueId.HasValue && x.배차대기Id == queueId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .ToListAsync(cancellationToken);
        var transportEvents = await db.운송이벤트
            .AsNoTracking()
            .Where(x => x.의뢰Id == normalizedOrderNo)
            .OrderByDescending(x => x.이벤트시각)
            .Take(50)
            .Select(x => new 음식주문운영이벤트응답
            {
                이벤트Id = x.Id,
                이벤트유형 = x.이벤트타입,
                이벤트시각Utc = x.이벤트시각
            })
            .ToListAsync(cancellationToken);
        var deliveryAttempts = await db.음식배달시도
            .AsNoTracking()
            .Where(x => x.주문번호 == normalizedOrderNo)
            .OrderByDescending(x => x.시도순번)
            .Select(x => new 음식배달시도운영응답
            {
                시도StableId = x.시도StableId,
                제안Id = x.제안Id,
                기사Id = x.기사Id,
                시도순번 = x.시도순번,
                Revision = x.Revision,
                상태Code = x.상태Code,
                수락시각Utc = x.수락시각Utc,
                표시준비예정시각Utc = x.표시준비예정시각Utc,
                가게도착시각Utc = x.가게도착시각Utc,
                픽업완료시각Utc = x.픽업완료시각Utc,
                중단시각Utc = x.중단시각Utc,
                전달완료시각Utc = x.전달완료시각Utc,
                현장대기초 = x.현장대기초,
                중단사유Code = x.중단사유Code ?? string.Empty,
                책임Code = x.책임Code ?? string.Empty,
                조리지연재배차여부 = x.조리지연재배차여부,
                재조리요청StableId = x.재조리요청StableId ?? string.Empty,
                재조리요청시각Utc = x.재조리요청시각Utc,
                유산추정여부 = x.유산추정여부,
                악용확정여부 = x.악용확정여부,
                검토사유 = x.검토사유 ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var driverSettlement = await db.음식주문기사정산
            .AsNoTracking().Include(x => x.지급검증목록)
            .SingleOrDefaultAsync(x => x.주문번호 == normalizedOrderNo, cancellationToken);
        var normalizedOrderStatus = 음식주문상태코드.Normalize(order.상태);
        var latestDeliveryAttempt = deliveryAttempts.FirstOrDefault();
        var delayEvaluation = 음식배달운영지연판정Policy.판정(
            new 음식배달운영지연판정입력(
                normalizedOrderStatus,
                now,
                order.조리예상완료시각Utc,
                FindTransitionAt(order, 음식주문상태코드.픽업대기),
                latestDeliveryAttempt?.픽업완료시각Utc,
                latestDeliveryAttempt?.전달완료시각Utc));
        var recommendationExpired = IsRecommendationExpired(queue, now);
        var dispatchLedgerMissing = IsDispatchLedgerMissing(
            normalizedOrderStatus,
            queue);
        var dispatchLinkMismatch = HasDispatchLinkMismatch(
            order.배차대기Id,
            queue);
        var warnings = BuildWarnings(
            normalizedOrderStatus,
            order.배차대기Id,
            queue,
            ledgerOutboxes,
            recommendationOutboxes,
            delayEvaluation,
            now);
        var recoveryGuides = BuildRecoveryGuides(
            normalizedOrderStatus,
            order.배차대기Id,
            queue,
            ledgerOutboxes,
            recommendationOutboxes,
            delayEvaluation);
        var outboxes = BuildOutboxes(ledgerOutboxes, recommendationOutboxes, now);
        var checkpoints = BuildCheckpoints(order, queue, outboxes);
        var latestDeliveryAttemptInterrupted = latestDeliveryAttempt?.상태Code
                                              == 음식배달시도상태Code.중단;
        var lifecycleAlignment = 음식배달운영생명주기조화Projector.판정(
            new 음식배달운영생명주기조화입력(
                normalizedOrderStatus,
                delayEvaluation.음식점준비지연상태Code,
                delayEvaluation.음식점준비초과분,
                delayEvaluation.배달진행지연상태Code,
                delayEvaluation.배달진행초과분,
                배차원장누락여부: dispatchLedgerMissing,
                배차연결불일치여부: dispatchLinkMismatch,
                기사추천만료여부: recommendationExpired,
                최근배달시도중단여부: latestDeliveryAttemptInterrupted,
                공동원장동기화확인필요여부: outboxes.Any(x =>
                    x.종류 == "음식 공동 원장 동기화" && x.운영자확인필요),
                기사알림확인필요여부: outboxes.Any(x =>
                    x.종류 == "기사 추천 알림" && x.운영자확인필요),
                기사배정확정여부: order.배차상태 == 음식주문배차상태코드.기사배정
                    && !dispatchLinkMismatch
                    && queue is not null
                    && queue.상태 == 상태값.배차대기상태.확정
                    && queue.배차큐단계 == 상태값.배차큐단계.확정
                    && !string.IsNullOrWhiteSpace(queue.확정기사Id)
                    && latestDeliveryAttempt is not null
                    && latestDeliveryAttempt.제안Id == queue.의뢰Id
                    && latestDeliveryAttempt.기사Id == queue.확정기사Id
                    && latestDeliveryAttempt.중단시각Utc is null
                    && latestDeliveryAttempt.전달완료시각Utc is null));

        return new 음식주문운영추적응답
        {
            DriverSettlement = driverSettlement is null ? null
                : Ssalddel.Application.Food.음식주문기사정산Recorder.ToDto(
                    driverSettlement, currentExecutionModeCode: executionMode?.Mode.ToString()),
            주문번호 = order.주문번호,
            음식점명 = order.음식점명,
            주문상태 = normalizedOrderStatus,
            배차상태 = order.배차상태,
            전체상태 = ResolveOverallStatus(
                normalizedOrderStatus,
                warnings,
                lifecycleAlignment),
            배차대기Id = queue?.Id ?? order.배차대기Id,
            운송번호 = queue?.운송번호 ?? string.Empty,
            운송상태 = queue?.상태 ?? string.Empty,
            원본의뢰유형 = queue?.원본의뢰유형 ?? string.Empty,
            원본의뢰Id = queue?.원본의뢰Id ?? string.Empty,
            커뮤니티원장Id = order.커뮤니티원장Id ?? string.Empty,
            커뮤니티원장상태 = order.커뮤니티원장상태 ?? string.Empty,
            추천상태 = ResolveRecommendationStatus(queue),
            추천라운드 = queue?.추천라운드 ?? 0,
            추천만료시각Utc = queue?.추천만료시각,
            추천만료됨 = recommendationExpired,
            생명주기조화 = lifecycleAlignment,
            생성시각Utc = order.CreatedAt,
            최근변경시각Utc = ResolveLastChangedAt(
                order.UpdatedAt,
                order.상태이력.Select(x => x.전이시각Utc),
                queue,
                ledgerOutboxes,
                recommendationOutboxes,
                transportEvents),
            조회시각Utc = now,
            체크포인트 = checkpoints,
            Outbox목록 = outboxes,
            운송이벤트목록 = transportEvents,
            배달시도목록 = deliveryAttempts,
            경고목록 = warnings,
            복구안내목록 = recoveryGuides
        };
    }

    private async Task<운송원장?> ResolveQueueAsync(
        long? dispatchWaitingId,
        string orderNo,
        CancellationToken cancellationToken)
    {
        if (dispatchWaitingId is > 0)
        {
            var byId = await db.운송원장
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == dispatchWaitingId.Value, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        return await db.운송원장
            .AsNoTracking()
            .Where(x => x.의뢰Id == orderNo
                        || ((x.원본의뢰유형 == 운송의뢰배차원천유형.음식점주문
                             || x.원본의뢰유형 == 운송의뢰배차원천유형.음식주문)
                            && x.원본의뢰Id == orderNo))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyList<음식주문운영Outbox응답> BuildOutboxes(
        IReadOnlyList<음식마트원장동기화Outbox> ledgerOutboxes,
        IReadOnlyList<배차추천알림Outbox> recommendationOutboxes,
        DateTime now)
    {
        var ledgerItems = ledgerOutboxes.Select(x => new 음식주문운영Outbox응답
        {
            종류 = "음식 공동 원장 동기화",
            OutboxId = x.Id,
            상태 = x.처리상태,
            시도횟수 = x.시도횟수,
            마지막시도시각Utc = x.마지막시도시각Utc,
            갱신시각Utc = x.UpdatedAtUtc,
            재시도예정 = x.처리상태 == OutboxProcessingStatuses.Pending
                         && x.시도횟수 < OutboxProcessingPolicy.MaximumAttempts,
            운영자확인필요 = x.처리상태 == OutboxProcessingStatuses.Failed
                         || IsStaleProcessing(x.처리상태, x.UpdatedAtUtc, now),
            실패요약 = string.IsNullOrWhiteSpace(x.마지막오류)
                ? string.Empty
                : "마지막 공동 원장 동기화 처리에서 오류가 기록되었습니다."
        });
        var recommendationItems = recommendationOutboxes.Select(x => new 음식주문운영Outbox응답
        {
            종류 = "기사 추천 알림",
            OutboxId = x.Id,
            상태 = x.발송상태,
            시도횟수 = x.시도횟수,
            마지막시도시각Utc = x.마지막시도시각,
            갱신시각Utc = x.UpdatedAt,
            재시도예정 = x.발송상태 == OutboxProcessingStatuses.Pending,
            운영자확인필요 = x.발송상태 == OutboxProcessingStatuses.Failed,
            실패요약 = x.발송상태 == OutboxProcessingStatuses.Failed
                ? "기사 추천 알림 발송에 실패했습니다. 앱의 서버 재조회 경로를 함께 확인해야 합니다."
                : string.Empty
        });

        return ledgerItems
            .Concat(recommendationItems)
            .OrderByDescending(x => x.갱신시각Utc)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildWarnings(
        string orderStatus,
        long? recordedDispatchWaitingId,
        운송원장? queue,
        IReadOnlyList<음식마트원장동기화Outbox> ledgerOutboxes,
        IReadOnlyList<배차추천알림Outbox> recommendationOutboxes,
        음식배달운영지연판정결과 delayEvaluation,
        DateTime now)
    {
        var warnings = new List<string>();
        var closed = orderStatus is 음식주문상태코드.거절 or 음식주문상태코드.취소;
        var dispatchExpected = orderStatus is not 음식주문상태코드.주문대기
            and not 음식주문상태코드.거절
            and not 음식주문상태코드.취소;

        if (dispatchExpected && queue is null)
        {
            warnings.Add("음식점 이행이 시작됐지만 연결된 배차·운송 실행 원장을 찾지 못했습니다.");
        }

        if (recordedDispatchWaitingId is > 0
            && queue is not null
            && queue.Id != recordedDispatchWaitingId.Value)
        {
            warnings.Add("음식 주문에 기록된 배차대기 ID와 주문번호로 복구한 운송 실행 원장이 다릅니다.");
        }

        if (queue is not null
            && (!string.Equals(queue.의뢰Id, queue.원본의뢰Id, StringComparison.Ordinal)
                || !운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형)))
        {
            warnings.Add("운송 실행 원장의 음식 주문 상관관계가 표준 연결 규칙과 다릅니다.");
        }

        if (delayEvaluation.음식점준비지연상태Code != 음식배달운영지연상태Codes.없음)
        {
            warnings.Add($"음식점 준비가 예상 완료 시각을 {delayEvaluation.음식점준비초과분}분 초과했습니다.");
        }

        if (delayEvaluation.배달진행지연상태Code != 음식배달운영지연상태Codes.없음)
        {
            warnings.Add($"픽업 이후 배달 진행이 기준 시간을 {delayEvaluation.배달진행초과분}분 초과했습니다.");
        }

        if (ledgerOutboxes.Count == 0)
        {
            warnings.Add("음식 공동 원장 동기화 Outbox 기록을 찾지 못했습니다.");
        }
        else if (ledgerOutboxes.Any(x => x.처리상태 == OutboxProcessingStatuses.Failed))
        {
            warnings.Add("음식 공동 원장 동기화가 최대 재시도 뒤 실패한 항목이 있습니다.");
        }
        else if (ledgerOutboxes.Any(x => IsStaleProcessing(x.처리상태, x.UpdatedAtUtc, now)))
        {
            warnings.Add("음식 공동 원장 동기화 처리 lease가 만료된 항목이 있습니다.");
        }

        if (!closed
            && queue is not null
            && queue.추천라운드 > 0
            && recommendationOutboxes.Count == 0)
        {
            warnings.Add("추천 라운드는 시작됐지만 기사 추천 알림 Outbox 기록을 찾지 못했습니다.");
        }
        else if (recommendationOutboxes.Any(x => x.발송상태 == OutboxProcessingStatuses.Failed))
        {
            warnings.Add("기사 추천 알림 발송 실패 항목이 있습니다.");
        }

        return warnings;
    }

    private static IReadOnlyList<string> BuildRecoveryGuides(
        string orderStatus,
        long? recordedDispatchWaitingId,
        운송원장? queue,
        IReadOnlyList<음식마트원장동기화Outbox> ledgerOutboxes,
        IReadOnlyList<배차추천알림Outbox> recommendationOutboxes,
        음식배달운영지연판정결과 delayEvaluation)
    {
        var guides = new List<string>();
        var dispatchExpected = orderStatus is not 음식주문상태코드.주문대기
            and not 음식주문상태코드.거절
            and not 음식주문상태코드.취소;

        if (dispatchExpected && queue is null)
        {
            guides.Add("음식점 수락 Event와 배차대기 생성 로그를 확인한 뒤 같은 주문번호로 배차 생성 멱등 요청을 재처리합니다.");
        }

        if (recordedDispatchWaitingId is > 0
            && queue is not null
            && queue.Id != recordedDispatchWaitingId.Value)
        {
            guides.Add("원장을 직접 덮어쓰지 말고 음식 주문의 배차 연결 Event를 재처리해 stable ID를 다시 투영합니다.");
        }

        if (delayEvaluation.음식점준비지연상태Code
            == 음식배달운영지연상태Codes.운영자확인필요)
        {
            guides.Add("음식점의 현재 준비 상태와 변경된 준비 예정시각을 확인하고 필요한 경우 주문자에게 지연 사실을 안내합니다.");
        }

        if (delayEvaluation.배달진행지연상태Code
            == 음식배달운영지연상태Codes.운영자확인필요)
        {
            guides.Add("기사의 마지막 진행 상태와 안전 문제 여부를 확인하고, 필요한 경우 기사 또는 주문자에게 연락합니다.");
        }

        if (ledgerOutboxes.Any(x => x.처리상태 == OutboxProcessingStatuses.Failed))
        {
            guides.Add("실패 원인을 해소한 뒤 관리자 사유와 함께 공동 원장 Outbox를 재처리해야 합니다.");
        }

        if (recommendationOutboxes.Any(x => x.발송상태 == OutboxProcessingStatuses.Failed))
        {
            guides.Add("기사 push token과 FCM 설정을 확인하되, 기사 앱의 30초 서버 재조회로 추천이 복구되는지도 확인합니다.");
        }

        return guides;
    }

    private static IReadOnlyList<음식주문운영체크포인트응답> BuildCheckpoints(
        살뜰.도메인.음식.음식주문 order,
        운송원장? queue,
        IReadOnlyList<음식주문운영Outbox응답> outboxes)
    {
        var status = 음식주문상태코드.Normalize(order.상태);
        var closed = status is 음식주문상태코드.거절 or 음식주문상태코드.취소;
        var restaurantCompleted = status is not 음식주문상태코드.주문대기;
        var driverAssigned = status is 음식주문상태코드.기사배정
            or 음식주문상태코드.픽업완료
            or 음식주문상태코드.전달완료
            or 음식주문상태코드.수령확인;
        var delivered = status is 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인;
        var ledgerFailed = outboxes.Any(x => x.종류 == "음식 공동 원장 동기화" && x.운영자확인필요);
        var ledgerPending = outboxes.Any(x => x.종류 == "음식 공동 원장 동기화" && x.재시도예정);

        return
        [
            Checkpoint(
                "food-order",
                "주문 등록",
                음식주문운영추적상태코드.완료,
                "RDB 음식 주문 원장이 존재합니다.",
                order.CreatedAt),
            Checkpoint(
                "restaurant",
                "음식점 이행",
                closed
                    ? 음식주문운영추적상태코드.종료
                    : restaurantCompleted
                        ? 음식주문운영추적상태코드.완료
                        : 음식주문운영추적상태코드.진행중,
                closed
                    ? $"주문이 {status} 상태로 종료됐습니다."
                    : restaurantCompleted
                        ? $"음식점 처리 상태는 {status}입니다."
                        : "음식점의 주문 확인을 기다립니다.",
                order.음식점수락시각Utc),
            Checkpoint(
                "dispatch",
                "배차·추천",
                closed
                    ? 음식주문운영추적상태코드.해당없음
                    : queue is null && restaurantCompleted
                        ? 음식주문운영추적상태코드.복구필요
                        : driverAssigned
                            ? 음식주문운영추적상태코드.완료
                                : queue is null
                                    ? 음식주문운영추적상태코드.미시작
                                    : 음식주문운영추적상태코드.진행중,
                closed
                    ? "종료 주문에는 새 배차를 만들지 않습니다."
                    : queue is null
                        ? "연결된 배차·운송 실행 원장이 없습니다."
                        : $"현재 추천 상태는 {ResolveRecommendationStatus(queue)}입니다.",
                queue?.UpdatedAt),
            Checkpoint(
                "delivery",
                "기사 전달",
                closed
                    ? 음식주문운영추적상태코드.해당없음
                    : delivered
                        ? 음식주문운영추적상태코드.완료
                        : driverAssigned
                            ? 음식주문운영추적상태코드.진행중
                            : 음식주문운영추적상태코드.미시작,
                delivered ? "기사가 전달 완료를 기록했습니다." : "기사 전달 완료 전입니다.",
                FindTransitionAt(order, 음식주문상태코드.전달완료)),
            Checkpoint(
                "receipt",
                "주문자 수령 확인",
                closed
                    ? 음식주문운영추적상태코드.해당없음
                    : status == 음식주문상태코드.수령확인
                        ? 음식주문운영추적상태코드.완료
                        : delivered
                            ? 음식주문운영추적상태코드.주의
                            : 음식주문운영추적상태코드.미시작,
                status == 음식주문상태코드.수령확인
                    ? "주문자 수령 확인까지 완료됐습니다."
                    : delivered
                        ? "기사 전달은 끝났고 주문자의 실제 수령 확인을 기다립니다."
                        : "기사 전달 완료 뒤 주문자가 확인할 수 있습니다.",
                FindTransitionAt(order, 음식주문상태코드.수령확인)),
            Checkpoint(
                "ledger-sync",
                "공동 원장 동기화",
                ledgerFailed
                    ? 음식주문운영추적상태코드.복구필요
                    : ledgerPending
                        ? 음식주문운영추적상태코드.진행중
                        : outboxes.Any(x => x.종류 == "음식 공동 원장 동기화")
                            ? 음식주문운영추적상태코드.완료
                            : 음식주문운영추적상태코드.주의,
                ledgerFailed
                    ? "운영자 확인이 필요한 원장 동기화 항목이 있습니다."
                    : ledgerPending
                        ? "원장 동기화 재시도를 기다립니다."
                        : "원장 동기화 처리 상태를 확인했습니다.",
                outboxes
                    .Where(x => x.종류 == "음식 공동 원장 동기화")
                    .Select(x => (DateTime?)x.갱신시각Utc)
                    .FirstOrDefault())
        ];
    }

    private static 음식주문운영체크포인트응답 Checkpoint(
        string key,
        string name,
        string status,
        string description,
        DateTime? changedAt)
        => new()
        {
            단계Key = key,
            단계명 = name,
            상태 = status,
            설명 = description,
            변경시각Utc = changedAt
        };

    private static DateTime? FindTransitionAt(
        살뜰.도메인.음식.음식주문 order,
        string nextStatus)
        => order.상태이력
            .Where(x => x.다음상태 == nextStatus)
            .OrderByDescending(x => x.전이시각Utc)
            .Select(x => (DateTime?)x.전이시각Utc)
            .FirstOrDefault();

    private static string ResolveOverallStatus(
        string orderStatus,
        IReadOnlyList<string> warnings,
        음식배달운영생명주기조화응답 lifecycleAlignment)
    {
        if (orderStatus is 음식주문상태코드.거절 or 음식주문상태코드.취소)
        {
            return 음식주문운영추적상태코드.종료;
        }

        if (lifecycleAlignment.운영자확인필요여부)
        {
            return 음식주문운영추적상태코드.복구필요;
        }

        if (warnings.Count > 0)
        {
            return 음식주문운영추적상태코드.주의;
        }

        return orderStatus == 음식주문상태코드.수령확인
            ? 음식주문운영추적상태코드.완료
            : 음식주문운영추적상태코드.진행중;
    }

    private static string ResolveRecommendationStatus(운송원장? queue)
        => queue?.배차노출상태 switch
        {
            null => 음식주문운영추적상태코드.미시작,
            상태값.배차노출상태.계획대기 => "계획대기",
            상태값.배차노출상태.계획시도중 => "계획시도중",
            상태값.배차노출상태.계획실패 => "계획실패",
            상태값.배차노출상태.추천대기 => "추천대기",
            상태값.배차노출상태.추천중 => "추천중",
            상태값.배차노출상태.추천만료 => "추천만료",
            상태값.배차노출상태.추천거절 => "추천거절",
            상태값.배차노출상태.추천후보없음 => "추천후보없음",
            상태값.배차노출상태.공개대기 => "공개대기",
            상태값.배차노출상태.공개중 => "공개중",
            상태값.배차노출상태.확정 => "기사확정",
            상태값.배차노출상태.종료 => "종료",
            _ => "알수없음"
        };

    private static bool IsRecommendationExpired(운송원장? queue, DateTime now)
        => queue is not null
           && (queue.배차노출상태 == 상태값.배차노출상태.추천만료
               || (queue.배차큐단계 == 상태값.배차큐단계.배차추천
                   && queue.배차노출상태 == 상태값.배차노출상태.추천중
                   && queue.추천만료시각.HasValue
                   && queue.추천만료시각.Value <= now));

    private static bool IsDispatchLedgerMissing(
        string orderStatus,
        운송원장? queue)
    {
        var dispatchExpected = orderStatus is not 음식주문상태코드.주문대기
            and not 음식주문상태코드.거절
            and not 음식주문상태코드.취소;
        return dispatchExpected && queue is null;
    }

    private static bool HasDispatchLinkMismatch(
        long? recordedDispatchWaitingId,
        운송원장? queue)
        => queue is not null
           && ((recordedDispatchWaitingId is > 0
                && queue.Id != recordedDispatchWaitingId.Value)
               || !string.Equals(queue.의뢰Id, queue.원본의뢰Id, StringComparison.Ordinal)
               || !운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형));

    private static bool IsStaleProcessing(string status, DateTime updatedAtUtc, DateTime now)
        => status == OutboxProcessingStatuses.Processing
           && updatedAtUtc <= now - OutboxProcessingPolicy.LeaseTimeout;

    private static DateTime ResolveLastChangedAt(
        DateTime orderUpdatedAt,
        IEnumerable<DateTime> orderTransitionTimes,
        운송원장? queue,
        IEnumerable<음식마트원장동기화Outbox> ledgerOutboxes,
        IEnumerable<배차추천알림Outbox> recommendationOutboxes,
        IEnumerable<음식주문운영이벤트응답> transportEvents)
    {
        var values = new List<DateTime> { orderUpdatedAt };
        values.AddRange(orderTransitionTimes);
        if (queue is not null)
        {
            values.Add(queue.UpdatedAt);
        }

        values.AddRange(ledgerOutboxes.Select(x => x.UpdatedAtUtc));
        values.AddRange(recommendationOutboxes.Select(x => x.UpdatedAt));
        values.AddRange(transportEvents.Select(x => x.이벤트시각Utc));
        return values.Max();
    }
}
