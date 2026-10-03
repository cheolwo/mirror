using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public sealed record 배차경로좌표(decimal Latitude, decimal Longitude);

    public sealed record 배차경로예상결과(
        decimal? DistanceKm,
        TimeSpan? Duration,
        decimal? TollFare,
        string 계산방식 = "좌표근사",
        bool 실제경로여부 = false) { public string? RouteOption { get; init; } }

    public sealed record 배차삽입경로예상결과(
        decimal? 기존경로거리Km,
        decimal? 삽입경로거리Km,
        decimal? 기존경로소요시간분,
        decimal? 삽입경로소요시간분,
        decimal? 기존배송지연분,
        decimal? 삽입추가톨비);

    public sealed record 배차추천판정결과(
        string 추천유형,
        decimal 허용지연분,
        bool 화물민감여부,
        bool 단독배송여부,
        bool 묶음삽입가능,
        bool 도착후추천가능,
        bool 차량적합여부,
        string[] 차량부적합사유,
        string[] 차량경고);

    public sealed record 배차추천평가결과(
        decimal? 추천점수,
        string[] 배지,
        string[] 경고,
        string 추천사유,
        string? 복귀추천사유);

    public sealed record 복귀지결정결과(
        string? 주소,
        배차경로좌표? 좌표,
        string 출처,
        bool 복귀지기준사용됨);

    public sealed record 기사운송일정항목(
        string 의뢰Id,
        string 단계유형,
        string 주소,
        배차경로좌표? 좌표,
        DateTime? 기준시각,
        DateTime? 시간창종료일시,
        int 순서,
        long? 운송Id,
        bool 진행중운송여부,
        bool 후보의뢰여부);

    public sealed record 기사운송일정계획(
        string 기사Id,
        DateTime 기준시각,
        배차경로좌표? 시작좌표,
        IReadOnlyList<기사운송일정항목> 항목목록);

    public sealed record 운송일정도착예상항목(
        string 의뢰Id,
        string 단계유형,
        int 순서,
        string 주소,
        DateTime? 예상도착시각,
        DateTime? 시간창종료일시,
        bool 시간위반여부,
        decimal? 시간위반분);

    public sealed record 운송삽입시도결과(
        int 삽입인덱스,
        string 시도유형,
        bool 경로변경시도여부,
        bool 전체완수가능여부,
        decimal? 총소요시간분,
        decimal? 총거리Km,
        decimal? 총추가지연분,
        decimal? 단순이어가기대비절감분,
        decimal? 최대시간위반분,
        string[] 위반사유,
        IReadOnlyList<string> 경로순서,
        IReadOnlyList<운송일정도착예상항목> 도착예상목록);

    public sealed record 운송삽입평가결과(
        bool 삽입가능여부,
        bool 전체완수가능여부,
        int? 최적삽입인덱스,
        bool 경로변경이점여부,
        decimal? 경로변경절감분,
        decimal? 총소요시간분,
        decimal? 총거리Km,
        decimal? 총추가지연분,
        decimal? 최대시간위반분,
        string[] 위반사유,
        IReadOnlyList<string> 권장경로순서,
        IReadOnlyList<운송일정도착예상항목> 도착예상목록,
        IReadOnlyList<운송삽입시도결과> 시도목록);
}
