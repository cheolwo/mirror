using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Shipper.Request;

/// <summary>
/// 결제 확보와 명시적인 후불·현장지급 조건을 구분한다.
/// 배차를 허용해도 실제 결제·청구·기사 지급 상태는 변경하지 않는다.
/// </summary>
public static class 화주운송배차진입정책
{
    public const string 진입불가오류코드 = "FreightSettlementNotReady";

    public static 화주운송배차진입판정 판정(화주운송의뢰 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.상태, 상태값.의뢰상태.생성됨, StringComparison.Ordinal)
            || string.Equals(request.배차상태, 상태값.배차상태.취소, StringComparison.Ordinal))
        {
            return 화주운송배차진입판정.불가("취소되었거나 유효한 생성 상태가 아닌 의뢰는 수락할 수 없습니다.");
        }

        if (request.결제상태 != 상태값.결제상태.결제대기
            && request.결제상태 != 상태값.결제상태.결제완료)
        {
            return 화주운송배차진입판정.불가("취소·환불되었거나 확인할 수 없는 결제 상태의 의뢰는 수락할 수 없습니다.");
        }

        if (!TryParseStoredEnum(request.정산시점, out 정산시점 settlementTime)
            || !TryParseStoredEnum(request.정산상태, out 운임정산상태 settlementStatus))
        {
            return 화주운송배차진입판정.불가("정산 시점과 정산 상태를 확인할 수 없어 수락할 수 없습니다.");
        }

        if (settlementStatus is 운임정산상태.정산취소
            or 운임정산상태.비정상운송검토보류
            or 운임정산상태.미수발생)
        {
            return 화주운송배차진입판정.불가("취소·보류·미수 상태의 정산은 수락할 수 없습니다.");
        }

        if (request.결제상태 == 상태값.결제상태.결제완료)
        {
            return 화주운송배차진입판정.가능함();
        }

        // 청구대기·인수증대기와 월말 후불승인대기는 승인 사실이 아니다.
        var authorized = settlementTime switch
        {
            정산시점.현장지급 => settlementStatus == 운임정산상태.현장수금예정,
            정산시점.운송완료후정산 or 정산시점.월말정산
                => settlementStatus == 운임정산상태.후불승인완료,
            _ => false
        };

        return authorized
            ? 화주운송배차진입판정.가능함()
            : 화주운송배차진입판정.불가("결제 확보 또는 후불·현장지급 조건 확인이 완료된 의뢰만 수락할 수 있습니다.");
    }

    private static bool TryParseStoredEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
        => Enum.TryParse(value, ignoreCase: false, out parsed)
           && Enum.IsDefined(parsed)
           && string.Equals(value, parsed.ToString(), StringComparison.Ordinal);
}

public sealed record 화주운송배차진입판정(bool 가능, string 사유)
{
    public static 화주운송배차진입판정 가능함() => new(true, string.Empty);

    public static 화주운송배차진입판정 불가(string 사유) => new(false, 사유);
}
