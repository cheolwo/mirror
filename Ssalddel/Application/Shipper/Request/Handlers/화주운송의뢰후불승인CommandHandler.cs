using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Community;
using 살뜰.Services.Dispatch.Queue;
using ShipRequest = Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 화주운송의뢰후불승인CommandHandler : IRequestHandler<화주운송의뢰후불승인Command, Result<ShipRequest.화주운송의뢰응답>>
{
    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I운송의뢰배차대기Service _dispatchQueueService;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;
    private readonly I화주운송업무담당자UseCase _operatorUseCase;

    public 화주운송의뢰후불승인CommandHandler(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor,
        I운송의뢰배차대기Service dispatchQueueService,
        I운송원장Mongo동기화Service transportLedgerSync,
        I화주운송업무담당자UseCase operatorUseCase)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _dispatchQueueService = dispatchQueueService;
        _transportLedgerSync = transportLedgerSync;
        _operatorUseCase = operatorUseCase;
    }

    public async Task<Result<ShipRequest.화주운송의뢰응답>> Handle(화주운송의뢰후불승인Command request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("RequestId is required");
        }

        var entity = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (entity == null)
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("의뢰를 찾을 수 없습니다.");
        }

        if (!await _operatorUseCase.권한보유Async(
                entity,
                운송업무권한Codes.정산확인,
                cancellationToken))
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("의뢰를 찾을 수 없습니다.");
        }

        var hasOpenAbnormalTransportIncident = await _db.비정상운송사건
            .AsNoTracking()
            .AnyAsync(
                x => x.운송의뢰Id == entity.의뢰Id
                     && (x.정산보류적용여부
                         || x.상태Code == 비정상운송사건상태Codes.운영검토대기),
                cancellationToken);
        if (hasOpenAbnormalTransportIncident)
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>(
                "비정상 운송 사건의 운영 검토 중에는 후불 승인을 변경할 수 없습니다.");
        }

        if (!Enum.TryParse<ShipRequest.정산시점>(entity.정산시점, ignoreCase: false, out var settlementTime) ||
            (settlementTime != ShipRequest.정산시점.운송완료후정산 && settlementTime != ShipRequest.정산시점.월말정산))
        {
            return Result.Fail<ShipRequest.화주운송의뢰응답>("후불 승인은 운송완료후정산 또는 월말정산 의뢰만 가능합니다.");
        }

        entity.정산상태 = ShipRequest.운임정산상태.후불승인완료.ToString();
        entity.정산메모 = MergeMemo(entity.정산메모, request.승인메모);
        entity.배차상태 = 상태값.배차상태.매칭중;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dispatchQueueService.생성또는조회Async(
            화주운송의뢰출고예정정규화.To출고예정운송대상(entity),
            new 운송의뢰배차대기생성옵션
            {
                픽업상세주소 = entity.픽업_상세주소,
                하차상세주소 = entity.하차_상세주소
            },
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _transportLedgerSync.화주운송의뢰동기화Async(entity, _currentUserAccessor.UserId ?? entity.화주Id, cancellationToken);

        return Result.Ok(화주운송의뢰매퍼.To응답(entity));
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
