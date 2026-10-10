using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 의뢰접수요청조회QueryHandler(
    SsalddelContext db,
    ICurrentUserAccessor currentUser,
    ISender sender) : IRequestHandler<의뢰접수요청조회Query, 화주운송의뢰응답?>
{
    public async Task<화주운송의뢰응답?> Handle(의뢰접수요청조회Query request, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var key = request.ClientRequestId?.Trim();
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(key) || key.Length > 200)
            return null;

        // 관리자·업무 위임 여부로 다른 사람의 접수 번호를 탐색하지 않습니다.
        var id = await db.화주운송의뢰.AsNoTracking()
            .Where(x => x.클라이언트요청Id == key
                && (x.주문자UserId == owner || (x.주문자UserId == string.Empty && x.화주Id == owner)))
            .Select(x => x.의뢰Id)
            .FirstOrDefaultAsync(cancellationToken);
        return id is null ? null : await sender.Send(new 의뢰단건조회Query(id), cancellationToken);
    }
}
