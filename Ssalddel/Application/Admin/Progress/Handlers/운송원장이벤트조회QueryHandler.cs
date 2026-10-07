using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Services.Community;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Admin.Progress;

public sealed class 운송원장이벤트조회QueryHandler : IRequestHandler<운송원장이벤트조회Query, Result<운송원장이벤트응답>>
{
    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I운송원장Mongo동기화Service _원장동기화Service;

    public 운송원장이벤트조회QueryHandler(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor,
        I운송원장Mongo동기화Service 원장동기화Service)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _원장동기화Service = 원장동기화Service;
    }

    public async Task<Result<운송원장이벤트응답>> Handle(운송원장이벤트조회Query request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            return Result.Fail<운송원장이벤트응답>("RequestId is required");
        }

        var requestId = request.RequestId.Trim();
        var shipperRequest = await _db.화주운송의뢰
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        var transport = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.운송번호 == requestId, cancellationToken);

        if (shipperRequest is null && transport is null)
        {
            return Result.Fail<운송원장이벤트응답>("운송 원장을 찾을 수 없습니다.");
        }

        if (!CanReadLedger(shipperRequest, transport))
        {
            return Result.Fail<운송원장이벤트응답>("운송 원장 조회 권한이 없습니다.");
        }

        var eventQuery = _db.운송이벤트
            .AsNoTracking()
            .Where(x => x.의뢰Id == requestId);
        if (request.SinceUtc.HasValue)
        {
            eventQuery = eventQuery.Where(x => x.이벤트시각 > request.SinceUtc.Value);
        }

        var events = await eventQuery
            .OrderByDescending(x => x.이벤트시각)
            .Take(100)
            .Select(x => new 운송원장이벤트항목응답
            {
                Id = x.Id,
                이벤트타입 = x.이벤트타입,
                이벤트시각 = x.이벤트시각,
                메타데이터 = x.메타데이터
            })
            .ToListAsync(cancellationToken);
        var 동기화상태 = await _원장동기화Service.상태조회Async(requestId, cancellationToken);

        return Result.Ok(new 운송원장이벤트응답
        {
            의뢰Id = requestId,
            의뢰상태 = shipperRequest?.상태 ?? string.Empty,
            결제상태 = shipperRequest?.결제상태 ?? string.Empty,
            배차상태 = shipperRequest?.배차상태 ?? transport?.상태 ?? string.Empty,
            정산상태 = shipperRequest?.정산상태 ?? string.Empty,
            의뢰UpdatedAt = shipperRequest?.UpdatedAt,
            운송Id = transport?.Id,
            운송상태 = transport?.상태 ?? string.Empty,
            운송UpdatedAt = transport?.UpdatedAt,
            Mongo원장Id = 동기화상태.원장Id,
            Mongo원장존재 = 동기화상태.Mongo원장존재,
            Mongo원장상태 = 동기화상태.Mongo원장상태 ?? string.Empty,
            Mongo원장현재단계Key = 동기화상태.현재단계Key ?? string.Empty,
            Mongo원장대상OsCode = 동기화상태.대상OsCode ?? string.Empty,
            Mongo원장UpdatedAtUtc = 동기화상태.Mongo원장UpdatedAtUtc,
            Mongo원장블록수 = 동기화상태.Mongo원장블록수,
            Rdb운송실행투영존재 = 동기화상태.Rdb운송실행투영존재,
            원장동기화메시지 = 동기화상태.메시지,
            마지막변경시각 = ResolveLatestChangedAt(shipperRequest, transport, events),
            이벤트목록 = events
        });
    }

    private bool CanReadLedger(화주운송의뢰? shipperRequest, 운송원장? transport)
    {
        var role = _currentUserAccessor.Role;
        var userId = _currentUserAccessor.UserId;

        if (string.Equals(role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        if (string.Equals(role, 역할명.화주, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, 역할명.판매자, StringComparison.OrdinalIgnoreCase))
        {
            return shipperRequest is not null
                   && (string.Equals(shipperRequest.화주Id, userId, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(shipperRequest.주문자UserId, userId, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(role, 역할명.기사, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, 역할명.용달기사, StringComparison.OrdinalIgnoreCase))
        {
            return transport is not null
                   // 음식 배달은 전용 API에서 상태·열람 기한을 검사합니다. 감사 메타데이터의 고객 좌표를 우회 공개하지 않습니다.
                   && transport.배차업무유형 != 상태값.배차업무유형.음식배달
                   && string.Equals(transport.기사_운송자, userId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static DateTime ResolveLatestChangedAt(
        화주운송의뢰? shipperRequest,
        운송원장? transport,
        IReadOnlyList<운송원장이벤트항목응답> events)
    {
        var latest = DateTime.MinValue;
        if (shipperRequest is not null)
        {
            latest = Max(latest, shipperRequest.UpdatedAt);
        }

        if (transport is not null)
        {
            latest = Max(latest, transport.UpdatedAt);
        }

        foreach (var item in events)
        {
            latest = Max(latest, item.이벤트시각);
        }

        return latest == DateTime.MinValue ? DateTime.UtcNow : latest;
    }

    private static DateTime Max(DateTime left, DateTime right)
        => left >= right ? left : right;
}
