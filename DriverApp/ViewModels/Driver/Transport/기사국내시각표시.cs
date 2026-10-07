using System.Globalization;

namespace DriverApp.ViewModels.Driver.Transport;

public static class 기사국내시각표시
{
    public static DateTimeOffset 한국시간(DateTime value)
    {
        // SQL의 Kind 없는 값도 기존 서버 계약대로 UTC로 해석한다.
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(9));
    }

    public static string 표시(DateTime? value, string format = "MM-dd HH:mm", string fallback = "시각 미확인")
        => value.HasValue
            ? $"{한국시간(value.Value).ToString(format, CultureInfo.InvariantCulture)} (한국시간)"
            : fallback;
}
