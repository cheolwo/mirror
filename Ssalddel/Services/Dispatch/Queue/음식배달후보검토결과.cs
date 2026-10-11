namespace 살뜰.Services.Dispatch.Queue;

/// <summary>실제 후보 선정에서 읽은 공간과 기사 판정을 보존한다. 배정이나 원장 상태를 변경하지 않는다.</summary>
public sealed record 음식배달후보검토결과(
    배차추천후보? 선정후보,
    string 주배달권키,
    IReadOnlyList<string> 조회배달권키목록,
    IReadOnlyList<음식배달후보검토항목> 후보목록,
    IReadOnlyList<string> 최종허용기사Ids,
    bool 수요자료최신,
    bool 수요예외사용,
    bool 픽업적시성예외사용,
    string? 중단사유Code);

/// <summary>판정Code가 명시하는 관문까지만 확인했다. null 수치는 미조회·미확인이며 0이 아니다.</summary>
public sealed record 음식배달후보검토항목(
    string 기사Id,
    int 공간단계,
    string 공간설명,
    string 판정Code,
    decimal? 거리Km,
    int? 픽업단계,
    decimal? 도착차이분,
    decimal? 추천점수,
    decimal? 대기점수,
    int? 수요우선순위,
    string? 수요사유Code)
{
    public string? 위치기준Code { get; init; }
    public DateTime? 기존수행완료예상시각Utc { get; init; }
    public decimal? 픽업예상소요분 { get; init; }
    public string? 경로근거Code { get; init; }
}
