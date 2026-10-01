using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.도메인.운송;
using 홍달.도메인.창고;

namespace Hongdal.Services.LogisticsProcessing.Warehouse;

public interface IWarehousePerspectiveReadService
{
    Task<Result<입고요청페이지응답>> QueryExpectedInboundsAsync(
        string perspectiveCode,
        string? communityLedgerId,
        입고요청목록조회요청 request,
        CancellationToken cancellationToken);

    Task<Result<출고예정페이지응답>> QueryExpectedOutboundsAsync(
        string perspectiveCode,
        string? communityLedgerId,
        출고예정목록조회요청 request,
        CancellationToken cancellationToken);
}

/// <summary>
/// 역할 이름이 아니라 현재 사용자와 주문·판매·창고·운송·공동 원장의 실제 관계를 기준으로
/// 입고/출고 예정 읽기 범위를 제한합니다.
/// </summary>
public sealed class WarehousePerspectiveReadService(
    HongdalContext db,
    ICurrentUserAccessor currentUserAccessor,
    I커뮤니티원장저장소 communityLedgerStore) : IWarehousePerspectiveReadService
{
    public async Task<Result<입고요청페이지응답>> QueryExpectedInboundsAsync(
        string perspectiveCode,
        string? communityLedgerId,
        입고요청목록조회요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Failure<입고요청페이지응답>("로그인 사용자를 확인할 수 없습니다.", StatusCodes.Status401Unauthorized);
        }

        var normalizedPerspective = perspectiveCode?.Trim();
        var normalizedLedgerId = string.IsNullOrWhiteSpace(communityLedgerId) ? null : communityLedgerId.Trim();
        if (string.Equals(normalizedPerspective, 창고업무관점코드.공동원장, StringComparison.OrdinalIgnoreCase))
        {
            var ledgerAccess = await CheckLedgerAccessAsync(normalizedLedgerId, userId, cancellationToken);
            if (ledgerAccess.IsFailed)
            {
                return Result.Fail<입고요청페이지응답>(ledgerAccess.Errors);
            }
        }

        var query = db.입고요청
            .AsNoTracking()
            .Where(item => item.상태 == 입고상태코드.예정);
        query = ApplyInboundPerspective(query, normalizedPerspective, normalizedLedgerId, userId);
        if (query is null)
        {
            return Failure<입고요청페이지응답>("지원하지 않는 입고 예정 관점입니다.", StatusCodes.Status400BadRequest);
        }

        query = ApplyInboundFilters(query, request);
        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var totalCount = await query.CountAsync(cancellationToken);
        var sorted = SortInbounds(query, request);
        var entities = await sorted
            .Skip(Skip(page, pageSize))
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var expectedOutbounds = await LoadExpectedOutboundsAsync(entities, cancellationToken);

        return Result.Ok(new 입고요청페이지응답
        {
            Items = entities
                .Select(entity => ToInboundResponse(entity, expectedOutbounds.GetValueOrDefault(entity.Id)))
                .ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<출고예정페이지응답>> QueryExpectedOutboundsAsync(
        string perspectiveCode,
        string? communityLedgerId,
        출고예정목록조회요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Failure<출고예정페이지응답>("로그인 사용자를 확인할 수 없습니다.", StatusCodes.Status401Unauthorized);
        }

        var normalizedPerspective = perspectiveCode?.Trim();
        var normalizedLedgerId = string.IsNullOrWhiteSpace(communityLedgerId) ? null : communityLedgerId.Trim();
        if (string.Equals(normalizedPerspective, 창고업무관점코드.공동원장, StringComparison.OrdinalIgnoreCase))
        {
            var ledgerAccess = await CheckLedgerAccessAsync(normalizedLedgerId, userId, cancellationToken);
            if (ledgerAccess.IsFailed)
            {
                return Result.Fail<출고예정페이지응답>(ledgerAccess.Errors);
            }
        }

        var query = db.출고예정
            .AsNoTracking()
            .Where(item => item.상태 == 출고상태코드.예정);
        query = ApplyOutboundPerspective(query, normalizedPerspective, normalizedLedgerId, userId);
        if (query is null)
        {
            return Failure<출고예정페이지응답>("지원하지 않는 출고 예정 관점입니다.", StatusCodes.Status400BadRequest);
        }

        query = ApplyOutboundFilters(query, request);
        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await SortOutbounds(query, request)
            .Skip(Skip(page, pageSize))
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var warehouses = await LoadWarehousesAsync(entities, cancellationToken);
        var transports = await LoadTransportsAsync(entities, cancellationToken);

        return Result.Ok(new 출고예정페이지응답
        {
            Items = entities.Select(entity => ToOutboundResponse(
                    entity,
                    warehouses.GetValueOrDefault(entity.출고창고Id),
                    FindTransport(transports, entity.운송의뢰Id)))
                .ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private IQueryable<입고요청>? ApplyInboundPerspective(
        IQueryable<입고요청> query,
        string? perspectiveCode,
        string? ledgerId,
        string userId)
        => perspectiveCode?.ToLowerInvariant() switch
        {
            창고업무관점코드.주문자 => query.Where(item => item.주문자UserId == userId),
            창고업무관점코드.판매자 => query.Where(item => item.판매자UserId == userId),
            창고업무관점코드.창고관리자 => query.Where(item =>
                db.창고.Any(warehouse =>
                    warehouse.Id == item.창고Id && warehouse.소유자UserId == userId)
                || db.창고사용자.Any(warehouseUser =>
                    warehouseUser.창고Id == item.창고Id && warehouseUser.UserId == userId)),
            창고업무관점코드.운송담당자 => query.Where(item =>
                item.운송의뢰Id != null
                && db.운송원장.Any(transport =>
                    (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
                    && (transport.화주Id == userId
                        || transport.현재추천대상기사Id == userId
                        || transport.확정기사Id == userId))),
            창고업무관점코드.공동원장 => query.Where(item => item.커뮤니티원장Id == ledgerId),
            _ => null
        };

    private IQueryable<출고예정>? ApplyOutboundPerspective(
        IQueryable<출고예정> query,
        string? perspectiveCode,
        string? ledgerId,
        string userId)
        => perspectiveCode?.ToLowerInvariant() switch
        {
            창고업무관점코드.주문자 => query.Where(item => item.주문자UserId == userId),
            창고업무관점코드.판매자 => query.Where(item => item.판매자UserId == userId),
            창고업무관점코드.창고관리자 => query.Where(item =>
                db.창고.Any(warehouse =>
                    warehouse.Id == item.출고창고Id && warehouse.소유자UserId == userId)
                || db.창고사용자.Any(warehouseUser =>
                    warehouseUser.창고Id == item.출고창고Id && warehouseUser.UserId == userId)),
            창고업무관점코드.운송담당자 => query.Where(item =>
                item.운송의뢰Id != null
                && db.운송원장.Any(transport =>
                    (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
                    && (transport.화주Id == userId
                        || transport.현재추천대상기사Id == userId
                        || transport.확정기사Id == userId))),
            창고업무관점코드.공동원장 => query.Where(item => item.커뮤니티원장Id == ledgerId),
            _ => null
        };

    private static IQueryable<입고요청> ApplyInboundFilters(
        IQueryable<입고요청> query,
        입고요청목록조회요청 request)
    {
        if (request.WarehouseId is > 0)
        {
            query = query.Where(item => item.창고Id == request.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.FlowType))
        {
            var flowType = 입고흐름유형코드.Normalize(request.FlowType);
            query = query.Where(item => item.입고흐름유형 == flowType);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var idMatched = long.TryParse(search, out var inboundId);
            query = query.Where(item =>
                (idMatched && item.Id == inboundId)
                || item.공급처코드.Contains(search)
                || item.공급처명.Contains(search)
                || item.원주문참조번호.Contains(search)
                || item.주문참조번호.Contains(search));
        }

        return query;
    }

    private static IQueryable<출고예정> ApplyOutboundFilters(
        IQueryable<출고예정> query,
        출고예정목록조회요청 request)
    {
        if (request.WarehouseId is > 0)
        {
            query = query.Where(item => item.출고창고Id == request.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var idMatched = long.TryParse(search, out var outboundId);
            query = query.Where(item =>
                (idMatched && item.Id == outboundId)
                || item.주문참조번호.Contains(search)
                || item.상품명.Contains(search)
                || item.SKU.Contains(search));
        }

        return query;
    }

    private static IOrderedQueryable<입고요청> SortInbounds(
        IQueryable<입고요청> query,
        입고요청목록조회요청 request)
    {
        var descending = request.SortDescending;
        return request.SortBy?.Trim() switch
        {
            nameof(입고요청항목응답.Id) => descending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id),
            nameof(입고요청항목응답.창고Id) => descending ? query.OrderByDescending(x => x.창고Id).ThenByDescending(x => x.Id) : query.OrderBy(x => x.창고Id).ThenBy(x => x.Id),
            nameof(입고요청항목응답.공급처명) => descending ? query.OrderByDescending(x => x.공급처명).ThenByDescending(x => x.Id) : query.OrderBy(x => x.공급처명).ThenBy(x => x.Id),
            nameof(입고요청항목응답.예정도착일) => descending ? query.OrderByDescending(x => x.예정도착일).ThenByDescending(x => x.Id) : query.OrderBy(x => x.예정도착일).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };
    }

    private static IOrderedQueryable<출고예정> SortOutbounds(
        IQueryable<출고예정> query,
        출고예정목록조회요청 request)
    {
        var descending = request.SortDescending;
        return request.SortBy?.Trim() switch
        {
            nameof(출고예정항목응답.Id) => descending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id),
            nameof(출고예정항목응답.출고창고Id) => descending ? query.OrderByDescending(x => x.출고창고Id).ThenByDescending(x => x.Id) : query.OrderBy(x => x.출고창고Id).ThenBy(x => x.Id),
            nameof(출고예정항목응답.주문참조번호) => descending ? query.OrderByDescending(x => x.주문참조번호).ThenByDescending(x => x.Id) : query.OrderBy(x => x.주문참조번호).ThenBy(x => x.Id),
            nameof(출고예정항목응답.상품명) => descending ? query.OrderByDescending(x => x.상품명).ThenByDescending(x => x.Id) : query.OrderBy(x => x.상품명).ThenBy(x => x.Id),
            nameof(출고예정항목응답.생성일시) => descending ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };
    }

    private async Task<Result> CheckLedgerAccessAsync(
        string? ledgerId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ledgerId))
        {
            return Failure("공동 원장 ID가 필요합니다.", StatusCodes.Status400BadRequest);
        }

        var ledger = await communityLedgerStore.원장조회Async(ledgerId.Trim(), cancellationToken);
        if (ledger is null)
        {
            return Failure("공동 원장을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        var hasAccess = string.Equals(ledger.생성자UserId, userId, StringComparison.OrdinalIgnoreCase)
                        || ledger.참여자목록.Any(participant => string.Equals(
                            participant.UserId,
                            userId,
                            StringComparison.OrdinalIgnoreCase));
        return hasAccess
            ? Result.Ok()
            : Failure("현재 사용자는 이 공동 원장의 참여자가 아닙니다.", StatusCodes.Status403Forbidden);
    }

    private async Task<IReadOnlyDictionary<long, 출고예정>> LoadExpectedOutboundsAsync(
        IReadOnlyCollection<입고요청> inbounds,
        CancellationToken cancellationToken)
    {
        var ids = inbounds.Select(item => item.Id).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, 출고예정>();
        }

        var items = await db.출고예정
            .AsNoTracking()
            .Where(item => item.입고요청Id.HasValue && ids.Contains(item.입고요청Id.Value))
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return items.GroupBy(item => item.입고요청Id!.Value)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<IReadOnlyDictionary<long, 창고>> LoadWarehousesAsync(
        IReadOnlyCollection<출고예정> outbounds,
        CancellationToken cancellationToken)
    {
        var ids = outbounds.Select(item => item.출고창고Id).Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<long, 창고>()
            : await db.창고.AsNoTracking()
                .Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, 운송원장>> LoadTransportsAsync(
        IReadOnlyCollection<출고예정> outbounds,
        CancellationToken cancellationToken)
    {
        var requestIds = outbounds.Select(item => item.운송의뢰Id)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requestIds.Count == 0)
        {
            return new Dictionary<string, 운송원장>(StringComparer.OrdinalIgnoreCase);
        }

        var items = await db.운송원장.AsNoTracking()
            .Where(item => requestIds.Contains(item.의뢰Id) || requestIds.Contains(item.원본의뢰Id))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArrayAsync(cancellationToken);
        var result = new Dictionary<string, 운송원장>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.의뢰Id))
            {
                result.TryAdd(item.의뢰Id, item);
            }

            if (!string.IsNullOrWhiteSpace(item.원본의뢰Id))
            {
                result.TryAdd(item.원본의뢰Id, item);
            }
        }

        return result;
    }

    private static 운송원장? FindTransport(
        IReadOnlyDictionary<string, 운송원장> transports,
        string? requestId)
        => string.IsNullOrWhiteSpace(requestId) ? null : transports.GetValueOrDefault(requestId);

    private static 입고요청항목응답 ToInboundResponse(입고요청 entity, 출고예정? expectedOutbound)
        => new()
        {
            Id = entity.Id,
            창고Id = entity.창고Id,
            커뮤니티원장Id = entity.커뮤니티원장Id,
            커뮤니티원장템플릿Key = entity.커뮤니티원장템플릿Key,
            커뮤니티원장상태 = entity.커뮤니티원장상태,
            입고흐름유형 = entity.입고흐름유형,
            입고생성경로 = entity.입고생성경로,
            계약선행여부 = entity.계약선행여부,
            자동생성여부 = entity.자동생성여부,
            주문Id = entity.주문Id,
            주문참조번호 = entity.주문참조번호,
            주문자UserId = entity.주문자UserId,
            판매자UserId = entity.판매자UserId,
            출고예정Id = entity.출고예정Id,
            운송의뢰Id = entity.운송의뢰Id,
            공급처코드 = entity.공급처코드,
            공급처명 = entity.공급처명,
            예정상품명 = expectedOutbound?.상품명 ?? string.Empty,
            예정SKU = expectedOutbound?.SKU ?? string.Empty,
            예정수량 = expectedOutbound?.수량,
            원주문참조번호 = entity.원주문참조번호,
            상태 = entity.상태,
            예정도착일 = entity.예정도착일,
            입고완료일시 = entity.입고완료일시,
            계약정보 = new 입고계약스냅샷
            {
                계약번호 = entity.계약번호,
                계약유형 = entity.계약유형,
                계약상대방명 = entity.계약상대방명,
                정산방식 = entity.정산방식,
                판매수수료율 = entity.판매수수료율,
                보관료일단가 = entity.보관료일단가,
                통관필요여부 = entity.통관필요여부,
                계약시작일 = entity.계약시작일,
                계약종료일 = entity.계약종료일,
                계약메모 = entity.계약메모
            }.Normalize()
        };

    private static 출고예정항목응답 ToOutboundResponse(
        출고예정 entity,
        창고? warehouse,
        운송원장? transport)
        => new()
        {
            Id = entity.Id,
            주문Id = entity.주문Id,
            주문참조번호 = entity.주문참조번호,
            판매상품Id = entity.판매상품Id,
            입고상품Id = entity.입고상품Id,
            판매자UserId = entity.판매자UserId,
            주문자UserId = entity.주문자UserId,
            출고창고Id = entity.출고창고Id,
            출고창고명 = warehouse?.창고명 ?? string.Empty,
            출고창고주소 = warehouse?.주소 ?? string.Empty,
            출고묶음Id = entity.출고묶음Id,
            상품명 = entity.상품명,
            SKU = entity.SKU,
            수량 = entity.수량,
            상태 = entity.상태,
            운송의뢰Id = entity.운송의뢰Id,
            입고요청Id = entity.입고요청Id,
            예정출고일 = transport?.출발_픽업,
            예정도착일 = transport?.도착,
            출고처리일시 = entity.출고처리일시,
            커뮤니티원장Id = entity.커뮤니티원장Id,
            커뮤니티원장템플릿Key = entity.커뮤니티원장템플릿Key,
            커뮤니티원장상태 = entity.커뮤니티원장상태,
            생성일시 = entity.CreatedAt
        };

    private string? CurrentUserId()
    {
        var userId = currentUserAccessor.UserId?.Trim();
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }

    private static int Skip(int page, int pageSize)
        => (int)Math.Min((long)page * pageSize, int.MaxValue);

    private static Result Failure(string message, int statusCode)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", statusCode));

    private static Result<T> Failure<T>(string message, int statusCode)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", statusCode));
}
