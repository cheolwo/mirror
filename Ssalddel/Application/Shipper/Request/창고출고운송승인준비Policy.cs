using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Engine;

namespace Ssalddel.Application.Shipper.Request;

/// <summary>창고가 만든 검토 전 운송 초안을 배차 승인으로 잠그기 전에 기존 화주 입력·저장 견적을 확인합니다.</summary>
public static class 창고출고운송승인준비Policy
{
    public static (string Code, string Message)? 확인(화주운송의뢰 request, 운송원장? transport, 운임구성? fare)
    {
        // 일반 화물·음식·생활 배송의 기존 승인 정책을 확장하지 않습니다.
        if (transport?.원본의뢰유형 != 운송의뢰배차원천유형.창고출고연계운송) return null;
        if (!Coordinate(request.픽업_위도, request.픽업_경도) || !Coordinate(request.하차_위도, request.하차_경도))
            return ("WarehouseReconsignmentCoordinatesMissing", "상차·하차 위치 좌표를 먼저 확인해 주세요. 승인 전 화주 운송 상세에서 위치와 견적을 보완할 수 있습니다.");
        if (request.운임구성Id is null || fare is null || fare.Id != request.운임구성Id || fare.의뢰Id != request.의뢰Id
            || request.최종운임 is not > 0m || fare.최종운임 is not > 0m || fare.예상거리Km is not > 0m
            || fare.기사지급예정운임 is not > 0m || request.결제예정금액 is not > 0)
            return ("WarehouseReconsignmentFareMissing", "저장된 운송 견적·거리·기사 보수·결제 예정액을 먼저 확인해 주세요. 미확정 금액을 0원이나 고객 청구액으로 대신하지 않습니다.");
        if (fare.최종운임 != request.최종운임 || request.결제예정금액 < request.최종운임
            || fare.기사지급예정운임 > request.결제예정금액)
            return ("WarehouseReconsignmentFareChanged", "저장 견적과 청구·기사 보수 근거가 일치하지 않습니다. 현재 운송 조건으로 견적을 다시 확인해 주세요.");
        return null;
    }

    private static bool Coordinate(decimal? latitude, decimal? longitude)
        => latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m;
}
