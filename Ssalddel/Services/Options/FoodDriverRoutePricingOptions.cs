namespace 살뜰.Services.Options;

// 운영자 서버 설정. 미설정 시간 구간에 할증을 추정하지 않는다.
public sealed class FoodDriverRoutePricingOptions
{
    public const string SectionName = "FoodDriverRoutePricing";
    public string CarRouteOption { get; set; } = "trafast";
    public string MotorcycleRouteOption { get; set; } = "traavoidcaronly";
    public List<FoodDriverTimeBand> TimeBands { get; set; } = [];

    public FoodDriverTimeBand? ResolveBand(DateTimeOffset utc)
    {
        foreach (var b in TimeBands)
            if (string.IsNullOrWhiteSpace(b.Code) || b.StartMinuteKst is < 0 or >= 1440
                || b.EndMinuteKst is < 0 or >= 1440 || b.StartMinuteKst == b.EndMinuteKst
                || b.CarSurchargeKrw < 0 || b.MotorcycleSurchargeKrw < 0
                || (b.CarRouteOption is not null && b.CarRouteOption is not ("trafast" or "tracomfort" or "traoptimal" or "traavoidtoll" or "traavoidcaronly")))
                throw new ArgumentException("FoodPricingTimeBandInvalid");
        var kst = utc.ToOffset(TimeSpan.FromHours(9));
        var minute = kst.Hour * 60 + kst.Minute;
        var matches = TimeBands.Where(b => b.StartMinuteKst < b.EndMinuteKst
            ? minute >= b.StartMinuteKst && minute < b.EndMinuteKst
            : minute >= b.StartMinuteKst || minute < b.EndMinuteKst).ToArray();
        if (matches.Length > 1) throw new ArgumentException("FoodPricingTimeBandsOverlap");
        return matches.SingleOrDefault();
    }
}
public sealed class FoodDriverTimeBand
{
    public string Code { get; set; } = "";
    public int StartMinuteKst { get; set; }
    public int EndMinuteKst { get; set; }
    public string? CarRouteOption { get; set; }
    public decimal CarSurchargeKrw { get; set; }
    public decimal MotorcycleSurchargeKrw { get; set; }
}
