using Ssalddel.Contracts.Admin.Dashboard;

namespace Ssalddel.Application.Admin.Dashboard;

public sealed class 관리자대시보드요약조회QueryHandler : IRequestHandler<관리자대시보드요약조회Query, 관리자대시보드요약응답>
{
    private readonly SsalddelContext _db;

    public 관리자대시보드요약조회QueryHandler(SsalddelContext db)
    {
        _db = db;
    }

    public async Task<관리자대시보드요약응답> Handle(관리자대시보드요약조회Query request, CancellationToken cancellationToken)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        // EF Core DbContext는 하나의 인스턴스에서 동시 쿼리를 지원하지 않는다.
        // 관리자 요약은 같은 요청 범위의 읽기 사본이므로 집계를 순차 수행한다.
        var 오늘의뢰수 = await _db.화주운송의뢰.CountAsync(
            x => x.CreatedAt >= todayStart && x.CreatedAt < tomorrowStart,
            cancellationToken);
        var 결제대기수 = await _db.화주운송의뢰.CountAsync(
            x => x.결제상태 == 상태값.결제상태.결제대기,
            cancellationToken);
        var 결제완료수 = await _db.화주운송의뢰.CountAsync(
            x => x.결제상태 == 상태값.결제상태.결제완료,
            cancellationToken);

        var 배차대기수 = await _db.운송원장.CountAsync(
            x => x.상태 == 상태값.배차대기상태.대기,
            cancellationToken);
        var 배차확정수 = await _db.운송원장.CountAsync(
            x => x.상태 == 상태값.배차대기상태.확정,
            cancellationToken);

        var 운송중수 = await _db.운송원장.CountAsync(
            x => x.상태 == "운송중",
            cancellationToken);
        var 완료수 = await _db.운송원장.CountAsync(
            x => x.상태 == "완료",
            cancellationToken);
        var 운송예외수 = await _db.운송원장.CountAsync(
            x => x.첨부_json.Contains("transport-field-exception"),
            cancellationToken);
        var 관리자확인필요수 = await _db.운송원장.CountAsync(
            x => x.첨부_json.Contains("adminReviewRequired\":true"),
            cancellationToken);

        var 취소수 = await _db.화주운송의뢰.CountAsync(
            x => x.상태 == "취소",
            cancellationToken);
        var 환불수 = await _db.결제.CountAsync(
            x => x.결제상태 == 상태값.결제상태.환불됨,
            cancellationToken);

        return new 관리자대시보드요약응답
        {
            오늘의뢰수 = 오늘의뢰수,
            결제대기수 = 결제대기수,
            결제완료수 = 결제완료수,
            배차대기수 = 배차대기수,
            배차확정수 = 배차확정수,
            운송중수 = 운송중수,
            완료수 = 완료수,
            취소환불수 = 취소수 + 환불수,
            운송예외수 = 운송예외수,
            관리자확인필요수 = 관리자확인필요수
        };
    }
}
