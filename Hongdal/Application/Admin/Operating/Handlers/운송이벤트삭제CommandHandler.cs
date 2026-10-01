namespace Hongdal.Application.Admin.Operating;

public sealed class 운송이벤트삭제CommandHandler : IRequestHandler<운송이벤트삭제Command, FluentResults.Result<Unit>>
{
    private readonly HongdalContext _db;

    public 운송이벤트삭제CommandHandler(HongdalContext db)
    {
        _db = db;
    }

    public async Task<FluentResults.Result<Unit>> Handle(운송이벤트삭제Command request, CancellationToken cancellationToken)
    {
        var entity = await _db.운송이벤트.FindAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return FluentResults.Result.Fail<Unit>("운송이벤트를 찾을 수 없습니다.");
        }

        if (string.Equals(
                entity.이벤트타입,
                홍달.도메인.운송.운송이벤트유형.배차엔진판단감사,
                StringComparison.Ordinal))
        {
            return FluentResults.Result.Fail<Unit>(
                "배차 엔진 판단 감사 이벤트는 삭제할 수 없습니다.");
        }

        _db.운송이벤트.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return FluentResults.Result.Ok(Unit.Value);
    }
}
