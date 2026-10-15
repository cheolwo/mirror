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

        var receiptStages = new[] { ShipRequest.운임정산상태.후불승인완료.ToString(), ShipRequest.운임정산상태.인수증대기.ToString(),
            ShipRequest.운임정산상태.인수증등록완료.ToString(), ShipRequest.운임정산상태.청구대기.ToString(),
            ShipRequest.운임정산상태.입금대기.ToString(), ShipRequest.운임정산상태.입금확인완료.ToString(), ShipRequest.운임정산상태.정산완료.ToString() };
        if (!receiptStages.Contains(entity.정산상태, StringComparer.Ordinal))
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("인수증 등록은 후불 승인 이후에만 가능합니다.");
        }

        var completedTransportExists = await _db.운송원장.AsNoTracking().AnyAsync(x =>
            (x.의뢰Id == entity.의뢰Id || (x.의뢰Id == string.Empty && x.운송번호 == entity.의뢰Id))
            && x.상태 == 살뜰.도메인.공통.상태값.배차상태.인수완료 && x.도착 != null, cancellationToken);
        if (!completedTransportExists)
            return Result.Fail<ShipRequest.화주운송의뢰응답>(new Error("기사의 운송 완료를 확인한 뒤 인수증을 등록해 주세요.")
                .WithMetadata("ErrorCode", "CargoCompletionRequired").WithMetadata("StatusCode", 409));

        var completionHandoff = await _completionHandoffService.화주인수Async(
            entity.의뢰Id,
            cancellationToken);

        if (completionHandoff is null || completionHandoff.상태Code != 운영체제업무인계상태Codes.수락됨)
            return Result.Fail<ShipRequest.화주운송의뢰응답>(new Error("운송 완료 인계를 아직 확인하지 못했습니다. 같은 의뢰를 다시 조회해 주세요.")
                .WithMetadata("ErrorCode", "CargoCompletionHandoffPending").WithMetadata("StatusCode", 409));

        if (entity.인수증등록일시 is not null)
            return entity.인수증번호 == request.인수증번호.Trim()
                ? Result.Ok(화주운송의뢰매퍼.To응답(entity, completionHandoff: completionHandoff))
                : Result.Fail<ShipRequest.화주운송의뢰응답>(new Error("이미 등록한 인수증이 있습니다. 기존 기록을 확인해 주세요.")
                    .WithMetadata("ErrorCode", "CargoReceiptAlreadyRegistered").WithMetadata("StatusCode", 409));

        entity.인수증번호 = request.인수증번호.Trim();
        entity.인수증등록일시 = DateTime.UtcNow;
        // 인수 확인이 이미 진행한 청구·수납·정산 상태를 되돌리지 않는다.
        if (entity.정산상태 == ShipRequest.운임정산상태.후불승인완료.ToString()
            || entity.정산상태 == ShipRequest.운임정산상태.인수증대기.ToString())
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
