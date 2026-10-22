using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.기사;
using 살뜰.도메인.화주;
using 살뜰.Services.Storage.Local;
using Ssalddel.Application.Shipper.Request;

namespace 살뜰.Services.Dispatch.Coordination;

public static class 국내화물배차후보금지정책
{
    public static bool 추천잠금가능(운송원장 배차대기, DateTime 기준시각Utc)
        => 배차대기.상태 == 상태값.배차대기상태.대기
           && 배차대기.배차큐단계 is not (상태값.배차큐단계.확정 or 상태값.배차큐단계.종료)
           && !유효추천잠금중(배차대기, 기준시각Utc);

    public static bool 유효추천잠금중(운송원장 배차대기, DateTime 기준시각Utc)
        => 배차대기.배차노출상태 == 상태값.배차노출상태.추천중
           && !string.IsNullOrWhiteSpace(배차대기.현재추천대상기사Id)
           && (!배차대기.추천만료시각.HasValue || 배차대기.추천만료시각 > 기준시각Utc);

    public static IReadOnlyList<string> 운송의뢰후보금지사유(
        운송원장 배차대기,
        화주운송의뢰? 운송의뢰,
        DateTime 기준시각Utc)
    {
        var 금지사유목록 = new List<string>();

        if (운송의뢰 is null)
        {
            금지사유목록.Add("운송 의뢰 원장을 찾을 수 없습니다.");
        }
        else if (운송의뢰.정산시점 == "선결제" && 운송의뢰.결제상태 != 상태값.결제상태.결제완료)
        {
            // 이전에 생성된 미결제 큐도 추천 전에 검사합니다. 수락 단계의 기존 검사를 대체하지 않습니다.
            var entry = 화주운송배차진입정책.판정(운송의뢰);
            if (!entry.가능) 금지사유목록.Add(entry.사유);
        }

        if (배차대기.배차업무유형 != 상태값.배차업무유형.용달운송)
        {
            금지사유목록.Add("국내 화물 운송 배차 대상이 아닙니다.");
        }

        if (배차대기.상태 != 상태값.배차대기상태.대기)
        {
            금지사유목록.Add("배차 대기 상태가 아닙니다.");
        }

        if (배차대기.배차큐단계 is 상태값.배차큐단계.확정 or 상태값.배차큐단계.종료)
        {
            금지사유목록.Add("이미 확정 또는 종료된 배차 건입니다.");
        }

        if (유효추천잠금중(배차대기, 기준시각Utc))
        {
            금지사유목록.Add("다른 기사에게 추천 잠금 중인 운송 의뢰입니다.");
        }

        if (!배차대기.픽업_위도.HasValue || !배차대기.픽업_경도.HasValue)
        {
            금지사유목록.Add("상차지 좌표가 없습니다.");
        }

        if (!배차대기.하차_위도.HasValue || !배차대기.하차_경도.HasValue)
        {
            금지사유목록.Add("하차지 좌표가 없습니다.");
        }

        return 금지사유목록;
    }

    public static IReadOnlyList<string> 기사후보금지사유(
        국내화물운송기사상태Snapshot 기사상태,
        용달기사? 기사)
    {
        var 금지사유목록 = new List<string>();

        if (기사 is null)
        {
            금지사유목록.Add("기사 원장을 찾을 수 없습니다.");
        }
        else if (!string.Equals(기사.상태, "활동중", StringComparison.Ordinal))
        {
            금지사유목록.Add("활동중 기사 상태가 아닙니다.");
        }

        if (!string.Equals(기사상태.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
        {
            금지사유목록.Add("기사가 운행중 상태가 아닙니다.");
        }

        if (!기사상태.Latitude.HasValue || !기사상태.Longitude.HasValue)
        {
            금지사유목록.Add("기사 현재 위치가 없습니다.");
        }

        return 금지사유목록;
    }

    public static IReadOnlyList<string> 조합후보금지사유(
        string 기사Id,
        IReadOnlySet<string> 거절기사Ids)
    {
        return 거절기사Ids.Contains(기사Id)
            ? ["기사가 이미 거절한 의뢰입니다."]
            : [];
    }
}
