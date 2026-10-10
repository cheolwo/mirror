using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Mart;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.배차;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Operations;

public sealed partial class 마트기사업무Service
{
    public Task<Result<마트기사업무항목Dto>> 수락Async(string 기사Id, string offerId, 마트기사수락요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RespondAsync(기사Id, offerId, request.ClientRequestId, "Accept", InputDigest("Accept", offerId, request),
            request.ExpectedRecommendationRound, request.ExpectedQueueUpdatedAtUtc, true, cancellationToken);
    }

    public Task<Result<마트기사업무항목Dto>> 거절Async(string 기사Id, string offerId, 마트기사거절요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((request.ReasonCode?.Length ?? 0) > 80) return Task.FromResult(Fail<마트기사업무항목Dto>("MartRejectReasonInvalid", 400));
        return RespondAsync(기사Id, offerId, request.ClientRequestId, "Reject", InputDigest("Reject", offerId, request),
            request.ExpectedRecommendationRound, request.ExpectedQueueUpdatedAtUtc, false, cancellationToken);
    }

    private Task<Result<마트기사업무항목Dto>> RespondAsync(string driverId, string offerId, Guid requestId,
        string kind, string digest, int round, DateTime updatedAt, bool accept, CancellationToken ct)
        => TransactionAsync(async () =>
        {
            if (!IsDriver(driverId)) return Fail<마트기사업무항목Dto>("MartDriverIdentityRequired", 403);
            var receipt = await ReceiptAsync(requestId, ct);
            if (receipt is not null) return SameReceipt(receipt, kind, offerId, digest)
                ? await ReceiptResultAsync(receipt, ct) : Fail<마트기사업무항목Dto>("MartDriverRequestConflict");
            var queue = await QueueAsync(offerId, ct);
            if (queue is null) return Fail<마트기사업무항목Dto>("MartDriverWorkNotFound", 404);
            if (round < 1 || queue.추천라운드 != round || Utc(queue.UpdatedAt) != Utc(updatedAt)
                || !배차응답가능정책.추천수락가능(queue, driverId, Now))
                return Fail<마트기사업무항목Dto>("MartDriverRecommendationChanged");
            if (await db.Set<마트배송시도>().AnyAsync(x => x.배차의뢰Id == offerId, ct))
                return Fail<마트기사업무항목Dto>("MartDriverAttemptExists");

            마트배송시도? attempt = null;
            if (accept)
            {
                var receivingState = await receiving.수신상태조회Async(driverId, ct);
                if (receivingState.주체Id != driverId || !운영배차수신상태Policy.신규배차수신허용(receivingState))
                    return Fail<마트기사업무항목Dto>("MartDriverReceivingOff");
                if (await Ssalddel.Services.Community.NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(db, driverId, ct))
                    return Fail<마트기사업무항목Dto>("MemberWorkInProgress");
                var active = await db.운송원장.CountAsync(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                    && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료
                    && (x.확정기사Id == driverId || x.기사_운송자 == driverId), ct);
                if (!음식배달기사활성업무Policy.수락가능한가(active, 1))
                    return Fail<마트기사업무항목Dto>("MartDriverActiveWorkLimitExceeded");
                var preparation = await source.조회Async(queue.원본의뢰Id, ct);
                if (!preparation.원천결속됨 || !preparation.픽업준비완료)
                    return Fail<마트기사업무항목Dto>("MartDriverPreparationNotReady");
            }
            if (!await TouchQueueAsync(queue, ct)) return Fail<마트기사업무항목Dto>("MartDriverConcurrentChange");
            queue.현재추천대상기사Id = null; queue.추천시작시각 = null; queue.추천만료시각 = null;
            if (accept)
            {
                queue.확정기사Id = driverId; queue.기사_운송자 = driverId;
                queue.상태 = 상태값.배차대기상태.확정; queue.배차큐단계 = 상태값.배차큐단계.확정;
                queue.배차노출상태 = 상태값.배차노출상태.확정;
                attempt = new 마트배송시도 { 시도StableId = "mart-attempt:" + Guid.NewGuid().ToString("N"),
                    배차의뢰Id = offerId, 주문참조번호 = queue.원본의뢰Id, 기사Id = driverId, 추천라운드 = round,
                    Revision = 1, 수락시각Utc = Now, CreatedAtUtc = Now, UpdatedAtUtc = Now };
                db.Set<마트배송시도>().Add(attempt);
                await UpdateOrderAsync(queue, "기사 배정", "픽업대기", ct);
                Followups(queue, attempt);
            }
            else
            {
                queue.마지막거절기사Id = driverId; queue.배차노출상태 = 상태값.배차노출상태.추천대기;
                Followups(queue, null, "AdvanceRecommendation");
                Followups(queue, null, "TransportLedger");
            }
            AddReceipt(requestId, kind, offerId, queue.원본의뢰Id, digest);
            return Result.Ok(accept ? Item(queue, attempt, null, driverId) : new 마트기사업무항목Dto
                { OfferId = offerId, OrderReference = queue.원본의뢰Id, StateCode = "Rejected", QueueUpdatedAtUtc = Utc(queue.UpdatedAt) });
        }, requestId, ct);

    public Task<Result<마트기사업무항목Dto>> 전달Async(string 기사Id, string offerId, 마트기사전달요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = InputDigest("Deliver", offerId, request);
        return TransactionAsync(async () =>
        {
            if (!IsDriver(기사Id)) return Fail<마트기사업무항목Dto>("MartDriverIdentityRequired", 403);
            var receipt = await ReceiptAsync(request.ClientRequestId, cancellationToken);
            if (receipt is not null) return SameReceipt(receipt, "Deliver", offerId, digest)
                ? await ReceiptResultAsync(receipt, cancellationToken) : Fail<마트기사업무항목Dto>("MartDriverRequestConflict");
            var queue = await QueueAsync(offerId, cancellationToken);
            var attempt = await db.Set<마트배송시도>().SingleOrDefaultAsync(x => x.배차의뢰Id == offerId && x.기사Id == 기사Id, cancellationToken);
            var label = queue is null ? null : await db.Set<마트포장전표>().SingleOrDefaultAsync(x => x.주문참조번호 == queue.원본의뢰Id, cancellationToken);
            if (queue is null || queue.확정기사Id != 기사Id || attempt is null) return Fail<마트기사업무항목Dto>("MartDriverWorkNotFound", 404);
            if (attempt.Revision != request.ExpectedRevision || attempt.상태Code != "PickedUp"
                || queue.상태 != 상태값.배차상태.상차완료 || queue.배차큐단계 != 상태값.배차큐단계.확정
                || label is null || label.활성여부 || !label.픽업시각Utc.HasValue
                || attempt.픽업전표Id != label.전표Id || attempt.픽업전표Revision != label.Revision
                || !attempt.픽업시각Utc.HasValue || Utc(attempt.픽업시각Utc.Value) != Utc(label.픽업시각Utc.Value))
                return Fail<마트기사업무항목Dto>("MartDriverPickupProofRequired");
            if (!await TouchQueueAsync(queue, cancellationToken)) return Fail<마트기사업무항목Dto>("MartDriverConcurrentChange");
            attempt.상태Code = "Delivered"; attempt.Revision++; attempt.전달시각Utc = Now; attempt.UpdatedAtUtc = Now;
            // 하차는 기사 전달 사실입니다. 수령인의 인수 확인이나 정산·지급 확정으로 올리지 않습니다.
            queue.상태 = 상태값.배차상태.하차완료; queue.배차큐단계 = 상태값.배차큐단계.종료;
            queue.배차노출상태 = 상태값.배차노출상태.종료; queue.도착 = Now;
            await UpdateOrderAsync(queue, "전달 완료", "전달완료", cancellationToken);
            Followups(queue, attempt); AddReceipt(request.ClientRequestId, "Deliver", offerId, queue.원본의뢰Id, digest);
            return Result.Ok(Item(queue, attempt, label, 기사Id));
        }, request.ClientRequestId, cancellationToken);
    }
}
