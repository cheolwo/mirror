namespace 살뜰.Services.Dispatch.Queue;

/// <summary>
/// 실제 화물 후보 순회에서 확인한 값이다. 반경·원거리 병합 전에 제외된 기사를 읽었다고 표시하지 않는다.
/// 기사 식별자는 내부 관찰 연결용이며 로컬 학습 출력과 모델 입력에서는 익명 토큰으로 치환한다.
/// </summary>
public sealed record 용달운송후보검토결과(
    배차추천후보? 선정후보,
    IReadOnlyList<용달운송후보검토항목> 후보목록,
    IReadOnlyList<string> 최종허용기사Ids,
    string? 중단사유Code);

/// <summary>탈락은 실제 도달한 최초 관문만 기록한다. null 수치는 미조회이며 0이 아니다.</summary>
public sealed record 용달운송후보검토항목(
    string 기사Id,
    string 판정Code,
    decimal? 거리Km,
    decimal? 기본추천점수,
    decimal? 대기점수,
    decimal? 추천점수,
    string? 추천사유)
{
    public string? 픽업기준Code { get; init; }
    public DateTime? 기존수행완료예상시각Utc { get; init; }
    public DateTime? 픽업도착예상시각Utc { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public 배차추천후보? 서버후보 { get; init; }
}

/// <summary>직접 지정 추천도 같은 적격 검사를 적용하지만 자동 추천의 순위와 일치할 필요는 없다.</summary>
public interface I용달운송대상후보Service
{
    Task<배차추천후보?> 대상후보조회Async(살뜰.도메인.운송.운송원장 queue, string driverId, CancellationToken ct = default);
}

/// <summary>선택과 같은 1회 계산의 메모리 관찰용이다. 상태 변경과 외부 호출은 맡기지 않는다.</summary>
public interface I용달운송후보검토Observer
{
    void OnReviewed(string requestId, 용달운송후보검토결과 trace);
}
