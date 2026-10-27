using 살뜰.Services.Dispatch.Recommendation;

namespace 살뜰.Services.Dispatch.Coordination;

public sealed record 국내화물배차조율입력요청
{
    public IReadOnlyList<string>? 의뢰Ids { get; init; }

    public IReadOnlyList<string>? 기사Ids { get; init; }

    public int 최대운송의뢰수 { get; init; } = 30;

    public int 최대기사수 { get; init; } = 100;

    // 기본은 한 제안의 응답을 기다린 뒤 다음 제안을 준비한다. 명시한 다건 설정은 보존한다.
    public int 기사당최대추천건수 { get; init; } = 1;

    public decimal? 목표기사건당지급액 { get; init; }

    public decimal? 기사목표지급액미달패널티배수 { get; init; }

    public decimal? 기사목표지급액초과패널티배수 { get; init; }
    public IReadOnlyDictionary<string, 화물상하차작업시간>? 상하차작업시간 { get; init; }
}

public sealed record 국내화물배차조율입력(
    DateTime 기준시각Utc,
    int 기사당최대추천건수,
    IReadOnlyList<운송의뢰조율입력> 운송의뢰목록,
    IReadOnlyList<기사후보조율입력> 기사후보목록,
    IReadOnlyList<운송의뢰기사조합평가> 조합평가목록,
    IReadOnlyList<운송의뢰수익묶음후보>? 수익묶음후보목록 = null,
    국내화물기사배정AI정책? 기사배정AI정책 = null)
{
    public 국내화물배차판정근거? 판정근거 { get; init; }
    public IReadOnlyDictionary<string, 화물상하차작업시간>? 상하차작업시간 { get; init; }
}

public sealed record 운송의뢰조율입력(
    long 배차대기Id,
    string 의뢰Id,
    string 원본의뢰유형,
    string 화물종류,
    string 화물온도조건,
    decimal? 화물중량Kg,
    decimal? 최종운임,
    string 배달권키,
    string 배달권명,
    배차경로좌표? 상차좌표,
    배차경로좌표? 하차좌표,
    DateTime? 상차시간창시작Utc,
    DateTime? 상차시간창종료Utc,
    DateTime? 하차시간창시작Utc,
    DateTime? 하차시간창종료Utc,
    int 추천라운드,
    DateTime 생성시각Utc);

public sealed record 기사후보조율입력(
    string 기사Id,
    string 차량종류,
    string 운행상태,
    int 현재수락운송건수,
    string 배달권키,
    string 배달권명,
    배차경로좌표? 현재좌표,
    decimal Aging점수,
    DateTime Aging기준시각Utc,
    decimal? 상차접근허용반경Km,
    DateTime? 최근위치수신시각Utc);

public sealed record 운송의뢰기사조합평가(
    string 의뢰Id,
    string 기사Id,
    bool 추천가능여부,
    decimal? 상차지거리Km,
    decimal? 상차지이동시간분,
    decimal? 화물운송시간분,
    decimal? 총예상시간분,
    decimal? 총예상거리Km,
    decimal? 예상톨비,
    decimal? 예상운임,
    decimal? 예상총비용,
    decimal? 예상순이익,
    bool 일정삽입가능여부,
    bool 전체일정완수가능여부,
    int? 최적삽입인덱스,
    bool 경로변경이점여부,
    decimal? 경로변경절감분,
    decimal? 총추가지연분,
    bool 동일배달권여부,
    bool 인접배달권여부,
    decimal? 하차후복귀거리Km,
    decimal 복귀시간대부담점수,
    bool 퇴근시간대복귀부담여부,
    decimal 추천점수,
    string 추천사유,
    IReadOnlyList<string> 배지,
    IReadOnlyList<string> 경고,
    IReadOnlyList<string> 제외사유)
{
    public 국내화물배차판정근거? 판정근거 { get; init; }
}

public sealed record 국내화물배차조율결과(
    DateTime 기준시각Utc,
    IReadOnlyList<국내화물배차제안> 추천배정목록,
    IReadOnlyList<국내화물배차제외> 제외목록,
    IReadOnlyList<국내화물배차보류> 보류목록,
    decimal? 전체예상비용,
    decimal? 전체예상운임,
    decimal? 전체예상순이익,
    string 적용알고리즘 = "균형_연속경로우선",
    decimal 가용기사운송의뢰비율 = 0m)
{
    public 국내화물배차판정근거? 판정근거 { get; init; }
    public IReadOnlyList<국내화물검증수행계획> 검증계획목록 { get; init; } = [];
}

public sealed record 국내화물배차제안(
    int 순번,
    string 의뢰Id,
    string 기사Id,
    int 기사별추천순번,
    decimal 추천점수,
    decimal? 예상총비용,
    decimal? 예상운임,
    decimal? 예상순이익,
    string 추천사유,
    IReadOnlyList<string> 배지)
{
    public 국내화물배차판정근거? 판정근거 { get; init; }
    public string? 수행계획Id { get; init; }
}

/// <summary>순차 관측의 범위와 원장 지문이다. SQL·위치 저장소의 원자적 관측이나 실측 확인을 뜻하지 않는다.</summary>
public sealed record 국내화물배차판정근거
{
    public DateTime 기준시각Utc { get; init; }
    public DateTime 조회시작시각Utc { get; init; }
    public DateTime 조회완료시각Utc { get; init; }
    public string 조회범위 { get; init; } = "Unknown";
    public bool 원자적조회여부 { get; init; }
    public string? 입력지문 { get; init; }
    public string? 기사기본지문 { get; init; }
    public string? 실차제원지문 { get; init; }
    public string? 지문판본 { get; init; }
    public string? 기사고정조건지문 { get; init; }
    public string? 현재수행지문 { get; init; }
    public string? 위치관측지문 { get; init; }
    public DateTime? 위치기록시각Utc { get; init; }
    public DateTime? 위치수신시각Utc { get; init; }
    // 대기 순위의 관측 자료는 안전 조건의 재확인 지문과 분리한다. 추천 기록 자체가 이 값을 바꿀 수 있다.
    public DateTime? 대기순위기준시각Utc { get; init; }
    public decimal? 대기순위점수 { get; init; }
    public string? 순위관측지문 { get; init; }
    public IReadOnlyList<string> 미확인사유 { get; init; } = [];
    public IReadOnlyList<string> 차단사유 { get; init; } = [];
    public IReadOnlyDictionary<string, string> 의뢰근거 { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> 기사근거 { get; init; } = new Dictionary<string, string>();
}

public sealed record 국내화물추천저장근거
{
    public 국내화물배차판정근거? 판정근거 { get; init; }
    public 국내화물검증수행계획? 수행계획 { get; init; }
    public string? 조율입력지문 { get; init; }
    public string? 재검증상태Code { get; init; }
    public DateTime? 재검증시각Utc { get; init; }
}

public sealed record 국내화물배차제외(
    string 의뢰Id,
    string 기사Id,
    IReadOnlyList<string> 제외사유);

public sealed record 국내화물배차보류(
    string 의뢰Id,
    string 사유);

public sealed record 국내화물배차조율적용결과(
    DateTime 기준시각Utc,
    IReadOnlyList<국내화물배차추천잠금> 잠금목록,
    IReadOnlyList<국내화물배차추천잠금실패> 실패목록)
{
    public int 잠금건수 => 잠금목록.Count;

    public int 실패건수 => 실패목록.Count;
}

public sealed record 국내화물배차추천잠금(
    string 의뢰Id,
    string 기사Id,
    int 추천라운드,
    DateTime 추천시작시각Utc,
    DateTime 추천만료시각Utc);

public sealed record 국내화물배차추천잠금실패(
    string 의뢰Id,
    string 기사Id,
    string 사유);
