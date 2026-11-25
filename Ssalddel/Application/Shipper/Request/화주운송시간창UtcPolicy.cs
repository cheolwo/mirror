namespace Ssalddel.Application.Shipper.Request;

/// <summary>화주 시간창의 같은 순간을 검증·원장·관계형 저장 전에 UTC로 보존합니다.</summary>
internal static class 화주운송시간창UtcPolicy
{
    internal static 의뢰생성Command 정규화(의뢰생성Command request)
        => request with
        {
            픽업시간창시작일시 = 시각(request.픽업시간창시작일시),
            픽업시간창종료일시 = 시각(request.픽업시간창종료일시),
            하차시간창시작일시 = 시각(request.하차시간창시작일시),
            하차시간창종료일시 = 시각(request.하차시간창종료일시)
        };

    internal static 의뢰수정Command 정규화(의뢰수정Command request)
        => request with { 픽업지 = 위치(request.픽업지), 하차지 = 위치(request.하차지) };

    private static 위치정보입력값 위치(위치정보입력값 value)
        => value with
        {
            시간창시작일시 = 시각(value.시간창시작일시),
            시간창종료일시 = 시각(value.시간창종료일시)
        };

    private static DateTime? 시각(DateTime? value) => value.HasValue ? 시각(value.Value) : null;

    private static DateTime 시각(DateTime value)
        // Kind 없는 기존 입력은 UTC ticks입니다. 기본 날짜와 누락값은 실제 약속으로 만들지 않습니다.
        => value == default ? value : value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
}
