using 살뜰.Services.Dispatch.Recommendation;

namespace 살뜰.Services.Dispatch.Coordination;

/// <summary>검토용 계획 요약이다. 고객 주소·좌표·연락처를 복제하지 않는다.</summary>
public sealed record 국내화물계획경유점(
    string 의뢰Id, string 단계유형, int 순서, DateTime? 도착시각Utc, DateTime? 출발시각Utc);

public sealed record 국내화물검증수행계획
{
    public string 계획Id { get; init; } = "";
    public string 기사Id { get; init; } = "";
    public IReadOnlyList<string> 의뢰Ids { get; init; } = [];
    public bool 확인됨 { get; init; }
    public bool 가능여부 { get; init; }
    public string 사유Code { get; init; } = "CargoPlan.NotVerified";
    public string? 입력지문 { get; init; }
    public IReadOnlyList<국내화물계획경유점> 경로순서 { get; init; } = [];
    public 화물구간적재검증결과? 구간검증결과 { get; init; }
    public decimal? 총소요시간분 { get; init; }
    public decimal? 총거리Km { get; init; }
    public decimal? 합의기사보수합계 { get; init; }
    public IReadOnlyDictionary<string, 화물상하차작업시간> 상하차작업시간 { get; init; }
        = new Dictionary<string, 화물상하차작업시간>();
    public string 탐색범위 { get; init; } = "기존 제안 순서에서 연속·분리 삽입, 전체 최적성 미확인";
}

public interface I국내화물배차계획검증Service
{
    Task<국내화물배차조율결과> 검증Async(국내화물배차조율입력 input,
        국내화물배차조율결과 result, CancellationToken ct = default);
}
