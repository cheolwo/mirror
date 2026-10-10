using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Mart;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;

namespace Ssalddel.Services.Operations;

public sealed partial class 마트기사업무Service
{
    public async Task<Result<마트기사업무목록Dto>> 조회Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        if (!IsDriver(기사Id)) return Fail<마트기사업무목록Dto>("MartDriverIdentityRequired", 403);
        if (!gate.Enabled) return Result.Ok(new 마트기사업무목록Dto
        {
            Enabled = false, Notice = "살뜰마트 기사 업무가 비활성화되어 있습니다.", UpdatedAtUtc = Now
        });
        var ids = await db.운송원장.AsNoTracking()
            .Where(x => x.원본의뢰유형 == SourceType && x.배차업무유형 == 상태값.배차업무유형.음식배달
                && x.배차큐단계 != 상태값.배차큐단계.종료 && x.상태 != 상태값.배차상태.취소
                && (x.확정기사Id == 기사Id || x.현재추천대상기사Id == 기사Id))
            .OrderByDescending(x => x.UpdatedAt).Select(x => x.의뢰Id).Take(200).ToArrayAsync(cancellationToken);
        var items = new List<마트기사업무항목Dto>();
        foreach (var id in ids)
        {
            var result = await 업무조회Async(기사Id, id, cancellationToken);
            if (result.IsSuccess) items.Add(result.Value);
        }
        return Result.Ok(new 마트기사업무목록Dto { Enabled = true, Items = items, UpdatedAtUtc = Now });
    }

    public async Task<Result<마트기사업무항목Dto>> 업무조회Async(string 기사Id, string offerId, CancellationToken cancellationToken = default)
    {
        if (!gate.Enabled) return Fail<마트기사업무항목Dto>("MartDriverWorkflowDisabled", 403);
        if (!IsDriver(기사Id)) return Fail<마트기사업무항목Dto>("MartDriverIdentityRequired", 403);
        var queue = await QueueAsync(offerId, cancellationToken);
        if (queue is null || (queue.확정기사Id != 기사Id
                && !배차응답가능정책.추천수락가능(queue, 기사Id, Now)))
            return Fail<마트기사업무항목Dto>("MartDriverWorkNotFound", 404);
        var attempt = await db.Set<마트배송시도>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.배차의뢰Id == queue.의뢰Id && x.기사Id == 기사Id, cancellationToken);
        if (queue.확정기사Id == 기사Id && attempt is null) return Fail<마트기사업무항목Dto>("MartDriverAttemptNotFound", 404);
        var label = await db.Set<마트포장전표>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.주문참조번호 == queue.원본의뢰Id, cancellationToken);
        var item = Item(queue, attempt, label, 기사Id);
        if (item.CanPickup)
        {
            var preparation = await source.조회Async(queue.원본의뢰Id, cancellationToken);
            item.CanPickup = preparation.원천결속됨 && preparation.픽업준비완료;
        }
        return Result.Ok(item);
    }

    public async Task<Result<마트기사업무항목Dto>> 요청결과조회Async(Guid clientRequestId, CancellationToken cancellationToken = default)
    {
        if (!gate.Enabled) return Fail<마트기사업무항목Dto>("MartDriverWorkflowDisabled", 403);
        if (user.UserId is null || !IsDriver(user.UserId)) return Fail<마트기사업무항목Dto>("MartDriverIdentityRequired", 403);
        var receipt = await ReceiptAsync(clientRequestId, cancellationToken);
        if (receipt is null || receipt.작업Code == "IssueLabel") return Fail<마트기사업무항목Dto>("MartDriverRequestNotFound", 404);
        return await ReceiptResultAsync(receipt, cancellationToken);
    }

    private async Task<Result<마트기사업무항목Dto>> ReceiptResultAsync(마트기사요청영수증 receipt, CancellationToken ct)
    {
        if (!receipt.성공여부) return Fail<마트기사업무항목Dto>(receipt.결과Code, receipt.결과StatusCode);
        // 거절은 정상 업무 선택입니다. 다음 기사 배정 뒤의 위치/주소를 전 기사에게 다시 노출하지 않습니다.
        if (receipt.작업Code == "Reject") return Result.Ok(new 마트기사업무항목Dto
        {
            OfferId = receipt.배차의뢰Id, OrderReference = receipt.주문참조번호, StateCode = "Rejected", IsReplay = true
        });
        var result = await 업무조회Async(receipt.작업자UserId, receipt.배차의뢰Id, ct);
        if (result.IsSuccess) result.Value.IsReplay = true;
        return result;
    }

    private 마트기사업무항목Dto Item(운송원장 queue, 마트배송시도? attempt, 마트포장전표? label, string driverId)
    {
        var assigned = queue.확정기사Id == driverId && attempt?.기사Id == driverId;
        var completed = attempt?.상태Code == "Delivered";
        var offered = attempt is null && 배차응답가능정책.추천수락가능(queue, driverId, Now);
        return new()
        {
            OfferId = queue.의뢰Id, OrderReference = queue.원본의뢰Id, StateCode = attempt?.상태Code ?? "Offered",
            Revision = attempt?.Revision ?? 0, AttemptStableId = attempt?.시도StableId ?? "",
            RecommendationRound = queue.추천라운드, QueueUpdatedAtUtc = Utc(queue.UpdatedAt),
            Pickup = new FoodDeliveryDriverStopDto { Label = "살뜰마트 픽업", Address = completed ? "" : queue.픽업_도로명주소,
                Latitude = assigned && !completed ? queue.픽업_위도 : null, Longitude = assigned && !completed ? queue.픽업_경도 : null },
            Dropoff = new FoodDeliveryDriverStopDto { Label = "전달지", Address = assigned && !completed ? queue.하차_도로명주소 : "수락 후 확인",
                Latitude = assigned && !completed ? queue.하차_위도 : null, Longitude = assigned && !completed ? queue.하차_경도 : null },
            DriverPayout = queue.기사지급예정액, LabelRevision = assigned && label is not null ? label.Revision : null,
            CanAccept = offered, CanReject = offered,
            CanPickup = assigned && attempt?.상태Code == "Accepted" && label?.활성여부 == true && !label.픽업시각Utc.HasValue
                && queue.상태 == 상태값.배차대기상태.확정 && queue.배차큐단계 == 상태값.배차큐단계.확정,
            CanDeliver = assigned && attempt?.상태Code == "PickedUp" && attempt.픽업시각Utc.HasValue
                && queue.상태 == 상태값.배차상태.상차완료 && queue.배차큐단계 == 상태값.배차큐단계.확정
                && label is not null && !label.활성여부 && label.픽업시각Utc.HasValue
                && attempt.픽업전표Id == label.전표Id && attempt.픽업전표Revision == label.Revision
                && Utc(attempt.픽업시각Utc.Value) == Utc(label.픽업시각Utc.Value)
        };
    }
}
