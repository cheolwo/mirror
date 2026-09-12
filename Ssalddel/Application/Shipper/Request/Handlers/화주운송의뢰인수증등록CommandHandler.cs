using FluentResults;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Operations;
using ShipRequest = Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 화주운송의뢰인수증등록CommandHandler : IRequestHandler<화주운송의뢰인수증등록Command, Result<ShipRequest.화주운송의뢰응답>>
{
    private readonly SsalddelContext _db;
    private readonly I화물운송완료화주인수인계Service _completionHandoffService;
    private readonly I화주운송업무담당자UseCase _operatorUseCase;

    public 화주운송의뢰인수증등록CommandHandler(
        SsalddelContext db,
        I화물운송완료화주인수인계Service completionHandoffService,
        I화주운송업무담당자UseCase operatorUseCase)
    {
        _db = db;
        _completionHandoffService = completionHandoffService;
        _operatorUseCase = operatorUseCase;
    }

    public async Task<Result<ShipRequest.화주운송의뢰응답>> Handle(화주운송의뢰인수증등록Command request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("RequestId is required");
        }

        if (string.IsNullOrWhiteSpace(request.인수증번호))
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("인수증번호 is required");
        }

        var entity = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (entity == null)
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("의뢰를 찾을 수 없습니다.");
        }

        if (!await _operatorUseCase.권한보유Async(
                entity,
                운송업무권한Codes.인수확인,
                cancellationToken))
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("의뢰를 찾을 수 없습니다.");
        }

        if (entity.정산상태 != ShipRequest.운임정산상태.후불승인완료.ToString() &&
            entity.정산상태 != ShipRequest.운임정산상태.인수증대기.ToString())
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("인수증 등록은 후불 승인 이후에만 가능합니다.");
        }

        var completionHandoff = await _completionHandoffService.화주인수Async(
            entity.의뢰Id,
            cancellationToken);

        entity.인수증번호 = request.인수증번호.Trim();
        entity.인수증등록일시 = DateTime.UtcNow;
        entity.정산상태 = ShipRequest.운임정산상태.인수증등록완료.ToString();
        entity.정산메모 = MergeMemo(entity.정산메모, request.등록메모);
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok(화주운송의뢰매퍼.To응답(
            entity,
            completionHandoff: completionHandoff));
    }

    private static string MergeMemo(string? origin, string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo))
        {
            return origin ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(origin))
        {
            return memo.Trim();
        }

        return $"{origin.Trim()} | {memo.Trim()}";
    }
}
