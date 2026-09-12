using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 의뢰단건조회QueryHandler : IRequestHandler<의뢰단건조회Query, 화주운송의뢰응답?>
{
    private readonly SsalddelContext _db;
    private readonly I화주운송업무담당자UseCase _operatorUseCase;

    public 의뢰단건조회QueryHandler(
        SsalddelContext db,
        I화주운송업무담당자UseCase operatorUseCase)
    {
        _db = db;
        _operatorUseCase = operatorUseCase;
    }

    public async Task<화주운송의뢰응답?> Handle(의뢰단건조회Query request, CancellationToken cancellationToken)
    {
        var entity = await _db.화주운송의뢰
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.의뢰Id == request.RequestId, cancellationToken);

        if (entity == null)
        {
            return null;
        }

        if (!await _operatorUseCase.권한보유Async(
                entity,
                Ssalddel.Contracts.Common.Operations.운송업무권한Codes.진행조회,
                cancellationToken))
        {
            return null;
        }

        var executionByRequestId = await 화주운송실행정보조회.조회Async(
            _db,
            [entity.의뢰Id],
            cancellationToken);
        executionByRequestId.TryGetValue(entity.의뢰Id, out var execution);

        return 화주운송의뢰매퍼.To응답(
            entity,
            execution?.운송원장,
            execution?.기사,
            execution?.최근위치,
            execution?.운영체제인계,
            execution?.운송완료화주인계,
            execution?.비정상운송사건목록);
    }
}
