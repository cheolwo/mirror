using System.Globalization;

namespace Ssalddel.Contracts.Common.Transport;

/// <summary>원장 값을 바꾸지 않는 화물 조건 표시입니다. 누락 조건을 협의나 기본 운송 방식으로 추정하지 않습니다.</summary>
public static class 화물운송조건표시
{
    private static readonly CultureInfo 한국표시 = CultureInfo.GetCultureInfo("ko-KR");

    public static string 운송방식(string? value)
        => value?.Trim() is { Length: > 0 } text ? text == "단독" ? "독차" : text : "미확인";

    /// <summary>제안된 방식을 수락하는 버튼 이름입니다. 운송 방식 선택이나 상차 완료 명령이 아닙니다.</summary>
    public static string 수락버튼(string? value)
        => 운송방식(value) is "독차" or "혼적" ? 운송방식(value) : "수락";

    public static string 차량종류(string? value)
        => string.IsNullOrWhiteSpace(value) ? "미확인" : value.Trim();

    public static string 중량(decimal? value)
        => value is > 0 ? value.Value.ToString("#,0.###", 한국표시) + " kg" : "미확인";

    public static string 수량(int? value)
        => value is > 0 ? value.Value.ToString("N0", 한국표시) + "개" : "미확인";

    /// <summary>기존 원장의 UTC 시각을 한국 시각으로 표시합니다. Kind 없는 기존 UTC 값도 UTC로 해석합니다.</summary>
    public static string 시간창(DateTime? start, DateTime? end)
        => 시간창표시(start, end, "yyyy-MM-dd HH:mm", true);

    /// <summary>추천 요약용 한국 시각입니다. 연도는 펼친 상세의 시간창 표시에서 확인합니다.</summary>
    public static string 간략시간창(DateTime? start, DateTime? end)
        => 시간창표시(start, end, "MM/dd HH:mm", false);

    private static string 시간창표시(DateTime? start, DateTime? end, string dateFormat, bool includeZone)
    {
        var 시작 = 한국시각(start);
        var 종료 = 한국시각(end);
        if (시작 is null && 종료 is null) return "미확인";
        var 종료형식 = 시작 is { } from && 종료 is { } to && from.Date == to.Date ? "HH:mm" : dateFormat;
        var 범위 = (시작?.ToString(dateFormat, CultureInfo.InvariantCulture) ?? "시작 미확인")
            + " ~ " + (종료?.ToString(종료형식, CultureInfo.InvariantCulture) ?? "종료 미확인")
            + (includeZone ? " (한국)" : string.Empty);
        return 시작 is { } first && 종료 is { } last && first > last ? "시간 범위 확인 필요 · " + 범위 : 범위;
    }

    private static DateTimeOffset? 한국시각(DateTime? value)
    {
        if (value is null || value.Value == default) return null;
        try
        {
            var utc = value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime()
                : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
            return new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(9));
        }
        catch (ArgumentException) { return null; }
    }
}
