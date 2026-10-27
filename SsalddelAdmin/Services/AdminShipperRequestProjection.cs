using System.Text.Json;
using SharedRequest = Ssalddel.Contracts.Shipper.Request.화주운송의뢰응답;

namespace SsalddelAdmin.Services;

/// <summary>공유 API 계약의 알려진 정산 열거값을 관리자 표시용 이름으로 바꿉니다. 금액·업무 상태를 계산하지 않습니다.</summary>
public static class AdminShipperRequestProjection
{
    public static 화주운송의뢰응답 ToView(SharedRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            의뢰Id = source.의뢰Id, 의뢰상태 = source.의뢰상태, 결제상태 = source.결제상태,
            정산상태 = source.정산상태, 배차상태 = source.배차상태, 운송방식 = source.운송방식,
            결제수단 = source.결제수단, 정산시점 = EnumName(source.정산시점, "$.정산시점"),
            증빙방식 = EnumName(source.증빙방식, "$.증빙방식"), 수납주체 = EnumName(source.수납주체, "$.수납주체"),
            세금계산서필요 = source.세금계산서필요, 현금영수증필요 = source.현금영수증필요,
            정산메모 = source.정산메모, 생성일시 = source.생성일시,
            픽업지 = source.픽업지, 픽업상세지 = source.픽업상세지,
            픽업위도 = source.픽업위도, 픽업경도 = source.픽업경도,
            하차지 = source.하차지, 하차상세지 = source.하차상세지,
            하차위도 = source.하차위도, 하차경도 = source.하차경도,
            대기료 = source.대기료, 수작업비 = source.수작업비, 할증 = source.할증, 최종운임 = source.최종운임,
            요약 = source.요약 is { } summary ? new 의뢰요약
            { 화물종류 = summary.화물종류, 픽업지 = summary.픽업지, 하차지 = summary.하차지 } : null
        };
    }

    private static string EnumName<T>(T? value, string path) where T : struct, Enum
    {
        if (!value.HasValue) return "미확인";
        if (!Enum.IsDefined(value.Value))
            throw new JsonException("지원하지 않는 운송 정산 조건입니다.", path, null, null);
        return value.Value.ToString();
    }
}
