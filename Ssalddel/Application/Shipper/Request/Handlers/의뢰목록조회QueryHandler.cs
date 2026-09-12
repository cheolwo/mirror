using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Application.CommandProcessing;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 의뢰목록조회QueryHandler : IRequestHandler<의뢰목록조회Query, IReadOnlyList<화주운송의뢰응답>>
{
    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I화주운송업무담당자UseCase _operatorUseCase;

    public 의뢰목록조회QueryHandler(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor,
        I화주운송업무담당자UseCase operatorUseCase)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _operatorUseCase = operatorUseCase;
    }

    public async Task<IReadOnlyList<화주운송의뢰응답>> Handle(의뢰목록조회Query request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 || request.PageSize > 200 ? 50 : request.PageSize;
        var currentUserId = _currentUserAccessor.UserId;
        var isServerAdmin = 주문자권한검사.IsServerAdmin(_currentUserAccessor);

        if (!isServerAdmin && string.IsNullOrWhiteSpace(currentUserId))
        {
            return [];
        }

        var query = _db.화주운송의뢰.AsNoTracking().AsQueryable();

        if (isServerAdmin)
        {
            if (!string.IsNullOrWhiteSpace(request.ShipperId))
            {
                query = query.Where(r => r.화주Id == request.ShipperId);
            }
        }
        else
        {
            var delegatedRequestIds = (await _operatorUseCase
                    .조회가능운송의뢰IdsAsync(cancellationToken))
                .ToList();
            query = query.Where(r => r.화주Id == currentUserId
                                     || (r.화주Id == string.Empty && r.주문자UserId == currentUserId)
                                     || delegatedRequestIds.Contains(r.의뢰Id));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(r => r.상태 == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.PaymentStatus))
        {
            query = query.Where(r => r.결제상태 == request.PaymentStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.DispatchStatus))
        {
            query = query.Where(r => r.배차상태 == request.DispatchStatus);
        }

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var executionByRequestId = await 화주운송실행정보조회.조회Async(
            _db,
            items.Select(x => x.의뢰Id).ToList(),
            cancellationToken);

        return items.Select(item =>
        {
            executionByRequestId.TryGetValue(item.의뢰Id, out var execution);
            return 화주운송의뢰매퍼.To응답(
                item,
                execution?.운송원장,
                execution?.기사,
                execution?.최근위치,
                execution?.운영체제인계,
                execution?.운송완료화주인계,
                execution?.비정상운송사건목록);
        }).ToList();
    }
}
