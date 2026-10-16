namespace DriverApp.Models.Driver.Samples;

/// <summary>휴대폰 자기북 방향을 진북 방향으로 보정하고 짧은 기간 확인된 값만 표시합니다.</summary>
public sealed class 기사위치방향State
{
    public static readonly TimeSpan MaximumHeadingAge = TimeSpan.FromSeconds(10);
    private const double SmoothingWeight = 0.2d;
    private double? _bearing;
    private DateTime? _observedAtUtc;

    public static double? NormalizeDegrees(double degrees)
        => double.IsFinite(degrees) ? (degrees % 360d + 360d) % 360d : null;

    public bool Observe(double magneticHeadingDegrees, double declinationDegrees, DateTime observedAtUtc)
    {
        if (_observedAtUtc is { } previous && observedAtUtc < previous) return false;
        if (!double.IsFinite(magneticHeadingDegrees) || !double.IsFinite(declinationDegrees)
            || observedAtUtc.Kind != DateTimeKind.Utc)
        {
            Clear();
            return false;
        }

        var corrected = NormalizeDegrees(magneticHeadingDegrees + declinationDegrees);
        if (!corrected.HasValue) { Clear(); return false; }
        var previousBearing = Bearing(observedAtUtc);
        // 359도와 1도 사이를 반대편 180도로 평균하지 않습니다.
        var shortestChange = previousBearing.HasValue
            ? (corrected.Value - previousBearing.Value + 540d) % 360d - 180d : 0d;
        _bearing = previousBearing.HasValue
            ? NormalizeDegrees(previousBearing.Value + shortestChange * SmoothingWeight) : corrected;
        _observedAtUtc = observedAtUtc;
        return true;
    }

    public double? Bearing(DateTime nowUtc)
        => nowUtc.Kind == DateTimeKind.Utc && _observedAtUtc is { } observedAt
            && nowUtc >= observedAt && nowUtc - observedAt <= MaximumHeadingAge ? _bearing : null;

    public void Clear() { _bearing = null; _observedAtUtc = null; }
}
