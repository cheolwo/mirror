using Hongdal.Contracts.Admin.Progress;
using 홍달.도메인.운송;

namespace Hongdal.Application.Admin.Operating;

public sealed class 운송이벤트생성CommandHandler : IRequestHandler<운송이벤트생성Command, 운송이벤트로그응답>
{
    private readonly HongdalContext _db;

    public 운송이벤트생성CommandHandler(HongdalContext db)
    {
        _db = db;
    }

    public async Task<운송이벤트로그응답> Handle(운송이벤트생성Command request, CancellationToken cancellationToken)
    {
        if (string.Equals(
                request.이벤트타입,
                운송이벤트유형.배차엔진판단감사,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "배차 엔진 판단 감사 이벤트는 배차 실행 경로에서만 생성할 수 있습니다.");
        }

        var entity = new 운송이벤트
        {
            의뢰Id = request.의뢰Id,
            이벤트타입 = request.이벤트타입,
            이벤트시각 = request.이벤트시각 == default ? DateTime.UtcNow : request.이벤트시각,
            메타데이터 = request.메타데이터 ?? string.Empty
        };

        await _db.운송이벤트.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new 운송이벤트로그응답
        {
            Id = entity.Id,
            의뢰Id = entity.의뢰Id,
            이벤트타입 = entity.이벤트타입,
            이벤트시각 = entity.이벤트시각,
            메타데이터 = entity.메타데이터
        };
    }
}
