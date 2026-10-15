using FluentResults;
using Ssalddel.Application.CommandProcessing;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 의뢰삭제CommandHandler : IRequestHandler<의뢰삭제Command, Result<Unit>>
{
    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public 의뢰삭제CommandHandler(SsalddelContext db, ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<Result<Unit>> Handle(의뢰삭제Command request, CancellationToken cancellationToken)
    {
        var entity = await _db.화주운송의뢰.FirstOrDefaultAsync(r => r.의뢰Id == request.RequestId, cancellationToken);
        if (entity == null)
        {
            return Result.Fail<Unit>("의뢰를 찾을 수 없습니다.");
        }

        if (!주문자권한검사.IsServerAdmin(_currentUserAccessor)
            && !주문자권한검사.IsOwner(entity, _currentUserAccessor.UserId))
        {
            return Result.Fail<Unit>("의뢰를 찾을 수 없습니다.");
        }

        // 서버 의뢰는 Mongo 정본 접수 뒤의 SQL 투영이다. '생성됨'도 미제출 폼 초안이 아니다.
        // 물리 삭제하면 운송·창고·수납·완료 인계의 참조가 끊기므로 기존 취소/운영 검토로 안내한다.
        return Result.Fail<Unit>(new Error("이미 접수된 운송 의뢰는 삭제할 수 없습니다. 의뢰 상세에서 취소 또는 운영자 검토를 요청해 주세요.")
            .WithMetadata("ErrorCode", "CargoRequestCancellationRequired")
            .WithMetadata("StatusCode", Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict));
    }
}
