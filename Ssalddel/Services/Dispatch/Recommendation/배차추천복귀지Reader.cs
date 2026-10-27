using Microsoft.EntityFrameworkCore;

namespace 살뜰.Services.Dispatch.Recommendation;

internal sealed class 배차추천복귀지Reader(SsalddelContext db)
{
    internal const string 오늘복귀지출처 = "오늘복귀지";
    internal const string 기본복귀지출처 = "기본복귀지";
    internal const string 복귀지없음출처 = "없음";

    internal async Task<복귀지결정결과> ResolveAsync(string driverId, 용달기사? driver)
    {
        var currentShift = await db.기사근무
            .AsNoTracking()
            .Where(x => x.기사Id == driverId)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(currentShift?.오늘의복귀지주소)
            && currentShift.오늘의복귀지위도.HasValue
            && currentShift.오늘의복귀지경도.HasValue)
        {
            return new 복귀지결정결과(
                currentShift.오늘의복귀지주소,
                new 배차경로좌표(currentShift.오늘의복귀지위도.Value, currentShift.오늘의복귀지경도.Value),
                string.IsNullOrWhiteSpace(currentShift.복귀지출처) ? 오늘복귀지출처 : currentShift.복귀지출처,
                true);
        }

        if (!string.IsNullOrWhiteSpace(driver?.기본복귀지주소)
            && driver.기본복귀지위도.HasValue
            && driver.기본복귀지경도.HasValue)
        {
            return new 복귀지결정결과(
                driver.기본복귀지주소,
                new 배차경로좌표(driver.기본복귀지위도.Value, driver.기본복귀지경도.Value),
                기본복귀지출처,
                true);
        }

        return new 복귀지결정결과(null, null, 복귀지없음출처, false);
    }
}
