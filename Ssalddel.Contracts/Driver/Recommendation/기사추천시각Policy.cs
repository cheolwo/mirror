namespace Ssalddel.Contracts.Driver.Recommendation;

/// <summary>기사 추천 원장의 UTC 시각을 응답 경계에서 보존합니다. 유효 시간이나 만료 판정 규칙을 바꾸지 않습니다.</summary>
public static class 기사추천시각Policy
{
    public static DateTime? Utc로정규화(DateTime? value)
    {
        if (value is not { } instant) return null;
        return instant.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(instant, DateTimeKind.Utc),
            DateTimeKind.Local => instant.ToUniversalTime(),
            _ => instant
        };
    }
}
