using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using Ssalddel.Application.Driver.Transport;

namespace Ssalddel.Application.Driver.DispatchAction;

public static class 배차응답가능정책
{
    public const string 물품보유복구필요Code = "FreightCustodyRecoveryRequired";
    public const string 취소상태변경Code = "FreightCancellationStateChanged";
    public const string 물품보유복구안내 = "물품을 픽업한 뒤에는 일반 수락 취소를 할 수 없습니다. 현재 운송의 문제 신고에서 반납·재전달 등 다음 처리를 확인해 주세요.";

    public static bool 추천수락가능(운송원장 queue, string 기사Id, DateTime 기준시각Utc)
        => 추천응답가능(queue, 기사Id, 기준시각Utc);

    public static bool 추천거절가능(운송원장 queue, string 기사Id, DateTime 기준시각Utc)
        => 추천응답가능(queue, 기사Id, 기준시각Utc);

    public static bool 공개배차수락가능(운송원장 queue)
        => 대기상태인가(queue) && queue.생활배송수락준비완료
           && Ssalddel.Services.Community.생활배송배차Policy.공개허용(queue)
           && ((queue.배차큐단계 == 상태값.배차큐단계.공개배차 && queue.배차노출상태 == 상태값.배차노출상태.공개중)
               || Ssalddel.Services.Community.생활배송배차Policy.병행공개가능(queue))
           && queue.확정기사Id is null;

    public static bool 수락취소가능(운송원장 queue, string 기사Id)
    {
        if (string.IsNullOrWhiteSpace(기사Id) || queue.일반회원배송전용 || 물품인수이후인가(queue)) return false;
        // 확정 기사와 예전 추천 대상이 다르면 확정 주체가 우선한다.
        var acceptedByDriver = string.Equals(queue.확정기사Id ?? queue.현재추천대상기사Id, 기사Id, StringComparison.Ordinal)
                               && (string.IsNullOrWhiteSpace(queue.기사_운송자) || queue.기사_운송자 == 기사Id);
        var cancelableState = queue.상태 == 상태값.배차대기상태.확정
                              || queue.배차큐단계 == 상태값.배차큐단계.확정;
        var beforePickup = queue.상태 is 상태값.배차대기상태.확정 or 기사운송상태코드.배차확정
            or 기사운송상태코드.이동중 or 기사운송상태코드.상차지도착;
        return cancelableState && beforePickup && acceptedByDriver;
    }

    public static bool 물품인수이후인가(운송원장 queue)
        => queue.출발_픽업.HasValue || queue.도착.HasValue || 인수이후상태인가(queue.상태);

    public static bool 인수이후상태인가(string? 상태)
        => 상태 is 기사운송상태코드.상차완료 or 기사운송상태코드.운송중
            or 기사운송상태코드.하차지도착 or 기사운송상태코드.인수완료;

    private static bool 추천응답가능(운송원장 queue, string 기사Id, DateTime 기준시각Utc)
        => 대기상태인가(queue) && queue.생활배송수락준비완료 && Ssalddel.Services.Community.생활배송배차Policy.자동추천가능(queue)
           && queue.배차큐단계 == 상태값.배차큐단계.배차추천
           && queue.배차노출상태 == 상태값.배차노출상태.추천중
           && string.Equals(queue.현재추천대상기사Id, 기사Id, StringComparison.Ordinal)
           && queue.추천만료시각.HasValue
           && queue.추천만료시각 > 기준시각Utc;

    private static bool 대기상태인가(운송원장 queue)
        => queue.상태 == 상태값.배차대기상태.대기;
}
