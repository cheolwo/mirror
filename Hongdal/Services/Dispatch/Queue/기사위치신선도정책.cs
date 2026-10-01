namespace 홍달.Services.Dispatch.Queue;

public static class 기사위치신선도정책
{
    public static bool 유효한가(
        DateTime? 위치수신시각Utc,
        DateTime 기준시각Utc,
        int 유효시간분)
    {
        if (!위치수신시각Utc.HasValue)
        {
            return false;
        }

        var 유효시간 = TimeSpan.FromMinutes(Math.Max(1, 유효시간분));
        var 경과시간 = 기준시각Utc - 위치수신시각Utc.Value;
        var 허용시계오차 = TimeSpan.FromMinutes(1);
        return 경과시간 >= -허용시계오차 && 경과시간 <= 유효시간;
    }
}
