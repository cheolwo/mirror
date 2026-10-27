using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Hubs;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Common.Transport;

/// <summary>연결된 화주 원장의 조건만 읽습니다. 저장값·요금·추천 적격성은 변경하지 않습니다.</summary>
public static class 화물운송조건Projector
{
    // 한국시간 작성 입력은 기존 API adapter에서 UTC로 전송됩니다.
    // EF의 Kind 없는 값도 같은 UTC ticks이며, 레거시 기본 날짜를 실제 약속 시각으로 만들지 않습니다.
    public static DateTime? 시각(DateTime? value)
        => value is { } instant && instant != default
            ? 기사추천시각Policy.Utc로정규화(instant) : null;

    public static void 투영(DispatchRecommendationDto target, 화주운송의뢰? source)
    {
        if (source?.의뢰Id != target.의뢰Id) source = null;
        target.운송방식 = source?.운송방식;
        target.차량종류 = source?.차량종류;
        target.화물중량Kg = source?.화물중량Kg;
        target.화물수량 = source?.화물수량;
        target.상차시간창시작일시 = 시각(source?.픽업_시간창_시작일시);
        target.상차시간창종료일시 = 시각(source?.픽업_시간창_종료일시);
        target.하차시간창시작일시 = 시각(source?.하차_시간창_시작일시);
        target.하차시간창종료일시 = 시각(source?.하차_시간창_종료일시);
    }

    public static void 투영(기사운송의뢰상세응답 target, 화주운송의뢰? source)
    {
        if (source?.의뢰Id != target.의뢰Id) source = null;
        target.운송방식 = source?.운송방식;
        target.차량종류 = source?.차량종류;
        target.화물중량Kg = source?.화물중량Kg;
        target.화물수량 = source?.화물수량;
        target.상차시간창시작일시 = 시각(source?.픽업_시간창_시작일시);
        target.상차시간창종료일시 = 시각(source?.픽업_시간창_종료일시);
        target.하차시간창시작일시 = 시각(source?.하차_시간창_시작일시);
        target.하차시간창종료일시 = 시각(source?.하차_시간창_종료일시);
    }

    public static void 투영(운송진행응답 target, 화주운송의뢰? source, string requestId)
    {
        if (source?.의뢰Id != requestId) source = null;
        target.운송방식 = source?.운송방식;
        target.차량종류 = source?.차량종류;
        target.화물중량Kg = source?.화물중량Kg;
        target.화물수량 = source?.화물수량;
        target.상차시간창시작일시 = 시각(source?.픽업_시간창_시작일시);
        target.상차시간창종료일시 = 시각(source?.픽업_시간창_종료일시);
        target.하차시간창시작일시 = 시각(source?.하차_시간창_시작일시);
        target.하차시간창종료일시 = 시각(source?.하차_시간창_종료일시);
    }
}
