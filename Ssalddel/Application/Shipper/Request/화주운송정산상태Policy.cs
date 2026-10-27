using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

/// <summary>생성의 미지정 증빙과 수정의 명시 증빙에 같은 정산상태 매핑을 적용한다.</summary>
internal static class 화주운송정산상태Policy
{
    internal static string 판정(정산시점 settlementTime, 증빙방식? evidenceMethod)
    {
        return settlementTime switch
        {
            정산시점.현장지급 => 운임정산상태.현장수금예정.ToString(),
            정산시점.운송완료후정산 when evidenceMethod == 증빙방식.인수증 => 운임정산상태.인수증대기.ToString(),
            정산시점.운송완료후정산 => 운임정산상태.청구대기.ToString(),
            정산시점.월말정산 => 운임정산상태.후불승인대기.ToString(),
            _ => 운임정산상태.결제대기.ToString()
        };
    }
}
