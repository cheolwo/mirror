namespace 살뜰.도메인.음식;

public sealed record 음식배달기사제안요금판정(
    decimal 기본거리지급액,
    decimal 기상할증액,
    decimal 기사지급예정액,
    bool 기상할증적용여부);

public static class 음식배달기사제안요금Policy
{
    public const string 기본판본 = "food-driver-offer-pricing.r2";

    public static 음식배달기사제안요금판정 판정(
        음식운영정책 policy,
        decimal? 픽업지에서전달지까지거리Km,
        bool 유효한강수근거있음,
        bool 강수중)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var distanceMeters = Math.Max(0m, 픽업지에서전달지까지거리Km ?? 0m) * 1000m;
        var additionalMeters = Math.Max(0m, distanceMeters - policy.포함거리Meters);
        var distanceUnits = additionalMeters <= 0m
            ? 0m
            : Math.Ceiling(additionalMeters / Math.Max(1, policy.거리단위Meters));
        var basePayout = Math.Max(
            policy.기사최소지급액,
            policy.기사기본지급액 + distanceUnits * policy.기사거리단위지급액);
        var weatherApplied = policy.기사기상할증활성화여부
                             && 유효한강수근거있음
                             && 강수중;
        var weatherSurcharge = weatherApplied
            ? Math.Max(0m, policy.기사기상할증액)
            : 0m;

        return new 음식배달기사제안요금판정(
            basePayout,
            weatherSurcharge,
            basePayout + weatherSurcharge,
            weatherApplied);
    }
}
