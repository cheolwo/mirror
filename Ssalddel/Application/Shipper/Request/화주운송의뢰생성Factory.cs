using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.화주;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Shipper.Request;

internal sealed record 화주운송의뢰생성정산입력(
    정산시점 정산시점, string 결제수단, string 증빙방식, string 수납주체,
    string 정산상태, decimal? 최종운임, int? 결제예정금액);

/// <summary>이미 확인한 입력·좌표·식별자로 새 의뢰와 운임 구성을 만든다. 저장·권한·시각 생성은 호출자가 소유한다.</summary>
internal static class 화주운송의뢰생성Factory
{
    internal static 화주운송의뢰생성정산입력 정산입력(의뢰생성Command request)
    {
        var settlementCondition = request.정산조건;
        var settlementTime = settlementCondition?.정산시점 ?? 정산시점.선결제;
        var paymentMethod = settlementCondition?.결제수단.ToString() ?? request.결제수단 ?? "카드";
        var evidenceMethod = settlementCondition?.증빙방식.ToString() ?? 증빙방식.없음.ToString();
        var collector = settlementCondition?.수납주체.ToString() ?? 수납주체.플랫폼.ToString();
        var settlementStatus = 화주운송정산상태Policy.판정(settlementTime, settlementCondition?.증빙방식);
        var finalFare = request.최종운임 ?? request.결제예정금액;
        var paymentAmount = request.결제예정금액 ?? (finalFare.HasValue ? decimal.ToInt32(finalFare.Value) : null);

        return new(settlementTime, paymentMethod, evidenceMethod, collector, settlementStatus, finalFare, paymentAmount);
    }

    internal static 화주운송의뢰 생성(
        의뢰생성Command request, string requestId, string currentUserId, string shipperId,
        string clientRequestId, string paymentStatus, 화주운송의뢰생성정산입력 settlement,
        (decimal? lat, decimal? lng) pickup, (decimal? lat, decimal? lng) dropoff)
    {
        return new 화주운송의뢰
        {
            모의처리처수행 = request.SimulationFulfillment,
            자동처리처수행 = request.SimulationFulfillment && request.SimulationAutomaticFulfillment,
            모의처리희망완료Utc = request.SimulationFulfillment ? request.SimulationDesiredCompletionUtc : null,
            // 새 모바일 접수는 첫 요청이 아직 저장 중이어도 같은 Mongo 원장/CAS와 RDB 키를 사용합니다.
            // 기존 요청의 ID와 생활배송 키 의미는 변경하지 않습니다.
            의뢰Id = requestId,
            화주Id = shipperId,
            주문자UserId = currentUserId,
            화물종류 = request.화물종류,
            화물설명 = request.화물설명 ?? string.Empty,
            화물수량 = request.화물수량,
            화물길이Mm = request.화물길이Mm,
            화물폭Mm = request.화물폭Mm,
            화물높이Mm = request.화물높이Mm,
            화물팔레트개수 = request.팔레트개수,
            화물중량Kg = request.화물중량Kg,
            화물부피Cbm = request.화물부피Cbm,
            화물파손주의여부 = request.화물파손주의여부,
            화물온도조건 = request.화물온도조건 ?? "상온",
            운송방식 = request.운송방식 ?? "혼적",
            차량종류 = request.차량종류 ?? string.Empty,
            결제수단 = settlement.결제수단,
            정산시점 = settlement.정산시점.ToString(),
            증빙방식 = settlement.증빙방식,
            수납주체 = settlement.수납주체,
            정산상태 = request.SimulationFulfillment ? "모의 수행·정산 제외" : settlement.정산상태,
            정산메모 = request.정산조건?.정산메모 ?? string.Empty,
            세금계산서필요 = request.정산조건?.세금계산서필요 ?? false,
            현금영수증필요 = request.정산조건?.현금영수증필요 ?? false,
            결제예정금액 = settlement.결제예정금액,
            픽업_도로명주소 = request.픽업도로명주소,
            픽업_상세주소 = request.픽업상세주소 ?? string.Empty,
            픽업_위도 = pickup.lat,
            픽업_경도 = pickup.lng,
            픽업_연락처_이름 = request.픽업연락처이름,
            픽업_연락처_전화번호 = request.픽업연락처전화번호,
            픽업_시간창_시작일시 = request.픽업시간창시작일시,
            픽업_시간창_종료일시 = request.픽업시간창종료일시,
            픽업_작업시간분 = request.픽업작업시간분,
            하차_도로명주소 = request.하차도로명주소,
            하차_상세주소 = request.하차상세주소 ?? string.Empty,
            하차_위도 = dropoff.lat,
            하차_경도 = dropoff.lng,
            하차_연락처_이름 = request.하차연락처이름,
            하차_연락처_전화번호 = request.하차연락처전화번호,
            하차_시간창_시작일시 = request.하차시간창시작일시,
            하차_시간창_종료일시 = request.하차시간창종료일시,
            하차_작업시간분 = request.하차작업시간분,
            서비스레벨 = request.서비스레벨 ?? string.Empty,
            요청사항 = request.요청사항 ?? string.Empty,
            대기료 = request.대기료,
            수작업비 = request.수작업비,
            할증 = request.할증,
            최종운임 = settlement.최종운임,
            클라이언트요청Id = clientRequestId,
            생활배송접수키 = clientRequestId.StartsWith(Ssalddel.Contracts.Common.Community.NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal)
                ? Ssalddel.Services.Community.생활배송배차Policy.접수키(currentUserId, clientRequestId) : null,
            상태 = 상태값.의뢰상태.생성됨,
            결제상태 = paymentStatus,
            배차상태 = 상태값.배차상태.미시작,
            // 모델 기본 시각은 쓰지 않고 Handler가 구성 직후 기존 순서로 확정합니다.
            CreatedAt = default,
            UpdatedAt = default
        };
    }

    internal static 운임구성 운임구성생성(의뢰생성Command request, 화주운송의뢰 entity)
    {
        return new 운임구성
        {
            의뢰Id = entity.의뢰Id,
            기본운임 = request.기본운임 ?? 0m,
            거리운임 = request.거리운임 ?? 0m,
            할증 = request.할증 ?? 0m,
            대기료 = request.대기료 ?? 0m,
            수작업비 = request.수작업비 ?? 0m,
            최종운임 = entity.최종운임 ?? 0m,
            기사지급예정운임 = request.기사지급예정운임,
            예상거리Km = request.예상거리Km,
            Km당단가 = request.Km당단가,
            최소운임 = request.최소운임,
            거리계산방식 = request.거리계산방식,
            단가출처 = request.단가출처,
            CreatedAt = default,
            UpdatedAt = default
        };
    }
}
