using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Community;

/// <summary>현재 양측 동의와 취소 원천을 검증하고 자식 운송부터 상위 음식 원장 순으로 투영합니다.</summary>
internal sealed class 음식합의취소투영PayloadHandler(
    SsalddelContext db,
    I음식마트원장Mongo동기화Service ledgerSync,
    ISsalddelFoodOrderStore? foodOrderStore,
    I운송원장Mongo동기화Service? transportLedgerSync)
{
    public async Task 처리Async(음식마트원장동기화Outbox item,
        음식합의취소투영의도 intent, CancellationToken cancellationToken)
    {
        if (intent.OrderNo != item.원천Id || !Guid.TryParseExact(intent.AgreementId, "N", out _)
            || item.멱등키 != "food-agreed-cancellation:" + intent.AgreementId)
            throw new InvalidOperationException("FoodAgreedCancellationSourceMismatch");
        var agreement = await db.Set<음식주문취소합의>().AsNoTracking().SingleOrDefaultAsync(
            x => x.합의StableId == intent.AgreementId && x.주문번호 == intent.OrderNo, cancellationToken);
        var order = foodOrderStore?.GetOrder(item.원천Id)
            ?? throw new InvalidOperationException("FoodOutboxSourceUnavailable");
        if (agreement?.취소반영시각Utc is null || !agreement.주문자동의 || !agreement.음식점동의
            || order.상태 != 음식주문상태코드.취소 || order.배차대기Id != intent.QueueId)
            throw new InvalidOperationException("FoodAgreedCancellationCanonicalStateMismatch");
        if (intent.QueueId is { } queueId)
        {
            // 과거 payload의 상태를 발행하지 않는다. 현재 SQL 취소 큐와 실제 원주문 출처를 다시 결속한다.
            var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == queueId, cancellationToken);
            if (queue is null || queue.원본의뢰Id != intent.OrderNo
                || !운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형)
                || queue.배차업무유형 != 상태값.배차업무유형.음식배달
                || queue.상태 != 상태값.배차상태.취소 || queue.배차큐단계 != 상태값.배차큐단계.종료
                || !string.IsNullOrWhiteSpace(queue.확정기사Id))
                throw new InvalidOperationException("FoodAgreedCancellationTransportMismatch");
            if (transportLedgerSync is null || await transportLedgerSync.운송실행투영동기화Async(queue, item.변경자, cancellationToken) is null)
                throw new InvalidOperationException("FoodAgreedCancellationTransportUnavailable");
        }
        // 자식 배송 투영부터 성공시킨 뒤 음식 원장의 포함 관계와 취소 상태를 발행한다.
        // 중간 실패는 같은 Outbox에서 현재 SQL 상태·Mongo CAS를 다시 읽어 재처리한다.
        if (await ledgerSync.음식주문동기화Async(order, item.변경자, cancellationToken) is null)
            throw new InvalidOperationException("FoodAgreedCancellationLedgerUnavailable");
    }
}
