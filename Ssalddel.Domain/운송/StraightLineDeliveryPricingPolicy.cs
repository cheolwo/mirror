using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.운송;

/// <summary>픽업·전달 좌표의 구면 직선거리와 거리비만 계산합니다. 주행 경로·ETA·수락 적격성 근거가 아닙니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.Domain,
    "픽업에서 전달까지의 원래 직선거리로 비례 거리비를 계산한다.",
    StepKey = "domain.straight-line-delivery-pricing", FlowOrder = 41,
    Effects = SsalddelCodeEffect.None, ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "거리 표시를 반올림하기 전 원거리로 거리비만 원 단위 반올림한다. 경로·시간·차량·혼적 안전 검사를 대체하지 않는다.")]
public static class StraightLineDeliveryPricingPolicy
{
    public const string Revision = "driver-straight-line-pricing.r1";
    public const string DistanceBasisCode = "StraightLine";
    public const string RoundingCode = "ProportionalAwayFromZero";
    private const double EarthMeanRadiusKm = 6371.0088d;

    public static decimal CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        if (lat1 is < -90m or > 90m || lat2 is < -90m or > 90m
            || lon1 is < -180m or > 180m || lon2 is < -180m or > 180m)
            throw new ArgumentException("StraightLinePricingCoordinatesInvalid");
        if (lat1 == lat2 && lon1 == lon2) return 0m;
        var firstLatitude = (double)lat1 * Math.PI / 180d;
        var secondLatitude = (double)lat2 * Math.PI / 180d;
        var latitudeDifference = ((double)lat2 - (double)lat1) * Math.PI / 180d;
        var longitudeDifference = ((double)lon2 - (double)lon1) * Math.PI / 180d;
        var haversine = Math.Pow(Math.Sin(latitudeDifference / 2d), 2d)
            + Math.Cos(firstLatitude) * Math.Cos(secondLatitude) * Math.Pow(Math.Sin(longitudeDifference / 2d), 2d);
        haversine = Math.Clamp(haversine, 0d, 1d);
        return (decimal)(EarthMeanRadiusKm * 2d * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1d - haversine)));
    }

    public static decimal CalculateDistanceFee(decimal rawDistanceKm, decimal ratePerKmKrw = 1000m)
    {
        if (rawDistanceKm < 0m || rawDistanceKm > 20100m || ratePerKmKrw is < 0m or > 1000000000m)
            throw new ArgumentException("StraightLinePricingDistanceOrRateInvalid");
        return decimal.Round(rawDistanceKm * ratePerKmKrw, 0, MidpointRounding.AwayFromZero);
    }
}
