using 홍달.도메인.공통;
using 홍달.도메인.배차;

namespace 홍달.Services.Dispatch.Queue;

public static class 배차실행인덱스재구성정책
{
    public static IQueryable<운송원장> 미처리운송의뢰쿼리(
        this IQueryable<운송원장> query,
        DateTime 기준시각Utc)
        => query
            .Where(x => x.배차업무유형 == 상태값.배차업무유형.용달운송
                        || x.배차업무유형 == 상태값.배차업무유형.음식배달)
            .Where(x => x.상태 == 상태값.배차대기상태.대기)
            .Where(x => x.배차큐단계 != 상태값.배차큐단계.확정 && x.배차큐단계 != 상태값.배차큐단계.종료)
            .Where(x => x.배차노출상태 != 상태값.배차노출상태.추천중
                        || string.IsNullOrWhiteSpace(x.현재추천대상기사Id)
                        || x.추천만료시각 <= 기준시각Utc);

    public static bool 미처리운송의뢰인가(운송원장 queue, DateTime 기준시각Utc)
        => queue.배차업무유형 is 상태값.배차업무유형.용달운송 or 상태값.배차업무유형.음식배달
           && queue.상태 == 상태값.배차대기상태.대기
           && queue.배차큐단계 is not 상태값.배차큐단계.확정 and not 상태값.배차큐단계.종료
           && !유효한추천중잠금인가(queue, 기준시각Utc);

    public static bool 유효한추천중잠금인가(운송원장 queue, DateTime 기준시각Utc)
        => queue.배차노출상태 == 상태값.배차노출상태.추천중
           && !string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
           && (!queue.추천만료시각.HasValue || queue.추천만료시각 > 기준시각Utc);
}
