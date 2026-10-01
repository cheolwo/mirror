namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static class KamisPriceRequestRules
{
    public static void ValidatePeriod(DateOnly startDate, DateOnly endDate)
    {
        if (startDate.Year is < 1990 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(startDate));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("종료일은 시작일보다 빠를 수 없습니다.", nameof(endDate));
        }

        if (endDate >= startDate.AddYears(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(endDate),
                "KAMIS 기간 조회는 시작일을 포함해 최대 1년 미만 범위로 요청해야 합니다.");
        }
    }
}
