using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Fulfillment;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Food;

/// <summary>공통 수행의 원천은 음식 주문입니다. 외부 모의 배정은 자체 기사/큐를 만들지 않습니다.</summary>
public sealed class 음식수행원천Adapter(SsalddelContext db, ISsalddelFoodOrderStore store,
    음식배차원천Builder builder, I음식배달기사제안요금Service pricing, TimeProvider clock) : I수행원천Adapter
{
    public string SourceKind => FulfillmentSourceKinds.FoodOrder;

    public async Task<FulfillmentSourceSnapshot?> 읽기Async(FulfillmentSourceKey key, CancellationToken ct)
    {
        if (key.SourceKind != SourceKind) return null;
        var order = await db.음식주문.AsNoTracking().Include(x => x.상품목록).Include(x => x.상태이력)
            .SingleOrDefaultAsync(x => x.주문번호 == key.SourceId, ct);
        if (order is null || !order.모의처리처수행) return null;
        var profile = await db.음식점공개프로필.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.음식점Id, ct);
        var preparation = 음식주문현재조리Policy.계산(order);
        var everCooked = order.상태이력.Any(x => x.사유 == "음식점 조리 시작" || x.사유 == 음식주문현재조리Policy.재조리요청이력사유);
        var closed = order.상태 is 음식주문상태코드.취소 or 음식주문상태코드.거절 or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인;
        var ready = order.음식점수락시각Utc.HasValue && !closed;
        decimal? distance = null, cost = null, travelMinutes = null;
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == key.SourceId, ct);
        var active = current is not null && current.StateCode is AutoFulfillmentStates.Submitting or AutoFulfillmentStates.ResultUnknown
            or AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.InTransit or AutoFulfillmentStates.CancelPending;
        if (ready && !everCooked && !active)
        {
            var dto = store.GetOrder(order.주문번호)!;
            if (string.IsNullOrWhiteSpace(dto.음식점주소) && profile is not null)
            { dto.음식점주소 = profile.공개주소; dto.음식점위도 = profile.위도; dto.음식점경도 = profile.경도; }
            try
            {
                var quote = await pricing.수행조건산정Async(await builder.조건Async(dto, ct), "Motorcycle", ct);
                if (quote.경로차량Code == "Motorcycle" && quote.경로옵션Code == "traavoidcaronly"
                    && quote.산정거리Km is >= 0 && quote.경로소요시간초 is >= 0)
                { distance = quote.산정거리Km; cost = quote.요금.기사지급예정액; travelMinutes = decimal.Ceiling((decimal)quote.경로소요시간초.Value / 60m); }
            }
            catch (Exception) when (!ct.IsCancellationRequested) { /* 불명확한 경로를 가용 후보로 만들지 않습니다. */ }
        }
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            order.음식점Id, Address = string.IsNullOrWhiteSpace(order.음식점주소) ? profile?.공개주소 : order.음식점주소,
            order.수령인명, order.수령인연락처, order.수령지주소, order.수령지상세주소, order.수령요청사항,
            Lines = order.상품목록.OrderBy(x => x.메뉴Id).ThenBy(x => x.상품명).Select(x => new { x.메뉴Id, x.수량, x.단가 })
        }))));
        return new(key, order.주문자UserId, order.상태이력.Count, digest, closed, ready, everCooked,
            !closed && !everCooked && order.상태 != 음식주문상태코드.픽업완료,
            distance, cost, clock.GetUtcNow().UtcDateTime, preparation.ReadyAtUtc.HasValue ? 0 : order.적용조리분 ?? 0,
            WeightKg: null, Quantity: order.상품목록.Sum(x => x.수량), RestaurantId: order.음식점Id, Leg: 음식수행권위Policy.Leg(order), TravelMinutes: travelMinutes);
    }

    public async Task<FulfillmentExecutionResult> 내부접수Async(FulfillmentAttemptContext context, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var order = await SourceAsync(context, ct);
        if (order is null || !음식수행권위Policy.내부허용(db, order)) return new(AutoFulfillmentStates.ResultUnknown);
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == context.Key.SourceId, ct);
        var attempt = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.AttemptId == context.AttemptId, ct);
        var consent = await db.AutoFulfillmentConsents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == context.Key.SourceId, ct);
        var quote = attempt is null ? null : await db.NeighborhoodFulfillmentQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.QuoteId == attempt.QuoteId, ct);
        var source = await 읽기Async(context.Key, ct);
        if (current?.AttemptId != context.AttemptId || current.Revision != context.Revision || attempt is null || attempt.ProviderId != "internal"
            || consent is null || consent.Revoked || consent.Revision != context.ConsentRevision || source is null || source.IsClosed || source.HasCookingStarted
            || !source.ReadyForRouting || source.InputDigest != consent.InputDigest || quote?.InputDigest != source.InputDigest
            || quote.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || source.SourceRevision != attempt.SourceRevision || source.Leg != context.Leg)
            return new(AutoFulfillmentStates.Failed, context.AttemptId, 0);
        if (order.배차대기Id is null)
        {
            var previousQueue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == order.주문번호, ct);
            if (previousQueue is not null)
            {
                // 새 동의 판본의 현재 내부 시도만 확인된 미배정 취소 큐를 재개합니다.
                // 큐 고유 ID/과거 음식 시도·비용 이력은 유지하며 활성/결과 불명 큐는 재사용하지 않습니다.
                var confirmedPreviousCancellation = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().AnyAsync(x =>
                    x.SourceKind == SourceKind && x.RequestId == order.주문번호 && x.AttemptId != context.AttemptId
                    && x.ProviderId == "internal" && x.StateCode == AutoFulfillmentStates.Cancelled
                    && x.ConfirmedIncurredCostKrw != null, ct);
                if (!confirmedPreviousCancellation || previousQueue.원본의뢰Id != order.주문번호
                    || previousQueue.배차업무유형 != 상태값.배차업무유형.음식배달
                    || previousQueue.상태 != 상태값.배차상태.취소 || !string.IsNullOrWhiteSpace(previousQueue.확정기사Id)
                    || !string.IsNullOrWhiteSpace(previousQueue.기사_운송자))
                    return new(AutoFulfillmentStates.ResultUnknown, context.AttemptId);
                previousQueue.상태 = 상태값.배차대기상태.대기;
                previousQueue.배차큐단계 = 상태값.배차큐단계.계획배차;
                previousQueue.배차노출상태 = 상태값.배차노출상태.계획대기;
                previousQueue.현재추천대상기사Id = null; previousQueue.추천시작시각 = null; previousQueue.추천만료시각 = null;
                previousQueue.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            }
            var queue = await builder.생성Async(store.GetOrder(order.주문번호)!, ct);
            store.배차대기반영(order.주문번호, queue.Id, clock.GetUtcNow().UtcDateTime);
        }
        var result = await 내부조회Async(context, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return result;
    }

    public async Task<FulfillmentExecutionResult> 내부조회Async(FulfillmentAttemptContext context, CancellationToken ct)
    {
        var order = await SourceAsync(context, ct);
        if (order is null) return new(AutoFulfillmentStates.ResultUnknown);
        if (order.상태 is 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인) return new(AutoFulfillmentStates.Completed, context.AttemptId, context.QuotedCostKrw);
        if (order.상태 == 음식주문상태코드.픽업완료) return new(AutoFulfillmentStates.InTransit, context.AttemptId);
        if (order.배차대기Id is not { } id) return new(AutoFulfillmentStates.ResultUnknown);
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.Id == id && x.원본의뢰Id == order.주문번호, ct);
        if (queue is null) return new(AutoFulfillmentStates.ResultUnknown);
        if (queue.상태 == 상태값.배차상태.취소) return new(AutoFulfillmentStates.Cancelled, context.AttemptId, 0);
        return new(string.IsNullOrWhiteSpace(queue.확정기사Id) ? AutoFulfillmentStates.Accepted : AutoFulfillmentStates.DriverAssigned, context.AttemptId);
    }

    public async Task<FulfillmentExecutionResult> 내부취소Async(FulfillmentAttemptContext context, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == context.Key.SourceId, ct);
        if (current?.AttemptId != context.AttemptId || current.Revision != context.Revision)
            return new(AutoFulfillmentStates.ResultUnknown);
        var order = await SourceAsync(context, ct);
        if (order is null) return new(AutoFulfillmentStates.ResultUnknown);
        if (order.상태이력.Any(x => x.사유 == "음식점 조리 시작") || order.상태 == 음식주문상태코드.픽업완료)
            return new(AutoFulfillmentStates.CancelPending, context.AttemptId);
        if (order.배차대기Id is { } id)
        {
            var queue = await db.운송원장.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (queue is null || !string.IsNullOrWhiteSpace(queue.확정기사Id)) return new(AutoFulfillmentStates.CancelPending, context.AttemptId);
            queue.상태 = 상태값.배차상태.취소; queue.현재추천대상기사Id = null;
            queue.추천만료시각 = null; queue.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            order.배차대기Id = null;
            order.배차상태 = 음식주문배차상태코드.미요청;
            await db.SaveChangesAsync(ct);
        }
        if (tx is not null) await tx.CommitAsync(ct);
        return new(AutoFulfillmentStates.Cancelled, context.AttemptId, 0);
    }

    public async Task<Result> 외부증거반영Async(FulfillmentExecutionEvidence evidence, CancellationToken ct)
    {
        var context = evidence.Attempt;
        var order = await SourceAsync(context, ct);
        if (order is null || context.ProviderId == "internal" || order.배차대기Id.HasValue)
            return Fail("FoodFulfillmentEvidenceMismatch", "현재 외부 음식 수행 원천을 확인해 주세요.");
        if (context.Leg != 음식수행권위Policy.Leg(order) || order.상태 is 음식주문상태코드.취소 or 음식주문상태코드.거절 or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인)
            return Fail("FoodFulfillmentSourceClosed", "종료된 주문이나 이전 조리차수의 증거는 적용할 수 없습니다.");
        var current = await db.NeighborhoodFulfillments.SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == order.주문번호, ct);
        var attempt = await db.NeighborhoodFulfillmentAttempts.SingleOrDefaultAsync(x => x.AttemptId == context.AttemptId, ct);
        var job = await db.FulfillmentMockJobs.SingleOrDefaultAsync(x => x.AttemptId == context.AttemptId, ct);
        if (current?.AttemptId != context.AttemptId || current.Revision != context.Revision || attempt?.ProviderId != context.ProviderId
            || job is null || job.SourceKind != SourceKind || job.RequestId != order.주문번호 || job.ProviderId != context.ProviderId || job.Leg != context.Leg
            || string.IsNullOrWhiteSpace(evidence.EvidenceId)) return Fail("FoodFulfillmentEvidenceStale", "이전 수행의 증거는 현재 주문을 변경할 수 없습니다.");
        var preparation = 음식주문현재조리Policy.계산(order);
        var previous = order.상태; var next = previous;
        var reason = $"모의 외부 수행 · {evidence.EvidenceKind} · {context.AttemptId} · {evidence.EvidenceId}";
        switch (evidence.EvidenceKind)
        {
            case FulfillmentEvidenceKinds.DriverAssigned:
                if (job.StateCode != AutoFulfillmentStates.Accepted || string.IsNullOrWhiteSpace(evidence.ExternalDriverReference)) return Fail("FoodAssignmentEvidenceRequired", "접수 뒤 현재 기사 배정 증거가 필요합니다.");
                // 같은 현재 업체/시도 안의 재배정은 새 업체 접수가 아닙니다. 조리 이력을 유지합니다.
                next = preparation.CookingStartedAtUtc.HasValue || preparation.ReadyAtUtc.HasValue ? previous : 음식주문상태코드.기사배정;
                order.배차상태 = 음식주문배차상태코드.기사배정;
                reason = 음식수행권위Policy.AssignmentMarker(context.AttemptId, context.Leg, job.AssignmentRevision + 1);
                break;
            case FulfillmentEvidenceKinds.AssignmentReleased:
            case FulfillmentEvidenceKinds.Rejected:
            case FulfillmentEvidenceKinds.Cancelled:
                if (job.StateCode is AutoFulfillmentStates.InTransit or AutoFulfillmentStates.Completed) return Fail("FoodPickedUpRoutingLocked", "픽업 뒤의 수행을 취소 증거로 되돌릴 수 없습니다.");
                if (!preparation.CookingStartedAtUtc.HasValue && !preparation.ReadyAtUtc.HasValue) next = 음식주문상태코드.주문확인;
                order.배차상태 = 음식주문배차상태코드.미요청;
                break;
            case FulfillmentEvidenceKinds.PickedUp:
                if (job.StateCode != AutoFulfillmentStates.DriverAssigned || preparation.ReadyAtUtc is null
                    || !음식수행권위Policy.외부배정유효(db, order)) return Fail("FoodPickupEvidenceRequired", "현재 기사 배정과 음식 준비 완료 뒤에만 픽업할 수 있습니다.");
                next = 음식주문상태코드.픽업완료; order.배차상태 = 음식주문배차상태코드.배달중;
                break;
            case FulfillmentEvidenceKinds.Delivered:
                if (job.StateCode != AutoFulfillmentStates.InTransit || previous != 음식주문상태코드.픽업완료) return Fail("FoodDeliveryEvidenceRequired", "현재 픽업 증거 뒤에만 전달을 완료할 수 있습니다.");
                next = 음식주문상태코드.전달완료; order.배차상태 = 음식주문배차상태코드.배달완료;
                break;
            default: return Fail("FoodFulfillmentEvidenceUnsupported", "지원하지 않는 음식 수행 증거입니다.");
        }
        order.상태 = next; order.UpdatedAt = evidence.RecordedAtUtc;
        order.상태이력.Add(new 음식주문상태이력 { 이전상태 = previous, 다음상태 = next, 사유 = reason,
            처리UserId = "fulfillment-simulation", 전이시각Utc = evidence.RecordedAtUtc });
        // Coordinator가 job/current/attempt와 함께 Save+commit합니다.
        return Result.Ok();
    }

    private Task<음식주문?> SourceAsync(FulfillmentAttemptContext context, CancellationToken ct)
        => db.음식주문.Include(x => x.상태이력).Include(x => x.상품목록).SingleOrDefaultAsync(x => x.주문번호 == context.Key.SourceId
            && x.모의처리처수행 && context.Key.SourceKind == FulfillmentSourceKinds.FoodOrder, ct);
    private static Result Fail(string code, string message) => Result.Fail(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", 409));
}
