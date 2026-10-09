using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Transport;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.Food;

/// <summary>음식 원천의 주소와 상품으로만 조건/큐를 구성합니다. 견적 조건은 큐로 저장하지 않습니다.</summary>
public sealed class 음식배차원천Builder(SsalddelContext db, I운송의뢰배차대기Service queueService,
    IKakao좌표변환Service geo)
{
    public async Task<운송원장> 조건Async(음식주문응답 order, CancellationToken ct)
    {
        var pickupAddress = string.IsNullOrWhiteSpace(order.음식점상세주소) ? order.음식점주소 : $"{order.음식점주소} {order.음식점상세주소}";
        var pickup = await CoordinateAsync(order.음식점위도, order.음식점경도, pickupAddress, ct);
        var dropoff = await CoordinateAsync(null, null, order.수령인정보.주소, ct);
        return new 운송원장
        {
            의뢰Id = order.주문번호, 원본의뢰Id = order.주문번호, 원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            배차업무유형 = 상태값.배차업무유형.음식배달, 화주Id = $"restaurant:{order.음식점Id}",
            픽업_도로명주소 = pickupAddress, 픽업_상세주소 = order.음식점상세주소,
            하차_도로명주소 = order.수령인정보.주소, 하차_상세주소 = order.수령인정보.상세주소,
            픽업_위도 = pickup?.Lat, 픽업_경도 = pickup?.Lng, 하차_위도 = dropoff?.Lat, 하차_경도 = dropoff?.Lng
        };
    }

    public async Task<운송원장> 생성Async(음식주문응답 order, CancellationToken ct)
    {
        var source = await 조건Async(order, ct);
        var target = new 출고예정운송대상
        {
            원천유형 = 출고예정운송대상원천유형.음식주문, 원천참조번호 = order.주문번호, 운송의뢰Id = order.주문번호,
            표시명 = FoodOrderSampleData.BuildMenuSummary(order.상품목록), 판매자UserId = source.화주Id, 주문자UserId = order.주문자UserId,
            상차주소 = source.픽업_도로명주소, 상차위도 = source.픽업_위도, 상차경도 = source.픽업_경도,
            하차주소 = source.하차_도로명주소, 하차위도 = source.하차_위도, 하차경도 = source.하차_경도,
            온도조건 = "음식", 파손주의 = true,
            Lines = order.상품목록.Select((line, index) => new 출고예정운송대상라인
            { LineKey = $"{order.주문번호}-{index + 1}", Sku = line.상품명, ProductName = line.상품명, Quantity = line.수량 }).ToArray()
        };
        var queue = await queueService.생성또는조회Async(target, new 운송의뢰배차대기생성옵션
        {
            의뢰Id = order.주문번호, 화주Id = source.화주Id, 배차업무유형 = 상태값.배차업무유형.음식배달,
            원본의뢰유형 = 운송의뢰배차원천유형.음식점주문, 원본의뢰Id = order.주문번호,
            픽업상세주소 = source.픽업_상세주소, 하차상세주소 = source.하차_상세주소, 상태 = 상태값.배차대기상태.대기
        }, ct);
        await db.SaveChangesAsync(ct);
        return queue;
    }

    private async Task<(decimal Lat, decimal Lng)?> CoordinateAsync(decimal? lat, decimal? lng, string address, CancellationToken ct)
    {
        if (lat is >= -90 and <= 90 && lng is >= -180 and <= 180) return (lat.Value, lng.Value);
        if (string.IsNullOrWhiteSpace(address)) return null;
        try
        {
            var found = await geo.주소정보조회Async(address, ct);
            return found?.위도 is { } x && found.경도 is { } y ? (x, y) : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return null; }
    }
}
