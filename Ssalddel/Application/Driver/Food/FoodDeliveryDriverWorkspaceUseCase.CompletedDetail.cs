using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Driver.Food;

public sealed partial class FoodDeliveryDriverWorkspaceUseCase
{
    public async Task<FoodDeliveryCompletedDeliveryDetailDto?> GetCompletedDeliveryDetailAsync(
        string driverId, string settlementId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(driverId) || string.IsNullOrWhiteSpace(settlementId)) return null;
        // 소유권 검사를 조회 조건에 포함합니다. 다른 기사의 정산 존재 여부와 고객 정보는 반환하지 않습니다.
        var settlement = await _db.음식주문기사정산.AsNoTracking().Include(x => x.지급검증목록)
            .SingleOrDefaultAsync(x => x.정산StableId == settlementId && x.기사Id == driverId, cancellationToken);
        if (settlement is null) return null;

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var result = new FoodDeliveryCompletedDeliveryDetailDto
        {
            Settlement = 음식주문기사정산Recorder.ToDto(settlement, currentExecutionModeCode: _executionMode.Mode.ToString()),
            ServerNowUtc = nowUtc
        };
        var (status, expiresAt) = FoodDeliveryCompletedDetailAccessPolicy.Evaluate(
            settlement.전달완료시각Utc, nowUtc, _completedDetailAccessOptions);
        result.DetailAccessStatusCode = status;
        result.DetailExpiresAtUtc = expiresAt;
        if (status != FoodDeliveryCompletedDetailAccessStatusCodes.Allowed) return result;

        // 주문·시도·운송 연결이 끊어졌거나 재배차된 자료를 현재 기사에게 보여 주지 않습니다.
        var attempt = await _db.음식배달시도.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == settlement.배달시도Id && x.시도StableId == settlement.배달시도StableId
                 && x.기사Id == driverId && x.주문번호 == settlement.주문번호, cancellationToken);
        var transport = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == settlement.운송Id && x.확정기사Id == driverId && x.기사_운송자 == driverId
                 && x.배차업무유형 == 상태값.배차업무유형.음식배달
                 && (x.원본의뢰유형 == 운송의뢰배차원천유형.음식점주문
                     || x.원본의뢰유형 == 운송의뢰배차원천유형.음식주문
                     || x.원본의뢰유형 == "음식점주문")
                 && x.원본의뢰Id == settlement.주문번호
                 && x.배차큐단계 == 상태값.배차큐단계.종료, cancellationToken);
        if (attempt is null || transport is null || attempt.제안Id != transport.의뢰Id
            || attempt.중단시각Utc.HasValue || attempt.상태Code != 음식배달시도상태Code.전달완료
            || attempt.전달완료시각Utc != settlement.전달완료시각Utc)
            return EvidenceUnavailable(result);

        var order = await _db.음식주문.AsNoTracking().Include(x => x.상품목록).SingleOrDefaultAsync(
            x => x.Id == settlement.음식주문Id && x.주문번호 == settlement.주문번호, cancellationToken);
        if (order is null || 음식주문상태코드.Normalize(order.상태) is not (음식주문상태코드.전달완료 or 음식주문상태코드.수령확인))
            return EvidenceUnavailable(result);

        // DB 조회 중 기한을 지나면 응답 생성 시점에 다시 차단합니다.
        result.ServerNowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (result.ServerNowUtc >= expiresAt!.Value)
        {
            result.DetailAccessStatusCode = FoodDeliveryCompletedDetailAccessStatusCodes.Expired;
            return result;
        }
        result.OrderDetails = new FoodDeliveryCompletedOrderDetailsDto
        {
            RestaurantAddress = Address(order.음식점주소, order.음식점상세주소),
            TotalOrderAmount = order.총주문금액,
            Items = order.상품목록.OrderBy(x => x.Id).Select(x => new FoodDeliveryCompletedOrderItemDto
            {
                MenuName = x.상품명, Quantity = x.수량, UnitPrice = x.단가
            }).ToArray()
        };
        result.CustomerDetails = new FoodDeliveryCompletedCustomerDetailsDto
        {
            DisplayName = order.수령인명,
            ContactPhone = order.수령인연락처,
            Address = Address(order.수령지주소, order.수령지상세주소),
            DeliveryInstructions = order.수령요청사항
        };
        return result;
    }

    private static FoodDeliveryCompletedDeliveryDetailDto EvidenceUnavailable(FoodDeliveryCompletedDeliveryDetailDto result)
    {
        result.DetailAccessStatusCode = FoodDeliveryCompletedDetailAccessStatusCodes.CompletionEvidenceUnavailable;
        return result;
    }

    private static string Address(string address, string detail)
        => string.Join(" ", new[] { address, detail }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
