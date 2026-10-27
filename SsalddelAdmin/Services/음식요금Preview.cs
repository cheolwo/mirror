using Ssalddel.Contracts.Admin.Restaurants;

namespace SsalddelAdmin.Services;

/// <summary>관리자 화면의 기본·거리비 견적 표시만 계산한다. 실제 제안 산정 정책은 서버가 소유한다.</summary>
internal static class 음식요금Preview
{
    public static 음식요금PreviewResult Calculate(음식배달요금정책응답 policy, int distanceMeters)
    {
        var billableDistance = Math.Max(0, distanceMeters - Math.Max(0, policy.IncludedDistanceMeters));
        var unit = Math.Max(1, policy.DistanceUnitMeters);
        var units = (int)Math.Ceiling(billableDistance / (double)unit);
        var platformFee = Math.Max(policy.MinimumFee, policy.BaseFee + units * policy.DistanceUnitFee);
        var driverPayout = Math.Max(policy.DriverMinimumPayout, policy.DriverBasePayout + units * policy.DriverDistanceUnitPayout);
        return new(platformFee, driverPayout);
    }
}

internal readonly record struct 음식요금PreviewResult(decimal PlatformFee, decimal DriverPayout)
{
    public decimal Margin => PlatformFee - DriverPayout;
}
